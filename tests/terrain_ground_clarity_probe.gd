extends SceneTree

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	assert(DisplayServer.get_name() != "headless")
	root.size = Vector2i(1280, 800)
	var scene: Node = load("res://addons/beep_game_builder_cs/templates/scenes/terrain/terrain_generator_lab.tscn").instantiate()
	scene.get_node("World").set("MapSize", 0)
	scene.get_node("World").set("Seed", 31415)
	root.add_child(scene)
	await process_frame
	await process_frame
	scene.set_process(false)
	scene.get_node("HUD").hide()
	scene.get_node("Preview/MapOverlay").hide()
	var preview: Node2D = scene.get_node("Preview")
	var painted: Node = scene.get_node("Preview/Splat")
	var material: ShaderMaterial = painted.get_node("SplatSurface").material
	var shared_path := "res://addons/beep_game_builder_cs/shaders/water_common.gdshaderinc"
	var production := FileAccess.get_file_as_string("res://addons/beep_game_builder_cs/shaders/terrain_splat.gdshader").replace('#include "' + shared_path + '"', FileAccess.get_file_as_string(shared_path))
	var shader := Shader.new()
	shader.code = production.replace("TIME", "0.0").replace("shader_type canvas_item;", "shader_type canvas_item;\nuniform float clarity_bias = 0.0;").replace("vec3 colour = textureGrad(albedo, uv, dx, dy).rgb;", "dx *= exp2(clarity_bias); dy *= exp2(clarity_bias); vec3 colour = textureGrad(albedo, uv, dx, dy).rgb;")
	material.shader = shader
	var cells: Node = scene.get_node("Preview/Cells")
	var snapshot := var_to_bytes(cells.call("GetCells"))
	var maps: Dictionary = {}
	for slot in ["id_map", "shade_map", "coast_map"]: maps[slot] = material.get_shader_parameter(slot)
	DirAccess.make_dir_recursive_absolute("res://tests/output/ground_clarity")
	for variant in ["baseline", "bias_half", "bias_one", "neutral", "edges"]:
		material.set_shader_parameter("clarity_bias", 0.0 if variant == "baseline" else (-1.0 if variant == "bias_one" else -0.5))
		var neutral: bool = variant in ["neutral", "edges"]
		material.set_shader_parameter("tint_rock", Vector3.ONE if neutral else Vector3(0.78, 0.78, 0.78))
		material.set_shader_parameter("tint_dry_grass", Vector3.ONE if neutral else Vector3(0.80, 0.84, 0.56))
		material.set_shader_parameter("blend_width", 0.20 if variant == "edges" else 0.42)
		for zoom in [0.36, 1.0]:
			preview.scale = Vector2.ONE * zoom
			var focus := Vector2(16, 16) if zoom < 1.0 else Vector2(16, 8)
			preview.position = Vector2(640, 400) - focus * 64.0 * zoom
			await process_frame
			await RenderingServer.frame_post_draw
			assert(root.get_texture().get_image().save_png("res://tests/output/ground_clarity/%s-zoom-%s.png" % [variant, zoom]) == OK)
	for slot in maps: assert(material.get_shader_parameter(slot) == maps[slot])
	assert(var_to_bytes(cells.call("GetCells")) == snapshot)
	scene.free()
	print("[terrain-ground-clarity] OK")
	quit()
