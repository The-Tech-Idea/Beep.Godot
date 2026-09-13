extends "res://tools/terrain-library/package-depth-waterfall-route.gd"

const ISO_SOURCE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/isometric_granite_lake_v1/"
const ISO_LAKE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/lake_river_depth_v1/isometric/"
const ISO_DEPTH = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/lake_depth_v1/isometric/"

func run() -> void:
	DirAccess.make_dir_recursive_absolute(BASE+"isometric")
	var entries = []
	for direction in ["north_south","west_east"]:
		var ns = direction=="north_south"
		var source_path = ISO_SOURCE+direction+".tscn"
		var scene = load(source_path).instantiate()
		var reference = load(ISO_DEPTH+"lake_depth_review.tscn").instantiate()
		var layout = load(ISO_SOURCE+direction+"_lake_layout.tscn").instantiate()
		var old_layout: TileMapLayer = layout.get_node("Water")
		var lower_plane = scene.get_node("LowerSurfacePlane")
		var paths: Array[NodePath] = []
		for path in lower_plane.layer_paths:
			if not str(path).begins_with("../Lake_"): paths.append(path)
		lower_plane.layer_paths.assign(paths)
		var clock = scene.get_node("WaterClock")
		scene.remove_child(clock)
		clock.free()
		for child in scene.get_children():
			if child is AnimatedSprite2D:
				if str(child.name).begins_with("Lake_"):
					scene.remove_child(child)
					child.free()
				else: bind_native_clock(scene,str(child.name))
		var water = TileMapLayer.new()
		water.name = "LakeWater"
		water.z_index = 2
		water.tile_set = load(ISO_LAKE+"lake_river_depth.tres")
		water.material = reference.get_node("Water").material.duplicate()
		water.material.resource_local_to_scene = true
		var clock_error = load("res://tools/terrain-library/connected-water-clock.gd").enable(water)
		check(clock_error.is_empty(),clock_error)
		if not clock_error.is_empty(): quit(1); return
		water.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
		water.collision_enabled = false
		water.navigation_enabled = false
		for cell in old_layout.get_used_cells():
			var data = old_layout.get_cell_tile_data(cell)
			if data.terrain==0 or old_layout.get_cell_source_id(cell)>0:
				water.set_cell(cell,old_layout.get_cell_source_id(cell),old_layout.get_cell_atlas_coords(cell),0)
		scene.add_child(water)
		water.owner = scene
		# The native stem replaces its overlapping preview sprite at the same cell.
		var stem_sprite = scene.get_node("LowerRiver7")
		check(stem_sprite.position==water.map_to_local(Vector2i(1,7) if ns else Vector2i(7,1))-Vector2(32,16),"Native stem anchor differs")
		scene.remove_child(stem_sprite)
		stem_sprite.free()
		lower_plane.layer_paths.erase(NodePath("../LowerRiver7"))
		var depth = TileMapLayer.new()
		depth.name = "LakeDepth"
		depth.z_index = 2
		depth.tile_set = load(ISO_DEPTH+"lake_depth.tres")
		depth.material = reference.get_node("Depth").material.duplicate()
		depth.material.resource_local_to_scene = true
		depth.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
		depth.collision_enabled = false
		depth.navigation_enabled = false
		scene.add_child(depth)
		depth.owner = scene
		var shallow: Array[Vector2i] = []
		for along in range(8,13):
			for cross in range(-1,4):
				if along<10 or cross<1:
					shallow.append(Vector2i(cross,along) if ns else Vector2i(along,cross))
		depth.set_cells_terrain_connect(shallow,0,0,false)
		var binding = load("res://addons/beep_game_builder_cs/ecs/terrain/TerrainLakeDepthBinding.gd").new()
		binding.name = "LakeDepthBinding"
		binding.water_path = NodePath("../LakeWater")
		binding.depth_path = NodePath("../LakeDepth")
		scene.add_child(binding)
		binding.owner = scene
		lower_plane.layer_paths.append(NodePath("../LakeWater"))
		lower_plane.layer_paths.append(NodePath("../LakeDepth"))
		root.add_child(scene)
		check(scene.get_node("SurfacePlane").refresh().is_empty(),"Upper surface binding")
		check(lower_plane.refresh().is_empty(),"Lower surface binding")
		check(binding.refresh().is_empty(),"Native isometric depth: "+binding.last_error)
		check(water.get_used_cells().size()==26,"Expected 25 lake cells and one native stem")
		water.material.set_shader_parameter("depth_field_enabled",false)
		water.material.set_shader_parameter("depth_field_texture",null)
		water.material.set_shader_parameter("shore_field_texture",null)
		scene.set_meta("preview_uses_baked_lake",false)
		scene.set_meta("pending","Visual review, natural cliff contacts, wider routes, other banks, production approval")
		var packed = PackedScene.new()
		check(packed.pack(scene)==OK and ResourceSaver.save(packed,BASE+"isometric/"+direction+".tscn")==OK,"Save native isometric route")
		entries.append({"scene":direction+".tscn","source":source_path,"sourceSha256":FileAccess.get_sha256(source_path),"nativeLakeCells":26,"depthPainting":true})
		reference.free()
		layout.free()
		scene.free()
	var file = FileAccess.open(BASE+"isometric/manifest.json",FileAccess.WRITE)
	file.store_string(JSON.stringify({"status":"candidate","projection":"isometric","cases":entries,"visualRisePixels":64,"validationProbe":"tests/terrain_isometric_depth_route_probe.gd","approvalEvidence":null,"pending":["visual_review","wide_routes","natural_cliff_contacts","other_banks","production_integration"]},"  "))
	print("ISOMETRIC DEPTH ROUTE ","PASSED" if errors.is_empty() else "FAILED")
	quit(0 if errors.is_empty() else 1)
