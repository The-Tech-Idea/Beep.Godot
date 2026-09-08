extends SceneTree
var failures: Array[String] = []
func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		push_error(message)
func _initialize() -> void: run.call_deferred()
func run() -> void:
	root.size = Vector2i(1280, 800)
	root.content_scale_size = root.size
	var scene: Node = load("res://addons/beep_game_builder_cs/templates/scenes/actors/actor_lab.tscn").instantiate()
	root.add_child(scene)
	current_scene = scene
	var deadline := Time.get_ticks_msec() + 60000
	while not scene.ready_to_play and Time.get_ticks_msec() < deadline: await process_frame
	check(scene.ready_to_play, "Escort lab did not generate")
	if not scene.ready_to_play:
		scene.free()
		quit(1)
		return
	var player: Node = scene.get_node("Player")
	var registry: Node = scene.get_node("Registry")
	var owned: Array = player.GetOwnedActors()
	owned.sort()
	var leader: String = owned[0]
	scene.get_node("HUD/Toolbar/Row/Escort").pressed.emit()
	check(player.PossessedActorId.is_empty() and player.GetSelectedActors() == [leader], "Escort altered avatar-free control or failed to select leader")
	for i in owned.size():
		check(registry.FindActor(owned[i]).FollowTargetActorId == ("" if i == 0 else owned[i - 1]), "Escort did not submit convoy commands")
	var original: Vector2 = registry.FindActor(owned[1]).get_parent().global_position
	var goal: Vector2i = scene.available_cells[mini(scene.available_cells.size() - 1, 80)]
	check(player.IssueOrder(0, goal, "", false, "") == 1, "Leader move rejected")
	for i in 200: await physics_frame
	check(registry.FindActor(owned[1]).get_parent().global_position.distance_to(original) > 16, "Companion did not move with leader")
	for id in owned:
		var body: Node2D = registry.FindActor(id).get_parent()
		check(not scene.get_node("Navigation").IsBlocked(scene.get_node("Grid").WorldToCell(body.global_position)), "Escort walked onto blocked terrain")
	scene.save_snapshot()
	scene.select_all()
	player.IssueOrder(2, Vector2i.ZERO, "", false, "")
	scene.load_snapshot()
	for i in 5: await physics_frame
	check(registry.FindActor(owned[1]).FollowTargetActorId == leader, "Lab snapshot lost follow order")
	scene.update_status()
	var centre := Vector2.ZERO
	for id in owned: centre += registry.FindActor(id).get_parent().global_position
	scene.get_node("Camera2D/Controller").FocusWorld(centre / owned.size(), true)
	check(not scene.get_node("HUD/Toolbar").get_global_rect().intersects(scene.get_node("HUD/Status").get_global_rect()), "Escort toolbar overlaps status")
	if DisplayServer.get_name() != "headless":
		await process_frame
		await RenderingServer.frame_post_draw
		DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path("res://tests/output/actors"))
		root.get_texture().get_image().save_png("res://tests/output/actors/escort.png")
	root.size = Vector2i(768, 800)
	root.content_scale_size = root.size
	for i in 4: await process_frame
	check(not scene.get_node("HUD/Toolbar").get_global_rect().intersects(scene.get_node("HUD/Status").get_global_rect()), "Narrow escort toolbar overlaps status")
	if DisplayServer.get_name() != "headless":
		await RenderingServer.frame_post_draw
		root.get_texture().get_image().save_png("res://tests/output/actors/escort_narrow.png")
	scene.free()
	print("[actor-escort-lab] OK" if failures.is_empty() else "[actor-escort-lab] FAILED")
	quit(0 if failures.is_empty() else 1)
