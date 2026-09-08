extends SceneTree
const GRID := "res://addons/beep_game_builder_cs/ecs/grid/"
var failures: Array[String] = []
func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		push_error(message)
func wait_until(predicate: Callable, message: String) -> void:
	var deadline := Time.get_ticks_msec() + 10000
	while not predicate.call() and Time.get_ticks_msec() < deadline: await process_frame
	check(predicate.call(), message)
func _initialize() -> void: run.call_deferred()
func run() -> void:
	var host := Node.new()
	root.add_child(host)
	var cells: Node = load(GRID + "GridCellDataComponent.cs").new()
	cells.name = "Cells"
	host.add_child(cells)
	var archive: Node = load(GRID + "GridCellArchiveComponent.cs").new()
	archive.CellDataPath = NodePath("../Cells")
	archive.ArchiveDirectory = "user://tests/chunk_budget_%s_%s" % [OS.get_process_id(), Time.get_ticks_usec()]
	archive.MaximumResidentChunks = 1
	host.add_child(archive)
	for x in 4: cells.SetTerrainKind(Vector2i(x * 32, 0), "forest")
	cells.SetChunkPins(host, [Vector2i.ZERO])
	cells.Till(Vector2i(32, 0))
	cells.PlantCrop(Vector2i(32, 0), "wheat", 2, -1)
	await process_frame
	check(cells.StoredChunkCount == 4 and not archive.IsBusy, "Disabled budget performed work")
	var edited := [false]
	archive.ChunkSaveFinished.connect(func(_id, success, _error):
		if success and not edited[0] and cells.HasCell(Vector2i(64, 0)):
			edited[0] = true
			cells.SetMetadata(Vector2i(64, 0), "newest", 9))
	archive.AutoEnforceChunkBudget = true
	await wait_until(func(): return cells.StoredChunkCount == 2 and not archive.IsBusy, "Budget did not retire eligible chunks")
	check(archive.ResidentChunksOverBudget == 1 and cells.IsChunkAvailable(Vector2i.ZERO) and cells.IsChunkAvailable(Vector2i(1, 0)), "Budget discarded protected simulation to reach its target")
	cells.AdvanceDay(1)
	check(cells.GetCropAgeDays(Vector2i(32, 0)) == 1, "Budget suspended crop simulation")
	archive.AutoEnforceChunkBudget = false
	check(archive.LoadChunk(Vector2i(2, 0)) and cells.GetMetadata(Vector2i(64, 0), "newest") == 9, "Edit after save completion was discarded during budget eviction")
	# Demand loading and pressure coexist; requested chunks are never immediately evicted.
	cells.SetChunkPins(host, [Vector2i.ZERO, Vector2i(2, 0), Vector2i(3, 0)])
	archive.AutoLoadPinnedChunks = true
	archive.AutoEnforceChunkBudget = true
	await wait_until(func(): return cells.IsChunkAvailable(Vector2i(3, 0)), "Pinned demand failed under budget pressure")
	check(cells.StoredChunkCount == 4 and archive.ResidentChunksOverBudget == 3, "Budget overrode newly loaded demand")
	cells.ReleaseChunkPins(host)
	cells.RemoveCrop(Vector2i(32, 0), false)
	await wait_until(func(): return cells.StoredChunkCount == 1 and not archive.IsBusy, "Budget did not recover after pins and crops were released")
	archive.AutoEnforceChunkBudget = false
	archive.AutoLoadPinnedChunks = false
	# A write destination conflict must retain its cells and allow another candidate to retire.
	cells.ClearCells()
	for x in 3: cells.SetTerrainKind(Vector2i(x * 32, 0), "mud")
	cells.SetChunkPins(host, [Vector2i.ZERO])
	var conflict: String = archive.GetChunkPath(Vector2i(1, 0))
	DirAccess.remove_absolute(conflict)
	DirAccess.make_dir_absolute(conflict)
	archive.AutoEnforceChunkBudget = true
	await wait_until(func(): return cells.IsChunkEvicted(Vector2i(2, 0)) and not archive.IsBusy, "Failed save starved other eligible chunks")
	check(cells.HasCell(Vector2i(32, 0)) and cells.StoredChunkCount == 2, "Failed publication discarded live cells")
	archive.AutoEnforceChunkBudget = false
	DirAccess.remove_absolute(conflict)
	# Explicit pumping lets the test disable the policy before publication, regardless of worker speed.
	archive.AutoEnforceChunkBudget = true
	archive.set_process(false)
	await wait_until(func():
		archive.ProcessChunkBudget()
		archive.set_process(false)
		return archive.IsSaving, "Budget retry did not start")
	archive.AutoEnforceChunkBudget = false
	await wait_until(func(): return not archive.IsBusy, "Disabled budget failed to drain its save")
	check(cells.HasCell(Vector2i(32, 0)), "Disabling the policy still evicted pending data")
	# A published budget save is verified and released in the same frame, not at the next 100 ms
	# tick. Explicit pumping again: request, publish, then one budget call with no time passing.
	archive.AutoEnforceChunkBudget = true
	archive.set_process(false)
	await wait_until(func():
		archive.ProcessChunkBudget()
		archive.set_process(false)
		return archive.IsSaving, "Budget did not request the remaining save")
	await wait_until(func():
		archive.ProcessPendingSave()
		archive.set_process(false)
		return not archive.IsBusy, "Budget save did not publish")
	archive.ProcessChunkBudget()
	archive.set_process(false)
	check(cells.IsChunkEvicted(Vector2i(1, 0)), "Published budget save waited for the next 100 ms tick instead of releasing in the same frame")
	archive.AutoEnforceChunkBudget = false
	cells.ReleaseChunkPins(host)
	for x in 4: DirAccess.remove_absolute(archive.GetChunkPath(Vector2i(x, 0)))
	DirAccess.remove_absolute(ProjectSettings.globalize_path(archive.ArchiveDirectory))
	host.free()
	print("[terrain-chunk-budget] OK" if failures.is_empty() else "[terrain-chunk-budget] FAILED")
	quit(0 if failures.is_empty() else 1)
