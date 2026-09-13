extends SceneTree

const SCENE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/elevation/staging/grass_granite_front_v1/river_waterfall_review.tscn"
const OUTPUT = "res://addons/beep_game_builder_cs/generated/test/library/output/"

func _initialize() -> void:
	call_deferred("run")

func capture(viewport: SubViewport) -> Image:
	await process_frame
	await RenderingServer.frame_post_draw
	return viewport.get_texture().get_image()

func run() -> void:
	var errors = []
	var lake_route = "--lake-route" in OS.get_cmdline_user_args()
	var viewport = SubViewport.new()
	viewport.size = Vector2i(384,800 if lake_route else 384)
	viewport.transparent_bg = true
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(viewport)
	var scene = load(SCENE.replace("river_waterfall_review.tscn","river_waterfall_lake_review.tscn") if lake_route else SCENE).instantiate()
	scene.position = Vector2(32,96)
	viewport.add_child(scene)
	var before = await capture(viewport)
	await create_timer(0.35).timeout
	var after = await capture(viewport)
	var lake_motion = 0
	if lake_route:
		var lake = scene.get_node("LowerLake")
		if lake.get_cell_source_id(Vector2i(2,5))!=1: errors.append("Missing explicit lake inlet")
		for y in range(416,672):
			for x in range(96,288):
				var a = before.get_pixel(x,y)
				var b = after.get_pixel(x,y)
				if abs(a.r-b.r)+abs(a.g-b.g)+abs(a.b-b.b)>0.015: lake_motion += 1
		if lake_motion<50: errors.append("Lake is not animated")
	for child in scene.get_children():
		if child is AnimatedSprite2D:
			child.pause()
			child.set_frame_and_progress(0,0)
	var masked = await capture(viewport)
	var cliff = scene.get_node("StaticCliff")
	cliff.visible = false
	var grass_reference = await capture(viewport)
	cliff.visible = true
	cliff.material.set_shader_parameter("surface_enabled",false)
	var original = await capture(viewport)
	cliff.material.set_shader_parameter("surface_enabled",true)
	var mask = Image.load_from_file("res://addons/beep_game_builder_cs/generated/dev/cartoon/elevation/staging/grass_granite_front_v1/runtime/grass_surface_mask.png")
	var grass_mismatches = 0
	var grass_samples = 0
	var rock_changes = 0
	for y in range(80):
		for x in range(320):
			if x>=128 and x<192: continue
			var point = Vector2i(x+32,y+144)
			var actual = masked.get_pixelv(point)
			var reference = grass_reference.get_pixelv(point)
			if y<12 and mask.get_pixel(x,y).r>0.99:
				grass_samples += 1
				if abs(actual.r-reference.r)+abs(actual.g-reference.g)+abs(actual.b-reference.b)>0.015:
					grass_mismatches += 1
			if y>=24:
				var rock = original.get_pixelv(point)
				if abs(actual.r-rock.r)+abs(actual.g-rock.g)+abs(actual.b-rock.b)>0.015: rock_changes += 1
	scene.position += Vector2(16,8)
	viewport.canvas_transform = Transform2D(0,Vector2(-16,-8))
	var translated = await capture(viewport)
	var sliding = 0
	for y in range(32,352):
		for x in range(32,352):
			var a = masked.get_pixel(x,y)
			var b = translated.get_pixel(x,y)
			if abs(a.r-b.r)+abs(a.g-b.g)+abs(a.b-b.b)>0.015: sliding += 1
	if grass_samples<1000 or grass_mismatches!=0: errors.append("Grass rim does not match shared surface")
	if rock_changes!=0: errors.append("Grass mask changes rock pixels")
	if sliding!=0: errors.append("Surface slides when the scene moves")
	var fixed_changes = 0
	var motion = [0,0,0,0]
	for y in range(32,352):
		for x in range(32,352):
			var a = before.get_pixel(x,y)
			var b = after.get_pixel(x,y)
			var changed = abs(a.r-b.r)+abs(a.g-b.g)+abs(a.b-b.b)>0.015
			if x<160 or x>=224:
				if changed: fixed_changes += 1
			elif changed:
				var local_y = y-96
				var role = 0 if local_y<64 else (1 if local_y<124 else (2 if local_y<150 else 3))
				motion[role] += 1
	if fixed_changes!=0: errors.append("Cliff or ground moved")
	for count in motion:
		if count<20: errors.append("Static water role")
	if scene.get_node("StaticCliff").position.y+16!=64: errors.append("Incorrect lip alignment")
	if scene.get_node("StaticCliff").position.y+80!=128: errors.append("Incorrect contact alignment")
	var contact = scene.get_node("CliffBottomContact")
	if contact.texture.get_size()!=Vector2(320,40): errors.append("Incorrect contact footprint")
	if contact.position.y+int(contact.get_meta("nominal_contact_y"))!=128: errors.append("Contact changes nominal ground line")
	if int(contact.get_meta("visual_rise_contribution"))!=0: errors.append("Contact changes cliff rise")
	if contact.get_index()>=scene.get_node("WaterfallWaterOnly").get_index(): errors.append("Contact occludes water layer")
	var prefix = "granite_lake_route_" if lake_route else "granite_route_"
	if before.save_png(OUTPUT+prefix+"a.png")!=OK: errors.append("Capture failed")
	if after.save_png(OUTPUT+prefix+"b.png")!=OK: errors.append("Capture failed")
	var file = FileAccess.open(OUTPUT+prefix+"render.json",FileAccess.WRITE)
	file.store_string(JSON.stringify({"status":"passed" if errors.is_empty() else "failed","errors":errors,"fixedTerrainChanges":fixed_changes,"motionByRole":motion,"lakeRoute":lake_route,"lakeChangedPixels":lake_motion,"grassSamples":grass_samples,"grassMismatches":grass_mismatches,"rockChanges":rock_changes,"slidingPixels":sliding,"visualApproval":false,"externalSeamsValidated":false},"  "))
	viewport.free()
	print("GRANITE ROUTE RENDER ","PASSED" if errors.is_empty() else "FAILED")
	quit(0 if errors.is_empty() else 1)
