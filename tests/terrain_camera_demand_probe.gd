extends SceneTree
const GRID := "res://addons/beep_game_builder_cs/ecs/grid/"
var failures: Array[String] = []
func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		push_error(message)
func _initialize() -> void: run.call_deferred()
func verify_visible(grid: Node2D, cells: Node, viewport: SubViewport) -> void:
	var inverse := grid.get_global_transform_with_canvas().affine_inverse()
	for y in [0.0, 0.25, 0.5, 0.75, 1.0]:
		for x in [0.0, 0.25, 0.5, 0.75, 1.0]:
			var world: Vector2 = grid.to_global(inverse * (Vector2(viewport.size) * Vector2(x, y)))
			var cell: Vector2i = grid.WorldToCell(world)
			if Rect2i(-128, -128, 256, 256).has_point(cell):
				check(cells.IsChunkPinned(Vector2i(cell.x >> 5, cell.y >> 5)), "Visible cell was outside demand")
func run() -> void:
	var viewport := SubViewport.new()
	viewport.size = Vector2i(256, 256)
	root.add_child(viewport)
	var host := Node2D.new()
	viewport.add_child(host)
	var grid: Node2D = load(GRID + "GridProjectionComponent.cs").new()
	grid.name = "Grid"
	grid.DrawGrid = false
	grid.TrackMouseCell = false
	grid.TileSize = Vector2(32, 32)
	host.add_child(grid)
	var cells: Node = load(GRID + "GridCellDataComponent.cs").new()
	cells.name = "Cells"
	host.add_child(cells)
	var other: Node = load(GRID + "GridCellDataComponent.cs").new()
	other.name = "Other"
	host.add_child(other)
	viewport.canvas_transform = Transform2D(0.0, Vector2(-128, -128))
	var demand: Node = load(GRID + "GridCameraDemandComponent.cs").new()
	demand.GridPath = NodePath("../Grid")
	demand.CellDataPath = NodePath("../Cells")
	demand.BoundsCells = Rect2i(-128, -128, 256, 256)
	demand.PaddingChunks = 0
	host.add_child(demand)
	check(demand.RefreshDemand() and cells.PinnedChunkCount == 1 and cells.IsChunkPinned(Vector2i.ZERO), "Initial viewport chunk demand incorrect")
	for i in 5: demand.RefreshDemand()
	check(cells.GetChunkPinCount(Vector2i.ZERO) == 1, "Stationary refresh accumulated duplicate pins")
	var archive: Node = load(GRID + "GridCellArchiveComponent.cs").new()
	archive.CellDataPath = NodePath("../Cells")
	archive.ArchiveDirectory = "user://tests/camera_demand_%s_%s" % [OS.get_process_id(), Time.get_ticks_usec()]
	host.add_child(archive)
	cells.SetTerrainKind(Vector2i.ZERO, "forest")
	demand.Enabled = false
	check(archive.SaveChunk(Vector2i.ZERO) and archive.EvictSavedChunk(Vector2i.ZERO), "Could not prepare camera reload fixture")
	archive.AutoLoadPinnedChunks = true
	demand.Enabled = true
	var deadline := Time.get_ticks_msec() + 10000
	while not cells.IsChunkAvailable(Vector2i.ZERO) and Time.get_ticks_msec() < deadline: await process_frame
	check(cells.IsChunkAvailable(Vector2i.ZERO) and cells.GetTerrainKind(Vector2i.ZERO) == "forest", "Camera demand did not trigger automatic disk reload")
	DirAccess.remove_absolute(archive.GetChunkPath(Vector2i.ZERO))
	DirAccess.remove_absolute(ProjectSettings.globalize_path(archive.ArchiveDirectory))
	archive.free()
	cells.SetChunkPins(host, [Vector2i.ZERO])
	viewport.canvas_transform = Transform2D(0.0, Vector2(-1152, -128))
	demand.RefreshDemand()
	check(cells.IsChunkPinned(Vector2i(1, 0)) and cells.GetChunkPinCount(Vector2i.ZERO) == 1, "Pan lost independent simulation demand")
	cells.ReleaseChunkPins(host)
	check(not cells.IsChunkPinned(Vector2i.ZERO), "Pan retained obsolete camera demand")
	demand.PaddingChunks = 1
	demand.RefreshDemand()
	check(demand.DemandedChunkCount == 9, "Camera padding did not request a neighborhood")
	# Real Camera2D transforms, including rotated and zoomed isometric grids.
	var camera := Camera2D.new()
	viewport.add_child(camera)
	camera.ignore_rotation = false
	camera.position = Vector2(600, 350)
	camera.rotation = 0.65
	camera.zoom = Vector2(0.75, 1.25)
	camera.make_current()
	camera.force_update_scroll()
	await process_frame
	for projection in [0, 1]:
		grid.Projection = projection
		grid.position = Vector2(90, -50)
		grid.rotation = -0.2
		grid.scale = Vector2(1.2, 0.8)
		demand.RefreshDemand()
		verify_visible(grid, cells, viewport)
	check(demand.DemandedChunkCount > 0, "Transformed camera demand vanished")
	demand.CellDataPath = NodePath("../Other")
	demand.RefreshDemand()
	check(cells.PinnedChunkCount == 0 and other.PinnedChunkCount > 0, "Rebinding camera leaked old-world pins")
	demand.Enabled = false
	check(other.PinnedChunkCount == 0, "Disabled camera retained demand")
	demand.Enabled = true
	check(other.PinnedChunkCount > 0, "Enabled camera did not reacquire demand")
	host.remove_child(demand)
	check(other.PinnedChunkCount == 0, "Detached camera demand leaked pins")
	host.add_child(demand)
	check(other.PinnedChunkCount > 0, "Reattached camera did not reacquire pins")
	# Full zoom-out clips to finite world bounds rather than loading arbitrary coordinates.
	camera.zoom = Vector2(0.001, 0.001)
	camera.force_update_scroll()
	await process_frame
	demand.RefreshDemand()
	check(demand.DemandedChunkCount == 64, "Overview demand was not clipped to finite chunk bounds: %s chunks, canvas %s" % [demand.DemandedChunkCount, viewport.canvas_transform])
	for chunk in other.GetPinnedChunks():
		check(Rect2i(-4, -4, 8, 8).has_point(chunk), "Out-of-world chunk was requested")
	var previous_transform := grid.transform
	grid.transform = Transform2D(Vector2.ZERO, Vector2.ZERO, Vector2.ZERO)
	check(not demand.RefreshDemand() and other.PinnedChunkCount == 64, "Invalid transform discarded previously valid demand")
	grid.transform = previous_transform
	camera.zoom = Vector2.ONE
	camera.position = Vector2(100000, 100000)
	camera.force_update_scroll()
	await process_frame
	demand.RefreshDemand()
	check(other.PinnedChunkCount == 0, "Off-map camera retained unrelated terrain")
	viewport.free()
	print("[terrain-camera-demand] OK" if failures.is_empty() else "[terrain-camera-demand] FAILED")
	quit(0 if failures.is_empty() else 1)
