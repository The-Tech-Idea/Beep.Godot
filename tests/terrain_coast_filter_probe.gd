extends SceneTree

const WIDTH := 32
const PIXELS := 512

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	assert(DisplayServer.get_name() != "headless")
	var probe = load("res://tests/TerrainCoastFilterSmoke.cs").new()
	assert(probe.call("Run"))
	if "--profile" in OS.get_cmdline_user_args(): probe.call("Profile")
	var viewport := SubViewport.new()
	viewport.size = Vector2i(PIXELS, PIXELS)
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(viewport)
	var field := Image.create(WIDTH, WIDTH, false, Image.FORMAT_RF)
	for y in WIDTH:
		for x in WIDTH:
			field.set_pixel(x, y, Color(clampf(0.5 + (y - curve(x)) / 16.0, 0.0, 1.0), 0, 0))
	var material := ShaderMaterial.new()
	var shader := Shader.new()
	shader.code = '''shader_type canvas_item;
uniform sampler2D raw_coast : filter_linear, repeat_disable;
uniform sampler2D baked_coast : filter_linear, repeat_disable;
void fragment() {
	COLOR = vec4(texture(baked_coast, UV).r, texture(raw_coast, UV).r, 0.0, 1.0);
}'''
	material.shader = shader
	var raw := ImageTexture.create_from_image(field)
	material.set_shader_parameter("raw_coast", raw)
	material.set_shader_parameter("baked_coast", probe.call("Bake", raw, Vector2i(8, 8)))
	var rect := ColorRect.new()
	rect.size = Vector2.ONE * PIXELS
	rect.material = material
	viewport.add_child(rect)
	await process_frame
	await RenderingServer.frame_post_draw
	var rendered := viewport.get_texture().get_image()
	var maximum_error := 0.0
	var error_at := Vector2i.ZERO
	for y in range(0, PIXELS, 7):
		for x in range(0, PIXELS, 7):
			var texel := (Vector2(x, y) + Vector2.ONE * 0.5) * WIDTH / PIXELS - Vector2.ONE * 0.5
			var error := absf(rendered.get_pixel(x, y).r - reference(field, texel))
			if error > maximum_error:
				maximum_error = error
				error_at = Vector2i(x, y)
	print("[terrain-coast-filter] cached reconstruction reference error=", maximum_error, " at=", error_at)
	var linear_error := 0.0
	var cubic_error := 0.0
	var samples := 0
	for x in range(24, PIXELS - 24):
		var expected := (curve((x + 0.5) * WIDTH / PIXELS - 0.5) + 0.5) * PIXELS / WIDTH - 0.5
		linear_error += absf(crossing(rendered, x, false) - expected)
		cubic_error += absf(crossing(rendered, x, true) - expected)
		samples += 1
	print("[terrain-coast-filter] reference max error=", maximum_error,
		" curved-shore pixel MAE linear=", linear_error / samples, " cubic=", cubic_error / samples)
	assert(maximum_error < 0.01, "Cached coast differs from independent cubic reference")
	assert(cubic_error < linear_error * 0.8, "Interpolating reconstruction did not improve the analytic curved shore")
	# At one texel per gameplay cell, preserve linear isolated-cell coverage.
	material.set_shader_parameter("baked_coast", probe.call("Bake", raw, Vector2i(WIDTH, WIDTH)))
	await process_frame
	await RenderingServer.frame_post_draw
	var coarse := viewport.get_texture().get_image()
	for y in range(0, PIXELS, 7):
		for x in range(0, PIXELS, 7):
			var pixel := coarse.get_pixel(x, y)
			assert(pixel.r == pixel.g, "Coarse cell mask was smoothed")
	viewport.free()
	probe.free()
	print("[terrain-coast-filter] OK")
	quit()

func curve(x: float) -> float:
	return 15.5 + 4.0 * sin(x * 0.37)

func weight(distance: float) -> float:
	var x := absf(distance)
	if x <= 1.0: return 1.5 * x * x * x - 2.5 * x * x + 1.0
	if x < 2.0: return -0.5 * x * x * x + 2.5 * x * x - 4.0 * x + 2.0
	return 0.0

func reference(field: Image, at: Vector2) -> float:
	var cell := Vector2i(at.floor())
	var result := 0.0
	for dy in range(-1, 3):
		for dx in range(-1, 3):
			var neighbour := cell + Vector2i(dx, dy)
			result += field.get_pixel(clampi(neighbour.x, 0, WIDTH - 1), clampi(neighbour.y, 0, WIDTH - 1)).r \
				* weight(at.x - neighbour.x) * weight(at.y - neighbour.y)
	return clampf(result, 0.0, 1.0)

func crossing(image: Image, x: int, cubic: bool) -> float:
	for y in range(1, PIXELS):
		var previous := image.get_pixel(x, y - 1)
		var current := image.get_pixel(x, y)
		var a := previous.r if cubic else previous.g
		var b := current.r if cubic else current.g
		if a < 0.5 and b >= 0.5:
			return y - 1 + (0.5 - a) / (b - a)
	return -1.0
