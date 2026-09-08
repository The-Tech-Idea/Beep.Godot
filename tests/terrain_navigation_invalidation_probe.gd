extends SceneTree

const BASE := "res://addons/beep_game_builder_cs/ecs/grid/"
var failures: Array[String] = []
var results := {}

func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		push_error(message)

func _initialize() -> void: run.call_deferred()

func run() -> void:
	var host := Node.new()
	root.add_child(host)
	var cells: Node = load(BASE + "GridCellDataComponent.cs").new()
	cells.name = "Cells"
	host.add_child(cells)
	var nav: Node = load(BASE + "GridNavigationComponent.cs").new()
	nav.CellDataPath = NodePath("../Cells")
	nav.BoundsSize = Vector2i(64, 64)
	nav.PathExpansionsPerFrame = 16
	host.add_child(nav)
	nav.PathRequestCompleted.connect(func(id, path, reason): results[id] = [path, reason])
	var harmless := {
		"new tilled cell": func(): cells.Till(Vector2i(8, 8)),
		"water": func(): cells.Water(Vector2i(8, 8)),
		"plant": func(): cells.PlantCrop(Vector2i(8, 8), "corn", 1, -1),
		"growth": func(): cells.AdvanceDay(1),
		"harvest": func(): cells.HarvestCrop(Vector2i(8, 8), true),
		"clear unblocked land": func(): cells.ClearLand(Vector2i(9, 9)),
		"same terrain": func(): cells.SetTerrainKind(Vector2i(1, 1), " GRASS "),
		"same fill": func(): cells.FillTerrain(Rect2i(0, 0, 2, 2), "grass"),
		"same blocked bit": func(): cells.SetFlags(Vector2i(2, 2), 3),
		"add existing blocked": func(): cells.AddFlag(Vector2i(2, 2), 1),
		"remove unrelated flag": func(): cells.RemoveFlag(Vector2i(2, 2), 2),
		"same relief": func(): cells.SetMetadata(Vector2i(3, 3), "terrain_relief", 1),
		"same ramp": func(): cells.SetMetadata(Vector2i(4, 4), "terrain_ramp_direction", Vector2i.RIGHT),
		"decorations": func(): cells.SetMetadata(Vector2i(8, 8), "terrain_feature", "forest"),
		"shade": func(): cells.SetMetadata(Vector2i(8, 8), "terrain_shade", 0.7),
		"unrelated metadata": func(): cells.SetMetadata(Vector2i(8, 8), "custom", {"value": 42}),
		"normalized default": func(): cells.DefaultTerrainKind = "GRASS"
	}
	var traversal := {
		"terrain kind": func(): cells.SetTerrainKind(Vector2i(1, 1), "mud"),
		"fill": func(): cells.FillTerrain(Rect2i(8, 8, 2, 2), "water"),
		"set blocked": func(): cells.SetFlags(Vector2i(2, 2), 0),
		"add blocked": func(): cells.AddFlag(Vector2i(6, 6), 1),
		"remove blocked": func(): cells.RemoveFlag(Vector2i(2, 2), 1),
		"clear blocked land": func(): cells.ClearLand(Vector2i(2, 2)),
		"relief": func(): cells.SetMetadata(Vector2i(3, 3), "terrain_relief", 2),
		"ramp": func(): cells.SetMetadata(Vector2i(4, 4), "terrain_ramp_direction", Vector2i.DOWN),
		"default terrain": func(): cells.DefaultTerrainKind = "mud",
		"bulk load": func(): cells.LoadCells(cells.GetCells(), true),
		"chunk restore": func(): cells.RestoreChunkState(cells.CaptureChunkState()),
		"clear world": func(): cells.ClearCells()
	}
	for mutations in [harmless, traversal]:
		for label in mutations:
			cells.DefaultTerrainKind = "grass"
			cells.ClearCells()
			cells.SetTerrainKind(Vector2i(1, 1), "grass")
			cells.SetFlags(Vector2i(2, 2), 1)
			cells.SetMetadata(Vector2i(3, 3), "terrain_relief", 1)
			cells.SetMetadata(Vector2i(4, 4), "terrain_ramp_direction", Vector2i.RIGHT)
			if label in ["water", "plant", "growth", "harvest"]: cells.Till(Vector2i(8, 8))
			if label in ["growth", "harvest"]: cells.PlantCrop(Vector2i(8, 8), "corn", 1, -1)
			if label == "harvest": cells.AdvanceDay(1)
			var id: int = nav.RequestCellPath(Vector2i.ZERO, Vector2i(60, 60))
			nav.ProcessPathRequests()
			check(not results.has(id), "Fixture completed before mutation: " + label)
			mutations[label].call()
			for frame in 1000:
				nav.ProcessPathRequests()
				if results.has(id): break
			check(results.has(id), "Search never finished: " + label)
			if results.has(id):
				var expected := "navigation_changed" if mutations == traversal else ""
				check(results[id][1] == expected, "Incorrect invalidation for " + label + ": " + results[id][1])
	# A farm tick on every slice must not starve an active search.
	cells.DefaultTerrainKind = "grass"
	cells.ClearCells()
	cells.Till(Vector2i(8, 8))
	cells.PlantCrop(Vector2i(8, 8), "corn", 1, 1)
	var farming: int = nav.RequestCellPath(Vector2i.ZERO, Vector2i(60, 60))
	for frame in 1000:
		nav.ProcessPathRequests()
		cells.AdvanceDay(1)
		cells.Water(Vector2i(8, 8))
		if results.has(farming): break
	check(results.has(farming) and results[farming][1] == "", "Continuous farming starved pathfinding")
	host.free()
	print("[terrain-navigation-invalidation] OK" if failures.is_empty() else "[terrain-navigation-invalidation] FAILED")
	quit(0 if failures.is_empty() else 1)
