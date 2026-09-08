extends "res://tests/terrain_collision_probe.gd"

var completed: Array = []
var built := 0
var publication_seen := false
var remove_collision := false
var change_source := false

func wait_for_world(world: Node) -> void:
	while world.IsGenerating: await process_frame

func run() -> void:
	create_timer(45).timeout.connect(func(): quit(1))
	var host := Node2D.new()
	root.add_child(host)
	var cells := make("grid/GridCellDataComponent", host, "Cells", {})
	var generator := make("terrain/TerrainGeneratorComponent", host, "Generator", {
		"CellDataPath": NodePath("../Cells"), "TopologySamplesPerCell": 2})
	make("grid/GridProjectionComponent", host, "Grid", {"TrackMouseCell": false, "DrawGrid": false})
	make("terrain/TerrainPaintedRendererComponent", host, "Painted", {
		"TerrainGeneratorPath": NodePath("../Generator"), "CellDataPath": NodePath("../Cells"),
		"RefreshOnReady": false, "TileSize": 16})
	var collision := make("terrain/TerrainCollisionComponent", host, "Collision", {
		"RefreshOnReady": false, "ChunksPerFrame": 1, "process_priority": 100})
	var world := make("terrain/TerrainWorldComponent", host, "World", {
		"GeneratorPath": NodePath("../Generator"), "CellDataPath": NodePath("../Cells"),
		"GridPath": NodePath("../Grid"), "PaintedRendererPath": NodePath("../Painted"),
		"CollisionPath": NodePath("../Collision"), "BuildOnReady": false,
		"ParticipatesInSave": false, "UseCustomBounds": true, "CustomBounds": Vector2i(96, 32)})
	var on_built := func(_size):
		assert(collision.IsReady, "WorldBuilt fired before physics readiness")
		assert(not world.IsGenerating, "Completion event still owns the active generation")
		built += 1
	world.WorldBuilt.connect(on_built)
	world.GenerationFinished.connect(func(success, message): completed.append([success, message]))
	var on_progress := func(stage, _fraction):
		if stage != "Preparing terrain collision": return
		publication_seen = true
		assert(world.IsGenerating and not world.CanCancelGeneration)
		assert(collision.IsUpdating and not collision.IsReady)
		assert(world.CaptureState().seed == generator.Seed, "Save recipe differs from committed cells")
		world.CancelGeneration()
		assert(world.IsGenerating, "Post-commit cancellation abandoned live collision")
		if remove_collision: host.remove_child(collision)
		if change_source: world.GeneratorPath = NodePath("../Missing")
	world.GenerationProgress.connect(on_progress)
	assert(world.BeginNewWorld() and world.CanCancelGeneration)
	var collision_frames := 0
	while world.IsGenerating:
		await process_frame
		if publication_seen and world.IsGenerating:
			collision_frames += 1
			assert(built == 0)
			assert(collision.ChunksRebuiltLastUpdate <= 1)
	assert(publication_seen and collision_frames >= 3, "Collision publication did not span frames")
	assert(built == 1 and completed[-1][0])
	assert(cells.CellCount > 0)
	change_source = true
	assert(world.BeginNewWorld())
	await wait_for_world(world)
	assert(built == 1 and not completed[-1][0] and "sources changed" in completed[-1][1])
	world.GeneratorPath = NodePath("../Generator")
	change_source = false
	# A missing collision consumer must fail, not emit a usable-world event.
	remove_collision = true
	world.Seed = 777
	assert(world.BeginNewWorld())
	await wait_for_world(world)
	assert(built == 1 and not completed[-1][0])
	assert("removed" in completed[-1][1])
	host.add_child(collision)
	remove_collision = false
	# Degenerate geometry is a terminal publication failure, not an endless wait.
	# Remove existing bodies before testing a singular transform to avoid native body inversion errors.
	collision.free()
	collision = make("terrain/TerrainCollisionComponent", host, "Collision", {
		"GridPath": NodePath("../Grid"), "CellDataPath": NodePath("../Cells"),
		"RefreshOnReady": false, "ChunksPerFrame": 1, "LandCollisionLayer": 1})
	collision.transform = Transform2D(Vector2(1, 0), Vector2(2, 0), Vector2.ZERO)
	# The earlier callbacks captured the previous node, so disconnect progress/success checks.
	world.GenerationProgress.disconnect(on_progress)
	world.WorldBuilt.disconnect(on_built)
	world.WorldBuilt.connect(func(_size): built += 1)
	assert(world.BeginNewWorld())
	await wait_for_world(world)
	assert(built == 1 and not completed[-1][0] and collision.FailedChunkCount > 0)
	collision.transform = Transform2D.IDENTITY
	assert(world.BeginNewWorld())
	await wait_for_world(world)
	assert(built == 2 and completed[-1][0] and collision.IsReady)
	host.free()
	print("[terrain-world-collision-publication] frame budget, ready events, commit boundary, removal, failure and recovery OK")
	quit()
