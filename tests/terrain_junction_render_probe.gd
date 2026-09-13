extends SceneTree

const BASE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/river_junctions_v1/"
const OUTPUT = "res://addons/beep_game_builder_cs/generated/test/library/output/"
var errors: Array[String] = []

func _initialize() -> void:
	call_deferred("run")

func check(ok: bool, message: String) -> void:
	if not ok:
		errors.append(message)
		push_error(message)

func capture(viewport: SubViewport) -> Image:
	await process_frame
	await RenderingServer.frame_post_draw
	return viewport.get_texture().get_image()

func run() -> void:
	var results = []
	for projection in ["square","isometric"]:
		var viewport = SubViewport.new()
		viewport.size = Vector2i(3200,1700)
		viewport.transparent_bg = true
		viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
		root.add_child(viewport)
		viewport.canvas_transform = Transform2D(0,Vector2(850,100) if projection=="isometric" else Vector2(60,60))
		var scene = load(BASE+projection+"/river_junctions_review.tscn").instantiate()
		viewport.add_child(scene)
		var layer: TileMapLayer = scene.get_node("Water")
		var atlas: TileSetAtlasSource = layer.tile_set.get_source(0)
		check(atlas.get_tiles_count()==32,"Missing junction profiles")
		for index in range(32):
			var coords = Vector2i(index%8,index/8)
			check(atlas.get_tile_animation_frames_count(coords)==16,"Missing animation frames")
			check(is_equal_approx(atlas.get_tile_animation_speed(coords),16.0/1.2),"Incorrect animation period")
		var before = await capture(viewport)
		await create_timer(0.35).timeout
		var after = await capture(viewport)
		var missing = 0
		for cell in layer.get_used_cells():
			var screen = Vector2i(viewport.canvas_transform*layer.to_global(layer.map_to_local(cell)))
			if not Rect2i(Vector2i.ZERO,viewport.size).has_point(screen):
				missing += 1
			elif before.get_pixelv(screen).a < 0.99:
				missing += 1
		var water_changes = 0
		var land_changes = 0
		var land_samples = 0
		for y in range(viewport.size.y):
			for x in range(viewport.size.x):
				var a = before.get_pixel(x,y)
				if a.a<0.99: continue
				var b = after.get_pixel(x,y)
				var changed = abs(a.r-b.r)+abs(a.g-b.g)+abs(a.b-b.b)>0.015
				if a.b>a.r+0.2 and a.g>a.r+0.2:
					if changed: water_changes += 1
				else:
					land_samples += 1
					if changed: land_changes += 1
		check(missing==0,"Blank or clipped junction cells")
		check(water_changes>1000,"Missing river motion")
		check(land_samples>1000 and land_changes==0,"Land or bank motion")
		check(before.save_png(OUTPUT+"junctions_"+projection+".png")==OK,"Capture failed")
		results.append({"projection":projection,"missingCells":missing,"waterChangedPixels":water_changes,"landChangedPixels":land_changes})
		viewport.free()
	var file = FileAccess.open(OUTPUT+"junctions_render.json",FileAccess.WRITE)
	file.store_string(JSON.stringify({"status":"passed" if errors.is_empty() else "failed","results":results,"errors":errors,"visualApproval":false},"  "))
	print("JUNCTION RENDER ","PASSED" if errors.is_empty() else "FAILED")
	quit(0 if errors.is_empty() else 1)
