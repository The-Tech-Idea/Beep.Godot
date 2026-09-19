extends SceneTree

var failures: Array[String] = []
func check(value: bool, message: String) -> void:
	if not value:
		failures.append(message)
		push_error(message)

func _initialize() -> void: run.call_deferred()

# `stream` is stated, never inferred from the size. This probe compares a STREAMED renderer against
# a FULL one, and it used to get the second by making it small enough to fall under the streaming
# threshold - so when that threshold moved down to where real maps are (2026-09-19, it was 65,536
# cells and no map this game generates reached it), the 128x128 reference quietly began streaming
# too and the comparison became a streamed map against itself.
func make_view(host: Node, size: Vector2i, stream: bool = true) -> Node2D:
	var view: Node2D = load("res://addons/beep_game_builder_cs/ecs/terrain/TerrainFeatureRendererComponent.cs").new()
	view.CellDataPath = NodePath("../Cells")
	view.RefreshOnReady = false
	view.StreamLargeMaps = stream
	view.BoundsSize = size
	view.WoodsSheetPath = "res://addons/beep_game_builder_cs/textures/plants/forest_trees.png"
	view.FeatureCellsPerFrame = 128
	view.FeaturePreloadChunks = 0
	host.add_child(view)
	view.Rebuild()
	view.set_process(false)
	return view

func settle(view: Node) -> void:
	for i in 300:
		view.UpdateFeatureResidency()
		check(view.FeatureCellsProcessedLastFrame <= 128, "Feature work exceeded cell budget")
		if view.PendingFeatureChunkCount == 0: break
	check(view.PendingFeatureChunkCount == 0, "Visible feature chunks did not settle")

func capture(name: String) -> void:
	if DisplayServer.get_name() == "headless": return
	RenderingServer.force_draw(false)
	var picture := root.get_texture().get_image()
	var green := 0
	for y in range(0, picture.get_height(), 4):
		for x in range(0, picture.get_width(), 4):
			var pixel := picture.get_pixel(x, y)
			if pixel.g > pixel.r * 1.15 and pixel.g > pixel.b * 1.15: green += 1
	check(green > 50, "Streamed prop viewport contains no visible foliage")
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path("res://tests/output/terrain_async"))
	picture.save_png("res://tests/output/terrain_async/" + name + ".png")

