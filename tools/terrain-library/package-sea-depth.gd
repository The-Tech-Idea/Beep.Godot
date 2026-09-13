extends SceneTree

const BASE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/shared_sea_v1/"
const SEA = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/sea_banks_v1/"
var errors: Array[String] = []

func _initialize() -> void:
	call_deferred("run")

func check(ok: bool,message: String) -> void:
	if not ok:
		errors.append(message)
		push_error(message)

func run() -> void:
	for projection in ["square","isometric"]:
		var folder = BASE+projection+"/"
		var tiles: TileSet = load(SEA+projection+"/grass_sea.tres").duplicate(true)
		tiles.set_meta("terrain_library_role","sea_depth")
		tiles.set_terrain_name(0,0,"shallow_sea")
		var texture = ImageTexture.create_from_image(Image.load_from_file(folder+"depth_control_16.png"))
		check(ResourceSaver.save(texture,folder+"depth_control_16.res")==OK,"Save depth controls")
		var atlas: TileSetAtlasSource = tiles.get_source(0)
		atlas.texture = load(folder+"depth_control_16.res")
		for index in range(48):
			var coords = Vector2i(index%8,index/8)
			atlas.set_tile_animation_columns(coords,1)
			atlas.set_tile_animation_separation(coords,Vector2i(0,5))
			atlas.set_tile_animation_frames_count(coords,16)
			atlas.set_tile_animation_speed(coords,16.0/1.2)
		check(ResourceSaver.save(tiles,folder+"sea_depth.tres")==OK,"Save depth TileSet")
		var scene = load(folder+"sea_review.tscn").instantiate()
		root.add_child(scene)
		var water: TileMapLayer = scene.get_node("Water")
		var depth = TileMapLayer.new()
		depth.name = "Depth"
		depth.tile_set = load(folder+"sea_depth.tres")
		depth.material = water.material.duplicate()
		depth.material.resource_local_to_scene = true
		depth.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
		depth.collision_enabled = false
		depth.navigation_enabled = false
		depth.z_index = 1
		scene.add_child(depth)
		depth.owner = scene
		var plane = scene.get_node("SurfacePlane")
		plane.layer_paths.append(NodePath("../Depth"))
		var guard = load("res://addons/beep_game_builder_cs/ecs/terrain/TerrainWaterDepthGuard.gd").new()
		guard.name = "DepthGuard"
		guard.water_path = NodePath("../Water")
		guard.depth_path = NodePath("../Depth")
		scene.add_child(guard)
		guard.owner = scene
		var shallow: Array[Vector2i] = []
		# Interior shelf with a deep hole and a separate shallow patch.
		for y in range(0,8):
			for x in range(13,22):
				if x>=15 and x<=17 and y>=2 and y<=4: continue
				if x>=20 and y>=5: continue
				shallow.append(Vector2i(x,y))
		for y in range(11,14):
			for x in range(14,17): shallow.append(Vector2i(x,y))
		depth.set_cells_terrain_connect(shallow,0,0,false)
		check(guard.refresh().is_empty(),"Invalid depth showcase: "+guard.last_error)
		scene.name = "SeaDepthCandidate"
		scene.set_meta("pending","Visual depth approval, authored coast/depth and river/depth connectors, other materials")
		var packed = PackedScene.new()
		check(packed.pack(scene)==OK,"Pack depth showcase")
		check(ResourceSaver.save(packed,folder+"depth_review.tscn")==OK,"Save depth showcase")
		scene.free()
	print("SEA DEPTH ","PASSED" if errors.is_empty() else "FAILED")
	quit(0 if errors.is_empty() else 1)
