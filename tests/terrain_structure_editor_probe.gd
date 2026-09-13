@tool
extends "res://tests/terrain_library_editor_probe.gd"

func _ready() -> void:
	if Engine.is_editor_hint() and OS.get_cmdline_user_args().has("--terrain-structure-editor-probe"):
		tree = get_tree()
		call_deferred("run")

func controls() -> Control:
	return find_control(EditorInterface.get_inspector(), "StructureControls")

func run() -> void:
	await settle()
	for attempt in range(300):
		if not EditorInterface.get_resource_filesystem().is_scanning(): break
		await tree.create_timer(0.1).timeout
	var access := EditorPlugin.new()
	var manager := access.get_undo_redo()
	for style in ["square", "iso"]:
		var source: String = OUTPUT + "structure_" + style + ".tscn"
		if not FileAccess.file_exists(source):
			check(false, "Run terrain_library_structure_probe.gd first")
			continue
		var fixture: PackedScene = ResourceLoader.load(source, "PackedScene", ResourceLoader.CACHE_MODE_IGNORE)
		var path: String = OUTPUT + "structure_editor_" + style + "_" + str(Time.get_ticks_usec()) + ".tscn"
		check(ResourceSaver.save(fixture, path) == OK, "Save isolated editor fixture")
		EditorInterface.get_selection().clear()
		EditorInterface.inspect_object(null)
		await settle()
		EditorInterface.open_scene_from_path(path)
		await settle()
		var scene := EditorInterface.get_edited_scene_root()
		if scene == null or scene.scene_file_path != path:
			check(false, "Structure editor fixture failed to open")
			continue
		var layer = scene.get_node("Structures")
		await inspect(layer)
		if not await press("BeginStructureEdit"): continue
		var field: LineEdit = controls().find_child("PlacementId", true, false)
		field.text = "editor_bridge"
		if not await press("StageStructure"): continue
		check(layer.has_node("StructureWorkingCopy/editor_bridge"), "Inspector Stage failed")
		check(not layer.has_node("editor_bridge"), "Stage published live structure")
		EditorInterface.save_scene_as(path, false)
		await settle()
		EditorInterface.get_selection().clear()
		EditorInterface.inspect_object(null)
		await settle()
		var saved: PackedScene = ResourceLoader.load(path, "PackedScene", ResourceLoader.CACHE_MODE_IGNORE)
		var reopened := saved.instantiate()
		tree.root.add_child(reopened)
		await settle()
		var restored = reopened.get_node("Structures")
		check(restored.StructureEditActive, "Disk save lost active structure session")
		check(restored.PrepareStructureApply().size() == 1, "Disk-reopened structure cannot Apply: " + restored.Problem)
		reopened.free()
		await inspect(layer)
		if not await press("ApplyStructures"): continue
		check(layer.has_node("editor_bridge") and not layer.StructureEditActive, "Inspector Apply failed")
		var history_id := manager.get_object_history_id(layer)
		check(history_id > 0, "Structure Apply must use scene undo history")
		var history := manager.get_history_undo_redo(history_id)
		check(history.get_current_action_name() == "Apply structure additions", "Incorrect structure history action")
		history.undo()
		await settle()
		check(layer.has_node("StructureWorkingCopy/editor_bridge") and layer.StructureEditActive, "Editor Undo failed")
		history.redo()
		await settle()
		check(layer.has_node("editor_bridge"), "Editor Redo failed")
		if not await press("BeginStructureEdit"): continue
		var live = layer.get_node("editor_bridge")
		var baseline: Transform2D = live.transform
		field = controls().find_child("PlacementId", true, false)
		var instances: OptionButton = controls().find_child("ExistingStructure", true, false)
		for i in range(instances.item_count):
			if instances.get_item_text(i) == "editor_bridge":
				instances.select(i)
				instances.item_selected.emit(i)
		check(field.text == "editor_bridge", "Instance selector did not load placement ID")
		check(controls().find_child("StructureX", true, false).value == 0, "Instance selector did not load anchor")
		controls().find_child("StructureX", true, false).value = 1
		controls().find_child("StructureY", true, false).value = 1
		if not await press("StageStructureMove"): continue
		check(not layer.GetStructureMovePreview().is_empty(), "Editor move preview geometry missing")
		check(live.transform == baseline, "Inspector staging moved live art")
		if not await press("ApplyStructures"): continue
		check(live.get_meta("terrain_structure_anchor") == Vector2i(1, 1), "Inspector move Apply failed")
		check(layer.GetStructureMovePreview().is_empty(), "Editor Apply retained move preview")
		check(history.get_current_action_name() == "Move terrain structure", "Move is not a scene undo action")
		history.undo()
		await settle()
		check(live.transform.is_equal_approx(baseline) and layer.PendingStructureMove.size() > 0, "Move Undo lost baseline or staging")
		history.redo()
		await settle()
		check(live.get_meta("terrain_structure_anchor") == Vector2i(1, 1), "Move Redo failed")
		if not await press("BeginStructureEdit"): continue
		field = controls().find_child("PlacementId", true, false)
		field.text = "discard_me"
		if not await press("StageStructure"): continue
		if not await press("DiscardStructures"): continue
		check(layer.has_node("editor_bridge") and not layer.has_node("StructureWorkingCopy/discard_me"), "Discard touched live or retained staged structure")
		EditorInterface.save_scene_as(path, false)
		var published: PackedScene = ResourceLoader.load(path, "PackedScene", ResourceLoader.CACHE_MODE_IGNORE)
		var final_copy := published.instantiate()
		check(final_copy.has_node("Structures/editor_bridge"), "Published structure lost on disk")
		check(not final_copy.get_node("Structures").StructureEditActive, "Discard state lost on disk")
		final_copy.free()
	access.free()
	for ref in hidden_controls:
		var control = ref.get_ref()
		if is_instance_valid(control): control.hide()
	print("TERRAIN STRUCTURE EDITOR: ", "PASS" if errors.is_empty() else "FAIL")
	tree.quit(0 if errors.is_empty() else 1)
