extends "res://tests/terrain_coastal_depth_render_probe.gd"

const ROUTE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/depth_waterfall_route_v1/square/route.tscn"
const ORIGINAL = "res://addons/beep_game_builder_cs/generated/dev/cartoon/elevation/staging/grass_granite_front_v1/river_waterfall_lake_review.tscn"

func rendered_sprite_frame(rendered: Image,sprite: Sprite2D,area: Rect2i) -> Dictionary:
	var material = sprite.material as ShaderMaterial
	var atlas = (material.get_shader_parameter("animation_atlas") as Texture2D).get_image()
	var origin: Vector2 = material.get_shader_parameter("animation_origin")
	var stride: Vector2 = material.get_shader_parameter("animation_stride")
	var top = Vector2i(sprite.position+Vector2(80,100))
	var best_frame = -1
	var best_error = INF
	for frame in range(16):
		var score = 0.0
		for y in range(area.position.y,area.end.y,3):
			for x in range(area.position.x,area.end.x,3):
				var expected = atlas.get_pixelv(Vector2i(origin+stride*frame)+Vector2i(x,y))
				var actual = rendered.get_pixelv(top+Vector2i(x,y))
				score += abs(expected.r-actual.r)+abs(expected.g-actual.g)+abs(expected.b-actual.b)
		if score<best_error:
			best_error = score
			best_frame = frame
	return {"frame":best_frame,"error":best_error}

