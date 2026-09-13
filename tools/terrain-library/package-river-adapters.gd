extends SceneTree

const BASE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/river_adapters_v1/"
const SECTIONS = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/river_sections_v1/"
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
		var straight = load(SECTIONS+projection+"/river_sections.tres") as TileSet
		var tiles = TileSet.new()
		tiles.tile_size = straight.tile_size
		tiles.tile_shape = straight.tile_shape
		tiles.tile_layout = straight.tile_layout
		var texture = ImageTexture.create_from_image(Image.load_from_file(folder+"river_adapters_16.png"))
		check(ResourceSaver.save(texture,folder+"river_adapters_16.res")==OK,"Texture save failed")
		var atlas = TileSetAtlasSource.new()
		atlas.texture = load(folder+"river_adapters_16.res")
		atlas.texture_region_size = tiles.tile_size
		tiles.add_source(atlas,0)
		tiles.add_source(straight.get_source(1).duplicate(),1)
		tiles.add_source(straight.get_source(0).duplicate(),2)
		var patterns = {}
		for profile in manifest.profiles:
			var pattern = TileMapPattern.new()
			for cell in profile.cells:
				var coords = Vector2i(cell.atlas[0],cell.atlas[1])
				atlas.create_tile(coords)
				atlas.set_tile_animation_columns(coords,1)
				atlas.set_tile_animation_separation(coords,Vector2i(0,7))
				atlas.set_tile_animation_frames_count(coords,16)
				atlas.set_tile_animation_speed(coords,16.0/1.2)
				pattern.set_cell(Vector2i(cell.local[0],cell.local[1]),0,coords,0)
				check(pattern.get_cell_alternative_tile(Vector2i(cell.local[0],cell.local[1]))==0,"Invalid pattern alternative")
			tiles.add_pattern(pattern)
			patterns[profile.id] = pattern
		check(tiles.get_patterns_count()==16,"Missing native patterns")
		check(ResourceSaver.save(tiles,folder+"river_adapters.tres")==OK,"TileSet save failed")
		var scene = load(SECTIONS+projection+"/river_widths_review.tscn").instantiate()
		scene.name = "RiverWidthTransitionReview"
		var layer = scene.get_node("Water") as TileMapLayer
		layer.tile_set = load(folder+"river_adapters.tres")
		layer.clear()
		for y in range(18):
			for x in range(6):
				layer.set_cell(Vector2i(x,y),1,Vector2i.ZERO)
		# One route: 1 -> 2 -> 3 -> 2 -> 1 cells, with two-cell adapters.
		for section in [[0,1],[4,2],[8,3],[12,2],[16,1]]:
			for y in range(section[0],section[0]+2):
				for x in range(section[1]):
					var kind_index = 0 if section[1]==1 else (1 if x==0 else (3 if x==section[1]-1 else 2))
					layer.set_cell(Vector2i(x+1,y),2,Vector2i(kind_index,0))
		for adapter in [[2,"narrow_expand",1],[6,"edge_expand",2],[10,"edge_contract",2],[14,"narrow_contract",1]]:
			layer.set_pattern(Vector2i(adapter[2],adapter[0]),patterns["north_south."+adapter[1]])
			if adapter[2]==2:
				for y in range(adapter[0],adapter[0]+2):
					layer.set_cell(Vector2i(1,y),2,Vector2i(1,0))
		var packed = PackedScene.new()
		check(packed.pack(scene)==OK,"Scene pack failed")
		check(ResourceSaver.save(packed,folder+"river_adapter_review.tscn")==OK,"Scene save failed")
		check(ResourceLoader.load(folder+"river_adapter_review.tscn","",ResourceLoader.CACHE_MODE_IGNORE)!=null,"Scene reload failed")
		var original: Array = []
		for cell in layer.get_used_cells():
			original.append({"cell":cell,"source":layer.get_cell_source_id(cell),"atlas":layer.get_cell_atlas_coords(cell)})
		for direction in range(1,4):
			layer.clear()
			for entry in original:
				var cell: Vector2i = entry.cell
				var coords: Vector2i = entry.atlas
				var target = Vector2i(cell.x,17-cell.y) if direction==1 else (Vector2i(cell.y,cell.x) if direction==2 else Vector2i(17-cell.y,cell.x))
				if entry.source==0:
					var local = Vector2i(coords.x%2,coords.y%2)
					local = Vector2i(local.x,1-local.y) if direction==1 else (Vector2i(local.y,local.x) if direction==2 else Vector2i(1-local.y,local.x))
					coords = Vector2i((coords.x/2)*2+local.x,direction*2+local.y)
				elif entry.source==2:
					coords.y = direction
				layer.set_cell(target,entry.source,coords,0)
			check(layer.get_used_cells().size()==108,"Directional route lost cells")
			for cell in layer.get_used_cells():
				check(layer.get_cell_tile_data(cell)!=null,"Directional route has an invalid tile")
			var direction_name = ["north_south","south_north","west_east","east_west"][direction]
			packed = PackedScene.new()
			check(packed.pack(scene)==OK,"Directional scene pack failed")
			check(ResourceSaver.save(packed,folder+"river_adapter_"+direction_name+".tscn")==OK,"Directional scene save failed")
		scene.free()
	print("RIVER ADAPTERS ","PASSED" if errors.is_empty() else "FAILED")
	quit(0 if errors.is_empty() else 1)
