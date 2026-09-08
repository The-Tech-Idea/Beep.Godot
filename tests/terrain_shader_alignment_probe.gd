extends SceneTree

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	if DisplayServer.get_name() == "headless":
		push_error("This probe requires GPU rendering")
		quit(1)
		return
	var viewport := SubViewport.new()
	viewport.size = Vector2i(384, 384)
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(viewport)
	var cells = load("res://addons/beep_game_builder_cs/ecs/grid/GridCellDataComponent.cs").new()
	cells.name = "Cells"
	viewport.add_child(cells)
	cells.call("FillTerrain", Rect2i(0, 0, 4, 4), "grass")
	var view = load("res://addons/beep_game_builder_cs/ecs/terrain/TerrainPaintedRendererComponent.cs").new()
	view.set("CellDataPath", NodePath("../Cells"))
	view.set("BoundsSize", Vector2i(4, 4))
	view.set("RefreshOnReady", false)
	view.position = Vector2(64, 64)
	viewport.add_child(view)
	view.call("Rebuild")
	var layer: TileMapLayer = view.get_node("SplatSurface")
	var failed := false
	for shader_name in ["terrain_splat", "iso_water"]:
		failed = await check_shader(viewport, layer, shader_name) or failed
	var iso_tiles: TileSet = layer.tile_set.duplicate(true)
	iso_tiles.tile_shape = TileSet.TILE_SHAPE_ISOMETRIC
	iso_tiles.tile_layout = TileSet.TILE_LAYOUT_DIAMOND_DOWN
	layer.tile_set = iso_tiles
	view.position = Vector2(160, 32)
	failed = await check_shader(viewport, layer, "iso_water", true) or failed
	iso_tiles.tile_size = Vector2i(64, 32)
	failed = await check_shader(viewport, layer, "iso_water", true) or failed
	viewport.free()
	print("[terrain-shader-alignment] FAILED" if failed else "[terrain-shader-alignment] OK")
	quit(1 if failed else 0)

func check_shader(viewport: SubViewport, layer: TileMapLayer, shader_name: String, isometric := false) -> bool:
	var shader := Shader.new()
	var production := FileAccess.get_file_as_string("res://addons/beep_game_builder_cs/shaders/" + shader_name + ".gdshader")
	var vertex_pattern := RegEx.new()
	vertex_pattern.compile("void vertex\\(\\) \\{[\\s\\S]*?\\}")
	var vertex := vertex_pattern.search(production)
	assert(vertex != null)
	var projection := "vec2 tile = surface_local / cell_size;"
	if shader_name == "iso_water":
		var start := production.find("\tvec2 half_cell")
		var end := production.find("\tvec2 UVm", start)
		assert(start >= 0 and end > start)
		projection = production.substr(start, end - start)
	shader.code = "shader_type canvas_item; render_mode unshaded; uniform vec2 cell_size=vec2(64.0); uniform vec2 tile_offset=vec2(0.0); uniform float flat_projection=1.0; uniform bool tile_batch=true; varying vec2 surface_local; " + vertex.get_string() + " void fragment(){" + projection + "COLOR=vec4((floor(tile)+vec2(0.5))/4.0,0.0,1.0);}"
	var material := ShaderMaterial.new()
	material.shader = shader
	material.set_shader_parameter("flat_projection", 0.0 if isometric else 1.0)
	material.set_shader_parameter("cell_size", Vector2(layer.tile_set.tile_size))
	layer.material = material
	await process_frame
	await RenderingServer.frame_post_draw
	var pixels := viewport.get_texture().get_image()
	var failed := false
	for y in 4:
		for x in 4:
			for offset in ([Vector2(-8, 0), Vector2(8, 0)] if isometric else [Vector2(-16, -16), Vector2(16, 16)]):
				var sample := Vector2i(layer.to_global(layer.map_to_local(Vector2i(x, y))) + offset)
				var actual := pixels.get_pixelv(sample)
				var expected := Color((x + 0.5) / 4, (y + 0.5) / 4, 0)
				if abs(actual.r - expected.r) > 0.01 or abs(actual.g - expected.g) > 0.01:
					print("[terrain-shader-alignment] ", shader_name, " mismatch ", Vector2i(x,y), " at ", sample, " actual ", actual, " expected ", expected)
					failed = true
	return failed
