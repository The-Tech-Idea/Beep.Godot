extends "res://tests/terrain_painted_archive_probe.gd"

var preparation_frames := 0
var previous_loaded := 0
var old_id: Texture2D
var mutate := false
var exit_world := false
var finished: Array = []
var coast_frames := 0
var change_coast := false

func wait_world(world: Node) -> void:
	while is_instance_valid(world) and world.IsGenerating: await process_frame

func run() -> void:
	create_timer(45).timeout.connect(func(): quit(1))
	var host := Node2D.new()
	root.add_child(host)
	var cells: Node = load(ECS + "grid/GridCellDataComponent.cs").new()
	cells.name = "Cells"
	host.add_child(cells)
	var generator: Node = load(ECS + "terrain/TerrainGeneratorComponent.cs").new()
	generator.name = "Generator"
	generator.CellDataPath = NodePath("../Cells")
	generator.TopologySamplesPerCell = 2
	generator.GenerateOnReady = false
	host.add_child(generator)
	var painter: Node = load(ECS + "terrain/TerrainPaintedRendererComponent.cs").new()
	painter.name = "Painter"
	painter.CellDataPath = NodePath("../Cells")
	painter.TerrainGeneratorPath = NodePath("../Generator")
	painter.RefreshOnReady = false
	host.add_child(painter)
	var world: Node = load(ECS + "terrain/TerrainWorldComponent.cs").new()
	world.GeneratorPath = NodePath("../Generator")
	world.CellDataPath = NodePath("../Cells")
	world.PaintedRendererPath = NodePath("../Painter")
	world.BuildOnReady = false
	world.ParticipatesInSave = false
	world.UseCustomBounds = true
	world.CustomBounds = Vector2i(64, 33)
	world.PublicationCellsPerFrame = 127
	host.add_child(world)
	world.NewWorld()
	var baseline := maps(painter)
	old_id = painter.get_node("SplatSurface").material.get_shader_parameter("id_map")
	world.GenerationFinished.connect(func(success, message): finished.append([success, message]))
	world.GenerationProgress.connect(func(stage, _fraction):
		if stage == "Computing painted coast":
			coast_frames += 1
			assert(painter.IsPreparingSnapshot)
			assert(painter.get_node("SplatSurface").material.get_shader_parameter("id_map") == old_id)
			if change_coast:
				change_coast = false
				painter.CoastDetail = 3
			if exit_world: world.queue_free()
			return
		if stage != "Preparing painted terrain": return
		var loaded: int = painter.PreparedSnapshotCells
		if loaded == 0: previous_loaded = 0
		assert(loaded - previous_loaded <= 127, "Snapshot exceeded its cell budget")
		previous_loaded = loaded
		preparation_frames += 1
		assert(painter.IsPreparingSnapshot and not world.CanCancelGeneration)
		assert(painter.get_node("SplatSurface").material.get_shader_parameter("id_map") == old_id,
			"Partial snapshot replaced the visible terrain")
		if mutate and loaded > 0:
			mutate = false
			cells.SetTerrainKind(Vector2i(2, 2), "lava")
		)
	assert(world.BeginNewWorld())
	await wait_world(world)
	assert(finished[-1][0] and preparation_frames >= 16 and coast_frames > 0 and not painter.IsPreparingSnapshot)
	var prepared := maps(painter)
	for slot in MAPS: assert(prepared[slot] == baseline[slot], "Staging changed " + slot)
	old_id = painter.get_node("SplatSurface").material.get_shader_parameter("id_map")
	mutate = true
	assert(world.BeginNewWorld())
	await wait_world(world)
	assert(not finished[-1][0] and "changed" in finished[-1][1])
	assert(not painter.IsPreparingSnapshot)
	assert(painter.get_node("SplatSurface").material.get_shader_parameter("id_map") == old_id)
	assert(world.BeginNewWorld())
	await wait_world(world)
	assert(finished[-1][0])
	var recovered := maps(painter)
	for slot in MAPS: assert(recovered[slot] == baseline[slot], "Recovery changed " + slot)
	old_id = painter.get_node("SplatSurface").material.get_shader_parameter("id_map")
	change_coast = true
	assert(world.BeginNewWorld())
	await wait_world(world)
	assert(not finished[-1][0] and "settings changed" in finished[-1][1])
	assert(not painter.IsPreparingSnapshot)
	painter.CoastDetail = 12
	exit_world = true
	assert(world.BeginNewWorld())
	await wait_world(world)
	assert(not painter.IsPreparingSnapshot, "Removed world retained snapshot work")
	host.free()
	await create_timer(1).timeout
	print("[terrain-painted-publication] budget, worker coast, exact five-map parity, retention, stale settings and scene exit OK")
	quit()
