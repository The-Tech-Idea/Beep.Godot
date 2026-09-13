extends "res://tools/terrain-library/package-depth-waterfall-route.gd"

const WIDE_LAKE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/lake_mouth_sections_v1/square/"
const FALL_FRAMES = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/waterfall_sections_v1/water_rise_64.tres"

func section(scene: Node2D,template: Sprite2D,label: String,position: Vector2,atlas: Texture2D,origin: Vector2,stride: Vector2,size: Vector2) -> void:
	var sprite = template.duplicate() as Sprite2D
	sprite.name = label
	sprite.position = position
	sprite.material = template.material.duplicate()
	var region = AtlasTexture.new()
	region.atlas = atlas
	region.region = Rect2(origin,size)
	sprite.texture = region
	for key in {"animation_atlas":atlas,"animation_origin":origin,"animation_stride":stride,"animation_size":size,"animation_atlas_size":atlas.get_size()}:
		var values = {"animation_atlas":atlas,"animation_origin":origin,"animation_stride":stride,"animation_size":size,"animation_atlas_size":atlas.get_size()}
		sprite.material.set_shader_parameter(key,values[key])
	scene.add_child(sprite)
	sprite.owner = scene
	if label.begins_with("River"): scene.get_node("SurfacePlane").layer_paths.append(NodePath("../"+label))

