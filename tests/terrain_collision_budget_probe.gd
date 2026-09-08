extends "res://tests/terrain_collision_probe.gd"

func step(collision: Node, budget: int) -> void:
	collision.ChunksPerFrame = budget
	collision._Process(0.016)
	collision.set_process(false)
	assert(collision.ChunksRebuiltLastUpdate <= clampi(budget, 1, 64))

func run() -> void:
	create_timer(30).timeout.connect(func(): quit(1))
	var host := Node2D.new()
	root.add_child(host)
	var grid := make("grid/GridProjectionComponent", host, "Grid", {
		"TrackMouseCell": false, "DrawGrid": false})
	var cells := make("grid/GridCellDataComponent", host, "Cells", {"DefaultTerrainKind": "water"})
	var collision := make("terrain/TerrainCollisionComponent", host, "Collision", {
		"GridPath": NodePath("../Grid"), "CellDataPath": NodePath("../Cells"),
		"RefreshOnReady": false, "BoundsSize": Vector2i(160, 32)})
	collision.RequestRebuild()
	assert(collision.IsUpdating and not collision.IsChunkReady(Vector2i.ZERO))
	step(collision, 2)
	assert(collision.ShapeCount == 2 and collision.PendingChunkCount == 3)
	assert(not collision.IsChunkReady(Vector2i(4, 0)))
	# Repeated edits coalesce, including a chunk already waiting in the FIFO.
	cells.SetTerrainKind(Vector2i(129, 1), "grass")
	cells.SetTerrainKind(Vector2i(129, 2), "grass")
	assert(collision.PendingChunkCount == 3)
	step(collision, 0)
	assert(collision.ChunksRebuiltLastUpdate == 1 and collision.PendingChunkCount == 2)
	step(collision, 100)
	assert(not collision.IsUpdating and collision.PendingChunkCount == 0)
	await settle()
	assert(collision.IsChunkReady(Vector2i(4, 0)))
	assert(not hits(host, grid.CellToWorld(Vector2i(129, 1)), 4))
	assert(hits(host, grid.CellToWorld(Vector2i(130, 1)), 4))
	# Retire old chunks within the same budget when bounds shrink.
	collision.BoundsSize = Vector2i(32, 32)
	collision.RequestRebuild()
	step(collision, 1)
	assert(collision.PendingChunkCount == 4)
	while collision.IsUpdating: step(collision, 1)
	await settle()
	assert(collision.ShapeCount == 1 and not collision.IsChunkReady(Vector2i(4, 0)))
	assert(not hits(host, grid.CellToWorld(Vector2i(130, 1)), 4))
	# Synchronous initialization cancels a previously queued replacement.
	collision.BoundsSize = Vector2i(160, 32)
	collision.RequestRebuild()
	step(collision, 1)
	collision.Rebuild()
	assert(not collision.IsUpdating and collision.PendingChunkCount == 0)
	await settle()
	assert(collision.IsChunkReady(Vector2i(4, 0)))
	# Missing sources retire geometry, without publishing ready chunks.
	collision.CellDataPath = NodePath("../Missing")
	collision.RequestRebuild()
	while collision.IsUpdating: step(collision, 2)
	assert(collision.ShapeCount == 0 and not collision.IsChunkReady(Vector2i.ZERO))
	collision.CellDataPath = NodePath("../Cells")
	collision.RequestRebuild()
	# Exercise automatic frame scheduling as well as deterministic budget steps.
	collision.ChunksPerFrame = 1
	while collision.IsUpdating:
		await process_frame
		assert(collision.ChunksRebuiltLastUpdate <= 1)
	await settle()
	assert(collision.IsChunkReady(Vector2i.ZERO))
	var early: Node = load(BASE + "terrain/TerrainCollisionComponent.cs").new()
	early.RefreshOnReady = false
	early.GridPath = NodePath("../Grid")
	early.CellDataPath = NodePath("../Cells")
	early.BoundsSize = Vector2i(32, 32)
	early.RequestRebuild()
	host.add_child(early)
	assert(early.is_processing(), "Pre-tree request never started")
	while early.IsUpdating: await process_frame
	await settle()
	assert(early.IsChunkReady(Vector2i.ZERO))
	early.free()
	host.remove_child(collision)
	assert(not collision.IsChunkReady(Vector2i.ZERO))
	host.add_child(collision)
	assert(collision.IsUpdating and not collision.IsChunkReady(Vector2i.ZERO))
	while collision.IsUpdating: await process_frame
	await settle()
	assert(collision.IsChunkReady(Vector2i.ZERO))
	host.free()
	print("[terrain-collision-budget] budget, coalescing, shrinking, cancellation, sources and reattachment OK")
	quit()
