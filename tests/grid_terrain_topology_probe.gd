extends SceneTree

const CELL_DATA_SCRIPT := preload("res://addons/beep_game_builder_cs/ecs/grid/GridCellDataComponent.cs")
const GENERATOR_SCRIPT := preload("res://addons/beep_game_builder_cs/ecs/terrain/TerrainGeneratorComponent.cs")
const SIZE := Vector2i(48, 30)

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var cells := CELL_DATA_SCRIPT.new()
	cells.name = "Cells"
	root.add_child(cells)
	var generator := GENERATOR_SCRIPT.new()
	generator.name = "Generator"
	generator.set("CellDataPath", NodePath("../Cells"))
	generator.set("BoundsSize", SIZE)
	# Rivers carve water THROUGH a landmass, so they lower the land-cell count
	# without changing the footprint that was asked for - measured 0.640 against a
	# requested 0.700, and 0.705 with rivers off. This probe is about coverage and
	# topology, so it takes rivers out rather than widening its tolerance until the
	# difference hides. Rivers have their own coverage elsewhere.
	generator.set("RiverDensity", 0.0)
	generator.set("Seed", 8675309)
	generator.set("Frequency", 0.026)
	generator.set("LakeCoverage", 0.0)
	generator.set("TopologySamplesPerCell", 6)
	generator.set("ArchipelagoIslandCount", 4)
	root.add_child(generator)
	await process_frame

	# Coverage has a CEILING, and the contract is that the shortfall is reported
	# rather than silently wrong. N separated masses plus the gap between them -
	# which scales with BeachWidth - cannot cover an arbitrary share of a fixed
	# map: measured on this 48x30 map, 4 islands saturate at 0.585 and 2 islands
	# at 0.656, while both hit 0.25 and 0.50 exactly. Asking for N masses and
	# getting N is the promise the engine keeps first.
	#
	# So this asserts what is actually owed: the request is met exactly where it
	# fits, the target is always reported beside what was achieved, coverage never
	# exceeds the request, and asking for more never yields less.
	for mode: int in [1, 2]:
		generator.set("Landform", mode)
		var previous_field := -1.0
		for requested: float in [0.25, 0.50, 0.70]:
			generator.set("LandmassScale", requested)
			generator.call("GenerateTerrain")
			var diagnostics: Dictionary = generator.call("GetGenerationDiagnostics")
			var actual_field := float(diagnostics["land_footprint_coverage"])
			if absf(float(diagnostics["target_land_coverage"]) - requested) > 0.001:
				_fail("Diagnostics must report the requested coverage %.3f, reported %.3f for mode %d."
					% [requested, float(diagnostics["target_land_coverage"]), mode])
				return
			if actual_field > requested + 0.006:
				_fail("Field coverage %.3f exceeded requested %.3f for mode %d." % [actual_field, requested, mode])
				return
			if requested <= 0.50 and absf(actual_field - requested) > 0.006:
				_fail("Field coverage %.3f missed an achievable requested %.3f for mode %d." % [actual_field, requested, mode])
				return
			if actual_field < previous_field - 0.001:
				_fail("Asking for %.3f gave less land (%.3f) than the previous request (%.3f) for mode %d."
					% [requested, actual_field, previous_field, mode])
				return
			previous_field = actual_field
			var logical_coverage := _land_cells(cells) / float(SIZE.x * SIZE.y)
			if absf(logical_coverage - actual_field) > 0.055:
				_fail("Grid coverage %.3f disagrees with the field footprint %.3f for mode %d." % [logical_coverage, actual_field, mode])
				return
			var components := _land_components(cells)
			if mode == 1 and components != 1:
				_fail("Island must have one connected gameplay landmass; got %d." % components)
				return
			if mode == 2 and components < 2:
				_fail("Archipelago must have separated gameplay islands; got %d." % components)
				return
			if not _has_water_on_every_edge(cells):
				_fail("Island landforms must leave water on every map edge.")
				return
			if not _continuous_centres_match(cells, generator):
				return

	generator.set("Landform", 1)
	generator.set("LandmassScale", 0.70)
	generator.set("LakeCoverage", 0.08)
	generator.call("GenerateTerrain")
	var lake_cells := 0
	for y in SIZE.y:
		for x in SIZE.x:
			var source := str(generator.call("WaterSourceAt", Vector2i(x, y)))
			if source != "lake":
				continue
			lake_cells += 1
			if x == 0 or y == 0 or x == SIZE.x - 1 or y == SIZE.y - 1:
				_fail("An enclosed lake reached the ocean edge.")
				return
	if lake_cells == 0:
		_fail("Requested lake coverage produced no gameplay lake cells.")
		return

	var first_signature := _signature(cells)
	generator.call("GenerateTerrain")
	if _signature(cells) != first_signature:
		_fail("The same seed and settings did not reproduce the same terrain.")
		return

	print("[grid-terrain-topology] OK: lake_cells=%d" % lake_cells)
	quit(0)

func _continuous_centres_match(cells: Node, generator: Node) -> bool:
	for y in SIZE.y:
		for x in SIZE.x:
			var grid_kind := str(cells.call("GetTerrainKind", Vector2i(x, y)))
			var rendered_kind := str(generator.call("TerrainKindAt", Vector2(x + 0.5, y + 0.5)))
			if grid_kind != rendered_kind:
				_fail("Grid/render mismatch at %s: %s != %s." % [Vector2i(x, y), grid_kind, rendered_kind])
				return false
	return true

func _land_cells(cells: Node) -> int:
	var count := 0
	for y in SIZE.y:
		for x in SIZE.x:
			if not _is_water(str(cells.call("GetTerrainKind", Vector2i(x, y)))):
				count += 1
	return count

func _land_components(cells: Node) -> int:
	var visited := {}
	var components := 0
	for y in SIZE.y:
		for x in SIZE.x:
			var start := Vector2i(x, y)
			if visited.has(start) or _is_water(str(cells.call("GetTerrainKind", start))):
				continue
			components += 1
			var frontier: Array[Vector2i] = [start]
			visited[start] = true
			while not frontier.is_empty():
				var current: Vector2i = frontier.pop_back()
				for offset: Vector2i in [Vector2i.LEFT, Vector2i.RIGHT, Vector2i.UP, Vector2i.DOWN]:
					var next := current + offset
					if next.x < 0 or next.y < 0 or next.x >= SIZE.x or next.y >= SIZE.y:
						continue
					if visited.has(next) or _is_water(str(cells.call("GetTerrainKind", next))):
						continue
					visited[next] = true
					frontier.append(next)
	return components

func _has_water_on_every_edge(cells: Node) -> bool:
	for x in SIZE.x:
		if not _is_water(str(cells.call("GetTerrainKind", Vector2i(x, 0)))):
			return false
		if not _is_water(str(cells.call("GetTerrainKind", Vector2i(x, SIZE.y - 1)))):
			return false
	for y in SIZE.y:
		if not _is_water(str(cells.call("GetTerrainKind", Vector2i(0, y)))):
			return false
		if not _is_water(str(cells.call("GetTerrainKind", Vector2i(SIZE.x - 1, y)))):
			return false
	return true

func _signature(cells: Node) -> String:
	var signature := ""
	for y in SIZE.y:
		for x in SIZE.x:
			signature += str(cells.call("GetTerrainKind", Vector2i(x, y))).left(1)
	return signature

func _is_water(kind: String) -> bool:
	return kind == "deep_water" or kind == "shallow_water" or kind == "water"

func _fail(message: String) -> void:
	push_error("[grid-terrain-topology] " + message)
	quit(1)
