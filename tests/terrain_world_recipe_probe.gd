extends SceneTree

# The recipe is the save; the cells are the map.
#
# TerrainWorldComponent persists its axes and seed and regenerates the world
# from them on load, writing NOTHING into the grid's cells. The cells are the
# live map - filled once by the generator, edited by the player, saved by
# GridWorldStateComponent - and a regenerated world must not paint over them.
# The subsurface store's remaining amounts, saved separately, must line up
# with the regenerated deposits again. Before this the seed was never saved
# at all: a reload drew the scene's authored world over cells restored from a
# different one, and the store's drawdown pointed at deposits that had moved.

const GENERATOR := preload("res://addons/beep_game_builder_cs/ecs/terrain/TerrainGeneratorComponent.cs")
const CELLS := preload("res://addons/beep_game_builder_cs/ecs/grid/GridCellDataComponent.cs")
const DATA_LAYERS := preload("res://addons/beep_game_builder_cs/ecs/terrain/TerrainDataLayersComponent.cs")
const WORLD := preload("res://addons/beep_game_builder_cs/ecs/terrain/TerrainWorldComponent.cs")
const WORLD_STATE := preload("res://addons/beep_game_builder_cs/ecs/grid/GridWorldStateComponent.cs")
const STORE := preload("res://addons/beep_game_builder_cs/ecs/grid/GridSubsurfaceStoreComponent.cs")

const SEED_SAVED := 424242
const SEED_SCENE := 777
const MAP_SIZE_TINY := 0          # TerrainMapSize.Tiny = 32x32
const RESOURCES_OIL_AND_GAS := 1  # ResourceSet.OilAndGas: underground deposits exist
const RESOURCE_LEVEL_ABUNDANT := 2
const SIZE := Vector2i(32, 32)
const SAVED_START_AREA_RADIUS := 6
const SAVED_START_DISTANCE_SCALING := 1.5

var failures: Array[String] = []

func start_area_cells(layers: Node) -> int:
	var reserved := 0
	for y in SIZE.y:
		for x in SIZE.x:
			if int(layers.call("StartAreaAt", Vector2i(x, y))) > 0:
				reserved += 1
	return reserved

# Every cell's distance to the nearest start as the layers publish it; -1 throughout when none was measured.
func start_distances(layers: Node) -> Array[int]:
	var distances: Array[int] = []
	for y in SIZE.y:
		for x in SIZE.x:
			distances.append(int(layers.call("StartDistanceAt", Vector2i(x, y))))
	return distances

func check(ok: bool, message: String) -> void:
	if ok:
		print("  ok    %s" % message)
	else:
		print("  FAIL  %s" % message)
		failures.append(message)

func _initialize() -> void:
	call_deferred("_run")

# The world's collaborators, wired the way terrain_generator_lab.tscn wires
# them: one generator writing one cell store, one set of data layers reading
# the generator, one store reading the layers.
func make_world(seed_value: int, build_on_ready: bool) -> Node:
	var world: Node = WORLD.new()
	world.name = "World%d" % seed_value
	world.set("GeneratorPath", NodePath("../TerrainGenerator"))
	world.set("DataLayersPath", NodePath("../Layers"))
	world.set("MapSize", MAP_SIZE_TINY)
	world.set("Resources", RESOURCES_OIL_AND_GAS)
	world.set("ResourceLevel", RESOURCE_LEVEL_ABUNDANT)
	world.set("Seed", seed_value)
	world.set("BuildOnReady", build_on_ready)
	root.add_child(world)
	return world

