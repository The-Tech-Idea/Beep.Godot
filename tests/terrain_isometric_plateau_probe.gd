extends SceneTree

const BASE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/elevation/staging/isometric_plateaus_v1/"
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
	var base = BASE
	var prefix = "isometric_plateau"
	var manifest = JSON.parse_string(FileAccess.get_file_as_string(base+"manifest.json"))
	if "--long-faces" in OS.get_cmdline_user_args():
		base = "res://addons/beep_game_builder_cs/generated/dev/cartoon/elevation/staging/isometric_granite_faces_v1/"
		prefix = "isometric_long_face"
		manifest = {"cases":[{"scene":"faces_review.tscn","dimensions":[4,4]}]}
	for entry in manifest.cases:
		var viewport = SubViewport.new()
		viewport.size = Vector2i(512,384)
		viewport.transparent_bg = true
		viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
		root.add_child(viewport)
		var scene = load(base+entry.scene).instantiate()
		scene.position = Vector2(192,128)
		viewport.add_child(scene)
		var walls: TileMapLayer = scene.get_node("CliffWalls")
		var top: TileMapLayer = scene.get_node("PlateauSurface")
		if scene.get_node("SurfacePlane").refresh()!="": errors.append("Invalid surface binding")
		var full = await capture(viewport)
		walls.visible = false
		for name in ["LightFace","ShadeFace"]:
			if scene.has_node(name): scene.get_node(name).visible = false
		var surface = await capture(viewport)
		walls.visible = true
		for name in ["LightFace","ShadeFace"]:
			if scene.has_node(name): scene.get_node(name).visible = true
		var mismatch = 0
		var sampled = 0
		for y in range(384):
			for x in range(512):
				var a = surface.get_pixel(x,y)
				if a.a<0.99: continue
				sampled += 1
				var b = full.get_pixel(x,y)
				if abs(a.r-b.r)+abs(a.g-b.g)+abs(a.b-b.b)+abs(a.a-b.a)>0.015: mismatch += 1
		var w = int(entry.dimensions[0])
		var d = int(entry.dimensions[1])
		var points = PackedVector2Array([
			walls.map_to_local(Vector2i.ZERO)+Vector2(0,-80),
			walls.map_to_local(Vector2i(w-1,0))+Vector2(32,-64),
			walls.map_to_local(Vector2i(w-1,0))+Vector2(32,0),
			walls.map_to_local(Vector2i(w-1,d-1))+Vector2(0,16),
			walls.map_to_local(Vector2i(0,d-1))+Vector2(-32,0),
			walls.map_to_local(Vector2i(0,d-1))+Vector2(-32,-64)])
		var holes = 0
		for y in range(384):
			for x in range(512):
				var p = Vector2(x+0.5,y+0.5)-scene.position
				if not Geometry2D.is_point_in_polygon(p,points): continue
				var near_edge = false
				for i in range(points.size()):
					if p.distance_to(Geometry2D.get_closest_point_to_segment(p,points[i],points[(i+1)%points.size()]))<2.0: near_edge = true
				if not near_edge and full.get_pixel(x,y).a<0.99: holes += 1
		if sampled<w*d*900: errors.append("Missing plateau surface")
		if mismatch!=0: errors.append("Internal cliff rim occludes plateau")
		if holes!=0: errors.append("Gaps inside assembled cliff silhouette")
		if full.save_png(OUTPUT+prefix+"_%dx%d.png" % [w,d])!=OK: errors.append("Capture failed")
		results.append({"dimensions":[w,d],"surfaceSamples":sampled,"surfaceMismatches":mismatch,"interiorAlphaGaps":holes})
		viewport.free()
	var report = FileAccess.open(OUTPUT+prefix+"s_render.json",FileAccess.WRITE)
	report.store_string(JSON.stringify({"status":"passed" if errors.is_empty() else "failed","errors":errors,"results":results,"visualApproval":false},"  "))
	print("ISOMETRIC PLATEAU RENDER ","PASSED" if errors.is_empty() else "FAILED")
	quit(0 if errors.is_empty() else 1)
