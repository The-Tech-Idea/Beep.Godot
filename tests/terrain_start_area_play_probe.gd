extends SceneTree

# FEAT-09, played: the generated start areas answer for gameplay through GridStartAreaComponent,
# and every consumer asks it rather than reading start 0.
#   - OriginOf(k) is the generator's start k; an index past the last start is NoCell;
#   - SpawnCellsFor walks only the start's own area, skips blocked cells, and orders by
#     (distance², y, x);
#   - placement with RestrictBuildToStartArea refuses outside_start_area outside the active
#     start's area and allows inside; off, the same cell is allowed; unwired, not_ready;
#   - SpawnAtStartArea puts successive workers on successive spawn cells, never on one another;
#   - the overlay's start-area segments are exactly every area border side plus every
#     headquarters footprint's perimeter, and none with ShowStartAreas off.

const TERRAIN := "res://addons/beep_game_builder_cs/ecs/terrain/"
const GRID := "res://addons/beep_game_builder_cs/ecs/grid/"
const SIZE := Vector2i(48, 48)
const RADIUS := 10

var failures: Array[String] = []
var rejections: Array[String] = []

func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		push_error(message)

func make(path: String, label: String, values: Dictionary = {}) -> Node:
	var node: Node = load(path).new()
	node.name = label
	for key in values: node.set(key, values[key])
	root.add_child(node)
	return node

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	var cells := make(GRID + "GridCellDataComponent.cs", "Cells")
	var generator := make(TERRAIN + "TerrainGeneratorComponent.cs", "Generator", {
		"BoundsSize": SIZE, "Seed": 31415, "UseClimateBiomeMaps": true, "UseScaleRules": true,
		"CellDataPath": NodePath("../Cells"), "GenerateOnReady": false, "ClearExistingCells": true})
	generator.call("ApplyMapSetup", 0, 1, 1, 1, 1, 1)
	var kit: Resource = load(TERRAIN + "TerrainStartKit.cs").new()
	kit.set("MinAreaCells", 80)
	generator.set("StartKit", kit)
	generator.set("StartAreaRadius", RADIUS)
	generator.call("GenerateTerrain")
	var layers := make(TERRAIN + "TerrainDataLayersComponent.cs", "Layers", {
		"TerrainGeneratorPath": NodePath("../Generator"), "BoundsSize": SIZE, "RefreshOnReady": false})
	layers.call("Rebuild")
	var grid := make(GRID + "GridProjectionComponent.cs", "Grid", {"TileSize": Vector2(64, 64)})
	var navigation := make(GRID + "GridNavigationComponent.cs", "Navigation", {
		"GridPath": NodePath("../Grid"), "CellDataPath": NodePath("../Cells"), "BoundsSize": SIZE})
	var start_area := make(GRID + "GridStartAreaComponent.cs", "StartArea", {
		"CellDataPath": NodePath("../Cells"), "DataLayersPath": NodePath("../Layers"), "NavigationPath": NodePath("../Navigation")})
	await process_frame

	var starts: Array = generator.call("GetStartPositions")
	var reports: Array = generator.call("GetStartAreaReports")
	check(starts.size() >= 2, "the fixture map needs two starts (%d)" % starts.size())
	for k in starts.size():
		check(start_area.call("OriginOf", k) == starts[k], "OriginOf(%d) is %s, start %d is %s" % [k, start_area.call("OriginOf", k), k, starts[k]])
	check(start_area.call("OriginOf", starts.size()) == Vector2i(-2147483648, -2147483648), "OriginOf past the last start is NoCell")

	# The active start: the largest usable area, so its spawn and placement cells are plentiful.
	var active := -1
	for report in reports:
		if report["usable"] and (active < 0 or report["cell_count"] > reports[active]["cell_count"]):
			active = report["index"]
	check(active >= 0, "the fixture map holds a usable start")
	var other := 0 if active != 0 else 1
	start_area.set("LocalStartIndex", active)
	var origin: Vector2i = starts[active]

	# ── Spawn cells ──────────────────────────────────────────────────────────
	# Block the second-nearest area cell, so the walk must step over it.
	var unblocked: Array = start_area.call("SpawnCellsFor", active, 8)
	check(unblocked.size() == 8, "SpawnCellsFor(%d, 8) found %d cells" % [active, unblocked.size()])
	navigation.call("SetBlocked", unblocked[1], true)
	var spawn: Array = start_area.call("SpawnCellsFor", active, 6)
	check(spawn.size() == 6 and not spawn.has(unblocked[1]), "SpawnCellsFor skips a blocked cell (%s)" % [spawn])
	for i in spawn.size():
		check(int(generator.call("StartAreaAt", spawn[i])) == active + 1, "spawn cell %s is outside start %d's area" % [spawn[i], active])
		if i > 0:
			var previous: Vector2i = spawn[i - 1]
			var d_prev := (previous - origin).length_squared()
			var d_here := (Vector2i(spawn[i]) - origin).length_squared()
			check(d_prev < d_here or (d_prev == d_here and (previous.y < spawn[i].y or (previous.y == spawn[i].y and previous.x < spawn[i].x))),
				"spawn cells are not ordered by (distance², y, x) at %d: %s then %s" % [i, previous, spawn[i]])
	navigation.call("SetBlocked", unblocked[1], false)

	# ── Placement ────────────────────────────────────────────────────────────
	var objects := Node2D.new()
	objects.name = "Objects"
	root.add_child(objects)
	var placement := make(GRID + "GridPlacementComponent.cs", "Placement", {
		"GridPath": NodePath("../Grid"), "CellDataPath": NodePath("../Cells"), "NavigationPath": NodePath("../Navigation"),
		"PlacementRootPath": NodePath("../Objects"), "UseMouseInput": false, "KeepPlacingAfterConfirm": true,
		"MarkPlacedCellsOccupied": false, "MarkPlacedCellsBlockedInNavigation": false,
		"RestrictBuildToStartArea": true, "StartAreaPath": NodePath("../StartArea")})
	placement.connect("PlacementRejected", func(_id, _x, _y, reason): rejections.append(reason))
	var source := Node2D.new()
	var packed := PackedScene.new()
	packed.pack(source)
	source.free()
	placement.set("PlacementScene", packed)
	placement.call("BeginPlacement", "hut")
	check(reason_at(placement, starts[other]) == "outside_start_area", "another start's origin is refused as outside_start_area (%s)" % [rejections])
	check(reason_at(placement, origin) == "", "the active start's own origin is allowed (%s)" % [rejections])
	placement.set("RestrictBuildToStartArea", false)
	check(reason_at(placement, starts[other]) == "", "with the restriction off, the other start's origin is allowed (%s)" % [rejections])
	placement.set("RestrictBuildToStartArea", true)
	placement.set("StartAreaPath", NodePath("../MissingStartArea"))
	check(reason_at(placement, origin) == "not_ready", "an unwired start area is not_ready, never 'outside' (%s)" % [rejections])
	placement.set("StartAreaPath", NodePath("../StartArea"))
	start_area.set("CellDataPath", NodePath("../MissingCells"))
	check(reason_at(placement, origin) == "not_ready", "a start area without its cells is not_ready, never 'outside' (%s)" % [rejections])
	start_area.set("CellDataPath", NodePath("../Cells"))
	check(reason_at(placement, origin) == "", "rewiring the cells restores the answer (%s)" % [rejections])
	placement.call("CancelPlacement")
	for child in objects.get_children(): child.free()

	# ── Spawner ──────────────────────────────────────────────────────────────
	var units := Node2D.new()
	units.name = "Units"
	root.add_child(units)
	make(GRID + "GridJobQueueComponent.cs", "Jobs")
	var spawner := make(GRID + "GridWorkerSpawnerComponent.cs", "Spawner", {
		"GridPath": NodePath("../Grid"), "NavigationPath": NodePath("../Navigation"), "JobQueuePath": NodePath("../Jobs"),
		"CellDataPath": NodePath("../Cells"), "UnitsRootPath": NodePath("../Units"),
		"SpawnAtStartArea": true, "StartAreaPath": NodePath("../StartArea")})
	var spawned_cells: Array[Vector2i] = []
	spawner.connect("UnitSpawned", func(_unit, _id, x, y): spawned_cells.append(Vector2i(x, y)))
	for i in 3:
		check(spawner.call("SpawnWorker") != null, "SpawnAtStartArea worker %d was rejected" % i)
	var expected: Array = start_area.call("SpawnCellsFor", active, 3)
	check(spawned_cells.size() == 3 and spawned_cells[0] == expected[0] and spawned_cells[1] == expected[1] and spawned_cells[2] == expected[2],
		"workers took successive spawn cells %s, expected %s" % [spawned_cells, expected])

	# ── Overlay ──────────────────────────────────────────────────────────────
	var overlay := make(TERRAIN + "TerrainMapOverlayComponent.cs", "Overlay", {
		"TerrainGeneratorPath": NodePath("../Generator"), "BoundsSize": SIZE, "RefreshOnReady": false,
		"ShowUndergroundResources": false, "ShowStartAreas": true})
	overlay.call("Rebuild")
	var sides := 0
	for y in SIZE.y:
		for x in SIZE.x:
			var id: int = generator.call("StartAreaAt", Vector2i(x, y))
			if id == 0: continue
			for step in [Vector2i.LEFT, Vector2i.RIGHT, Vector2i.UP, Vector2i.DOWN]:
				var next: Vector2i = Vector2i(x, y) + step
				var next_id: int = generator.call("StartAreaAt", next) if Rect2i(Vector2i.ZERO, SIZE).has_point(next) else 0
				if next_id != id: sides += 1
	for report in reports:
		var footprint: Vector2i = report["footprint"]
		sides += 2 * (footprint.x + footprint.y)
	check(int(overlay.get("StartAreaSegmentCount")) == sides, "the overlay baked %d start-area segments, the map has %d border and headquarters sides" % [int(overlay.get("StartAreaSegmentCount")), sides])
	overlay.set("ShowStartAreas", false)
	overlay.call("Rebuild")
	check(int(overlay.get("StartAreaSegmentCount")) == 0, "ShowStartAreas off bakes no segments")

	print("[terrain-start-area-play] start %d of %d active, %d spawn cells, %d overlay sides" % [active, starts.size(), spawn.size(), sides])
	print("[terrain-start-area-play] OK" if failures.is_empty() else "[terrain-start-area-play] FAILED")
	quit(0 if failures.is_empty() else 1)

# Why a 1x1 build cannot stand at a cell - "" when it can - read from the rejection a confirm raises.
func reason_at(placement: Node, cell: Vector2i) -> String:
	rejections.clear()
	placement.call("MovePreviewToCell", cell)
	placement.call("ConfirmPlacement")
	return rejections[0] if not rejections.is_empty() else ""
