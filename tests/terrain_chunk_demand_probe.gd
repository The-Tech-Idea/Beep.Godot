extends SceneTree
const GRID := "res://addons/beep_game_builder_cs/ecs/grid/"
var failures: Array[String] = []
var results: Dictionary = {}
func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		push_error(message)
func wait_until(predicate: Callable, description: String) -> void:
	var deadline := Time.get_ticks_msec() + 10000
	while not predicate.call() and Time.get_ticks_msec() < deadline: await process_frame
	check(predicate.call(), description)
func _initialize() -> void: run.call_deferred()
func run() -> void:
	var host := Node.new()
	root.add_child(host)
	var cells: Node = load(GRID + "GridCellDataComponent.cs").new()
	cells.name = "Cells"
	host.add_child(cells)
	var archive: Node = load(GRID + "GridCellArchiveComponent.cs").new()
	archive.CellDataPath = NodePath("../Cells")
	archive.ArchiveDirectory = "user://tests/chunk_demand_%s_%s" % [OS.get_process_id(), Time.get_ticks_usec()]
	archive.DemandRetrySeconds = 5
	host.add_child(archive)
	archive.ChunkLoadFinished.connect(func(id, success, error): results[id] = [success, error])
	var a := Vector2i.ZERO
	var b := Vector2i(1, 0)
	for chunk in [a, b]:
		cells.SetTerrainKind(chunk * 32, "forest")
		check(archive.SaveChunk(chunk) and archive.EvictSavedChunk(chunk), "Could not prepare archived chunk")
	cells.SetChunkPins(host, [a])
	await process_frame
	check(not archive.IsBusy and not cells.IsChunkAvailable(a), "Disabled automatic loading performed I/O")
	archive.AutoLoadPinnedChunks = true
	await wait_until(func(): return cells.IsChunkAvailable(a), "Pinned chunk did not load automatically")
	check(cells.GetTerrainKind(Vector2i.ZERO) == "forest" and not cells.IsChunkAvailable(b), "Loader restored unrequested data")
	var missing := Vector2i(-1, 0)
	cells.SetChunkAvailable(missing, false)
	cells.SetChunkPins(host, [missing, b])
	await wait_until(func(): return cells.IsChunkAvailable(b), "Missing file starved another requested chunk")
	await wait_until(func(): return results.values().any(func(result): return not result[0]), "Missing-file attempt was not reported")
	var attempts: int = results.size()
	await create_timer(0.4).timeout
	check(results.size() == attempts and not cells.IsChunkAvailable(missing), "Missing file retried continuously or became ready")
	# Lose demand after the worker starts, before the main-thread publication.
	cells.ReleaseChunkPins(host)
	check(archive.SaveChunk(a) and archive.EvictSavedChunk(a), "Cancellation fixture failed")
	cells.SetChunkPins(host, [a])
	archive.set_process(false)
	await wait_until(func():
		archive.ProcessChunkDemand()
		archive.set_process(false)
		return archive.IsLoading, "Automatic read did not start")
	var cancelled: int = archive.PendingLoadId
	cells.ReleaseChunkPins(host)
	archive.set_process(true)
	await wait_until(func(): return results.has(cancelled), "Cancelled demand did not drain")
	check(not results[cancelled][0] and not cells.IsChunkAvailable(a), "Lost demand published stale chunk")
	archive.AutoLoadPinnedChunks = false
	var manual: int = archive.RequestLoadChunk(a)
	await wait_until(func(): return results.has(manual), "Manual load stopped working with automatic mode disabled")
	check(results[manual][0] and cells.IsChunkAvailable(a), "Automatic policy cancelled explicit restore")
	# Default terrain can change away and back while a manual read is in flight.
	manual = archive.RequestLoadChunk(b)
	cells.DefaultTerrainKind = "mud"
	cells.DefaultTerrainKind = "grass"
	await wait_until(func(): return results.has(manual), "Changed-default read did not drain")
	check(not results[manual][0], "Default change away/back escaped load revision validation")
	archive.AutoLoadPinnedChunks = true
	cells.SetChunkPins(host, [missing])
	host.remove_child(archive)
	host.add_child(archive)
	archive.set_process(false)
	await wait_until(func():
		archive.ProcessChunkDemand()
		archive.set_process(false)
		return archive.IsLoading, "Reattached automatic archive stopped processing")
	archive.AutoLoadPinnedChunks = false
	await wait_until(func(): return not archive.IsBusy, "Disabling automatic loader did not drain read")
	cells.ReleaseChunkPins(host)
	for chunk in [a, b]: DirAccess.remove_absolute(archive.GetChunkPath(chunk))
	DirAccess.remove_absolute(ProjectSettings.globalize_path(archive.ArchiveDirectory))
	host.free()
	print("[terrain-chunk-demand] OK" if failures.is_empty() else "[terrain-chunk-demand] FAILED")
	quit(0 if failures.is_empty() else 1)
