extends SceneTree

const KIT_CONTEXT_MENU := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitContextMenu.cs")

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var viewport_node := SubViewport.new()
	viewport_node.name = "SmallViewport"
	viewport_node.size = Vector2i(320, 240)
	viewport_node.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(viewport_node)

	var host := Control.new()
	host.name = "Host"
	host.theme = _probe_theme()
	host.set_meta("_beep_kit_genre", "citybuilder")
	viewport_node.add_child(host)

	var menu := KIT_CONTEXT_MENU.new() as Control
	menu.name = "LongContextMenu"
	host.add_child(menu)
	menu.call("SetItems", [
		"Inspect extremely long generated building configuration label",
		"Assign worker",
		"Cancel"
	])

	await process_frame
	await process_frame

	menu.call("PopupAt", Vector2(306, 20))
	await process_frame
	await process_frame

	var rect := menu.get_global_rect()
	var viewport := menu.get_viewport().get_visible_rect()
	if rect.position.x < 5.0 or rect.position.y < 5.0:
		return _fail("Context menu starts outside safe viewport margin: " + str(rect))
	if rect.end.x > viewport.end.x - 5.0 or rect.end.y > viewport.end.y - 5.0:
		return _fail("Context menu overflows viewport: rect=" + str(rect) + " viewport=" + str(viewport))
	if rect.size.x > viewport.size.x - 12.0:
		return _fail("Context menu width was not capped to viewport: rect=" + str(rect))

	print("[kit-context-menu-viewport] OK: long context menus cap width and clamp inside the viewport.")
	quit(0)

func _probe_theme() -> Theme:
	var theme := Theme.new()
	theme.set_color("neutral", "BeepSemantic", Color(0.09, 0.10, 0.11, 1.0))
	theme.set_color("accent", "BeepSemantic", Color(0.86, 0.54, 0.14, 1.0))
	theme.set_color("font_color", "Label", Color(0.94, 0.93, 0.88, 1.0))
	theme.set_font_size("font_size", "Label", 16)
	return theme

func _fail(message: String) -> void:
	push_error("[kit-context-menu-viewport] " + message)
	quit(1)
