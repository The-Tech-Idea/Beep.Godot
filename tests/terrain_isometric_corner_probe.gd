extends SceneTree

const BASE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/elevation/staging/grass_granite_iso_corner_v1/"
const OUTPUT = "res://addons/beep_game_builder_cs/generated/test/library/output/"

func _initialize() -> void:
	call_deferred("run")

func capture(viewport: SubViewport) -> Image:
	await process_frame
	await RenderingServer.frame_post_draw
	return viewport.get_texture().get_image()

func run() -> void:
	var errors = []
	var source_image = Image.load_from_file(BASE+"runtime/corner_64.png")
	var interior_holes = 0
	for y in range(4,28):
		for x in range(4,60):
			if abs(x+0.5-32)/32.0+abs(y+0.5-16)/16.0<0.8 and source_image.get_pixel(x,y).a<0.99:
				interior_holes += 1
	if interior_holes!=0: errors.append("Chroma removal cut holes in plateau interior")
	var viewport = SubViewport.new()
	viewport.size = Vector2i(192,192)
	viewport.transparent_bg = true
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(viewport)
	var scene = load(BASE+"corner_review.tscn").instantiate()
	scene.position = Vector2(96,128)
	viewport.add_child(scene)
	var layer: TileMapLayer = scene.get_node("Corner")
	var plane = scene.get_node("SurfacePlane")
	if plane.refresh()!="": errors.append("Invalid corner surface plane")
	var painted = await capture(viewport)
	layer.material.set_shader_parameter("surface_enabled",false)
	var native = await capture(viewport)
	layer.visible = false
	var sprite = Sprite2D.new()
	sprite.texture = load(BASE+"runtime/corner_64.res")
	sprite.centered = false
	sprite.position = layer.map_to_local(Vector2i.ZERO)+Vector2(-32,-80)
	sprite.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	scene.add_child(sprite)
	var reference = await capture(viewport)
	var mismatches = 0
	var opaque = 0
	for y in range(192):
		for x in range(192):
			var a = native.get_pixel(x,y)
			var b = reference.get_pixel(x,y)
			if a.a>0.99: opaque += 1
			if abs(a.r-b.r)+abs(a.g-b.g)+abs(a.b-b.b)+abs(a.a-b.a)>0.015: mismatches += 1
	if mismatches!=0: errors.append("Native tile does not match declared ground anchor")
	if opaque<4000: errors.append("Corner missing or clipped")
	if layer.tile_set.tile_size!=Vector2i(64,32): errors.append("Incorrect diamond footprint")
	if painted.save_png(OUTPUT+"isometric_corner.png")!=OK: errors.append("Capture failed")
	var file = FileAccess.open(OUTPUT+"isometric_corner_render.json",FileAccess.WRITE)
	file.store_string(JSON.stringify({"status":"passed" if errors.is_empty() else "failed","errors":errors,"nativeAnchorMismatches":mismatches,"plateauHoles":interior_holes,"opaquePixels":opaque,"visualApproval":false,"neighborJoinsValidated":false},"  "))
	viewport.free()
	print("ISOMETRIC CORNER RENDER ","PASSED" if errors.is_empty() else "FAILED")
	quit(0 if errors.is_empty() else 1)
