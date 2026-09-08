extends SceneTree

const BASE := "res://addons/beep_game_builder_cs/ecs/"

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	assert(DisplayServer.get_name() != "headless")
	var viewport := SubViewport.new()
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(viewport)
	var cells: Node = load(BASE + "grid/GridCellDataComponent.cs").new()
	cells.name = "Cells"
	viewport.add_child(cells)
	var view: Node2D = load(BASE + "terrain/TerrainPaintedRendererComponent.cs").new()
	view.set("RefreshOnReady", false)
	view.set("CellDataPath", NodePath("../Cells"))
	view.set("TileSize", 16)
	view.set("ShadeStrength", 0.0)
	viewport.add_child(view)
	var profile = load(BASE + "terrain/TerrainMaterialTiling.cs").new()
	profile.set("Rock", 5.25)
	profile.set("Sand", 7.5)
	profile.set("SeamBlend", 0.04)
	view.set("MaterialTiling", profile)
	var image := Image.create(64, 64, false, Image.FORMAT_RGBA8)
	for y in 64:
		for x in 64:
			image.set_pixel(x, y, Color(0.2 + 0.6 * x / 63.0, 0.2 + 0.5 * y / 63.0, 0.3 if (x / 8 + y / 8) % 2 else 0.7))
	image.generate_mipmaps()
	var texture := ImageTexture.create_from_image(image)
	for origin in [Vector2i(-13, -17), Vector2i(7, 11)]:
		for mode in ["grass", "boundary", "coast", "water"]:
			cells.call("ClearCells")
			for y in 32:
				for x in 32:
					var kind := "grass"
					if mode == "boundary" and x >= 16: kind = "rock"
					if mode == "coast": kind = "sand" if x < 16 else "deep_water"
					if mode == "water": kind = "deep_water"
					cells.call("SetTerrainKind", origin + Vector2i(x, y), kind)
			var snapshot := var_to_bytes(cells.call("GetCells"))
			for zoom in [0.5, 1.0, 2.0]:
				viewport.size = Vector2i(Vector2(256, 256) * zoom)
				view.scale = Vector2(zoom, zoom * 0.8) if zoom == 2.0 else Vector2.ONE * zoom
				view.rotation = 0.13 if zoom == 2.0 else 0.0
				view.position = Vector2(viewport.size) * 0.5 - view.transform.basis_xform(Vector2(origin + Vector2i(16, 16)) * 16.0)
				var captures: Array[Image] = []
				for crop in [Vector2i.ZERO, Vector2i(3, 5), Vector2i(5, 3)]:
					view.set("BoundsOrigin", origin + crop)
					view.set("BoundsSize", Vector2i(32, 32) if crop == Vector2i.ZERO else Vector2i(24, 24))
					view.call("Rebuild")
					var material: ShaderMaterial = view.get_node("SplatSurface").material
					material.set_shader_parameter("wave_speed", 0.0)
					for slot in ["grass", "rock", "sand", "shallow", "deep"]:
						material.set_shader_parameter("tex_" + slot, texture)
					await process_frame
					await RenderingServer.frame_post_draw
					captures.append(viewport.get_texture().get_image())
				for i in range(1, captures.size()):
					var error := pixel_error(captures[0], captures[i])
					print("[terrain-material-origin] ", origin, " ", mode, " zoom=", zoom, " crop=", i, " mean_error=", error)
					assert(error < 0.0002, "Cropping moved the ground/water material or boundary noise")
			assert(var_to_bytes(cells.call("GetCells")) == snapshot, "Rendering changed live cells")
	viewport.free()
	await check_water_views(texture)
	print("[terrain-material-origin] OK")
	quit()

func check_water_views(texture: Texture2D) -> void:
	for isometric in [false, true]:
		var viewport := SubViewport.new()
		viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
		root.add_child(viewport)
		var cells: Node = load(BASE + "grid/GridCellDataComponent.cs").new()
		cells.name = "Cells"
		cells.set("DefaultTerrainKind", "deep_water")
		viewport.add_child(cells)
		var script_name := "TerrainIsometricRendererComponent" if isometric else "TerrainTileRendererComponent"
		var view: Node2D = load(BASE + "terrain/" + script_name + ".cs").new()
		view.set("RefreshOnReady", false)
		view.set("CellDataPath", NodePath("../Cells"))
		view.set("WaterShaderPath", "res://addons/beep_game_builder_cs/shaders/iso_water.gdshader")
		for property in ["MaxOpacity", "ShoreOpacity", "LakeOpacity"]: view.set(property, 1.0)
		if isometric:
			view.set("BlockSheetPath", "res://addons/beep_game_builder_cs/textures/iso/voxel_blocks.png")
			view.set("SheetColumns", 9)
			view.set("SheetRows", 16)
			view.set("CellSize", Vector2i(92, 53))
			view.set("BlockLift", 27)
		else:
			view.set("WaterAtlasPath", "res://addons/beep_game_builder_cs/textures/tiles/water_15piece.png")
		viewport.add_child(view)
		for origin in [Vector2i(-13, -17), Vector2i(7, 11)]:
			for zoom in [0.5, 1.0, 2.0]:
				viewport.size = Vector2i(Vector2(256, 256) * zoom)
				view.scale = Vector2.ONE * zoom * (0.3 if isometric else 0.25)
				view.rotation = 0.13
				var captures: Array[Image] = []
				for crop in [Vector2i.ZERO, Vector2i(3, 5), Vector2i(5, 3)]:
					view.set("BoundsOrigin", origin + crop)
					view.set("BoundsSize", Vector2i(32, 32) if crop == Vector2i.ZERO else Vector2i(24, 24))
					view.call("Rebuild")
					var layer: TileMapLayer = view.get_node("IsoSeabed") if isometric else view.call("GetTerrainLayer")
					view.position = Vector2(viewport.size) * 0.5 - view.transform.basis_xform(layer.map_to_local(origin + Vector2i(16, 16)))
					var surface: CanvasItem = view.get_node("IsoWater" if isometric else "TileWater")
					var material: ShaderMaterial = surface.material
					assert(material.get_shader_parameter("map_origin") == Vector2(origin + crop), "Water renderer did not bind grid origin")
					material.set_shader_parameter("wave_speed", 0.0)
					for slot in ["sand", "shallow", "deep"]: material.set_shader_parameter("tex_" + slot, texture)
					await process_frame
					await RenderingServer.frame_post_draw
					captures.append(viewport.get_texture().get_image())
				for i in range(1, captures.size()):
					var error := pixel_error(captures[0], captures[i])
					print("[terrain-material-origin] ", script_name, " ", origin, " zoom=", zoom, " crop=", i, " mean_error=", error)
					assert(error < 0.0002, "Cropping moved projected water textures or noise")
		assert(cells.call("GetCells").is_empty(), "Water renderer created grid records")
		viewport.free()

func pixel_error(a: Image, b: Image) -> float:
	var error := 0.0
	# Keep coast-field edge extrapolation outside the comparison region.
	var area := Rect2i(a.get_size() / 4, a.get_size() / 2)
	for y in range(area.position.y, area.end.y):
		for x in range(area.position.x, area.end.x):
			var left := a.get_pixel(x, y)
			var right := b.get_pixel(x, y)
			assert(left.a == 1.0 and right.a == 1.0, "Terrain surface has holes")
			error += absf(left.r - right.r) + absf(left.g - right.g) + absf(left.b - right.b)
	return error / (area.get_area() * 3.0)
