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
	archive.ArchiveDirectory = "user://tests/cell_budget_%s_%s" % [OS.get_process_id(), Time.get_ticks_usec()]
	archive.MaximumResidentChunks = 256
	host.add_child(archive)
	cells.SetTerrainKind(Vector2i(-32, 0), "forest")
	cells.SetTerrainKind(Vector2i.ZERO, "grass")
	cells.FillTerrain(Rect2i(32, 0, 32, 32), "desert")
	check(cells.CellCount == 1026 and cells.StoredChunkCount == 3, "Sparse/dense fixture count incorrect")
	check(cells.GetStoredChunkCellCount(Vector2i(-1, 0)) == 1 and cells.GetStoredChunkCellCount(Vector2i(1, 0)) == 1024, "Per-chunk counts incorrect")
	check(cells.GetStoredChunkCellCount(Vector2i(9, 9)) == 0, "Missing chunk reports stored cells")
	archive.AutoEnforceChunkBudget = true
	for i in 12: await process_frame
	check(cells.CellCount == 1026 and archive.ResidentCellsOverBudget == 0, "Default cell budget is not disabled")
	archive.MaximumResidentCells = 2
	await wait_until(func(): return cells.CellCount == 2 and not archive.IsBusy, "Cell pressure did not retire dense chunk")
	check(cells.HasCell(Vector2i(-32, 0)) and cells.HasCell(Vector2i.ZERO), "Dense-first selection unnecessarily evicted sparse chunks")
	check(cells.GetStoredChunkCellCount(Vector2i(1, 0)) == 0 and archive.ResidentChunksOverBudget == 0 and archive.ResidentCellsOverBudget == 0, "Eviction left stale cell accounting")
	archive.AutoEnforceChunkBudget = false
	check(archive.LoadChunk(Vector2i(1, 0)) and cells.CellCount == 1026, "Reload did not restore dense count")
	cells.SetChunkPins(host, [Vector2i(-1, 0), Vector2i.ZERO, Vector2i(1, 0)])
	archive.AutoEnforceChunkBudget = true
	for i in 20: await process_frame
	check(archive.ResidentCellsOverBudget == 1024 and cells.CellCount == 1026, "Cell pressure overrode pins")
	# Lowering pressure while a save is running must not discard its source after publication.
	archive.set_process(false)
	cells.ReleaseChunkPins(host)
	cells.SetMetadata(Vector2i(32, 0), "revision", 1)
	await wait_until(func():
		archive.ProcessChunkBudget()
		archive.set_process(false)
		return archive.IsSaving, "Edited dense chunk was not scheduled for saving")
	archive.MaximumResidentCells = 2048
	archive.set_process(true)
	await wait_until(func(): return not archive.IsBusy, "Budget save did not drain")
	for i in 20: await process_frame
	check(cells.CellCount == 1026 and cells.HasCell(Vector2i(32, 0)), "Raised budget still evicted pending candidate")
	archive.MaximumResidentCells = 2
	await wait_until(func(): return cells.CellCount == 2 and not archive.IsBusy, "Lowered cell limit did not resume retirement")
	archive.AutoEnforceChunkBudget = false
	check(archive.LoadChunk(Vector2i(1, 0)) and cells.GetMetadata(Vector2i(32, 0), "revision") == 1, "Cell budget lost latest edit")
	cells.ClearCells()
	check(cells.CellCount == 0 and archive.ResidentCellsOverBudget == 0, "Full replacement retained stale pressure")
	for at in [Vector2i(-1, 0), Vector2i.ZERO, Vector2i(1, 0)]: DirAccess.remove_absolute(archive.GetChunkPath(at))
	DirAccess.remove_absolute(ProjectSettings.globalize_path(archive.ArchiveDirectory))
	host.free()
	print("[terrain-cell-budget] OK" if failures.is_empty() else "[terrain-cell-budget] FAILED")
	quit(0 if failures.is_empty() else 1)
