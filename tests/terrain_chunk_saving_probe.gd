extends SceneTree

const GRID := "res://addons/beep_game_builder_cs/ecs/grid/"
var failures: Array[String] = []
var results := {}
var loads := {}

func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		push_error(message)

func pump(archive: Node) -> void:
	for frame in 300:
		archive.ProcessPendingLoad()
		archive.ProcessPendingSave()
		if not archive.IsBusy: return
		await physics_frame
	check(false, "Archive I/O did not finish within test deadline")

func request(archive: Node, chunk := Vector2i.ZERO) -> int:
	var id: int = archive.RequestSaveChunk(chunk)
	archive.set_process(false)
	check(id > 0, "Save request rejected: " + archive.LastError)
	return id

func has_temporary(directory: String) -> bool:
	for file in DirAccess.get_files_at(directory):
		if file.ends_with(".tmp"): return true
	return false

func await_temporary(directory: String, present: bool) -> void:
	for frame in 300:
		if has_temporary(directory) == present: return
		await physics_frame
	check(false, "Temporary file did not reach expected state")

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
	archive.ArchiveDirectory = "user://tests/chunk_saving_%s_%s" % [OS.get_process_id(), Time.get_ticks_usec()]
	host.add_child(archive)
	archive.ChunkSaveFinished.connect(func(id, success, error): results[id] = [success, error])
	archive.ChunkLoadFinished.connect(func(id, success, error): loads[id] = [success, error])
	var directory := ProjectSettings.globalize_path(archive.ArchiveDirectory)
	var path: String = archive.GetChunkPath(Vector2i.ZERO)
	cells.SetTerrainKind(Vector2i.ZERO, "grass")
	check(archive.SaveChunk(Vector2i.ZERO), "Could not prepare previous archive")
	var original := FileAccess.get_file_as_bytes(path)
	cells.SetTerrainKind(Vector2i.ZERO, "mud")
	cells.SetMetadata(Vector2i.ZERO, "typed", {"point": Vector2i(4, 5), "bytes": PackedByteArray([0, 255])})
	var expected: Dictionary = cells.GetCell(Vector2i.ZERO).duplicate(true)
	var id := request(archive)
	check(archive.IsSaving and archive.IsBusy and archive.PendingSaveId == id, "Request did not expose saving state")
	check(archive.RequestSaveChunk(Vector2i.ONE) == 0 and archive.RequestLoadChunk(Vector2i.ZERO) == 0, "Shared I/O slot admitted overlapping requests")
	check(not archive.SaveChunk(Vector2i.ZERO) and not archive.LoadChunk(Vector2i.ZERO), "Synchronous APIs ignored outstanding save")
	cells.SetTerrainKind(Vector2i.ZERO, "desert")
	await await_temporary(directory, true)
	check(FileAccess.get_file_as_bytes(path) == original, "Worker replaced archive without main-thread approval")
	await pump(archive)
	check(results[id][0] and cells.GetTerrainKind(Vector2i.ZERO) == "desert", "Saving mutated newer live data")
	check(not archive.IsChunkSaveCurrent(Vector2i.ZERO), "Snapshot save incorrectly marked newer live edits as saved")
	check(archive.LoadChunk(Vector2i.ZERO) and cells.GetCell(Vector2i.ZERO) == expected, "Snapshot did not retain request-time typed data")
	var saved := FileAccess.get_file_as_bytes(path)
	cells.SetTerrainKind(Vector2i.ZERO, "forest")
	id = request(archive)
	await await_temporary(directory, true)
	archive.CancelSave()
	check(archive.IsBusy and archive.RequestLoadChunk(Vector2i.ZERO) == 0, "Cancelled write released slot before draining")
	await pump(archive)
	check(results[id] == [false, "cancelled"] and FileAccess.get_file_as_bytes(path) == saved and not has_temporary(directory), "Cancellation replaced previous save or leaked a temporary file")
	id = request(archive)
	archive.CellDataPath = NodePath("../Other")
	await pump(archive)
	check(results[id] == [false, "archive_target_changed"] and FileAccess.get_file_as_bytes(path) == saved, "Rebound archive published old target's snapshot")
	archive.CellDataPath = NodePath("../Cells")
	id = request(archive)
	var configured: String = archive.ArchiveDirectory
	archive.ArchiveDirectory = configured + "/other"
	await pump(archive)
	check(results[id] == [false, "archive_target_changed"] and FileAccess.get_file_as_bytes(path) == saved, "Changed directory published pending save")
	archive.ArchiveDirectory = configured
	id = request(archive)
	host.remove_child(archive)
	host.add_child(archive)
	archive.set_process(false)
	check(archive.IsBusy and archive.RequestSaveChunk(Vector2i.ZERO) == 0, "Reattachment admitted I/O before draining")
	await pump(archive)
	check(not results.has(id) and FileAccess.get_file_as_bytes(path) == saved and not has_temporary(directory), "Detached write published or signaled after reattachment")
	cells.SetChunkAvailable(Vector2i.ZERO, false)
	check(archive.RequestSaveChunk(Vector2i.ZERO) == 0 and not archive.IsBusy and archive.PendingSaveId == 0, "Unavailable chunk was accepted for saving")
	cells.SetChunkAvailable(Vector2i.ZERO, true)
	cells.SetMetadata(Vector2i.ZERO, "large", "x".repeat(15000))
	archive.MaximumChunkBytes = 1024
	# The payload size is known only once the worker has encoded it, so an oversized snapshot
	# fails through completion; the previous file survives and the slot is released.
	id = request(archive)
	await pump(archive)
	check(results.has(id) and not results[id][0] and not results[id][1].is_empty() and not archive.IsBusy
		and FileAccess.get_file_as_bytes(path) == saved, "Oversized snapshot was published or damaged previous save")
	archive.MaximumChunkBytes = 8 * 1024 * 1024
	check(archive.LoadChunk(Vector2i.ZERO), "Could not restore valid fixture")
	# A file cannot act as the archive directory; the worker must report failure without touching it.
	archive.ArchiveDirectory = path
	id = request(archive)
	await pump(archive)
	check(not results[id][0] and not results[id][1].is_empty() and FileAccess.get_file_as_bytes(path) == saved, "Worker I/O failure lost the previous file")
	archive.ArchiveDirectory = configured
	var conflicting := Vector2i(10, 10)
	var conflict_path: String = archive.GetChunkPath(conflicting)
	DirAccess.make_dir_absolute(conflict_path)
	id = request(archive, conflicting)
	await pump(archive)
	check(not results[id][0] and not has_temporary(directory), "Failed final replacement leaked its prepared file")
	DirAccess.remove_absolute(conflict_path)
	var next := [0]
	id = request(archive)
	var first := id
	archive.ChunkSaveFinished.connect(func(completed, success, _error):
		if completed == first and success: next[0] = archive.RequestLoadChunk(Vector2i.ZERO))
	await pump(archive)
	check(results[id][0] and next[0] > id and loads.has(next[0]) and loads[next[0]][0], "Save completion could not reentrantly load with a unique ID")
	var negative := Vector2i(-1, -2)
	cells.SetTerrainKind(Vector2i(-1, -33), "snow")
	id = archive.RequestSaveChunk(negative)
	for frame in 300:
		if results.has(id): break
		await process_frame
	check(id > 0 and results.has(id) and results[id][0] and not archive.IsBusy, "Automatic frame processing did not save negative-coordinate chunk")
	check(archive.IsChunkSaveCurrent(negative), "Unmodified async snapshot did not record its content revision")
	check(archive.LoadChunk(negative) and cells.GetTerrainKind(Vector2i(-1, -33)) == "snow", "Negative chunk failed to round-trip")
	# The node will not return to the tree: worker cleanup must not rely on another _Process.
	id = request(archive)
	await await_temporary(directory, true)
	archive.free()
	await await_temporary(directory, false)
	check(not results.has(id) and FileAccess.get_file_as_bytes(path) == saved, "Freed node published a pending save or emitted completion")
	check(DirAccess.get_files_at(directory).size() == 2, "Archive leaked files after cancellation/lifecycle failures")
	DirAccess.remove_absolute(path)
	DirAccess.remove_absolute(directory.path_join("-1_-2.chunk"))
	DirAccess.remove_absolute(directory)
	host.free()
	print("[terrain-chunk-saving] OK" if failures.is_empty() else "[terrain-chunk-saving] FAILED")
	quit(0 if failures.is_empty() else 1)
