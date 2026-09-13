extends "res://tests/terrain_coastal_depth_render_probe.gd"

const ROUTES = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/depth_waterfall_route_v1/isometric/"

func frame_reference(atlas: Image,origin: Vector2i,stride: Vector2i,size: Vector2i,screen: Vector2i) -> Dictionary:
	var points = []
	for y in range(2,size.y-2,3):
		for x in range(2,size.x-2,3):
			var point = Vector2i(x,y)
			var valid = true
			for frame in range(16):
				var c = atlas.get_pixelv(origin+stride*frame+point)
				if c.a<1.0 or c.b<=c.r or c.g<0.35: valid = false
			if valid: points.append(point)
	check(points.size()>10,"Not enough opaque water samples for independent phase audit")
	return {"atlas":atlas,"origin":origin,"stride":stride,"screen":screen,"points":points}

func infer_frame(rendered: Image,reference: Dictionary) -> Dictionary:
	var scores = []
	for frame in range(16):
		var score = 0.0
		for point in reference.points:
			var expected = reference.atlas.get_pixelv(reference.origin+reference.stride*frame+point)
			var actual = rendered.get_pixelv(reference.screen+point)
			score += abs(expected.r-actual.r)+abs(expected.g-actual.g)+abs(expected.b-actual.b)
		scores.append(score)
	var best = scores.min()
	var index = scores.find(best)
	scores.sort()
	return {"frame":index,"error":best,"unique":scores[1]-scores[0]>0.01}

func run() -> void:
	var results = []
	for direction in ["north_south","west_east"]:
		var viewport = SubViewport.new()
		viewport.size = Vector2i(1152,720)
		viewport.transparent_bg = true
		viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
		root.add_child(viewport)
		var scene = load(ROUTES+direction+".tscn").instantiate()
		scene.position = Vector2(512,176)
		viewport.add_child(scene)
		var water: TileMapLayer = scene.get_node("LakeWater")
		var depth: TileMapLayer = scene.get_node("LakeDepth")
		var binding = scene.get_node("LakeDepthBinding")
		for name in ["SurfacePlane","LowerSurfacePlane"]: check(scene.get_node(name).refresh().is_empty(),"Surface binding")
		check(binding.refresh().is_empty(),"Depth binding")
		check(water.get_used_cells().size()==26 and not scene.has_node("WaterClock") and not scene.has_node("Lake_1_8"),"Baked lake remains")
		for child in scene.get_children():
			if child is Sprite2D and child.material is ShaderMaterial:
				child.material.set_shader_parameter("frame_override",0.0)
		water.tile_set = water.tile_set.duplicate(true)
		speed(water,0.001)
		water.material.set_shader_parameter("connected_water_clock",false)
		for i in range(4): await process_frame
		binding.set_process(false)
		var shaded = await capture(viewport)
		water.material.set_shader_parameter("depth_field_enabled",false)
		var flat = await capture(viewport)
		var contrast = difference(flat,shaded)
		check(contrast.changed>100 and contrast.landChanged==0 and contrast.alphaChanged==0,"Depth affects terrain or is absent")
		check(binding.refresh().is_empty(),"Restore depth")
		var saved = depth.tile_map_data.duplicate()
		depth.clear()
		check(binding.refresh().is_empty(),"Erase depth")
		var erased = await capture(viewport)
		check(difference(shaded,erased).changed>100,"Native paint does not update visible depth")
		depth.tile_map_data = saved
		check(binding.refresh().is_empty(),"Restore native paint")
		var packed = PackedScene.new()
		var path = OUTPUT+"isometric_depth_"+direction+"_roundtrip.tscn"
		check(packed.pack(scene)==OK and ResourceSaver.save(packed,path)==OK,"Save route")
		var reopened = load(path).instantiate()
		root.add_child(reopened)
		check(reopened.get_node("LakeDepthBinding").refresh().is_empty(),"Reopen route")
		reopened.free()
		for child in scene.get_children():
			if child is Sprite2D and child.material is ShaderMaterial:
				child.material.set_shader_parameter("frame_override",-1.0)
		speed(water,16.0/1.2)
		water.material.set_shader_parameter("connected_water_clock",true)
		var first = await capture(viewport)
		await create_timer(0.3).timeout
		var second = await capture(viewport)
		var motion = difference(first,second)
		check(motion.changed>100 and motion.landChanged==0 and motion.alphaChanged==0,"Motion missing or terrain moves")
		first.save_png(OUTPUT+"isometric_depth_route_"+direction+".png")
		# Fresh native speeds avoid freeze/resume perturbing the clock being audited.
		viewport.remove_child(scene)
		scene.free()
		scene = load(ROUTES+direction+".tscn").instantiate()
		scene.position = Vector2(512,176)
		viewport.add_child(scene)
		for name in ["SurfacePlane","LowerSurfacePlane"]: check(scene.get_node(name).refresh().is_empty(),"Phase surface binding")
		check(scene.get_node("LakeDepthBinding").refresh().is_empty(),"Phase depth binding")
		water = scene.get_node("LakeWater")
		var stem = Vector2i(1,7) if direction=="north_south" else Vector2i(7,1)
		var source = water.tile_set.get_source(2) as TileSetAtlasSource
		var refs = [frame_reference(source.texture.get_image(),water.get_cell_atlas_coords(stem)*Vector2i(64,32),Vector2i(0,128),Vector2i(64,32),Vector2i(water.to_global(water.map_to_local(stem))-Vector2(32,16)))]
		for name in ["LowerRiver6","Waterfall"]:
			var sprite = scene.get_node(name) as Sprite2D
			var m = sprite.material as ShaderMaterial
			refs.append(frame_reference(m.get_shader_parameter("animation_atlas").get_image(),Vector2i(m.get_shader_parameter("animation_origin")),Vector2i(m.get_shader_parameter("animation_stride")),Vector2i(m.get_shader_parameter("animation_size")),Vector2i(sprite.global_position)))
		var samples = []
		var seen = {}
		for sample in range(256):
			await create_timer(0.04).timeout
			var rendered = await capture(viewport)
			var frames = []
			for reference in refs: frames.append(infer_frame(rendered,reference))
			seen[frames[0].frame] = true
			for frame in frames: check(frame.frame==frames[0].frame and frame.error<0.01 and frame.unique,"Isometric rendered phase mismatch: "+direction+" "+str(frame))
			samples.append(frames)
		check(seen.size()==16,"Isometric phase audit missed frames")
		results.append({"direction":direction,"depth":contrast,"motion":motion,"nativePaint":true,"saveReopen":true,"phaseSamples":samples,"distinctFrames":seen.size()})
		viewport.free()
	var report = FileAccess.open(OUTPUT+"isometric_depth_route_render.json",FileAccess.WRITE)
	report.store_string(JSON.stringify({"status":"passed" if errors.is_empty() else "failed","cases":results,"errors":errors,"phaseAudit":"rendered_pixel_inference","visualApproval":false},"  "))
	print("ISOMETRIC DEPTH ROUTE GPU ","PASSED" if errors.is_empty() else "FAILED")
	quit(0 if errors.is_empty() else 1)
