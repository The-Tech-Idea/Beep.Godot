extends SceneTree

const BASE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/river_lake_connectors_v1/"
const LAKE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/lake_banks_v1/"
const RIVER = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/river_sections_v1/"
var errors: Array[String] = []

func _initialize() -> void:
	call_deferred("run")

func check(ok: bool,message: String) -> void:
	if not ok:
		errors.append(message)
		push_error(message)

func run() -> void:
	var manifest = JSON.parse_string(FileAccess.get_file_as_string(BASE+"manifest.json"))
	for projection in ["square","isometric"]:
		var folder = BASE+projection+"/"
		var scene = load(LAKE+projection+"/lake_review.tscn").instantiate()
		var layer: TileMapLayer = scene.get_node("Water")
		var tiles: TileSet = layer.tile_set.duplicate(true)
		tiles.add_custom_data_layer()
		tiles.set_custom_data_layer_name(0,"flow_profile")
		tiles.set_custom_data_layer_type(0,TYPE_STRING)
		var texture = ImageTexture.create_from_image(Image.load_from_file(folder+"river_lake_16.png"))
		check(ResourceSaver.save(texture,folder+"river_lake_16.res")==OK,"Save connector texture")
		var atlas = TileSetAtlasSource.new()
		atlas.texture = load(folder+"river_lake_16.res")
		atlas.texture_region_size = tiles.tile_size
		tiles.add_source(atlas,1)
		for index in range(8):
			var coords = Vector2i(index,0)
			atlas.create_tile(coords)
			atlas.set_tile_animation_columns(coords,1)
			atlas.set_tile_animation_frames_count(coords,16)
			atlas.set_tile_animation_speed(coords,16.0/1.2)
			atlas.get_tile_data(coords,0).set_custom_data("flow_profile",manifest.profiles[index].id)
		var river: TileSet = load(RIVER+projection+"/river_sections.tres")
		tiles.add_source(river.get_source(0).duplicate(true),2)
		check(ResourceSaver.save(tiles,folder+"river_lake.tres")==OK,"Save connector TileSet")
		layer.tile_set = load(folder+"river_lake.tres")
		layer.clear()
		for y in range(11):
			for x in range(11): layer.set_cell(Vector2i(x,y),0,Vector2i(7,5),0)
		var cells: Array[Vector2i] = []
		for y in range(3,9):
			for x in range(2,9): cells.append(Vector2i(x,y))
		cells.append(Vector2i(5,2))
		layer.set_cells_terrain_connect(cells,0,0,false)
		layer.set_cell(Vector2i(5,3),1,Vector2i.ZERO,0)
		for y in range(3): layer.set_cell(Vector2i(5,y),2,Vector2i.ZERO,0)
		for cell in layer.get_used_cells(): check(layer.get_cell_tile_data(cell)!=null,"Missing route tile")
		scene.name = "ExplicitRiverLakeInletReview"
		var packed = PackedScene.new()
		check(packed.pack(scene)==OK,"Pack river lake scene")
		check(ResourceSaver.save(packed,folder+"river_lake_review.tscn")==OK,"Save river lake scene")
		scene.free()
	print("RIVER LAKE ","PASSED" if errors.is_empty() else "FAILED")
	quit(0 if errors.is_empty() else 1)
