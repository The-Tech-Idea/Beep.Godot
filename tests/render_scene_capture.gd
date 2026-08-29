extends SceneTree

const DEFAULT_SCENE_PATH := "res://addons/beep_game_builder_cs/templates/scenes/kit_gallery.tscn"
const DEFAULT_OUTPUT_PATH := "res://tmp/scene_capture.png"

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var trace := OS.get_environment("BEEP_CAPTURE_TRACE") == "1"
	if trace:
		print("[scene-capture] begin")
	var width := int(OS.get_environment("GODOT_CAPTURE_WIDTH"))
	var height := int(OS.get_environment("GODOT_CAPTURE_HEIGHT"))
	if width <= 0:
		width = 1280
	if height <= 0:
		height = 720
	root.size = Vector2i(width, height)
	root.content_scale_size = Vector2i(width, height)

	var scene_path := OS.get_environment("GODOT_CAPTURE_SCENE")
	var output_path := OS.get_environment("GODOT_CAPTURE_OUTPUT")
	if scene_path.is_empty():
		scene_path = DEFAULT_SCENE_PATH
	if output_path.is_empty():
		output_path = DEFAULT_OUTPUT_PATH
	var packed := load(scene_path)
	if packed == null or not (packed is PackedScene):
		push_error("[scene-capture] Could not load " + scene_path)
		quit(1)
		return

	if trace:
		print("[scene-capture] instantiate " + scene_path)
	var scene: Node = packed.instantiate()
	if trace:
		print("[scene-capture] add_child begin")
	root.add_child(scene)
	if trace:
		print("[scene-capture] add_child done")
		print("[scene-capture] paused=" + str(paused))
	if OS.get_environment("BEEP_CAPTURE_HIDE_SCENE") == "1" and scene is CanvasItem:
		(scene as CanvasItem).visible = false
		if trace:
			print("[scene-capture] scene hidden")
	paused = false
	for i in range(8):
		await process_frame
	for i in range(3):
		RenderingServer.force_draw(false)
		if trace:
			print("[scene-capture] forced draw " + str(i + 1))

	var image := root.get_texture().get_image()
	if image == null or image.is_empty():
		push_error("[scene-capture] Viewport image is empty.")
		quit(1)
		return

	var non_empty_pixels := 0
	for y in range(0, image.get_height(), 16):
		for x in range(0, image.get_width(), 16):
			var color := image.get_pixel(x, y)
			if color.a > 0.01 and (color.r + color.g + color.b) > 0.04:
				non_empty_pixels += 1

	if non_empty_pixels < 32:
		push_error("[scene-capture] Scene rendered too few visible pixels: " + str(non_empty_pixels))
		quit(1)
		return

	var err := image.save_png(output_path)
	if err != OK:
		push_error("[scene-capture] Could not save screenshot: " + str(err))
		quit(1)
		return

	print("[scene-capture] OK: saved " + output_path + " with visible pixels " + str(non_empty_pixels))
	quit(0)
