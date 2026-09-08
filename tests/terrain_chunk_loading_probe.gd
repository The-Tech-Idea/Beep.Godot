extends SceneTree

const GRID := "res://addons/beep_game_builder_cs/ecs/grid/"
var failures: Array[String] = []
var results := {}

func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		push_error(message)

func pump(archive: Node) -> void:
	for frame in 300:
		archive.ProcessPendingLoad()
		if not archive.IsLoading: return
		await physics_frame
	check(false, "Chunk I/O did not finish within the test deadline")

func request(archive: Node, chunk: Vector2i = Vector2i.ZERO) -> int:
	var id: int = archive.RequestLoadChunk(chunk)
	archive.set_process(false)
	check(id > 0, "Read request was not accepted: " + archive.LastError)
	return id

func _initialize() -> void: run.call_deferred()

func run() -> void:
	var host := Node.new()
	root.add_child(host)
	var cells: Node = load(GRID + "GridCellDataComponent.cs").new()
	cells.name = "Cells"
	host.add_child(cells)
	var other: Node = load(GRID + "GridCellDataComponent.cs").new()
	other.name = "Other"
	host.add_child(other)
	var archive: Node = load(GRID + "GridCellArchiveComponent.cs").new()
	archive.CellDataPath = NodePath("../Cells")
	archive.ArchiveDirectory = "user://tests/chunk_loading_%s_%s" % [OS.get_process_id(), Time.get_ticks_usec()]
	host.add_child(archive)
	archive.ChunkLoadFinished.connect(func(id, success, error): results[id] = [success, error])
	cells.SetTerrainKind(Vector2i.ZERO, "water")
	check(archive.SaveChunk(Vector2i.ZERO), "Could not prepare chunk file")
	var path: String = archive.GetChunkPath(Vector2i.ZERO)
	cells.SetTerrainKind(Vector2i.ZERO, "grass")
	cells.SetChunkAvailable(Vector2i.ZERO, false)
	var id := request(archive)
	check(archive.RequestLoadChunk(Vector2i.ZERO) == 0 and archive.RequestSaveChunk(Vector2i.ZERO) == 0 and not archive.SaveChunk(Vector2i.ZERO), "Busy archive admitted conflicting I/O")
	check(cells.GetTerrainKind(Vector2i.ZERO) == "grass", "Read request published before main-thread completion processing")
	await pump(archive)
	check(results.has(id) and results[id][0] and cells.GetTerrainKind(Vector2i.ZERO) == "water" and cells.IsCellAvailable(Vector2i.ZERO), "Async load failed to publish data/readiness")
	id = request(archive)
	cells.SetTerrainKind(Vector2i.ZERO, "mud")
	await pump(archive)
	check(results[id] == [false, "cell_data_changed"] and cells.GetTerrainKind(Vector2i.ZERO) == "mud", "Pending read overwrote a newer local edit")
	id = request(archive)
	cells.SetTerrainKind(Vector2i(64, 0), "desert")
	await pump(archive)
	check(results[id][0] and cells.GetTerrainKind(Vector2i(64, 0)) == "desert", "Unrelated single-cell edit invalidated or was overwritten by read")
	id = request(archive)
	cells.AdvanceDay(1)
	await pump(archive)
	check(results[id] == [false, "cell_data_changed"], "Read ignored world day advancement")
	id = request(archive)
	cells.ClearCells()
	await pump(archive)
	check(not results[id][0] and cells.CellCount == 0, "Stale read resurrected a cleared world")
	id = request(archive)
	archive.CancelLoad()
	check(archive.IsLoading and archive.RequestLoadChunk(Vector2i.ZERO) == 0, "Cancellation allowed overlapping worker reads")
	await pump(archive)
	check(results[id] == [false, "cancelled"] and cells.CellCount == 0, "Cancelled load changed the map")
	id = request(archive)
	archive.CellDataPath = NodePath("../Other")
	await pump(archive)
	check(results[id] == [false, "archive_target_changed"] and other.CellCount == 0, "Rebound loader wrote into the wrong cell service")
	archive.CellDataPath = NodePath("../Cells")
	id = request(archive)
	host.remove_child(archive)
	host.add_child(archive)
	archive.set_process(false)
	check(archive.RequestLoadChunk(Vector2i.ZERO) == 0, "Reattachment started a second read before cancelled work drained")
	await pump(archive)
	check(not results.has(id) and cells.CellCount == 0, "Detached request emitted completion or published stale cells")
	id = request(archive, Vector2i(100, 100))
	await pump(archive)
	check(not results[id][0] and not results[id][1].is_empty(), "Missing file succeeded asynchronously")
	var next := [0]
	id = request(archive)
	var first := id
	archive.ChunkLoadFinished.connect(func(completed, success, _error):
		if completed == first and success: next[0] = archive.RequestLoadChunk(Vector2i.ZERO))
	await pump(archive)
	check(results[id][0] and next[0] > 0 and results.has(next[0]) and results[next[0]][0], "Completion callback could not safely request another load")
	check(not archive.IsLoading and archive.PendingLoadId == 0, "Completed loader retained pending state")
	id = archive.RequestLoadChunk(Vector2i.ZERO)
	check(id > 0, "Automatic read request was rejected")
	for frame in 300:
		if results.has(id): break
		await process_frame
	check(results.has(id) and results[id][0] and not archive.IsLoading, "Godot frame processing did not complete the read")
	DirAccess.remove_absolute(path)
	DirAccess.remove_absolute(ProjectSettings.globalize_path(archive.ArchiveDirectory))
	host.free()
	print("[terrain-chunk-loading] OK" if failures.is_empty() else "[terrain-chunk-loading] FAILED")
	quit(0 if failures.is_empty() else 1)
