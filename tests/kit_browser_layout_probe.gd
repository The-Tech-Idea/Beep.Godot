extends SceneTree

const SCENE_PATH := "res://addons/beep_game_builder_cs/templates/scenes/kit_browser.tscn"

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	if not await _probe(Vector2i(1280, 720), "desktop"):
		return
	if not await _probe(Vector2i(390, 844), "mobile"):
		return

	print("[kit-browser-layout] OK: design-time browser shell fits desktop and mobile viewports.")
	quit(0)

func _probe(size: Vector2i, label: String) -> bool:
	root.size = size
	root.content_scale_size = size

	var packed := load(SCENE_PATH)
	if packed == null or not (packed is PackedScene):
		return _fail("Could not load " + SCENE_PATH)

	var scene := (packed as PackedScene).instantiate()
	root.add_child(scene)
	await process_frame
	await process_frame
	await process_frame

	var ok := _check(scene, size, label)
	scene.queue_free()
	await process_frame
	return ok

func _check(scene: Node, size: Vector2i, label: String) -> bool:
	var title := _control(scene, "Margin/Root/TitleLabel")
	var margin := _control(scene, "Margin")
	var root_box := _control(scene, "Margin/Root")
	var header := _control(scene, "Margin/Root/Header")
	var picker := _control(scene, "Margin/Root/Header/GenrePicker")
	var summary := _control(scene, "Margin/Root/SummaryLabel")
	var scroll := _control(scene, "Margin/Root/ScrollFrame/Scroll")
	var content := _control(scene, "Margin/Root/ScrollFrame/Scroll/Content")
	if title == null or margin == null or root_box == null or header == null or picker == null or summary == null or scroll == null or content == null:
		return false

	var viewport := Rect2(Vector2.ZERO, Vector2(size))
	for entry in [
		{ "name": label + " TitleLabel", "rect": title.get_global_rect() },
		{ "name": label + " Header", "rect": header.get_global_rect() },
		{ "name": label + " GenrePicker", "rect": picker.get_global_rect() },
		{ "name": label + " SummaryLabel", "rect": summary.get_global_rect() },
		{ "name": label + " Scroll", "rect": scroll.get_global_rect() },
	]:
		var r: Rect2 = entry["rect"]
		if r.position.x < -1.0 or r.end.x > viewport.end.x + 1.0:
			return _fail(entry["name"] + " overflows horizontally: " + str(r) + " viewport=" + str(viewport) + " " + _diagnostics(root_box, scroll, content))
		if r.size.x <= 8.0 or r.size.y <= 8.0:
			return _fail(entry["name"] + " collapsed: " + str(r))

	if label == "desktop":
		if header.get_global_rect().position.y < title.get_global_rect().end.y - 1.0:
			return _fail("Desktop browser toolbar overlaps the title.")
	else:
		if header.get_global_rect().position.y < title.get_global_rect().end.y - 1.0:
			return _fail("Mobile browser toolbar overlaps the wrapped title.")
		if summary.get_global_rect().position.y < picker.get_global_rect().end.y - 1.0:
			return _fail("Mobile browser summary overlaps the genre picker.")
		if content.get_global_rect().position.x < -1.0:
			return _fail("Mobile browser content starts outside the viewport: " + str(content.get_global_rect()))
		if content.get_global_rect().end.x > scroll.get_global_rect().end.x + 1.0:
			return _fail("Mobile browser content requires horizontal scrolling: content=" + str(content.get_global_rect()) + " scroll=" + str(scroll.get_global_rect()))
		if scroll.get_h_scroll_bar().visible:
			return _fail("Mobile browser must not show a horizontal scrollbar.")

	return true

func _control(scene: Node, path: NodePath) -> Control:
	var node := scene.get_node_or_null(path)
	if node == null:
		_fail("Missing control at " + str(path))
		return null
	if not (node is Control):
		_fail(str(path) + " is not a Control.")
		return null
	return node as Control

func _diagnostics(root_box: Control, scroll: Control, content: Control) -> String:
	var wide := PackedStringArray()
	for child in content.get_children():
		if child is Control:
			var c := child as Control
			if c.get_combined_minimum_size().x > 354.0:
				wide.append(str(c.name) + "=" + str(c.get_combined_minimum_size()) + _wide_descendants(c, 354.0, 2))
	return "root=" + str(root_box.get_global_rect()) + "/" + str(root_box.get_combined_minimum_size()) \
		+ " scroll=" + str(scroll.get_global_rect()) + "/" + str(scroll.get_combined_minimum_size()) \
		+ " content=" + str(content.get_global_rect()) + "/" + str(content.get_combined_minimum_size()) \
		+ " wide=[" + ", ".join(wide) + "]"

func _wide_descendants(node: Control, limit: float, depth: int) -> String:
	if depth <= 0:
		return ""
	var parts := PackedStringArray()
	for child in node.get_children():
		if child is Control:
			var c := child as Control
			if c.get_combined_minimum_size().x > limit:
				parts.append(str(c.name) + "=" + str(c.get_combined_minimum_size()) + _wide_descendants(c, limit, depth - 1))
	if parts.is_empty():
		return ""
	return "{ " + ", ".join(parts) + " }"

func _fail(message: String) -> bool:
	push_error("[kit-browser-layout] " + message)
	quit(1)
	return false
