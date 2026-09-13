extends "res://tests/terrain_mask_connections_probe.gd"

func run() -> void:
	DirAccess.make_dir_recursive_absolute(OUTPUT)
	var results: Array = []
	for projection in ["square", "isometric"]:
		var viewport = SubViewport.new()
		viewport.size = Vector2i(384,384)
		viewport.transparent_bg = true
		viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
		root.add_child(viewport)
		viewport.canvas_transform = Transform2D(0, Vector2(160,160))
		var scene = load(BASE + projection + "/surface_candidate_v1/grass_dirt_review.tscn").instantiate()
		viewport.add_child(scene)
		var layer = scene.get_node("Ground") as TileMapLayer
		var overview = Image.create(384 * 8, 384 * 6, false, Image.FORMAT_RGBA8)
		overview.fill(Color.TRANSPARENT)
		var cases: Array = []
		for mask in range(256):
			if normalize(mask) != mask:
				continue
			layer.clear()
			for y in range(-2,3):
				for x in range(-2,3):
					layer.set_cell(Vector2i(x,y), 0, Vector2i(7,5))
			var cells: Array[Vector2i] = [Vector2i.ZERO]
			for b in range(8):
				if mask & (1 << b):
					cells.append(DIRECTIONS[b])
			layer.set_cells_terrain_connect(cells, 0, 0, false)
			await process_frame
			await RenderingServer.frame_post_draw
			var capture = viewport.get_texture().get_image()
			var gaps = 0
			var exposed_masks = 0
			var samples = 0
			var colors = {}
			for y in range(384):
				for x in range(384):
					var cell = layer.local_to_map(Vector2(x + 0.5, y + 0.5) - Vector2(160,160))
					if abs(cell.x) > 1 or abs(cell.y) > 1:
						continue
					var color = capture.get_pixel(x,y)
					samples += 1
					if color.a < 0.99:
						gaps += 1
					if (color.r > 0.98 and color.g < 0.03 and color.b < 0.03) or (color.b > 0.98 and color.r < 0.03 and color.g < 0.03):
						exposed_masks += 1
					colors[color.to_rgba32()] = true
			var expected_samples = 9216 if projection == "isometric" else 36864
			check(samples == expected_samples, "Rendered samples do not cover the full 3x3 projected footprint")
			check(gaps == 0, "%s mask %s has %s alpha gaps" % [projection,mask,gaps])
			check(exposed_masks == 0, "%s mask %s exposes marker colors" % [projection,mask])
			check(colors.size() > 10, "Surface texture did not render")
			var index = cases.size()
			overview.blit_rect(capture, Rect2i(0,0,384,384), Vector2i(index % 8, index / 8) * 384)
			cases.append({"mask":mask,"overviewCell":[index % 8, index / 8],"samples":samples,"alphaGaps":gaps,"exposedMasks":exposed_masks})
		check(cases.size() == 47, "Incomplete rendered mask set")
		check(overview.save_png(OUTPUT + "all_masks_" + projection + ".png") == OK, "Cannot save mask overview")
		results.append({"projection":projection,"cases":cases})
		viewport.free()
	var file = FileAccess.open(OUTPUT + "all_masks_render.json", FileAccess.WRITE)
	file.store_string(JSON.stringify({"status":"passed" if errors.is_empty() else "failed","results":results,"errors":errors,"visualApproval":false,"limitations":["Checks alpha coverage and exposed markers, not artistic quality or all possible multi-cell arrangements."]}, "  "))
	print("ALL MASK RENDER ", "PASSED" if errors.is_empty() else "FAILED")
	quit(0 if errors.is_empty() else 1)
