extends SceneTree

const KIT_CHECK_BOX_SCRIPT := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitCheckBox.cs")
const KIT_CHECK_BUTTON_SCRIPT := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitCheckButton.cs")

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	root.size = Vector2i(560, 160)
	root.content_scale_size = root.size

	var bg := ColorRect.new()
	bg.color = Color(0.02, 0.03, 0.03, 1.0)
	bg.set_anchors_preset(Control.PRESET_FULL_RECT)
	root.add_child(bg)

	var host := Control.new()
	host.name = "Host"
	host.set_anchors_preset(Control.PRESET_FULL_RECT)
	host.theme = _probe_theme()
	host.set_meta("_beep_kit_genre", "citybuilder")
	bg.add_child(host)

	var checkbox := KIT_CHECK_BOX_SCRIPT.new() as CheckBox
	checkbox.name = "UncheckedBox"
	checkbox.text = "Textures"
	checkbox.button_pressed = false
	checkbox.position = Vector2(32, 48)
	checkbox.size = Vector2(190, 48)
	host.add_child(checkbox)

	var switch := KIT_CHECK_BUTTON_SCRIPT.new() as CheckButton
	switch.name = "UncheckedSwitch"
	switch.text = "Effects"
	switch.button_pressed = false
	switch.position = Vector2(270, 46)
	switch.size = Vector2(240, 52)
	host.add_child(switch)

	await process_frame
	await process_frame
	await process_frame
	await process_frame
	RenderingServer.force_draw(false)

	var image := root.get_texture().get_image()
	if image == null or image.is_empty():
		return _fail("Viewport image is empty.")

	var checkbox_lum := _checkbox_box_luminance(image, checkbox)
	if checkbox_lum < 0.14:
		return _fail("Unchecked KitCheckBox well is too dark: luminance=" + str(checkbox_lum))

	var switch_lum := _switch_track_luminance(image, switch)
	if switch_lum < 0.14:
		return _fail("Unchecked KitCheckButton track is too dark: luminance=" + str(switch_lum))

	print("[kit-check-controls-contrast] OK: unchecked checkbox and switch wells stay readable on dark skins.")
	quit(0)

func _probe_theme() -> Theme:
	var theme := Theme.new()
	theme.set_color("neutral", "BeepSemantic", Color(0.035, 0.055, 0.055, 1.0))
	theme.set_color("accent", "BeepSemantic", Color(0.86, 0.54, 0.14, 1.0))
	theme.set_color("success", "BeepSemantic", Color(0.28, 0.62, 0.40, 1.0))
	theme.set_color("font_color", "Label", Color(0.94, 0.93, 0.88, 1.0))
	theme.set_font_size("font_size", "Label", 16)
	return theme

func _checkbox_box_luminance(image: Image, checkbox: CheckBox) -> float:
	var r := checkbox.get_global_rect()
	var fs: int = max(1, checkbox.get_theme_font_size("font_size"))
	var b: float = clamp(float(fs) * 1.08, 15.0, 22.0)
	var box := Rect2(
		r.position + Vector2(max(1.0, float(fs) * 0.10), (r.size.y - b) * 0.5),
		Vector2(b, b)
	)
	return _average_luminance(image, box.grow(-b * 0.30))

func _switch_track_luminance(image: Image, check_button: CheckButton) -> float:
	var r := check_button.get_global_rect()
	var fs: int = max(1, check_button.get_theme_font_size("font_size"))
	var h: float = max(22.0, float(fs) * 1.5)
	var w: float = h * 2.05
	var track := Rect2(
		r.position + Vector2(r.size.x - w - 2.0, (r.size.y - h) * 0.5),
		Vector2(w, h)
	)
	var sample := Rect2(
		track.position + Vector2(w * 0.60, h * 0.30),
		Vector2(w * 0.24, h * 0.40)
	)
	return _average_luminance(image, sample)

func _average_luminance(image: Image, rect: Rect2) -> float:
	var left := clampi(int(floor(rect.position.x)), 0, image.get_width() - 1)
	var right := clampi(int(ceil(rect.end.x)), 0, image.get_width() - 1)
	var top := clampi(int(floor(rect.position.y)), 0, image.get_height() - 1)
	var bottom := clampi(int(ceil(rect.end.y)), 0, image.get_height() - 1)
	var total := 0.0
	var count := 0
	for y in range(top, bottom + 1):
		for x in range(left, right + 1):
			var c := image.get_pixel(x, y)
			total += c.r * 0.2126 + c.g * 0.7152 + c.b * 0.0722
			count += 1
	if count == 0:
		return 0.0
	return total / float(count)

func _fail(message: String) -> void:
	push_error("[kit-check-controls-contrast] " + message)
	quit(1)
