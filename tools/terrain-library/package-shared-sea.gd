extends SceneTree

const BASE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/shared_sea_v1/"
const SEA = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/sea_banks_v1/"
const INLETS = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/river_sea_connectors_v1/"
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
		var tiles: TileSet = load(INLETS+projection+"/river_sea.tres").duplicate(true)
		tiles.set_meta("terrain_library_role","sea_surface")
		for index in range(2):
			var name = "sea" if index==0 else "inlets"
			var texture = ImageTexture.create_from_image(Image.load_from_file(folder+name+"_control_16.png"))
			check(ResourceSaver.save(texture,folder+name+"_control_16.res")==OK,"Save control texture")
			var atlas: TileSetAtlasSource = tiles.get_source(index)
			atlas.texture = load(folder+name+"_control_16.res")
		var wide_texture = ImageTexture.create_from_image(Image.load_from_file(folder+"wide_inlets_control_16.png"))
		check(ResourceSaver.save(wide_texture,folder+"wide_inlets_control_16.res")==OK,"Save width controls")
		var wide = TileSetAtlasSource.new()
		wide.texture = load(folder+"wide_inlets_control_16.res")
		wide.texture_region_size = tiles.tile_size
		tiles.add_source(wide,3)
		for entry in manifest.widthModules:
			var coords = Vector2i(entry.atlas[0],entry.atlas[1])
			wide.create_tile(coords)
			wide.set_tile_animation_columns(coords,1)
			wide.set_tile_animation_separation(coords,Vector2i(0,3))
			wide.set_tile_animation_frames_count(coords,16)
			wide.set_tile_animation_speed(coords,16.0/1.2)
			wide.get_tile_data(coords,0).set_custom_data("flow_profile",entry.id)
		for port in range(4):
			for width in [1,2,3,5]:
				var pattern = TileMapPattern.new()
				for index in range(width):
					var cell = Vector2i(index,0) if port%2==0 else Vector2i(0,index)
					var source = 1 if width==1 else 3
					var coords = Vector2i(port,0) if width==1 else Vector2i(0 if index==0 else (2 if index==width-1 else 1),port)
					pattern.set_cell(cell,source,coords,0)
				tiles.add_pattern(pattern)
				check(ResourceSaver.save(pattern,folder+"mouth_"+str(manifest.profiles[port].port)+"_"+str(width)+".tres")==OK,"Save mouth pattern")
		check(ResourceSaver.save(tiles,folder+"shared_sea.tres")==OK,"Save shared sea tiles")
		for name in ["sea_review","river_sea_review"]:
			var original = SEA if name=="sea_review" else INLETS
			var scene = load(original+projection+"/"+name+".tscn").instantiate()
			var layer: TileMapLayer = scene.get_node("Water")
			var previous: ShaderMaterial = layer.material
			var material = ShaderMaterial.new()
			material.shader = load("res://addons/beep_game_builder_cs/shaders/terrain_shared_sea.gdshader")
			material.set_shader_parameter("secondary_texture",previous.get_shader_parameter("secondary_texture"))
			material.set_shader_parameter("water_base",Vector3(manifest.waterBase[0],manifest.waterBase[1],manifest.waterBase[2])/255.0)
			material.set_shader_parameter("cell_bias",Vector2(0.5,0.5) if projection=="isometric" else Vector2.ZERO)
			material.resource_local_to_scene = true
			layer.material = material
			layer.tile_set = load(folder+"shared_sea.tres")
			scene.name = "SharedSeaSurfaceCandidate"
			scene.set_meta("production_ready",false)
			scene.set_meta("pending","Visual review, wide mouths, depth transitions, other bank materials")
			var packed = PackedScene.new()
			check(packed.pack(scene)==OK,"Pack shared sea scene")
			check(ResourceSaver.save(packed,folder+name+".tscn")==OK,"Save shared sea scene")
			scene.free()
		package_width_scenes(folder,manifest)
	print("SHARED SEA ","PASSED" if errors.is_empty() else "FAILED")
	quit(0 if errors.is_empty() else 1)

func route_cell(port: int,cross: int,along: int) -> Vector2i:
	if port==1: return Vector2i(9-along,cross)
	if port==2: return Vector2i(cross,9-along)
	if port==3: return Vector2i(along,cross)
	return Vector2i(cross,along)

func package_width_scenes(folder: String,manifest: Dictionary) -> void:
	for port in range(4):
		var scene = load(folder+"river_sea_review.tscn").instantiate()
		var layer: TileMapLayer = scene.get_node("Water")
		layer.clear()
		var water: Array[Vector2i] = []
		for cross in range(-2,21):
			for along in range(-1,14):
				var cell = route_cell(port,cross,along)
				layer.set_cell(cell,0,Vector2i(7,5),0)
				if along>=4: water.append(cell)
		var cursor = 1
		for width in [1,2,3,5]:
			for index in range(width):
				for along in range(4): water.append(route_cell(port,cursor+index,along))
			cursor += width+2
		layer.set_cells_terrain_connect(water,0,0,false)
		cursor = 1
		for width in [1,2,3,5]:
			var pattern: TileMapPattern = load(folder+"mouth_"+str(manifest.profiles[port].port)+"_"+str(width)+".tres")
			layer.set_pattern(route_cell(port,cursor,4),pattern)
			for index in range(width):
				var column = 0 if width==1 else (1 if index==0 else (3 if index==width-1 else 2))
				for along in range(4): layer.set_cell(route_cell(port,cursor+index,along),2,Vector2i(column,[0,3,1,2][port]),0)
			cursor += width+2
		for cell in layer.get_used_cells(): check(layer.get_cell_tile_data(cell)!=null,"Missing width scene cell")
		scene.name = "SeaMouthWidths_"+str(manifest.profiles[port].port)
		scene.set_meta("pending","Visual approval, other banks, depth boundaries, engine pack integration")
		var packed = PackedScene.new()
		check(packed.pack(scene)==OK,"Pack mouth widths")
		check(ResourceSaver.save(packed,folder+"mouth_widths_"+str(manifest.profiles[port].port)+".tscn")==OK,"Save mouth width scene")
		scene.free()
