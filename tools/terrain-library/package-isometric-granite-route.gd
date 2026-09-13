extends SceneTree

const BASE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/isometric_granite_route_v1/"
const CLIFF = "res://addons/beep_game_builder_cs/generated/dev/cartoon/elevation/staging/isometric_granite_faces_v1/native_faces_review.tscn"
const RIVER = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/river_sections_v1/isometric/river_widths_review.tscn"
const FALL = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/isometric_waterfall_v1/fall_64.tres"
const PLANE = "res://addons/beep_game_builder_cs/ecs/terrain/TerrainSurfacePlane.gd"

func _initialize() -> void:
	DirAccess.make_dir_recursive_absolute(BASE)
	var errors = []
	var entries = []
	for direction in ["north_south","west_east"]:
		var ns = direction=="north_south"
		var scene = load(CLIFF).instantiate()
		scene.name = "IsometricRiverCliffRoute"
		var top: TileMapLayer = scene.get_node("PlateauSurface")
		var reference = load(RIVER).instantiate()
		var river_layer: TileMapLayer = reference.get_node("Water")
		var atlas: TileSetAtlasSource = river_layer.tile_set.get_source(0)
		var frames = SpriteFrames.new()
		frames.remove_animation("default")
		frames.add_animation("flow")
		frames.set_animation_speed("flow",16.0/1.2)
		for frame in range(16):
			var region = AtlasTexture.new()
			region.atlas = atlas.texture
			region.region = Rect2(0,frame*128+(0 if ns else 64),64,32)
			region.filter_clip = true
			frames.add_frame("flow",region)
		var frame_file = BASE+direction+"_river.tres"
		if ResourceSaver.save(frames,frame_file)!=OK: errors.append("River frames save failed")
		var ground = TileMapLayer.new()
		ground.name = "LowerGround"
		ground.tile_set = top.tile_set
		ground.material = top.material.duplicate()
		ground.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
		ground.z_index = -1
		for along in range(4,8):
			for cross in range(4): ground.set_cell(Vector2i(cross,along) if ns else Vector2i(along,cross),0,Vector2i(6,5),0)
		scene.add_child(ground)
		ground.owner = scene
		var lower_plane = load(PLANE).new()
		lower_plane.name = "LowerSurfacePlane"
		lower_plane.projection = 1
		lower_plane.cell_size = Vector2(64,32)
		lower_plane.layer_paths.assign([NodePath("../LowerGround")])
		scene.add_child(lower_plane)
		lower_plane.owner = scene
		var upper_plane = scene.get_node("SurfacePlane")
		upper_plane.owner = scene
		var animated: Array[AnimatedSprite2D] = []
		for along in range(8):
			var sprite = AnimatedSprite2D.new()
			sprite.name = ("UpperRiver" if along<4 else "LowerRiver")+str(along)
			sprite.sprite_frames = load(frame_file)
			sprite.animation = "flow"
			sprite.centered = false
			sprite.z_index = 2
			sprite.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
			sprite.material = river_layer.material.duplicate()
			var cell = Vector2i(1,along) if ns else Vector2i(along,1)
			sprite.position = top.map_to_local(cell)-Vector2(32,16)-Vector2(0,64 if along<4 else 0)
			scene.add_child(sprite)
			sprite.owner = scene
			var plane = upper_plane if along<4 else lower_plane
			plane.layer_paths.append(NodePath("../"+str(sprite.name)))
			animated.append(sprite)
		var inlet = Vector2i(1,3) if ns else Vector2i(3,1)
		var outlet = Vector2i(1,4) if ns else Vector2i(4,1)
		var upstream_anchor = Vector2(64 if ns else 32,16)
		var downstream_anchor = Vector2(32 if ns else 64,96)
		var fall = AnimatedSprite2D.new()
		fall.name = "Waterfall"
		fall.sprite_frames = load(FALL)
		fall.animation = direction+".narrow"
		fall.centered = false
		fall.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
		fall.z_index = 3
		fall.position = top.map_to_local(inlet)-Vector2(0,64)-upstream_anchor
		scene.add_child(fall)
		fall.owner = scene
		animated.append(fall)
		# A single native animation timeline drives every connected sheet in phase.
		var animation = Animation.new()
		animation.length = 1.2
		animation.loop_mode = Animation.LOOP_LINEAR
		for sprite in animated:
			var track = animation.add_track(Animation.TYPE_VALUE)
			animation.track_set_path(track,NodePath(str(sprite.name)+":frame"))
			animation.value_track_set_update_mode(track,Animation.UPDATE_DISCRETE)
			for frame in range(16): animation.track_insert_key(track,frame*1.2/16,frame)
		var library = AnimationLibrary.new()
		library.add_animation("flow",animation)
		var player = AnimationPlayer.new()
		player.name = "WaterClock"
		player.add_animation_library("",library)
		player.autoplay = "flow"
		scene.add_child(player)
		player.owner = scene
		scene.set_meta("production_ready",false)
		scene.set_meta("visual_rise_pixels",64)
		scene.set_meta("missing_artwork","Natural bank/lip contacts, bottom overlays, lake/sea continuation, visual approval")
		var file = direction+".tscn"
		var packed = PackedScene.new()
		var result = packed.pack(scene)
		if result==OK: result = ResourceSaver.save(packed,BASE+file)
		if result!=OK: errors.append("Route save failed")
		entries.append({"scene":file,"direction":direction,"inletCell":[inlet.x,inlet.y],"outletCell":[outlet.x,outlet.y],"upstreamAnchor":[upstream_anchor.x,upstream_anchor.y],"downstreamAnchor":[downstream_anchor.x,downstream_anchor.y],"upstreamElevationPixels":64,"downstreamElevationPixels":0})
		reference.free()
		scene.free()
	var file = FileAccess.open(BASE+"manifest.json",FileAccess.WRITE)
	file.store_string(JSON.stringify({"schemaVersion":1,"status":"candidate","style":"cartoon","projection":"isometric","cases":entries,"frames":16,"periodSeconds":1.2,"clock":"AnimationPlayer:WaterClock/flow","sourceScenes":[CLIFF,RIVER],"waterfallFrames":FALL,"approvalEvidence":null,"productionReady":false,"pending":["Natural bank and cliff contacts","Foam visual approval","Lake and sea continuation","Wide routes","Engine pack bindings"]},"  "))
	print("ISOMETRIC GRANITE ROUTE ","PASSED" if errors.is_empty() else "FAILED")
	quit(0 if errors.is_empty() else 1)
