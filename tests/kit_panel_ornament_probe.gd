extends SceneTree

const KIT_PANEL_SCRIPT := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitPanel.cs")
const KIT_ORNAMENT_SCRIPT := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitOrnament.cs")

const META_GENERATED := "kit_archetype_ornament"

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	root.size = Vector2i(360, 240)
	root.content_scale_size = root.size

	var panel := KIT_PANEL_SCRIPT.new() as Control
	panel.name = "Panel"
	panel.size = Vector2(220, 140)

	var saved_generated := KIT_ORNAMENT_SCRIPT.new() as Control
	saved_generated.name = "PreviouslyGeneratedOrnament"
	saved_generated.visible = false
	saved_generated.set_meta(META_GENERATED, true)
	panel.add_child(saved_generated)

	root.add_child(panel)
	await process_frame
	await process_frame

	if not is_instance_valid(saved_generated) or saved_generated.get_parent() != panel:
		return _fail("KitPanel deleted an existing ornament while generation was disabled.")

	var plain_panel := KIT_PANEL_SCRIPT.new() as Control
	plain_panel.name = "PlainPanel"
	plain_panel.size = Vector2(220, 140)
	plain_panel.set("Archetype", 4) # Settings
	root.add_child(plain_panel)
	await process_frame
	await process_frame

	if _kit_ornament_count(plain_panel) != 0:
		return _fail("KitPanel created ornament children while generation was disabled.")

	var generated_panel := KIT_PANEL_SCRIPT.new() as Control
	generated_panel.name = "GeneratedPanel"
	generated_panel.visible = false
	generated_panel.size = Vector2(220, 140)
	generated_panel.set("GenerateOrnamentsWhenMissing", true)
	generated_panel.set("Archetype", 4) # Settings
	root.add_child(generated_panel)
	await process_frame
	await process_frame

	if _kit_ornament_count(generated_panel) != 1:
		return _fail("KitPanel did not create the explicit generated fallback ornament.")

	generated_panel.set("Archetype", 0) # None
	await process_frame
	await process_frame

	if _kit_ornament_count(generated_panel) != 0:
		return _fail("KitPanel did not clean explicit generated fallback ornaments when generation was enabled.")

	print("[kit-panel-ornament] OK: default panel archetypes do not create or delete children; explicit fallback still works.")
	quit(0)

func _kit_ornament_count(node: Node) -> int:
	var count := 0
	for child in node.get_children():
		if child.get_script() == KIT_ORNAMENT_SCRIPT:
			count += 1
	return count

func _fail(message: String) -> void:
	push_error("[kit-panel-ornament] " + message)
	quit(1)
