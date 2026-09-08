extends SceneTree

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	var viewport := SubViewport.new()
	viewport.size = Vector2i(128, 128)
	viewport.transparent_bg = true
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(viewport)
	var mask := Image.create(64, 64, false, Image.FORMAT_RGBA8)
	mask.fill(Color.WHITE)
	mask.fill_rect(Rect2i(0, 0, 16, 16), Color.TRANSPARENT)
	var coast := Image.create(1, 1, false, Image.FORMAT_RGBA8)
	coast.fill(Color(1, 1, 0, 1))
	var sprite := Sprite2D.new()
	sprite.centered = false
	sprite.texture = ImageTexture.create_from_image(mask)
	sprite.modulate.a = 0.5
	var material := ShaderMaterial.new()
	material.shader = load("res://addons/beep_game_builder_cs/shaders/iso_water.gdshader")
	material.set_shader_parameter("coast_map", ImageTexture.create_from_image(coast))
	material.set_shader_parameter("flat_projection", 1.0)
	material.set_shader_parameter("map_size", Vector2(1, 1))
	material.set_shader_parameter("cell_size", Vector2(64, 64))
	material.set_shader_parameter("max_opacity", 0.6)
	material.set_shader_parameter("shore_opacity", 0.6)
	material.set_shader_parameter("lake_opacity", 0.6)
	material.set_shader_parameter("foam_strength", 0.0)
	sprite.material = material
	viewport.add_child(sprite)
	await process_frame
	await RenderingServer.frame_post_draw
	var rendered := viewport.get_texture().get_image()
	assert(rendered.get_pixel(8, 8).a < 0.01, "Water shader filled a transparent tile corner")
	var alpha := rendered.get_pixel(32, 32).a
	assert(absf(alpha - 0.3) < 0.03, "Water shader discarded CanvasItem opacity: %f" % alpha)
	viewport.free()
	print("[terrain-water-alpha] OK")
	quit()
