extends SceneTree

var failures: Array[String] = []
func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		push_error(message)
func _initialize() -> void: run.call_deferred()
func action(name: String, pressed: bool) -> void:
	var event := InputEventAction.new()
	event.action = name
	event.pressed = pressed
	Input.parse_input_event(event)
func run() -> void:
	root.size = Vector2i(1280, 800)
	root.content_scale_size = root.size
	var had_move := InputMap.has_action("move_right")
	var scene: Node = load("res://addons/beep_game_builder_cs/templates/scenes/actors/party_lab.tscn").instantiate()
	root.add_child(scene)
	current_scene = scene
	var deadline := Time.get_ticks_msec() + 60000
	while not scene.ready_to_play and Time.get_ticks_msec() < deadline: await process_frame
	check(scene.ready_to_play, "Party lab did not generate")
	if not scene.ready_to_play:
		scene.free()
		quit(1)
		return
	var player: Node = scene.get_node("Player")
	var registry: Node = scene.get_node("Registry")
	var row: Node = scene.get_node("HUD/Toolbar/Row")
	var members: Array = player.GetGroupActors(0)
	check(members.size() == 6 and not player.PossessedActorId.is_empty(), "Party scene did not establish its authored group")
	var initial: String = player.PossessedActorId
	for id in members:
		check(registry.FindActor(id).FollowTargetActorId == ("" if id == initial else initial), "Companion handoff was not active at startup")
	row.get_node("Next").pressed.emit()
	check(player.PossessedActorId != initial, "Next button did not change possession")
	row.get_node("Previous").pressed.emit()
	check(player.PossessedActorId == initial, "Previous button did not restore leader")
	row.get_node("Hold").pressed.emit()
	row.get_node("Next").pressed.emit()
	for id in members: check(not registry.FindActor(id).HasOrders or id == initial, "Manual companion hold was overridden")
	row.get_node("Regroup").pressed.emit()
	var leader: Node = registry.FindActor(player.PossessedActorId)
	var body: CharacterBody2D = leader.get_parent()
	var before := body.global_position
	# Exercise the actual input pipeline, not direct component calls.
	action("move_right", true)
	for i in 30: await physics_frame
	action("move_right", false)
	for i in 20: await physics_frame
	check(body.global_position.distance_to(before) > 12, "Keyboard input did not move the possessed native body")
	check(body.velocity.length() < 1, "Released movement remained latched")
	check(not scene.get_node("Camera2D/Controller").UseKeyboardPan, "Camera still competes for movement keys")
	var boundary_checked := false
	for cell in scene.available_cells:
		if boundary_checked: break
		for direction in [Vector2i.LEFT, Vector2i.RIGHT, Vector2i.UP, Vector2i.DOWN]:
			var adjacent: Vector2i = cell + direction
			if not scene.get_node("Navigation").IsInBounds(adjacent) or not scene.get_node("Navigation").IsBlocked(adjacent): continue
			var origin: Vector2 = scene.get_node("Grid").CellToWorld(cell)
			var destination: Vector2 = scene.get_node("Grid").CellToWorld(adjacent)
			check(body.test_move(Transform2D(0, origin), destination - origin), "Blocked terrain has no native body collision")
			boundary_checked = true
			break
	check(boundary_checked, "Party collision fixture did not contain a blocked boundary")
	var selected: String = player.PossessedActorId
	action("party_lab_next", true)
	await process_frame
	await process_frame
	action("party_lab_next", false)
	await process_frame
	check(player.PossessedActorId != selected, "Configured party shortcut did not switch actor")
	row.get_node("Save").pressed.emit()
	selected = player.PossessedActorId
	row.get_node("Hold").pressed.emit()
	row.get_node("Next").pressed.emit()
	row.get_node("Load").pressed.emit()
	check(player.PossessedActorId == selected, "Lab restore lost possession")
	for id in members:
		check(registry.FindActor(id).FollowTargetActorId == ("" if id == selected else selected), "Lab restore lost companion following")
	for i in 90: await physics_frame
	var occupied: Array[Rect2] = []
	for id in members:
		var actor: Node = registry.FindActor(id)
		check(not scene.get_node("Navigation").IsBlocked(scene.get_node("Grid").WorldToCell(actor.get_parent().global_position)), "Party member entered blocked terrain")
		var footprint: Vector2 = actor.Definition.Footprint
		var bounds := Rect2(actor.get_parent().global_position - footprint * 0.5, footprint)
		for previous in occupied: check(not bounds.intersects(previous), "Native party members still overlap after regrouping")
		occupied.append(bounds)
	for viewport_size in [Vector2i(1280, 800), Vector2i(768, 800)]:
		root.size = viewport_size
		root.content_scale_size = viewport_size
		for i in 4: await process_frame
		check(not scene.get_node("HUD/Toolbar").get_global_rect().intersects(scene.get_node("HUD/Status").get_global_rect()), "Party toolbar overlaps status")
		for control in row.get_children():
			if control is Control and control.visible:
				check(control.get_global_rect().end.x <= viewport_size.x, "Party toolbar control exceeds viewport")
		if DisplayServer.get_name() != "headless":
			await RenderingServer.frame_post_draw
			DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path("res://tests/output/actors"))
			root.get_texture().get_image().save_png("res://tests/output/actors/party_%d.png" % viewport_size.x)
	scene.free()
	check(InputMap.has_action("move_right") == had_move and not InputMap.has_action("party_lab_next"), "Party lab leaked its input bindings")
	print("[actor-party-lab] OK" if failures.is_empty() else "[actor-party-lab] FAILED")
	quit(0 if failures.is_empty() else 1)
