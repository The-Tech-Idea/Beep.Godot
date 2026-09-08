extends SceneTree

# RequestSaveChunk copies the chunk's records at request time and hands them to a worker, which
# encodes, hashes and writes them; the main thread's share is the copy. This holds the contract
# that follows: the saved file is the request-time content whatever the live store does after;
# cancellation, target rebinding and size failures arrive through completion without touching
# the previous file; a freed node leaks neither admission nor a completion; and the main thread
# does not encode -- measured on a chunk heavy enough that the old staged encode (32 cells a
# frame, ~75 ms a chunk on a 1024x1024 world) cannot hide inside the bound.

const BASE := "res://addons/beep_game_builder_cs/ecs/grid/"
var failures: Array[String] = []
var results := {}
var heavy_results := {}

func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		push_error(message)

func wait_until(predicate: Callable, message: String) -> void:
	var deadline := Time.get_ticks_msec() + 10000
	while not predicate.call() and Time.get_ticks_msec() < deadline: await process_frame
	check(predicate.call(), message)

# Pumps completion until the archive is idle; returns the main-thread milliseconds spent doing so.
func pump(archive: Node) -> float:
	var spent := 0.0
	for i in 500:
		var started := Time.get_ticks_usec()
		archive.ProcessPendingSave()
		spent += (Time.get_ticks_usec() - started) / 1000.0
		if not archive.IsBusy: return spent
		await process_frame
	check(false, "Save did not drain")
	return spent

func _initialize() -> void: run.call_deferred()

