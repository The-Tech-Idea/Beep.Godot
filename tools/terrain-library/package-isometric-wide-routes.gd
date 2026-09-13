extends "res://tools/terrain-library/package-depth-waterfall-route.gd"

const ISO_WIDE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/lake_mouth_sections_v1/isometric/lake_mouth_sections.tres"
const SHARED = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/shared_waterfall_v1/"

func cell(ns: bool,cross: int,along: int) -> Vector2i:
	return Vector2i(cross,along) if ns else Vector2i(along,cross)

func sprite_section(scene: Node2D,template: Sprite2D,label: String,position: Vector2,texture: AtlasTexture,stride: Vector2,plane: Node) -> Sprite2D:
	var sprite = template.duplicate() as Sprite2D
	sprite.name = label
	sprite.position = position
	sprite.texture = texture
	sprite.material = template.material.duplicate()
	var values = {"animation_atlas":texture.atlas,"animation_origin":texture.region.position,"animation_stride":stride,"animation_size":texture.region.size,"animation_atlas_size":texture.atlas.get_size()}
	for key in values: sprite.material.set_shader_parameter(key,values[key])
	scene.add_child(sprite)
	sprite.owner = scene
	if plane: plane.layer_paths.append(NodePath("../"+label))
	return sprite

func run() -> void:
	var records = []
	var parameters = JSON.parse_string(FileAccess.get_file_as_string(SHARED+"motion_parameters.json"))
	var manifest = JSON.parse_string(FileAccess.get_file_as_string(SHARED+"projections_manifest.json"))
	var frames_path = ""
	for entry in manifest.exports:
		if entry.projection=="isometric" and entry.risePixels==64: frames_path=SHARED+entry.spriteFrames
	check(not frames_path.is_empty() and ResourceLoader.exists(frames_path),"Missing manifest-declared isometric waterfall frames")
	if frames_path.is_empty() or not ResourceLoader.exists(frames_path): quit(1); return
	for ns in [true,false]:
		var direction = "north_south" if ns else "west_east"
		for width in [1,2,3,5,9]:
			var scene = load(BASE+"isometric/"+direction+".tscn").instantiate()
			var top: TileMapLayer = scene.get_node("PlateauSurface")
			var ground: TileMapLayer = scene.get_node("LowerGround")
			var water: TileMapLayer = scene.get_node("LakeWater")
			var depth: TileMapLayer = scene.get_node("LakeDepth")
			water.tile_set = load(ISO_WIDE)
			water.material = water.material.duplicate()
			var error = load("res://tools/terrain-library/connected-water-clock.gd").enable(water)
			check(error.is_empty(),error)
			if not error.is_empty(): quit(1); return
			for layer in [top,ground,water,depth]: layer.clear()
			for along in range(4):
				for cross in range(width+2): top.set_cell(cell(ns,cross,along),0,Vector2i(6,5))
			for along in range(4,14):
				for cross in range(-2,width+3): ground.set_cell(cell(ns,cross,along),0,Vector2i(6,5))
			var light: TileMapLayer = scene.get_node("LightFace")
			var shade: TileMapLayer = scene.get_node("ShadeFace")
			light.clear()
			shade.clear()
			var size = Vector2i(width+2,4) if ns else Vector2i(4,width+2)
			for x in range(size.x): light.set_cell(Vector2i(x,size.y-1),x%4,Vector2i.ZERO)
			for y in range(size.y): shade.set_cell(Vector2i(size.x-1,y),7-y%4,Vector2i.ZERO)
			scene.set_meta("dimensions_cells",size)
			var upper_plane = scene.get_node("SurfacePlane")
			var lower_plane = scene.get_node("LowerSurfacePlane")
			upper_plane.layer_paths.assign([NodePath("../CliffWalls"),NodePath("../PlateauSurface")])
			lower_plane.layer_paths.assign([NodePath("../LowerGround"),NodePath("../LakeWater"),NodePath("../LakeDepth")])
			var river_template: Sprite2D = scene.get_node("UpperRiver0")
			var fall_template: Sprite2D = scene.get_node("Waterfall")
			var source: TileSetAtlasSource = water.tile_set.get_source(2)
			var frames = load(frames_path) as SpriteFrames
			for section in range(width):
				var kind = 0 if width==1 else (1 if section==0 else (3 if section==width-1 else 2))
				var kind_name = ["narrow","low_bank","middle","high_bank"][kind]
				var river = AtlasTexture.new()
				river.atlas = source.texture
				river.region = Rect2(kind*64,0 if ns else 64,64,32)
				for along in range(7):
					var position = top.map_to_local(cell(ns,section+1,along))-Vector2(32,16)-Vector2(0,64 if along<4 else 0)
					sprite_section(scene,river_template,"Flow_%d_%d"%[section,along],position,river,Vector2(0,128),upper_plane if along<4 else lower_plane)
				var first = frames.get_frame_texture(direction+"."+kind_name+".offset_0",0) as AtlasTexture
				var second = frames.get_frame_texture(direction+"."+kind_name+".offset_0",1) as AtlasTexture
				var anchor = Vector2(64 if ns else 32,16)
				var position = top.map_to_local(cell(ns,section+1,3))-Vector2(0,64)-anchor
				var fall = sprite_section(scene,fall_template,"Fall_%d"%section,position,first,second.region.position-first.region.position,null)
				var m: ShaderMaterial = fall.material
				m.set_shader_parameter("shared_waterfall_enabled",true)
				m.set_shader_parameter("waterfall_noise",load(SHARED+"motion_noise.res"))
				m.set_shader_parameter("waterfall_base_bytes",Vector3(parameters.baseColorBytes[0],parameters.baseColorBytes[1],parameters.baseColorBytes[2]))
				m.set_shader_parameter("waterfall_kind",kind)
				m.set_shader_parameter("waterfall_offset",float(section*64))
				m.set_shader_parameter("waterfall_projection",1 if ns else 2)
			for child in scene.get_children():
				if child is Sprite2D and (str(child.name).begins_with("UpperRiver") or str(child.name).begins_with("LowerRiver") or child.name=="Waterfall"):
					scene.remove_child(child)
					child.free()
			var cells: Array[Vector2i] = []
			for along in range(8,13):
				for cross in range(0,width+2): cells.append(cell(ns,cross,along))
			for section in range(width): cells.append(cell(ns,section+1,7))
			water.set_cells_terrain_connect(cells,0,0,false)
			for section in range(width):
				var kind = 0 if width==1 else (1 if section==0 else (3 if section==width-1 else 2))
				water.set_cell(cell(ns,section+1,7),2,Vector2i(kind,0 if ns else 2))
				water.set_cell(cell(ns,section+1,8),1 if width==1 else 3,Vector2i(0 if ns else 6,0) if width==1 else Vector2i(kind-1,0 if ns else 6))
			var shallow: Array[Vector2i] = []
			for c in cells:
				var along = c.y if ns else c.x
				var cross = c.x if ns else c.y
				if along>=8 and (along<10 or cross==0): shallow.append(c)
			depth.set_cells_terrain_connect(shallow,0,0,false)
			root.add_child(scene)
			check(upper_plane.refresh().is_empty() and lower_plane.refresh().is_empty(),"Isometric width surfaces")
			check(scene.get_node("LakeDepthBinding").refresh().is_empty(),"Isometric width lake contacts")
			water.material.set_shader_parameter("depth_field_enabled",false)
			water.material.set_shader_parameter("depth_field_texture",null)
			water.material.set_shader_parameter("shore_field_texture",null)
			scene.set_meta("width_cells",width)
			scene.set_meta("pending","GPU full-route review, cliff repetition and contacts, production approval")
			var path = "isometric/"+direction+"_width_"+str(width)+".tscn"
			var packed = PackedScene.new()
			check(packed.pack(scene)==OK and ResourceSaver.save(packed,BASE+path)==OK,"Save isometric width route")
			records.append({"direction":direction,"width":width,"scene":path,"risePixels":64})
			scene.free()
	var file = FileAccess.open(BASE+"isometric/widths_manifest.json",FileAccess.WRITE)
	file.store_string(JSON.stringify({"status":"candidate","cases":records,"approvalEvidence":null,"pending":["GPU_full_route_review","cliff_repetition_contacts","performance","production_integration"]},"  "))
	print("ISOMETRIC WIDE ROUTES ","PASSED" if errors.is_empty() else "FAILED")
	quit(0 if errors.is_empty() else 1)
