extends SceneTree

var failures: Array[String] = []
func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		push_error(message)

func _initialize() -> void:
	create_timer(40).timeout.connect(func(): push_error("Economy lab timed out"); quit(1))
	run.call_deferred()

func run() -> void:
	root.size = Vector2i(1280, 800)
	var scene: Node = load("res://addons/beep_game_builder_cs/templates/scenes/actors/economy_lab.tscn").instantiate()
	root.add_child(scene)
	current_scene = scene
	scene.clock.set_process(false)
	scene.clock.ScheduledWorkBudgetMilliseconds = 0.0
	scene.set_process(false)
	check(scene.simulation.ProductionCount == 3 and scene.wallet.GetAmount("wood") == 117, "Authored producers did not start")
	check(scene.world.get_node("Player").PossessedActorId.is_empty(), "Economy demo requires a possessed avatar")
	scene.controls.get_node("Residency").pressed.emit()
	await process_frame
	check(scene.registry.FindActor("mill_1") == null, "Unload button retained actor scene")
	scene.clock.AdvanceTurns(4.0)
	check(scene.wallet.GetAmount("plank") == 6, "Unloaded mill stopped producing")
	scene.controls.get_node("Residency").pressed.emit()
	check(scene.registry.FindActor("mill_1") != null, "Wake button failed")
	scene.controls.get_node("Pause").pressed.emit()
	scene.clock.AdvanceTurns(4.0)
	check(scene.wallet.GetAmount("plank") == 10, "Pause did not affect exactly one mill")
	scene.controls.get_node("Save").pressed.emit()
	var saved_planks: int = scene.wallet.GetAmount("plank")
	scene.controls.get_node("Remove").pressed.emit()
	check(scene.simulation.ProductionCount == 2, "Remove button left world production")
	scene.controls.get_node("Advance").pressed.emit()
	scene.controls.get_node("Restore").pressed.emit()
	check(scene.wallet.GetAmount("plank") == saved_planks and scene.simulation.ProductionCount == 3,
		"Snapshot did not restore economy and actors together")
	check(scene.simulation.GetProduction("mill_1").state == 2, "Snapshot lost paused state")
	scene.controls.get_node("Pause").pressed.emit()
	check(scene.simulation.GetProduction("mill_1").state == 1, "Resume button failed after restore")
	await process_frame
	scene.refresh()
	if DisplayServer.get_name() != "headless":
		await process_frame
		await RenderingServer.frame_post_draw
		DirAccess.make_dir_recursive_absolute("res://tests/output/economy_lab")
		root.get_texture().get_image().save_png("res://tests/output/economy_lab/desktop.png")
		root.size = Vector2i(900, 680)
		await process_frame
		await process_frame
		await RenderingServer.frame_post_draw
		root.get_texture().get_image().save_png("res://tests/output/economy_lab/compact.png")
	scene.free()
	print("[economy-lab] OK: authored controls, unload/wake, production, pause, removal and restore" if failures.is_empty() else "[economy-lab] FAILED")
	quit(0 if failures.is_empty() else 1)
