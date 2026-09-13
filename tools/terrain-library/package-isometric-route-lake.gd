extends SceneTree

const BASE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/isometric_granite_lake_v1/"
const ROUTE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/isometric_granite_route_v1/"
const CONNECTORS = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/river_lake_connectors_v1/"

func _initialize() -> void:
	DirAccess.make_dir_recursive_absolute(BASE+"runtime")
	var errors = []
	var entries = []
	var connector_manifest = JSON.parse_string(FileAccess.get_file_as_string(CONNECTORS+"manifest.json"))
	var route_manifest = JSON.parse_string(FileAccess.get_file_as_string(ROUTE+"manifest.json"))
	var saved_frames = {}
	for entry in route_manifest.cases:
		var ns = entry.direction=="north_south"
		var scene = load(ROUTE+entry.scene).instantiate()
		scene.name = "IsometricWaterfallLakeRoute"
		var reference = load(CONNECTORS+"isometric/river_lake_review.tscn").instantiate()
		var layout: TileMapLayer = reference.get_node("Water")
		layout.clear()
		var ground: TileMapLayer = scene.get_node("LowerGround")
		ground.owner = scene
		for along in range(7,14):
			for cross in range(-2,5):
				var cell = Vector2i(cross,along) if ns else Vector2i(along,cross)
				layout.set_cell(cell,0,Vector2i(7,5),0)
				ground.set_cell(cell,0,Vector2i(6,5),0)
		var cells: Array[Vector2i] = []
		for along in range(8,13):
			for cross in range(-1,4): cells.append(Vector2i(cross,along) if ns else Vector2i(along,cross))
		var stem = Vector2i(1,7) if ns else Vector2i(7,1)
		var connected = cells.duplicate()
		connected.append(stem)
		layout.set_cells_terrain_connect(connected,0,0,false)
		var mouth = Vector2i(1,8) if ns else Vector2i(8,1)
		var profile_id = "north_inlet" if ns else "west_inlet"
		var mouth_coords = Vector2i(-1,-1)
		for profile in connector_manifest.profiles:
			if profile.id==profile_id: mouth_coords = Vector2i(profile.atlas[0],profile.atlas[1])
		if mouth_coords.x<0: errors.append("Missing explicit lake inlet")
		layout.set_cell(mouth,1,mouth_coords,0)
		layout.set_cell(stem,2,Vector2i(0,0 if ns else 2),0)
		# Keep the native paintable layout as the authoring source for the synchronized preview.
		var blueprint = PackedScene.new()
		var result = blueprint.pack(reference)
		if result==OK: result = ResourceSaver.save(blueprint,BASE+str(entry.direction)+"_lake_layout.tscn")
		if result!=OK: errors.append("Lake authoring layout save failed")
		var player: AnimationPlayer = scene.get_node("WaterClock")
		var library = player.get_animation_library("").duplicate(true)
		player.remove_animation_library("")
		player.add_animation_library("",library)
		player.owner = scene
		var animation = library.get_animation("flow")
		var plane = scene.get_node("LowerSurfacePlane")
		plane.owner = scene
		var cell_records = []
		for cell in cells:
			var source_id = layout.get_cell_source_id(cell)
			var coords = layout.get_cell_atlas_coords(cell)
			var atlas: TileSetAtlasSource = layout.tile_set.get_source(source_id)
			if layout.get_cell_tile_data(cell)==null: errors.append("Missing lake tile"); continue
			var key = "%d_%d_%d" % [source_id,coords.x,coords.y]
			var frame_file = BASE+"runtime/lake_"+key+".tres"
			if not saved_frames.has(key):
				var frames = SpriteFrames.new()
				frames.remove_animation("default")
				frames.add_animation("lake")
				frames.set_animation_speed("lake",16.0/1.2)
				for frame in range(16):
					var region = AtlasTexture.new()
					region.atlas = atlas.texture
					region.region = Rect2(coords.x*64,(coords.y+frame*(6 if source_id==0 else 1))*32,64,32)
					region.filter_clip = true
					frames.add_frame("lake",region)
				if ResourceSaver.save(frames,frame_file)!=OK: errors.append("Lake frames save failed")
				saved_frames[key] = true
			var sprite = AnimatedSprite2D.new()
			sprite.name = "Lake_%d_%d" % [cell.x,cell.y]
			sprite.sprite_frames = load(frame_file)
			sprite.animation = "lake"
			sprite.centered = false
			sprite.z_index = 2
			sprite.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
			sprite.material = layout.material.duplicate()
			sprite.position = layout.map_to_local(cell)-Vector2(32,16)
			scene.add_child(sprite)
			sprite.owner = scene
			plane.layer_paths.append(NodePath("../"+str(sprite.name)))
			var track = animation.add_track(Animation.TYPE_VALUE)
			animation.track_set_path(track,NodePath(str(sprite.name)+":frame"))
			animation.value_track_set_update_mode(track,Animation.UPDATE_DISCRETE)
			for frame in range(16): animation.track_insert_key(track,frame*1.2/16,frame)
			cell_records.append({"cell":[cell.x,cell.y],"sourceId":source_id,"atlas":[coords.x,coords.y],"sprite":str(sprite.name)})
		scene.set_meta("missing_artwork","Natural cliff bank contacts, foam approval, sea continuation, wide river mouths")
		var packed = PackedScene.new()
		result = packed.pack(scene)
		if result==OK: result = ResourceSaver.save(packed,BASE+entry.scene)
		if result!=OK: errors.append("Lake route save failed")
		var record = entry.duplicate(true)
		record.lakeCells = cell_records
		record.lakeInletCell = [mouth.x,mouth.y]
		record.lakeInletProfile = profile_id
		record.authoringLayout = str(entry.direction)+"_lake_layout.tscn"
		entries.append(record)
		reference.free()
		scene.free()
	var manifest = FileAccess.open(BASE+"manifest.json",FileAccess.WRITE)
	manifest.store_string(JSON.stringify({"schemaVersion":1,"status":"candidate","style":"cartoon","projection":"isometric","cases":entries,"sourceRoute":ROUTE+"manifest.json","sourceConnectors":CONNECTORS+"manifest.json","frames":16,"periodSeconds":1.2,"clock":"shared_AnimationPlayer","nativeLayoutRetained":true,"previewUsesBakedLayout":true,"layoutEditSynchronization":"pending","productionReady":false,"approvalEvidence":null,"pending":["Visual approval","Natural cliff contacts","Foam refinement","Sea continuation","Wide river mouths","Engine pack bindings"]},"  "))
	print("ISOMETRIC GRANITE LAKE ROUTE ","PASSED" if errors.is_empty() else "FAILED")
	quit(0 if errors.is_empty() else 1)
