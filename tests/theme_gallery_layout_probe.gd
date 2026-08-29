extends SceneTree

const SCENE_PATH := "res://addons/beep_game_builder_cs/templates/scenes/theme_gallery.tscn"

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	if not await _probe(Vector2i(1280, 720), "desktop"):
		return
	if not await _probe(Vector2i(390, 844), "mobile"):
		return

	print("[theme-gallery-layout] OK: theme gallery shell and picker controls fit desktop and mobile viewports.")
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
	var title := _control(scene, "Margin/VBox/TitleLabel")
	var header := _control(scene, "Margin/VBox/Header")
	var genre := _control(scene, "Margin/VBox/Header/GenreOption")
	var theme := _control(scene, "Margin/VBox/Header/ThemeOption")
	var palette := _control(scene, "Margin/VBox/Header/PaletteOption")
	var scroll := _control(scene, "Margin/VBox/ScrollFrame/Scroll")
	var type_section := _control(scene, "Margin/VBox/ScrollFrame/Scroll/Content/TypeSection")
	var type_frame := _control(scene, "Margin/VBox/ScrollFrame/Scroll/Content/TypeSection/Frame")
	if title == null or header == null or genre == null or theme == null or palette == null or scroll == null or type_section == null or type_frame == null:
		return false
	if scene.get_node_or_null("Margin/VBox/Header/TexturesCheck") != null:
		return _fail(label + " theme gallery must not expose a UI chrome texture toggle.")

	var viewport := Rect2(Vector2.ZERO, Vector2(size))
	for entry in [
		{ "name": label + " TitleLabel", "rect": title.get_global_rect() },
		{ "name": label + " Header", "rect": header.get_global_rect() },
		{ "name": label + " GenreOption", "rect": genre.get_global_rect() },
		{ "name": label + " ThemeOption", "rect": theme.get_global_rect() },
		{ "name": label + " PaletteOption", "rect": palette.get_global_rect() },
		{ "name": label + " Scroll", "rect": scroll.get_global_rect() },
	]:
		var r: Rect2 = entry["rect"]
		if r.position.x < -1.0 or r.end.x > viewport.end.x + 1.0:
			return _fail(entry["name"] + " overflows horizontally: " + str(r) + " viewport=" + str(viewport))
		if r.size.x <= 8.0 or r.size.y <= 8.0:
			return _fail(entry["name"] + " collapsed: " + str(r))

	if header.get_global_rect().position.y < title.get_global_rect().end.y - 1.0:
		return _fail(label + " theme gallery header overlaps title.")
	if scroll.get_global_rect().position.y < header.get_global_rect().end.y - 1.0:
		return _fail(label + " theme gallery scroll body overlaps picker header.")
	if not viewport.intersects(type_section.get_global_rect()):
		return _fail(label + " theme gallery does not show the first sample section in the initial viewport.")
	if str(type_frame.get("Title")) != "":
		return _fail(label + " first sample frame must not duplicate the page title as a panel plaque.")

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

func _fail(message: String) -> bool:
	push_error("[theme-gallery-layout] " + message)
	quit(1)
	return false
