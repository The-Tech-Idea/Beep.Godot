extends "res://tests/terrain_lab_build.gd"

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	root.size = Vector2i(1280, 800)
	var scene: Node = load("res://addons/beep_game_builder_cs/templates/scenes/terrain/terrain_generator_lab.tscn").instantiate()
	var large := "--large" in OS.get_cmdline_user_args()
	scene.get_node("World").set("MapSize", 4 if large else 0)
	root.add_child(scene)
	var build := await await_lab_build(scene.get_node("World"))
	assert(build.success, "the lab's first build did not succeed: %s" % build.message)
	var world: Node = scene.get_node("World")
	var cells: Node = scene.get_node("Preview/Cells")
	var view: OptionButton = scene.get_node("HUD/Settings/Scroll/Controls/ViewRow/View")
	# Four projections, then one entry per art style in TerrainLabComponent.StyleProfiles. This read
	# `item_count == 6` with the two styles hard-coded, from when the lab held one property per style.
	var styles = scene.get("StyleProfiles")
	assert(styles != null and styles.size() >= 2, "the lab lists no art styles")
	assert(view.item_count == 4 + styles.size(),
		"the view menu lists %d entries for four projections and %d styles" % [view.item_count, styles.size()])
	assert(view.get_item_text(4) == "Pixel Art" and view.get_item_text(5) == "Cartoon",
		"the first two styles are '%s' and '%s'" % [view.get_item_text(4), view.get_item_text(5)])
	assert(view.get_global_rect().position.y < 100, "Map styles are buried below generation settings")
	var revision: int = cells.get("TerrainRevision")
	var dimensions: Vector2i = world.get("BuiltSize")
	var cell_count := dimensions.x * dimensions.y
	var overlay: Node = scene.get_node("Preview/MapOverlay")
	var resource_icons: CanvasItem = scene.get_node("Preview/Diagnostics/Resources")
	var diagnostics_toggle: BaseButton = scene.get_node("HUD/Settings/Scroll/Controls/Diagnostics")
	assert(not diagnostics_toggle.button_pressed)
	for style in range(3):
		view.select(3)
		view.item_selected.emit(3)
		assert(not view.disabled, "Styles must be reachable from every view")
		var selected: int = [0, 4, 5][style]
		view.select(selected)
		view.item_selected.emit(selected)
		await process_frame
		assert(world.get("Projection") == 0 and view.selected == selected)
		assert(world.get("MapArt") == (null if selected < 4 else styles[selected - 4]),
			"view %d put the wrong art on the world" % selected)
		assert(cells.get("TerrainRevision") == revision, "Changing style regenerated gameplay terrain")
		assert(overlay.get("UndergroundPatchCount") == 0 and not resource_icons.is_visible_in_tree(),
			"Resource diagnostics stained the normal terrain preview")
		diagnostics_toggle.button_pressed = true
		assert(overlay.get("UndergroundPatchCount") > 0, "Survey overlay cannot be enabled explicitly")
		assert(resource_icons.is_visible_in_tree(), "Resource icons cannot be enabled explicitly")
		diagnostics_toggle.button_pressed = false
		assert(overlay.get("UndergroundPatchCount") == 0 and not resource_icons.is_visible_in_tree())
		assert(cells.get("TerrainRevision") == revision, "Diagnostics changed gameplay terrain")
		if large:
			var surface: TileMapLayer = scene.get_node("Preview/Splat/SplatSurface")
			var coast: Texture2D = surface.material.get_shader_parameter("coast_map")
			assert(coast.get_size() == Vector2(1408, 880), "Huge map exceeded bounded native-resolution reconstruction")
		await capture(("large_" if large else "") + "style_%d" % style)
		if large:
			var preview: Node2D = scene.get_node("Preview")
			var saved_transform := preview.transform
			scene.get_node("HUD").hide()
			preview.scale = Vector2.ONE * 0.5
			preview.position = Vector2(640, 400) - Vector2(27, 36) * 32.0
			await process_frame
			await capture("large_closeup_style_%d" % style)
			preview.transform = saved_transform
			scene.get_node("HUD").show()
		if large and style == 2:
			var surface: TileMapLayer = scene.get_node("Preview/Splat/SplatSurface")
			surface.material.set_shader_parameter("contour_debug", true)
			await process_frame
			await capture("large_contours")
			surface.material.set_shader_parameter("contour_debug", false)
	for projection in [1, 3]:
		view.select(projection)
		view.item_selected.emit(projection)
		await process_frame
		await process_frame
		if projection == 3:
			var iso: Node = scene.get_node("Preview/IsoAutotile")
			var diagnostics: Dictionary = iso.call("GetPaintDiagnostics")
			assert(diagnostics.valid and diagnostics.requested == cell_count and diagnostics.missing == 0 and diagnostics.unmapped == 0)
			var layer: TileMapLayer = iso.call("GetTerrainLayer")
			assert(layer.get_used_cells().size() == cell_count)
			var before: Array[Vector2i] = []
			for cell in layer.get_used_cells(): before.append(layer.get_cell_atlas_coords(cell))
			iso.call("Rebuild")
			var after: Array[Vector2i] = []
			for cell in layer.get_used_cells(): after.append(layer.get_cell_atlas_coords(cell))
			assert(before == after, "Native tile selection changed on redraw")
		else:
			var grass: TileMapLayer = scene.get_node("Preview/TileRenderer/GrassTiles")
			assert(grass.material is ShaderMaterial and grass.material.get_shader_parameter("ground_texture") != null)
		await capture(("large_" if large else "") + "view_%d" % projection)
	scene.free()
	print("[terrain-lab-styles] OK")
	quit()

func capture(label: String) -> void:
	if DisplayServer.get_name() == "headless": return
	await RenderingServer.frame_post_draw
	DirAccess.make_dir_recursive_absolute("res://tests/output/lab_styles")
	assert(root.get_texture().get_image().save_png("res://tests/output/lab_styles/%s.png" % label) == OK)
