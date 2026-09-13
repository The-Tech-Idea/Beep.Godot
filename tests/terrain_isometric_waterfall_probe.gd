extends SceneTree

const BASE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/isometric_waterfall_v1/"
const OUTPUT = "res://addons/beep_game_builder_cs/generated/test/library/output/"

func _initialize() -> void:
	call_deferred("run")

func capture(viewport: SubViewport) -> Image:
	await process_frame
	await RenderingServer.frame_post_draw
	return viewport.get_texture().get_image()

func run() -> void:
	var errors = []
	var results = []
	var manifest = JSON.parse_string(FileAccess.get_file_as_string(BASE+"manifest.json"))
	for entry in manifest.cases:
		var viewport = SubViewport.new()
		viewport.size = Vector2i(384,256)
		viewport.transparent_bg = true
		viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
		root.add_child(viewport)
		var scene = load(BASE+manifest.showcase).instantiate()
		var assembly = scene.get_node(entry.node)
		scene.remove_child(assembly)
		scene.free()
		assembly.position = Vector2(48,40)
		viewport.add_child(assembly)
		for sprite in assembly.get_children():
			sprite.stop()
			sprite.frame = 0
			if sprite.sprite_frames.get_frame_count(sprite.animation)!=16: errors.append("Frame count mismatch")
			if abs(sprite.sprite_frames.get_animation_speed(sprite.animation)-16.0/1.2)>0.001: errors.append("Animation period mismatch")
		var first = await capture(viewport)
		for sprite in assembly.get_children(): sprite.frame = 5
		var second = await capture(viewport)
		var changed = 0
		var alpha_changes = 0
		var opaque = 0
		var clipped = 0
		for y in range(256):
			for x in range(384):
				var a = first.get_pixel(x,y)
				var b = second.get_pixel(x,y)
				if a.a>0.99: opaque += 1
				if abs(a.a-b.a)>0.001: alpha_changes += 1
				if abs(a.r-b.r)+abs(a.g-b.g)+abs(a.b-b.b)>0.02: changed += 1
				if (x==0 or y==0 or x==383 or y==255) and a.a>0: clipped += 1
		if opaque<1000 or changed<100: errors.append("Missing visible animated water")
		if alpha_changes: errors.append("Water silhouette changed")
		if clipped: errors.append("Water assembly clipped")
		if entry.width==3 and entry.risePixels==64:
			first.save_png(OUTPUT+"iso_fall_"+str(entry.direction)+"_frame0.png")
			second.save_png(OUTPUT+"iso_fall_"+str(entry.direction)+"_frame5.png")
		results.append({"case":entry.node,"opaquePixels":opaque,"changedPixels":changed,"alphaChanges":alpha_changes,"clippedPixels":clipped})
		viewport.free()
	var file = FileAccess.open(OUTPUT+"isometric_waterfall_render.json",FileAccess.WRITE)
	file.store_string(JSON.stringify({"status":"passed" if errors.is_empty() else "failed","errors":errors,"results":results,"visualApproval":false},"  "))
	print("ISOMETRIC WATERFALL RENDER ","PASSED" if errors.is_empty() else "FAILED")
	quit(0 if errors.is_empty() else 1)
