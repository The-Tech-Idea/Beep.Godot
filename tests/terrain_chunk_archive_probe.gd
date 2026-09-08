extends SceneTree

const GRID := "res://addons/beep_game_builder_cs/ecs/grid/"
var failures: Array[String] = []
var publications := 0

func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		push_error(message)

func write(path: String, bytes: PackedByteArray) -> void:
	var file := FileAccess.open(path, FileAccess.WRITE)
	file.store_buffer(bytes)
	file.close()

func _initialize() -> void: run.call_deferred()

func run() -> void:
	var host := Node.new()
	root.add_child(host)
	var cells: Node = load(GRID + "GridCellDataComponent.cs").new()
	cells.name = "Cells"
	host.add_child(cells)
	var archive: Node = load(GRID + "GridCellArchiveComponent.cs").new()
	archive.CellDataPath = NodePath("../Cells")
	host.add_child(archive)
	check(not archive.SaveChunk(Vector2i.ZERO), "Archive silently chose a shared default directory")
	archive.ArchiveDirectory = "user://tests/chunk_archive_%s_%s" % [OS.get_process_id(), Time.get_ticks_usec()]
	cells.CellsChanged.connect(func(): publications += 1)
	var cell := Vector2i(-1, -33)
	var chunk := Vector2i(-1, -2)
	var neighbor := Vector2i(0, -33)
	cells.SetTerrainKind(cell, "water")
	cells.SetFlags(cell, 1)
	cells.SetMetadata(cell, "example", {"points": [Vector2i(4, 5), Vector2(1.25, 2.5)], "flags": PackedByteArray([0, 255])})
	cells.SetTerrainKind(neighbor, "desert")
	var expected: Dictionary = cells.GetCell(cell).duplicate(true)
	check(archive.SaveChunk(chunk), "Saving a typed negative-coordinate chunk failed: " + archive.LastError)
	var path: String = archive.GetChunkPath(chunk)
	var original := FileAccess.get_file_as_bytes(path)
	check(not original.is_empty(), "Archive did not create an actual disk file")
	var captured: Dictionary = cells.CaptureSingleChunkState(chunk)
	check(Marshalls.base64_to_raw(captured.chunks[0].data) == var_to_bytes(cells.GetChunkCells(chunk)), "Optimized capture changed the cell packet")
	# Only metadata is user-controlled; every unsupported nested type must still fail.
	for unsupported in [host, Callable(self, "run"), host.tree_exiting, RID(), {host: "object key"}]:
		cells.SetMetadata(cell, "unsafe", {"nested": [unsupported]})
		check(not archive.SaveChunk(chunk) and FileAccess.get_file_as_bytes(path) == original, "Unsupported metadata replaced a valid archive")
		check(archive.LoadChunk(chunk), "Could not restore after unsupported metadata")
	var nested: Variant = "leaf"
	for i in 61: nested = [nested]
	cells.SetMetadata(cell, "depth", nested)
	check(archive.SaveChunk(chunk), "Portable metadata at depth 64 was rejected")
	var deepest := FileAccess.get_file_as_bytes(path)
	cells.SetMetadata(cell, "depth", [nested])
	check(not archive.SaveChunk(chunk) and FileAccess.get_file_as_bytes(path) == deepest, "Depth 65 metadata passed validation")
	write(path, original)
	check(archive.LoadChunk(chunk), "Could not restore after depth validation")
	cells.SetTerrainKind(cell, "grass")
	cells.SetTerrainKind(Vector2i(-2, -33), "mud")
	publications = 0
	check(archive.LoadChunk(chunk), "Loading saved chunk failed: " + archive.LastError)
	check(cells.GetCell(cell) == expected and not cells.HasCell(Vector2i(-2, -33)), "Chunk restore lost metadata or retained stale records")
	check(cells.GetTerrainKind(neighbor) == "desert" and cells.CellCount == 2 and publications == 1, "Chunk restore changed a neighbor or published more than once")
	var before: Dictionary = cells.CaptureChunkState()
	publications = 0
	write(path, "truncated{".to_utf8_buffer())
	check(not archive.LoadChunk(chunk) and not archive.LastError.is_empty() and publications == 0, "Corrupt archive was accepted or published")
	check(cells.CaptureChunkState() == before, "Failed archive load mutated live cells")
	var wrong: Dictionary = cells.CaptureSingleChunkState(Vector2i.ZERO)
	write(path, JSON.stringify(wrong).to_utf8_buffer())
	check(not archive.LoadChunk(chunk) and publications == 0, "Wrong-coordinate archive overwrote requested chunk")
	write(path, JSON.stringify(before).to_utf8_buffer())
	check(not archive.LoadChunk(chunk) and publications == 0, "Multi-chunk packet was accepted by a single-chunk load")
	write(path, original)
	cells.SetMetadata(cell, "large", "x".repeat(15000))
	archive.MaximumChunkBytes = 1024
	check(not archive.SaveChunk(chunk) and FileAccess.get_file_as_bytes(path) == original, "Oversized save destroyed previous archive")
	write(path, "x".repeat(2048).to_utf8_buffer())
	check(not archive.LoadChunk(chunk) and publications == 0, "Oversized file was accepted")
	write(path, original)
	archive.MaximumChunkBytes = 8 * 1024 * 1024
	check(archive.LoadChunk(chunk), "Valid archive could not recover after rejected operations")
	check(not archive.LoadChunk(Vector2i(100, 100)), "Missing archive reported success")
	var empty := Vector2i(2, 2)
	check(archive.SaveChunk(empty), "Could not save an empty chunk tombstone")
	cells.SetTerrainKind(Vector2i(64, 64), "forest")
	check(archive.LoadChunk(empty) and not cells.HasCell(Vector2i(64, 64)) and cells.CellCount == 2, "Empty chunk restore did not remove only its own records")
	var complete: Dictionary = cells.CaptureChunkState()
	cells.ClearCells()
	cells.RestoreChunkState(complete)
	check(cells.GetCell(cell) == expected and cells.GetTerrainKind(neighbor) == "desert", "Full snapshot codec regressed after single-chunk support")
	var directory := ProjectSettings.globalize_path(archive.ArchiveDirectory)
	check(DirAccess.get_files_at(directory).size() == 2, "Archive left temporary files behind")
	# Remove only the two files created in this probe's unique directory.
	DirAccess.remove_absolute(path)
	DirAccess.remove_absolute(archive.GetChunkPath(empty))
	DirAccess.remove_absolute(directory)
	host.free()
	print("[terrain-chunk-archive] OK" if failures.is_empty() else "[terrain-chunk-archive] FAILED")
	quit(0 if failures.is_empty() else 1)
