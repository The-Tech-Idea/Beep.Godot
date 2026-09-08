extends SceneTree

const CELLS = preload("res://addons/beep_game_builder_cs/ecs/grid/GridCellDataComponent.cs")
var failures: Array[String] = []

func _initialize() -> void:
	call_deferred("run")

func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		printerr("[terrain-iso-cliff] FAIL: " + message)

func settle() -> void:
	await process_frame
	await process_frame
	await RenderingServer.frame_post_draw

func fixture() -> ImageTexture:
	var image := Image.create(828, 1712, false, Image.FORMAT_RGBA8)
	image.fill(Color.BLUE)
	for y in range(107):
		for x in range(92):
			var upper := absf(x + 0.5 - 46) * 53.0 / 92.0
			var lower := 53.0 - upper
			var colour := Color(0, 0, 0, 0)
			if y + 0.5 >= upper and y + 0.5 < lower:
				colour = Color(0, 0.6 + 0.3 * (float((x / 3 + y / 3) % 2)), 0, 1)
			elif y + 0.5 >= lower and y + 0.5 < lower + 54:
				colour = Color.RED
			image.set_pixel(x, y, colour)
	image.generate_mipmaps()
	return ImageTexture.create_from_image(image)

func counts(image: Image) -> Vector3i:
	var result := Vector3i.ZERO
	for y in range(128, 390):
		for x in range(64, 448):
			var colour := image.get_pixel(x, y)
			if colour.g > 0.45 and colour.r < 0.1 and colour.b < 0.1:
				result.x += 1
			if colour.r > 0.9 and colour.g < 0.1 and colour.b < 0.1:
				result.y += 1
			if colour.b > 0.2:
				result.z += 1
	return result

func run() -> void:
	Engine.max_fps = 120
	root.size = Vector2i(512, 512)
	root.transparent_bg = true
	var host := Node2D.new()
	root.add_child(host)
	var cells = CELLS.new()
	cells.name = "Cells"
	host.add_child(cells)
	# This synthetic 92x107 fixture must not inherit the demo's choice of atlas.
	var iso: Node2D = load("res://addons/beep_game_builder_cs/ecs/terrain/TerrainIsometricRendererComponent.cs").new()
	iso.set("BlockSheetPath", "res://addons/beep_game_builder_cs/textures/iso/voxel_blocks.png")
	iso.set("TopSheetPath", "res://addons/beep_game_builder_cs/textures/iso/voxel_tops_seamless.png")
	iso.set("SheetColumns", 9)
	iso.set("SheetRows", 16)
	iso.set("CellSize", Vector2i(92, 53))
	iso.set("BlockLift", 27)
	iso.set("TopLift", 0)
	iso.set("GrassFrame", 0)
	cells.call("SetTerrainKind", Vector2i.ZERO, "grass")
	iso.set("RefreshOnReady", false)
	iso.set("CellDataPath", NodePath("../Cells"))
	iso.set("BoundsSize", Vector2i.ONE)
	iso.set("TerrainVariants", PackedStringArray())
	host.add_child(iso)
	var texture := fixture()
	DirAccess.make_dir_recursive_absolute("res://tests/output/iso_cliff")
	for zoom in [1.0, 2.0, 0.5]:
		var captures: Array[Image] = []
		for height in [54, 28]:
			iso.set("LevelHeight", height)
			iso.call("Rebuild")
			var ground: TileMapLayer = iso.get_node("IsoLevel1")
			var atlas: TileSetAtlasSource = ground.tile_set.get_source(0)
			atlas.texture = texture
			check((atlas.get_tile_data(Vector2i.ZERO, 0).material != null) == (height == 28), "Side-fit material did not follow elevation spacing")
			for child in iso.get_children():
				if child is CanvasItem:
					child.visible = child == ground
			host.scale = Vector2.ONE * zoom
			host.position = Vector2(256, 210) - iso.call("SurfacePosition", Vector2i.ZERO) * zoom
			await settle()
			var capture := root.get_texture().get_image()
			captures.append(capture)
			capture.save_png("res://tests/output/iso_cliff/height_%d_zoom_%s.png" % [height, zoom])
		var original := counts(captures[0])
		var fitted := counts(captures[1])
		var changed_top := 0
		for y in range(128, 260):
			for x in range(64, 448):
				var colour := captures[0].get_pixel(x, y)
				if colour.g > 0.45 and colour.r < 0.01 and colour.a > 0.95:
					var other := captures[1].get_pixel(x, y)
					if absf(colour.g - other.g) > 0.01:
						changed_top += 1
		check(changed_top == 0, "Side fitting altered top texture detail")
		check(original.x > 100 and fitted.x > 100, "Top diamond missing")
		check(absf(float(fitted.x - original.x)) / original.x < 0.08, "Side fitting resized the top diamond")
		check(original.y > 100 and float(fitted.y) / original.y > 0.4 and float(fitted.y) / original.y < 0.61, "Cliff face was not shortened to the authored spacing")
		check(fitted.z == 0, "Fitted sides sampled an adjacent atlas frame")
		print("[terrain-iso-cliff] zoom ", zoom, " top/side/bleed original=", original, " fitted=", fitted)
	host.free()
	if failures.is_empty():
		print("[terrain-iso-cliff] OK")
	quit(0 if failures.is_empty() else 1)
