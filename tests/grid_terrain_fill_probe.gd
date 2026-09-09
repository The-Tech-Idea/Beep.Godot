extends SceneTree

var batches := 0
var singles := 0

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	var cells: Node = load("res://addons/beep_game_builder_cs/ecs/grid/GridCellDataComponent.cs").new()
	root.add_child(cells)
	cells.call("SetFlags", Vector2i(-1, 2), 4)
	cells.call("SetMetadata", Vector2i(-1, 2), "terrain_elevation", 2.5)
	assert(cells.call("PlantCrop", Vector2i(-1, 2), "wheat", 3, -1))
	cells.connect("CellsChanged", func(_kind, _chunks): batches += 1)
	cells.connect("CellChanged", func(_x, _y): singles += 1)
	cells.call("FillTerrain", Rect2i(-2, 1, 3, 2), "  gravel  ")
	assert(cells.get("CellCount") == 6)
	assert(batches == 1 and singles == 0, "Fill must emit one bulk notification")
	assert(cells.call("GetTerrainKind", Vector2i(-1, 2)) == "gravel")
	assert(cells.call("GetCropId", Vector2i(-1, 2)) == "wheat")
	assert(cells.call("GetFlags", Vector2i(-1, 2)) & 4)
	assert(cells.call("GetMetadata", Vector2i(-1, 2), "terrain_elevation") == 2.5)
	assert(not cells.call("HasCell", Vector2i(1, 2)))
	cells.call("FillTerrain", Rect2i(-2, 1, 3, 2), "gravel")
	cells.call("FillTerrain", Rect2i(10, 10, 0, 3), "sand")
	assert(batches == 1, "Identical and empty fills must not rebuild views")
	cells.call("FillTerrain", Rect2i(7, 8, 1, 1), "")
	assert(cells.call("HasCell", Vector2i(7, 8)))
	assert(cells.call("GetTerrainKind", Vector2i(7, 8)) == "grass")
	assert(batches == 2)
	cells.free()
	print("[grid-terrain-fill] OK")
	quit()
