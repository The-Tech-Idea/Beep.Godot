extends SceneTree

func _initialize() -> void:
	call_deferred("run")

func check_bounds(view: Node, cell_edge: float, low: float, high: float) -> void:
	var bounds: Array = view.call("GetStampBounds")
	assert(not bounds.is_empty(), "No props to measure in " + view.name)
	for rect: Rect2 in bounds:
		var extent := maxf(rect.size.x, rect.size.y) / cell_edge
		assert(extent >= low - 0.001 and extent <= high + 0.001,
			"%s: %.3f cells is outside %.3f..%.3f" % [view.name, extent, low, high])
	print("[terrain-prop-sizing] ", view.name, ": ", bounds.size(), " measured props")

func run() -> void:
	var rules: Resource = load("res://addons/beep_game_builder_cs/textures/terrain/terrain_prop_sizing.tres")
	for canvas in [64, 256]:
		var image := Image.create(canvas, canvas, false, Image.FORMAT_RGBA8)
		image.fill(Color.TRANSPARENT)
		image.fill_rect(Rect2i(10, 20, 20, 40), Color.WHITE)
		var texture := ImageTexture.create_from_image(image)
		assert(rules.call("VisibleRegion", texture, 1, 1, 0) == Rect2(10, 20, 20, 40), "Transparent padding affects visible size")
	for kind in ["woods", "oasis", "marsh", "bush", "small_rock", "large_rock"]:
		assert(is_finite(rules.call("SizeInCells", kind, NAN)))
		assert(rules.call("SizeInCells", kind, -100.0) > 0)
	var scene: Node = load("res://addons/beep_game_builder_cs/templates/scenes/terrain/terrain_generator_lab.tscn").instantiate()
	var world: Node = scene.get_node("World")
	world.set("MapSize", 0)
	root.add_child(scene)
	await process_frame
	await process_frame
	var cells: Node = scene.get_node("Preview/Cells")
	var before := var_to_bytes(cells.call("GetCells"))
	for projection in [0, 1, 2, 3]:
		world.set("Projection", projection)
		world.call("Redraw")
		await process_frame
		var flat: Node = scene.get_node("Preview/Features")
		var iso: Node = scene.get_node("Preview/IsoFeatures")
		assert(flat.get("PropSizing") == rules and iso.get("PropSizing") == rules)
		if projection < 2:
			check_bounds(flat, 64.0, 1.75, 2.25)
			check_bounds(scene.get_node("Preview/RockObjects"), 64.0, 0.25, 0.65)
		elif projection == 2:
			var corners: PackedVector2Array = scene.get_node("Preview/Iso").call("SurfaceCorners", Vector2i(16, 8))
			check_bounds(iso, corners[0].distance_to(corners[1]), 1.75, 2.25)
		assert(var_to_bytes(cells.call("GetCells")) == before)
	# One resource edit changes every renderer, independent of presentation.
	var original: Vector2 = rules.get("Trees")
	rules.set("Trees", Vector2(1.25, 1.25))
	for projection in [0, 1, 2]:
		world.set("Projection", projection)
		world.call("Redraw")
		if projection < 2:
			check_bounds(scene.get_node("Preview/Features"), 64.0, 1.25, 1.25)
		else:
			var corners: PackedVector2Array = scene.get_node("Preview/Iso").call("SurfaceCorners", Vector2i(16, 8))
			check_bounds(scene.get_node("Preview/IsoFeatures"), corners[0].distance_to(corners[1]), 1.25, 1.25)
	rules.set("Trees", original)
	scene.free()
	print("[terrain-prop-sizing] OK")
	quit()
