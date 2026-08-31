extends SceneTree

const CELL_DATA_SCRIPT := preload("res://addons/beep_game_builder_cs/ecs/terrain/GridCellDataComponent.cs")
const GENERATOR_SCRIPT := preload("res://addons/beep_game_builder_cs/ecs/terrain/GridTerrainGeneratorComponent.cs")

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var cells := CELL_DATA_SCRIPT.new()
	cells.name = "Cells"
	root.add_child(cells)

	var generator := GENERATOR_SCRIPT.new()
	generator.name = "Generator"
	generator.set("CellDataPath", NodePath("../Cells"))
	generator.set("UsePainterSettings", false)
	generator.set("BoundsSize", Vector2i(64, 40))
	generator.set("Seed", 31415)
	generator.set("LakeCoverage", 0.18)
	generator.set("LakeFrequencyMultiplier", 0.11)
	generator.set("SwampCoverage", 0.45)
	generator.set("SnowCoverage", 0.45)
	generator.set("IceCoverage", 0.45)
	root.add_child(generator)
	await process_frame
	generator.call("GenerateTerrain")

	var counts := { "swamp": 0, "snow": 0, "ice": 0, "deep_water": 0, "shallow_water": 0 }
	for y in 40:
		for x in 64:
			var terrain: String = str(cells.call("GetTerrainKind", Vector2i(x, y)))
			if counts.has(terrain):
				counts[terrain] += 1

	if counts["swamp"] == 0 or counts["snow"] == 0 or counts["ice"] == 0:
		_fail("Feature coverage did not produce all requested feature biomes: %s" % counts)
		return
	if counts["deep_water"] + counts["shallow_water"] == 0:
		_fail("Water coverage did not produce the unified water family.")
		return

	generator.set("Landform", 1) # Island
	generator.set("LakeCoverage", 0.0)
	generator.set("LandmassScale", 0.35)
	generator.call("GenerateTerrain")
	var small_island_land := _land_cells(cells, Vector2i(64, 40))
	if not _has_water_on_every_edge(cells, Vector2i(64, 40)):
		_fail("Island mode must surround land with water on every map edge.")
		return
	if _land_components(cells, Vector2i(64, 40)) != 1:
		_fail("Island mode must produce one connected landmass.")
		return

	generator.set("LandmassScale", 0.85)
	generator.call("GenerateTerrain")
	var large_island_land := _land_cells(cells, Vector2i(64, 40))
	var first_seed_signature := _terrain_signature(cells, Vector2i(64, 40))
	if large_island_land <= small_island_land:
		_fail("Increasing LandmassScale must increase generated island land area.")
		return

	generator.set("Seed", 31416)
	generator.call("GenerateTerrain")
	if _terrain_signature(cells, Vector2i(64, 40)) == first_seed_signature:
		_fail("Changing the terrain seed must change the generated island layout.")
		return

	generator.set("Seed", 31415)
	generator.set("LandmassScale", 0.70)
	generator.set("Frequency", 0.072)

	generator.set("Landform", 2) # Archipelago
	generator.call("GenerateTerrain")
	if not _has_water_on_every_edge(cells, Vector2i(64, 40)):
		_fail("Archipelago mode must surround the map with water.")
		return
	var archipelago_components := _land_components(cells, Vector2i(64, 40))
	if archipelago_components < 2:
		_fail("Archipelago mode must produce multiple separated landmasses; got %d (mode %s)." % [archipelago_components, str(generator.get("Landform"))])
		return

	root.remove_child(generator)
	generator.free()
	root.remove_child(cells)
	cells.free()
	print("[grid-terrain-features] OK: %s" % counts)
	quit(0)

func _fail(message: String) -> void:
	push_error("[grid-terrain-features] " + message)
	quit(1)

func _has_water_on_every_edge(cells: Node, size: Vector2i) -> bool:
	for x in size.x:
		if not _is_water(str(cells.call("GetTerrainKind", Vector2i(x, 0)))):
			return false
		if not _is_water(str(cells.call("GetTerrainKind", Vector2i(x, size.y - 1)))):
			return false
	for y in size.y:
		if not _is_water(str(cells.call("GetTerrainKind", Vector2i(0, y)))):
			return false
		if not _is_water(str(cells.call("GetTerrainKind", Vector2i(size.x - 1, y)))):
			return false
	return true

func _land_components(cells: Node, size: Vector2i) -> int:
	var visited := {}
	var components := 0
	for y in size.y:
		for x in size.x:
			var start := Vector2i(x, y)
			if visited.has(start) or _is_water(str(cells.call("GetTerrainKind", start))):
				continue
			components += 1
			var frontier: Array[Vector2i] = [start]
			visited[start] = true
			while not frontier.is_empty():
				var current: Vector2i = frontier.pop_back()
				for offset: Vector2i in [Vector2i.LEFT, Vector2i.RIGHT, Vector2i.UP, Vector2i.DOWN]:
					var next: Vector2i = current + offset
					if next.x < 0 or next.y < 0 or next.x >= size.x or next.y >= size.y:
						continue
					if visited.has(next) or _is_water(str(cells.call("GetTerrainKind", next))):
						continue
					visited[next] = true
					frontier.append(next)
	return components

func _is_water(terrain: String) -> bool:
	return terrain == "deep_water" or terrain == "shallow_water" or terrain == "water"

func _land_cells(cells: Node, size: Vector2i) -> int:
	var count := 0
	for y in size.y:
		for x in size.x:
			if not _is_water(str(cells.call("GetTerrainKind", Vector2i(x, y)))):
				count += 1
	return count

func _terrain_signature(cells: Node, size: Vector2i) -> String:
	var signature := ""
	for y in size.y:
		for x in size.x:
			signature += str(cells.call("GetTerrainKind", Vector2i(x, y))).left(1)
	return signature