func _run() -> void:
	var cells: Node = CELLS.new()
	cells.name = "Cells"
	root.add_child(cells)

	var generator: Node = GENERATOR.new()
	generator.name = "TerrainGenerator"
	generator.set("CellDataPath", NodePath("../Cells"))
	generator.set("GenerateOnReady", false)
	generator.set("ClearExistingCells", true)
	root.add_child(generator)

	var layers: Node = DATA_LAYERS.new()
	layers.MaterializeTileLayers = true
	layers.name = "Layers"
	layers.set("TerrainGeneratorPath", NodePath("../TerrainGenerator"))
	layers.set("BoundsSize", SIZE)
	layers.set("RefreshOnReady", false)
	root.add_child(layers)

	var grid_state: Node = WORLD_STATE.new()
	grid_state.name = "GridState"
	grid_state.set("CellDataPath", NodePath("../Cells"))
	grid_state.set("CapturePlacementOccupancy", false)
	grid_state.set("CaptureNavigationBlocks", false)
	grid_state.set("CaptureRoads", false)
	grid_state.set("CaptureGridObjects", false)
	grid_state.set("CaptureSelection", false)
	grid_state.set("CaptureJobs", false)
	root.add_child(grid_state)

	var store: Node = STORE.new()
	store.name = "Subsurface"
	store.set("DataLayersPath", NodePath("../Layers"))
	root.add_child(store)

	var world := make_world(SEED_SAVED, false)
	world.set("StartAreaRadius", SAVED_START_AREA_RADIUS)
	world.set("StartDistanceScaling", SAVED_START_DISTANCE_SCALING)
	await process_frame

	# ── A new world: the generator fills the cells once, and the layers agree ──
	world.call("NewWorld")
	check(Vector2i(world.get("BuiltSize")) == SIZE, "a Tiny world is 32x32 (%s)" % str(world.get("BuiltSize")))
	var saved_area_cells := start_area_cells(layers)
	check(saved_area_cells > 0, "the world's StartAreaRadius reached the generator: %d cells are reserved" % saved_area_cells)
	var saved_distances := start_distances(layers)
	check(saved_distances.min() == 0 and saved_distances.max() > 0,
		"the world's StartDistanceScaling reached the generator: distances run %d..%d" % [saved_distances.min(), saved_distances.max()])
	check(str(world.call("StatusLine")).contains("areas "), "the status line reports start-area usability: %s" % world.call("StatusLine"))
	check(int(cells.get("CellCount")) == SIZE.x * SIZE.y, "NewWorld filled every cell (%d)" % int(cells.get("CellCount")))
	var original_data_tiles: TileSet = layers.get_node("TerrainData").tile_set
	world.call("Redraw")
	check(layers.get_node("TerrainData").tile_set == original_data_tiles, "Redraw reuses unchanged generated data layers")

	var fresh_mismatch := 0
	for y in SIZE.y:
		for x in SIZE.x:
			var c := Vector2i(x, y)
			if str(cells.call("GetTerrainKind", c)) != str(layers.call("GeneratedTerrainAt", c)):
				fresh_mismatch += 1
	check(fresh_mismatch == 0, "on a new world the live cells and the generated layers say the same kind everywhere (%d differ)" % fresh_mismatch)

	# A deposit to work, so the store has a drawdown to carry.
	var deposit := Vector2i(-1, -1)
	for y in SIZE.y:
		for x in SIZE.x:
			if str(layers.call("UndergroundResourceAt", Vector2i(x, y))) != "":
				deposit = Vector2i(x, y)
				break
		if deposit.x >= 0:
			break
	check(deposit.x >= 0, "the saved world holds an underground deposit to draw from")
	if deposit.x < 0:
		print("\nRESULT: %d FAILED" % failures.size())
		quit(1)
		return
	var deposit_id: String = str(store.call("ResourceIdAt", deposit))
	var remaining_full: int = int(store.call("RemainingAt", deposit))
	var drawn: int = int(store.call("Draw", deposit, 1))
	check(drawn == 1 and int(store.call("RemainingAt", deposit)) == remaining_full - 1,
		"one unit of %s was drawn (%d of %d left)" % [deposit_id, remaining_full - 1, remaining_full])

	# The player's edit: a kind the generator never produces, on a cell that is
	# not the deposit, so the two facts stay distinguishable.
	var edit := Vector2i(SIZE.x / 2, SIZE.y / 2)
	if edit == deposit:
		edit += Vector2i(1, 0)
	cells.call("SetTerrainKind", edit, "probe_mark")
	check(str(cells.call("GetTerrainKind", edit)) == "probe_mark", "the player edited a cell's live kind")
	check(str(layers.call("GeneratedTerrainAt", edit)) != "probe_mark", "the generated layers still say what the recipe made there - two facts, two owners")

	# A sample of the generated world to recognise it by after the round trip.
	var samples: Array[Vector2i] = []
	var expected_generated: Array[String] = []
	for y in range(0, SIZE.y, 5):
		for x in range(0, SIZE.x, 5):
			samples.append(Vector2i(x, y))
			expected_generated.append(str(layers.call("GeneratedTerrainAt", Vector2i(x, y))))

	# ── Save: three saveables, three disjoint stores ─────────────────────────
	var recipe: Dictionary = world.call("CaptureState")
	var grid_snapshot: Dictionary = grid_state.call("CaptureState")
	var store_snapshot: Dictionary = store.call("CaptureState")
	check(int(recipe.get("seed", -1)) == SEED_SAVED, "the recipe carries the seed (%d)" % int(recipe.get("seed", -1)))
	check(recipe.has("map_type") and recipe.has("map_size") and recipe.has("sea_level") and recipe.has("resources"),
		"the recipe carries the axes, not the layers")
	check(not recipe.has("cells") and not recipe.has("cell_data"), "the recipe carries no cell payload - the cells are GridWorldState's")
	check(int(recipe.get("start_area_radius", -1)) == SAVED_START_AREA_RADIUS and int(recipe.get("version", -1)) == 5,
		"the recipe carries start_area_radius %d at version %d" % [int(recipe.get("start_area_radius", -1)), int(recipe.get("version", -1))])
	check(is_equal_approx(float(recipe.get("start_distance_scaling", -1.0)), SAVED_START_DISTANCE_SCALING),
		"the recipe carries start_distance_scaling %s" % str(recipe.get("start_distance_scaling", "(absent)")))

	# ── A different scene: the authored seed changed, a fresh world was built ─
	world.set("Seed", SEED_SCENE)
	world.set("StartAreaRadius", 0)
	world.set("StartDistanceScaling", 0.0)
	world.call("NewWorld")
	check(start_area_cells(layers) == 0 and not str(world.call("StatusLine")).contains("areas "),
		"a world with no start-area radius reserves nothing and reports no areas")
	check(start_distances(layers).max() == -1, "a world that scales nothing by distance measures no distance")
	var diverged := 0
	check(layers.get_node("TerrainData").tile_set != original_data_tiles, "NewWorld rebuilds recipe data rather than reusing stale tiles")
	for i in samples.size():
		if str(layers.call("GeneratedTerrainAt", samples[i])) != expected_generated[i]:
			diverged += 1
	check(diverged > 0, "the scene's authored seed makes a recognisably different world (%d of %d samples differ)" % [diverged, samples.size()])
	check(str(cells.call("GetTerrainKind", edit)) != "probe_mark", "and the fresh world overwrote the edit, as a new world should")

	# ── Load, in the least convenient order: the world LAST ──────────────────
	# If RestoreWorld wrote cells, restoring it after the grid would wipe the
	# restored edit. It must not.
	store.call("RestoreState", store_snapshot)
	grid_state.call("RestoreState", grid_snapshot)
	world.call("RestoreState", recipe)

	check(int(world.get("Seed")) == SEED_SAVED, "the recipe restored the saved seed over the scene's authored one")
	check(int(world.get("StartAreaRadius")) == SAVED_START_AREA_RADIUS and start_area_cells(layers) == saved_area_cells,
		"the recipe restored the start-area radius and the same %d reserved cells (%d)" % [saved_area_cells, start_area_cells(layers)])
	check(is_equal_approx(float(world.get("StartDistanceScaling")), SAVED_START_DISTANCE_SCALING) and start_distances(layers) == saved_distances,
		"the recipe restored the start-distance scaling and the same distances")
	var regenerated_wrong := 0
	for i in samples.size():
		if str(layers.call("GeneratedTerrainAt", samples[i])) != expected_generated[i]:
			regenerated_wrong += 1
	check(regenerated_wrong == 0, "the layers publish the SAVED world again, not the scene's (%d of %d samples wrong)" % [regenerated_wrong, samples.size()])
	check(str(cells.call("GetTerrainKind", edit)) == "probe_mark", "the edited cell kept its edit - a regenerated world does not paint over the live map")

	var restored_mismatch := 0
	for y in SIZE.y:
		for x in SIZE.x:
			var c := Vector2i(x, y)
			if c == edit:
				continue
			if str(cells.call("GetTerrainKind", c)) != str(layers.call("GeneratedTerrainAt", c)):
				restored_mismatch += 1
	check(restored_mismatch == 0, "every other restored cell lines up with the regenerated world - same world, two owners (%d differ)" % restored_mismatch)

	check(str(store.call("ResourceIdAt", deposit)) == deposit_id, "the deposit is back where the saved world put it (%s)" % deposit_id)
	check(int(store.call("RemainingAt", deposit)) == remaining_full - 1,
		"the store's drawdown reads against the regenerated deposit (%d left)" % int(store.call("RemainingAt", deposit)))

	# ── BuildOnReady yields to a restore ────────────────────────────────────
	# A scene that builds on ready and is then loaded into: the deferred build
	# must stand down, or a fresh world lands on top of the restored cells.
	world.free()
	await process_frame
	var ready_world := make_world(SEED_SCENE, true)
	ready_world.call("RestoreState", recipe)   # same frame, before the deferred build runs
	await process_frame
	await process_frame
	check(int(ready_world.get("Seed")) == SEED_SAVED, "a BuildOnReady world that was restored keeps the saved seed")
	check(str(cells.call("GetTerrainKind", edit)) == "probe_mark", "and its deferred build stood down - the restored edit survived the ready frame")
	ready_world.call("NewWorld")
	check(str(cells.call("GetTerrainKind", edit)) != "probe_mark", "an explicit NewWorld after a restore still builds a new world - the yield is for the deferred build only")

	print("\nRESULT: ", "all checks passed" if failures.is_empty() else "%d FAILED" % failures.size())
	quit(1 if failures.size() > 0 else 0)
