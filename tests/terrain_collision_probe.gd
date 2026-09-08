extends SceneTree

const BASE := "res://addons/beep_game_builder_cs/ecs/"

func make(path: String, host: Node, label: String, properties: Dictionary) -> Node:
	var node: Node = load(BASE + path + ".cs").new()
	node.name = label
	for key in properties: node.set(key, properties[key])
	host.add_child(node)
	return node

func _initialize() -> void:
	call_deferred("run")

func settle() -> void:
	await process_frame
	await physics_frame
	await physics_frame

func hits(host: Node2D, position: Vector2, mask: int) -> bool:
	var query := PhysicsPointQueryParameters2D.new()
	query.position = position
	query.collision_mask = mask
	return not host.get_world_2d().direct_space_state.intersect_point(query).is_empty()

func run() -> void:
	var host := Node2D.new()
	root.add_child(host)
	var layer := TileMapLayer.new()
	layer.name = "Native"
	layer.tile_set = TileSet.new()
	layer.tile_set.tile_size = Vector2i(96, 48)
	layer.position = Vector2(500, -230)
	layer.rotation = 0.24
	layer.scale = Vector2(1.2, 0.8)
	host.add_child(layer)
	var grid := make("grid/GridProjectionComponent", host, "Grid", {
		"TileMapLayerPath": NodePath("../Native"), "TrackMouseCell": false, "DrawGrid": false})
	var cells := make("grid/GridCellDataComponent", host, "Cells", {})
	var collision := make("terrain/TerrainCollisionComponent", host, "Collision", {
		"GridPath": NodePath("../Grid"), "CellDataPath": NodePath("../Cells"),
		"RefreshOnReady": false, "BoundsOrigin": Vector2i(-5, 8), "BoundsSize": Vector2i(3, 3)})
	collision.position = Vector2(-110, 70)
	collision.rotation = -0.31
	var at := Vector2i(-4, 9)
	var mover := CharacterBody2D.new()
	mover.collision_layer = 0
	mover.collision_mask = 4
	var shape := CollisionShape2D.new()
	shape.shape = CircleShape2D.new()
	shape.shape.radius = 2
	mover.add_child(shape)
	host.add_child(mover)
	for projection in [TileSet.TILE_SHAPE_SQUARE, TileSet.TILE_SHAPE_ISOMETRIC]:
		layer.tile_set.tile_shape = projection
		grid.call("NotifyGeometryChanged")
		cells.call("SetTerrainKind", at, "water")
		collision.call("Rebuild")
		await settle()
		var center: Vector2 = grid.call("CellToWorld", at)
		assert(collision.get("ShapeCount") == 1)
		assert(hits(host, center, 4), "Native physics missed projected water")
		assert(not hits(host, center, 8), "Water used steep collision mask")
		mover.global_position = center - Vector2(300, 0)
		assert(mover.test_move(mover.global_transform, Vector2(600, 0)), "CharacterBody passed through water geometry")
		cells.call("SetTerrainKind", at, "grass")
		await settle()
		assert(collision.get("ShapeCount") == 0 and not hits(host, center, 4), "Unflooding retained native collision")
		assert(not mover.test_move(mover.global_transform, Vector2(600, 0)), "Removed water still blocks motion")
		cells.call("SetTerrainKind", at, "rock")
		await settle()
		assert(hits(host, center, 8) and not hits(host, center, 4), "Live terrain classification did not change physics mask")
		collision.set("CellDataPath", NodePath("../Missing"))
		collision.call("Rebuild")
		await settle()
		assert(collision.get("ShapeCount") == 0 and not hits(host, center, 8), "Missing source left stale collision")
		collision.set("CellDataPath", NodePath("../Cells"))
		cells.call("SetTerrainKind", at, "grass")
	for y in range(8, 11):
		for x in range(-5, -2): cells.call("SetTerrainKind", Vector2i(x, y), "water")
	cells.call("SetTerrainKind", at, "grass")
	cells.call("SetMetadata", at, "terrain_relief", 1)
	var elevated := make("terrain/TerrainIsometricRendererComponent", host, "Elevated", {
		"CellDataPath": NodePath("../Cells"), "RefreshOnReady": false,
		"BoundsOrigin": Vector2i(-5, 8), "BoundsSize": Vector2i(3, 3),
		"CellSize": Vector2i(111, 64), "LevelHeight": 120,
		"BlockSheetPath": "res://addons/beep_game_builder_cs/textures/iso/kenney_voxel_blocks.png",
		"SheetColumns": 8, "SheetRows": 7,
		"WaterShaderPath": "res://addons/beep_game_builder_cs/shaders/iso_water.gdshader"})
	elevated.position = Vector2(125, -85)
	elevated.rotation = 0.12
	elevated.call("Rebuild")
	grid.set("ElevatedTerrainPath", NodePath("../Elevated"))
	collision.set("LandCollisionLayer", 2)
	collision.call("Rebuild")
	await settle()
	var raised: Vector2 = grid.call("CellToWorld", at)
	assert(hits(host, raised, 2), "Physics missed raised land surface")
	cells.call("SetMetadata", at, "terrain_relief", 0)
	await settle()
	var flattened: Vector2 = grid.call("CellToWorld", at)
	assert(raised.distance_to(flattened) > 64, "Fixture did not move the surface")
	assert(hits(host, flattened, 2) and not hits(host, raised, 2), "Flattening left collision on the old surface")
	grid.set("ElevatedTerrainPath", NodePath("../MissingSurface"))
	grid.call("NotifyGeometryChanged")
	await settle()
	assert(collision.get("ShapeCount") == 0, "Missing projected surface retained collision")
	host.free()
	print("[terrain-collision] OK")
	quit()
