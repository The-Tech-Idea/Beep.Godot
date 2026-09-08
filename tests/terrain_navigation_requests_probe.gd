extends SceneTree

const BASE := "res://addons/beep_game_builder_cs/ecs/grid/"
var failures: Array[String] = []
var results := {}

func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		push_error(message)

func _initialize() -> void: run.call_deferred()

func make(script: String, host: Node, node_name: String) -> Node:
	var node: Node = load(BASE + script + ".cs").new()
	node.name = node_name
	host.add_child(node)
	return node

func complete(id: int, path: Array[Vector2i], reason: String) -> void:
	check(not results.has(id), "Request completed more than once")
	results[id] = {"path": path, "reason": reason}

func drain(nav: Node) -> void:
	for frame in 100000:
		nav.ProcessPathRequests()
		check(nav.PathExpansionsLastFrame <= nav.PathExpansionsPerFrame, "Path work exceeded frame budget")
		check(nav.ActivePathSearchCount <= nav.MaximumActiveSearches, "Search working sets exceeded active cap")
		if nav.PendingPathRequestCount == 0: return
	check(false, "Path request queue did not finish: %s pending, %s active" % [nav.PendingPathRequestCount, nav.ActivePathSearchCount])

func run() -> void:
	var host := Node2D.new()
	root.add_child(host)
	current_scene = host
	var cells := make("GridCellDataComponent", host, "Cells")
	var placement := make("GridPlacementComponent", host, "Placement")
	var roads := make("GridRoadComponent", host, "Roads")
	roads.DrawRoads = false
	var nav := make("GridNavigationComponent", host, "Navigation")
	nav.CellDataPath = NodePath("../Cells")
	nav.PlacementPath = NodePath("../Placement")
	nav.RoadPath = NodePath("../Roads")
	nav.BoundsSize = Vector2i(128, 128)
	nav.MaxVisitedCells = 20000
	nav.PathExpansionsPerFrame = 256
	nav.MaximumActiveSearches = 4
	nav.connect("PathRequestCompleted", complete)
	for x in range(16, 120, 16):
		for y in 128:
			if absi(y - (x * 3) % 128) > 3: nav.SetBlocked(Vector2i(x, y), true)
	for y in range(10, 25):
		for x in range(2, 14): cells.SetTerrainKind(Vector2i(x, y), "mud")
	for y in 128: roads.SetRoad(Vector2i(1, y), true)
	var expected := {}
	for i in 200:
		var start := Vector2i(i % 12, (i * 7) % 128)
		var goal := Vector2i(116 + i % 12, (i * 11) % 128)
		var path: Array = nav.FindCellPath(start, goal)
		check(not path.is_empty(), "Synchronous reference could not cross fixture")
		var id: int = nav.RequestCellPath(start, goal)
		check(id > 0, "Valid batch request rejected")
		expected[id] = path
	check(nav.ActivePathSearchCount == 0 and results.is_empty(), "Enqueue performed a synchronous search")
	drain(nav)
	for id in expected:
		check(results.has(id) and results[id].reason == "" and results[id].path == expected[id], "Budgeted route differs from synchronous A*")
	results.clear()
	nav.ClearBlocked()
	nav.PathExpansionsPerFrame = 16
	nav.MaximumActiveSearches = 1
	var mutations: Array[Callable] = [
		func(): cells.SetTerrainKind(Vector2i(70, 70), "deep_water"),
		func(): cells.SetMetadata(Vector2i(71, 71), "terrain_relief", 1),
		func(): placement.SetOccupied(Vector2i(72, 72), true),
		func(): roads.SetRoad(Vector2i(73, 73), true),
		func(): nav.SetBlocked(Vector2i(74, 74), true),
		func(): nav.Diagonals = 0,
		func(): nav.TerrainCostMultipliers["grass"] = 1.3,
		func(): nav.BlockedTerrainKinds[0] = "custom_obstacle",
		func(): nav.CellDataPath = NodePath("../Missing")
	]
	for mutate in mutations:
		var id: int = nav.RequestCellPath(Vector2i.ZERO, Vector2i(127, 127))
		nav.ProcessPathRequests()
		check(not results.has(id), "Long search completed before mutation")
		mutate.call()
		drain(nav)
		check(results.has(id) and results[id].reason == "navigation_changed" and results[id].path.is_empty(), "Changed world delivered a stale path")
	var first: int = nav.RequestCellPath(Vector2i.ZERO, Vector2i(127, 127))
	var second: int = nav.RequestCellPath(Vector2i.ZERO, Vector2i(127, 126))
	nav.ProcessPathRequests()
	check(nav.CancelPathRequest(first) and nav.CancelPathRequest(second), "Could not cancel active and queued requests")
	drain(nav)
	check(not results.has(first) and not results.has(second), "Cancelled request emitted completion")
	nav.MaximumPendingRequests = 1
	var pending: int = nav.RequestCellPath(Vector2i.ZERO, Vector2i(127, 127))
	check(pending > 0 and nav.RequestCellPath(Vector2i.ZERO, Vector2i.ONE) == 0, "Queue capacity ignored")
	nav.ClearPathRequests()
	check(nav.PendingPathRequestCount == 0 and nav.ActivePathSearchCount == 0, "Clear retained request state")
	var failed: int = nav.RequestCellPath(Vector2i.ZERO, Vector2i(-1, -1))
	drain(nav)
	check(results[failed].reason == "goal_blocked_or_out_of_bounds", "Invalid goal did not report failure")
	nav.MaximumPendingRequests = 16
	var replacement := [0]
	var short_id: int = nav.RequestCellPath(Vector2i.ZERO, Vector2i.ONE)
	var on_short := func(id: int, _path: Array[Vector2i], _reason: String):
		if id == short_id:
			nav.ClearPathRequests()
			replacement[0] = nav.RequestCellPath(Vector2i.ZERO, Vector2i(2, 2))
			nav.ProcessPathRequests() # Must not enter the scheduler recursively.
	nav.connect("PathRequestCompleted", on_short)
	drain(nav)
	check(replacement[0] > 0 and results.has(replacement[0]), "Completion reentrancy lost replacement request")
	nav.disconnect("PathRequestCompleted", on_short)
	var automatic: int = nav.RequestCellPath(Vector2i.ZERO, Vector2i(4, 4))
	for frame in 300:
		await process_frame
		if results.has(automatic): break
	check(results.has(automatic) and results[automatic].reason == "", "Normal Godot processing did not advance requests")
	nav.RequestCellPath(Vector2i.ZERO, Vector2i(127, 127))
	host.remove_child(nav)
	check(nav.PendingPathRequestCount == 0, "Detached navigation retained requests")
	nav.free()
	host.free()
	print("[terrain-navigation-requests] 200 routes and lifecycle checks OK" if failures.is_empty() else "[terrain-navigation-requests] FAILED")
	quit(0 if failures.is_empty() else 1)