func run() -> void:
	var shared_path = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/shared_waterfall_v1/shared_curtain_16"
	var shared_texture = ImageTexture.create_from_image(Image.load_from_file(shared_path+".png"))
	check(ResourceSaver.save(shared_texture,shared_path+".res")==OK,"Save shared waterfall texture")
	shared_texture = load(shared_path+".res")
	var parameters = JSON.parse_string(FileAccess.get_file_as_string("res://addons/beep_game_builder_cs/generated/dev/cartoon/water/shared_waterfall_v1/motion_parameters.json"))
	var entries = []
	for width in [1,2,3,5,9]:
		var scene = load(BASE+"square/route.tscn").instantiate()
		var plane = scene.get_node("SurfacePlane")
		var water: TileMapLayer = scene.get_node("LowerLake")
		var depth: TileMapLayer = scene.get_node("LakeDepth")
		water.tile_set = load(WIDE_LAKE+"lake_mouth_sections.tres")
		water.material = water.material.duplicate()
		var clock_error = load("res://tools/terrain-library/connected-water-clock.gd").enable(water)
		check(clock_error.is_empty(),clock_error)
		if not clock_error.is_empty(): quit(1); return
		water.clear()
		depth.clear()
		var columns: int = width+4
		for name in ["UpperGround","LowerGround"]:
			var ground: Sprite2D = scene.get_node(name)
			ground.region_rect.size.x = columns*64
		# Repeat calibrated 64px wall regions, never scale the source rock pixels.
		for name in ["StaticCliff","CliffBottomContact"]:
			var original: Sprite2D = scene.get_node(name)
			for x in range(columns):
				var sprite = original.duplicate() as Sprite2D
				sprite.name = name+str(x)
				sprite.position.x = x*64
				var region = AtlasTexture.new()
				region.atlas = original.texture
				region.region = Rect2((x%5)*64,0,64,original.texture.get_height())
				sprite.texture = region
				if original.material: sprite.material = original.material.duplicate()
				scene.add_child(sprite)
				scene.move_child(sprite,original.get_index())
				sprite.owner = scene
				if name=="StaticCliff": plane.layer_paths.append(NodePath("../"+str(sprite.name)))
			plane.layer_paths.erase(NodePath("../"+name))
			scene.remove_child(original)
			original.free()
		var river: Sprite2D = scene.get_node("Upstream")
		var fall: Sprite2D = scene.get_node("WaterfallWaterOnly")
		var river_atlas = water.tile_set.get_source(2).texture as Texture2D
		var frames = load(FALL_FRAMES) as SpriteFrames
		var names = ["narrow","left","middle","right"]
		# Use the authored animation order to keep bank roles identical to river columns.
		var manifest = JSON.parse_string(FileAccess.get_file_as_string("res://addons/beep_game_builder_cs/generated/dev/cartoon/water/waterfall_sections_v1/manifest.json"))
		names.assign(manifest.sections)
		for x in range(width):
			var kind = 0 if width==1 else (1 if x==0 else (3 if x==width-1 else 2))
			for y in [-64,192]: section(scene,river,"River%d_%d"%[x,y],Vector2((x+2)*64,y),river_atlas,Vector2(kind*64,0),Vector2(0,256),Vector2(64,64))
			var first = frames.get_frame_texture(names[kind],0) as AtlasTexture
			var second = frames.get_frame_texture(names[kind],1) as AtlasTexture
			section(scene,fall,"Fall%d"%x,Vector2((x+2)*64,0),shared_texture,Vector2(kind*64,0),second.region.position-first.region.position,first.region.size)
			var m: ShaderMaterial = scene.get_node("Fall%d"%x).material
			m.set_shader_parameter("shared_waterfall_enabled",true)
			m.set_shader_parameter("waterfall_noise",load("res://addons/beep_game_builder_cs/generated/dev/cartoon/water/shared_waterfall_v1/motion_noise.res"))
			m.set_shader_parameter("waterfall_base_bytes",Vector3(parameters.baseColorBytes[0],parameters.baseColorBytes[1],parameters.baseColorBytes[2]))
			m.set_shader_parameter("waterfall_kind",kind)
			m.set_shader_parameter("waterfall_offset",float(x*64))
		for name in ["Upstream","Downstream","WaterfallWaterOnly"]:
			plane.layer_paths.erase(NodePath("../"+name))
			var old = scene.get_node(name)
			scene.remove_child(old)
			old.free()
		var cells: Array[Vector2i] = []
		for y in range(2,10):
			for x in range(columns):
				water.set_cell(Vector2i(x,y),0,Vector2i(7,5))
				if y>=5 and y<9 and x>=1 and x<columns-1: cells.append(Vector2i(x,y))
		for x in range(width): cells.append(Vector2i(x+2,4))
		water.set_cells_terrain_connect(cells,0,0,false)
		# Repeat middle mouth modules directly so wider routes need no new pattern asset.
		for x in range(width): water.set_cell(Vector2i(x+2,5),1 if width==1 else 3,Vector2i.ZERO if width==1 else Vector2i(0 if x==0 else (2 if x==width-1 else 1),0))
		for x in range(width): water.set_cell(Vector2i(x+2,4),2,Vector2i(0 if width==1 else (1 if x==0 else (3 if x==width-1 else 2)),0))
		var shallow: Array[Vector2i] = []
		for cell in cells:
			if cell.y>=5 and (cell.y==5 or cell.x==1): shallow.append(cell)
		depth.set_cells_terrain_connect(shallow,0,0,false)
		root.add_child(scene)
		check(plane.refresh().is_empty(),"Wide route surface")
		check(scene.get_node("LakeDepthBinding").refresh().is_empty(),"Wide route depth/flow")
		water.material.set_shader_parameter("depth_field_enabled",false)
		water.material.set_shader_parameter("depth_field_texture",null)
		water.material.set_shader_parameter("shore_field_texture",null)
		scene.set_meta("width_cells",width)
		scene.set_meta("pending","GPU join/phase review, cliff repeat refinement, natural contacts, production approval")
		var packed = PackedScene.new()
		var path = "square/width_"+str(width)+".tscn"
		check(packed.pack(scene)==OK and ResourceSaver.save(packed,BASE+path)==OK,"Save width route")
		entries.append({"width":width,"scene":path,"risePixels":64})
		scene.free()
	var output = FileAccess.open(BASE+"square/widths_manifest.json",FileAccess.WRITE)
	output.store_string(JSON.stringify({"status":"candidate","cases":entries,"approvalEvidence":null,"pending":["GPU_join_phase_review","cliff_repetition_refinement","isometric_widths","production_integration"]},"  "))
	print("WIDE DEPTH ROUTES ","PASSED" if errors.is_empty() else "FAILED")
	quit(0 if errors.is_empty() else 1)
