extends SceneTree

const BASE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/elevation/staging/isometric_granite_faces_v1/"
const OUTPUT = "res://addons/beep_game_builder_cs/generated/test/library/output/"

func _initialize() -> void:
	call_deferred("run")

func capture(viewport: SubViewport) -> Image:
	await process_frame
	await RenderingServer.frame_post_draw
	return viewport.get_texture().get_image()

func run() -> void:
	var errors = []
	var viewport = SubViewport.new()
	viewport.size = Vector2i(512,384)
	viewport.transparent_bg = true
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(viewport)
	var native = load(BASE+"native_faces_review.tscn").instantiate()
	native.position = Vector2(192,128)
	viewport.add_child(native)
	if native.get_node("SurfacePlane").refresh()!="": errors.append("Invalid surface plane")
	var samples = 0
	for face in range(2):
		var layer: TileMapLayer = native.get_node("LightFace" if face==0 else "ShadeFace")
		if layer.tile_set.get_patterns_count()!=2: errors.append("Missing patterns")
		if layer.get_used_cells().size()!=4: errors.append("Pattern footprint mismatch")
		for cell in layer.get_used_cells():
			var data = layer.get_cell_tile_data(cell)
			if data==null: errors.append("Invalid native tile"); continue
			if data.get_custom_data("visual_rise_pixels")!=64: errors.append("Incorrect visual rise")
			if data.get_custom_data("art_face")!=("light" if face==0 else "shade"): errors.append("Incorrect face metadata")
			if layer.local_to_map(layer.map_to_local(cell))!=cell: errors.append("Picking transform mismatch")
			samples += 1
	var actual = await capture(viewport)
	var light: TileMapLayer = native.get_node("LightFace")
	light.erase_cell(Vector2i(1,3))
	var erased = await capture(viewport)
	light.set_pattern(Vector2i(0,3),light.tile_set.get_pattern(0))
	var restored = await capture(viewport)
	var erased_changes = 0
	var restore_mismatches = 0
	for y in range(384):
		for x in range(512):
			var a = actual.get_pixel(x,y)
			var b = erased.get_pixel(x,y)
			var c = restored.get_pixel(x,y)
			if abs(a.a-b.a)>0.05: erased_changes += 1
			if abs(a.r-c.r)+abs(a.g-c.g)+abs(a.b-c.b)+abs(a.a-c.a)>0.015: restore_mismatches += 1
	if erased_changes<1000: errors.append("Erasure did not remove the face module")
	if restore_mismatches: errors.append("Pattern repaint did not restore the assembly")
	native.visible = false
	var reference = load(BASE+"faces_review.tscn").instantiate()
	reference.position = native.position
	viewport.add_child(reference)
	if reference.get_node("SurfacePlane").refresh()!="": errors.append("Invalid reference surface")
	var expected = await capture(viewport)
	var mismatch = 0
	var opaque = 0
	for y in range(384):
		for x in range(512):
			var a = actual.get_pixel(x,y)
			var b = expected.get_pixel(x,y)
			if a.a>0.99: opaque += 1
			if abs(a.r-b.r)+abs(a.g-b.g)+abs(a.b-b.b)+abs(a.a-b.a)>0.015: mismatch += 1
	if mismatch: errors.append("Native face offsets differ from sprite reference")
	if opaque<30000: errors.append("Missing terrain render")
	if actual.save_png(OUTPUT+"isometric_native_faces.png")!=OK: errors.append("Capture failed")
	var file = FileAccess.open(OUTPUT+"isometric_native_faces_render.json",FileAccess.WRITE)
	file.store_string(JSON.stringify({"status":"passed" if errors.is_empty() else "failed","errors":errors,"nativeTilesChecked":samples,"referenceMismatches":mismatch,"opaquePixels":opaque,"erasedChangedPixels":erased_changes,"repaintMismatches":restore_mismatches,"visualApproval":false},"  "))
	viewport.free()
	print("NATIVE ISOMETRIC FACE RENDER ","PASSED" if errors.is_empty() else "FAILED")
	quit(0 if errors.is_empty() else 1)
