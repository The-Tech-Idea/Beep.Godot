extends SceneTree

var failures: Array[String] = []
func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		push_error(message)
func _initialize() -> void: run.call_deferred()
func capture(name: String) -> void:
	if DisplayServer.get_name() == "headless": return
	await process_frame
	await RenderingServer.frame_post_draw
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path("res://tests/output/streaming_lab"))
	root.get_texture().get_image().save_png("res://tests/output/streaming_lab/" + name + ".png")
func run() -> void:
	root.size = Vector2i(1280, 800)
	root.content_scale_size = root.size
	var scene: Node = load("res://addons/beep_game_builder_cs/templates/scenes/actors/streaming_lab.tscn").instantiate()
	root.add_child(scene)
	current_scene = scene
	var deadline := Time.get_ticks_msec() + 120000
	while not scene.ready_to_play and Time.get_ticks_msec() < deadline: await process_frame
	check(scene.ready_to_play, "Streaming lab generation failed")
	if not scene.ready_to_play:
		scene.free()
		quit(1)
		return
	check(scene.get_node("World").BuiltSize == Vector2i(300, 256), "Lab did not generate its large world")
	check(scene.get_node("Registry").ActorCount == 6, "Streaming lab has no owned workers")
	check(scene.get_node("Player").PossessedActorId.is_empty(), "Streaming lab requires an avatar")
	var cells: Node = scene.get_node("Cells")
	var archive: Node = scene.get_node("Archive")
	var demand: Node = scene.get_node("CameraDemand")
	deadline = Time.get_ticks_msec() + 20000
	while cells.EvictedChunkCount < 2 and Time.get_ticks_msec() < deadline: await process_frame
	check(cells.EvictedChunkCount >= 2, "Lab did not retire any idle chunks")
	check(not scene.get_node("HUD/Toolbar").get_global_rect().intersects(scene.get_node("HUD/Status").get_global_rect()), "Worker status overlaps toolbar")
	await capture("workers")
	scene.fit_view()
	for i in 8: await process_frame
	check(demand.IsUsingOverview, "Fit map did not hand camera demand to the overview")
	await capture("overview")
	var target := Vector2i.ZERO
	var found := false
	for y in 8:
		for x in 10:
			if cells.IsChunkEvicted(Vector2i(x, y)):
				target = Vector2i(x, y)
				found = true
				break
		if found: break
	check(found, "No archived chunk available for camera reload")
	archive.AutoEnforceChunkBudget = false
	scene.get_node("Camera2D/Controller").FocusWorld(scene.get_node("Grid").CellToWorld(target * 32 + Vector2i(16, 16)), true)
	scene.get_node("Camera2D/Controller").SetZoomLevel(0.8, true)
	deadline = Time.get_ticks_msec() + 20000
	while cells.IsChunkEvicted(target) and Time.get_ticks_msec() < deadline: await process_frame
	check(not cells.IsChunkEvicted(target), "Camera did not reload the archived area")
	check(not demand.IsUsingOverview, "Detail camera retained overview-only demand")
	check(scene.get_node("Registry").ActorCount == 6, "Camera culling removed owned actors")
	await capture("reloaded")
	root.size = Vector2i(768, 800)
	root.content_scale_size = root.size
	for i in 4: await process_frame
	check(not scene.get_node("HUD/Toolbar").get_global_rect().intersects(scene.get_node("HUD/Status").get_global_rect()), "Wrapped toolbar overlaps worker status")
	check(scene.get_node("HUD/Toolbar").get_global_rect().end.x <= root.size.x, "Wrapped toolbar exceeds window")
	await capture("narrow")
	archive.AutoLoadPinnedChunks = false
	deadline = Time.get_ticks_msec() + 10000
	while archive.IsBusy and Time.get_ticks_msec() < deadline: await process_frame
	check(not archive.IsBusy, "Lab archive did not finish pending I/O")
	var directory: String = ProjectSettings.globalize_path(archive.ArchiveDirectory)
	# This fixture owns the unique session directory; never touch other lab sessions.
	if not archive.IsBusy:
		for file in DirAccess.get_files_at(directory): DirAccess.remove_absolute(directory.path_join(file))
		DirAccess.remove_absolute(directory)
	scene.free()
	print("[streaming lab] OK" if failures.is_empty() else "[streaming lab] FAILED")
	quit(0 if failures.is_empty() else 1)
