extends SceneTree

const GRID := "res://addons/beep_game_builder_cs/ecs/grid/"
var failures: Array[String] = []
var publications := 0
var results := {}

func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		push_error(message)

func make(type: String, host: Node, label: String, properties: Dictionary = {}) -> Node:
	var node: Node = load(GRID + type + ".cs").new()
	node.name = label
	for key in properties: node.set(key, properties[key])
	host.add_child(node)
	node.set_process(false)
	node.set_physics_process(false)
	return node

func _initialize() -> void: run.call_deferred()

func run() -> void:
	var host := Node2D.new()
	root.add_child(host)
	var cells := make("GridCellDataComponent", host, "Cells")
	make("GridProjectionComponent", host, "Grid", {"DrawGrid": false})
	var nav := make("GridNavigationComponent", host, "Navigation", {"CellDataPath": NodePath("../Cells"), "GridPath": NodePath("../Grid"), "BoundsSize": Vector2i(96, 64), "PathExpansionsPerFrame": 1, "TreatCellDataBlockedAsBlocked": false, "AllowBlockedGoal": true, "AllowBlockedStart": true})
	var placement := make("GridPlacementComponent", host, "Placement", {"CellDataPath": NodePath("../Cells"), "GridPath": NodePath("../Grid"), "NavigationPath": NodePath("../Navigation"), "TreatCellDataBlockedAsUnplaceable": false, "TreatBlockedTerrainKindsAsUnplaceable": false, "UseMouseInput": false})
	var archive := make("GridCellArchiveComponent", host, "Archive", {"CellDataPath": NodePath("../Cells")})
	cells.CellsChanged.connect(func(): publications += 1)
	nav.PathRequestCompleted.connect(func(id, path, reason): results[id] = [path, reason])
	cells.SetTerrainKind(Vector2i(33, 1), "water")
	cells.Till(Vector2i(34, 1))
	cells.PlantCrop(Vector2i(34, 1), "corn", 2, -1)
	var packet: Dictionary = cells.CaptureSingleChunkState(Vector2i(1, 0))
	var id: int = nav.RequestCellPath(Vector2i.ZERO, Vector2i(95, 63))
	nav.ProcessPathRequests()
	check(not results.has(id), "Pending-route fixture finished too early")
	publications = 0
	cells.SetChunkAvailable(Vector2i(1, 0), false)
	cells.SetChunkAvailable(Vector2i(1, 0), false)
	check(publications == 1 and cells.UnavailableChunkCount == 1, "Availability change was not idempotent")
	check(not cells.IsCellAvailable(Vector2i(33, 1)) and cells.IsCellAvailable(Vector2i(31, 1)), "Availability did not respect chunk bounds")
	check(nav.IsBlocked(Vector2i(33, 1)) and not nav.CanTraverse(Vector2i(31, 1), Vector2i(32, 1)), "Blocked overrides allowed entry into unavailable data")
	check(nav.FindCellPath(Vector2i.ZERO, Vector2i(33, 1)).is_empty(), "AllowBlockedGoal admitted an unavailable goal")
	check(nav.FindCellPath(Vector2i(33, 1), Vector2i.ZERO).is_empty(), "AllowBlockedStart admitted an unavailable start")
	check(is_inf(nav.TraversalCost(Vector2i(31, 1), Vector2i(32, 1))), "Unavailable terrain produced a finite traversal cost")
	check(not placement.CanPlace(Vector2i(35, 1)), "Placement bypassed unavailable cells")
	check(not archive.SaveChunk(Vector2i(1, 0)) and archive.LastError.contains("unavailable"), "Unavailable data could overwrite a valid archive")
	for frame in 100:
		nav.ProcessPathRequests()
		if results.has(id): break
	check(results.has(id) and results[id][1] == "navigation_changed", "Availability did not invalidate an active route")
	cells.AdvanceDay(2)
	check(cells.GetCropAgeDays(Vector2i(34, 1)) == 2 and cells.CellCount == 2 and cells.GetTerrainKind(Vector2i(33, 1)) == "water", "Readiness change discarded data or suspended resident crop simulation")
	cells.RestoreSingleChunkState(Vector2i(1, 0), packet)
	check(cells.IsCellAvailable(Vector2i(35, 1)) and placement.CanPlace(Vector2i(35, 1)), "Published chunk did not restore availability")
	# Even unrestricted diagonal movement cannot cut across an unavailable side chunk.
	nav.Diagonals = 2
	cells.SetChunkAvailable(Vector2i(0, 1), false)
	check(not nav.CanTraverse(Vector2i(31, 31), Vector2i(32, 32)), "Diagonal movement crossed an unavailable corner")
	cells.SetChunkAvailable(Vector2i(-1, -2), false)
	check(not cells.IsCellAvailable(Vector2i(-1, -33)) and cells.IsCellAvailable(Vector2i(0, -33)), "Negative chunk availability used truncation instead of floor division")
	cells.ClearCells()
	check(cells.UnavailableChunkCount == 0 and cells.IsCellAvailable(Vector2i(-1, -33)), "Clearing the world retained stale availability")
	cells.SetChunkAvailable(Vector2i.ZERO, false)
	cells.LoadCells([], true)
	check(cells.UnavailableChunkCount == 0, "Full empty cell publication retained unavailable chunks")
	var smoke: Node = load("res://tests/CoreGameplaySmoke.cs").new()
	host.add_child(smoke)
	check(smoke.RunChunkAvailabilitySaveCheck(), smoke.Failure)
	host.free()
	print("[terrain-chunk-availability] OK" if failures.is_empty() else "[terrain-chunk-availability] FAILED")
	quit(0 if failures.is_empty() else 1)
