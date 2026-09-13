extends "res://tests/terrain_coastal_depth_render_probe.gd"

const LAKE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/lake_depth_v1/"
const ORIGINAL = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/lake_banks_v1/"

func run() -> void:
	var results: Array = []
	for projection in ["square","isometric"]:
		var viewport = SubViewport.new()
		viewport.size = Vector2i(920,760)
		viewport.transparent_bg = true
		viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
		root.add_child(viewport)
		var scene = load(LAKE+projection+"/lake_depth_review.tscn").instantiate()
		viewport.add_child(scene)
		var water: TileMapLayer = scene.get_node("Water")
		var depth: TileMapLayer = scene.get_node("Depth")
		water.tile_set = water.tile_set.duplicate(true)
		depth.tile_set = depth.tile_set.duplicate(true)
		var atlas = water.tile_set.get_source(0) as TileSetAtlasSource
		var source = load(ORIGINAL+projection+"/grass_lake_16.res") as Texture2D
		check(atlas.texture.get_image().get_data()==source.get_image().get_data(),"Authored lake ripple frames changed")
		speed(water,0.001)
		speed(depth,0.001)
		var plane = scene.get_node("SurfacePlane")
		var binding = scene.get_node("CoastalDepthBinding")
		check(plane.refresh().is_empty() and binding.refresh().is_empty(),"Lake surface/depth rejected")
		for i in range(4): await process_frame
		binding.set_process(false)
		var size = Vector2(water.tile_set.tile_size)
		var cells = water.get_used_cells()
		var bounds = Rect2(water.map_to_local(cells[0])-size/2,size)
		for cell in cells: bounds = bounds.merge(Rect2(water.map_to_local(cell)-size/2,size))
		var scale = minf((viewport.size.x-80)/bounds.size.x,(viewport.size.y-80)/bounds.size.y)
		var origin = (Vector2(viewport.size)-bounds.size*scale)/2-bounds.position*scale
		var transform = Transform2D(Vector2(scale,0),Vector2(0,scale),origin)
		viewport.canvas_transform = transform
		var painted = depth.tile_map_data
		var shaded = await capture(viewport)
		water.material.set_shader_parameter("depth_field_enabled",false)
		var original = await capture(viewport)
		var contrast = difference(original,shaded)
		check(contrast.changed>500 and contrast.landChanged==0 and contrast.alphaChanged==0,"Lake depth alters banks or has no contrast")
		depth.set_cell(Vector2i.ZERO,0,Vector2i(6,5),0)
		check(not binding.refresh().is_empty(),"Invalid lake land paint accepted")
		var invalid = difference(original,await capture(viewport))
		check(invalid.changed==0 and invalid.alphaChanged==0,"Invalid depth does not preserve original lake")
		depth.tile_map_data = painted
		check(binding.refresh().is_empty(),"Lake restoration rejected")
		var restored = difference(shaded,await capture(viewport))
		check(restored.changed==0 and restored.alphaChanged==0,"Lake restoration changes shading")
		depth.clear()
		check(binding.refresh().is_empty(),"Erased depth rejected")
		var deep_image = await capture(viewport)
		var erased = difference(shaded,deep_image)
		check(erased.changed>100 and erased.landChanged==0 and erased.alphaChanged==0,"Depth erasure has no effect or alters banks")
		var deep_samples = 0
		var deep_errors = 0
		for cell in water.get_used_cells():
			var data = water.get_cell_tile_data(cell)
			if data==null or data.terrain!=0: continue
			var full = true
			for bit in range(16):
				if data.is_valid_terrain_peering_bit(bit) and data.get_terrain_peering_bit(bit)!=0: full = false
			if not full: continue
			for dy in [-2,0,2]:
				for dx in [-4,0,4]:
					var point = Vector2i(transform*(water.map_to_local(cell)+Vector2(dx,dy)))
					var before = original.get_pixelv(point)
					var after = deep_image.get_pixelv(point)
					if abs(after.r-before.r*0.65)+abs(after.g-before.g*0.8)+abs(after.b-before.b*0.9)>0.015: deep_errors += 1
					deep_samples += 1
		check(deep_samples>0 and deep_errors==0,"Full lake cells contain erroneous shallow atlas blocks")
		depth.tile_map_data = painted
		check(binding.refresh().is_empty(),"Restoring depth paint failed")
		viewport.canvas_transform.origin += Vector2(16,12)
		var camera = difference(shaded,await capture(viewport),Vector2i(16,12))
		check(camera.changed==0 and camera.alphaChanged==0,"Camera slides lake depth")
		viewport.canvas_transform = transform
		plane.repeat_cells = 8
		check(plane.refresh().is_empty() and binding.refresh().is_empty(),"Repeat change rejected")
		var repeated = await capture(viewport)
		# Grass texture scale may change; water must not change with surface repeats.
		var repeat_water_changes = 0
		for y in range(shaded.get_height()):
			for x in range(shaded.get_width()):
				var p = shaded.get_pixel(x,y)
				var q = repeated.get_pixel(x,y)
				if p.a>0.99 and p.b>p.r+0.03 and p.g>p.r+0.03 and abs(p.r-q.r)+abs(p.g-q.g)+abs(p.b-q.b)>0.015: repeat_water_changes += 1
		check(repeat_water_changes==0,"Surface repeat rescales lake water/depth")
		plane.repeat_cells = 4
		check(plane.refresh().is_empty() and binding.refresh().is_empty(),"Repeat restoration failed")
		binding.set_process(true)
		speed(water,16.0/1.2)
		speed(depth,16.0/1.2)
		var first = await capture(viewport)
		await create_timer(0.35).timeout
		var second = await capture(viewport)
		var motion = difference(first,second)
		check(motion.changed>100 and motion.landChanged==0 and motion.alphaChanged==0,"Lake motion missing or terrain moves")
		check(shaded.save_png(OUTPUT+"lake_depth_"+projection+"_still.png")==OK,"Save lake capture")
		check(first.save_png(OUTPUT+"lake_depth_"+projection+"_a.png")==OK and second.save_png(OUTPUT+"lake_depth_"+projection+"_b.png")==OK,"Save lake animation captures")
		results.append({"projection":projection,"sourceFramesUnchanged":true,"contrast":contrast,"invalidPaint":invalid,"restored":restored,"erased":erased,"deepInteriorSamples":deep_samples,"deepInteriorErrors":deep_errors,"camera":camera,"repeatWaterChanges":repeat_water_changes,"motion":motion})
		viewport.free()
	var file = FileAccess.open(OUTPUT+"lake_depth_render.json",FileAccess.WRITE)
	file.store_string(JSON.stringify({"status":"passed" if errors.is_empty() else "failed","results":results,"errors":errors,"visualApproval":false},"  "))
	print("LAKE DEPTH RENDER ","PASSED" if errors.is_empty() else "FAILED")
	quit(0 if errors.is_empty() else 1)
