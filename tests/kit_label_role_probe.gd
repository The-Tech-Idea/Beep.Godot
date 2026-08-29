extends SceneTree

const THEME_PRESET_SCRIPT := preload("res://addons/beep_game_builder_cs/ecs/ui/ThemePresetComponent.cs")
const KIT_LABEL_SCRIPT := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitLabel.cs")
const ROLE_TITLE := 0
const ROLE_CAPTION := 4
const TYPOGRAPHY_META := "_beep_typography"

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	root.size = Vector2i(420, 180)
	root.content_scale_size = root.size

	var host := Control.new()
	host.name = "Host"
	host.set_anchors_preset(Control.PRESET_FULL_RECT)
	root.add_child(host)

	var label := KIT_LABEL_SCRIPT.new() as Label
	label.name = "TitleLabel"
	label.text = "Pump Station"
	label.set("AutoRole", false)
	label.set("Role", ROLE_CAPTION)
	host.add_child(label)

	var plain := Label.new()
	plain.name = "PlainTitle"
	plain.text = "Plain Title"
	host.add_child(plain)

	var theme := THEME_PRESET_SCRIPT.new()
	theme.name = "Theme"
	host.add_child(theme)

	await process_frame
	await process_frame

	if label.has_meta(TYPOGRAPHY_META):
		return _fail("ThemePresetComponent stamped typography metadata onto KitLabel.")
	if plain.has_meta(TYPOGRAPHY_META) == false:
		return _fail("ThemePresetComponent no longer styles ordinary Label nodes.")
	if label.has_theme_font_override("font") == false:
		return _fail("KitLabel did not create its local font override when the inherited theme matched.")
	if label.has_theme_font_size_override("font_size") == false:
		return _fail("KitLabel did not create its local font-size override.")
	if label.has_theme_color_override("font_color") == false:
		return _fail("KitLabel did not create its local font-color override when the inherited theme matched.")

	var caption_font_size := label.get_theme_font_size("font_size")
	var caption_min := label.get_combined_minimum_size()

	label.set("Role", ROLE_TITLE)
	await process_frame
	await process_frame

	var title_font_size := label.get_theme_font_size("font_size")
	var title_min := label.get_combined_minimum_size()

	if title_font_size <= caption_font_size:
		return _fail("Title role did not increase font size: caption=" + str(caption_font_size) + " title=" + str(title_font_size))
	if title_min.y <= caption_min.y:
		return _fail("Title role did not refresh minimum height: caption=" + str(caption_min) + " title=" + str(title_min))

	print("[kit-label-role] OK: KitLabel role changes refresh font size and minimum layout.")
	quit(0)

func _fail(message: String) -> void:
	push_error("[kit-label-role] " + message)
	quit(1)
