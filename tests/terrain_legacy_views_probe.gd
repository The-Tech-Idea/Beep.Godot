extends SceneTree

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	root.size = Vector2i(1280, 800)
	var scene = load("res://addons/beep_game_builder_cs/templates/scenes/terrain/terrain_generator_lab.tscn").instantiate()
	var world = scene.get_node("World")
	world.MapSize = 0
	root.add_child(scene)
	await process_frame
	var deadline = Time.get_ticks_msec() + 30000
	while world.IsGenerating and Time.get_ticks_msec() < deadline: await process_frame
	if world.IsGenerating or world.BuiltSize == Vector2i.ZERO:
		push_error("Generator did not finish")
		quit(1)
		return
	var view = scene.get_node("HUD/Settings/Scroll/Controls/ViewRow/View") as OptionButton
	var records: Array = []
	for projection in [1, 3]:
		view.select(projection)
		view.item_selected.emit(projection)
		for frame in range(5): await process_frame
		var renderer = scene.get_node("Preview/TileRenderer" if projection == 1 else "Preview/IsoAutotile")
		deadline = Time.get_ticks_msec() + 30000
		while projection == 3 and renderer.IsRebuilding and Time.get_ticks_msec() < deadline: await process_frame
		var layers: Array = []
		for child in renderer.get_children():
			if child is TileMapLayer:
				layers.append({"name":child.name,"visible":child.is_visible_in_tree(),"cells":child.get_used_cells().size(),"hasTiles":child.tile_set != null})
		var record = {"projection":projection,"visible":renderer.is_visible_in_tree(),"libraryPack":renderer.LibraryPack != null,"layers":layers}
		if projection == 3: record["diagnostics"] = renderer.GetPaintDiagnostics()
		records.append(record)
		await RenderingServer.frame_post_draw
		DirAccess.make_dir_recursive_absolute("res://tests/output/legacy_views")
		root.get_texture().get_image().save_png("res://tests/output/legacy_views/view_%d.png" % projection)
	var file = FileAccess.open("res://tests/output/legacy_views/report.json", FileAccess.WRITE)
	file.store_string(JSON.stringify(records,"  "))
	print("LEGACY VIEW DIAGNOSTICS ", JSON.stringify(records))
	scene.free()
	quit()
