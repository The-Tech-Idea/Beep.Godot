extends SceneTree

const BASE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/ground_masks/staging/"
const OUTPUT = "res://addons/beep_game_builder_cs/generated/test/library/output/"
var errors: Array[String] = []

func _initialize() -> void:
	call_deferred("run")

func check(value: bool, reason: String) -> void:
	if not value:
		errors.append(reason)
		push_error(reason)

func capture(viewport: SubViewport) -> Image:
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	await process_frame
	await RenderingServer.frame_post_draw
	return viewport.get_texture().get_image()

func changed_samples(a: Image, b: Image) -> int:
	var count = 0
	for y in range(40, 800, 3):
		for x in range(40, 1100, 3):
			var first = a.get_pixel(x,y)
			var second = b.get_pixel(x,y)
			if abs(first.r-second.r) + abs(first.g-second.g) + abs(first.b-second.b) + abs(first.a-second.a) > 0.02:
				count += 1
	return count

func run() -> void:
	DirAccess.make_dir_recursive_absolute(OUTPUT)
	var results: Array = []
	var surface_art = "--surface-art" in OS.get_cmdline_user_args()
	var prefix = "surface_" if surface_art else "mask_"
	for projection in ["square", "isometric"]:
		var viewport = SubViewport.new()
		viewport.size = Vector2i(1200, 900)
		viewport.transparent_bg = true
		root.add_child(viewport)
		var folder = BASE + projection + ("/surface_candidate_v1/" if surface_art else "/")
		var packed = load(folder + "grass_dirt_review.tscn") as PackedScene
		if packed == null:
			check(false, "Missing fixture " + projection)
			viewport.free()
			continue
		var scene = packed.instantiate()
		viewport.add_child(scene)
		var layer = scene.get_node("Ground") as TileMapLayer
		if projection == "isometric":
			var origin = layer.map_to_local(Vector2i.ZERO)
			check(layer.map_to_local(Vector2i.RIGHT) - origin == Vector2(32, 16), "Isometric X axis does not match material plane")
			check(layer.map_to_local(Vector2i.DOWN) - origin == Vector2(-32, 16), "Isometric Y axis does not match material plane")
		layer.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
		var material = layer.material.duplicate() as ShaderMaterial
		layer.material = material
		var pattern = Image.create(8, 8, false, Image.FORMAT_RGBA8)
		for y in range(8):
			for x in range(8):
				pattern.set_pixel(x, y, Color(0.2 + x * 0.07, 0.3 + y * 0.07, 0.25, 1))
		if not surface_art:
			material.set_shader_parameter("surface_texture", ImageTexture.create_from_image(pattern))
		var offset = Vector2(500, 100) if projection == "isometric" else Vector2(60, 60)
		viewport.canvas_transform = Transform2D(0, offset)
		var before = await capture(viewport)
		check(not before.is_empty(), projection + " empty render")
		check(before.save_png(OUTPUT + prefix + projection + "_native.png") == OK, "Cannot save capture")
		var split_changes = -1
		var transform_changes = -1
		if surface_art:
			var second = TileMapLayer.new()
			second.name = "SecondGround"
			second.tile_set = layer.tile_set
			second.material = layer.material
			second.texture_filter = layer.texture_filter
			scene.add_child(second)
			for cell in layer.get_used_cells():
				if cell.x % 2 == 0:
					second.set_cell(cell, layer.get_cell_source_id(cell), layer.get_cell_atlas_coords(cell), layer.get_cell_alternative_tile(cell))
					layer.erase_cell(cell)
			var plane = scene.get_node("SurfacePlane")
			plane.layer_paths.assign([NodePath("../Ground"), NodePath("../SecondGround")])
			check(plane.refresh() == "", "Shared plane binding failed")
			var split = await capture(viewport)
			split_changes = changed_samples(before, split)
			check(split_changes == 0, "Splitting ground across layers changed the surface")
			var original_canvas = viewport.canvas_transform
			scene.transform = Transform2D(0.31, Vector2(1.2, 0.8), 0.0, Vector2(83, -41))
			viewport.canvas_transform = original_canvas * scene.transform.affine_inverse()
			check(plane.refresh() == "", "Transformed plane binding failed")
			var transformed = await capture(viewport)
			transform_changes = changed_samples(before, transformed)
			check(transform_changes == 0, "Map transform changed the material-plane appearance")
			scene.transform = Transform2D.IDENTITY
			viewport.canvas_transform = original_canvas
			check(plane.refresh() == "", "Plane restore failed")
		var shift = Vector2(17, 23)
		viewport.canvas_transform = Transform2D(0, offset + shift)
		var after = await capture(viewport)
		var compared = 0
		var changed = 0
		var opaque = 0
		var colors = {}
		for y in range(40, 800, 3):
			for x in range(40, 1100, 3):
				var a = before.get_pixel(x, y)
				var b = after.get_pixel(x + 17, y + 23)
				if a.a > 0.99:
					opaque += 1
					colors[a.to_rgba32()] = true
				compared += 1
				if abs(a.r - b.r) + abs(a.g - b.g) + abs(a.b - b.b) + abs(a.a - b.a) > 0.02:
					changed += 1
		check(opaque > 1000, projection + " blank or incomplete rendered terrain")
		check(colors.size() > 10, projection + " shared diagnostic surface did not render")
		check(changed == 0, projection + " surface slides when camera moves: " + str(changed))
		viewport.canvas_transform = Transform2D(Vector2(2, 0), Vector2(0, 2), offset)
		var magnified = await capture(viewport)
		check(magnified.save_png(OUTPUT + prefix + projection + "_2x.png") == OK, "Cannot save magnified capture")
		results.append({"projection": projection, "compared_pixels": compared, "camera_changed_pixels": changed, "opaque_samples": opaque, "colors": colors.size(), "split_layer_changed_pixels": split_changes, "map_transform_changed_pixels": transform_changes})
		viewport.free()
	var report = {"status": "passed" if errors.is_empty() else "failed", "results": results, "errors": errors, "visual_approval": false}
	var file = FileAccess.open(OUTPUT + prefix + "render.json", FileAccess.WRITE)
	file.store_string(JSON.stringify(report, "  "))
	print("MASK RENDER ", report.status.to_upper())
	quit(0 if errors.is_empty() else 1)
