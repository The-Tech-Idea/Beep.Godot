extends "res://tests/terrain_lab_build.gd"

const DIR := "res://addons/beep_game_builder_cs/templates/scenes/terrain/"
const OUT := "res://tests/output/art_styles/"

func _initialize() -> void:
	call_deferred("run")

func capture(scene: Node, name: String) -> PackedByteArray:
	scene.get_node("HUD").hide()
	scene.get_node("Preview/MapOverlay").hide()
	var preview: Node2D = scene.get_node("Preview")
	preview.scale = Vector2.ONE
	preview.position = Vector2(640, 400) - Vector2(16, 8) * 64.0
	var material: ShaderMaterial = scene.get_node("Preview/Splat/SplatSurface").material
	material.set_shader_parameter("wave_speed", 0.0)
	await process_frame
	await RenderingServer.frame_post_draw
	var image := root.get_texture().get_image()
	assert(image.save_png(OUT + name + ".png") == OK)
	return image.get_data()

func run() -> void:
	assert(DisplayServer.get_name() != "headless")
	root.size = Vector2i(1280, 800)
	DirAccess.make_dir_recursive_absolute(OUT)
	var scene: Node = load(DIR + "terrain_generator_lab.tscn").instantiate()
	var world: Node = scene.get_node("World")
	world.set("MapSize", 0)
	root.add_child(scene)
	var build := await await_lab_build(world)
	assert(build.success, "the lab's first build did not succeed: %s" % build.message)
	assert(world.get("MapArt") == null, "Original must remain the default")
	var snapshot := var_to_bytes(scene.get_node("Preview/Cells").call("GetCells"))
	var baseline := await capture(scene, "original")
	var option: OptionButton = scene.get_node("HUD/Settings/Scroll/Controls/ViewRow/View")
	for selected in [1, 2, 0]:
		var view_index: int = [0, 4, 5][selected]
		option.select(view_index)
		option.item_selected.emit(view_index)
		assert(var_to_bytes(scene.get_node("Preview/Cells").call("GetCells")) == snapshot, "Art change rewrote live cells")
		var name: String = ["restored", "pixel", "cartoon"][selected]
		var rendered := await capture(scene, name)
		assert((rendered == baseline) == (selected == 0), "Original did not restore exactly, or new art did not change pixels")
		if selected > 0:
			var sizing: Resource = world.get("PropSizing")
			assert(sizing.call("SizeInCells", "small_rock", 1000.0) <= 0.40001)
			assert(sizing.call("SizeInCells", "woods", 1000.0) <= sizing.get("Trees").y)
			var trees: Node = scene.get_node("Preview/Features")
			var old_jitter: float = trees.get("ScaleJitter")
			trees.set("ScaleJitter", 20.0)
			trees.call("Rebuild")
			for bounds: Rect2 in trees.call("GetStampBounds"):
				assert(maxf(bounds.size.x, bounds.size.y) <= sizing.get("Trees").y * 64.0 + 0.01)
			trees.set("ScaleJitter", old_jitter)
			trees.call("Rebuild")
	scene.free()
	for file in ["terrain_pixel_art_demo.tscn", "terrain_cartoon_demo.tscn"]:
		var demo: Node = load(DIR + file).instantiate()
		assert(demo.get_node("World").get("MapArt") != null)
		demo.free()
	print("[terrain-art-styles] OK")
	quit()
