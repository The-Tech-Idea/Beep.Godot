extends "res://tests/terrain_collision_probe.gd"

func run() -> void:
	var host := Node2D.new()
	root.add_child(host)
	var grid := make("grid/GridProjectionComponent", host, "Grid", {
		"TileSize": Vector2(24, 16), "TrackMouseCell": false, "DrawGrid": false})
	grid.position = Vector2(30, -25)
	grid.rotation = 0.17
	grid.scale = Vector2(1.1, 0.9)
	var cells := make("grid/GridCellDataComponent", host, "Cells", {"DefaultTerrainKind": "water"})
	var collision := make("terrain/TerrainCollisionComponent", host, "Collision", {
		"GridPath": NodePath("../Grid"), "CellDataPath": NodePath("../Cells"),
		"RefreshOnReady": false, "BoundsOrigin": Vector2i(-512, -512), "BoundsSize": Vector2i(1024, 1024)})
	collision.position = Vector2(-17, 48)
	collision.rotation = -0.25
	var started := Time.get_ticks_msec()
	collision.Rebuild()
	var elapsed := Time.get_ticks_msec() - started
	assert(collision.ShapeCount == 1024, "Million-cell ocean was not reduced to one shape per chunk")
	await settle()
	for cell in [Vector2i(-512, -512), Vector2i(511, 511), Vector2i.ZERO, Vector2i(-1, 31)]:
		assert(hits(host, grid.CellToWorld(cell), 4), "Merged ocean missed a cell")
	assert(not hits(host, grid.CellToWorld(Vector2i(512, 0)), 4), "Merged ocean escaped bounds")
	cells.SetTerrainKind(Vector2i(-1, -1), "grass")
	cells.SetTerrainKind(Vector2i(-2, -1), "rock")
	await settle()
	assert(collision.ChunksRebuiltLastUpdate == 1, "One chunk edit rebuilt unrelated collision")
	assert(not hits(host, grid.CellToWorld(Vector2i(-1, -1)), 4), "Rectangle merging filled a dry hole")
	assert(hits(host, grid.CellToWorld(Vector2i(-2, -1)), 8), "Steep collision missing")
	assert(not hits(host, grid.CellToWorld(Vector2i(-2, -1)), 4), "Steep cell inherited water collision")
	assert(hits(host, grid.CellToWorld(Vector2i(-3, -1)), 4), "Hole removed neighboring water")
	assert(cells.SetChunkAvailable(Vector2i(1, 1), false))
	await settle()
	assert(collision.ChunksRebuiltLastUpdate == 1, "Availability change rebuilt every chunk")
	assert(not hits(host, grid.CellToWorld(Vector2i(40, 40)), 4), "Unavailable chunk retained collision")
	assert(cells.SetChunkAvailable(Vector2i(1, 1), true))
	await settle()
	assert(collision.ChunksRebuiltLastUpdate == 1 and hits(host, grid.CellToWorld(Vector2i(40, 40)), 4), "Reload did not restore collision")
	# Manual isometric grids are affine too; native offset layouts are not merged.
	grid.Projection = 1
	collision.BoundsOrigin = Vector2i(2, 3)
	collision.BoundsSize = Vector2i(8, 6)
	collision.Rebuild()
	await settle()
	assert(collision.ShapeCount == 1, "Flat isometric rectangle was not merged")
	for y in range(2, 10):
		for x in range(1, 11):
			assert(hits(host, grid.CellToWorld(Vector2i(x, y)), 4) == Rect2i(2, 3, 8, 6).has_point(Vector2i(x, y)), "Isometric merged footprint differs")
	collision.BoundsOrigin = Vector2i(-9, -7)
	collision.BoundsSize = Vector2i(17, 13)
	for y in range(-7, 6):
		for x in range(-9, 8):
			var kind_index := posmod(x * 7 + y * 11, 5)
			cells.SetTerrainKind(Vector2i(x, y), "grass" if kind_index == 0 else "rock" if kind_index == 1 else "water")
	for projection in [0, 1]:
		grid.Projection = projection
		collision.Rebuild()
		await settle()
		for y in range(-7, 6):
			for x in range(-9, 8):
				var kind_index := posmod(x * 7 + y * 11, 5)
				var point: Vector2 = grid.CellToWorld(Vector2i(x, y))
				assert(hits(host, point, 4) == (kind_index >= 2), "Mixed-map water coverage differs")
				assert(hits(host, point, 8) == (kind_index == 1), "Mixed-map steep coverage differs")
	host.free()
	print("[terrain-collision-chunks] 1024x1024 ocean: 1024 shapes, %d ms; holes, masks, local edits, availability and isometric coverage OK" % elapsed)
	quit()
