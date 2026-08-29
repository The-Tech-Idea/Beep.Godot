extends SceneTree

const KIT_BUTTON_SCRIPT := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitButton.cs")

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	root.size = Vector2i(420, 180)
	root.content_scale_size = root.size

	var bg := ColorRect.new()
	bg.color = Color(0.08, 0.08, 0.11, 1.0)
	bg.set_anchors_preset(Control.PRESET_FULL_RECT)
	root.add_child(bg)

	var flow := HFlowContainer.new()
	flow.position = Vector2(24, 56)
	flow.custom_minimum_size = Vector2(360, 80)
	flow.size = Vector2(360, 80)
	flow.add_theme_constant_override("h_separation", 10)
	flow.add_theme_constant_override("v_separation", 8)
	bg.add_child(flow)

	var buy := KIT_BUTTON_SCRIPT.new() as Button
	buy.text = "BUY"
	buy.set("BadgeText", "1200")
	flow.add_child(buy)

	var back := KIT_BUTTON_SCRIPT.new() as Button
	back.text = "BACK"
	flow.add_child(back)

	await process_frame
	await process_frame
	await process_frame
	RenderingServer.force_draw(false)

	var buy_min := buy.get_combined_minimum_size()
	var back_min := back.get_combined_minimum_size()
	if buy_min.x <= back_min.x:
		return _fail("Badge button minimum width does not reserve badge room: buy=" + str(buy_min) + " back=" + str(back_min))

	var buy_rect := buy.get_global_rect()
	var back_rect := back.get_global_rect()
	if back_rect.position.x <= buy_rect.end.x:
		return _fail("Flow layout overlapped adjacent buttons: buy=" + str(buy_rect) + " back=" + str(back_rect))

	var image := root.get_texture().get_image()
	if image == null or image.is_empty():
		return _fail("Viewport image is empty.")

	var gap_left := int(ceil(buy_rect.end.x + 1.0))
	var gap_right := int(floor(back_rect.position.x - 1.0))
	var top := int(max(0.0, buy_rect.position.y))
	var bottom := int(min(float(image.get_height() - 1), buy_rect.position.y + min(buy_rect.size.y, 24.0)))
	if gap_right <= gap_left:
		return _fail("No measurable gap between buttons: buy=" + str(buy_rect) + " back=" + str(back_rect))

	for y in range(top, bottom + 1):
		for x in range(gap_left, gap_right + 1):
			var color := image.get_pixel(x, y)
			if _differs(color, bg.color):
				return _fail("Badge or chrome painted into inter-button gap at " + str(Vector2i(x, y)) + ": " + str(color))

	print("[kit-button-badge] OK: badged KitButton reserves width and keeps badge paint inside its own rect.")
	quit(0)

func _differs(a: Color, b: Color) -> bool:
	return absf(a.r - b.r) + absf(a.g - b.g) + absf(a.b - b.b) + absf(a.a - b.a) > 0.08

func _fail(message: String) -> void:
	push_error("[kit-button-badge] " + message)
	quit(1)
