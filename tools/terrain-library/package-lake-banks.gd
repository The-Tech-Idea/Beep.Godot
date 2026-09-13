extends "res://tools/terrain-library/build-mask-fixtures.gd"

const LAKE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/lake_banks_v1/"
var failures: Array[String] = []

func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		push_error(message)

func package(iso: bool, masks: Array[int], frames: Image) -> Dictionary:
	var projection = "isometric" if iso else "square"
	var folder = LAKE + projection + "/"
	DirAccess.make_dir_recursive_absolute(folder)
	var size = Vector2i(64,32 if iso else 64)
	var image = Image.create(512,size.y * 6 * 16,false,Image.FORMAT_RGBA8)
	image.fill(Color.TRANSPARENT)
	var tiles = TileSet.new()
	tiles.tile_size = size
	if iso:
		tiles.tile_shape = TileSet.TILE_SHAPE_ISOMETRIC
		tiles.tile_layout = TileSet.TILE_LAYOUT_DIAMOND_DOWN
	tiles.add_terrain_set()
	tiles.set_terrain_set_mode(0,TileSet.TERRAIN_MODE_MATCH_CORNERS_AND_SIDES)
	tiles.add_terrain(0)
	tiles.set_terrain_name(0,0,"shallow_water")
	var bits = [TileSet.CELL_NEIGHBOR_TOP_RIGHT_SIDE,TileSet.CELL_NEIGHBOR_BOTTOM_RIGHT_SIDE,TileSet.CELL_NEIGHBOR_BOTTOM_LEFT_SIDE,TileSet.CELL_NEIGHBOR_TOP_LEFT_SIDE,TileSet.CELL_NEIGHBOR_RIGHT_CORNER,TileSet.CELL_NEIGHBOR_BOTTOM_CORNER,TileSet.CELL_NEIGHBOR_LEFT_CORNER,TileSet.CELL_NEIGHBOR_TOP_CORNER] if iso else [TileSet.CELL_NEIGHBOR_TOP_SIDE,TileSet.CELL_NEIGHBOR_RIGHT_SIDE,TileSet.CELL_NEIGHBOR_BOTTOM_SIDE,TileSet.CELL_NEIGHBOR_LEFT_SIDE,TileSet.CELL_NEIGHBOR_TOP_RIGHT_CORNER,TileSet.CELL_NEIGHBOR_BOTTOM_RIGHT_CORNER,TileSet.CELL_NEIGHBOR_BOTTOM_LEFT_CORNER,TileSet.CELL_NEIGHBOR_TOP_LEFT_CORNER]
	for frame in range(16):
		for index in range(48):
			var origin = Vector2i(index % 8,index / 8 + frame * 6) * size
			for y in range(size.y):
				for x in range(size.x):
					var p = Vector2(x,y)
					if iso:
						var dx = (x+0.5-32.0)/64.0
						var dy = (y+0.5-16.0)/32.0
						p = Vector2(dx+dy+0.5,-dx+dy+0.5)*64.0-Vector2(0.5,0.5)
						if p.x < -0.5 or p.y < -0.5 or p.x >= 63.5 or p.y >= 63.5:
							continue
					var color = Color.BLUE
					if index < 47:
						var distance = distance_to_edge(masks[index],p)
						if distance > 1.5:
							color = frames.get_pixel(clampi(int(round(p.x)),0,63),clampi(int(round(p.y)),0,63)+frame*64)
						elif distance >= -1.0:
							color = Color("bda779")
					image.set_pixelv(origin+Vector2i(x,y),color)
	check(image.save_png(folder+"grass_lake_16.png")==OK,"Cannot save lake atlas")
	var texture = ImageTexture.create_from_image(image)
	check(ResourceSaver.save(texture,folder+"grass_lake_16.res")==OK,"Cannot save lake texture")
	var atlas = TileSetAtlasSource.new()
	atlas.texture = load(folder+"grass_lake_16.res")
	atlas.texture_region_size = size
	tiles.add_source(atlas,0)
	for index in range(48):
		var coords = Vector2i(index%8,index/8)
		atlas.create_tile(coords)
		if index < 47:
			atlas.set_tile_animation_columns(coords,1)
			atlas.set_tile_animation_separation(coords,Vector2i(0,5))
			atlas.set_tile_animation_frames_count(coords,16)
			atlas.set_tile_animation_speed(coords,16.0/1.2)
		var data = atlas.get_tile_data(coords,0)
		data.terrain_set = 0
		data.terrain = 0 if index < 47 else -1
		for b in range(8):
			check(data.is_valid_terrain_peering_bit(bits[b]),"Invalid native bank peering bit")
			data.set_terrain_peering_bit(bits[b],0 if index < 47 and masks[index] & (1<<b) else -1)
	check(ResourceSaver.save(tiles,folder+"grass_lake.tres")==OK,"Cannot save lake TileSet")
	var scene = Node2D.new()
	scene.name = "LakeBankCandidate"
	var layer = TileMapLayer.new()
	layer.name = "Water"
	layer.tile_set = load(folder+"grass_lake.tres")
	layer.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	var material = ShaderMaterial.new()
	material.shader = load("res://addons/beep_game_builder_cs/shaders/terrain_mask_surface_cartoon.gdshader")
	material.set_shader_parameter("secondary_enabled",true)
	material.set_shader_parameter("secondary_texture",load(BASE+"runtime/surfaces_v1/grass_256.res"))
	layer.material = material
	scene.add_child(layer)
	layer.owner = scene
	var plane = load("res://addons/beep_game_builder_cs/ecs/terrain/TerrainSurfacePlane.gd").new()
	plane.name = "SurfacePlane"
	plane.projection = 1 if iso else 0
	plane.cell_size = Vector2(size)
	plane.layer_paths.assign([NodePath("../Water")])
	scene.add_child(plane)
	plane.owner = scene
	var cells: Array[Vector2i] = []
	for y in range(10):
		for x in range(12):
			var cell = Vector2i(x,y)
			layer.set_cell(cell,0,Vector2i(7,5))
			if x>1 and x<10 and y>1 and y<8 and not (x>=5 and x<=6 and y>=4 and y<=5):
				cells.append(cell)
	layer.set_cells_terrain_connect(cells,0,0,false)
	var packed = PackedScene.new()
	check(packed.pack(scene)==OK,"Cannot pack lake scene")
	check(ResourceSaver.save(packed,folder+"lake_review.tscn")==OK,"Cannot save lake scene")
	check(ResourceLoader.load(folder+"lake_review.tscn","",ResourceLoader.CACHE_MODE_IGNORE)!=null,"Lake scene reload failed")
	scene.free()
	return {"projection":projection,"masks":47,"frames":16,"periodSeconds":1.2,"visualApproval":false}

func run() -> void:
	var masks: Array[int] = []
	for mask in range(256):
		if normalize(mask)==mask:
			masks.append(mask)
	var frames = Image.load_from_file(LAKE+"lake_surface_frames.png")
	if frames == null:
		quit(1)
		return
	var results = [package(false,masks,frames),package(true,masks,frames)]
	var file = FileAccess.open("res://addons/beep_game_builder_cs/generated/test/library/output/lake_banks.json",FileAccess.WRITE)
	file.store_string(JSON.stringify({"status":"passed" if failures.is_empty() else "failed","results":results,"errors":failures},"  "))
	print("LAKE BANKS ","PASSED" if failures.is_empty() else "FAILED")
	quit(0 if failures.is_empty() else 1)
