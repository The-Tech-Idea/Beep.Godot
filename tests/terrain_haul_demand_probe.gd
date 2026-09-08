extends SceneTree
const BASE := "res://addons/beep_game_builder_cs/ecs/grid/"
var failures: Array[String] = []
func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		push_error(message)
func make(kind: String, parent: Node, label: String, properties: Dictionary = {}) -> Node:
	var node: Node = load(BASE + kind + "Component.cs").new()
	node.name = label
	for key in properties: node.set(key, properties[key])
	parent.add_child(node)
	return node
func _initialize() -> void: run.call_deferred()
func run() -> void:
	var host := Node2D.new()
	root.add_child(host)
	var grid := make("GridProjection", host, "Grid", {"TileSize": Vector2(32, 32), "DrawGrid": false, "TrackMouseCell": false})
	var cells := make("GridCellData", host, "Cells")
	cells.FillTerrain(Rect2i(0, 0, 160, 1), "grass")
	var nav := make("GridNavigation", host, "Navigation", {"GridPath": NodePath("../Grid"), "CellDataPath": NodePath("../Cells"), "BoundsSize": Vector2i(160, 1)})
	var archive := make("GridCellArchive", host, "Archive", {"CellDataPath": NodePath("../Cells"), "ArchiveDirectory": "user://tests/haul_demand_%s_%s" % [OS.get_process_id(), Time.get_ticks_usec()], "AutoLoadPinnedChunks": true})
	for chunk in [Vector2i(2, 0), Vector2i(4, 0)]:
		check(archive.SaveChunk(chunk) and archive.EvictSavedChunk(chunk), "Could not archive haul endpoint")
	var depot: Node = load("res://tests/actor_test_depot.gd").new()
	depot.name = "Depot"
	host.add_child(depot)
	var body := Node2D.new()
	body.name = "Truck"
	host.add_child(body)
	body.global_position = grid.CellToWorld(Vector2i.ZERO)
	var follower := make("GridPathFollower", body, "Follower", {"GridPath": NodePath("../../Grid"), "NavigationPath": NodePath("../../Navigation"), "DriveCharacterBody": false, "Speed": 1024.0})
	follower.AutoAdvancePath = false
	var hauler := make("GridHauler", body, "Hauler", {"PathFollowerPath": NodePath("../Follower"), "DepotStoragePath": NodePath("../../Depot"), "ChunkCellDataPath": NodePath("../../Cells"), "DepotCell": Vector2i(129, 0), "RegisterOnReady": false})
	hauler.set_process(false)
	check(hauler.RequestHaul(Vector2i(66, 0), "wood", 3), "Archived pickup was rejected rather than awaited")
	check(hauler.IsWaitingForTerrain and hauler.IsBusy and not follower.IsMoving, "Hauler tried to move before endpoint readiness")
	check(cells.IsChunkPinned(Vector2i(2, 0)) and cells.IsChunkPinned(Vector2i(4, 0)), "Accepted haul did not pin both endpoints")
	check(not archive.EvictSavedChunk(Vector2i(4, 0)), "Archive retired a haul endpoint")
	var deadline := Time.get_ticks_msec() + 15000
	while hauler.CarryingAmount > 0 and Time.get_ticks_msec() < deadline:
		await process_frame
		nav.ProcessPathRequests()
		follower.AdvancePath(100.0)
		hauler.AdvanceWork(1.0)
	check(hauler.CarryingAmount == 0 and depot.Stored("wood") == 3, "Endpoint loading did not complete exactly one delivery")
	check(not cells.HasChunkPins(hauler), "Empty hauler retained endpoint pins")
	# Cancelled cargo stays protected at its depot, but no longer needs the pickup.
	check(archive.SaveChunk(Vector2i(2, 0)) and archive.EvictSavedChunk(Vector2i(2, 0)), "Could not retire pickup for cancellation fixture")
	check(hauler.RequestHaul(Vector2i(66, 0), "wood", 2), "Second haul rejected")
	var pending: Dictionary = hauler.CaptureState()
	check(pending.waiting_terrain and pending.pickup_x == 66, "Waiting pickup intent was not saved")
	hauler.CancelHaul("fixture")
	check(hauler.CarryingAmount == 2 and not hauler.IsWaitingForTerrain, "Cancellation lost cargo or left a waiting leg")
	check(not cells.IsChunkPinned(Vector2i(2, 0)) and cells.IsChunkPinned(Vector2i(4, 0)), "Cancelled haul did not release pickup while protecting depot")
	check(not hauler.TryDeliverCargo() and depot.Stored("wood") == 3, "Suspended cargo delivered remotely")
	var other := make("GridCellData", host, "Other")
	hauler.ChunkCellDataPath = NodePath("../../Other")
	check(not cells.HasChunkPins(hauler) and other.IsChunkPinned(Vector2i(4, 0)), "Rebinding left old endpoint pins")
	hauler.ChunkCellDataPath = NodePath("../../Cells")
	check(not other.HasChunkPins(hauler) and cells.IsChunkPinned(Vector2i(4, 0)), "Rebinding back did not restore demand")
	host.remove_child(body)
	check(not cells.HasChunkPins(hauler), "Removed hauler retained endpoint demand")
	host.add_child(body)
	hauler.set_process(false)
	follower.AutoAdvancePath = false
	check(cells.IsChunkPinned(Vector2i(4, 0)), "Reattached cargo did not restore depot demand")
	hauler.DepotCell = Vector2i(90, 0)
	check(cells.IsChunkPinned(Vector2i(2, 0)) and not cells.IsChunkPinned(Vector2i(4, 0)), "Changed depot did not transfer demand")
	check(hauler.ResumeHaulToDepot() and hauler.IsWaitingForTerrain, "Resuming to archived depot did not wait")
	hauler.CancelHaul("restore")
	hauler.RestoreState(pending)
	check(not hauler.IsBusy and hauler.CarryingAmount == 2 and cells.IsChunkPinned(Vector2i(4, 0)), "Standalone restore did not protect suspended cargo")
	hauler.Unload("wood", 2)
	check(not cells.HasChunkPins(hauler), "Cargo transfer out retained depot pins")
	# Actor snapshots can resume a haul that had no route yet because its pickup was archived.
	var registry: Node = load("res://addons/beep_game_builder_cs/ecs/actors/ActorRegistryComponent.cs").new()
	registry.name = "Registry"
	host.add_child(registry)
	var actor: Node = load("res://addons/beep_game_builder_cs/ecs/actors/ActorComponent.cs").new()
	actor.ActorId = "hauler"
	actor.RegistryPath = NodePath("../../Registry")
	body.add_child(actor)
	actor.set_physics_process(false)
	check(hauler.RequestHaul(Vector2i(66, 0), "wood", 1) and hauler.IsWaitingForTerrain, "Actor restore fixture was not waiting for terrain")
	var saved: Dictionary = JSON.parse_string(JSON.stringify(actor.CaptureActor()))
	hauler.CancelHaul("snapshot")
	actor.RestoreActor(saved)
	check(hauler.IsWaitingForTerrain and hauler.IsBusy and hauler.CarryingAmount == 1, "Actor restore lost waiting terrain intent")
	check(cells.IsChunkPinned(Vector2i(2, 0)) and cells.IsChunkPinned(Vector2i(4, 0)), "Actor restore lost endpoint demand")
	deadline = Time.get_ticks_msec() + 15000
	while hauler.CarryingAmount > 0 and Time.get_ticks_msec() < deadline:
		await process_frame
		nav.ProcessPathRequests()
		follower.AdvancePath(100.0)
		hauler.AdvanceWork(1.0)
	check(hauler.CarryingAmount == 0 and depot.Stored("wood") == 4, "Restored waiting haul did not complete exactly once")
	archive.AutoLoadPinnedChunks = false
	deadline = Time.get_ticks_msec() + 10000
	while archive.IsBusy and Time.get_ticks_msec() < deadline: await process_frame
	check(not archive.IsBusy, "Haul fixture read did not finish")
	if not archive.IsBusy:
		for chunk in [Vector2i(2, 0), Vector2i(4, 0)]: DirAccess.remove_absolute(archive.GetChunkPath(chunk))
		DirAccess.remove_absolute(ProjectSettings.globalize_path(archive.ArchiveDirectory))
	host.free()
	print("[terrain-haul-demand] OK" if failures.is_empty() else "[terrain-haul-demand] FAILED")
	quit(0 if failures.is_empty() else 1)
