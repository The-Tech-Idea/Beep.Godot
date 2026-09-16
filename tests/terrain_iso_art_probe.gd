extends "res://tests/terrain_lab_build.gd"

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	assert(DisplayServer.get_name() != "headless")
	root.size = Vector2i(1280, 800)
	var scene: Node = load("res://addons/beep_game_builder_cs/templates/scenes/terrain/terrain_generator_lab.tscn").instantiate()
	scene.get_node("World").set("MapSize", 0)
	scene.get_node("World").set("Seed", 31415)
	root.add_child(scene)
	var build := await await_lab_build(scene.get_node("World"))
	assert(build.success, "the lab's first build did not succeed: %s" % build.message)
	var iso: Node2D = scene.get_node("Preview/Iso")
	assert(iso.get("BlockSheetPath").ends_with("/kenney_voxel_blocks.png"))
	assert(iso.get("TopSheetPath").ends_with("/kenney_voxel_tops.png"))
	assert(iso.get("SheetColumns") == 8 and iso.get("SheetRows") == 7)
	assert(iso.get("CellSize") == Vector2i(111, 64))
	assert(iso.get("GrassFrame") == 54 and "grass=54" in iso.get("TerrainVariants"), "Plain green ground was replaced")
	for property in ["BlockSheetPath", "TopSheetPath"]:
		var texture: Texture2D = load(iso.get(property))
		assert(texture != null, "Missing authored atlas: " + str(iso.get(property)))
		assert(texture.get_image().has_mipmaps(), "Missing imported mipmaps: " + str(iso.get(property)))
	var demo: Node = load("res://addons/beep_game_builder_cs/templates/scenes/terrain/terrain_iso_demo.tscn").instantiate()
	for property in ["BlockSheetPath", "TopSheetPath", "SheetColumns", "SheetRows", "CellSize", "BlockLift", "TopLift", "LevelHeight", "GrassFrame", "DryGrassFrame", "DesertFrame", "SandFrame", "TundraFrame", "SnowFrame", "IceFrame", "JungleFrame", "SwampFrame", "GravelFrame", "RockFrame", "TerrainVariants"]:
		assert(iso.get(property) == demo.get_node("World/Iso").get(property), "Demo atlas binding drift: " + property)
	demo.free()
	var chooser: OptionButton = scene.get_node("HUD/Settings/Scroll/Controls/ViewRow/View")
	chooser.select(2)
	chooser.item_selected.emit(2)
	await process_frame
	await process_frame
	scene.set_process(false)
	var seabed: TileMapLayer = iso.get_node("IsoSeabed")
	for cell in seabed.get_used_cells():
		assert(seabed.get_cell_source_id(cell) == 1, "Flat seabed rendered cliff sides between water cells")
	scene.get_node("HUD").hide()
	scene.get_node("Preview/MapOverlay").hide()
	var preview: Node2D = scene.get_node("Preview")
	var extent: Rect2 = iso.get("SurfaceExtent")
	assert(extent.has_area() and iso.get("HasSurface"))
	var fit := minf(1200.0 / extent.size.x, 720.0 / extent.size.y)
	preview.scale = Vector2.ONE * fit
	preview.position = Vector2(640, 400) - extent.get_center() * fit
	await process_frame
	await RenderingServer.frame_post_draw
	DirAccess.make_dir_recursive_absolute("res://tests/output/iso_art")
	assert(root.get_texture().get_image().save_png("res://tests/output/iso_art/authored.png") == OK)
	var grid: Node = scene.get_node("Preview/Grid")
	for cell in [Vector2i(10, 6), Vector2i(16, 8), Vector2i(20, 12)]:
		var expected := iso.to_global(iso.call("SurfacePosition", cell))
		assert(grid.call("CellToWorld", cell).distance_to(expected) < 0.01,
			"Gameplay grid did not use the authored atlas elevation geometry")
	var focus: Vector2 = preview.to_local(iso.to_global(iso.call("SurfacePosition", Vector2i(16, 8))))
	preview.scale = Vector2.ONE * fit * 1.8
	preview.position = Vector2(640, 400) - focus * preview.scale
	await process_frame
	await RenderingServer.frame_post_draw
	assert(root.get_texture().get_image().save_png("res://tests/output/iso_art/close.png") == OK)
	scene.free()
	print("[terrain-iso-art] OK")
	quit()
