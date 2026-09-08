extends SceneTree

var failures: Array[String] = []
func check(value: bool, message: String) -> void:
	if not value:
		failures.append(message)
		push_error(message)

func _initialize() -> void: call_deferred("run")

func run() -> void:
	root.size = Vector2i(1280, 800)
	root.content_scale_size = Vector2i(1280, 800)
	var scene: Node = load("res://addons/beep_game_builder_cs/templates/scenes/actors/actor_lab.tscn").instantiate()
	root.add_child(scene)
	current_scene = scene
	for i in 300:
		await process_frame
		if scene.ready_to_play: break
	check(scene.ready_to_play, "Actor lab did not finish generation")
	var registry: Node = scene.get_node("Registry")
	var player: Node = scene.get_node("Player")
	check(registry.ActorCount == 6, "Authored actor definition did not spawn six workers")
	check(player.PossessedActorId.is_empty(), "RTS demo created a player avatar")
	for actor_id in player.GetOwnedActors():
		var actor: Node = registry.FindActor(actor_id)
		var sprite: Sprite2D = actor.get_parent().get_node("Sprite2D")
		var extent := sprite.get_rect().size * sprite.scale
		check(extent.x <= 58.01 and extent.y <= 48.01, "Actor art exceeds definition size limits")
	scene.get_node("HUD/Toolbar/Row/Work").pressed.emit()
	for i in 15: await physics_frame
	scene.save_snapshot()
	player.IssueOrder(2, Vector2i.ZERO, "", false, "")
	scene.load_snapshot()
	for i in 420: await physics_frame
	if scene.get_node("Jobs").CompletedCount != 6:
		print("[actor lab] jobs ", scene.get_node("Jobs").GetJobs())
		for id in player.GetOwnedActors():
			var body: Node = registry.FindActor(id).get_parent()
			print("[actor lab] ", id, " position=", body.position, " worker=", body.get_node("GridWorker").State,
				" remaining=", body.get_node("GridWorker").WorkRemainingTurns, " orders=", registry.FindActor(id).HasOrders)
	check(scene.get_node("Jobs").CompletedCount == 6, "Restored workers did not complete all six jobs")
	scene.save_snapshot()
	scene.spawn_worker()
	check(registry.ActorCount == 7, "Recruit button did not add a worker")
	scene.load_snapshot()
	await process_frame
	check(registry.ActorCount == 6 and scene.get_node("Actors").get_child_count() == 6, "Restore retained an actor absent from the snapshot")
	var screen_bounds := Rect2(Vector2.ZERO, Vector2(root.size))
	for id in player.GetOwnedActors():
		var body: Node2D = registry.FindActor(id).get_parent()
		check(screen_bounds.has_point(body.get_global_transform_with_canvas().origin), "Worker outside the initial camera view")
		check(not scene.get_node("Navigation").IsBlocked(scene.get_node("Grid").WorldToCell(body.global_position)), "Worker finished on blocked terrain")
	check(scene.get_node("HUD/Toolbar").get_global_rect().end.x <= root.size.x, "Toolbar extends outside the viewport")
	if DisplayServer.get_name() != "headless":
		for i in 3:
			await process_frame
			RenderingServer.force_draw(false)
		var image := root.get_texture().get_image()
		DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path("res://tests/output/actors"))
		check(image != null and not image.is_empty(), "Actor lab viewport is blank")
		if image != null: image.save_png("res://tests/output/actors/actor_lab.png")
	var camera: Camera2D = scene.get_node("Camera2D")
	scene.get_node("Camera2D/Controller").set_process(false)
	scene.get_node("Camera2D/Controller").set_physics_process(false)
	camera.zoom = Vector2(0.15, 0.15)
	camera.force_update_scroll()
	var overview: Node = scene.get_node("Overview")
	overview.RefreshOverview()
	check(overview.IsOverviewActive and overview.MarkerCount == 6, "Authored lab overview did not show six owned workers")
	if DisplayServer.get_name() != "headless":
		await process_frame
		await RenderingServer.frame_post_draw
		root.get_texture().get_image().save_png("res://tests/output/actors/actor_lab_overview.png")
	scene.free()
	print("[actor lab] OK" if failures.is_empty() else "[actor lab] FAILED")
	quit(0 if failures.is_empty() else 1)
