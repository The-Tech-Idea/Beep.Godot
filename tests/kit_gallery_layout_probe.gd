extends SceneTree

const SCENE_PATH := "res://addons/beep_game_builder_cs/templates/scenes/kit_gallery.tscn"

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	if not await _probe(Vector2i(1280, 720), "desktop"):
		return
	if not await _probe(Vector2i(390, 844), "mobile"):
		return

	print("[kit-gallery-layout] OK: desktop action strip and mobile stacked layout fit without key overlaps.")
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

	var ok := true
	if label == "desktop":
		ok = _check_desktop(scene)
	else:
		ok = _check_mobile(scene, size)

	scene.queue_free()
	await process_frame
	return ok

func _check_desktop(scene: Node) -> bool:
	var build := _control(scene, "Margin/Scroll/Root/Row1/Actions/Build")
	var dial := _control(scene, "Margin/Scroll/Root/Row1/Actions/Dial")
	var actions := _control(scene, "Margin/Scroll/Root/Row1/Actions")
	var row2 := _control(scene, "Margin/Scroll/Root/Row2")
	var footer := _control(scene, "Margin/Scroll/Root/Footer")
	var buy := _control(scene, "Margin/Scroll/Root/Footer/BuyButton")
	var back := _control(scene, "Margin/Scroll/Root/Footer/BackButton")
	if build == null or dial == null or actions == null or row2 == null or footer == null or buy == null or back == null:
		return false

	var build_rect := build.get_global_rect()
	var dial_rect := dial.get_global_rect()
	var actions_rect := actions.get_global_rect()
	var row2_rect := row2.get_global_rect()
	var footer_rect := footer.get_global_rect()
	var buy_rect := buy.get_global_rect()
	var back_rect := back.get_global_rect()
	if actions_rect.size.x < 320.0:
		return _fail("Desktop Actions flow is too narrow: " + str(actions_rect.size.x))
	if absf(build_rect.position.y - dial_rect.position.y) > 8.0:
		return _fail("Desktop action controls wrapped vertically instead of staying in a usable strip.")
	if footer_rect.position.y - row2_rect.end.y > 32.0:
		return _fail("Desktop footer is detached from the component grid: row2=" + str(row2_rect) + " footer=" + str(footer_rect))
	if buy_rect.size.x < 88.0:
		return _fail("Desktop buy button is too narrow for its badge: " + str(buy_rect))
	if back_rect.position.x - buy_rect.end.x < 16.0:
		return _fail("Desktop footer buttons are too close for badge rendering: buy=" + str(buy_rect) + " back=" + str(back_rect))
	return true

func _check_mobile(scene: Node, viewport_size: Vector2i) -> bool:
	var title := _control(scene, "Margin/Scroll/Root/TitleLabel")
	var equipment := _control(scene, "Margin/Scroll/Root/Row2/Equipment")
	var weather := _control(scene, "Margin/Scroll/Root/Row2/Equipment/EquipmentContent/Weather")
	var bag := _control(scene, "Margin/Scroll/Root/Row2/Bag")
	if title == null or equipment == null or weather == null or bag == null:
		return false

	var viewport := Rect2(Vector2.ZERO, Vector2(viewport_size))
	for entry in [
		{ "name": "TitleLabel", "rect": title.get_global_rect() },
		{ "name": "Equipment", "rect": equipment.get_global_rect() },
		{ "name": "Weather", "rect": weather.get_global_rect() },
		{ "name": "Bag", "rect": bag.get_global_rect() },
	]:
		var r: Rect2 = entry["rect"]
		if r.position.x < -1.0 or r.end.x > viewport.end.x + 1.0:
			return _fail("Mobile " + entry["name"] + " overflows horizontally: " + str(r))
		if r.size.x <= 8.0 or r.size.y <= 8.0:
			return _fail("Mobile " + entry["name"] + " collapsed: " + str(r))

	var equipment_rect := equipment.get_global_rect()
	var weather_rect := weather.get_global_rect()
	var bag_rect := bag.get_global_rect()
	if weather_rect.end.y > equipment_rect.end.y - 8.0:
		return _fail("Mobile Weather card spills outside Equipment panel: weather=" + str(weather_rect) + " equipment=" + str(equipment_rect))
	if bag_rect.position.y < equipment_rect.end.y:
		return _fail("Mobile Bag overlaps Equipment panel: bag=" + str(bag_rect) + " equipment=" + str(equipment_rect))
	if not viewport.intersects(title.get_global_rect()) or not viewport.intersects(equipment_rect):
		return _fail("Mobile gallery does not show the title and first panel in the initial viewport.")
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
	push_error("[kit-gallery-layout] " + message)
	quit(1)
	return false
