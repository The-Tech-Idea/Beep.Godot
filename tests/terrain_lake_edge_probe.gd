extends SceneTree

# What a lake actually is on a generated map: which cells are lake water, what the stage put
# around them, and what the painted view is told to draw its bank with. Written to find why a
# lake reads as a smear in the ground rather than as water with an edge.

const BASE := "res://addons/beep_game_builder_cs/ecs/"
const SIZE := Vector2i(48, 48)

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	var cells: Node = load(BASE + "grid/GridCellDataComponent.cs").new()
	cells.name = "Cells"
	root.add_child(cells)
	var generator: Node = load(BASE + "terrain/TerrainGeneratorComponent.cs").new()
	generator.name = "Generator"
	generator.set("CellDataPath", NodePath("../Cells"))
	generator.set("GenerateOnReady", false)
	generator.set("ClearExistingCells", true)
	generator.set("BoundsSize", SIZE)
	generator.set("Seed", 31415)
	generator.set("UseClimateBiomeMaps", true)
	generator.set("UseScaleRules", true)
	generator.set("LakeCoverage", 0.05)
	generator.set("LakeShoreWidth", 1.0)
	generator.set("BeachWidth", 1.0)
	root.add_child(generator)
	generator.call("ApplyMapSetup", 0, 1, 1, 1, 1, 1)
	print("after ApplyMapSetup: LakeShoreWidth=%s BeachWidth=%s LakeCoverage=%s"
		% [generator.get("LakeShoreWidth"), generator.get("BeachWidth"), generator.get("LakeCoverage")])
	generator.call("GenerateTerrain")

	var lake_cells: Array[Vector2i] = []
	var sand := 0
	for y in SIZE.y:
		for x in SIZE.x:
			var cell := Vector2i(x, y)
			var kind: String = generator.call("TerrainKindAt", cell)
			if generator.call("WaterSourceAt", cell) == "lake":
				lake_cells.append(cell)
			if kind == "sand":
				sand += 1
	print("lake cells=%d, sand cells=%d" % [lake_cells.size(), sand])
	if lake_cells.is_empty():
		print("[lake-edge] no lake on this map")
		quit(0)
		return

	# One lake, and everything around it: what the generator says, and what the live cells carry.
	var probe: Vector2i = lake_cells[lake_cells.size() / 2]
	print("probing around %s" % probe)
	for dy in range(-3, 4):
		var row := ""
		for dx in range(-3, 4):
			var cell: Vector2i = probe + Vector2i(dx, dy)
			var kind: String = generator.call("TerrainKindAt", cell)
			var source: String = generator.call("WaterSourceAt", cell)
			var width = cells.call("GetMetadata", cell, "terrain_lake_shore_width")
			row += "%-14s" % ("%s/%s/%s" % [kind.substr(0, 5), source.substr(0, 4) if source != "" else "-", width])
		print("  ", row)
	var relief_counts := {}
	for cell in lake_cells:
		var r: int = generator.call("ReliefAt", cell)
		relief_counts[r] = relief_counts.get(r, 0) + 1
	print("lake cell relief: ", relief_counts)

	# The SMALL ponds: a lake of one or two cells is what still reads as a smear. Group the lake
	# cells into connected bodies and report the smallest, with what surrounds them.
	var remaining := {}
	for cell in lake_cells: remaining[cell] = true
	var bodies: Array = []
	for cell in lake_cells:
		if not remaining.has(cell): continue
		var body: Array[Vector2i] = []
		var frontier: Array[Vector2i] = [cell]
		remaining.erase(cell)
		while not frontier.is_empty():
			var at: Vector2i = frontier.pop_back()
			body.append(at)
			for step in [Vector2i.LEFT, Vector2i.RIGHT, Vector2i.UP, Vector2i.DOWN]:
				var next: Vector2i = at + step
				if remaining.has(next):
					remaining.erase(next)
					frontier.append(next)
		bodies.append(body)
	bodies.sort_custom(func(a, b): return a.size() < b.size())
	print("lake bodies: %d, sizes %s" % [bodies.size(), bodies.map(func(b): return b.size())])
	var smallest: Array = bodies[0]
	print("smallest pond at %s (%d cells):" % [smallest[0], smallest.size()])
	for dy in range(-2, 3):
		var row := ""
		for dx in range(-2, 3):
			var cell: Vector2i = smallest[0] + Vector2i(dx, dy)
			row += "%-12s" % ("%s/%s" % [str(generator.call("TerrainKindAt", cell)).substr(0, 5),
				"lake" if generator.call("WaterSourceAt", cell) == "lake" else "-"])
		print("  ", row)
	print("[lake-edge] OK")
	quit(0)
