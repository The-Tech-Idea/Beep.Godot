extends SceneTree

const BASE := "res://addons/beep_game_builder_cs/ecs/"
var viewport: SubViewport

func _initialize() -> void:
	call_deferred("run")

func solid(colour: Color) -> ImageTexture:
	var image := Image.create(4, 4, false, Image.FORMAT_RGBA8)
	image.fill(colour)
	return ImageTexture.create_from_image(image)

func capture() -> Image:
	await process_frame
	await RenderingServer.frame_post_draw
	return viewport.get_texture().get_image()

func run() -> void:
	assert(DisplayServer.get_name() != "headless")
	viewport = SubViewport.new()
	viewport.size = Vector2i(256, 256)
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(viewport)
	var cells: Node = load(BASE + "grid/GridCellDataComponent.cs").new()
	cells.name = "Cells"
	viewport.add_child(cells)
	for y in range(4):
		for x in range(4):
			cells.call("SetTerrainKind", Vector2i(x, y), "desert")
	var view: Node2D = load(BASE + "terrain/TerrainPaintedRendererComponent.cs").new()
	view.set("RefreshOnReady", false)
	view.set("CellDataPath", NodePath("../Cells"))
	view.set("BoundsSize", Vector2i(4, 4))
	view.set("BlendWidth", 0.0)
	view.set("ShadeStrength", 0.0)
	view.set("EdgeNoise", 0.0)
	viewport.add_child(view)
	view.call("Rebuild")
	await process_frame
	await process_frame
	var material: ShaderMaterial = view.get_node("SplatSurface").material
	material.set_shader_parameter("tex_sand", solid(Color.RED))
	material.set_shader_parameter("tex_grass", solid(Color.GREEN))
	material.set_shader_parameter("tex_gravel", solid(Color.BLUE))
	for tint in ["tint_desert", "tint_grass", "tint_gravel"]:
		material.set_shader_parameter(tint, Vector3.ONE)
	material.set_shader_parameter("saturation", 1.0)
	material.set_shader_parameter("contrast", 1.0)
	material.set_shader_parameter("coast_wander", 0.0)
	var rendered := await capture()
	var wrong := 0
	for y in range(4, 252):
		for x in range(4, 252):
			var pixel := rendered.get_pixel(x, y)
			if pixel.r < 0.95 or pixel.g > 0.02 or pixel.b > 0.02:
				wrong += 1
	assert(wrong == 0, "Zero-width desert has %d uncovered/grass pixels" % wrong)
	for width in [0.0, 0.1, 0.42, 0.9]:
		material.set_shader_parameter("blend_width", width)
		material.set_shader_parameter("coast_wander", 2.0)
		material.set_shader_parameter("edge_noise", 1.0)
		rendered = await capture()
		for y in range(4, 252, 3):
			for x in range(4, 252, 3):
				var pixel := rendered.get_pixel(x, y)
				assert(pixel.r > 0.95 and pixel.g < 0.02 and pixel.b < 0.02,
					"Warped material lost coverage at blend width %f" % width)
	material.set_shader_parameter("coast_wander", 0.0)

	# A real live edit must change the material, without introducing a third terrain.
	for y in range(4):
		for x in range(2, 4):
			cells.call("SetTerrainKind", Vector2i(x, y), "gravel")
	view.set("BlendWidth", 0.42)
	view.call("Rebuild")
	await process_frame
	await process_frame
	var original_ids: PackedByteArray = material.get_shader_parameter("id_map").get_image().get_data()
	var original_coast: PackedByteArray = material.get_shader_parameter("coast_map").get_image().get_data()
	var widths: Array[int] = []
	for sharpness in [1.0, 4.0]:
		view.set("BlendSharpness", sharpness)
		view.call("Rebuild")
		assert(is_equal_approx(float(material.get_shader_parameter("blend_sharpness")), sharpness))
		assert(material.get_shader_parameter("id_map").get_image().get_data() == original_ids)
		assert(material.get_shader_parameter("coast_map").get_image().get_data() == original_coast)
		rendered = await capture()
		var mixed := 0
		for x in range(256):
			var pixel := rendered.get_pixel(x, 96)
			assert(pixel.g < 0.02, "Material blend introduced unrelated grass")
			assert(pixel.r + pixel.b > 0.95, "Blend left a dark gap")
			if pixel.r > 0.1 and pixel.b > 0.1: mixed += 1
		widths.append(mixed)
	assert(widths[1] > 0 and widths[1] < widths[0] / 2,
		"Sharpness did not narrow the transition: %s" % str(widths))
	print("[terrain-painted-blend] transition pixels at sharpness 1 / 4: %s" % str(widths))
	await verify_texture_edges(view, material)
	# All painted styles round an inland material corner using the same geometry.
	viewport.size = Vector2i(256, 256)
	view.scale = Vector2.ONE
	view.set("MaterialEdgeDetail", 0.0)
	view.set("BlendSharpness", 4.0)
	for y in 4:
		for x in 4:
			cells.call("SetTerrainKind", Vector2i(x, y), "grass" if x >= 1 and y >= 1 else "desert")
	view.call("Rebuild")
	await process_frame
	material.set_shader_parameter("tex_sand", solid(Color.RED))
	material.set_shader_parameter("tex_grass", solid(Color.GREEN))
	DirAccess.make_dir_recursive_absolute("res://tests/output/inland_contours")
	for style in [0, 1, 2]:
		material.set_shader_parameter("art_style", style)
		rendered = await capture()
		assert(rendered.save_png("res://tests/output/inland_contours/style_%d.png" % style) == OK)
		assert(rendered.get_pixel(66, 66).r > rendered.get_pixel(66, 66).g,
			"Inland corner still follows a square cell in style %d" % style)
		var interior := rendered.get_pixel(128, 128)
		var reference := rendered.get_pixel(220, 220)
		assert(interior.g > interior.r and absf(interior.g - reference.g) < 0.02,
			"Rounded corner erased terrain interior")
	await verify_ground_grain(view, material)
	viewport.free()
	print("[terrain-painted-blend] OK")
	quit()

