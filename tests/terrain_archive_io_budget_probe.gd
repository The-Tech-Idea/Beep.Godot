extends SceneTree
const GRID := "res://addons/beep_game_builder_cs/ecs/grid/"
const MIB := 1024 * 1024
var failures: Array[String] = []
func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		push_error(message)
func wait_until(predicate: Callable, message: String) -> void:
	var deadline := Time.get_ticks_msec() + 10000
	while not predicate.call() and Time.get_ticks_msec() < deadline: await process_frame
	check(predicate.call(), message)
func pump(archive: Node) -> void:
	await wait_until(func():
		archive.ProcessPendingLoad()
		archive.ProcessPendingSave()
		archive.set_process(false)
		return not archive.IsBusy, "Archive failed to drain")
func _initialize() -> void:
	ProjectSettings.set_setting("beep/streaming/archive_max_operations", 2)
	ProjectSettings.set_setting("beep/streaming/archive_max_inflight_bytes", 16 * MIB)
	run.call_deferred()
func run() -> void:
	var host := Node.new()
	root.add_child(host)
	var archives: Array[Node] = []
	var cells: Array[Node] = []
	var directories: Array[String] = []
	for i in 3:
		var data: Node = load(GRID + "GridCellDataComponent.cs").new()
		data.name = "Cells%d" % i
		host.add_child(data)
		data.SetTerrainKind(Vector2i.ZERO, "grass")
		cells.append(data)
		var archive: Node = load(GRID + "GridCellArchiveComponent.cs").new()
		archive.CellDataPath = NodePath("../" + data.name)
		archive.ArchiveDirectory = "user://tests/archive_io_%s_%s_%d" % [OS.get_process_id(), Time.get_ticks_usec(), i]
		host.add_child(archive)
		check(archive.SaveChunk(Vector2i.ZERO), "Could not create archive fixture")
		archives.append(archive)
		directories.append(ProjectSettings.globalize_path(archive.ArchiveDirectory))
	var a := archives[0]
	var b := archives[1]
	var c := archives[2]
	check(a.SharedIoOperationLimit == 2 and a.SharedIoByteLimit == 16 * MIB, "Project admission settings were not applied")
	for archive in [a, b, c]: archive.MaximumChunkBytes = MIB
	for archive in [a, b]:
		check(archive.RequestSaveChunk(Vector2i.ZERO) > 0, "Initial admission failed")
		archive.set_process(false)
	check(c.SharedIoActiveOperations == 2 and c.SharedIoReservedBytes == 2 * MIB, "Separate archives do not share admission")
	check(c.RequestLoadChunk(Vector2i.ZERO) == 0 and c.LastError == "archive_io_busy", "Saturated worker read was accepted")
	check(c.RequestSaveChunk(Vector2i.ZERO) == 0 and c.LastError == "archive_io_busy", "Saturated snapshot capture was accepted")
	check(not c.SaveChunk(Vector2i.ZERO) and not c.LoadChunk(Vector2i.ZERO) and not c.EvictSavedChunk(Vector2i.ZERO), "Synchronous operations bypass shared admission")
	a.CancelSave()
	check(c.SharedIoActiveOperations == 2, "Cancellation released capacity before draining")
	await pump(a)
	check(c.SharedIoActiveOperations == 1 and c.RequestLoadChunk(Vector2i.ZERO) > 0, "Released capacity did not admit a retry")
	c.set_process(false)
	# A permanently freed archive releases its worker lease without a main-thread completion pump.
	b.free()
	await wait_until(func(): return c.SharedIoActiveOperations == 1, "Freed write leaked shared capacity")
	await pump(c)
	check(c.SharedIoActiveOperations == 0 and c.SharedIoReservedBytes == 0, "Completed work retained reservations")
	a.MaximumChunkBytes = 12 * MIB
	c.MaximumChunkBytes = 8 * MIB
	check(c.SaveChunk(Vector2i.ZERO), "Could not refresh current-save marker after loading")
	check(a.RequestSaveChunk(Vector2i.ZERO) > 0, "Large payload envelope not admitted")
	a.set_process(false)
	check(c.SharedIoActiveOperations == 1 and c.RequestLoadChunk(Vector2i.ZERO) == 0 and c.LastError == "archive_io_busy", "Byte cap failed below the operation cap")
	check(not c.EvictSavedChunk(Vector2i.ZERO) and c.LastError == "archive_io_busy", "Eviction verification bypassed its read reservation")
	a.CancelSave()
	await pump(a)
	a.MaximumChunkBytes = 32 * MIB
	check(a.RequestSaveChunk(Vector2i.ZERO) == 0 and a.LastError == "archive_io_request_too_large" and c.SharedIoActiveOperations == 0, "Impossible payload envelope occupied capacity")
	a.MaximumChunkBytes = 8 * MIB
	cells[0].SetChunkAvailable(Vector2i.ZERO, false)
	check(a.RequestSaveChunk(Vector2i.ZERO) == 0 and c.SharedIoReservedBytes == 0, "Snapshot capture failure leaked admission")
	cells[0].SetChunkAvailable(Vector2i.ZERO, true)
	check(a.RequestLoadChunk(Vector2i.ZERO) > 0, "Detached read fixture rejected")
	a.set_process(false)
	host.remove_child(a)
	host.add_child(a)
	a.set_process(false)
	await pump(a)
	check(c.SharedIoActiveOperations == 0, "Detached read leaked admission")
	var removed_reader: Node = load(GRID + "GridCellArchiveComponent.cs").new()
	removed_reader.CellDataPath = a.CellDataPath
	removed_reader.ArchiveDirectory = a.ArchiveDirectory
	host.add_child(removed_reader)
	check(removed_reader.RequestLoadChunk(Vector2i.ZERO) > 0, "Permanent reader removal fixture rejected")
	removed_reader.set_process(false)
	removed_reader.free()
	await wait_until(func(): return c.SharedIoActiveOperations == 0, "Freed read required a completion pump to release capacity")
	check(not c.LoadChunk(Vector2i(99, 99)) and c.SharedIoReservedBytes == 0, "Failed synchronous read leaked admission")
	# Automatic terrain demand retries backpressure, rather than treating it as missing terrain.
	for archive in [a, c]:
		check(archive.RequestLoadChunk(Vector2i.ZERO) > 0, "Demand pressure fixture rejected")
		archive.set_process(false)
	var retry: Node = load(GRID + "GridCellArchiveComponent.cs").new()
	retry.CellDataPath = c.CellDataPath
	retry.ArchiveDirectory = c.ArchiveDirectory
	retry.DemandRetrySeconds = 0.25
	host.add_child(retry)
	cells[2].SetChunkAvailable(Vector2i.ZERO, false)
	cells[2].SetChunkPins(host, [Vector2i.ZERO])
	retry.AutoLoadPinnedChunks = true
	retry.ProcessChunkDemand()
	check(not retry.IsBusy and retry.LastError == "archive_io_busy", "Automatic demand ignored pressure")
	a.CancelLoad()
	c.CancelLoad()
	await pump(a)
	await pump(c)
	await wait_until(func(): return cells[2].IsChunkAvailable(Vector2i.ZERO) and not retry.IsBusy, "Automatic demand failed to recover after pressure")
	retry.AutoLoadPinnedChunks = false
	cells[2].ReleaseChunkPins(host)
	check(retry.SharedIoActiveOperations == 0 and retry.SharedIoReservedBytes == 0, "Automatic read leaked admission")
	host.free()
	for directory in directories:
		for file in DirAccess.get_files_at(directory):
			check(not file.ends_with(".tmp"), "Detached worker left a temporary file")
			DirAccess.remove_absolute(directory.path_join(file))
		DirAccess.remove_absolute(directory)
	print("[terrain-archive-io-budget] OK" if failures.is_empty() else "[terrain-archive-io-budget] FAILED")
	quit(0 if failures.is_empty() else 1)
