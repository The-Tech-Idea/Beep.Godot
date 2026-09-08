extends SceneTree

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	var scene = load("res://tests/examples/terrain_grid_playground.tscn").instantiate()
	scene.checkpoint_path = "user://terrain_grid_playground_probe.save"
	root.add_child(scene)
	await process_frame
	await process_frame
	var follower = scene.get_node("World/Truck/PathFollower")
	var icons = scene.get_node("World/Resources")
	var cells = scene.get_node("World/Cells")
	assert(scene.get_node("WorldBuilder").get("BuiltSize") == Vector2i(32, 32))
	assert(icons.get("IconCount") == 1, "Live deposit marker missing")
	var collision = scene.get_node("World/TerrainCollision")
	assert(collision.get("ShapeCount") > 0, "World did not build authored terrain collision")
	assert(collision.get_node(collision.get("GridPath")) == scene.get_node("World/Grid"))
	assert(collision.get_node(collision.get("CellDataPath")) == cells)
	if "--capture" in OS.get_cmdline_user_args():
		await RenderingServer.frame_post_draw
		var truck_screen: Vector2 = scene.get_node("World/Truck").get_global_transform_with_canvas().origin
		assert(root.get_visible_rect().grow(-60).has_point(truck_screen), "Starting truck is outside camera frame")
		DirAccess.make_dir_recursive_absolute("res://tests/output/terrain_playground")
		root.get_texture().get_image().save_png("res://tests/output/terrain_playground/start.png")
	scene.get_node("HUD/Toolbar/Gather").pressed.emit()
	assert(scene.act_at(scene.deposit_cell), "Authored Gather action failed")
	assert(scene.capture_checkpoint().is_empty(), "Checkpoint captured an unsupported in-flight route")
	# This scene uses CharacterBody2D.MoveAndSlide, which advances on physics time,
	# not the delta passed by a manual call to AdvancePath.
	for step in range(180):
		await physics_frame
		if not follower.get("IsMoving"): break
	if scene.get_node("World/Wallet").call("GetAmount", "stone") != 3:
		print("[playground diagnostic] status=", scene.get_node("HUD/ActionStatus").text,
			" moving=", follower.get("IsMoving"), " from=", scene.spawn_cell,
			" to=", scene.deposit_cell, " truck=", scene.get_node("World/Truck").global_position)
	assert(scene.get_node("World/Wallet").call("GetAmount", "stone") == 3, "Truck did not collect into shared wallet")
	await process_frame
	await process_frame
	assert(icons.get("IconCount") == 0, "Collected deposit kept a marker")
	var saved_seed: int = scene.get_node("WorldBuilder").get("Seed")
	var saved_spawn: Vector2i = scene.spawn_cell
	var saved_kind: String = cells.call("GetTerrainKind", saved_spawn)
	var saved_position: Vector2 = scene.get_node("World/Truck").global_position
	scene.get_node("HUD/Toolbar/Save").pressed.emit()
	assert(scene.get_node("HUD/ActionStatus").text == "Saved", "Authored save button did not write checkpoint")
	scene.get_node("HUD/Toolbar/Move").pressed.emit()
	assert(scene.act_at(scene.spawn_cell), "Return route missing")
	scene.get_node("HUD/Toolbar/Flood").pressed.emit()
	assert(scene.act_at(scene.spawn_cell))
	await physics_frame
	await physics_frame
	assert(not follower.get("IsMoving"), "Truck continued over newly flooded route")
	assert(scene.get_node("World/Navigation").call("IsBlocked", scene.spawn_cell))
	scene.get_node("HUD/Toolbar/Flatten").pressed.emit()
	cells.call("SetMetadata", scene.deposit_cell, "terrain_relief", 2)
	scene.act_at(scene.deposit_cell)
	assert(cells.call("GetMetadata", scene.deposit_cell, "terrain_relief") == 0)
	scene.get_node("HUD/Toolbar/Regenerate").pressed.emit()
	scene.get_node("World/Wallet").call("RestoreState", {})
	assert(scene.get_node("WorldBuilder").get("Seed") != saved_seed)
	scene.get_node("HUD/Toolbar/Load").pressed.emit()
	await process_frame
	await process_frame
	assert(scene.get_node("WorldBuilder").get("Seed") == saved_seed, "Load did not restore recipe")
	assert(scene.spawn_cell == saved_spawn and cells.call("GetTerrainKind", saved_spawn) == saved_kind, "Load lost saved live terrain")
	assert(scene.get_node("World/Wallet").call("GetAmount", "stone") == 3, "Load lost collected stock")
	assert(scene.get_node("World/Deposits/Stone").get("IsDepleted") and icons.get("IconCount") == 0, "Load respawned collected deposit")
	assert(scene.get_node("World/Truck").global_position.distance_to(saved_position) < 0.01, "Load misplaced truck")
	assert(not scene.get_node("World/Navigation").call("IsBlocked", saved_spawn), "Load retained post-save flooding in navigation")
	await physics_frame
	await physics_frame
	var point := PhysicsPointQueryParameters2D.new()
	point.position = scene.get_node("World/Grid").call("CellToWorld", saved_spawn)
	point.collision_mask = 4
	assert(scene.get_world_2d().direct_space_state.intersect_point(point).is_empty(), "Load retained flooded-cell collision")
	var clear_cell := saved_spawn + Vector2i.DOWN
	cells.call("RemoveFlag", clear_cell, 2)
	cells.call("SetMetadata", clear_cell, "terrain_feature", "woods")
	await process_frame
	await process_frame
	var features = scene.get_node("World/Features")
	var before_stamps: int = features.get("StampCount")
	scene.get_node("HUD/Toolbar/ClearJob").pressed.emit()
	assert(scene.act_at(clear_cell), "Authored clear-job action did not queue")
	assert(cells.call("GetFlags", clear_cell) & 2 == 0, "Queueing cleared terrain before work")
	assert(scene.capture_checkpoint().is_empty(), "Checkpoint ignored queued work")
	var saw_progress := false
	var deadline := Time.get_ticks_msec() + 6000
	while cells.call("GetFlags", clear_cell) & 2 == 0 and Time.get_ticks_msec() < deadline:
		await physics_frame
		var meter = scene.get_node("HUD/JobProgress")
		if meter.visible and meter.value > 0 and meter.value < 1: saw_progress = true
	assert(cells.call("GetFlags", clear_cell) & 2 != 0 and saw_progress, "Truck did not execute timed clear work")
	await process_frame
	await process_frame
	assert(features.get("StampCount") < before_stamps, "Completed clear job left vegetation visible")
	var cancelled_cell := saved_spawn - Vector2i.DOWN
	cells.call("RemoveFlag", cancelled_cell, 2)
	assert(scene.act_at(cancelled_cell))
	scene.get_node("HUD/Toolbar/CancelJobs").pressed.emit()
	await physics_frame
	assert(scene.get_node("World/Jobs").get("QueuedCount") == 0 and scene.get_node("World/Truck/GridWorker").get("CurrentJobId") == "")
	assert(cells.call("GetFlags", cancelled_cell) & 2 == 0, "Cancelled job mutated terrain")
	cells.call("SetTerrainKind", cancelled_cell, "water")
	assert(not scene.act_at(cancelled_cell), "Water accepted a clear-land job")
	var shelter_cell := saved_spawn + Vector2i.LEFT
	scene.get_node("HUD/BuildingToolbar/Place").pressed.emit()
	assert(not scene.act_at(cancelled_cell), "Water accepted shelter placement")
	assert(scene.act_at(shelter_cell), "Authored shelter placement failed")
	assert(scene.get_node("World/Buildings").get_child_count() == 1)
	assert(scene.get_node("World/Navigation").call("IsBlocked", shelter_cell), "Shelter did not block truck routing")
	assert(not scene.act_at(shelter_cell), "Shelters can overlap")
	assert(scene.capture_checkpoint().is_empty(), "Saved unfinished construction as a completed shelter")
	scene.get_node("HUD/Toolbar/CancelJobs").pressed.emit()
	await physics_frame
	assert(scene.get_node("World/Buildings").get_child_count() == 0, "Cancelled construction retained its building")
	assert(not scene.get_node("World/Navigation").call("IsBlocked", shelter_cell))
	assert(scene.act_at(shelter_cell))
	var construction = scene.get_node("World/Buildings").get_child(0)
	assert(not construction.get_node("GridObject").get("Complete"))
	var build_progress := false
	deadline = Time.get_ticks_msec() + 6000
	while is_instance_valid(construction) and not construction.get_node("GridObject").get("Complete") and Time.get_ticks_msec() < deadline:
		await physics_frame
		var meter = scene.get_node("HUD/JobProgress")
		if meter.visible and meter.value > 0 and meter.value < 1: build_progress = true
	assert(is_instance_valid(construction) and construction.get_node("GridObject").get("Complete") and build_progress, "Truck did not build shelter through timed work")
	await physics_frame
	var checkpoint: Dictionary = scene.capture_checkpoint()
	assert(checkpoint.buildings == [shelter_cell])
	scene.get_node("HUD/BuildingToolbar/Demolish").pressed.emit()
	assert(scene.act_at(shelter_cell))
	assert(not scene.get_node("World/Navigation").call("IsBlocked", shelter_cell), "Demolition retained routing block")
	assert(scene.restore_checkpoint(checkpoint), "Checkpoint could not recreate shelter")
	assert(scene.get_node("World/BuildSites").get("ActiveBuildSiteCount") == 0 and scene.get_node("World/Jobs").get("QueuedCount") == 0, "Restore queued construction for a completed shelter")
	assert(scene.get_node("World/Buildings").get_child_count() == 1)
	var shelter = scene.get_node("World/Buildings").get_child(0)
	assert(shelter.global_position.is_equal_approx(scene.get_node("World/Grid").call("CellToWorld", shelter_cell)))
	if "--capture" in OS.get_cmdline_user_args():
		await RenderingServer.frame_post_draw
		root.get_texture().get_image().save_png("res://tests/output/terrain_playground/building.png")
	scene.get_node("HUD/Toolbar/Regenerate").pressed.emit()
	assert(scene.get_node("World/Buildings").get_child_count() == 0, "New world retained old shelters")
	scene.free()
	print("[terrain-grid-playground] OK")
	quit()
