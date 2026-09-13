extends SceneTree

const LAKE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/lake_depth_v1/"
const CONTACTS = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/river_lake_connectors_v1/"
const BASE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/lake_river_depth_v1/"
const PORTS = [Vector2i.UP,Vector2i.RIGHT,Vector2i.DOWN,Vector2i.LEFT]
const MOUTHS = [Vector2i(6,3),Vector2i(9,6),Vector2i(6,9),Vector2i(3,6)]
const FLOW_ROWS = [[0,1],[3,2],[1,0],[2,3]]
var errors: Array[String] = []

func _initialize() -> void:
	call_deferred("run")

func check(ok: bool,message: String) -> void:
	if not ok:
		errors.append(message)
		push_error(message)

func run() -> void:
	var profiles = JSON.parse_string(FileAccess.get_file_as_string(CONTACTS+"manifest.json")).profiles
	var scenes: Array = []
	for projection in ["square","isometric"]:
		var folder = BASE+projection+"/"
		DirAccess.make_dir_recursive_absolute(folder)
		var tiles = load(LAKE+projection+"/lake_surface.tres").duplicate(true) as TileSet
		tiles.set_meta("lake_depth_river_contacts_v1",true)
		tiles.add_custom_data_layer()
		tiles.set_custom_data_layer_name(0,"flow_profile")
		tiles.set_custom_data_layer_type(0,TYPE_STRING)
		var source = load(CONTACTS+projection+"/river_lake.tres") as TileSet
		for id in [1,2]:
			var atlas = source.get_source(id).duplicate(true) as TileSetAtlasSource
			var texture = ImageTexture.create_from_image(atlas.texture.get_image())
			var path = folder+("lake_contacts_16.res" if id==1 else "river_sections_16.res")
			check(ResourceSaver.save(texture,path)==OK,"Save unchanged contact/river frames")
			atlas.texture = load(path)
			tiles.add_source(atlas,id)
		for index in range(8):
			var port: int = index/2
			var step: Vector2i = PORTS[port]
			var first = Vector2i(mini(0,step.x*3),mini(0,step.y*3))
			var pattern = TileMapPattern.new()
			pattern.set_cell(-first,1,Vector2i(index,0),0)
			for i in range(1,4): pattern.set_cell(step*i-first,2,Vector2i(0,FLOW_ROWS[port][index%2]),0)
			tiles.add_pattern(pattern)
		check(ResourceSaver.save(tiles,folder+"lake_river_depth.tres")==OK,"Save native contact TileSet")
		for index in range(8):
			var scene = load(LAKE+projection+"/lake_depth_review.tscn").instantiate()
			var water: TileMapLayer = scene.get_node("Water")
			var depth: TileMapLayer = scene.get_node("Depth")
			water.tile_set = load(folder+"lake_river_depth.tres")
			water.clear()
			depth.clear()
			for y in range(13):
				for x in range(13): water.set_cell(Vector2i(x,y),0,Vector2i(7,5),0)
			var cells: Array[Vector2i] = []
			for y in range(3,10):
				for x in range(3,10): cells.append(Vector2i(x,y))
			var port: int = index/2
			var mouth: Vector2i = MOUTHS[port]
			var step: Vector2i = PORTS[port]
			cells.append(mouth+step)
			water.set_cells_terrain_connect(cells,0,0,false)
			water.set_cell(mouth,1,Vector2i(index,0),0)
			for i in range(1,4): water.set_cell(mouth+step*i,2,Vector2i(0,FLOW_ROWS[port][index%2]),0)
			var shallow: Array[Vector2i] = []
			for y in range(3,10):
				for x in range(3,10):
					if x<=5 or y<=5: shallow.append(Vector2i(x,y))
			if not shallow.has(mouth): shallow.append(mouth)
			depth.set_cells_terrain_connect(shallow,0,0,false)
			root.add_child(scene)
			check(scene.get_node("SurfacePlane").refresh().is_empty(),"Contact surface failed")
			var binding = scene.get_node("CoastalDepthBinding")
			check(binding.refresh().is_empty(),"Contact rejected: "+binding.last_error)
			water.material.set_shader_parameter("depth_field_enabled",false)
			water.material.set_shader_parameter("depth_field_texture",null)
			water.material.set_shader_parameter("shore_field_texture",null)
			scene.name = "LakeRiverDepthCandidate"
			scene.set_meta("flow_profile",profiles[index].id)
			scene.set_meta("pending","Visual approval, wider contacts, other banks, sea-depth contacts and production integration")
			var packed = PackedScene.new()
			var relative = projection+"/"+profiles[index].id+".tscn"
			check(packed.pack(scene)==OK and ResourceSaver.save(packed,BASE+relative)==OK,"Save river/lake depth scene")
			scenes.append(relative)
			scene.free()
	var manifest = {"schemaVersion":1,"status":"technical_candidate","style":"cartoon","projections":["square","isometric"],"profiles":profiles,"scenes":scenes,"openingWidthCells":1,"patternsPerProjection":8,"frames":16,"periodSeconds":1.2,"depthBlendPixels":[0.5,8.0],"sourceManifest":CONTACTS+"manifest.json","sourceManifestSha256":FileAccess.get_sha256(CONTACTS+"manifest.json"),"approvalEvidence":null,"pending":["visual_approval","wide_contacts","sand_rock_banks","sea_depth_contacts","large_map_cost","production_integration"]}
	var file = FileAccess.open(BASE+"manifest.json",FileAccess.WRITE)
	file.store_string(JSON.stringify(manifest,"  "))
	print("LAKE RIVER DEPTH PACKAGE ","PASSED" if errors.is_empty() else "FAILED")
	quit(0 if errors.is_empty() else 1)
