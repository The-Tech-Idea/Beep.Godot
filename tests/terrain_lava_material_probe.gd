extends SceneTree

const BASE := "res://addons/beep_game_builder_cs/ecs/"
const ART := "res://addons/beep_game_builder_cs/textures/terrain/"

func _initialize() -> void:
	call_deferred("run")

func solid(colour: Color) -> ImageTexture:
	var pixels := Image.create(4, 4, false, Image.FORMAT_RGBA8)
	pixels.fill(colour)
	return ImageTexture.create_from_image(pixels)

func run() -> void:
	var viewport := SubViewport.new()
	viewport.size = Vector2i(768, 512)
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(viewport)
	var cells: Node = load(BASE + "grid/GridCellDataComponent.cs").new()
	cells.name = "Cells"
	viewport.add_child(cells)
	var origin := Vector2i(-5, 9)
	for y in 8:
		for x in 12:
			cells.call("SetTerrainKind", origin + Vector2i(x, y), "lava" if x < 4 else "rock" if x < 8 else "deep_water")
	var nav: Node = load(BASE + "grid/GridNavigationComponent.cs").new()
	nav.set("CellDataPath", NodePath("../Cells"))
	nav.set("BoundsOrigin", origin)
	nav.set("BoundsSize", Vector2i(12, 8))
	viewport.add_child(nav)
	var grid: Node = load(BASE + "grid/GridProjectionComponent.cs").new()
	grid.name = "Grid"
	grid.set("DrawGrid", false)
	viewport.add_child(grid)
	var placement: Node = load(BASE + "grid/GridPlacementComponent.cs").new()
	placement.set("UseMouseInput", false)
	placement.set("GridPath", NodePath("../Grid"))
	placement.set("CellDataPath", NodePath("../Cells"))
	viewport.add_child(placement)
	var view: Node2D = load(BASE + "terrain/TerrainPaintedRendererComponent.cs").new()
	view.set("RefreshOnReady", false)
	view.set("CellDataPath", NodePath("../Cells"))
	view.set("BoundsOrigin", origin)
	view.set("BoundsSize", Vector2i(12, 8))
	view.set("ShadeStrength", 0.0)
	# Still water with no surf, through the world's one water look (VIEW-04).
	var look: Resource = load(BASE + "terrain/TerrainWaterLook.cs").new()
	look.set("FoamStrength", 0.0)
	look.set("WaveIntensity", 0.0)
	look.set("ShallowTiles", 0.0)
	look.set("DeepTiles", 0.5)
	view.set("WaterLook", look)
	viewport.add_child(view)
	view.call("Rebuild")
	var material: ShaderMaterial = view.get_node("SplatSurface").material
	var ids: Image = material.get_shader_parameter("id_map").get_image()
	assert(roundi(ids.get_pixel(2, 4).r * 255.0) == 13, "Lava still aliases ordinary rock")
	assert(roundi(ids.get_pixel(6, 4).r * 255.0) == 10)
	assert(roundi(ids.get_pixel(10, 4).r * 255.0) == 12)
	assert(nav.call("IsBlocked", origin + Vector2i(2, 4)), "Lava lost its navigation rule")
	assert(not nav.call("IsBlocked", origin + Vector2i(6, 4)))
	assert(not placement.call("CanPlace", origin + Vector2i(2, 4)))
	assert(placement.call("CanPlace", origin + Vector2i(6, 4)))
	var coast_before: PackedByteArray = material.get_shader_parameter("coast_map").get_image().get_data()
	if DisplayServer.get_name() != "headless":
		material.set_shader_parameter("tex_lava", solid(Color.RED))
		material.set_shader_parameter("tex_rock", solid(Color.GREEN))
		material.set_shader_parameter("tex_sand", solid(Color.MAGENTA))
		material.set_shader_parameter("tex_shallow", solid(Color.BLUE))
		material.set_shader_parameter("tex_deep", solid(Color.BLUE))
		for tint in ["tint_lava", "tint_rock", "tint_shallow", "tint_deep"]:
			material.set_shader_parameter(tint, Vector3.ONE)
		material.set_shader_parameter("wave_speed", 0.0)
		for zoom in [0.5, 1.0, 2.0]:
			viewport.size = Vector2i(Vector2(768, 512) * zoom)
			view.scale = Vector2.ONE * zoom
			view.position = (Vector2.ONE * 32 - Vector2(origin) * 64) * zoom
			await process_frame
			await RenderingServer.frame_post_draw
			var rendered := viewport.get_texture().get_image()
			var lava := rendered.get_pixelv(Vector2i(Vector2(160, 288) * zoom))
			var rock := rendered.get_pixelv(Vector2i(Vector2(416, 288) * zoom))
			var water := rendered.get_pixelv(Vector2i(Vector2(672, 288) * zoom))
			assert(lava.r > 0.95 and lava.g < 0.02 and lava.b < 0.02, "Lava was skipped as water or replaced with rock")
			assert(rock.g > 0.95 and rock.r < 0.02 and rock.b < 0.02)
			assert(water.b > 0.8 and water.r < 0.1)
			assert(lava.a == 1.0 and rock.a == 1.0 and water.a == 1.0)
			print("[terrain-lava-material] distinct GPU cells at zoom ", zoom)
	# Rock/lava edits must not alter any coast bytes or become a water body.
	cells.call("SetTerrainKind", origin + Vector2i(2, 4), "rock")
	view.call("Rebuild")
	assert(material.get_shader_parameter("coast_map").get_image().get_data() == coast_before)
	assert(not nav.call("IsBlocked", origin + Vector2i(2, 4)))
	assert(placement.call("CanPlace", origin + Vector2i(2, 4)))
	var generator: Node = load(BASE + "terrain/TerrainGeneratorComponent.cs").new()
	generator.set("GenerateOnReady", false)
	generator.set("CellDataPath", NodePath("../Cells"))
	generator.set("BoundsOrigin", origin)
	generator.set("BoundsSize", Vector2i(12, 8))
	generator.set("Mode", 0)
	generator.set("Preset", 6)
	viewport.add_child(generator)
	generator.call("GenerateTerrain")
	view.call("Rebuild")
	var generated_ids: Image = material.get_shader_parameter("id_map").get_image()
	for y in 8:
		for x in 12:
			assert(cells.call("GetTerrainKind", origin + Vector2i(x, y)) == "lava")
			assert(roundi(generated_ids.get_pixel(x, y).r * 255.0) == 13)
	var imported: Texture2D = load(ART + "lava_ground.png")
	assert(imported != null and imported.get_image().has_mipmaps())
	assert(imported.get_image().detect_alpha() == Image.ALPHA_NONE)
	print("[terrain-lava-material] imported texture: ", imported.get_size())
	if DisplayServer.get_name() != "headless":
		await capture_volcanic_preset(viewport, cells, generator, view, origin)
	viewport.free()
	print("[terrain-lava-material] OK")
	quit()

