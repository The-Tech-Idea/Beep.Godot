extends SceneTree

const BASE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/waterfall_sections_v1/"
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
	var viewport = SubViewport.new()
	viewport.size = Vector2i(1000,750)
	viewport.transparent_bg = true
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(viewport)
	var scene = load(BASE+"water_only_review.tscn").instantiate()
	scene.position = Vector2(20,20)
	viewport.add_child(scene)
	var before = await capture(viewport)
	await create_timer(0.35).timeout
	var after = await capture(viewport)
	var results = []
	for assembly in scene.get_children():
		var rise = int(assembly.get_meta("rise_pixels"))
		var counts = [0,0,0,0]
		var alpha_changes = 0
		var pixels = 0
		for sprite in assembly.get_children():
			check(sprite.is_playing(),"Water section not playing")
			check(sprite.sprite_frames.get_frame_count(sprite.animation)==16,"Wrong frame count")
			check(is_equal_approx(sprite.sprite_frames.get_animation_speed(sprite.animation),16.0/1.2),"Wrong period")
			var origin = Vector2i(sprite.global_position)
			for y in range(128+rise):
				for x in range(64):
					var a = before.get_pixelv(origin+Vector2i(x,y))
					var b = after.get_pixelv(origin+Vector2i(x,y))
					if not is_equal_approx(a.a,b.a): alpha_changes += 1
					if a.a<0.99: continue
					pixels += 1
					if abs(a.r-b.r)+abs(a.g-b.g)+abs(a.b-b.b)>0.015:
						var role = 0 if y<59 else (1 if y<64+rise-4 else (2 if y<64+rise+15 else 3))
						counts[role] += 1
		check(alpha_changes==0,"Unstable waterfall footprint")
		check(pixels>1000,"Blank waterfall")
		for count in counts: check(count>20,"Static waterfall animation role")
		results.append({"assembly":assembly.name,"risePixels":rise,"changedPixelsByRole":counts,"alphaChanges":alpha_changes})
	check(before.save_png(OUTPUT+"waterfall_sections_a.png")==OK,"Save first capture")
	check(after.save_png(OUTPUT+"waterfall_sections_b.png")==OK,"Save second capture")
	var file = FileAccess.open(OUTPUT+"waterfall_sections_render.json",FileAccess.WRITE)
	file.store_string(JSON.stringify({"status":"passed" if errors.is_empty() else "failed","roles":["upstream","curtain_lip","impact_foam","pool"],"results":results,"errors":errors,"visualApproval":false,"terrainIncluded":false},"  "))
	viewport.free()
	print("WATERFALL RENDER ","PASSED" if errors.is_empty() else "FAILED")
	quit(0 if errors.is_empty() else 1)
