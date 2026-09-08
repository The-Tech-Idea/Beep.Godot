extends SceneTree

const GRID := "res://addons/beep_game_builder_cs/ecs/grid/"
var failures: Array[String] = []

func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		push_error(message)

func _initialize() -> void: run.call_deferred()

func run() -> void:
	var host := Node.new()
	root.add_child(host)
	var cells: Node = load(GRID + "GridCellDataComponent.cs").new()
	cells.name = "Cells"
	host.add_child(cells)
	var archive: Node = load(GRID + "GridCellArchiveComponent.cs").new()
	archive.CellDataPath = NodePath("../Cells")
	archive.ArchiveDirectory = "user://tests/chunk_revisions_%s_%s" % [OS.get_process_id(), Time.get_ticks_usec()]
	host.add_child(archive)
	var here := Vector2i(-1, -33)
	var chunk := Vector2i(-1, -2)
	var neighbor := Vector2i.ZERO
	cells.SetTerrainKind(here, "grass")
	cells.SetTerrainKind(neighbor, "desert")
	check(not archive.IsChunkSaveCurrent(chunk), "Unsaved chunk was current")
	check(archive.SaveChunk(chunk) and archive.SaveChunk(neighbor), "Fixture could not be saved")
	var original: int = cells.GetChunkRevision(chunk)
	check(archive.IsChunkSaveCurrent(chunk), "Successful save did not record current revision")
	var mutations: Array[Callable] = [
		func(): cells.SetTerrainKind(here, "forest"),
		func(): cells.SetFlags(here, 0),
		func(): cells.AddFlag(here, 1),
		func(): cells.RemoveFlag(here, 1),
		func(): cells.ClearLand(here),
		func(): cells.Till(here),
		func(): cells.Water(here),
		func(): cells.PlantCrop(here, "wheat", 1, -1),
		func(): cells.AdvanceDay(1),
		func(): cells.HarvestCrop(here, false),
		func(): cells.PlantCrop(here, "berry", 1, 2),
		func(): cells.HarvestCrop(here, false),
		func(): cells.RemoveCrop(here, false),
		func(): cells.SetMetadata(here, "custom", {"value": 3}),
		func(): cells.FillTerrain(Rect2i(here, Vector2i.ONE), "mud"),
		func(): cells.LoadCells([{"cell": here, "terrain": "grass"}], false)
	]
	for i in mutations.size():
		var before: int = cells.GetChunkRevision(chunk)
		mutations[i].call()
		check(cells.GetChunkRevision(chunk) > before and not archive.IsChunkSaveCurrent(chunk), "Mutation %s did not invalidate saved chunk" % i)
		check(archive.IsChunkSaveCurrent(neighbor), "Mutation %s invalidated unrelated chunk" % i)
		check(archive.SaveChunk(chunk) and archive.IsChunkSaveCurrent(chunk), "Resave did not update revision")
	check(cells.GetChunkRevision(chunk) > original, "Chunk revision was reset/reused after local edits")
	# Water expires even on an uncropped cell; dry, uncropped neighbors are unchanged.
	cells.Water(here)
	check(archive.SaveChunk(chunk), "Could not save uncropped watered cell")
	var events := [0]
	var changed := func(x, y):
		if Vector2i(x, y) == here: events[0] += 1
	cells.CellChanged.connect(changed)
	cells.AdvanceDay(1)
	cells.CellChanged.disconnect(changed)
	check(not archive.IsChunkSaveCurrent(chunk) and not cells.HasFlag(here, 8) and events[0] == 1, "Uncropped water expiry bypassed tracking or notification")
	check(archive.IsChunkSaveCurrent(neighbor), "Day advancement invalidated unchanged desert chunk")
	# Both directions of the metadata boundary must be detached, including nested containers.
	var metadata := {"nested": [{"value": 1}], "packed": PackedByteArray([1, 2])}
	cells.SetMetadata(here, "mutable", metadata)
	check(archive.SaveChunk(chunk), "Metadata fixture save failed")
	metadata.nested[0].value = 99
	metadata.packed[0] = 99
	var fetched: Dictionary = cells.GetMetadata(here, "mutable")
	check(fetched.nested[0].value == 1 and fetched.packed[0] == 1, "Setter retained caller's mutable metadata")
	fetched.nested[0].value = 77
	fetched.packed[0] = 77
	var reread: Dictionary = cells.GetMetadata(here, "mutable")
	check(reread.nested[0].value == 1 and reread.packed[0] == 1 and archive.IsChunkSaveCurrent(chunk), "Getter exposed a mutable live metadata alias")
	cells.SetMetadata(here, "mutable", fetched)
	check(not archive.IsChunkSaveCurrent(chunk), "Explicit metadata edit failed to dirty chunk")
	check(archive.SaveChunk(chunk), "Resaving metadata failed")
	var revision: int = cells.GetChunkRevision(chunk)
	cells.SetChunkAvailable(chunk, false)
	check(not archive.IsChunkSaveCurrent(chunk) and cells.GetChunkRevision(chunk) == revision, "Readiness was treated as content or allowed current unavailable data")
	cells.SetChunkAvailable(chunk, true)
	check(archive.IsChunkSaveCurrent(chunk), "Readiness-only roundtrip dirtied retained content")
	var single: Dictionary = cells.CaptureSingleChunkState(chunk)
	cells.RestoreSingleChunkState(chunk, single)
	check(not archive.IsChunkSaveCurrent(chunk) and archive.IsChunkSaveCurrent(neighbor), "Single-chunk publication revision affected wrong scope")
	check(archive.SaveChunk(chunk), "Could not resave restored chunk")
	var complete: Dictionary = cells.CaptureChunkState()
	cells.RestoreChunkState(complete)
	check(not archive.IsChunkSaveCurrent(chunk) and not archive.IsChunkSaveCurrent(neighbor), "Full restore retained saved-content tokens")
	check(archive.SaveChunk(chunk), "Could not save full-restored chunk")
	cells.DefaultTerrainKind = "snow"
	cells.DefaultTerrainKind = "grass"
	check(not archive.IsChunkSaveCurrent(chunk), "Default terrain roundtrip resurrected an old content token")
	check(archive.SaveChunk(chunk), "Could not save after default change")
	archive.CellDataPath = NodePath("../Missing")
	check(not archive.IsChunkSaveCurrent(chunk), "Unbound archive claimed current data")
	archive.CellDataPath = NodePath("../Cells")
	var configured: String = archive.ArchiveDirectory
	archive.ArchiveDirectory = configured + "/another"
	check(not archive.IsChunkSaveCurrent(chunk), "Another directory inherited a saved-content token")
	archive.ArchiveDirectory = configured
	archive.ForgetSavedChunks()
	check(not archive.IsChunkSaveCurrent(chunk), "Explicit invalidation retained saved-content tokens")
	check(archive.SaveChunk(chunk), "Could not save before replacement")
	cells.LoadCells([], true)
	check(not archive.IsChunkSaveCurrent(chunk), "Empty bulk replacement retained saved-content tokens")
	check(archive.SaveChunk(chunk), "Empty snapshot save failed")
	var empty_revision: int = cells.GetChunkRevision(chunk)
	cells.LoadCells([], true)
	check(cells.GetChunkRevision(chunk) > empty_revision and not archive.IsChunkSaveCurrent(chunk), "Empty world replacement reused old default-chunk token")
	var directory := ProjectSettings.globalize_path(configured)
	DirAccess.remove_absolute(archive.GetChunkPath(chunk))
	DirAccess.remove_absolute(archive.GetChunkPath(neighbor))
	DirAccess.remove_absolute(directory)
	host.free()
	print("[terrain-chunk-revisions] OK" if failures.is_empty() else "[terrain-chunk-revisions] FAILED")
	quit(0 if failures.is_empty() else 1)
