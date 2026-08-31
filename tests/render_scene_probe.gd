extends SceneTree

const SCENES := [
	{
		"path": "res://tests/examples/grid_world_kit_hud_example.tscn",
		"output": "res://tmp/grid_world_kit_hud_example.png",
		"min_pixels": 32,
	},
	{
		"path": "res://addons/beep_game_builder_cs/templates/scenes/theme_gallery.tscn",
		"output": "res://tmp/theme_gallery.png",
		"min_pixels": 128,
		"check": "theme_gallery",
	},
	{
		"path": "res://addons/beep_game_builder_cs/templates/scenes/hud.tscn",
		"output": "res://tmp/hud_template.png",
		"min_pixels": 24,
	},
]

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	root.size = Vector2i(1280, 720)
	for entry in SCENES:
		var ok := await _probe_scene(entry)
		if not ok:
			quit(1)
			return

	print("[render-probe] OK: rendered " + str(SCENES.size()) + " scenes.")
	quit(0)

func _probe_scene(entry: Dictionary) -> bool:
	var scene_path := str(entry.get("path", ""))
	var output_path := str(entry.get("output", ""))
	var packed := load(scene_path)
	if packed == null or not (packed is PackedScene):
		push_error("[render-probe] Could not load " + scene_path)
		return false

	var scene: Node = packed.instantiate()
	root.add_child(scene)
	for i in range(8):
		await process_frame

	if str(entry.get("check", "")) == "theme_gallery" and not _check_theme_gallery(scene):
		scene.queue_free()
		return false

	var image := root.get_texture().get_image()
	if image == null or image.is_empty():
		push_error("[render-probe] Viewport image is empty.")
		scene.queue_free()
		return false

	var non_empty_pixels := 0
	for y in range(0, image.get_height(), 16):
		for x in range(0, image.get_width(), 16):
			var color := image.get_pixel(x, y)
			if color.a > 0.01 and (color.r + color.g + color.b) > 0.04:
				non_empty_pixels += 1

	var min_pixels := int(entry.get("min_pixels", 32))
	if non_empty_pixels < min_pixels:
		push_error("[render-probe] Scene rendered too few visible pixels: " + scene_path + " = " + str(non_empty_pixels))
		scene.queue_free()
		return false

	var err := image.save_png(output_path)
	if err != OK:
		push_error("[render-probe] Could not save screenshot: " + str(err))
		scene.queue_free()
		return false

	print("[render-probe] OK: saved " + output_path + " with visible pixels " + str(non_empty_pixels))
	scene.queue_free()
	await process_frame
	return true

func _check_theme_gallery(scene: Node) -> bool:
	var option := scene.get_node_or_null("Margin/VBox/ScrollFrame/Scroll/Content/InputSection/InputMargin/InputVBox/InputRow/SampleOption")
	if option == null or option.get("item_count") < 3:
		push_error("[render-probe] Theme gallery SampleOption was not populated.")
		return false

	var items := scene.get_node_or_null("Margin/VBox/ScrollFrame/Scroll/Content/ListSection/ListMargin/ListVBox/ListRow/SampleItemList")
	if items == null or items.get("item_count") < 3:
		push_error("[render-probe] Theme gallery SampleItemList was not populated.")
		return false

	var tree := scene.get_node_or_null("Margin/VBox/ScrollFrame/Scroll/Content/ListSection/ListMargin/ListVBox/ListRow/SampleTree")
	if tree == null:
		push_error("[render-probe] Theme gallery SampleTree is missing.")
		return false

	var root_item: TreeItem = tree.get_root()
	if root_item == null or root_item.get_first_child() == null:
		push_error("[render-probe] Theme gallery SampleTree was not populated.")
		return false

	return true
