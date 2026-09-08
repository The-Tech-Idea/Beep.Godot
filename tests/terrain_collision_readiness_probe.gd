extends "res://tests/terrain_collision_probe.gd"

func _initialize() -> void:
	create_timer(30).timeout.connect(func():
		push_error("Collision readiness watchdog expired")
		quit(1))
	run.call_deferred()

func run() -> void:
	var host := Node2D.new()
	root.add_child(host)
	var grid := make("grid/GridProjectionComponent", host, "Grid", {"DrawGrid": false, "TrackMouseCell": false})
	var cells := make("grid/GridCellDataComponent", host, "Cells", {"DefaultTerrainKind": "water"})
	var collision := make("terrain/TerrainCollisionComponent", host, "Collision", {
		"GridPath": NodePath("../Grid"), "CellDataPath": NodePath("../Cells"),
		"RefreshOnReady": false, "BoundsSize": Vector2i(2, 1)})
	collision.Rebuild()
	assert(not collision.IsChunkReady(Vector2i.ZERO), "Physics-frame handoff was skipped")
	await settle()
	assert(collision.IsChunkReady(Vector2i.ZERO) and collision.FailedChunkCount == 0)
	grid.TileMapLayerPath = NodePath("../Missing")
	collision.Rebuild()
	await settle()
	assert(not collision.IsChunkReady(Vector2i.ZERO) and collision.FailedChunkCount == 1, "Missing required polygons were published as ready")
	assert(collision.ShapeCount == 0)
	var layer := TileMapLayer.new()
	layer.name = "Native"
	layer.tile_set = TileSet.new()
	layer.tile_set.tile_size = Vector2i(32, 32)
	host.add_child(layer)
	grid.TileMapLayerPath = NodePath("../Native")
	layer.transform = Transform2D(Vector2(1, 0), Vector2(2, 0), Vector2.ZERO)
	collision.Rebuild()
	await settle()
	assert(not collision.IsChunkReady(Vector2i.ZERO) and collision.ShapeCount == 0, "Collinear polygons were published as ready")
	layer.transform = Transform2D.IDENTITY
	# Configure the singular parent before native bodies exist. Making an existing
	# PhysicsBody2D singular itself triggers a separate Godot transform diagnostic.
	var singular := make("terrain/TerrainCollisionComponent", host, "Singular", {
		"GridPath": NodePath("../Grid"), "CellDataPath": NodePath("../Cells"),
		"RefreshOnReady": false, "BoundsSize": Vector2i(2, 1),
		"transform": Transform2D(Vector2(1, 0), Vector2(2, 0), Vector2.ZERO)})
	singular.Rebuild()
	await settle()
	assert(not singular.IsChunkReady(Vector2i.ZERO) and singular.ShapeCount == 0, "Singular collision transform was published as ready")
	singular.transform = Transform2D.IDENTITY
	singular.Rebuild()
	await settle()
	assert(singular.IsChunkReady(Vector2i.ZERO), "Corrected singular transform failed to recover")
	singular.free()
	collision.transform = Transform2D(Vector2(-1, 0), Vector2(0.2, 1), Vector2(40, 30))
	collision.Rebuild()
	await settle()
	assert(collision.IsChunkReady(Vector2i.ZERO) and collision.FailedChunkCount == 0, "Valid mirrored transform failed to recover")
	assert(hits(host, grid.CellToWorld(Vector2i.ZERO), 4), "Recovered native polygon missed water")
	var elevated := make("terrain/TerrainIsometricRendererComponent", host, "Elevated", {
		"CellDataPath": NodePath("../Cells"), "RefreshOnReady": false,
		"BoundsSize": Vector2i.ONE, "CellSize": Vector2i(111, 64),
		"BlockSheetPath": "res://addons/beep_game_builder_cs/textures/iso/kenney_voxel_blocks.png",
		"SheetColumns": 8, "SheetRows": 7,
		"WaterShaderPath": "res://addons/beep_game_builder_cs/shaders/iso_water.gdshader"})
	elevated.Rebuild()
	grid.ElevatedTerrainPath = NodePath("../Elevated")
	collision.Rebuild()
	await settle()
	assert(not collision.IsChunkReady(Vector2i.ZERO) and collision.FailedChunkCount == 1, "Partial elevated chunk was published as ready")
	assert(collision.ShapeCount == 0 and not hits(host, grid.CellToWorld(Vector2i.ZERO), 4), "Failed chunk retained its partially built shapes")
	collision.BoundsSize = Vector2i.ONE
	collision.Rebuild()
	await settle()
	assert(collision.IsChunkReady(Vector2i.ZERO) and collision.FailedChunkCount == 0 and collision.ShapeCount == 1, "Corrected surface bounds did not recover")
	host.free()
	print("[terrain-collision-readiness] missing, degenerate, singular and partial geometry rejected; corrected geometry recovers")
	quit()
