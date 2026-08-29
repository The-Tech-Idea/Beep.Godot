extends SceneTree

const SCENE_PATH := "res://tests/examples/grid_world_kit_hud_example.tscn"

func _init() -> void:
	call_deferred("_run")

func _run() -> void:
	var packed := load(SCENE_PATH) as PackedScene
	if packed == null:
		_fail("Could not load " + SCENE_PATH)
		return

	var scene := packed.instantiate()
	root.add_child(scene)
	await process_frame
	await process_frame

	if not await _press_and_wait(scene, "HUD/HudRoot/ToolPalette/Panel/Row/Clear"):
		return
	_expect(not scene.get_node("WorldArt/TreePatch").visible, "Clear did not remove the brush patch.")
	_expect(_amount(scene, "wood") == 140, "Clear did not add the collected wood.")

	if not await _press_and_wait(scene, "HUD/HudRoot/ToolPalette/Panel/Row/Road"):
		return
	_expect(scene.get_node("WorldArt/RoadExtension").visible, "Road did not reveal the new road segment.")
	_expect(_amount(scene, "stone") == 31, "Road did not deduct stone.")

	if not await _press_and_wait(scene, "HUD/HudRoot/ToolPalette/Panel/Row/Plant"):
		return
	_expect(scene.get_node("WorldArt/PreparedPlots").visible, "Plant did not prepare the plots.")
	_expect(scene.get_node("WorldArt/CropPatch").visible, "Plant did not show crops.")

	scene.queue_free()
	print("[showcase-interaction] OK: clear, road, and plant dispatches visibly change the world.")
	quit(0)

func _press_and_wait(scene: Node, path: NodePath) -> bool:
	var button := scene.get_node(path) as Button
	if button == null:
		_fail("Missing button at " + str(path))
		return false
	button.pressed.emit()
	await create_timer(2.35).timeout
	return true

func _amount(scene: Node, resource_id: String) -> int:
	var wallet := scene.get_node_or_null("Resources")
	if wallet == null:
		_fail("Missing resource wallet.")
		return 0
	return int(wallet.call("GetAmount", resource_id))

func _expect(condition: bool, message: String) -> void:
	if not condition:
		_fail(message)

func _fail(message: String) -> void:
	push_error("[showcase-interaction] " + message)
	quit(1)
