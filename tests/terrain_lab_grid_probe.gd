extends SceneTree

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	root.size = Vector2i(1280, 800)
	var scene = load("res://addons/beep_game_builder_cs/templates/scenes/terrain/terrain_generator_lab.tscn").instantiate()
	scene.get_node("World").set("MapSize", 0)
	root.add_child(scene)
	await process_frame
	await process_frame
	var world = scene.get_node("World")
	assert(world.get("BuiltSize") == Vector2i(32, 32), "Lab controls overwrote configured map size")
	assert(not scene.get_node("Preview/Iso").get("HasSurface"), "Startup built an inactive view despite RefreshOnReady=false")
	var grid = scene.get_node("Preview/Grid")
	var navigation = scene.get_node("Preview/Navigation")
	var cells = scene.get_node("Preview/Cells")
	var data_tiles: TileSet = scene.get_node("Preview/CellData/TerrainData").tile_set
	var chooser: OptionButton = scene.get_node("HUD/Settings/Scroll/Controls/ViewRow/View")
	var edit := Vector2i(12, 12)
	cells.call("SetTerrainKind", edit, "water")
	var reference_coast := PackedByteArray()
	var coast_paths := ["Preview/Splat/SplatSurface", "Preview/TileRenderer/TileWater", "Preview/Iso/IsoWater"]
	for projection in range(4):
		chooser.select(projection)
		chooser.item_selected.emit(projection)
		await process_frame
		await process_frame
		assert(world.get("Projection") == projection, "Lab dropdown did not switch view")
		assert(scene.get_node("Preview/Features").get("Seed") == world.get("Seed"), "Flat features lost world seed")
		assert(scene.get_node("Preview/IsoFeatures").get("Seed") == world.get("Seed"), "Isometric features retained an independent seed")
		assert(scene.get_node("Preview/CellData/TerrainData").tile_set == data_tiles, "Projection switch rebuilt generated data")
		assert(cells.call("GetTerrainKind", edit) == "water", "Lab dropdown regenerated the map")
		assert(navigation.call("IsBlocked", edit), "Navigation lost edited water")
		assert(navigation.get("BoundsSize") == world.get("BuiltSize"), "Navigation bounds differ from world")
		if projection < 3:
			var coast: PackedByteArray = scene.get_node(coast_paths[projection]).material.get_shader_parameter("coast_map").get_image().get_data()
			if reference_coast.is_empty(): reference_coast = coast
			else: assert(coast == reference_coast, "Views reconstructed different live coast/ocean fields")
			var surface: CanvasItem = scene.get_node(coast_paths[projection])
			var cached: Texture2D = surface.material.get_shader_parameter("coast_map")
			cells.call("SetMetadata", edit, "terrain_elevation", float(projection + 1))
			surface.get_parent().call("Rebuild")
			assert(surface.material.get_shader_parameter("coast_map") == cached, "Elevation-only rebuild reconstructed a view's coast")
		if projection < 2:
			var overlay = scene.get_node("Preview/MapOverlay")
			assert(overlay.get_node(overlay.get("GridPath")) == grid, "Overlay was not bound to gameplay grid")
			assert(overlay.to_global(overlay.call("CellPosition", edit)).distance_to(grid.call("CellToWorld", edit)) < 0.01,
				"Overlay marker disagrees with lab picking")
		if projection == 2:
			assert(grid.get_node(grid.get("ElevatedTerrainPath")) == scene.get_node("Preview/Iso"))
		else:
			if projection == 3:
				var diagnostics: Dictionary = scene.get_node("Preview/IsoAutotile").call("GetPaintDiagnostics")
				assert(diagnostics.valid and diagnostics.missing == 0 and diagnostics.unmapped == 0, "Lab TileSet did not paint every terrain")
				assert("View incomplete" not in scene.get_node("HUD/Status").text, "Completed native tiles still report incomplete")
			assert(grid.get("ElevatedTerrainPath").is_empty())
			var layer: TileMapLayer = grid.get_node(grid.get("TileMapLayerPath"))
			var expected := layer.to_global(layer.map_to_local(edit))
			assert(grid.call("CellToWorld", edit).distance_to(expected) < 0.01, "Grid did not follow lab framing")
			assert(grid.call("WorldToCell", expected) == edit, "Lab picking disagrees with grid")
		if "--capture" in OS.get_cmdline_user_args():
			await RenderingServer.frame_post_draw
			var capture := root.get_texture().get_image()
			assert(not capture.is_empty(), "Rendered viewport is empty")
			DirAccess.make_dir_recursive_absolute("res://tests/output/terrain_lab")
			assert(capture.save_png("res://tests/output/terrain_lab/view_%d.png" % projection) == OK)
	# Viewport-space panning and anchored zoom must survive transformed scene parents.
	var preview: Node2D = scene.get_node("Preview")
	chooser.select(0)
	chooser.item_selected.emit(0)
	var iso = scene.get_node("Preview/Iso")
	var hidden_rebuilds: Array[int] = [0]
	iso.connect("SurfaceRebuilt", func(): hidden_rebuilds[0] += 1)
	var previous_zoom := preview.scale.x
	world.set("Seed", 662)
	world.set("MapSize", 1)
	world.call("NewWorld")
	assert(world.get("BuiltSize") == Vector2i(48, 48) and preview.scale.x < previous_zoom, "Larger generated map did not reframe")
	await process_frame
	await process_frame
	assert(hidden_rebuilds[0] == 0, "Hidden isometric view rebuilt after cell replacement")
	assert(scene.get_node("Preview/IsoFeatures").get("Seed") == 662, "Hidden props did not receive the new world seed")
	assert(iso.get("BoundsSize") == Vector2i(48, 48) and scene.get_node("Preview/IsoFeatures").get("BoundsSize") == Vector2i(48, 48), "Inactive surface/props bounds diverged")
	assert(scene.get_node("Preview/IsoAutotile").get("BoundsSize") == Vector2i(48, 48))
	iso.show()
	await process_frame
	await process_frame
	assert(hidden_rebuilds[0] > 0, "Showing a deferred view did not refresh it")
	iso.hide()
	chooser.select(2)
	chooser.item_selected.emit(2)
	await process_frame
	assert(hidden_rebuilds[0] > 0 and iso.get("HasSurface"), "Reactivated isometric view did not rebuild")
	chooser.select(0)
	chooser.item_selected.emit(0)
	var holder := Node2D.new()
	holder.position = Vector2(73, -29)
	holder.rotation = 0.17
	holder.scale = Vector2(1.3, 0.8)
	scene.add_child(holder)
	preview.reparent(holder)
	var local_point := Vector2(180, 240)
	var screen_point := preview.get_global_transform_with_canvas() * local_point
	scene.call("ZoomPreviewAt", screen_point, 1.3)
	assert((preview.get_global_transform_with_canvas() * local_point).distance_to(screen_point) < 0.01, "Zoom drifted under transformed parent")
	var delta := Vector2(42, -18)
	scene.call("PanPreviewBy", delta)
	assert((preview.get_global_transform_with_canvas() * local_point).distance_to(screen_point + delta) < 0.01, "Pan used parent units instead of screen pixels")
	preview.reparent(scene)
	holder.free()
	scene.free()
	print("[terrain-lab-grid] OK")
	quit()