# The ground keeps a surface of its own. A ground texture is authored at a pattern scale that has to sit
# right beside the vehicles standing on it, and at that scale its source is minified about three times, so
# mipmapping averages the grain away and the ground reads as a flat wash. The grain pass samples the same
# texture again near one texel a pixel, as brightness only: the INTERIOR of one material must gain
# variation without its average colour moving.
func verify_ground_grain(view: Node, material: ShaderMaterial) -> void:
	viewport.size = Vector2i(256, 256)
	view.scale = Vector2.ONE
	for y in 4:
		for x in 4:
			view.get_node("../Cells").call("SetTerrainKind", Vector2i(x, y), "desert")
	# A one-pixel checker in a 512-pixel texture. The base sample squeezes the whole thing into one
	# 64-pixel tile - eight times minified, so its mips average it to flat grey, exactly what a ground
	# texture authored for the vehicles' scale does. The grain samples it over eight tiles, one texel a
	# pixel, where the checker survives. Same texture, same material: only the repeat differs.
	var checks := Image.create(512, 512, false, Image.FORMAT_RGBA8)
	for y in 512:
		for x in 512:
			var bright: bool = (x + y) % 2 == 0
			checks.set_pixel(x, y, Color(0.75, 0.75, 0.75) if bright else Color(0.25, 0.25, 0.25))
	checks.generate_mipmaps()
	view.set("BlendWidth", 0.0)
	view.set("EdgeNoise", 0.0)
	view.set("ShadeStrength", 0.0)
	view.set("GroundDetailTiles", 8.0)
	var look: Resource = load(BASE + "terrain/TerrainWaterLook.cs").new()
	look.set("GroundTextureTiles", 1.0)
	view.set("WaterLook", look)
	var readings := {}
	for strength in [0.0, 0.5]:
		view.set("GroundDetailStrength", strength)
		view.call("Rebuild")
		assert(is_equal_approx(float(material.get_shader_parameter("ground_detail_strength")), strength))
		material.set_shader_parameter("tex_sand", ImageTexture.create_from_image(checks))
		material.set_shader_parameter("tint_desert", Vector3.ONE)
		material.set_shader_parameter("coast_wander", 0.0)
		var rendered := await capture()
		var total := 0.0
		var squares := 0.0
		var samples := 0
		for y in range(96, 160):
			for x in range(96, 160):
				var luma := rendered.get_pixel(x, y).get_luminance()
				total += luma
				squares += luma * luma
				samples += 1
		var mean := total / samples
		readings[strength] = {"mean": mean, "variation": sqrt(maxf(0.0, (squares / samples) - (mean * mean)))}
	var flat: Dictionary = readings[0.0]
	var grained: Dictionary = readings[0.5]
	print("[terrain-painted-blend] ground grain: flat mean %.4f variation %.4f; grained mean %.4f variation %.4f"
		% [flat["mean"], flat["variation"], grained["mean"], grained["variation"]])
	assert(grained["variation"] > flat["variation"] * 3.0 + 0.01,
		"The grain left the ground as flat as it found it: %.4f against %.4f" % [grained["variation"], flat["variation"]])
	assert(absf(grained["mean"] - flat["mean"]) < 0.02,
		"The grain moved the material's own colour: %.4f against %.4f" % [grained["mean"], flat["mean"]])

