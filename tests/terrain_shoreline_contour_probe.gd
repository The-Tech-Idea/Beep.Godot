extends SceneTree

const OUT := "res://tests/output/shoreline_contours/"
const LAB := "res://addons/beep_game_builder_cs/templates/scenes/terrain/shoreline_contour_lab.tscn"

func _initialize() -> void:
	call_deferred("run")

func capture(name: String) -> Image:
	await process_frame
	await RenderingServer.frame_post_draw
	var image := root.get_texture().get_image()
	assert(image.save_png(OUT + name + ".png") == OK)
	return image

func run() -> void:
	var smoke: Node = load("res://tests/TerrainShorelineContourSmoke.cs").new()
	assert(smoke.call("Run"))
	if DisplayServer.get_name() == "headless":
		smoke.free()
		print("[shoreline-contours] CPU OK")
		quit()
		return
	DirAccess.make_dir_recursive_absolute(OUT)
	root.size = Vector2i(1280, 800)
	var scene: Node2D = load(LAB).instantiate()
	root.add_child(scene)
	while scene.get("generation_count") == 0: await process_frame
	var surface: ColorRect = scene.get_node("Surface")
	var material: ShaderMaterial = surface.material
	var field: RefCounted = scene.get("field")
	var texture: Texture2D = field.call("GetDistanceTexture")
	var bytes := texture.get_image().get_data()
	var first_generation: int = scene.get("generation_count")
	var images: Array[Image] = []
	for width in [0.0, 0.25, 1.0, 3.0]:
		scene.get_node("HUD/Toolbar/Margin/Controls/Width").value = width
		images.append(await capture("generated_width_" + str(width)))
		assert(texture.get_image().get_data() == bytes, "Beach width regenerated the coastline")
		assert(scene.get("generation_count") == first_generation)
	# Every sampled on-map pixel must obey the distance thresholds, not tile IDs.
	var transform := surface.get_global_transform_with_canvas()
	for y in range(80, 755, 3):
		for x in range(16, 1264, 3):
			var local := transform.affine_inverse() * (Vector2(x, y) + Vector2.ONE * 0.5)
			if not Rect2(Vector2.ZERO, surface.size).has_point(local): continue
			var distance: float = field.call("SampleDistance", local / 32.0)
			if absf(distance) < 0.015: continue
			for i in images.size():
				var pixel := images[i].get_pixel(x, y)
				var water := pixel.b > pixel.r and pixel.b > pixel.g
				assert(water == (distance >= 0.0), "Water contour changed with beach width")
				if distance >= 0.0: continue
				var width: float = [0.0, 0.25, 1.0, 3.0][i]
				if absf(distance + width) < 0.015: continue
				assert((pixel.g > pixel.r) == (width <= 0.0 or distance < -width), "Rendered inland edge differs from shared distance inset")
	for fixture in ["circle", "cove", "narrow"]:
		material.set_shader_parameter("distance_map", smoke.call("BuildFixture", fixture))
		scene.call("set_beach_width", 1.5)
		await capture(fixture)
	# Restore a real generated map for the final inspectable screenshot.
	material.set_shader_parameter("distance_map", texture)
	scene.get_node("HUD/Toolbar/Margin/Controls/Width").value = 1.0
	await capture("generated")
	scene.free()
	smoke.free()
	print("[shoreline-contours] GPU threshold, fixed coast, width controls and fixture captures OK")
	quit()
