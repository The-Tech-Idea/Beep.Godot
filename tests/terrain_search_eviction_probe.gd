extends SceneTree

const BASE := "res://addons/beep_game_builder_cs/ecs/grid/"
var failures: Array[String] = []

func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		push_error(message)

func make(kind: String, host: Node, label: String, properties: Dictionary = {}) -> Node:
	var node: Node = load(BASE + kind + "Component.cs").new()
	node.name = label
	for key in properties: node.set(key, properties[key])
	host.add_child(node)
	return node

func _initialize() -> void: run.call_deferred()

func run() -> void:
	var host := Node.new()
	root.add_child(host)
	var cells := make("GridCellData", host, "Cells")
	cells.FillTerrain(Rect2i(0, 0, 512, 1), "grass")
	var archive := make("GridCellArchive", host, "Archive", {
		"CellDataPath": NodePath("../Cells"),
		"ArchiveDirectory": "user://tests/search_eviction_%s_%s" % [OS.get_process_id(), Time.get_ticks_usec()]})
	for i in 42:
		cells.SetTerrainKind(Vector2i((100 + i) * 32, 0), "grass")
		check(archive.SaveChunk(Vector2i(100 + i, 0)), "Could not prepare distant archive")
	var nav := make("GridNavigation", host, "Navigation", {
		"CellDataPath": NodePath("../Cells"), "BoundsSize": Vector2i(512, 1),
		"LoadMissingTerrain": true, "PathExpansionsPerFrame": 16,
		"PathMillisecondsPerFrame": 0.0, "MaximumActiveSearches": 1})
	var outcomes := {}
	nav.PathRequestCompleted.connect(func(id, path, reason): outcomes[id] = {"length": path.size(), "reason": reason})
	var id: int = nav.RequestCellPath(Vector2i.ZERO, Vector2i(500, 0))
	nav.ProcessPathRequests()
	check(cells.IsChunkPinned(Vector2i.ZERO), "Search did not protect its observed terrain")
	for i in 40:
		check(archive.EvictSavedChunk(Vector2i(100 + i, 0)), "Could not evict unrelated chunk")
		nav.ProcessPathRequests()
	check(outcomes.has(id) and outcomes[id].reason == "" and outcomes[id].length == 501,
		"Unrelated evictions repeatedly restarted the protected search")
	check(cells.GetPinnedChunks().is_empty(), "Completed search retained pins")
	# Real edits must still invalidate the cached search, even in streaming mode.
	id = nav.RequestCellPath(Vector2i.ZERO, Vector2i(500, 0))
	nav.ProcessPathRequests()
	cells.SetTerrainKind(Vector2i(20, 0), "deep_water")
	for i in 40: nav.ProcessPathRequests()
	check(outcomes.has(id) and outcomes[id].reason == "no_path", "Terrain edit was ignored by pinned search")
	cells.SetTerrainKind(Vector2i(20, 0), "grass")
	# Non-streaming searches have no pin guarantee and retain global invalidation.
	nav.LoadMissingTerrain = false
	id = nav.RequestCellPath(Vector2i.ZERO, Vector2i(500, 0))
	nav.ProcessPathRequests()
	check(archive.EvictSavedChunk(Vector2i(140, 0)), "Could not prepare unpinned invalidation")
	nav.ProcessPathRequests()
	check(outcomes.has(id) and outcomes[id].reason == "navigation_changed", "Unpinned search lost eviction invalidation")
	for i in 42: DirAccess.remove_absolute(archive.GetChunkPath(Vector2i(100 + i, 0)))
	DirAccess.remove_absolute(ProjectSettings.globalize_path(archive.ArchiveDirectory))
	host.free()
	print("[terrain-search-eviction] OK" if failures.is_empty() else "[terrain-search-eviction] FAILED")
	quit(0 if failures.is_empty() else 1)
