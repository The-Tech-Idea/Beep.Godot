extends SceneTree

const BASE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/elevation/staging/isometric_shapes_v1/"
const OUTPUT = "res://addons/beep_game_builder_cs/generated/test/library/output/"
const SIZE = 640

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
		viewport.size = Vector2i(SIZE,SIZE)
		viewport.transparent_bg = true
		viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
		root.add_child(viewport)
		var scene = load(BASE+entry.scene).instantiate()
		scene.position = Vector2(288,192)
		viewport.add_child(scene)
		var walls: TileMapLayer = scene.get_node("CliffWalls")
		var top: TileMapLayer = scene.get_node("PlateauSurface")
		if walls.get_used_cells().size()!=entry.cells.size() or top.get_used_cells().size()!=entry.cells.size(): errors.append("Saved footprint mismatch")
		if scene.get_node("SurfacePlane").refresh()!="": errors.append("Invalid surface plane")
		var full = await capture(viewport)
		walls.visible = false
		var surface = await capture(viewport)
		var missing_centers = 0
		var filled_openings = 0
		for pair in entry.cells:
			var cell = Vector2i(pair[0],pair[1])
			var p = Vector2i(scene.position+top.position+top.map_to_local(cell))
			if surface.get_pixelv(p).a<0.99: missing_centers += 1
		for pair in entry.openings:
			var cell = Vector2i(pair[0],pair[1])
			var p = Vector2i(scene.position+top.position+top.map_to_local(cell))
			if surface.get_pixelv(p).a>0: filled_openings += 1
		# Rasterize the union of projected solid columns, then erode only its exterior.
		# This includes internal seams instead of excluding each individual tile edge.
		var expected = PackedByteArray()
		expected.resize(SIZE*SIZE)
		for pair in entry.cells:
			var center = scene.position+walls.map_to_local(Vector2i(pair[0],pair[1]))
			for y in range(int(center.y)-80,int(center.y)+16):
				for x in range(int(center.x)-32,int(center.x)+32):
					var dx = abs(x+0.5-center.x)
					var dy = y+0.5-center.y
					if dy>=-80+dx*0.5 and dy<=16-dx*0.5: expected[y*SIZE+x]=1
		var alpha_gaps = 0
		var interior_samples = 0
		var surface_mismatches = 0
		var unexpected_fill = 0
		for y in range(2,SIZE-2):
			for x in range(2,SIZE-2):
				var interior = true
				var exterior = true
				for dy in range(-2,3):
					for dx in range(-2,3):
						if expected[(y+dy)*SIZE+x+dx]==0: interior = false
						else: exterior = false
				if exterior and full.get_pixel(x,y).a>0.05: unexpected_fill += 1
				if interior:
					interior_samples += 1
					if full.get_pixel(x,y).a<0.99: alpha_gaps += 1
				var a = surface.get_pixel(x,y)
				if a.a<0.99: continue
				var b = full.get_pixel(x,y)
				if abs(a.r-b.r)+abs(a.g-b.g)+abs(a.b-b.b)+abs(a.a-b.a)>0.015: surface_mismatches += 1
		if missing_centers or filled_openings: errors.append("Incorrect top footprint")
		if alpha_gaps: errors.append("Gaps inside concave assembly")
		if unexpected_fill: errors.append("Unexpected geometry outside column union")
		if surface_mismatches: errors.append("Wall occludes plateau")
		if interior_samples<5000: errors.append("Insufficient geometry samples")
		if full.save_png(OUTPUT+"isometric_shape_"+str(entry.scene).replace(".tscn",".png"))!=OK: errors.append("Capture failed")
		results.append({"scene":entry.scene,"cells":entry.cells.size(),"openings":entry.openings.size(),"missingCenters":missing_centers,"filledOpenings":filled_openings,"interiorSamples":interior_samples,"alphaGaps":alpha_gaps,"unexpectedFill":unexpected_fill,"surfaceMismatches":surface_mismatches})
		viewport.free()
	var report = FileAccess.open(OUTPUT+"isometric_shapes_render.json",FileAccess.WRITE)
	report.store_string(JSON.stringify({"status":"passed" if errors.is_empty() else "failed","errors":errors,"results":results,"visualApproval":false},"  "))
	print("ISOMETRIC SHAPES RENDER ","PASSED" if errors.is_empty() else "FAILED")
	quit(0 if errors.is_empty() else 1)
