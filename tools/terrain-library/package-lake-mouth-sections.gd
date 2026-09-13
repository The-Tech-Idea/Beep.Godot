extends SceneTree

const BASE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/lake_mouth_sections_v1/"
const SOURCE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/lake_river_depth_v1/"
const PORTS = ["north","east","south","west"]
const FLOW_ROWS = [[0,1],[3,2],[1,0],[2,3]]
var errors: Array[String] = []

func _initialize() -> void:
	call_deferred("run")

func check(ok: bool,message: String) -> void:
	if not ok:
		errors.append(message)
		push_error(message)

func route_cell(port: int,cross: int,along: int) -> Vector2i:
	if port==1: return Vector2i(9-along,cross)
	if port==2: return Vector2i(cross,9-along)
	if port==3: return Vector2i(along,cross)
	return Vector2i(cross,along)

func run() -> void:
	var manifest = JSON.parse_string(FileAccess.get_file_as_string(BASE+"manifest.json"))
	var scenes: Array = []
	for projection in ["square","isometric"]:
		var folder = BASE+projection+"/"
		var tiles = load(SOURCE+projection+"/lake_river_depth.tres").duplicate(true) as TileSet
		tiles.set_meta("lake_depth_wide_contacts_v1",true)
		var texture = ImageTexture.create_from_image(Image.load_from_file(folder+"lake_mouth_sections_16.png"))
		check(ResourceSaver.save(texture,folder+"lake_mouth_sections_16.res")==OK,"Save wide lake contact texture")
		var atlas = TileSetAtlasSource.new()
		atlas.texture = load(folder+"lake_mouth_sections_16.res")
		atlas.texture_region_size = tiles.tile_size
		tiles.add_source(atlas,3)
		for profile in manifest.profiles:
			var coords = Vector2i(profile.atlas[0],profile.atlas[1])
			atlas.create_tile(coords)
			atlas.set_tile_animation_columns(coords,1)
			atlas.set_tile_animation_separation(coords,Vector2i(0,7))
			atlas.set_tile_animation_frames_count(coords,16)
			atlas.set_tile_animation_speed(coords,16.0/1.2)
			atlas.get_tile_data(coords,0).set_custom_data("flow_profile",profile.id)
		for index in range(8):
			var port: int = index/2
			var name = PORTS[port]+("_inlet" if index%2==0 else "_outlet")
			for width in [1,2,3,5]:
				var pattern = TileMapPattern.new()
				for i in range(width):
					var cell = Vector2i(i,0) if port%2==0 else Vector2i(0,i)
					pattern.set_cell(cell,1 if width==1 else 3,Vector2i(index,0) if width==1 else Vector2i(0 if i==0 else (2 if i==width-1 else 1),index),0)
				check(ResourceSaver.save(pattern,folder+name+"_"+str(width)+".tres")==OK,"Save wide lake pattern")
				if width>1: tiles.add_pattern(pattern)
		check(ResourceSaver.save(tiles,folder+"lake_mouth_sections.tres")==OK,"Save wide lake native tiles")
		for index in range(8):
			var port: int = index/2
			var name = PORTS[port]+("_inlet" if index%2==0 else "_outlet")
			var scene = load(SOURCE+projection+"/"+name+".tscn").instantiate()
			var water: TileMapLayer = scene.get_node("Water")
			var depth: TileMapLayer = scene.get_node("Depth")
			water.tile_set = load(folder+"lake_mouth_sections.tres")
			water.clear()
			depth.clear()
			var cells: Array[Vector2i] = []
			for cross in range(-3,22):
				for along in range(-1,16):
					var cell = route_cell(port,cross,along)
					water.set_cell(cell,0,Vector2i(7,5),0)
					if cross>=-2 and cross<=20 and along>=4 and along<14: cells.append(cell)
			var cursor = 1
			for width in [1,2,3,5]:
				for i in range(width):
					for along in range(4): cells.append(route_cell(port,cursor+i,along))
				cursor += width+2
			water.set_cells_terrain_connect(cells,0,0,false)
			cursor = 1
			for width in [1,2,3,5]:
				water.set_pattern(route_cell(port,cursor,4),load(folder+name+"_"+str(width)+".tres"))
				for i in range(width):
					var kind = 0 if width==1 else (1 if i==0 else (3 if i==width-1 else 2))
					for along in range(4): water.set_cell(route_cell(port,cursor+i,along),2,Vector2i(kind,FLOW_ROWS[port][index%2]),0)
				cursor += width+2
			var shallow: Array[Vector2i] = []
			for cross in range(-2,21):
				for along in range(4,11):
					if along<7 or cross<8: shallow.append(route_cell(port,cross,along))
			depth.set_cells_terrain_connect(shallow,0,0,false)
			root.add_child(scene)
			check(scene.get_node("SurfacePlane").refresh().is_empty(),"Wide lake surface failed")
			var binding = scene.get_node("CoastalDepthBinding")
			check(binding.refresh().is_empty(),"Wide lake rejected: "+binding.last_error)
			water.material.set_shader_parameter("depth_field_enabled",false)
			water.material.set_shader_parameter("depth_field_texture",null)
			water.material.set_shader_parameter("shore_field_texture",null)
			scene.name = "LakeMouthWidthCandidate"
			scene.set_meta("pending","Visual approval, sand/rock banks, production integration and large-map cost")
			var packed = PackedScene.new()
			var relative = projection+"/widths_"+name+".tscn"
			check(packed.pack(scene)==OK and ResourceSaver.save(packed,BASE+relative)==OK,"Save wide lake scene")
			scenes.append(relative)
			scene.free()
	if errors.is_empty():
		manifest.godotScenes = scenes
		manifest.patternsPerProjection = 32
		manifest.depthSupported = true
		manifest.pending = ["visual_approval","sand_rock_banks","large_map_cost","production_integration"]
		var file = FileAccess.open(BASE+"manifest.json",FileAccess.WRITE)
		file.store_string(JSON.stringify(manifest,"  "))
	print("LAKE MOUTH SECTIONS ","PASSED" if errors.is_empty() else "FAILED")
	quit(0 if errors.is_empty() else 1)