func run() -> void:
	var host := Node.new()
	root.add_child(host)
	var cells: Node = load(BASE + "GridCellDataComponent.cs").new()
	cells.name = "Cells"
	host.add_child(cells)
	cells.FillTerrain(Rect2i(0, 0, 16, 8), "grass")
	var last := Vector2i(15, 7)
	cells.SetMetadata(last, "nested", {"values": [1, 2, 3]})
	var expected: Dictionary = cells.GetCell(last)
	var archive: Node = load(BASE + "GridCellArchiveComponent.cs").new()
	archive.CellDataPath = NodePath("../Cells")
	archive.ArchiveDirectory = "user://tests/capture_budget_%s_%s" % [OS.get_process_id(), Time.get_ticks_usec()]
	host.add_child(archive)
	archive.set_process(false)
	archive.ChunkSaveFinished.connect(func(id, success, error): results[id] = [success, error])

	# Request-time content: edits after the request must not reach the file.
	var id: int = archive.RequestSaveChunk(Vector2i.ZERO)
	archive.set_process(false)
	check(id > 0 and archive.IsSaving, "Request was rejected: %s" % archive.LastError)
	cells.SetTerrainKind(last, "mud")
	cells.SetMetadata(last, "nested", {"values": [9]})
	await pump(archive)
	check(results.get(id) == [true, ""] and not archive.IsChunkSaveCurrent(Vector2i.ZERO), "Save lost its request-time revision")
	check(archive.LoadChunk(Vector2i.ZERO) and cells.GetCell(last) == expected, "The worker saw live records instead of the request-time copies")
	var path: String = archive.GetChunkPath(Vector2i.ZERO)
	var saved := FileAccess.get_file_as_bytes(path)

	# Cancellation drains through completion and leaves the file alone.
	id = archive.RequestSaveChunk(Vector2i.ZERO)
	archive.set_process(false)
	archive.CancelSave()
	check(archive.IsBusy, "Cancellation released the slot before completion")
	await pump(archive)
	check(results.get(id) == [false, "cancelled"] and FileAccess.get_file_as_bytes(path) == saved, "Cancellation replaced saved data")

	# Rebinding the target before completion fails the save.
	id = archive.RequestSaveChunk(Vector2i.ZERO)
	archive.set_process(false)
	archive.CellDataPath = NodePath("../Missing")
	await pump(archive)
	check(results.get(id) == [false, "archive_target_changed"], "Completion ignored target rebinding")
	archive.CellDataPath = NodePath("../Cells")

	# A payload over the byte limit is discovered on the worker and reported through completion.
	archive.MaximumChunkBytes = 1024
	id = archive.RequestSaveChunk(Vector2i.ZERO)
	archive.set_process(false)
	check(id > 0 and archive.IsSaving, "Size-limit fixture was rejected up front")
	await pump(archive)
	check(results.has(id) and not results[id][0] and FileAccess.get_file_as_bytes(path) == saved, "Late size failure replaced previous archive")
	archive.MaximumChunkBytes = 8 * 1024 * 1024

	# The main thread does not encode. A full 32x32 chunk whose every cell carries nested metadata:
	# the request plus every completion pump must stay far below one staged encode of it.
	var heavy: Node = load(BASE + "GridCellDataComponent.cs").new()
	heavy.name = "Heavy"
	host.add_child(heavy)
	heavy.FillTerrain(Rect2i(32, 0, 32, 32), "grass")
	for y in 32:
		for x in 32:
			heavy.SetMetadata(Vector2i(32 + x, y), "nested", {"values": [x, y, x * y], "tag": "cell_%d_%d" % [x, y]})
	var heavy_archive: Node = load(BASE + "GridCellArchiveComponent.cs").new()
	heavy_archive.CellDataPath = NodePath("../Heavy")
	heavy_archive.ArchiveDirectory = "user://tests/capture_budget_heavy_%s_%s" % [OS.get_process_id(), Time.get_ticks_usec()]
	host.add_child(heavy_archive)
	heavy_archive.set_process(false)
	heavy_archive.ChunkSaveFinished.connect(func(hid, success, error): heavy_results[hid] = [success, error])
	var started := Time.get_ticks_usec()
	var heavy_id: int = heavy_archive.RequestSaveChunk(Vector2i(1, 0))
	var request_ms := (Time.get_ticks_usec() - started) / 1000.0
	heavy_archive.set_process(false)
	var pump_ms: float = await pump(heavy_archive)
	check(heavy_results.get(heavy_id) == [true, ""], "Heavy chunk save failed: %s" % heavy_archive.LastError)
	print("[terrain-capture-budget] heavy chunk: request=%.2f ms, completion pumps=%.2f ms" % [request_ms, pump_ms])
	check(request_ms + pump_ms < 25.0, "Main thread spent %.1f ms on one chunk save; encoding belongs to the worker" % (request_ms + pump_ms))
	var heavy_path: String = heavy_archive.GetChunkPath(Vector2i(1, 0))
	check(heavy_archive.LoadChunk(Vector2i(1, 0)) and heavy.GetMetadata(Vector2i(63, 31), "nested") == {"values": [31, 31, 961], "tag": "cell_31_31"},
		"Worker-encoded chunk did not reload exactly")
	# Nor does the main thread decode. Evict the heavy chunk (a reload moved its revision, so it
	# is saved again first) and reload it asynchronously: request plus completion pumps must stay
	# far below decoding a thousand records with nested metadata.
	check(heavy_archive.SaveChunk(Vector2i(1, 0)) and heavy_archive.EvictSavedChunk(Vector2i(1, 0)), "Could not re-archive the heavy chunk: " + heavy_archive.LastError)
	check(not heavy.IsChunkAvailable(Vector2i(1, 0)), "Heavy chunk stayed resident after eviction")
	var before_reload: Dictionary = {}
	started = Time.get_ticks_usec()
	var reload_id: int = heavy_archive.RequestLoadChunk(Vector2i(1, 0))
	var reload_request_ms := (Time.get_ticks_usec() - started) / 1000.0
	heavy_archive.set_process(false)
	var reload_pump_ms := 0.0
	for i in 500:
		var t := Time.get_ticks_usec()
		heavy_archive.ProcessPendingLoad()
		reload_pump_ms += (Time.get_ticks_usec() - t) / 1000.0
		if not heavy_archive.IsBusy: break
		await process_frame
	check(reload_id > 0 and not heavy_archive.IsBusy and heavy.IsChunkAvailable(Vector2i(1, 0))
		and heavy.GetMetadata(Vector2i(63, 31), "nested") == {"values": [31, 31, 961], "tag": "cell_31_31"}
		and heavy.GetMetadata(Vector2i(32, 0), "nested") == {"values": [0, 0, 0], "tag": "cell_0_0"}, "Asynchronous reload did not restore the heavy chunk exactly")
	print("[terrain-capture-budget] heavy chunk reload: request=%.2f ms, completion pumps=%.2f ms" % [reload_request_ms, reload_pump_ms])
	# The request is where a decode would land if it moved back to the main thread (a mutation
	# that did so measured 90 ms here); the pumps carry publication plus whatever GC pause the
	# worker's allocations hand the main thread, so their bound is looser.
	check(reload_request_ms < 25.0, "Request spent %.1f ms on one chunk reload; decoding belongs to the worker" % reload_request_ms)
	check(reload_request_ms + reload_pump_ms < 60.0, "Main thread spent %.1f ms on one chunk reload" % (reload_request_ms + reload_pump_ms))

	# A freed node with a save in flight releases admission and emits nothing.
	id = archive.RequestSaveChunk(Vector2i.ZERO)
	archive.set_process(false)
	check(archive.SharedIoActiveOperations == 1, "Save did not reserve shared IO admission")
	archive.free()
	var observer: Node = load(BASE + "GridCellArchiveComponent.cs").new()
	host.add_child(observer)
	await wait_until(func(): return observer.SharedIoActiveOperations == 0, "Freed saving node leaked admission")
	check(not results.has(id), "Freed saving node emitted completion")
	check(FileAccess.get_file_as_bytes(path) == saved, "Freed saving node changed saved data")
	for file_path: String in [path, heavy_path]:
		var directory: String = file_path.get_base_dir()
		for file in DirAccess.get_files_at(directory):
			check(not file.ends_with(".tmp"), "A worker left a temporary file behind")
			DirAccess.remove_absolute(directory.path_join(file))
		DirAccess.remove_absolute(directory)
	host.free()
	print("[terrain-capture-budget] OK" if failures.is_empty() else "[terrain-capture-budget] FAILED")
	quit(0 if failures.is_empty() else 1)
