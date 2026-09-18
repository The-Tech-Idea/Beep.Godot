extends SceneTree

const ART := "res://addons/beep_game_builder_cs/textures/terrain/"

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	var args := OS.get_cmdline_user_args()
	var image_path: String = args[0] if not args.is_empty() else ProjectSettings.globalize_path(ART + "bedrock_ground.png")
	var rock := Image.load_from_file(image_path)
	assert(not rock.is_empty() and rock.get_width() >= 1024 and rock.get_width() == rock.get_height())
	assert(rock.detect_alpha() == Image.ALPHA_NONE, "Bedrock albedo contains transparency")
	var edge := Vector2.ZERO
	for i in rock.get_width():
		edge.x += difference(rock.get_pixel(0, i), rock.get_pixel(rock.get_width() - 1, i)) / rock.get_width()
		edge.y += difference(rock.get_pixel(i, 0), rock.get_pixel(i, rock.get_height() - 1)) / rock.get_width()
	print("[terrain-bedrock-texture] opposite-edge mean RGB difference=", edge)
	if not args.is_empty():
		assert(edge.x < 0.04 and edge.y < 0.04, "Candidate still needs repeat-edge correction")
		print("[terrain-bedrock-texture] candidate OK")
		quit()
		return
	var imported: Texture2D = load(ART + "bedrock_ground.png")
	assert(imported.get_image().has_mipmaps(), "Bedrock import lost its mip chain")
	var profile: Resource = load(ART + "painted_ground_tiling.tres")
	assert(profile.get("Rock") == 6.0 and is_equal_approx(profile.get("SeamBlend"), 0.04))
	for key in ["Grass", "DryGrass", "Sand", "Dirt", "Snow", "Mud", "Gravel", "Lava"]:
		assert(profile.get(key) == 0.0, "Bedrock preset changed another material's scale")
	for path in ["terrain/terrain_generator_lab", "terrain/terrain_splat_demo", "terrain/terrain_generation_layers_demo", "grid_world_2d_iso"]:
		var scene: Node = load("res://addons/beep_game_builder_cs/templates/scenes/" + path + ".tscn").instantiate()
		var view := scene.find_child("Splat", true, false)
		assert(view != null and view.get("RockTexturePath") == ART + "bedrock_ground.png", "Painted demo omitted bedrock: " + path)
		assert(view.get("MaterialTiling") == profile, "Painted demos have divergent tiling profiles: " + path)
		scene.free()
	if DisplayServer.get_name() != "headless": await check_repeat_pixels()
	print("[terrain-bedrock-texture] OK")
	quit()

func difference(a: Color, b: Color) -> float:
	return (absf(a.r - b.r) + absf(a.g - b.g) + absf(a.b - b.b)) / 3.0

func check_repeat_pixels() -> void:
	const BASE := "res://addons/beep_game_builder_cs/ecs/"
	var viewport := SubViewport.new()
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(viewport)
	var cells: Node = load(BASE + "grid/GridCellDataComponent.cs").new()
	cells.name = "Cells"
	cells.set("DefaultTerrainKind", "rock")
	viewport.add_child(cells)
	var view: Node2D = load(BASE + "terrain/TerrainPaintedRendererComponent.cs").new()
	view.set("RefreshOnReady", false)
	view.set("CellDataPath", NodePath("../Cells"))
	view.set("BoundsSize", Vector2i(8, 8))
	view.set("RockTexturePath", ART + "bedrock_ground.png")
	view.set("ShadeStrength", 0.0)
	# What is measured here is the seam correction at a texture repeat, so everything else that varies
	# a pixel is off: the hillshade above, and the ground grain, whose own high-frequency variation
	# (FIX-17) sits at a different repeat and would be counted as leftover seam.
	view.set("GroundDetailStrength", 0.0)
	var look: Resource = load(BASE + "terrain/TerrainWaterLook.cs").new()
	look.set("WaveIntensity", 0.0)
	look.set("FoamStrength", 0.0)
	view.set("WaterLook", look)
	var profile: Resource = load(BASE + "terrain/TerrainMaterialTiling.cs").new()
	profile.set("Rock", 4.0)
	view.set("MaterialTiling", profile)
	viewport.add_child(view)
	view.call("Rebuild")
	var material: ShaderMaterial = view.get_node("SplatSurface").material
	var data: Dictionary = {}
	for slot in ["id_map", "shade_map", "coast_map"]: data[slot] = material.get_shader_parameter(slot)
	DirAccess.make_dir_recursive_absolute("res://tests/output/bedrock")
	for zoom in [0.5, 1.0, 2.0]:
		viewport.size = Vector2i(Vector2(512, 512) * zoom)
		view.scale = Vector2.ONE * zoom
		var images: Array[Image] = []
		for blend in [0.0, 0.04]:
			profile.set("SeamBlend", blend)
			view.call("Rebuild")
			for slot in data: assert(material.get_shader_parameter(slot) == data[slot], "Seam correction rebuilt terrain maps")
			await process_frame
			await RenderingServer.frame_post_draw
			var image := viewport.get_texture().get_image()
			images.append(image)
			assert(image.save_png("res://tests/output/bedrock/repeat-%s-zoom-%s.png" % [blend, zoom]) == OK)
		var period := int(256 * zoom)
		var before := Vector2.ZERO
		var after := Vector2.ZERO
		for i in viewport.size.x:
			before.x += difference(images[0].get_pixel(period - 1, i), images[0].get_pixel(period, i)) / viewport.size.x
			after.x += difference(images[1].get_pixel(period - 1, i), images[1].get_pixel(period, i)) / viewport.size.x
			before.y += difference(images[0].get_pixel(i, period - 1), images[0].get_pixel(i, period)) / viewport.size.x
			after.y += difference(images[1].get_pixel(i, period - 1), images[1].get_pixel(i, period)) / viewport.size.x
		print("[terrain-bedrock-texture] zoom=", zoom, " repeat discontinuity before=", before, " after=", after)
		assert(after.x < before.x * 0.3 and after.y < before.y * 0.3, "Repeat correction did not remove the boundary jump")
		var margin := ceili(period * 0.04) + 2
		for y in viewport.size.y:
			for x in viewport.size.x:
				assert(images[1].get_pixel(x, y).a == 1.0, "Seam correction introduced alpha")
				var local := Vector2i(x % period, y % period)
				if local.x >= margin and local.x < period - margin and local.y >= margin and local.y < period - margin:
					assert(images[0].get_pixel(x, y) == images[1].get_pixel(x, y), "Seam correction blurred texture interiors")
	viewport.free()
