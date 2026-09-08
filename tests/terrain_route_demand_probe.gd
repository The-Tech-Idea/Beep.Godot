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
	var nav := make("GridNavigation", host, "Navigation", {"GridPath": NodePath("../Grid"), "CellDataPath": NodePath("../Cells"), "BoundsSize": Vector2i(160, 1), "LoadMissingTerrain": true})
	var archive := make("GridCellArchive", host, "Archive", {"CellDataPath": NodePath("../Cells"), "ArchiveDirectory": "user://tests/route_demand_%s_%s" % [OS.get_process_id(), Time.get_ticks_usec()], "AutoLoadPinnedChunks": true})
	for x in [1, 2, 3]:
		check(archive.SaveChunk(Vector2i(x, 0)) and archive.EvictSavedChunk(Vector2i(x, 0)), "Could not archive middle route chunk")
	# Connected before the follower: completed search data must remain pinned through all observers.
	nav.PathRequestCompleted.connect(func(_id, path, _reason):
		if path.size() > 0: check(cells.IsChunkPinned(Vector2i(2, 0)), "Completion callback exposed an unprotected route"))
	var body := Node2D.new()
	host.add_child(body)
	body.global_position = grid.CellToWorld(Vector2i.ZERO)
	var follower := make("GridPathFollower", body, "Follower", {"GridPath": NodePath("../../Grid"), "NavigationPath": NodePath("../../Navigation"), "DriveCharacterBody": false, "Speed": 10000.0})
	follower.AutoAdvancePath = false
	check(follower.MoveToCell(Vector2i(150, 0)), "Streamed route was rejected")
	var waited := false
	var deadline := Time.get_ticks_msec() + 15000
	while follower.IsPathPending and Time.get_ticks_msec() < deadline:
		nav.ProcessPathRequests()
		waited = waited or nav.TerrainWaitingRequestCount > 0
		await process_frame
	check(waited and follower.IsMoving and follower.LastMoveFailure.is_empty(), "Unknown frontier did not wait and resume")
	check(follower.GetWorldPath().size() == 151, "Streamed route is incomplete")
	for x in 5: check(cells.IsChunkPinned(Vector2i(x, 0)), "Installed route lost terrain protection")
	host.remove_child(body)
	check(cells.GetPinnedChunks().is_empty(), "Detached route retained pins")
	host.add_child(body)
	follower.AutoAdvancePath = false
	check(follower.IsPathPending, "Reattached streamed route was not revalidated")
	for i in 30:
		nav.ProcessPathRequests()
		if not follower.IsPathPending: break
	check(follower.IsMoving and not follower.IsPathPending, "Reattached route did not resume")
	follower.AdvancePath(100.0)
	check(follower.HasReachedDestination and not cells.HasChunkPins(follower), "Arrival did not release the route")
	# Unknown does not imply traversable: loaded water must still block the route.
	cells.SetTerrainKind(Vector2i(80, 0), "deep_water")
	check(archive.SaveChunk(Vector2i(2, 0)) and archive.EvictSavedChunk(Vector2i(2, 0)), "Could not archive barrier")
	body.global_position = grid.CellToWorld(Vector2i.ZERO)
	follower.MoveToCell(Vector2i(150, 0))
	deadline = Time.get_ticks_msec() + 10000
	while follower.IsPathPending and Time.get_ticks_msec() < deadline:
		nav.ProcessPathRequests()
		await process_frame
	check(not follower.IsMoving and follower.LastMoveFailure == "no_path", "Loaded water was treated as passable")
	check(cells.GetPinnedChunks().is_empty(), "Failed search leaked demand")
	cells.SetTerrainKind(Vector2i(80, 0), "grass")
	archive.AutoLoadPinnedChunks = false
	check(archive.SaveChunk(Vector2i(1, 0)) and archive.EvictSavedChunk(Vector2i(1, 0)), "Could not archive cancellation fixture")
	var first: int = nav.RequestCellPath(Vector2i.ZERO, Vector2i(150, 0))
	var second: int = nav.RequestCellPath(Vector2i.ZERO, Vector2i(150, 0))
	for i in 20: nav.ProcessPathRequests()
	check(nav.TerrainWaitingRequestCount == 2, "Waiting searches occupied active slots or finished early")
	nav.CancelPathRequest(first)
	check(cells.IsChunkPinned(Vector2i(1, 0)), "Cancellation released another request's demand")
	nav.CancelPathRequest(second)
	check(cells.GetPinnedChunks().is_empty() and nav.TerrainWaitingRequestCount == 0, "Cancelled requests leaked pins")
	# Missing archive cannot leave a permanent request, and limits report a distinct failure.
	nav.TerrainRequestTimeoutSeconds = 0.1
	follower.MoveToCell(Vector2i(150, 0))
	deadline = Time.get_ticks_msec() + 3000
	while follower.IsPathPending and Time.get_ticks_msec() < deadline:
		nav.ProcessPathRequests()
		await process_frame
	check(follower.LastMoveFailure == "terrain_request_timeout" and cells.GetPinnedChunks().is_empty(), "Timed-out terrain wait leaked or reported no_path")
	nav.MaximumTerrainChunksPerRequest = 1
	follower.MoveToCell(Vector2i(150, 0))
	for i in 5: nav.ProcessPathRequests()
	check(follower.LastMoveFailure == "terrain_demand_limit" and cells.GetPinnedChunks().is_empty(), "Chunk budget did not bound search demand")
	nav.MaximumTerrainChunksPerRequest = 256
	nav.TerrainRequestTimeoutSeconds = 30.0
	follower.MoveToCell(Vector2i(150, 0))
	for i in 10: nav.ProcessPathRequests()
	body.free()
	check(nav.PendingPathRequestCount == 0 and cells.GetPinnedChunks().is_empty(), "Removed actor retained search demand")
	deadline = Time.get_ticks_msec() + 10000
	while archive.IsBusy and Time.get_ticks_msec() < deadline: await process_frame
	check(not archive.IsBusy, "Route fixture archive did not drain")
	if not archive.IsBusy:
		for x in [1, 2, 3]: DirAccess.remove_absolute(archive.GetChunkPath(Vector2i(x, 0)))
		DirAccess.remove_absolute(ProjectSettings.globalize_path(archive.ArchiveDirectory))
	host.free()
	print("[terrain-route-demand] OK" if failures.is_empty() else "[terrain-route-demand] FAILED")
	quit(0 if failures.is_empty() else 1)
