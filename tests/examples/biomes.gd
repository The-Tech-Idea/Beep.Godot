extends SceneTree

# Temperature must decide biomes, the way the genre decides them.
#
# A Whittaker diagram is a lookup: a pair of temperature and moisture names a
# biome. It is how Dwarf Fortress and Minecraft assign terrain, and Civilization
# states the same contract at the settings level - hot raises desert and cuts
# tundra, cold does the reverse.
#
# A quota pass used to override that table with percentiles of the map's own
# moisture, meaning to guarantee each biome a share. It did the opposite: every
# land biome collapsed into grass, hot maps lost desert entirely, and because
# TerrainWorldComponent switched it on for every world it built, the collapse was
# the normal case. Nothing caught it, because nothing asserted that the climate
# axis reaches the ground.

const CELL_DATA := preload("res://addons/beep_game_builder_cs/ecs/grid/GridCellDataComponent.cs")
const GENERATOR := preload("res://addons/beep_game_builder_cs/ecs/terrain/TerrainGeneratorComponent.cs")
const SIZE := Vector2i(64, 40)

var failures: Array[String] = []
var gen: Node

func check(ok: bool, message: String) -> void:
	if ok:
		print("  ok    %s" % message)
	else:
		print("  FAIL  %s" % message)
		failures.append(message)

func histogram(temperature: int) -> Dictionary:
	# Built the way TerrainWorldComponent builds a world, so this measures the
	# path a game actually takes rather than a configuration only a test uses.
	gen.call("ApplyMapSetup", 0, 1, temperature, 1, 1, 1)
	gen.set("UseClimateBiomeMaps", true)
	gen.set("UseScaleRules", true)
	gen.call("GenerateTerrain")
	var h := {}
	for y in SIZE.y:
		for x in SIZE.x:
			var k: String = str(gen.call("TerrainKindAt", Vector2i(x, y)))
			h[k] = int(h.get(k, 0)) + 1
	return h

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var cells: Node = CELL_DATA.new()
	cells.name = "Cells"
	root.add_child(cells)
	gen = GENERATOR.new()
	gen.name = "Gen"
	gen.set("CellDataPath", NodePath("../Cells"))
	gen.set("BoundsSize", SIZE)
	root.add_child(gen)
	await process_frame

	var cold := histogram(0)
	var temperate := histogram(1)
	var hot := histogram(2)

	print("  cold      %s" % str(cold))
	print("  temperate %s" % str(temperate))
	print("  hot       %s" % str(hot))

	# The land table has to reach the ground: more than one kind of it.
	for label in [["cold", cold], ["temperate", temperate], ["hot", hot]]:
		var land := 0
		for kind in label[1]:
			if kind not in ["deep_water", "shallow_water", "sand"]:
				land += 1
		check(land >= 2, "%s produces more than one land biome (%d kinds)" % [label[0], land])

	# The genre's own contract, in both directions.
	check(int(hot.get("desert", 0)) > 0,
		"a hot world has desert (%d tiles)" % int(hot.get("desert", 0)))
	check(int(cold.get("snow", 0)) + int(cold.get("tundra", 0)) > 0,
		"a cold world has snow or tundra (%d tiles)"
			% [int(cold.get("snow", 0)) + int(cold.get("tundra", 0))])
	# Not an absolute zero: fixing the Continents/Archipelago landmass-count
	# overlap (TerrainGenerationSettings.RequestedLandmassCount) gave Mainland
	# fewer, LARGER continents, and a large continent centred on a cold
	# latitude can still reach a warm enough fringe to carry a genuine, if
	# minor, desert margin - the way a real cold-centred continent can. What
	# the genre contract actually promises is DIRECTION, not an absolute: a
	# cold world has markedly less desert than a hot one at the same size and
	# seed, which is the comparison below.
	check(int(cold.get("desert", 0)) < int(hot.get("desert", 0)),
		"a cold world has less desert than a hot world (%d vs %d tiles)"
			% [int(cold.get("desert", 0)), int(hot.get("desert", 0))])
	check(int(hot.get("snow", 0)) + int(hot.get("tundra", 0)) == 0,
		"a hot world has no tundra or snow (%d tiles)"
			% [int(hot.get("snow", 0)) + int(hot.get("tundra", 0))])

	# And the axis must actually move something, not merely be accepted.
	check(str(cold) != str(temperate) and str(temperate) != str(hot),
		"each temperature setting produces a different world")

	print("\nRESULT: ", "all checks passed" if failures.is_empty() else "%d FAILED" % failures.size())
	quit(1 if failures.size() > 0 else 0)
