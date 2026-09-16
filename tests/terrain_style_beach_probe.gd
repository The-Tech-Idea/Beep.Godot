extends "res://tests/terrain_lab_build.gd"

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	assert(DisplayServer.get_name() != "headless")
	root.size = Vector2i(1024, 1024)
	var scene: Node = load("res://addons/beep_game_builder_cs/templates/scenes/terrain/terrain_generator_lab.tscn").instantiate()
	var world: Node = scene.get_node("World")
	world.set("MapSize", 0)
	root.add_child(scene)
	var build := await await_lab_build(world)
	assert(build.success, "the lab's first build did not succeed: %s" % build.message)
	scene.get_node("HUD").hide()
	var preview: Node2D = scene.get_node("Preview")
	preview.scale = Vector2.ONE * 0.5
	preview.position = Vector2.ZERO
	var cells: Node = scene.get_node("Preview/Cells")
	var before := var_to_bytes(cells.call("GetCells"))
	var averages: Array[Vector3] = []
	var output := "res://tests/output/coastal_grass/"
	DirAccess.make_dir_recursive_absolute(output)
	for style in ["original", "pixel_art", "cartoon"]:
		world.set("MapArt", null if style == "original" else load("res://addons/beep_game_builder_cs/textures/map_art/" + style + ".tres"))
		world.call("Redraw")
		for node in ["Features", "RockObjects", "MapOverlay"]: preview.get_node(node).hide()
		var material: ShaderMaterial = preview.get_node("Splat/SplatSurface").material
		material.set_shader_parameter("wave_speed", 0.0)
		await process_frame
		await RenderingServer.frame_post_draw
		var image := root.get_texture().get_image()
		assert(image.save_png(output + style + ".png") == OK)
		var beach := 0
		var sum := Vector3.ZERO
		var coast: Image = (material.get_shader_parameter("coast_map") as Texture2D).get_image()
		var ids: Image = (material.get_shader_parameter("id_map") as Texture2D).get_image()
		var distance_range: float = material.get_shader_parameter("coast_range")
		for y in 32:
			for x in 32:
				var at := Vector2i(x, y)
				var colour := image.get_pixel(x * 32 + 16, y * 32 + 16)
				sum += Vector3(colour.r, colour.g, colour.b)
				var sample_at := Vector2i((Vector2(at) + Vector2.ONE * 0.5) * Vector2(coast.get_size()) / 32.0)
				var distance := (0.5 - coast.get_pixelv(sample_at).b) * 2.0 * distance_range
				var width := ids.get_pixelv(at).a * 4.0
				if width > 0.0 and distance > 0.3 and distance < width - 0.1:
					assert(colour.r > colour.g, "Grass/water displaced the beach at " + str(at) + " in " + style)
					beach += 1
		assert(beach > 10)
		assert(var_to_bytes(cells.call("GetCells")) == before)
		averages.append(sum / 1024.0)
		print("[terrain-style-beach] ", style, " checked sand cells=", beach)
	assert(averages[1].distance_to(averages[2]) > 0.08, "Styles still share the same palette")
	for projection in [1, 2, 3]:
		world.set("Projection", projection)
		world.call("Redraw")
		for node in ["Features", "RockObjects", "MapOverlay", "IsoFeatures"]: preview.get_node(node).hide()
		var extent: Rect2 = world.call("PreviewExtent")
		var fit := 960.0 / maxf(extent.size.x, extent.size.y)
		preview.scale = Vector2.ONE * fit
		preview.position = Vector2(512, 512) - extent.get_center() * fit
		await process_frame
		await RenderingServer.frame_post_draw
		assert(root.get_texture().get_image().save_png(output + "projection_%d.png" % projection) == OK)
		assert(var_to_bytes(cells.call("GetCells")) == before, "Projection changed the shared shoreline")
	scene.free()
	print("[terrain-style-beach] OK")
	quit()
