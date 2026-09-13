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

func difference(a: Image,b: Image) -> Dictionary:
	var changed = 0
	var land = 0
	var alpha = 0
	for y in range(a.get_height()):
		for x in range(a.get_width()):
			var p = a.get_pixel(x,y)
			var q = b.get_pixel(x,y)
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
		var scene = load(BASE+projection+"/depth_review.tscn").instantiate()
		viewport.add_child(scene)
		var water: TileMapLayer = scene.get_node("Water")
		var depth: TileMapLayer = scene.get_node("Depth")
		water.tile_set = water.tile_set.duplicate(true)
		depth.tile_set = depth.tile_set.duplicate(true)
		speed(water,0.001)
		speed(depth,0.001)
		var guard = scene.get_node("DepthGuard")
		check(scene.get_node("SurfacePlane").refresh().is_empty(),"Depth surface plane failed")
		check(guard.refresh().is_empty(),"Depth guard rejected fixture")
		var size = Vector2(water.tile_set.tile_size)
		var cells = water.get_used_cells()
		var bounds = Rect2(water.map_to_local(cells[0])-size/2,size)
		for cell in cells: bounds = bounds.merge(Rect2(water.map_to_local(cell)-size/2,size))
		var scale = minf((viewport.size.x-40)/bounds.size.x,(viewport.size.y-40)/bounds.size.y)
		var origin = (Vector2(viewport.size)-bounds.size*scale)/2-bounds.position*scale
		viewport.canvas_transform = Transform2D(Vector2(scale,0),Vector2(0,scale),origin)
		var visible = await capture(viewport)
		depth.visible = false
		var baseline = await capture(viewport)
		var contrast = difference(baseline,visible)
		check(contrast.changed>500,"Depth has no visible distinction")
		check(contrast.landChanged==0 and contrast.alphaChanged==0,"Depth overwrites banks or bounds")
		var hole = Vector2i(viewport.canvas_transform*water.to_global(water.map_to_local(Vector2i(16,3))))
		check(visible.get_pixelv(hole).is_equal_approx(baseline.get_pixelv(hole)),"Deep hole does not match open sea")
		depth.visible = true
		var painted = depth.tile_map_data
		depth.set_cell(Vector2i.ZERO,0,Vector2i(6,5),0)
		check(not guard.refresh().is_empty(),"Invalid depth fixture accepted")
		var blocked = difference(baseline,await capture(viewport))
		check(blocked.changed==0 and blocked.alphaChanged==0,"Guard did not preserve underlying sea display")
		check(depth.get_cell_source_id(Vector2i.ZERO)==0,"Guard destroyed invalid paint")
		depth.tile_map_data = painted
		check(guard.refresh().is_empty(),"Corrected depth fixture rejected")
		var restored = difference(visible,await capture(viewport))
		check(restored.changed==0 and restored.alphaChanged==0,"Corrected depth appearance did not restore")
		speed(water,16.0/1.2)
		speed(depth,16.0/1.2)
		var first = await capture(viewport)
		await create_timer(0.35).timeout
		var second = await capture(viewport)
		var motion = difference(first,second)
		check(motion.changed>100 and motion.landChanged==0 and motion.alphaChanged==0,"Depth animation/bank stability failed")
		check(visible.save_png(OUTPUT+"sea_depth_"+projection+"_still.png")==OK,"Save depth capture")
		check(first.save_png(OUTPUT+"sea_depth_"+projection+"_a.png")==OK and second.save_png(OUTPUT+"sea_depth_"+projection+"_b.png")==OK,"Save animated depth captures")
		results.append({"projection":projection,"depthContrast":contrast,"blockedDisplay":blocked,"restoredDisplay":restored,"motion":motion})
		viewport.free()
	var report = FileAccess.open(OUTPUT+"sea_depth_render.json",FileAccess.WRITE)
	report.store_string(JSON.stringify({"status":"passed" if errors.is_empty() else "failed","results":results,"errors":errors,"visualApproval":false},"  "))
	print("SEA DEPTH RENDER ","PASSED" if errors.is_empty() else "FAILED")
	quit(0 if errors.is_empty() else 1)