func verify_texture_edges(view: Node, material: ShaderMaterial) -> void:
	var stripes := Image.create(64, 64, false, Image.FORMAT_RGBA8)
	for y in 64:
		for x in 64:
			stripes.set_pixel(x, y, Color(0.9 if (y / 16) % 2 == 0 else 0.1, 0, 0))
	stripes.generate_mipmaps()
	material.set_shader_parameter("tex_sand", ImageTexture.create_from_image(stripes))
	var look: Resource = load(BASE + "terrain/TerrainWaterLook.cs").new()
	look.set("GroundTextureTiles", 4.0)
	view.set("WaterLook", look)
	view.set("BlendWidth", 0.42)
	view.set("EdgeNoise", 0.0)
	material.set_shader_parameter("coast_wander", 0.0)
	var data: Dictionary = {}
	for slot in ["id_map", "shade_map", "coast_map"]:
		data[slot] = material.get_shader_parameter(slot).get_image().get_data()
	for zoom in [0.5, 1.0, 2.0]:
		viewport.size = Vector2i(Vector2(256, 256) * zoom)
		view.scale = Vector2.ONE * zoom
		var images: Array[Image] = []
		for amount in [0.0, 0.75]:
			view.set("MaterialEdgeDetail", amount)
			view.call("Rebuild")
			assert(is_equal_approx(float(material.get_shader_parameter("material_edge_detail")), amount))
			for slot in data:
				assert(material.get_shader_parameter(slot).get_image().get_data() == data[slot])
			images.append(await capture())
		var boundary := roundi(127 * zoom)
		var bright := roundi(32 * zoom)
		var dark := roundi(96 * zoom)
		# Blue is the other material's weight: bright red detail must advance
		# while dark gaps admit blue. A global tint or sharper kernel fails this.
		var bright_change := images[0].get_pixel(boundary, bright).b - images[1].get_pixel(boundary, bright).b
		var dark_change := images[0].get_pixel(boundary, dark).b - images[1].get_pixel(boundary, dark).b
		assert(bright_change > 0.08 and dark_change < -0.04,
			"Texture detail did not control the edge: bright=%s dark=%s" % [bright_change, dark_change])
		var interior_error := 0.0
		for y in range(4, viewport.size.y - 4):
			for x in [roundi(48 * zoom), roundi(208 * zoom)]:
				var a := images[0].get_pixel(x, y)
				var b := images[1].get_pixel(x, y)
				interior_error = maxf(interior_error, maxf(absf(a.r - b.r), absf(a.b - b.b)))
				assert(a.a == 1.0 and b.a == 1.0, "Material detail made a transparent surface")
		assert(interior_error <= 1.0 / 255.0 + 0.0001, "Material edge detail changed the terrain interior")
		print("[terrain-painted-blend] texture edge zoom=%s bright=%s dark=%s interior_error=%s" % [zoom, bright_change, dark_change, interior_error])