func run() -> void:
	var viewport = SubViewport.new()
	viewport.size = Vector2i(480,900)
	viewport.transparent_bg = true
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	viewport.canvas_transform = Transform2D(0,Vector2(80,100))
	root.add_child(viewport)
	var scene = load(ROUTE).instantiate()
	viewport.add_child(scene)
	var original = load(ORIGINAL).instantiate()
	for name in ["UpperGround","LowerGround","StaticCliff","CliffBottomContact"]:
		check(scene.get_node(name).position==original.get_node(name).position and scene.get_node(name).texture.resource_path==original.get_node(name).texture.resource_path,"Retained terrain artwork changed")
	for name in ["WaterfallWaterOnly","Upstream","Downstream"]:
		var current = scene.get_node(name) as Sprite2D
		var before = original.get_node(name) as AnimatedSprite2D
		check(current.position==before.position,"Water sprite moved")
		var atlas = current.material.get_shader_parameter("animation_atlas") as Texture2D
		var first = before.sprite_frames.get_frame_texture(before.animation,0) as AtlasTexture
		check(atlas.get_image().get_data()==first.atlas.get_image().get_data(),"Approved water atlas changed")
	original.free()
	var lake: TileMapLayer = scene.get_node("LowerLake")
	var depth: TileMapLayer = scene.get_node("LakeDepth")
	var binding = scene.get_node("LakeDepthBinding")
	check(scene.get_node("SurfacePlane").refresh().is_empty() and binding.refresh().is_empty(),"Connected depth route rejected")
	var up: Sprite2D = scene.get_node("Upstream")
	var fall: Sprite2D = scene.get_node("WaterfallWaterOnly")
	var down: Sprite2D = scene.get_node("Downstream")
	check(up.position+Vector2(0,64)==fall.position,"Upper river does not meet waterfall")
	check(fall.position+Vector2(0,192)==down.position,"Waterfall does not meet lower river")
	check(down.position+Vector2(0,64)==lake.map_to_local(Vector2i(2,4))-Vector2(32,32),"Sprite river does not meet native river")
	check(lake.get_cell_source_id(Vector2i(2,5))==1,"Native lake inlet is missing")
	check(scene.get_meta("visual_rise_pixels")==64,"Visual cliff rise changed")
	var packed = PackedScene.new()
	var path = OUTPUT+"depth_waterfall_route_roundtrip.tscn"
	check(packed.pack(scene)==OK and ResourceSaver.save(packed,path)==OK,"Route save failed")
	var reopened = load(path).instantiate()
	root.add_child(reopened)
	check(reopened.get_node("LakeDepthBinding").refresh().is_empty(),"Route reopen rejected")
	reopened.free()
	for sprite in [up,fall,down]: sprite.material.set_shader_parameter("frame_override",0.0)
	lake.tile_set = lake.tile_set.duplicate(true)
	speed(lake,0.001)
	lake.material.set_shader_parameter("connected_water_clock",false)
	for i in range(4): await process_frame
	binding.set_process(false)
	var shaded = await capture(viewport)
	lake.material.set_shader_parameter("depth_field_enabled",false)
	var unshaded = await capture(viewport)
	var contrast = difference(unshaded,shaded)
	check(contrast.changed>100 and contrast.landChanged==0 and contrast.alphaChanged==0,"Lake depth is absent or affects terrain")
	check(binding.refresh().is_empty(),"Lake depth restore failed")
	for sprite in [up,fall,down]: sprite.material.set_shader_parameter("frame_override",-1.0)
	speed(lake,16.0/1.2)
	lake.material.set_shader_parameter("connected_water_clock",true)
	var first = await capture(viewport)
	await create_timer(0.3).timeout
	var second = await capture(viewport)
	var motion = difference(first,second)
	check(motion.changed>100 and motion.landChanged==0 and motion.alphaChanged==0,"Connected route animation absent or terrain moves")
	# Audit a fresh instance with untouched native speeds, not the frozen test copy.
	viewport.remove_child(scene)
	scene.free()
	scene = load(ROUTE).instantiate()
	viewport.add_child(scene)
	lake = scene.get_node("LowerLake")
	fall = scene.get_node("WaterfallWaterOnly")
	down = scene.get_node("Downstream")
	check(scene.get_node("SurfacePlane").refresh().is_empty() and scene.get_node("LakeDepthBinding").refresh().is_empty(),"Fresh phase-audit instance rejected")
	var atlas = lake.tile_set.get_source(2) as TileSetAtlasSource
	var river_image = atlas.texture.get_image()
	var phase: Array = []
	for sample in range(256):
		await create_timer(0.04).timeout
		var rendered = await capture(viewport)
		var best_frame = -1
		var best_error = INF
		for frame in range(16):
			var score = 0.0
			for y in range(2,62,3):
				for x in range(14,50,3):
					var expected = river_image.get_pixel(x,frame*256+y)
					var actual = rendered.get_pixel(80+128+x,100+256+y)
					score += abs(expected.r-actual.r)+abs(expected.g-actual.g)+abs(expected.b-actual.b)
			if score<best_error:
				best_error = score
				best_frame = frame
		var river_phase = rendered_sprite_frame(rendered,down,Rect2i(14,2,36,60))
		var fall_phase = rendered_sprite_frame(rendered,fall,Rect2i(14,74,36,40))
		phase.append({"spriteFrame":river_phase.frame,"spritePixelError":river_phase.error,"waterfallFrame":fall_phase.frame,"waterfallPixelError":fall_phase.error,"nativeFrame":best_frame,"nativePixelError":best_error})
	var synchronized = phase.all(func(p): return p.spriteFrame==p.nativeFrame and p.waterfallFrame==p.nativeFrame and p.nativePixelError<0.01 and p.spritePixelError<0.01 and p.waterfallPixelError<0.01)
	var seen: Dictionary = {}
	for sample in phase: seen[sample.nativeFrame] = true
	check(synchronized and seen.size()==16,"Native/sprite phase mismatch or incomplete frame coverage")
	check(first.save_png(OUTPUT+"depth_waterfall_route_square_a.png")==OK and second.save_png(OUTPUT+"depth_waterfall_route_square_b.png")==OK,"Save route captures")
	var report = {"status":"passed" if errors.is_empty() else "failed","errors":errors,"geometryAndDepth":contrast,"motion":motion,"phaseSynchronized":synchronized,"phaseSamples":phase,"saveReopen":true,"visualApproval":false,"acceptance":"technical_candidate" if synchronized else "animation_phase_pending"}
	var file = FileAccess.open(OUTPUT+"depth_waterfall_route_render.json",FileAccess.WRITE)
	file.store_string(JSON.stringify(report,"  "))
	viewport.free()
	print("DEPTH WATERFALL ROUTE ","PASSED" if errors.is_empty() else "FAILED"," phase synchronized: ",synchronized)
	quit(0 if errors.is_empty() else 1)
