extends SceneTree

# A time-sliced paint notices when the ground moves under it.
#
# TerrainIsometricAutotileRendererComponent paints large maps across frames. On every
# frame it must ask whether what it started from is still what it should be drawing -
# the window, the bindings, the TileSet, and in generated-preview mode the generator's
# settings. That check used to be a string key that Json.Stringify'd a reflection
# capture of the generator's whole export list each frame; it now compares the
# generator's own TerrainGenerationSettings record. The contract scan pins the cost.
# This pins the behaviour the cheaper check has to keep: a change mid-paint restarts
# the paint, and the finished layer reflects the change, not the start.

const BASE := "res://addons/beep_game_builder_cs/"
const Fixture := preload("res://tests/fixtures/iso_variant_tileset.gd")
# Every LAND kind, so which cells get painted follows the generated coastline.
const LAND := "grass,dry_grass,desert,sand,tundra,snow,ice,jungle,swamp,mud,gravel,rock,lava=0"

var failures: Array[String] = []

func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		push_error(message)

func _initialize() -> void:
	call_deferred("run")

func autotile(host: Node, name: String, size: Vector2i) -> Node2D:
	var view: Node2D = load(BASE + "ecs/terrain/TerrainIsometricAutotileRendererComponent.cs").new()
	view.name = name
	view.set("RefreshOnReady", false)
	view.set("BoundsSize", size)
	view.set("Tiles", Fixture.build(2))
	view.set("TerrainSet", 0)
	view.set("UseTerrainConnections", false)
	view.set("TerrainBindings", PackedStringArray([LAND]))
	# The smallest budget the export allows, so a 40x40 paint spans many frames.
	view.set("CellsPerFrame", 64)
	host.add_child(view)
	return view

# Waits until the time-sliced paint has actually taken its first step - the step in
# which RebuildSteps reads BoundsSize and resolves the generator's field. Changing an
# input BEFORE that step is not a change mid-paint: the paint just starts from the new
# value, and a staleness check that stopped looking at the input passes anyway. Two
# blind awaits stood here and did exactly that - a mutation that removed the
# BoundsSize compare passed - so the wait is on observed progress, not on frames.
func started(view: Node, label: String) -> bool:
	for i in 120:
		await process_frame
		if view.get("CellsProcessedLastFrame") > 0:
			return true
	check(false, "%s took no paint step in 120 frames" % label)
	return false

func settle(view: Node, label: String) -> void:
	for i in 400:
		if not view.get("IsRebuilding"):
			return
		await process_frame
	check(false, "%s never finished its time-sliced paint" % label)

func used(view: Node) -> Array:
	var layer: TileMapLayer = view.get_node_or_null("IsoTerrain")
	check(layer != null, "%s built no terrain layer" % view.name)
	if layer == null:
		return []
	var cells := layer.get_used_cells()
	cells.sort()
	return cells

func run() -> void:
	var host := Node2D.new()
	root.add_child(host)

	# 1. Live map: the window shrinks mid-paint. A paint that kept going would finish
	#    with the 1600 cells it was started on; a restarted one paints the 100 it was
	#    changed to.
	var cells: Node = load(BASE + "ecs/grid/GridCellDataComponent.cs").new()
	cells.name = "Cells"
	host.add_child(cells)
	cells.call("FillTerrain", Rect2i(0, 0, 40, 40), "grass")
	var live := autotile(host, "Live", Vector2i(40, 40))
	live.set("CellDataPath", NodePath("../Cells"))
	live.call("RequestRebuild")
	var live_started: bool = await started(live, "the live view")
	check(live_started and live.get("IsRebuilding"), "the 40x40 live paint finished in its first step at 64 cells a frame; the fixture is too small to catch a stale build")
	live.set("BoundsSize", Vector2i(10, 10))
	await settle(live, "the live view")
	var painted: int = used(live).size()
	check(painted == 100, "after shrinking the window mid-paint the live view holds %d cells, not the 100 of the new window - the paint did not restart" % painted)

	# 2. Generated preview: the generator's seed changes mid-paint. Only land is bound,
	#    so which cells are painted is the coastline - and the coastline is the seed's.
	var generator: Node = load(BASE + "ecs/terrain/TerrainGeneratorComponent.cs").new()
	generator.name = "Generator"
	generator.set("GenerateOnReady", false)
	generator.set("BoundsSize", Vector2i(40, 40))
	generator.set("StartPositionCount", 0)
	generator.set("Seed", 31415)
	host.add_child(generator)
	var preview := autotile(host, "Preview", Vector2i(40, 40))
	preview.set("TerrainGeneratorPath", NodePath("../Generator"))
	preview.call("RequestRebuild")
	var preview_started: bool = await started(preview, "the generated view")
	check(preview_started and preview.get("IsRebuilding"), "the 40x40 generated paint finished in its first step; the fixture is too small")
	generator.set("Seed", 4242)
	await settle(preview, "the generated view")
	var after_change := used(preview)

	# What a paint started AFTER the change draws - the reference the restarted paint
	# must match - and what the old seed drew, to prove the two seeds differ at all.
	var reference := autotile(host, "Reference", Vector2i(40, 40))
	reference.set("TerrainGeneratorPath", NodePath("../Generator"))
	reference.call("Rebuild")
	var expected := used(reference)
	generator.set("Seed", 31415)
	var stale := autotile(host, "Stale", Vector2i(40, 40))
	stale.set("TerrainGeneratorPath", NodePath("../Generator"))
	stale.call("Rebuild")
	var old_seed := used(stale)
	check(expected != old_seed, "seeds 31415 and 4242 paint the same coastline; the fixture cannot tell a stale paint from a fresh one")
	check(after_change == expected,
		"after the seed changed mid-paint the generated view holds %d cells; a paint restarted on the new seed holds %d - the settings change was not noticed" % [after_change.size(), expected.size()])

	host.free()
	if failures.is_empty():
		print("[terrain-autotile-staleness] OK")
		quit(0)
	else:
		print("[terrain-autotile-staleness] FAILED")
		quit(1)