func capture_volcanic_preset(viewport: SubViewport, cells: Node, generator: Node, view: Node2D, origin: Vector2i) -> void:
	var names := {"LavaTexturePath": "lava_ground.png", "RockTexturePath": "rock.png",
		"SandTexturePath": "sand.png", "ShallowWaterTexturePath": "water_shallow.png", "DeepWaterTexturePath": "water_deep.png"}
	for property in names:
		view.set(property, ART + names[property])
	view.set("ShadeStrength", 0.35)
	var look: Resource = view.get("WaterLook")
	look.set("FoamStrength", 0.5)
	look.set("WaveIntensity", 1.0)
	look.set("ShallowTiles", 1.8)
	look.set("DeepTiles", 4.5)
	var material: ShaderMaterial = view.get_node("SplatSurface").material
	material.set_shader_parameter("tint_rock", Vector3.ONE * 0.78)
	generator.set("BoundsSize", Vector2i(32, 32))
	generator.set("Mode", 1)
	generator.set("Preset", 6)
	generator.set("UseClimateBiomeMaps", false)
	generator.set("UseScaleRules", true)
	generator.set("Seed", 31415)
	generator.set("LandmassScale", 0.65)
	generator.call("GenerateTerrain")
	view.set("BoundsSize", Vector2i(32, 32))
	view.call("Rebuild")
	var kinds: Dictionary = {}
	for y in 32:
		for x in 32:
			var kind: String = cells.call("GetTerrainKind", origin + Vector2i(x, y))
			kinds[kind] = kinds.get(kind, 0) + 1
	print("[terrain-lava-material] generated volcanic kinds: ", kinds)
	assert(kinds.get("lava", 0) > 0 and kinds.get("rock", 0) > 0, "Volcanic preset did not create both crust and lava")
	assert(kinds.get("grass", 0) == 0, "Volcanic cleanup invented grass")
	for start in generator.call("GetStartPositions"):
		assert(cells.call("GetTerrainKind", start) != "lava", "Generator placed a starting position on blocked lava")
	viewport.size = Vector2i(1024, 768)
	DirAccess.make_dir_recursive_absolute("res://tests/output/lava_material")
	for zoom in [0.35, 1.0]:
		view.scale = Vector2.ONE * zoom
		view.position = Vector2(512, 384) - (Vector2(origin) * 64 + Vector2(16, 16) * 64 - Vector2.ONE * 32) * zoom
		await process_frame
		await RenderingServer.frame_post_draw
		assert(viewport.get_texture().get_image().save_png("res://tests/output/lava_material/generated-zoom-%s.png" % zoom) == OK)
