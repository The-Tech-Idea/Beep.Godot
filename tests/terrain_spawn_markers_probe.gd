extends SceneTree

# FEAT-12: a map carries its starts as ordinary scene nodes, and a playable cordon as bounds.
#   - a generated world writes Spawns/Start_<k> markers, and GridStartAreaComponent reads the same
#     cells back from them with NO generator and NO data layers wired;
#   - the Spawns node is deliberately offset from the map root, so a marker written in the wrong
#     space lands on the wrong cell;
#   - an authored map - two markers, no reservations - answers OriginOf, reports HasAreas false,
#     and leaves the build restriction inert with exactly one warning;
#   - PlayableInset holds the outer ring out of play: IsInBounds is false there and placement
#     refuses it as out_of_bounds, while the cell just inside is allowed.

const TERRAIN := "res://addons/beep_game_builder_cs/ecs/terrain/"
const GRID := "res://addons/beep_game_builder_cs/ecs/grid/"
const SIZE := Vector2i(48, 48)
const NO_CELL := Vector2i(-2147483648, -2147483648)

var failures: Array[String] = []
var rejections: Array[String] = []

func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		push_error(message)

func make(path: String, parent: Node, label: String, values: Dictionary = {}) -> Node:
	var node: Node = load(path).new()
	node.name = label
	for key in values: node.set(key, values[key])
	parent.add_child(node)
	return node

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	# ── A generated world publishes its starts ───────────────────────────────
	var host := Node2D.new()
	host.name = "Map"
	root.add_child(host)
	var cells := make(GRID + "GridCellDataComponent.cs", host, "Cells")
	var generator := make(TERRAIN + "TerrainGeneratorComponent.cs", host, "Generator", {
		"BoundsSize": SIZE, "Seed": 31415, "UseClimateBiomeMaps": true, "UseScaleRules": true,
		"CellDataPath": NodePath("../Cells"), "GenerateOnReady": false, "ClearExistingCells": true})
	generator.call("ApplyMapSetup", 0, 1, 1, 1, 1, 1)
	var kit: Resource = load(TERRAIN + "TerrainStartKit.cs").new()
	kit.set("MinAreaCells", 80)
	generator.set("StartKit", kit)
	generator.set("StartAreaRadius", 10)
	var painted := make(TERRAIN + "TerrainPaintedRendererComponent.cs", host, "Painted", {
		"TerrainGeneratorPath": NodePath("../Generator"), "BoundsSize": SIZE, "RefreshOnReady": false})
	var grid := make(GRID + "GridProjectionComponent.cs", host, "Grid", {"TileSize": Vector2(64, 64)})
	# Offset, so a marker written in the wrong space cannot land on the right cell by accident.
	var spawns := Node2D.new()
	spawns.name = "Spawns"
	spawns.position = Vector2(613, -417)
	host.add_child(spawns)
	var world := make(TERRAIN + "TerrainWorldComponent.cs", host, "World", {
		"GeneratorPath": NodePath("../Generator"), "PaintedRendererPath": NodePath("../Painted"),
		"GridPath": NodePath("../Grid"), "SpawnsPath": NodePath("../Spawns"),
		"UseCustomBounds": true, "CustomBounds": SIZE, "Seed": 31415,
		"StartAreaRadius": 10, "BuildOnReady": false, "ParticipatesInSave": false})
	world.call("NewWorld")
	await process_frame

	var starts: Array = generator.call("GetStartPositions")
	check(starts.size() >= 2, "the fixture map needs starts (%d)" % starts.size())
	check(spawns.get_child_count() == starts.size(), "the world wrote %d markers for %d starts" % [spawns.get_child_count(), starts.size()])
	for k in starts.size():
		var marker: Node = spawns.get_node_or_null("Start_%d" % k)
		check(marker != null, "start %d has no marker" % k)
		if marker != null:
			check(int(marker.get_meta("start_index")) == k, "marker %d carries start_index %s" % [k, marker.get_meta("start_index")])
			check(marker.get_meta("hq_footprint") == Vector2i(3, 3), "marker %d carries the headquarters footprint %s" % [k, marker.get_meta("hq_footprint")])

	# Read back with NO generator and NO data layers: the markers are the only record.
	var reader := make(GRID + "GridStartAreaComponent.cs", host, "StartArea", {
		"CellDataPath": NodePath("../Cells"), "SpawnsRootPath": NodePath("../Spawns"),
		"GridPath": NodePath("../Grid")})
	for k in starts.size():
		check(reader.call("OriginOf", k) == starts[k], "marker %d reads back as %s, the generator says %s" % [k, reader.call("OriginOf", k), starts[k]])
	check(reader.call("OriginOf", starts.size()) == NO_CELL, "an index past the last marker is NoCell")
	check(bool(reader.get("HasAreas")), "the generated map reserves ground, so HasAreas is true")
	# An emptied store holds no reservations: HasAreas must follow the cells, or a cleared map
	# reports areas whose every cell reads as outside one.
	cells.call("ClearCells")
	check(not bool(reader.get("HasAreas")), "a cleared cell store still reported start areas")
	generator.call("GenerateTerrain")
	check(bool(reader.get("HasAreas")), "regenerating the cells did not restore the reservations")

	# A rebuild republishes: markers belong to the world that was last built.
	world.call("NewWorld")
	check(spawns.get_child_count() == starts.size(), "a rebuild left %d markers for %d starts" % [spawns.get_child_count(), starts.size()])
	host.free()

	# ── An authored map: markers, no reservations ────────────────────────────
	var authored := Node2D.new()
	authored.name = "Authored"
	root.add_child(authored)
	var authored_cells := make(GRID + "GridCellDataComponent.cs", authored, "Cells", {"DefaultTerrainKind": "grass"})
	var authored_grid := make(GRID + "GridProjectionComponent.cs", authored, "Grid", {"TileSize": Vector2(64, 64)})
	var authored_spawns := Node2D.new()
	authored_spawns.name = "Spawns"
	authored_spawns.position = Vector2(-220, 96)
	authored.add_child(authored_spawns)
	var authored_cellsi := [Vector2i(4, 5), Vector2i(10, 5)]
	for k in authored_cellsi.size():
		var marker := Marker2D.new()
		marker.name = "Start_%d" % k
		authored_spawns.add_child(marker)
		marker.set_meta("start_index", k)
		marker.global_position = authored_grid.call("CellToWorld", authored_cellsi[k])
	var authored_reader := make(GRID + "GridStartAreaComponent.cs", authored, "StartArea", {
		"CellDataPath": NodePath("../Cells"), "SpawnsRootPath": NodePath("../Spawns"),
		"GridPath": NodePath("../Grid"), "LocalStartIndex": 1})
	check(authored_reader.call("OriginOf", 1) == authored_cellsi[1], "authored marker 1 reads as %s" % authored_reader.call("OriginOf", 1))
	check(not bool(authored_reader.get("HasAreas")), "an authored map with no reservations must report HasAreas false")

	var authored_placement := make(GRID + "GridPlacementComponent.cs", authored, "Placement", {
		"GridPath": NodePath("../Grid"), "CellDataPath": NodePath("../Cells"),
		"StartAreaPath": NodePath("../StartArea"), "RestrictBuildToStartArea": true,
		"UseMouseInput": false, "Footprint": Vector2i.ONE})
	var inert := true
	for cell in [Vector2i(4, 5), Vector2i(30, 30), Vector2i(31, 31)]:
		inert = inert and bool(authored_placement.call("CanPlace", cell))
	check(inert, "the build restriction must be inert on a map that reserves nothing")
	check(int(authored_placement.get("StartAreaWarnings")) == 1,
		"the unreserved map must be reported once, not per query (%d)" % int(authored_placement.get("StartAreaWarnings")))
	authored.free()

	# ── The cordon ───────────────────────────────────────────────────────────
	var cordon := Node2D.new()
	cordon.name = "Cordon"
	root.add_child(cordon)
	make(GRID + "GridCellDataComponent.cs", cordon, "Cells", {"DefaultTerrainKind": "grass"})
	make(GRID + "GridProjectionComponent.cs", cordon, "Grid", {"TileSize": Vector2(64, 64)})
	var navigation := make(GRID + "GridNavigationComponent.cs", cordon, "Navigation", {
		"GridPath": NodePath("../Grid"), "CellDataPath": NodePath("../Cells"),
		"BoundsOrigin": Vector2i.ZERO, "BoundsSize": SIZE})
	var placement := make(GRID + "GridPlacementComponent.cs", cordon, "Placement", {
		"GridPath": NodePath("../Grid"), "CellDataPath": NodePath("../Cells"),
		"NavigationPath": NodePath("../Navigation"), "UseMouseInput": false, "Footprint": Vector2i.ONE})
	placement.connect("PlacementRejected", func(_id, _x, _y, reason): rejections.append(reason))
	check(navigation.call("IsInBounds", Vector2i.ZERO), "without a cordon the outer ring is in play")
	navigation.set("PlayableInset", 1)
	check(not navigation.call("IsInBounds", Vector2i.ZERO), "a cordon of 1 holds the outer ring out of play")
	check(not navigation.call("IsInBounds", Vector2i(SIZE.x - 1, 20)), "the far edge is out of play too")
	check(navigation.call("IsInBounds", Vector2i.ONE), "the first playable cell is inside the cordon")
	check(int(navigation.get("EffectivePlayableInset")) == 1, "the applied cordon is 1")
	var source := Node2D.new()
	var packed := PackedScene.new()
	packed.pack(source)
	source.free()
	placement.set("PlacementScene", packed)
	placement.call("BeginPlacement", "hut")
	check(reason_at(placement, Vector2i.ZERO) == "out_of_bounds", "a cordoned cell is refused as out_of_bounds (%s)" % [rejections])
	check(reason_at(placement, Vector2i.ONE) == "", "the cell inside the cordon is allowed (%s)" % [rejections])
	# An inset that would leave nothing standing is bounded, and the bound is what applies.
	navigation.set("PlayableInset", 40)
	check(int(navigation.get("EffectivePlayableInset")) == (SIZE.x - 1) / 2, "an oversized cordon is bounded to %d (%d)" % [(SIZE.x - 1) / 2, int(navigation.get("EffectivePlayableInset"))])
	check(navigation.call("IsInBounds", Vector2i(SIZE.x / 2, SIZE.y / 2)), "the middle cell stays in play under a bounded cordon")
	cordon.free()

	print("[terrain-spawn-markers] OK" if failures.is_empty() else "[terrain-spawn-markers] FAILED")
	quit(0 if failures.is_empty() else 1)

func reason_at(placement: Node, cell: Vector2i) -> String:
	rejections.clear()
	placement.call("MovePreviewToCell", cell)
	placement.call("ConfirmPlacement")
	return rejections[0] if not rejections.is_empty() else ""
