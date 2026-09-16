extends SceneTree

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	var probe = load("res://tests/TerrainWaterSurfaceSmoke.cs").new()
	root.add_child(probe)
	assert(probe.call("Run"))
	probe.free()
	if DisplayServer.get_name() != "headless":
		await check_pixels()
		await check_feature_frames()
	print("[terrain-water-surface] OK")
	quit()

func solid(colour: Color) -> ImageTexture:
	var image := Image.create(4, 4, false, Image.FORMAT_RGBA8)
	image.fill(colour)
	return ImageTexture.create_from_image(image)

func check_pixels() -> void:
	const BASE := "res://addons/beep_game_builder_cs/ecs/"
	var viewport := SubViewport.new()
	viewport.size = Vector2i(1088, 1088)
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(viewport)
	var host := Node2D.new()
	host.position = Vector2(32, 32)
	viewport.add_child(host)
	var cells: Node = load(BASE + "grid/GridCellDataComponent.cs").new()
	cells.name = "Cells"
	host.add_child(cells)
	var generator: Node = load(BASE + "terrain/TerrainGeneratorComponent.cs").new()
	generator.set("GenerateOnReady", false)
	generator.set("CellDataPath", NodePath("../Cells"))
	generator.set("BoundsSize", Vector2i(32, 32))
	generator.set("TopologySamplesPerCell", 8)
	generator.set("LandmassScale", 0.42)
	generator.set("StartPositionCount", 0)
	host.add_child(generator)
	var painted: Node = load(BASE + "terrain/TerrainPaintedRendererComponent.cs").new()
	painted.set("RefreshOnReady", false)
	painted.set("CellDataPath", NodePath("../Cells"))
	painted.set("BoundsSize", Vector2i(32, 32))
	painted.set("TileSize", 16)
	painted.set("ShadeStrength", 0.0)
	# Still water with no surf: the sea's dials are the world's one look (VIEW-04).
	var look: Resource = load(BASE + "terrain/TerrainWaterLook.cs").new()
	look.set("FoamStrength", 0.0)
	look.set("WaveIntensity", 0.0)
	painted.set("WaterLook", look)
	host.add_child(painted)
	for seed in [31415, 12345, 98765, 8675309]:
		generator.set("Seed", seed)
		generator.call("GenerateTerrain")
		painted.call("Rebuild")
		var surface: TileMapLayer = painted.get_node("SplatSurface")
		var material: ShaderMaterial = surface.material
		for slot in ["grass", "dry_grass", "dirt", "snow", "mud", "gravel", "rock", "sand"]:
			material.set_shader_parameter("tex_" + slot, solid(Color.RED))
		for slot in ["shallow", "deep"]:
			material.set_shader_parameter("tex_" + slot, solid(Color.BLUE))
		for tint in ["grass", "dry_grass", "desert", "sand", "tundra", "snow", "ice", "jungle", "swamp", "gravel", "rock", "shallow", "deep"]:
			material.set_shader_parameter("tint_" + tint, Vector3.ONE)
		material.set_shader_parameter("shallow_sand", 0.0)
		material.set_shader_parameter("shore_wet", 0.0)
		material.set_shader_parameter("wave_speed", 0.0)
		for zoom in [0.5, 1.0, 2.0]:
			host.scale = Vector2.ONE * zoom
			await process_frame
			await RenderingServer.frame_post_draw
			var image := viewport.get_texture().get_image()
			for y in 32:
				for x in 32:
					var cell := Vector2i(x, y)
					var pixel := image.get_pixelv(Vector2i(surface.to_global(surface.map_to_local(cell))))
					var water: bool = generator.call("WaterSourceAt", cell) != ""
					assert(pixel.r + pixel.b > 0.7, "Missing generated terrain pixel")
					assert((pixel.b > pixel.r) == water, "Generated coast disagrees with grid centre: seed=%s cell=%s zoom=%s" % [seed, cell, zoom])
	viewport.free()

func check_feature_frames() -> void:
	const BASE := "res://addons/beep_game_builder_cs/ecs/"
	DirAccess.make_dir_recursive_absolute("res://tests/output/feature_scatter")
	var path := ProjectSettings.globalize_path("res://tests/output/feature_scatter/frames.png")
	var sheet := Image.create(192, 64, false, Image.FORMAT_RGBA8)
	var colours := [Color.RED, Color.GREEN, Color.BLUE]
	for i in 3: sheet.fill_rect(Rect2i(i * 64, 0, 64, 64), colours[i])
	assert(sheet.save_png(path) == OK)
	var viewport := SubViewport.new()
	viewport.size = Vector2i(512, 192)
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(viewport)
	var host := Node2D.new()
	host.position = Vector2(16, 16)
	viewport.add_child(host)
	var cells: Node = load(BASE + "grid/GridCellDataComponent.cs").new()
	cells.name = "Cells"
	host.add_child(cells)
	for i in 3:
		cells.call("SetTerrainKind", Vector2i(i, 0), ["grass", "snow", "jungle"][i])
		cells.call("SetMetadata", Vector2i(i, 0), "terrain_feature", ["woods", "forest", "jungle"][i])
	var snapshot := var_to_bytes(cells.call("GetCells"))
	var features: Node2D = load(BASE + "terrain/TerrainFeatureRendererComponent.cs").new()
	features.set("RefreshOnReady", false)
	features.set("CellDataPath", NodePath("../Cells"))
	features.set("BoundsSize", Vector2i(3, 1))
	features.set("WoodsSheetPath", path)
	features.set("WoodsColumns", 3)
	features.set("WoodsRows", 1)
	features.set("SpritesPerTile", 1)
	features.set("ForestExtraSprites", 0)
	features.set("PositionJitter", 0.0)
	features.set("ScaleJitter", 0.0)
	# Test frame bindings without adjacent opaque fixture sprites covering each other.
	var sizing: Resource = load(BASE + "terrain/TerrainPropSizing.cs").new()
	sizing.set("Trees", Vector2(0.8, 0.8))
	features.set("PropSizing", sizing)
	host.add_child(features)
	for reverse in [false, true]:
		features.set("WoodsFrameBindings", PackedStringArray(["grass=2", "snow=1", "jungle=0"] if reverse else ["grass=0", "snow=1", "jungle=2"]))
		features.call("Rebuild")
		assert(features.get("StampCount") == 3)
		for zoom in [0.5, 1.0, 2.0]:
			host.scale = Vector2.ONE * zoom
			await process_frame
			await RenderingServer.frame_post_draw
			var image := viewport.get_texture().get_image()
			for i in 3:
				var pixel := image.get_pixelv(Vector2i(host.to_global(Vector2(i * 64 + 32, 32))))
				var expected: Color = colours[2 - i if reverse else i]
				assert(Vector3(pixel.r, pixel.g, pixel.b).distance_to(Vector3(expected.r, expected.g, expected.b)) < 0.02,
					"Actual feature frame ignored terrain binding or retained a previous binding")
	assert(var_to_bytes(cells.call("GetCells")) == snapshot, "Feature styling changed live grid state")
	viewport.free()
