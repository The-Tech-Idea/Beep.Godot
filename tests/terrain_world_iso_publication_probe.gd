extends "res://tests/terrain_live_cells_probe.gd"

var finished: Array = []
var built := 0
var stage := false
var cancel_renderer := false
var remove_renderer := false

func run() -> void:
	create_timer(40).timeout.connect(func(): quit(1))
	var host := Node2D.new()
	root.add_child(host)
	var cells = CELLS.new()
	cells.name = "Cells"
	host.add_child(cells)
	var generator = GENERATOR.new()
	generator.name = "Generator"
	generator.CellDataPath = NodePath("../Cells")
	generator.TopologySamplesPerCell = 2
	host.add_child(generator)
	var renderer = RENDERER.new()
	renderer.name = "Renderer"
	renderer.RefreshOnReady = false
	renderer.Tiles = make_tiles()
	renderer.UseTerrainConnections = false
	renderer.TerrainBindings = PackedStringArray(["grass,forest,desert,sand,dry_grass,jungle,snow,ice,tundra,rock,gravel,mud,swamp,water,deep_water,shallow_water,sea,lake,river,lava=0"])
	renderer.CellsPerFrame = 64
	renderer.hide()
	host.add_child(renderer)
	var grid = load("res://addons/beep_game_builder_cs/ecs/grid/GridProjectionComponent.cs").new()
	grid.name = "Grid"
	grid.DrawGrid = false
	grid.TrackMouseCell = false
	host.add_child(grid)
	var collision = load("res://addons/beep_game_builder_cs/ecs/terrain/TerrainCollisionComponent.cs").new()
	collision.name = "Collision"
	collision.RefreshOnReady = false
	collision.ChunksPerFrame = 1
	host.add_child(collision)
	var world = load("res://addons/beep_game_builder_cs/ecs/terrain/TerrainWorldComponent.cs").new()
	world.GeneratorPath = NodePath("../Generator")
	world.CellDataPath = NodePath("../Cells")
	world.IsometricAutotileRendererPath = NodePath("../Renderer")
	world.GridPath = NodePath("../Grid")
	world.CollisionPath = NodePath("../Collision")
	world.Projection = 3
	world.BuildOnReady = false
	world.ParticipatesInSave = false
	world.UseCustomBounds = true
	world.CustomBounds = Vector2i(32, 24)
	host.add_child(world)
	world.WorldBuilt.connect(func(_size):
		check(not world.IsGenerating and not renderer.IsRebuilding, "World became ready before isometric publication")
		check(collision.IsReady, "World became ready before native collision")
		built += 1)
	world.GenerationFinished.connect(func(success, message): finished.append([success, message]))
	world.GenerationProgress.connect(func(name, _fraction):
		if name == "Preparing isometric terrain":
			stage = true
			check(not world.CanCancelGeneration and renderer.IsRebuilding, "Isometric preparation did not retain committed loading state")
			if cancel_renderer: renderer.CancelRebuild()
			if remove_renderer: host.remove_child(renderer))
	check(world.BeginNewWorld(), "Could not start isometric generation")
	var frames := 0
	while world.IsGenerating:
		await process_frame
		if stage and world.IsGenerating: frames += 1
	check(stage and frames > 10 and built == 1 and finished[-1][0], "World did not wait for successful isometric preparation: " + str(finished))
	var revision: int = renderer.PublicationRevision
	for i in 3: await process_frame
	check(renderer.PublicationRevision == revision, "Showing prepared terrain triggered another synchronous rebuild")
	cancel_renderer = true
	check(world.BeginNewWorld(), "Could not start cancellation case")
	while world.IsGenerating: await process_frame
	check(built == 1 and not finished[-1][0], "Cancelled renderer reported a ready world")
	cancel_renderer = false
	check(world.BeginNewWorld(), "Could not restart after renderer cancellation")
	while world.IsGenerating: await process_frame
	check(built == 2 and finished[-1][0], "Restart after renderer cancellation failed")
	remove_renderer = true
	check(world.BeginNewWorld(), "Could not start renderer removal case")
	while world.IsGenerating: await process_frame
	check(built == 2 and not finished[-1][0] and not renderer.IsRebuilding, "Removed renderer left generation active or reported success")
	renderer.free()
	host.free()
	await process_frame
	print("[world-iso-publication] OK" if failures.is_empty() else "[world-iso-publication] FAILED")
	quit(0 if failures.is_empty() else 1)
