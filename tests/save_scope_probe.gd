extends SceneTree

const APP = preload("res://addons/beep_game_builder_cs/ecs/GameApp.cs")
const INFO = preload("res://addons/beep_game_builder_cs/core/GameInfo.cs")
const STORAGE = preload("res://addons/beep_game_builder_cs/ecs/grid/GridStorageComponent.cs")
var failures: Array[String] = []

func _initialize() -> void:
	call_deferred("run")

func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		print("[save-scope] FAIL: " + message)

func storage(parent: Node, key: String) -> Node:
	var node = STORAGE.new()
	node.set("SaveKey", key)
	parent.add_child(node)
	return node

func run() -> void:
	var directory = "user://save_scope_%s" % Time.get_ticks_usec()
	var info = INFO.new()
	info.set("SaveDirectory", directory)
	var app = APP.new()
	app.set("Info", info)
	root.add_child(app)
	var game := Node.new()
	game.name = "ScopeGame"
	root.add_child(game)
	current_scene = game
	var inside = storage(game, "inside")
	var outside = storage(root, "outside")
	var removed := Node.new()
	game.add_child(removed)
	storage(removed, "removed_child")
	var saves = app.get("Saves")
	app.call("StartNewSession", "scope")
	saves.call("BeginSession", "scope")
	await process_frame
	await process_frame
	app.call("AddSessionScore", 17)
	saves.call("SetGameData", "choice", {"recipe": "sawmill"})
	removed.queue_free()
	check(saves.call("Save", 0), "Could not save current scene")
	var saved = JSON.parse_string(FileAccess.get_file_as_string(directory + "/save_0.json"))
	check(saved.game_data.has("inside"), "Current scene was excluded")
	check(not saved.game_data.has("outside"), "Sibling scene leaked into snapshot")
	check(not saved.game_data.has("removed_child"), "Queued parent leaked its child into snapshot")
	check(saved.session.session_score == 17, "GameApp session was excluded")
	# Restore must honor the same scope, not apply an inside record to an unrelated scene.
	outside.set("SaveKey", "inside")
	outside.call("Load", "wood", 3)
	saves.call("RestoreAllSaveables")
	check(outside.call("Stored", "wood") == 3, "Restore mutated an unrelated scene")
	inside.free()
	check(saves.call("Save", 0), "Could not replace snapshot after removal")
	saved = JSON.parse_string(FileAccess.get_file_as_string(directory + "/save_0.json"))
	check(not saved.game_data.has("inside"), "Deleted component survived a fresh snapshot")
	check(saved.custom_data.choice.recipe == "sawmill", "Fresh snapshot lost explicit game data")
	check(saves.call("Load", 0), "Could not reload custom data")
	check(saves.call("GetGameData", "choice", {}).recipe == "sawmill", "Custom data did not round-trip")
	saves.call("BeginSession", "scope")
	await process_frame
	await process_frame
	var before = FileAccess.get_file_as_string(directory + "/save_0.json")
	saves.set("SaveRootPath", NodePath("missing_scope"))
	check(not saves.call("Save", 0), "Invalid scope accepted a snapshot")
	check(before == FileAccess.get_file_as_string(directory + "/save_0.json"), "Failed capture overwrote the save")
	# An explicit subtree replaces CurrentScene while retaining app session data.
	outside.set("SaveKey", "explicit")
	saves.set("SaveRootPath", outside.get_path())
	app.call("StartNewSession", "explicit")
	saves.call("BeginSession", "explicit")
	await process_frame
	await process_frame
	check(saves.call("Save", 0), "Could not save explicit scope")
	saved = JSON.parse_string(FileAccess.get_file_as_string(directory + "/save_0.json"))
	check(saved.game_data.has("explicit") and not saved.game_data.has("inside"), "Explicit scope was ignored")
	saves.call("DeleteSave", 0)
	outside.free()
	game.free()
	app.free()
	print("[save-scope] OK" if failures.is_empty() else "[save-scope] FAILED")
	quit(0 if failures.is_empty() else 1)
