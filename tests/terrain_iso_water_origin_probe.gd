extends "res://tests/terrain_lab_build.gd"

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	assert(DisplayServer.get_name() != "headless")
	var scene: Node = load("res://addons/beep_game_builder_cs/templates/scenes/terrain/terrain_generator_lab.tscn").instantiate()
	scene.get_node("World").set("MapSize", 0)
	root.add_child(scene)
	var build := await await_lab_build(scene.get_node("World"))
	assert(build.success, "the lab's first build did not succeed: %s" % build.message)
	var chooser: OptionButton = scene.get_node("HUD/Settings/Scroll/Controls/ViewRow/View")
	chooser.select(2)
	chooser.item_selected.emit(2)
	await process_frame
	await process_frame
	var iso: Node = scene.get_node("Preview/Iso")
	var authored: Polygon2D = iso.get_node("IsoWater")
	var native: TileMapLayer = iso.get_node("IsoSeabed")
	var viewport := SubViewport.new()
	viewport.size = Vector2i(512, 512)
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(viewport)
	var water: Polygon2D = authored.duplicate()
	water.position = Vector2(256, 32)
	water.scale = Vector2.ONE * (90.0 / float(iso.get("CellSize").x))
	viewport.add_child(water)
	# Read the production vertex/projection functions, but encode cell coordinates
	# instead of sea colour so native map_to_local positions have exact expectations.
	var production := FileAccess.get_file_as_string("res://addons/beep_game_builder_cs/shaders/iso_water.gdshader")
	var pattern := RegEx.new()
	pattern.compile("void vertex\\(\\) \\{[\\s\\S]*?\\}")
	var vertex := pattern.search(production)
	assert(vertex != null)
	var start := production.find("\tvec2 half_cell")
	var end := production.find("\tvec2 UVm", start)
	assert(start >= 0 and end > start)
	var shader := Shader.new()
	shader.code = "shader_type canvas_item; render_mode unshaded; uniform vec2 cell_size; uniform vec2 tile_offset=vec2(0.0); uniform float flat_projection=0.0; uniform bool tile_batch=true; varying vec2 surface_local; " + vertex.get_string() + " void fragment(){" + production.substr(start, end - start) + "COLOR=vec4((floor(tile)+vec2(0.5))/4.0,0.0,1.0);}"
	var material := ShaderMaterial.new()
	material.shader = shader
	for parameter in ["cell_size", "tile_offset", "flat_projection", "tile_batch"]:
		var value = authored.material.get_shader_parameter(parameter)
		if value != null: material.set_shader_parameter(parameter, value)
	water.material = material
	for offset in [Vector2.ZERO, Vector2(7, -3)]:
		water.position += offset
		await process_frame
		await RenderingServer.frame_post_draw
		var pixels := viewport.get_texture().get_image()
		for y in range(4):
			for x in range(4):
				var at := Vector2i(water.to_global(native.map_to_local(Vector2i(x, y))))
				var actual := pixels.get_pixelv(at)
				var expected := Color((x + 0.5) / 4.0, (y + 0.5) / 4.0, 0.0)
				assert(absf(actual.r - expected.r) < 0.01 and absf(actual.g - expected.g) < 0.01,
					"Polygon water samples wrong cell at %s: expected=%s actual=%s" % [str(Vector2i(x, y)), str(expected), str(actual)])
	viewport.free()
	scene.free()
	print("[terrain-iso-water-origin] OK")
	quit()
