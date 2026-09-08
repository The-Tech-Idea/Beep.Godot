extends SceneTree

var failures: Array[String] = []
var built := 0
var finished: Array = []

func _initialize() -> void: run.call_deferred()

func check(value: bool, message: String) -> void:
	if not value:
		failures.append(message)
		push_error(message)

func settle(world: Node) -> void:
	var deadline := Time.get_ticks_msec() + 30000
	while world.IsGenerating and Time.get_ticks_msec() < deadline: await process_frame
	check(not world.IsGenerating, "World generation did not finish")

func run() -> void:
	var host := Node.new()
	var cells: Node = load("res://addons/beep_game_builder_cs/ecs/grid/GridCellDataComponent.cs").new()
	cells.name = "Cells"
	host.add_child(cells)
	var generator: Node = load("res://addons/beep_game_builder_cs/ecs/terrain/TerrainGeneratorComponent.cs").new()
	generator.name = "Generator"
	generator.CellDataPath = NodePath("../Cells")
	generator.TopologySamplesPerCell = 4
	host.add_child(generator)
	var world: Node = load("res://addons/beep_game_builder_cs/ecs/terrain/TerrainWorldComponent.cs").new()
	world.name = "World"
	world.GeneratorPath = NodePath("../Generator")
	world.BuildOnReady = false
	world.ParticipatesInSave = false
	world.UseCustomBounds = true
	world.CustomBounds = Vector2i(32, 24)
	host.add_child(world)
	root.add_child(host)
	world.WorldBuilt.connect(func(_size): built += 1)
	world.GenerationFinished.connect(func(success, message): finished.append([success, message]))
	var content_revision: int = cells.GetChunkRevision(Vector2i.ZERO)
	world.NewWorld()
	check(cells.GetChunkRevision(Vector2i.ZERO) > content_revision, "Synchronous generation reused a prior content token")
	content_revision = cells.GetChunkRevision(Vector2i.ZERO)
	var revision: int = cells.TerrainRevision
	var old_seed: int = generator.Seed
	var old_terrain: String = generator.TerrainKindAt(Vector2i(10, 10))
	world.Seed = 27182
	check(world.BeginNewWorld(), "Replacement request rejected")
	check(not world.BeginNewWorld(), "Overlapping request accepted")
	check(generator.Seed == old_seed, "Draft recipe changed live generator")
	check(generator.TerrainKindAt(Vector2i(10, 10)) == old_terrain, "Pending request replaced published field")
	check(world.CaptureState().seed == old_seed, "Pending draft was captured as the live save")
	world.CancelGeneration()
	await settle(world)
	check(cells.TerrainRevision == revision and built == 1, "Cancellation modified world")
	check(cells.GetChunkRevision(Vector2i.ZERO) == content_revision, "Cancellation changed content revision")
	check(not finished[-1][0], "Cancellation reported success")
	check(world.BeginNewWorld(), "Could not restart after cancellation")
	await settle(world)
	check(finished[-1][0] and built == 2, "Replacement was not published")
	check(generator.Seed == 27182 and world.CaptureState().seed == 27182, "Published recipe differs from field")
	check(cells.TerrainRevision == revision + 1, "Replacement did not publish one live-cell batch")
	check(cells.GetChunkRevision(Vector2i.ZERO) > content_revision, "Staged generation reused a prior content token")
	content_revision = cells.GetChunkRevision(Vector2i.ZERO)
	revision = cells.TerrainRevision
	world.Seed = 777
	check(world.BeginNewWorld(), "Stale-request test could not start")
	world.Seed = 778
	await settle(world)
	check(not finished[-1][0] and built == 2 and cells.TerrainRevision == revision, "Stale result was published")
	check(generator.Seed == 27182, "Discarded request changed live settings")
	var frequency: float = generator.Frequency
	check(world.BeginNewWorld(), "Generator-edit test could not start")
	generator.Frequency = frequency * 0.5
	await settle(world)
	check(not finished[-1][0] and cells.TerrainRevision == revision, "Generator edits were overwritten by a stale request")
	generator.Frequency = frequency
	var cancel_before_publish := func(stage, _fraction):
		if stage == "Publishing world": world.CancelGeneration()
	world.GenerationProgress.connect(cancel_before_publish)
	check(world.BeginNewWorld(), "Pre-publication cancellation could not start")
	await settle(world)
	world.GenerationProgress.disconnect(cancel_before_publish)
	check(not finished[-1][0] and cells.TerrainRevision == revision, "Completed worker ignored cancellation before publication")
	var staged_frames: Array = []
	var cancel_during_cells := func(stage, fraction):
		if stage == "Preparing gameplay cells":
			staged_frames.append(fraction)
			check(cells.TerrainRevision == revision, "Partially staged cells leaked into live map")
			check(cells.GetChunkRevision(Vector2i.ZERO) == content_revision, "Staging dirtied content before commit")
			check(generator.Seed == 27182, "Staging changed live generator configuration")
			world.CancelGeneration()
	world.GenerationProgress.connect(cancel_during_cells)
	check(world.BeginNewWorld(), "Cell-staging cancellation could not start")
	await settle(world)
	world.GenerationProgress.disconnect(cancel_during_cells)
	check(not staged_frames.is_empty(), "Replacement did not yield while staging cells")
	check(not finished[-1][0] and cells.TerrainRevision == revision, "Cancelled staged cells were committed")
	check(world.BeginNewWorld(), "Restart after cell-staging cancellation failed")
	await settle(world)
	check(finished[-1][0] and cells.TerrainRevision == revision + 1, "Staged restart did not commit exactly once")
	# Completion listeners can start another world without old cleanup stopping it.
	var chain := {"built": 0, "finished": 0}
	var restart_on_built := func(_size):
		check(not world.IsGenerating, "WorldBuilt observed the completed job as busy")
		check(not world.CanCancelGeneration, "Completed world remained cancellable")
		chain.built += 1
		if chain.built == 1:
			world.Seed = 81002
			check(world.BeginNewWorld(), "WorldBuilt could not start the next generation")
	var count_finished := func(success, _message):
		check(success, "Chained generation failed")
		chain.finished += 1
	world.WorldBuilt.connect(restart_on_built)
	world.GenerationFinished.connect(count_finished)
	world.Seed = 81001
	check(world.BeginNewWorld(), "Chained generation could not start")
	await settle(world)
	world.WorldBuilt.disconnect(restart_on_built)
	world.GenerationFinished.disconnect(count_finished)
	check(chain.built == 2 and chain.finished == 2, "Chained generation lost or duplicated completion")
	check(generator.Seed == 81002, "Old completion overwrote the replacement generation")
	var cancellation_chain := {"finished": 0}
	var restart_on_cancel := func(success, _message):
		check(not world.IsGenerating, "GenerationFinished observed an active old job")
		cancellation_chain.finished += 1
		if cancellation_chain.finished == 1:
			check(not success, "Cancelled generation unexpectedly succeeded")
			world.Seed = 81003
			check(world.BeginNewWorld(), "Cancellation listener could not restart generation")
		else:
			check(success, "Restart from cancellation failed")
	world.GenerationFinished.connect(restart_on_cancel)
	check(world.BeginNewWorld(), "Cancellation chain could not start")
	world.CancelGeneration()
	await settle(world)
	world.GenerationFinished.disconnect(restart_on_cancel)
	check(cancellation_chain.finished == 2 and generator.Seed == 81003, "Cancellation cleanup retired the next job")
	check(world.BeginNewWorld(), "Scene-exit test could not start")
	host.free()
	# Allow cancelled worker cleanup to run after every scene node is freed.
	await create_timer(1.0).timeout
	print("[terrain-world-generation] OK" if failures.is_empty() else "[terrain-world-generation] FAILED")
	quit(0 if failures.is_empty() else 1)
