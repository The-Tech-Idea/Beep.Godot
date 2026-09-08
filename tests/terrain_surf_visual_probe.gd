extends SceneTree

const SHADER := "res://addons/beep_game_builder_cs/shaders/terrain_splat.gdshader"
const SHARED := "res://addons/beep_game_builder_cs/shaders/water_common.gdshaderinc"

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
	preview.scale = Vector2.ONE
	preview.position = Vector2(640, 400) - Vector2(16, 8) * 64.0
	var painted: Node = scene.get_node("Preview/Splat")
	var material: ShaderMaterial = painted.get_node("SplatSurface").material
	var production := FileAccess.get_file_as_string(SHADER).replace('#include "' + SHARED + '"', FileAccess.get_file_as_string(SHARED))
	var shader := Shader.new()
	shader.code = production.replace("TIME", "capture_time").replace("shader_type canvas_item;", "shader_type canvas_item;\nuniform float capture_time = 0.0;")
	material.shader = shader
	var cells: Node = scene.get_node("Preview/Cells")
	var snapshot := var_to_bytes(cells.call("GetCells"))
	var maps: Dictionary = {}
	for slot in ["id_map", "shade_map", "coast_map"]: maps[slot] = material.get_shader_parameter(slot)
	DirAccess.make_dir_recursive_absolute("res://tests/output/surf")
	for spacing in [1.6, 6.4, 8.0]:
		material.set_shader_parameter("foam_tiles_across", spacing)
		for time in [0.0, 4.0, 8.0]:
			material.set_shader_parameter("capture_time", time)
			await process_frame
			await RenderingServer.frame_post_draw
			assert(root.get_texture().get_image().save_png("res://tests/output/surf/spacing-%s-time-%s.png" % [spacing, time]) == OK)
	for slot in maps: assert(material.get_shader_parameter(slot) == maps[slot])
	assert(var_to_bytes(cells.call("GetCells")) == snapshot)
	scene.free()
	print("[terrain-surf-visual] OK")
	quit()
