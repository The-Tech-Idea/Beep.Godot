extends SceneTree

# FIX-06: the archive bulk load-abort listener must read the CellsChanged payload.
# A Residency-only move or a scoped edit of an unrelated chunk must NOT abort an
# in-flight demand load; a whole-map change (empty chunk list) MUST still abort it.
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

func request(archive: Node) -> int:
	var id: int = archive.RequestLoadChunk(Vector2i.ZERO)
	archive.set_process(false)
	check(id > 0, "Read request was not accepted: " + archive.LastError)
	return id

func reset_cell(cells: Node) -> void:
	cells.SetChunkAvailable(Vector2i.ZERO, false)
	cells.SetTerrainKind(Vector2i.ZERO, "grass")

func _initialize() -> void: run.call_deferred()

func run() -> void:
	var host := Node.new()
	root.add_child(host)
	var cells: Node = load(GRID + "GridCellDataComponent.cs").new()
	cells.name = "Cells"
	host.add_child(cells)
	var archive: Node = load(GRID + "GridCellArchiveComponent.cs").new()
	archive.CellDataPath = NodePath("../Cells")
	archive.ArchiveDirectory = "user://tests/chunk_load_abort_%s_%s" % [OS.get_process_id(), Time.get_ticks_usec()]
	host.add_child(archive)
	archive.ChunkLoadFinished.connect(func(id, success, error): results[id] = [success, error])

	cells.SetTerrainKind(Vector2i.ZERO, "water")
	check(archive.SaveChunk(Vector2i.ZERO), "Could not prepare chunk file")
	var path: String = archive.GetChunkPath(Vector2i.ZERO)
	reset_cell(cells)

	# (1) Eviction of an UNRELATED chunk is a Residency change and must not abort the read.
	cells.SetTerrainKind(Vector2i(160, 0), "forest")   # chunk (5,0), never touched elsewhere
	var id := request(archive)
	var evicted := Vector2i(5, 0)
	check(cells.TryEvictChunk(evicted, cells.GetChunkRevision(evicted)), "Could not evict the unrelated chunk")
	await pump(archive)
	check(results[id][0] and cells.GetTerrainKind(Vector2i.ZERO) == "water",
		"Residency change of an unrelated chunk aborted the read")

	# (2) A scoped content change of an UNRELATED chunk must not abort the read either.
	reset_cell(cells)
	id = request(archive)
	cells.FillTerrain(Rect2i(192, 0, 2, 2), "desert")  # chunk (6,0)
	await pump(archive)
	check(results[id][0] and cells.GetTerrainKind(Vector2i.ZERO) == "water",
		"Scoped content change of an unrelated chunk aborted the read")

	# (3) A whole-map change (empty chunk list) MUST still abort the read.
	reset_cell(cells)
	id = request(archive)
	cells.ClearCells()
	await pump(archive)
	check(not results[id][0] and results[id][1] == "cell_data_changed",
		"Whole-map change did not abort the in-flight read")

	DirAccess.remove_absolute(path)
	DirAccess.remove_absolute(ProjectSettings.globalize_path(archive.ArchiveDirectory))
	host.free()
	print("[terrain-chunk-load-abort] OK" if failures.is_empty() else "[terrain-chunk-load-abort] FAILED")
	quit(0 if failures.is_empty() else 1)
