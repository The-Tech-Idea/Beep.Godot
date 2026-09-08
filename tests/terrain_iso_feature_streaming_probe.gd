extends SceneTree

var failures: Array[String] = []
const BASE := "res://addons/beep_game_builder_cs/"

func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		push_error(message)

func _initialize() -> void: run.call_deferred()

func settle(view: Node) -> void:
	view.set_process(false)
	for i in 500:
		view.UpdateFeatureResidency()
		check(view.FeatureCellsProcessedLastFrame <= 128, "Isometric prop work exceeded budget")
		if view.PendingFeatureChunkCount == 0: break
	check(view.PendingFeatureChunkCount == 0, "Isometric visible chunks did not settle")

func run() -> void:
	root.size = Vector2i(800, 600)
	var host := Node2D.new()
	root.add_child(host)
	var authored: Node = load(BASE + "templates/scenes/terrain/terrain_iso_demo.tscn").instantiate()
	var iso: Node2D = authored.get_node("World/Iso")
	iso.get_parent().remove_child(iso)
	authored.free()
	var cells: Node = load(BASE + "ecs/grid/GridCellDataComponent.cs").new()
	cells.name = "Cells"
	host.add_child(cells)
	var origin := Vector2i(-30, 17)
	for center in [Vector2i(40, 40), Vector2i(220, 220), Vector2i(257, 257)]:
		for y in range(center.y - 3, mini(center.y + 3, 260)):
			for x in range(center.x - 3, mini(center.x + 3, 260)):
				cells.SetMetadata(origin + Vector2i(x, y), "terrain_feature", "woods")
				cells.SetMetadata(origin + Vector2i(x, y), "terrain_relief", 1 if x % 2 == 0 else 0)
	cells.SetTerrainKind(origin + Vector2i(40, 40), "deep_water")
	iso.TerrainGeneratorPath = NodePath("")
	iso.CellDataPath = NodePath("../Cells")
	iso.BoundsOrigin = origin
	iso.BoundsSize = Vector2i(260, 260)
	iso.CoastDetail = 1
	iso.position = Vector2(110, -90)
	iso.rotation = 0.15
	iso.scale = Vector2(0.8, 0.9)
	host.add_child(iso)
	iso.Rebuild()
	check(iso.HasSurface, "Isometric test surface missing")
	var camera := Camera2D.new()
	host.add_child(camera)
	camera.position = iso.to_global(iso.SurfacePosition(origin + Vector2i(40, 40)))
	camera.rotation = -0.3
	await process_frame
	camera.force_update_scroll()
	var view: Node2D = load(BASE + "ecs/terrain/TerrainIsometricFeatureRendererComponent.cs").new()
	view.IsometricRendererPath = NodePath("../Iso")
	view.RefreshOnReady = false
	view.BoundsSize = iso.BoundsSize
	view.FeaturePreloadChunks = 0
	view.FeatureCellsPerFrame = 128
	view.WoodsSheetPath = BASE + "textures/plants/forest_trees.png"
	view.position = Vector2(-35, 20)
	host.add_child(view)
	view.Rebuild()
	view.set_process(false)
	check(view.GetStampAnchors().is_empty(), "Isometric rebuild eagerly populated all props")
	settle(view)
	var actual: Array = Array(view.GetStampAnchors())
	check(not actual.is_empty(), "Visible isometric forest was culled")
	check(view.ResidentFeatureCellCount > 0 and view.ResidentFeatureCellCount < 16000, "Isometric residency scanned whole map")
	if DisplayServer.get_name() != "headless":
		# Isolate props so green terrain cannot satisfy the foliage pixel check.
		iso.hide()
		await process_frame
		RenderingServer.force_draw(false)
		var picture := root.get_texture().get_image()
		var green := 0
		for y in range(0, picture.get_height(), 4):
			for x in range(0, picture.get_width(), 4):
				var pixel := picture.get_pixel(x, y)
				if pixel.g > pixel.r * 1.15 and pixel.g > pixel.b * 1.15: green += 1
		check(green > 50, "Streamed isometric viewport has no rendered foliage")
		DirAccess.make_dir_recursive_absolute("res://tests/output/terrain_async")
		picture.save_png("res://tests/output/terrain_async/streamed_iso_props.png")
		iso.show()
	view.StreamLargeMaps = false
	view.Rebuild()
	var reference: Array = Array(view.GetStampAnchors())
	check(reference.size() > actual.size(), "Reference does not cover offscreen props")
	for anchor in actual: check(anchor in reference, "Streaming changed isometric prop placement")
	for anchor in reference:
		var screen: Vector2 = view.get_global_transform_with_canvas() * anchor
		if Rect2(Vector2.ZERO, Vector2(root.size)).has_point(screen):
			check(anchor in actual, "Streaming omitted a visible full-renderer anchor")
	var expected_layers: Array = view.GetLayerDiagnostics()
	view.StreamLargeMaps = true
	view.Rebuild()
	settle(view)
	var layers: Array = view.GetLayerDiagnostics()
	for i in layers.size():
		check(layers[i].z == expected_layers[i].z and layers[i].level == expected_layers[i].level, "Streaming changed elevation ordering")
	for center in [Vector2i(220, 220), Vector2i(258, 258)]:
		camera.position = iso.to_global(iso.SurfacePosition(origin + center))
		camera.force_update_scroll()
		settle(view)
		check(not view.GetStampAnchors().is_empty(), "Camera jump missed isometric props")
		for anchor in view.GetStampAnchors():
			check(anchor in reference and anchor not in actual, "Jump retained distant props or changed scatter")
		check(view.ResidentFeatureCellCount < 16000, "Camera jump leaked resident cells")
	view.hide()
	view.UpdateFeatureResidency()
	check(view.ResidentFeatureChunkCount == 0 and view.GetStampAnchors().is_empty(), "Hidden isometric view retained chunks")
	view.show()
	await process_frame
	settle(view)
	check(not view.GetStampAnchors().is_empty(), "Showing isometric view did not restore props")
	var detailed: Array = Array(view.GetStampAnchors())
	var detail_zoom := camera.zoom
	camera.zoom = detail_zoom * 0.001
	camera.force_update_scroll()
	view.UpdateFeatureResidency()
	check(view.IsFeatureDetailSuppressed and view.GetStampAnchors().is_empty() and view.ResidentFeatureCellCount == 0, "Isometric overview retained detail")
	check(view.FeatureCellsProcessedLastFrame == 0, "Suppressed isometric features still processed cells")
	camera.zoom = detail_zoom
	camera.force_update_scroll()
	settle(view)
	check(Array(view.GetStampAnchors()) == detailed, "Zoom restore changed isometric scatter")
	for y in range(254, 260):
		for x in range(254, 260): cells.SetMetadata(origin + Vector2i(x, y), "terrain_feature", "")
	await process_frame
	await process_frame
	settle(view)
	check(view.GetStampAnchors().is_empty(), "Surface rebuild retained cleared forest")
	iso.free()
	view.UpdateFeatureResidency()
	check(view.GetStampAnchors().is_empty(), "Freed surface retained prop stamps")
	host.free()
	print("[terrain-iso-feature-streaming] OK" if failures.is_empty() else "[terrain-iso-feature-streaming] FAILED")
	quit(0 if failures.is_empty() else 1)