func run() -> void:
	root.size = Vector2i(800, 600)
	var host := Node2D.new()
	root.add_child(host)
	var cells: Node = load("res://addons/beep_game_builder_cs/ecs/grid/GridCellDataComponent.cs").new()
	cells.name = "Cells"
	host.add_child(cells)
	for center in [Vector2i(64, 64), Vector2i(700, 700), Vector2i(1020, 1020)]:
		for y in range(center.y - 4, mini(center.y + 4, 1024)):
			for x in range(center.x - 4, mini(center.x + 4, 1024)):
				cells.SetMetadata(Vector2i(x, y), "terrain_feature", "woods")
	for y in range(60, 64): cells.SetTerrainKind(Vector2i(64, y), "deep_water")
	var camera := Camera2D.new()
	host.add_child(camera)
	camera.position = Vector2(64, 64) * 64
	await process_frame
	camera.force_update_scroll()
	var view := make_view(host, Vector2i(1024, 1024))
	check(view.StampCount == 0 and view.ResidentFeatureCellCount == 0, "Rebuild eagerly generated the entire prop map")
	settle(view)
	check(view.ResidentFeatureCellCount > 0 and view.ResidentFeatureCellCount < 20000, "Feature residency not bounded to viewport")
	await process_frame
	capture("streamed_props")
	# The control: the same map built in one pass, so "streamed placement matches full placement"
	# compares two different things.
	var reference := make_view(host, Vector2i(128, 128), false)
	var expected: Array = Array(reference.GetStampAnchors())
	var actual: Array = Array(view.GetStampAnchors())
	check(not actual.is_empty() and actual.size() == expected.size(), "Streamed count differs from full renderer")
	for anchor in actual: check(anchor in expected, "Chunk scatter changed deterministic placement")
	for anchor in actual:
		check(cells.GetTerrainKind(Vector2i(floori(anchor.x / 64), floori(anchor.y / 64))) != "deep_water", "Tree anchored in deep water")
	reference.free()
	var before: int = view.StampCount
	cells.SetMetadata(Vector2i(60, 60), "terrain_feature", "")
	settle(view)
	check(view.StampCount < before, "Live edit retained cleared prop")
	camera.position = Vector2(700, 700) * 64
	camera.force_update_scroll()
	settle(view)
	check(view.StampCount > 0 and view.ResidentFeatureCellCount < 20000, "Camera jump leaked old props or missed new ones")
	for anchor in view.GetStampAnchors(): check(anchor.x > 600 * 64 and anchor.y > 600 * 64, "Old chunk props survived camera jump")
	camera.position = Vector2(1023, 1023) * 64
	camera.rotation = 0.5
	camera.force_update_scroll()
	settle(view)
	for anchor in view.GetStampAnchors(): check(Rect2(0, 0, 1024 * 64, 1024 * 64).has_point(anchor), "Edge chunk spawned outside finite bounds")
	await process_frame
	capture("streamed_props_edge")
	view.visible = false
	view.UpdateFeatureResidency()
	check(view.StampCount == 0 and view.ResidentFeatureChunkCount == 0, "Hidden renderer retained props")
	view.visible = true
	await process_frame
	view.set_process(false)
	settle(view)
	check(view.StampCount > 0, "Shown renderer did not restore props")
	var detailed: Array = Array(view.GetStampAnchors())
	camera.zoom = Vector2(0.01, 0.01)
	camera.force_update_scroll()
	view.UpdateFeatureResidency()
	check(view.IsFeatureDetailSuppressed and view.StampCount == 0 and view.ResidentFeatureCellCount == 0, "Overview retained tiny feature detail")
	check(view.FeatureCellsProcessedLastFrame == 0 and view.PendingFeatureChunkCount == 0, "Suppressed features still generated whole-map detail")
	camera.zoom = Vector2(0.03, 0.03)
	camera.force_update_scroll()
	view.UpdateFeatureResidency()
	check(view.IsFeatureDetailSuppressed, "Feature cutoff lacked hysteresis")
	view.MinimumDetailCellPixels = 0.0
	view.UpdateFeatureResidency()
	check(not view.IsFeatureDetailSuppressed and view.FeatureCellsProcessedLastFrame > 0, "Zero cutoff did not disable suppression")
	view.MinimumDetailCellPixels = 1.5
	camera.zoom = Vector2.ONE
	camera.force_update_scroll()
	settle(view)
	check(Array(view.GetStampAnchors()) == detailed, "Zoom restore changed deterministic feature placement")
	var grid: Node2D = load("res://addons/beep_game_builder_cs/ecs/grid/GridProjectionComponent.cs").new()
	grid.name = "Grid"
	grid.DrawGrid = false
	grid.Projection = 1
	grid.TileSize = Vector2(64, 32)
	grid.position = Vector2(100, -200)
	grid.rotation = 0.15
	host.add_child(grid)
	view.GridPath = NodePath("../Grid")
	view.position = Vector2(40, -70)
	view.scale = Vector2(0.8, 0.9)
	view.Rebuild()
	view.set_process(false)
	camera.position = grid.CellToWorld(Vector2i(700, 700))
	camera.rotation = -0.4
	camera.force_update_scroll()
	settle(view)
	check(view.StampCount > 0 and view.ResidentFeatureCellCount < 20000, "Transformed grid failed to load visible chunks")
	for anchor in view.GetStampAnchors():
		var cell: Vector2i = grid.WorldToCell(view.to_global(anchor))
		check(cell.x > 690 and cell.x < 710 and cell.y > 690 and cell.y < 710, "Transformed grid culled or positioned the wrong prop region")
	# VIEW-02: the detail cutoff measures the cell the grid BINDS, not the grid's manual TileSize
	# export. A native 96x48 layer at zoom 0.02 draws 1.92 px cells, above the 1.5 px cutoff;
	# measured on the export's 64 px it read 1.28 px and hid every prop on a map drawn at 96x48.
	var native := TileMapLayer.new()
	native.name = "Native"
	native.tile_set = TileSet.new()
	native.tile_set.tile_size = Vector2i(96, 48)
	host.add_child(native)
	grid.Projection = 0
	grid.TileSize = Vector2(64, 64)
	grid.position = Vector2.ZERO
	grid.rotation = 0.0
	grid.TileMapLayerPath = NodePath("../Native")
	check(grid.EffectiveTileSize == Vector2(96, 48), "Grid bound to a 96x48 layer reported %s" % str(grid.EffectiveTileSize))
	view.position = Vector2.ZERO
	view.scale = Vector2.ONE
	view.Rebuild()
	view.set_process(false)
	camera.rotation = 0.0
	camera.zoom = Vector2(0.02, 0.02)
	camera.position = native.to_global(native.map_to_local(Vector2i(700, 700)))
	camera.force_update_scroll()
	view.UpdateFeatureResidency()
	check(not view.IsFeatureDetailSuppressed, "Detail cutoff measured the grid's manual TileSize instead of the bound 96x48 layer")
	host.free()
	print("[terrain-feature-streaming] OK" if failures.is_empty() else "[terrain-feature-streaming] FAILED")
	quit(0 if failures.is_empty() else 1)
