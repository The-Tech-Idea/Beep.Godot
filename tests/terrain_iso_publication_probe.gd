extends "res://tests/terrain_live_cells_probe.gd"

func wait_build(renderer: Node, layer: TileMapLayer, previous: PackedByteArray) -> void:
	var frames := 0
	while renderer.IsRebuilding:
		await process_frame
		frames += 1
		check(renderer.CellsProcessedLastFrame <= renderer.CellsPerFrame + 63, "Native batch exceeded cell budget plus one 64-cell batch")
		if renderer.IsRebuilding:
			check(layer.tile_map_data == previous, "Partial terrain was published")
	check(frames > 1, "Budgeted rebuild did not yield")

func run() -> void:
	create_timer(30).timeout.connect(func(): quit(1))
	var host := Node2D.new()
	root.add_child(host)
	var cells = CELLS.new()
	cells.name = "Cells"
	host.add_child(cells)
	for y in 24:
		for x in 32:
			if x > 15: cells.SetTerrainKind(Vector2i(x, y), "water")
	var renderer = RENDERER.new()
	renderer.RefreshOnReady = false
	renderer.CellDataPath = NodePath("../Cells")
	renderer.BoundsSize = Vector2i(32, 24)
	renderer.Tiles = make_tiles()
	renderer.TerrainBindings = PackedStringArray(["grass=0", "water=1"])
	renderer.CellsPerFrame = 64
	host.add_child(renderer)
	for connections in [false, true]:
		renderer.UseTerrainConnections = connections
		renderer.Rebuild()
		var layer: TileMapLayer = renderer.GetTerrainLayer()
		var previous := layer.tile_map_data
		renderer.RequestRebuild()
		await wait_build(renderer, layer, previous)
		check(renderer.GetTerrainLayer() == layer, "Publication replaced the gameplay projection layer")
		check(renderer.GetPaintDiagnostics().valid, "Budgeted terrain connections lost coverage")
		check(layer.get_used_cells().size() == 32 * 24, "Publication changed finite bounds")
		for y in 24:
			for x in 32:
				check(layer.get_cell_tile_data(Vector2i(x, y)).terrain == (1 if x > 15 else 0), "Publication changed terrain assignment")
		if not connections: check(layer.tile_map_data == previous, "Assigned tile publication differs from synchronous output")
	var layer: TileMapLayer = renderer.GetTerrainLayer()
	var previous := layer.tile_map_data
	renderer.RequestRebuild()
	await process_frame
	renderer.CancelRebuild()
	check(not renderer.IsRebuilding and layer.tile_map_data == previous, "Cancellation replaced the visible map")
	check(renderer.get_node_or_null("IsoTerrainPending") == null, "Cancelled rebuild retained staging layer")
	renderer.RequestRebuild()
	await process_frame
	cells.SetTerrainKind(Vector2i(2, 2), "water")
	await wait_build(renderer, layer, previous)
	check(layer.get_cell_tile_data(Vector2i(2, 2)).terrain == 1, "Mid-build edit published a stale snapshot")
	previous = layer.tile_map_data
	renderer.RequestRebuild()
	await process_frame
	renderer.BoundsOrigin = Vector2i(-4, 5)
	await wait_build(renderer, layer, previous)
	check(layer.get_used_rect() == Rect2i(-4, 5, 32, 24), "Mid-build bounds edit was ignored")
	renderer.RequestRebuild()
	await process_frame
	host.free()
	await process_frame
	print("[iso-publication] OK" if failures.is_empty() else "[iso-publication] FAILED")
	quit(0 if failures.is_empty() else 1)
