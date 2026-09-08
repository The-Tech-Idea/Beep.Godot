extends SceneTree

const BASE := "res://addons/beep_game_builder_cs/ecs/"

func _initialize() -> void:
	call_deferred("run")

func sample_coast(image: Image, at: Vector2) -> float:
	var detail := image.get_width() / 6.0
	var p := at * detail - Vector2(0.5, 0.5)
	var base := Vector2i(floori(p.x), floori(p.y))
	var f := p - Vector2(base)
	var size := image.get_size() - Vector2i.ONE
	var a := image.get_pixelv(base.clamp(Vector2i.ZERO, size)).r
	var b := image.get_pixelv((base + Vector2i.RIGHT).clamp(Vector2i.ZERO, size)).r
	var c := image.get_pixelv((base + Vector2i.DOWN).clamp(Vector2i.ZERO, size)).r
	var d := image.get_pixelv((base + Vector2i.ONE).clamp(Vector2i.ZERO, size)).r
	return lerpf(lerpf(a, b, f.x), lerpf(c, d, f.x), f.y) - 0.5

func run() -> void:
	var host := Node2D.new()
	root.add_child(host)
	var cells: Node = load(BASE + "grid/GridCellDataComponent.cs").new()
	cells.name = "Cells"
	host.add_child(cells)
	var view: Node = load(BASE + "terrain/TerrainPaintedRendererComponent.cs").new()
	view.set("RefreshOnReady", false)
	view.set("CellDataPath", NodePath("../Cells"))
	view.set("BoundsSize", Vector2i(6, 6))
	host.add_child(view)
	for detail in [2, 4, 8]:
		view.set("CoastDetail", detail)
		for origin in [Vector2i.ZERO, Vector2i(-8, 11)]:
			view.set("BoundsOrigin", origin)
			for pattern in range(16):
				for y in range(6):
					for x in range(6):
						var bit := (y % 2) * 2 + (x % 2)
						cells.call("SetTerrainKind", origin + Vector2i(x, y),
							"water" if pattern & (1 << bit) else "desert")
				view.call("Rebuild")
				var coast: Image = view.get_node("SplatSurface").material.get_shader_parameter("coast_map").get_image()
				for y in range(6):
					for x in range(6):
						var wet := (pattern & (1 << ((y % 2) * 2 + (x % 2)))) != 0
						assert((sample_coast(coast, Vector2(x + 0.5, y + 0.5)) > 0.0) == wet,
							"Coast changed a cell centre: pattern=%d detail=%d cell=%s" % [pattern, detail, str(Vector2i(x, y))])
	# A lone land cell keeps its centre but no longer has square outer corners.
	view.set("BoundsOrigin", Vector2i.ZERO)
	view.set("CoastDetail", 8)
	for invert in [false, true]:
		for y in range(6):
			for x in range(6):
				var wet: bool = (Vector2i(x, y) != Vector2i(2, 2)) != invert
				cells.call("SetTerrainKind", Vector2i(x, y), "water" if wet else "desert")
		view.call("Rebuild")
		var coast: Image = view.get_node("SplatSurface").material.get_shader_parameter("coast_map").get_image()
		assert((sample_coast(coast, Vector2(2.5, 2.5)) > 0) == invert)
		assert((sample_coast(coast, Vector2(2.0625, 2.0625)) > 0) != invert,
			"Coast still repeats a square cell corner instead of rounding it")
		assert((sample_coast(coast, Vector2(2.3125, 2.3125)) > 0) == invert,
			"Coast rounding consumed the cell's usable centre")
	# One-cell channels retain every centre and their straight half-cell boundary.
	for invert in [false, true]:
		for y in range(6):
			for x in range(6):
				cells.call("SetTerrainKind", Vector2i(x, y), "water" if ((x == 2) != invert) else "desert")
		view.call("Rebuild")
		var coast: Image = view.get_node("SplatSurface").material.get_shader_parameter("coast_map").get_image()
		for y in range(6):
			assert((sample_coast(coast, Vector2(2.5, y + 0.5)) > 0) != invert)
			assert((sample_coast(coast, Vector2(1.5, y + 0.5)) > 0) == invert)
	if DisplayServer.get_name() != "headless":
		await check_rendered_centres(host, cells, view)
	host.free()
	print("[terrain-live-coast-shape] OK")
	quit()

func solid(colour: Color) -> ImageTexture:
	var image := Image.create(4, 4, false, Image.FORMAT_RGBA8)
	image.fill(colour)
	return ImageTexture.create_from_image(image)

func check_rendered_centres(host: Node2D, cells: Node, view: Node) -> void:
	var viewport := SubViewport.new()
	viewport.size = Vector2i(384, 384)
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(viewport)
	host.reparent(viewport)
	view.set("CoastDetail", 4)
	view.set("ShadeStrength", 0.0)
	view.set("FoamStrength", 0.0)
	view.set("WaveIntensity", 0.0)
	view.call("Rebuild")
	await process_frame
	await process_frame
	var surface: TileMapLayer = view.get_node("SplatSurface")
	var material: ShaderMaterial = surface.material
	material.set_shader_parameter("tex_sand", solid(Color.RED))
	material.set_shader_parameter("tex_shallow", solid(Color.BLUE))
	material.set_shader_parameter("tex_deep", solid(Color.BLUE))
	for tint in ["tint_desert", "tint_sand", "tint_shallow", "tint_deep"]:
		material.set_shader_parameter(tint, Vector3.ONE)
	material.set_shader_parameter("saturation", 1.0)
	material.set_shader_parameter("contrast", 1.0)
	material.set_shader_parameter("shore_wet", 0.0)
	material.set_shader_parameter("shallow_sand", 0.0)
	for zoom in [1.0, 0.5]:
		host.scale = Vector2.ONE * zoom
		for pattern in range(16):
			for y in range(6):
				for x in range(6):
					var bit := (y % 2) * 2 + (x % 2)
					cells.call("SetTerrainKind", Vector2i(x, y), "water" if pattern & (1 << bit) else "desert")
			view.call("Rebuild")
			await process_frame
			await RenderingServer.frame_post_draw
			var rendered := viewport.get_texture().get_image()
			for y in range(6):
				for x in range(6):
					var cell := Vector2i(x, y)
					var expected_water := (pattern & (1 << ((y % 2) * 2 + (x % 2)))) != 0
					var pixel := rendered.get_pixelv(Vector2i(surface.to_global(surface.map_to_local(cell))))
					assert(pixel.r + pixel.b > 0.9, "Coast pixel was missing or shader failed")
					assert((pixel.b > pixel.r) == expected_water,
						"Visible shoreline disagrees with grid centre: pattern=%d cell=%s zoom=%f" % [pattern, str(cell), zoom])
	host.reparent(root)
	viewport.free()
