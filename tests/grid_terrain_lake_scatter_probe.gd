extends SceneTree

const CELL_DATA_SCRIPT := preload("res://addons/beep_game_builder_cs/ecs/terrain/GridCellDataComponent.cs")
const GENERATOR_SCRIPT := preload("res://addons/beep_game_builder_cs/ecs/terrain/TerrainGeneratorComponent.cs")
const SCATTER_SCRIPT := preload("res://addons/beep_game_builder_cs/ecs/terrain/SeededTerrainPropScatterComponent.cs")

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var cells := CELL_DATA_SCRIPT.new()
	cells.name = "Cells"
	root.add_child(cells)

	var generator := GENERATOR_SCRIPT.new()
	generator.name = "Generator"
	generator.set("CellDataPath", NodePath("../Cells"))
	generator.set("UsePainterSettings", false)
	generator.set("BoundsSize", Vector2i(64, 40))
	generator.set("Seed", 424242)
	generator.set("LakeCoverage", 0.22)
	generator.set("LakeFrequencyMultiplier", 0.10)
	generator.set("LakeShoreWidth", 0.04)
	root.add_child(generator)
	await process_frame
	generator.call("GenerateTerrain")

	var lake_cells := 0
	for y in 40:
		for x in 64:
			var terrain := str(cells.call("GetTerrainKind", Vector2i(x, y)))
			if terrain == "deep_water" or terrain == "shallow_water":
				lake_cells += 1

	if lake_cells == 0:
		_fail("Explicit lake generation produced no lake water cells.")
		return

	var scatter := SCATTER_SCRIPT.new()
	scatter.name = "Scatter"
	scatter.set("TerrainGeneratorPath", NodePath("../Generator"))
	scatter.set("CellDataPath", NodePath("../Cells"))
	scatter.set("SizeInTiles", Vector2i(64, 40))
	scatter.set("TileSize", 64)
	scatter.set("Seed", 424242)
	scatter.set("MaxProps", 128)
	scatter.set("GrassCoverage", 0.35)
	scatter.set("DesertCoverage", 0.35)
	scatter.set("RockCoverage", 0.35)
	scatter.set("AllowShallowWaterProps", false)
	scatter.set("GrassPrimaryPath", "res://addons/beep_game_builder_cs/textures/plants/grasses01.png")
	scatter.set("GrassSecondaryPath", "C:/Users/f_ald/source/repos/The-Tech-Idea/Art/Plants/plantpack/isometric tiles/bush01.png")
	scatter.set("DesertPrimaryPath", "res://addons/beep_game_builder_cs/textures/plants/cactus01.png")
	scatter.set("DesertAccentPath", "res://addons/beep_game_builder_cs/textures/rocks/rock1.png")
	scatter.set("RockPrimaryPath", "res://addons/beep_game_builder_cs/textures/rocks/rock2.png")
	root.add_child(scatter)
	await process_frame
	scatter.call("Rebuild")
	await process_frame

	var prop_count := 0
	var water_props := 0
	for child in scatter.get_children():
		if not child.name.begins_with("GeneratedTerrainStamp_"):
			continue

		prop_count += 1
		var tile_position: Vector2 = child.position / 64.0
		var visual_terrain := str(generator.call("TerrainKindAt", tile_position))
		var cell_terrain := str(cells.call("GetTerrainKind", Vector2i(int(floor(tile_position.x)), int(floor(tile_position.y)))))
		if visual_terrain == "deep_water" or visual_terrain == "shallow_water" or visual_terrain == "water":
			water_props += 1
		elif cell_terrain == "deep_water" or cell_terrain == "shallow_water" or cell_terrain == "water":
			water_props += 1

	if prop_count == 0:
		_fail("Terrain prop scatter produced no props with configured palettes.")
		return
	if water_props > 0:
		_fail("Terrain prop scatter placed %d props on water." % water_props)
		return

	print("[grid-terrain-lake-scatter] OK: lake_cells=%d props=%d water_props=%d" % [lake_cells, prop_count, water_props])
	quit(0)

func _fail(message: String) -> void:
	push_error("[grid-terrain-lake-scatter] " + message)
	quit(1)
