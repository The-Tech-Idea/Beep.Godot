extends SceneTree

const THEME_PRESET_SCRIPT := preload("res://addons/beep_game_builder_cs/ecs/ui/ThemePresetComponent.cs")
const KIT_COLOR_RECT_SCRIPT := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitColorRect.cs")

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	root.size = Vector2i(320, 180)
	root.content_scale_size = root.size

	var host := Control.new()
	host.name = "Host"
	host.set_anchors_preset(Control.PRESET_FULL_RECT)
	root.add_child(host)

	var theme := THEME_PRESET_SCRIPT.new()
	theme.name = "Theme"
	host.add_child(theme)

	var preserved := KIT_COLOR_RECT_SCRIPT.new() as ColorRect
	preserved.name = "PreservedTransparent"
	preserved.color = Color(0, 0, 0, 0)
	preserved.set("AutoFallback", false)
	host.add_child(preserved)

	var fallback := KIT_COLOR_RECT_SCRIPT.new() as ColorRect
	fallback.name = "FallbackTransparent"
	fallback.color = Color(0, 0, 0, 0)
	host.add_child(fallback)

	await process_frame
	await process_frame

	if preserved.color.a > 0.001:
		return _fail("KitColorRect changed a transparent authored ColorRect while AutoFallback was false.")
	if fallback.color.a <= 0.02:
		return _fail("KitColorRect did not apply a fallback colour to a blank ColorRect by default.")

	var first := fallback.color
	theme.call("ApplyTheme")
	await process_frame
	if fallback.color != first:
		return _fail("KitColorRect rewrote its fallback colour during a no-op theme reapply.")

	print("[kit-color-rect-fallback] OK: transparent authored ColorRects opt out, blank templates still get themed fallback.")
	quit(0)

func _fail(message: String) -> void:
	push_error("[kit-color-rect-fallback] " + message)
	quit(1)
