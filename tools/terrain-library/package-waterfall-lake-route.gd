extends SceneTree

const CLIFF = "res://addons/beep_game_builder_cs/generated/dev/cartoon/elevation/staging/grass_granite_front_v1/"
const LAKE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/river_lake_connectors_v1/square/river_lake.tres"

func _initialize() -> void:
	var scene = load(CLIFF+"river_waterfall_review.tscn").instantiate()
	scene.name = "RiverWaterfallLowerRiverLakeCandidate"
	var layer = TileMapLayer.new()
	layer.name = "LowerLake"
	layer.tile_set = load(LAKE)
	layer.material = scene.get_node("Downstream").material.duplicate()
	layer.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	scene.add_child(layer)
	scene.move_child(layer,0)
	layer.owner = scene
	for y in range(2,10):
		for x in range(5): layer.set_cell(Vector2i(x,y),0,Vector2i(7,5),0)
	var cells: Array[Vector2i] = []
	for y in range(5,9):
		for x in range(1,4): cells.append(Vector2i(x,y))
	cells.append(Vector2i(2,4))
	layer.set_cells_terrain_connect(cells,0,0,false)
	layer.set_cell(Vector2i(2,5),1,Vector2i.ZERO,0)
	layer.set_cell(Vector2i(2,4),2,Vector2i.ZERO,0)
	scene.get_node("SurfacePlane").layer_paths.append(NodePath("../LowerLake"))
	scene.set_meta("missing_connections","Outer cliff repeats, side/corner contacts, sea outlet and isometric waterfall")
	var packed = PackedScene.new()
	var error = packed.pack(scene)
	if error==OK: error = ResourceSaver.save(packed,CLIFF+"river_waterfall_lake_review.tscn")
	scene.free()
	print("WATERFALL LAKE ROUTE ","PASSED" if error==OK else "FAILED")
	quit(0 if error==OK else 1)
