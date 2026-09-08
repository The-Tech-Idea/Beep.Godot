extends SceneTree

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	root.size = Vector2i(768, 576)
	var smoke: Node = load("res://tests/TerrainShorelineContourSmoke.cs").new()
	var cells: Node = load("res://addons/beep_game_builder_cs/ecs/grid/GridCellDataComponent.cs").new()
	cells.name = "Cells"
	root.add_child(cells)
	var painter: Node2D = load("res://addons/beep_game_builder_cs/ecs/terrain/TerrainPaintedRendererComponent.cs").new()
	painter.set("RefreshOnReady", false)
	painter.set("CellDataPath", NodePath("../Cells"))
	painter.set("BoundsSize", Vector2i(24, 18))
	painter.set("TileSize", 32)
	root.add_child(painter)
	var output := "res://tests/output/lake_banks/"
	DirAccess.make_dir_recursive_absolute(output)
	for width in [0.0, 0.25, 1.0, 2.0]:
		smoke.call("PopulateLakeFixture", cells, width)
		for style in ["original", "pixel_art", "cartoon"]:
			painter.set("MapArt", null if style == "original" else load("res://addons/beep_game_builder_cs/textures/map_art/" + style + ".tres"))
			painter.call("Rebuild")
			await process_frame
			if DisplayServer.get_name() == "headless": continue
			var material: ShaderMaterial = painter.get_node("SplatSurface").material
			material.set_shader_parameter("contour_debug", true)
			await RenderingServer.frame_post_draw
			var image := root.get_texture().get_image()
			assert(image.save_png(output + style + "_" + str(width) + ".png") == OK)
			# Independent radial oracle: all angles, not just gameplay-cell centres.
			var checks := 0
			for degrees in range(0, 360, 2):
				var direction := Vector2.from_angle(deg_to_rad(degrees))
				for radius in [4.18, 4.5, 4.85, 5.15, 5.8, 6.2]:
					if absf(radius - 4.0 - width) < 0.18: continue
					var point := Vector2i((Vector2(11, 9) + direction * radius) * 32.0)
					var color := image.get_pixelv(point)
					assert((color.r > color.g) == (width > 0.0 and radius < 4.0 + width), "Lake bank is not a circular inset at " + str(point))
					checks += 1
			# River must not inherit lake banks, and ocean keeps its own width.
			assert(image.get_pixel(20 * 32 - 8, 9 * 32).g > image.get_pixel(20 * 32 - 8, 9 * 32).r)
			assert(image.get_pixel(2 * 32 + 8, 9 * 32).r > image.get_pixel(2 * 32 + 8, 9 * 32).g)
			print("[lake-banks] ", style, " width=", width, " GPU radial checks=", checks)
	painter.free()
	cells.free()
	smoke.free()
	print("[terrain-lake-bank] OK")
	quit()
