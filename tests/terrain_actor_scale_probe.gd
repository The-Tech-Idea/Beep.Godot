extends SceneTree

var failures: Array[String] = []
var mode := "full"

func count_tile_layers(node: Node) -> int:
	var count := int(node is TileMapLayer)
	for child in node.get_children(): count += count_tile_layers(child)
	return count

func _initialize() -> void:
	create_timer(240).timeout.connect(func(): push_error("Combined terrain/actor benchmark timed out"); quit(1))
	run.call_deferred()

func run() -> void:
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--scale-mode="): mode = argument.trim_prefix("--scale-mode=")
	if mode not in ["full", "terrain-hidden", "actors-paused", "archive-paused"]:
		push_error("Unknown scale diagnostic mode: " + mode)
		quit(2)
		return
	root.size = Vector2i(1280, 800)
	root.content_scale_size = root.size
	var scene: Node = load("res://addons/beep_game_builder_cs/templates/scenes/actors/streaming_lab.tscn").instantiate()
	scene.get_node("World").CustomBounds = Vector2i(1024, 1024)
	scene.get_node("CameraDemand").BoundsCells = Rect2i(0, 0, 1024, 1024)
	# Keep generation publication resident until the actors have established their own demands.
	scene.get_node("HUD/Toolbar/Row/Streaming").button_pressed = false
	var started := Time.get_ticks_msec()
	root.add_child(scene)
	current_scene = scene
	while not scene.ready_to_play and Time.get_ticks_msec() - started < 180000: await process_frame
	if not scene.ready_to_play:
		push_error("Combined scene did not become ready")
		scene.free()
		quit(1)
		return
	var ready_ms := Time.get_ticks_msec() - started
	print("[terrain-actor-scale] world ready; collecting spawn cells")
	var nav: Node = scene.get_node("Navigation")
	var grid: Node = scene.get_node("Grid")
	var registry: Node = scene.get_node("Registry")
	var player: Node = scene.get_node("Player")
	var cells: Node = scene.get_node("Cells")
	var archive: Node = scene.get_node("Archive")
	var reachable: Array[Vector2i] = [scene.home]
	var excluded_nontraversable_edges := 0
	var visited := {scene.home: true}
	var cursor := 0
	while cursor < reachable.size() and reachable.size() < 1400:
		var cell := reachable[cursor]
		cursor += 1
		for offset in [Vector2i.UP, Vector2i.DOWN, Vector2i.LEFT, Vector2i.RIGHT]:
			var next: Vector2i = cell + offset
			if visited.has(next): continue
			if nav.IsInBounds(next) and not nav.IsBlocked(next):
				if not nav.CanTraverse(cell, next):
					excluded_nontraversable_edges += 1
					continue
				visited[next] = true
				reachable.append(next)
		if cursor % 128 == 0: await process_frame
	if reachable.size() < 1200:
		push_error("Benchmark needs at least 1200 dry connected cells")
		scene.free()
		quit(1)
		return
	var occupied := {}
	for id in player.GetOwnedActors():
		occupied[grid.WorldToCell(registry.FindActor(id).get_parent().global_position)] = true
	var spawn_started := Time.get_ticks_msec()
	print("[terrain-actor-scale] spawning workers")
	for cell in reachable:
		if registry.ActorCount >= 1000: break
		if occupied.has(cell): continue
		var actor = registry.SpawnActor("worker", "settlement", grid.CellToWorld(cell), "")
		if actor == null:
			failures.append("Worker spawn rejected")
			break
		if registry.ActorCount % 16 == 0: await process_frame
	var spawn_ms := Time.get_ticks_msec() - spawn_started
	scene.update_status()
	var ids: Array = player.GetOwnedActors()
	for id in ids:
		var body: Node = registry.FindActor(id).get_parent()
		if body.get_node("PathFollower").SetZIndexFromY or not body.get_parent().y_sort_enabled:
			failures.append("Authored actors must use their native Y-sorted container")
			break
	var submitted := 0
	var completion_reasons := {}
	nav.PathRequestCompleted.connect(func(_id, _path, reason):
		var key: String = "success" if reason.is_empty() else reason
		completion_reasons[key] = completion_reasons.get(key, 0) + 1)
	var command_script = load("res://addons/beep_game_builder_cs/ecs/actors/ActorCommand.cs")
	for i in mini(200, ids.size()):
		var command: Resource = command_script.new()
		command.IssuerId = "settlement"
		command.Recipients = [ids[i]]
		command.TargetCell = reachable[1000 + i]
		submitted += registry.Submit(command)
	scene.get_node("HUD/Toolbar/Row/Streaming").button_pressed = true
	# Apply diagnostic changes only after identical generation, spawning and orders.
	# Hidden terrain still runs CPU callbacks; paused actors retain their chunk pins.
	if mode == "terrain-hidden": scene.get_node("Painted").visible = false
	if mode == "actors-paused":
		scene.get_node("Actors").process_mode = Node.PROCESS_MODE_DISABLED
		nav.process_mode = Node.PROCESS_MODE_DISABLED
	if mode == "archive-paused": archive.AutoEnforceChunkBudget = false
	var tile_layers := count_tile_layers(scene)
	var frame_ms: Array[float] = []
	var draw_calls: Array[float] = []
	var physics_ms: Array[float] = []
	var engine_frame_ms: Array[float] = []
	var navigation_ms: Array[float] = []
	var navigation_expansions: Array[float] = []
	var physics_start := Engine.get_physics_frames()
	var previous := Time.get_ticks_usec()
	var measure_start := Time.get_ticks_msec()
	print("[terrain-actor-scale] measuring 1000 workers and 200 routes; mode=", mode)
	while Time.get_ticks_msec() - measure_start < 15000:
		await process_frame
		var now := Time.get_ticks_usec()
		frame_ms.append((now - previous) / 1000.0)
		navigation_ms.append(nav.PathMillisecondsLastFrame)
		navigation_expansions.append(nav.PathExpansionsLastFrame)
		physics_ms.append(Performance.get_monitor(Performance.TIME_PHYSICS_PROCESS) * 1000.0)
		engine_frame_ms.append(Performance.get_monitor(Performance.TIME_PROCESS) * 1000.0)
		draw_calls.append(Performance.get_monitor(Performance.RENDER_TOTAL_DRAW_CALLS_IN_FRAME))
		previous = now
	var physics_steps := Engine.get_physics_frames() - physics_start
	var measurement_ms := Time.get_ticks_msec() - measure_start
	var arrived := 0
	var waiting_paths := 0
	var moving_paths := 0
	for i in mini(200, ids.size()):
		var actor = registry.FindActor(ids[i])
		if actor != null:
			var follower: Node = actor.get_parent().get_node("PathFollower")
			if follower.IsPathPending: waiting_paths += 1
			elif follower.IsMoving: moving_paths += 1
		if actor != null and actor.get_parent().global_position.distance_to(grid.CellToWorld(reachable[1000 + i])) < 1:
			arrived += 1
	frame_ms.sort()
	physics_ms.sort()
	engine_frame_ms.sort()
	var total_expansions := 0.0
	for count in navigation_expansions: total_expansions += count
	navigation_ms.sort()
	navigation_expansions.sort()
	draw_calls.sort()
	if registry.ActorCount != 1000: failures.append("Expected 1000 worker instances")
	if submitted != 200: failures.append("Expected 200 accepted routes")
	if mode == "full" and arrived != submitted: failures.append("Not all accepted routes completed within the 15-second measurement")
	if not player.PossessedActorId.is_empty(): failures.append("RTS benchmark unexpectedly requires an avatar")
	var report := {"functional_complete": mode == "full" and failures.is_empty(), "performance_qualified": false,
		"excluded_nontraversable_edges": excluded_nontraversable_edges, "path_completion_reasons": completion_reasons,
		"fixture_revision": "traversable-connected-spawns",
		"navigation_ms_sampled_p50": navigation_ms[navigation_ms.size() / 2],
		"navigation_ms_sampled_p95": navigation_ms[int(navigation_ms.size() * 0.95)],
		"navigation_expansions_sampled_p50": navigation_expansions[navigation_expansions.size() / 2],
		"navigation_expansions_sampled_total": total_expansions,
		"navigation_budget_ms": nav.PathMillisecondsPerFrame,
		"navigation_budget_expansions": nav.PathExpansionsPerFrame,
		"active_path_searches": nav.ActivePathSearchCount, "terrain_waiting_requests": nav.TerrainWaitingRequestCount,
		"diagnostic_mode": mode, "fixture_valid": registry.ActorCount == 1000 and submitted == 200,
		"terrain_script": scene.get_node("Painted").get_script().resource_path,
		"tilemap_layer_count": tile_layers, "draw_calls_sampled_p50": draw_calls[draw_calls.size() / 2],
		"ready_ms": ready_ms, "spawn_ms": spawn_ms, "actors": registry.ActorCount,
		"submitted_routes": submitted, "arrived_after_15_seconds": arrived, "frames": frame_ms.size(),
		"frame_ms_p50": frame_ms[frame_ms.size() / 2], "frame_ms_p95": frame_ms[int(frame_ms.size() * 0.95)],
		"frame_ms_max": frame_ms.back(), "resident_cells": cells.CellCount, "resident_chunks": cells.StoredChunkCount,
		"measurement_ms": measurement_ms, "physics_steps": physics_steps,
		"physics_ticks_per_second": Engine.physics_ticks_per_second,
		"physics_steps_per_render_frame": float(physics_steps) / frame_ms.size(),
		"engine_physics_ms_sampled_p50": physics_ms[physics_ms.size() / 2],
		"engine_frame_ms_sampled_p50": engine_frame_ms[engine_frame_ms.size() / 2],
		"monitor_note": "Engine monitors may update with delay; timing samples are not a CPU phase breakdown.",
		"pending_path_requests": nav.PendingPathRequestCount,
		"waiting_actor_paths": waiting_paths, "moving_actor_paths": moving_paths,
		"evicted_chunks": cells.EvictedChunkCount, "pinned_chunks": cells.GetPinnedChunks().size(),
		"renderer": DisplayServer.get_name(), "failures": failures,
		"scope": "1024x1024 authored streaming lab, 1000 native static-art workers, 200 orders; 15-second wall-frame sample. Not animated/full-detail qualification or a memory measurement."}
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path("res://tests/output/terrain_actor_scale"))
	var suffix := "" if mode == "full" else "_" + mode
	var file := FileAccess.open("res://tests/output/terrain_actor_scale/report%s.json" % suffix, FileAccess.WRITE)
	file.store_string(JSON.stringify(report, "  "))
	file.close()
	if DisplayServer.get_name() != "headless":
		await RenderingServer.frame_post_draw
		root.get_texture().get_image().save_png("res://tests/output/terrain_actor_scale/detail%s.png" % suffix)
	archive.AutoEnforceChunkBudget = false
	archive.AutoLoadPinnedChunks = false
	var deadline := Time.get_ticks_msec() + 15000
	while archive.IsBusy and Time.get_ticks_msec() < deadline: await process_frame
	print("[terrain-actor-scale] ", JSON.stringify(report))
	scene.free()
	quit(0 if failures.is_empty() else 1)
