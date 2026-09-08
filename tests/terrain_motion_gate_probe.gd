extends "res://tests/terrain_collision_probe.gd"

func _initialize() -> void:
	create_timer(30).timeout.connect(func():
		push_error("Terrain motion gate watchdog expired")
		quit(1))
	run.call_deferred()

func until_ready(predicate: Callable) -> void:
	var deadline := Time.get_ticks_msec() + 10000
	while not predicate.call() and Time.get_ticks_msec() < deadline: await process_frame
	assert(predicate.call(), "Terrain motion readiness timed out")

func run() -> void:
	var added_actions: Array[String] = []
	for name in ["move_left", "move_right", "move_up", "move_down"]:
		if not InputMap.has_action(name):
			InputMap.add_action(name)
			added_actions.append(name)
	var host := Node2D.new()
	root.add_child(host)
	var grid := make("grid/GridProjectionComponent", host, "Grid", {
		"TileSize": Vector2(16, 16), "TrackMouseCell": false, "DrawGrid": false})
	var cells := make("grid/GridCellDataComponent", host, "Cells", {})
	cells.SetTerrainKind(Vector2i(33, 16), "water")
	var collision := make("terrain/TerrainCollisionComponent", host, "Collision", {
		"GridPath": NodePath("../Grid"), "CellDataPath": NodePath("../Cells"),
		"RefreshOnReady": false, "BoundsSize": Vector2i(96, 32)})
	var archive := make("grid/GridCellArchiveComponent", host, "Archive", {
		"CellDataPath": NodePath("../Cells"),
		"ArchiveDirectory": "user://tests/motion_gate_%s_%s" % [OS.get_process_id(), Time.get_ticks_usec()]})
	var missing := Vector2i(1, 0)
	assert(archive.SaveChunk(missing) and archive.EvictSavedChunk(missing))
	collision.Rebuild()
	await settle()
	var body := CharacterBody2D.new()
	body.motion_mode = CharacterBody2D.MOTION_MODE_FLOATING
	body.collision_mask = 4
	body.position = grid.CellToWorld(Vector2i(30, 16))
	var shape := CollisionShape2D.new()
	shape.shape = CircleShape2D.new()
	shape.shape.radius = 4
	body.add_child(shape)
	host.add_child(body)
	var controller := make("TopDownController", body, "Controller", {"Friction": 0.0})
	controller.set_physics_process(false)
	var gate := make("terrain/TerrainMotionGateComponent", body, "Gate", {
		"GridPath": NodePath("../../Grid"), "CellDataPath": NodePath("../../Cells"), "CollisionPath": NodePath("../../Collision")})
	var start := body.position
	body.velocity = Vector2(3000, 0)
	controller._PhysicsProcess(1.0 / 60)
	assert(body.position == start and body.velocity == Vector2.ZERO, "Controller entered unloaded terrain")
	assert(gate.WaitReason == "terrain_loading" and cells.IsChunkPinned(missing), "Blocked motion did not create archive demand")
	shape.shape.radius = 40
	assert(not gate.PrepareMotion(Vector2.ZERO), "Large native shape crossed missing terrain without demand")
	shape.shape.radius = 4
	assert(gate.PrepareMotion(Vector2.ZERO) and not cells.IsChunkPinned(missing), "Stopping motion retained stale forward demand")
	var dash := make("DashComponent", body, "Dash", {"DashSpeed": 3000.0, "GrantIFrames": false})
	dash.set_physics_process(false)
	assert(dash.TryDash(Vector2.RIGHT))
	body.velocity = Vector2.ZERO
	controller._PhysicsProcess(1.0 / 60)
	assert(body.position == start and gate.WaitReason == "terrain_loading", "Dash bypassed terrain gate")
	dash.CancelDash()
	dash.free()
	var knockback := make("KnockbackComponent", body, "Knockback", {"Strength": 3000.0, "MaxKnockbackMagnitude": 3000.0})
	knockback.set_physics_process(false)
	knockback.ApplyKnockback(body.global_position - Vector2.RIGHT)
	controller._PhysicsProcess(1.0 / 60)
	assert(body.position == start and gate.WaitReason == "terrain_loading", "Knockback bypassed terrain gate")
	knockback.free()
	archive.AutoLoadPinnedChunks = true
	await until_ready(func():
		gate.PrepareMotion(Vector2(50, 0))
		return cells.IsChunkAvailable(missing))
	await until_ready(func():
		gate.PrepareMotion(Vector2(50, 0))
		return collision.IsChunkReady(missing))
	body.velocity = Vector2(3000, 0)
	controller._PhysicsProcess(1.0 / 60)
	assert(gate.WaitReason == "" and body.position.x > start.x, "Loaded terrain did not resume native movement")
	assert(body.position.x < grid.CellToWorld(Vector2i(33, 16)).x - 7, "Movement bypassed newly loaded water collision")
	# A live edit invalidates readiness immediately, before its deferred physics rebuild.
	cells.SetTerrainKind(Vector2i(33, 16), "grass")
	assert(not gate.PrepareMotion(Vector2(50, 0)) and gate.WaitReason == "collision_pending", "Stale collision was considered ready")
	await settle()
	assert(gate.PrepareMotion(Vector2(50, 0)), "Edited collision never became ready")
	body.velocity = Vector2(3000, 0)
	controller._PhysicsProcess(1.0 / 60)
	assert(body.position.x > grid.CellToWorld(Vector2i(33, 16)).x, "Cleared obstacle still blocked movement")
	gate.Enabled = false
	assert(not cells.HasChunkPins(gate), "Disabling gate leaked pins")
	gate.Enabled = true
	body.position = start
	assert(gate.PrepareMotion(Vector2.ZERO), "Reenabled gate did not bind")
	gate.CellDataPath = NodePath("../../Missing")
	assert(not gate.PrepareMotion(Vector2.RIGHT) and not cells.HasChunkPins(gate), "Missing source allowed movement or leaked pins")
	gate.CellDataPath = NodePath("../../Cells")
	gate.MaximumDemandChunks = 1
	assert(not gate.PrepareMotion(Vector2(400, 0)) and gate.WaitReason == "motion_demand_limit", "Oversized demand was not bounded")
	gate.MaximumDemandChunks = 16
	assert(gate.PrepareMotion(Vector2.ZERO))
	for i in 4: await physics_frame
	assert(not cells.HasChunkPins(gate), "Stopped controller retained speculative demand")
	assert(gate.PrepareMotion(Vector2.ZERO))
	body.remove_child(gate)
	assert(not cells.HasChunkPins(gate), "Detached gate retained pins")
	gate.free()
	archive.AutoLoadPinnedChunks = false
	await until_ready(func(): return not archive.IsBusy)
	DirAccess.remove_absolute(archive.GetChunkPath(missing))
	DirAccess.remove_absolute(ProjectSettings.globalize_path(archive.ArchiveDirectory))
	host.free()
	for name in added_actions: InputMap.erase_action(name)
	print("[terrain-motion-gate] controller block/resume, archive demand, physics readiness, live edits and pin lifecycle OK")
	quit()
