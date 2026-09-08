extends SceneTree

const BASE := "res://addons/beep_game_builder_cs/ecs/"

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	assert(DisplayServer.get_name() != "headless")
	var viewport := SubViewport.new()
	viewport.size = Vector2i(512, 256)
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(viewport)
	var cells: Node = load(BASE + "grid/GridCellDataComponent.cs").new()
	cells.name = "Cells"
	viewport.add_child(cells)
	for y in range(4):
		for x in range(8):
			cells.call("SetTerrainKind", Vector2i(x, y), "grass")
			cells.call("SetMetadata", Vector2i(x, y), "terrain_elevation", absf(x - 3.5) / 3.5)
	var view: Node = load(BASE + "terrain/TerrainPaintedRendererComponent.cs").new()
	view.set("RefreshOnReady", false)
	view.set("CellDataPath", NodePath("../Cells"))
	view.set("BoundsSize", Vector2i(8, 4))
	viewport.add_child(view)
	assert(is_equal_approx(float(view.get("ShadeStrength")), 0.35))
	view.call("Rebuild")
	await process_frame
	await process_frame
	var material: ShaderMaterial = view.get_node("SplatSurface").material
	var pixels := Image.create(4, 4, false, Image.FORMAT_RGBA8)
	pixels.fill(Color(0.5, 0.5, 0.5))
	material.set_shader_parameter("tex_grass", ImageTexture.create_from_image(pixels))
	material.set_shader_parameter("tint_grass", Vector3.ONE)
	var ids: PackedByteArray = material.get_shader_parameter("id_map").get_image().get_data()
	var shades: PackedByteArray = material.get_shader_parameter("shade_map").get_image().get_data()
	var spans: Array[float] = []
	for strength in [0.35, 1.0]:
		view.set("ShadeStrength", strength)
		view.call("Rebuild")
		assert(material.get_shader_parameter("id_map").get_image().get_data() == ids)
		assert(material.get_shader_parameter("shade_map").get_image().get_data() == shades)
		await process_frame
		await RenderingServer.frame_post_draw
		var image := viewport.get_texture().get_image()
		var low := 1.0
		var high := 0.0
		for x in range(16, 496):
			var value := image.get_pixel(x, 128).r
			low = minf(low, value)
			high = maxf(high, value)
		spans.append(high - low)
	assert(spans[0] > 0.08 and spans[0] < 0.13, "Default shade removed relief or kept excessive contrast: %s" % str(spans))
	assert(spans[1] > spans[0] * 2.5, "Shade control no longer reaches full relief contrast")
	print("[terrain-painted-shading] default/full brightness spans: ", spans)
	viewport.free()
	print("[terrain-painted-shading] OK")
	quit()
