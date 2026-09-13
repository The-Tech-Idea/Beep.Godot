extends SceneTree

const BASE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/elevation/staging/isometric_tiers_v1/"
const OUTPUT = "res://addons/beep_game_builder_cs/generated/test/library/output/"

func _initialize() -> void:
	call_deferred("run")

func capture(viewport: SubViewport) -> Image:
	await process_frame
	await RenderingServer.frame_post_draw
	return viewport.get_texture().get_image()

func different(a: Color,b: Color) -> bool:
	return abs(a.r-b.r)+abs(a.g-b.g)+abs(a.b-b.b)+abs(a.a-b.a)>0.02

func run() -> void:
	var errors = []
	var results = []
	var manifest = JSON.parse_string(FileAccess.get_file_as_string(BASE+"manifest.json"))
	for entry in manifest.cases:
		var viewport = SubViewport.new()
		viewport.size = Vector2i(512,512)
		viewport.transparent_bg = true
		viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
		root.add_child(viewport)
		var scene = load(BASE+entry.scene).instantiate()
		scene.position = Vector2(160,256)
		viewport.add_child(scene)
		var images = []
		var surface_images = []
		for tier in scene.get_children():
			if tier.get_node("SurfacePlane").refresh()!="": errors.append("Invalid surface plane")
			tier.visible = false
		for tier in scene.get_children():
			tier.visible = true
			images.append(await capture(viewport))
			tier.get_node("CliffWalls").visible = false
			surface_images.append(await capture(viewport))
			tier.get_node("CliffWalls").visible = true
			tier.visible = false
		for tier in scene.get_children(): tier.visible = true
		var full = await capture(viewport)
		var sorting_mismatches = 0
		var overlap_samples = 0
		for y in range(512):
			for x in range(512):
				var highest = -1
				var occupied = 0
				for i in range(images.size()):
					if images[i].get_pixel(x,y).a>0.99:
						highest = i
						occupied += 1
				if highest<0: continue
				if occupied>1: overlap_samples += 1
				# Ignore antialiased upper outlines when comparing opaque ownership.
				var clear_above = true
				for i in range(highest+1,images.size()):
					if images[i].get_pixel(x,y).a>0: clear_above = false
				if clear_above and different(full.get_pixel(x,y),images[highest].get_pixel(x,y)): sorting_mismatches += 1
		var alignment_mismatches = 0
		var alignment_samples = 0
		var contact_gaps = 0
		var contact_samples = 0
		for i in range(1,entry.tiers.size()):
			var definition = entry.tiers[i]
			var tier = scene.get_node(definition.node)
			var walls: TileMapLayer = tier.get_node("CliffWalls")
			var expected = walls.map_to_local(Vector2i(definition.cell[0],definition.cell[1]))-walls.map_to_local(Vector2i.ZERO)-Vector2(0,definition.baseElevationPixels)
			if tier.position!=expected: errors.append("Native tier anchor mismatch")
			var size = Vector2i(definition.dimensions[0],definition.dimensions[1])
			var front = walls.map_to_local(size-Vector2i.ONE)+Vector2(0,16)
			var ends = [walls.map_to_local(Vector2i(0,size.y-1))+Vector2(-32,0),walls.map_to_local(Vector2i(size.x-1,0))+Vector2(32,0)]
			for end in ends:
				for step in range(4,29):
					var point = scene.position+tier.position+front.lerp(end,step/32.0)
					for dy in [-1,0,1]:
						contact_samples += 1
						if full.get_pixel(int(point.x),int(point.y)+dy).a<0.99: contact_gaps += 1
			for y in range(512-int(definition.baseElevationPixels)):
				for x in range(512):
					var upper = surface_images[i].get_pixel(x,y)
					var lower = surface_images[0].get_pixel(x,y+int(definition.baseElevationPixels))
					if upper.a<0.99 or lower.a<0.99: continue
					alignment_samples += 1
					if different(upper,lower): alignment_mismatches += 1
		if sorting_mismatches: errors.append("Tier sorting mismatch")
		if contact_gaps: errors.append("Gaps at tier contact")
		if alignment_mismatches: errors.append("Surface restarted across elevation")
		if overlap_samples<1000 or alignment_samples<1000: errors.append("Insufficient tier comparison coverage")
		if full.save_png(OUTPUT+"isometric_"+str(entry.scene).replace(".tscn",".png"))!=OK: errors.append("Capture failed")
		results.append({"scene":entry.scene,"overlapSamples":overlap_samples,"sortingMismatches":sorting_mismatches,"surfaceAlignmentSamples":alignment_samples,"surfaceAlignmentMismatches":alignment_mismatches,"contactSamples":contact_samples,"contactGaps":contact_gaps})
		viewport.free()
	var report = FileAccess.open(OUTPUT+"isometric_tiers_render.json",FileAccess.WRITE)
	report.store_string(JSON.stringify({"status":"passed" if errors.is_empty() else "failed","errors":errors,"results":results,"visualApproval":false},"  "))
	print("ISOMETRIC TIERS RENDER ","PASSED" if errors.is_empty() else "FAILED")
	quit(0 if errors.is_empty() else 1)
