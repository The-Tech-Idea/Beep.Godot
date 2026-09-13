@tool
extends Node

const OUTPUT = "res://addons/beep_game_builder_cs/generated/test/library/output/"
var errors: Array[String] = []
var tree: SceneTree
var hidden_controls: Array[WeakRef] = []

func _ready() -> void:
	if Engine.is_editor_hint() and OS.get_cmdline_user_args().has("--terrain-library-editor-probe"):
		tree = get_tree()
		call_deferred("run")

func check(ok: bool, reason: String) -> void:
	if not ok:
		errors.append(reason)
		push_error(reason)

func settle() -> void:
	await tree.create_timer(0.6).timeout

func controls() -> Control:
	return find_control(EditorInterface.get_inspector(), "TerrainLibraryControls")

func find_control(node: Node, wanted: String) -> Control:
	if node is Control and node.name == wanted: return node
	for child in node.get_children(true):
		var found := find_control(child, wanted)
		if found != null: return found
	return null

func press(name: String) -> bool:
	var panel := controls()
	if panel == null:
		check(false, "Terrain Inspector controls missing")
		return false
	var button: Button = panel.find_child(name, true, false)
	if button == null or button.disabled:
		check(false, "Terrain button missing or disabled: " + name)
		return false
	button.pressed.emit()
	await settle()
	return true

func inspect(node: Node) -> void:
	var inspector := EditorInterface.get_inspector()
	var ancestor: Node = inspector
	while ancestor != null:
		if ancestor is Control and not ancestor.visible:
			hidden_controls.append(weakref(ancestor))
			ancestor.show()
		ancestor = ancestor.get_parent()
	EditorInterface.get_selection().clear()
	EditorInterface.get_selection().add_node(node)
	EditorInterface.edit_node(node)
	await settle()

func run() -> void:
	await settle()
	for attempt in range(300):
		if not EditorInterface.get_resource_filesystem().is_scanning(): break
		await tree.create_timer(0.1).timeout
	var history_access := EditorPlugin.new()
	var manager := history_access.get_undo_redo()
	var run_id := str(Time.get_ticks_usec())
	for style in ["square", "iso"]:
		var source: String = OUTPUT + "elevation_" + style + ".tscn"
		if not FileAccess.file_exists(source):
			check(false, "Run terrain_library_elevation_probe.gd before the editor probe: " + source)
			continue
		var scene: PackedScene = ResourceLoader.load(source, "PackedScene", ResourceLoader.CACHE_MODE_IGNORE)
		var path: String = OUTPUT + "editor_review_" + style + "_" + run_id + ".tscn"
		check(ResourceSaver.save(scene, path) == OK, "Could not create disposable editor fixture")
		EditorInterface.get_selection().clear()
		EditorInterface.inspect_object(null)
		await settle()
		EditorInterface.open_scene_from_path(path)
		await settle()
		var root := EditorInterface.get_edited_scene_root()
		if root == null or root.scene_file_path != path:
			check(false, "Editor did not open fixture: " + path)
			continue
		var cells = root.get_node("Cells")
		var renderer = root.get_node("Renderer")
		var session = renderer.get_node("TerrainEditSession")
		var point := Vector2i(1, 1)
		check(session.Active, "Pending session did not reopen")
		check(cells.GetStoredChunks().size() > 0, "Editor-only seed did not restore empty cell store")
		check(cells.GetMetadata(point, "terrain_library_elevation") == "half", "Editor seed lost baseline profile")
		check(session.WorkingLayer.get_cell_tile_data(point).get_custom_data("elevation_profile") == "standard", "Pending elevation was lost")
		await inspect(renderer)
		if not await press("BeginTerrainEdit"): continue
		check(EditorInterface.get_inspector().get_edited_object() == session.WorkingLayer, "Edit did not select native working layer")
		if not await press("ApplyTerrainEdit"): continue
		check(not session.Active and cells.GetMetadata(point, "terrain_library_elevation") == "standard", "Inspector Apply did not commit")
		check(controls().find_child("StageElevation", true, false).disabled, "Elevation staging enabled outside an edit session")
		var history_id := manager.get_object_history_id(session)
		check(history_id > 0, "Apply was not assigned to scene history")
		var history := manager.get_history_undo_redo(history_id)
		check(history.get_current_action_name() == "Apply terrain edits", "Wrong editor undo action")
		history.undo()
		await settle()
		check(session.Active and cells.GetMetadata(point, "terrain_library_elevation") == "half", "Editor Undo failed")
		history.redo()
		await settle()
		check(not session.Active and cells.GetMetadata(point, "terrain_library_elevation") == "standard", "Editor Redo failed")
		if not await press("BeginTerrainEdit"): continue
		var profile: OptionButton = controls().find_child("ElevationProfile", true, false)
		for i in range(profile.item_count):
			if profile.get_item_metadata(i) == "quarter": profile.select(i)
		if not await press("StageElevation"): continue
		check(session.WorkingLayer.get_cell_tile_data(Vector2i.ZERO).get_custom_data("elevation_profile") == "quarter", "Inspector elevation staging failed")
		check(cells.GetMetadata(Vector2i.ZERO, "terrain_library_elevation") == null, "Staging touched live cells")
		history.undo()
		await settle()
		check(session.WorkingLayer.get_cell_tile_data(Vector2i.ZERO).get_custom_data("elevation_profile") == "", "Staging Undo failed")
		history.redo()
		await settle()
		# Save through the editor notification path, not only ResourceSaver.
		cells.SetMetadata(Vector2i.ZERO, "inventory_note", "newer-editor-value")
		EditorInterface.save_scene_as(path, false)
		await settle()
		EditorInterface.get_selection().clear()
		EditorInterface.inspect_object(null)
		await settle()
		var saved: PackedScene = ResourceLoader.load(path, "PackedScene", ResourceLoader.CACHE_MODE_IGNORE)
		var reopened := saved.instantiate()
		tree.root.add_child(reopened)
		await settle()
		var restored = reopened.get_node("Renderer/TerrainEditSession")
		check(restored.Active, "Editor save lost pending state")
		check(reopened.get_node("Cells").GetMetadata(Vector2i.ZERO, "inventory_note") == "newer-editor-value", "Editor pre-save seed missed latest gameplay data")
		check(not restored.PrepareApply().is_empty(), "Editor-saved pending copy cannot Apply: " + restored.Problem)
		reopened.free()
		await inspect(session.WorkingLayer)
		if not await press("DiscardTerrainEdit"): continue
		check(not session.Active and cells.GetMetadata(Vector2i.ZERO, "terrain_library_elevation") == null, "Discard applied pending elevation")
		EditorInterface.save_scene_as(path, false)
		var discarded: Node = ResourceLoader.load(path, "PackedScene", ResourceLoader.CACHE_MODE_IGNORE).instantiate()
		check(not discarded.get_node("Renderer/TerrainEditSession").Active, "Discarded state was not saved")
		discarded.free()
	history_access.free()
	for ref in hidden_controls:
		var control = ref.get_ref()
		if is_instance_valid(control): control.hide()
	print("TERRAIN LIBRARY EDITOR: ", "PASS" if errors.is_empty() else "FAIL")
	tree.quit(0 if errors.is_empty() else 1)
