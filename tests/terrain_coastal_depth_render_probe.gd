extends SceneTree

const BASE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/shared_sea_v1/"
const OUTPUT = "res://addons/beep_game_builder_cs/generated/test/library/output/"
var errors: Array[String] = []

func _initialize() -> void:
	call_deferred("run")

func check(ok: bool,message: String) -> void:
	if not ok:
		errors.append(message)
		push_error(message)

func speed(layer: TileMapLayer,value: float) -> void:
	for i in range(layer.tile_set.get_source_count()):
		var atlas = layer.tile_set.get_source(layer.tile_set.get_source_id(i)) as TileSetAtlasSource
		if atlas==null: continue
		for j in range(atlas.get_tiles_count()): atlas.set_tile_animation_speed(atlas.get_tile_id(j),value)

func capture(viewport: SubViewport) -> Image:
	await process_frame
	await RenderingServer.frame_post_draw
	return viewport.get_texture().get_image()

func difference(a: Image,b: Image,shift: Vector2i = Vector2i.ZERO) -> Dictionary:
	var changed = 0
	var land = 0
	var alpha = 0
	for y in range(30,a.get_height()-30):
		for x in range(30,a.get_width()-30):
			var p = a.get_pixel(x,y)
			var q = b.get_pixelv(Vector2i(x,y)+shift)
			if abs(p.a-q.a)>0.01: alpha += 1
			if p.a<0.99: continue
			if abs(p.r-q.r)+abs(p.g-q.g)+abs(p.b-q.b)>0.015:
				changed += 1
				if not (p.b>p.r+0.03 and p.g>p.r+0.03): land += 1
	return {"changed":changed,"landChanged":land,"alphaChanged":alpha}

func run() -> void:
	var results: Array = []
	for projection in ["square","isometric"]:
		var viewport = SubViewport.new()
		viewport.size = Vector2i(1100,850)
		viewport.transparent_bg = true
		viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
		root.add_child(viewport)
		var scene = load(BASE+projection+"/coastal_depth_review.tscn").instantiate()
		viewport.add_child(scene)
		var water: TileMapLayer = scene.get_node("Water")
		var depth: TileMapLayer = scene.get_node("Depth")
		water.tile_set = water.tile_set.duplicate(true)
		depth.tile_set = depth.tile_set.duplicate(true)
		speed(water,0.001)
		speed(depth,0.001)
		var binding = scene.get_node("CoastalDepthBinding")
		print("DEPTH ORIGIN ", projection, " ", water.map_to_local(Vector2i.ZERO), " x ", water.map_to_local(Vector2i.RIGHT), " y ", water.map_to_local(Vector2i.DOWN))
		check(scene.get_node("SurfacePlane").refresh().is_empty(),"Surface plane failed")
		check(binding.refresh().is_empty(),"Coastal fixture rejected")
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
		var coastal = await capture(viewport)
		water.material.set_shader_parameter("depth_field_enabled",false)
		var baseline = await capture(viewport)
		var contact = difference(baseline,coastal)
		check(contact.changed>500 and contact.landChanged==0 and contact.alphaChanged==0,"Coastal depth changes banks or is invisible")
		var painted = depth.tile_map_data
		depth.set_cell(Vector2i.ZERO,0,Vector2i(6,5),0)
		check(not binding.refresh().is_empty(),"Land depth paint accepted")
		var blocked = difference(baseline,await capture(viewport))
		check(blocked.changed==0 and blocked.alphaChanged==0,"Invalid paint replaced the sea display")
		depth.tile_map_data = painted
		check(binding.refresh().is_empty(),"Corrected coastal paint rejected")
		var restored = difference(coastal,await capture(viewport))
		check(restored.changed==0 and restored.alphaChanged==0,"Restored coast appearance differs")
		# Compare the new cell lookup against the existing calibrated depth overlay.
		depth.clear()
		var interior: Array[Vector2i] = []
		for y in range(0,4):
			for x in range(14,20): interior.append(Vector2i(x,y))
		depth.set_cells_terrain_connect(interior,0,0,false)
		check(binding.refresh().is_empty(),"Interior comparison rejected")
		var mapped = await capture(viewport)
		water.material.set_shader_parameter("depth_field_enabled",false)
		depth.material.set_shader_parameter("depth_permitted",true)
		var overlay = await capture(viewport)
		var equivalent = difference(mapped,overlay)
		check(equivalent.changed==0 and equivalent.alphaChanged==0,"Field does not match the calibrated depth overlay")
		depth.material.set_shader_parameter("depth_permitted",false)
		depth.tile_map_data = painted
		check(binding.refresh().is_empty(),"Coastal restore rejected")
		viewport.canvas_transform.origin += Vector2(16,12)
		var camera = difference(coastal,await capture(viewport),Vector2i(16,12))
		check(camera.changed==0 and camera.alphaChanged==0,"Camera slides the coastal depth field")
		viewport.canvas_transform = transform
		binding.set_process(true)
		speed(water,16.0/1.2)
		speed(depth,16.0/1.2)
		var first = await capture(viewport)
		await create_timer(0.35).timeout
		var second = await capture(viewport)
		var motion = difference(first,second)
		check(motion.changed>100 and motion.landChanged==0 and motion.alphaChanged==0,"Coastal depth animation moves land or bounds")
		check(coastal.save_png(OUTPUT+"coastal_depth_"+projection+"_still.png")==OK,"Save coast capture")
		check(mapped.save_png(OUTPUT+"coastal_depth_"+projection+"_mapped.png")==OK and overlay.save_png(OUTPUT+"coastal_depth_"+projection+"_overlay.png")==OK,"Save comparison captures")
		check(first.save_png(OUTPUT+"coastal_depth_"+projection+"_a.png")==OK and second.save_png(OUTPUT+"coastal_depth_"+projection+"_b.png")==OK,"Save animation captures")
		results.append({"projection":projection,"coastalContact":contact,"invalidPaint":blocked,"restored":restored,"overlayEquivalence":equivalent,"camera":camera,"motion":motion})
		viewport.free()
	var file = FileAccess.open(OUTPUT+"coastal_depth_render.json",FileAccess.WRITE)
	file.store_string(JSON.stringify({"status":"passed" if errors.is_empty() else "failed","results":results,"errors":errors,"visualApproval":false},"  "))
	print("COASTAL DEPTH RENDER ","PASSED" if errors.is_empty() else "FAILED")
	quit(0 if errors.is_empty() else 1)
