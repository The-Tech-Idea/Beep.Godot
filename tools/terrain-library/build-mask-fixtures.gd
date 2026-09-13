extends SceneTree

const BASE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/ground_masks/staging/"
const OUTPUT = "res://addons/beep_game_builder_cs/generated/test/library/output/mask_fixtures.json"
const SHADER = "res://addons/beep_game_builder_cs/shaders/terrain_mask_surface.gdshader"
var calibrated_surfaces: Dictionary = {}

func prepare_surfaces() -> bool:
	var folder = BASE + "runtime/surfaces_v1/"
	DirAccess.make_dir_recursive_absolute(folder)
	for kind in ["grass", "dirt"]:
		var source = Image.load_from_file(BASE + "sources/plain_" + kind + "_surface_v1.png")
		if source == null:
			push_error("Missing surface source: " + kind)
			return false
		# Four cells per repeat: cartoon master 4*128; runtime 4*64.
		var master = source.duplicate() as Image
		master.resize(512, 512, Image.INTERPOLATE_LANCZOS)
		if master.save_png(folder + kind + "_master512.png") != OK:
			return false
		var runtime = master.duplicate() as Image
		runtime.resize(256, 256, Image.INTERPOLATE_LANCZOS)
		if runtime.save_png(folder + kind + "_256.png") != OK:
			return false
		var texture = ImageTexture.create_from_image(runtime)
		var resource_path = folder + kind + "_256.res"
		if ResourceSaver.save(texture, resource_path) != OK:
			return false
		calibrated_surfaces[kind] = load(resource_path)
	return true

func _initialize() -> void:
	call_deferred("run")

func normalize(mask: int) -> int:
	for i in range(4):
		if not (mask & (1 << i)) or not (mask & (1 << ((i + 1) % 4))):
			mask &= ~(1 << (i + 4))
	return mask

func distance_to_edge(mask: int, p: Vector2) -> float:
	var delta = (p - Vector2(31.5, 31.5)).abs()
	var horizontal = 3 if p.x < 31.5 else 1
	var vertical = 0 if p.y < 31.5 else 2
	var diagonal = (7 if p.x < 31.5 else 4) if p.y < 31.5 else (6 if p.x < 31.5 else 5)
	var h = bool(mask & (1 << horizontal))
	var v = bool(mask & (1 << vertical))
	if h and v and mask & (1 << diagonal):
		return 64.0
	if h and v:
		return (Vector2(31.5, 31.5) - delta).length() - (31.5 - 21.76)
	if h:
		return 21.76 - delta.y
	if v:
		return 21.76 - delta.x
	return 21.76 - delta.length()

func checked_save(resource: Resource, location: String) -> void:
	assert(ResourceSaver.save(resource, location) == OK, "Cannot save " + location)

func build(iso: bool, masks: Array[int], surface_art: bool = false) -> Dictionary:
	var projection = "isometric" if iso else "square"
	var folder = BASE + projection + "/"
	if surface_art:
		folder += "surface_candidate_v1/"
	DirAccess.make_dir_recursive_absolute(folder)
	var size = Vector2i(64, 32 if iso else 64)
	var image = Image.create(size.x * 8, size.y * 6, false, Image.FORMAT_RGBA8)
	image.fill(Color.TRANSPARENT)
	var tiles = TileSet.new()
	tiles.tile_size = size
	if iso:
		tiles.tile_shape = TileSet.TILE_SHAPE_ISOMETRIC
		tiles.tile_layout = TileSet.TILE_LAYOUT_DIAMOND_DOWN
	tiles.add_terrain_set()
	tiles.set_terrain_set_mode(0, TileSet.TERRAIN_MODE_MATCH_CORNERS_AND_SIDES)
	tiles.add_terrain(0)
	tiles.set_terrain_name(0, 0, "grass")
	# Logical N/E/S/W and NE/SE/SW/NW are projected onto native neighbors.
	var bits = [TileSet.CELL_NEIGHBOR_TOP_RIGHT_SIDE, TileSet.CELL_NEIGHBOR_BOTTOM_RIGHT_SIDE, TileSet.CELL_NEIGHBOR_BOTTOM_LEFT_SIDE, TileSet.CELL_NEIGHBOR_TOP_LEFT_SIDE, TileSet.CELL_NEIGHBOR_RIGHT_CORNER, TileSet.CELL_NEIGHBOR_BOTTOM_CORNER, TileSet.CELL_NEIGHBOR_LEFT_CORNER, TileSet.CELL_NEIGHBOR_TOP_CORNER] if iso else [TileSet.CELL_NEIGHBOR_TOP_SIDE, TileSet.CELL_NEIGHBOR_RIGHT_SIDE, TileSet.CELL_NEIGHBOR_BOTTOM_SIDE, TileSet.CELL_NEIGHBOR_LEFT_SIDE, TileSet.CELL_NEIGHBOR_TOP_RIGHT_CORNER, TileSet.CELL_NEIGHBOR_BOTTOM_RIGHT_CORNER, TileSet.CELL_NEIGHBOR_BOTTOM_LEFT_CORNER, TileSet.CELL_NEIGHBOR_TOP_LEFT_CORNER]
	var atlas = TileSetAtlasSource.new()
	atlas.texture_region_size = size
	# Allocate the texture before creating atlas regions.
	atlas.texture = ImageTexture.create_from_image(image)
	tiles.add_source(atlas, 0)
	for index in range(48):
		var coords = Vector2i(index % 8, index / 8)
		atlas.create_tile(coords)
		var data = atlas.get_tile_data(coords, 0)
		data.terrain_set = 0
		data.terrain = 0 if index < 47 else -1
		for bit_index in range(8):
			if not data.is_valid_terrain_peering_bit(bits[bit_index]):
				push_error("Invalid neighbor %s in %s tile %s" % [bits[bit_index], projection, index])
				return {}
			data.set_terrain_peering_bit(bits[bit_index], 0 if index < 47 and masks[index] & (1 << bit_index) else -1)
		for y in range(size.y):
			for x in range(size.x):
				var p = Vector2(x, y)
				if iso:
					var dx = (x + 0.5 - 32.0) / 64.0
					var dy = (y + 0.5 - 16.0) / 32.0
					p = Vector2(dx + dy + 0.5, -dx + dy + 0.5) * 64.0 - Vector2(0.5, 0.5)
					if p.x < -0.5 or p.y < -0.5 or p.x >= 63.5 or p.y >= 63.5:
						continue
				var color = Color("d6ae70")
				if surface_art:
					color = Color.BLUE
				if index < 47:
					var distance = distance_to_edge(masks[index], p)
					if distance >= 1.0:
						color = Color.RED
					elif distance >= -0.5:
						color = Color("658c49")
				image.set_pixelv(coords * size + Vector2i(x, y), color)
	assert(image.save_png(folder + "grass_dirt_masks.png") == OK)
	atlas.texture = ImageTexture.create_from_image(image)
	if surface_art:
		checked_save(atlas.texture, folder + "grass_dirt_masks.res")
		atlas.texture = load(folder + "grass_dirt_masks.res")
	checked_save(tiles, folder + "grass_dirt_masks.tres")
	if surface_art:
		tiles = load(folder + "grass_dirt_masks.tres")
	var surface = Image.create(16, 16, false, Image.FORMAT_RGBA8)
	surface.fill(Color("91b968"))
	var material = ShaderMaterial.new()
	material.shader = load(SHADER)
	if surface_art:
		material.shader = load("res://addons/beep_game_builder_cs/shaders/terrain_mask_surface_cartoon.gdshader")
	material.set_shader_parameter("surface_enabled", true)
	material.set_shader_parameter("surface_texture", ImageTexture.create_from_image(surface))
	if surface_art:
		material.set_shader_parameter("surface_texture", calibrated_surfaces.grass)
		material.set_shader_parameter("secondary_enabled", true)
		material.set_shader_parameter("secondary_texture", calibrated_surfaces.dirt)
	if iso:
		material.set_shader_parameter("surface_u", Vector2(1.0 / 256.0, 1.0 / 128.0))
		material.set_shader_parameter("surface_v", Vector2(-1.0 / 256.0, 1.0 / 128.0))
	var root = Node2D.new()
	root.name = "MaskConnectionFixture"
	var layer = TileMapLayer.new()
	layer.name = "Ground"
	layer.tile_set = tiles
	layer.material = material
	layer.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	root.add_child(layer)
	layer.owner = root
	if surface_art:
		var plane = load("res://addons/beep_game_builder_cs/ecs/terrain/TerrainSurfacePlane.gd").new()
		plane.name = "SurfacePlane"
		plane.projection = 1 if iso else 0
		plane.cell_size = Vector2(size)
		plane.layer_paths.assign([NodePath("../Ground")])
		root.add_child(plane)
		plane.owner = root
	var cells: Array[Vector2i] = []
	for y in range(12):
		for x in range(16):
			var cell = Vector2i(x, y)
			layer.set_cell(cell, 0, Vector2i(7, 5))
			if x > 1 and x < 14 and y > 1 and y < 10 and not (x >= 6 and x <= 8 and y >= 5 and y <= 7):
				cells.append(cell)
	layer.set_cells_terrain_connect(cells, 0, 0, false)
	for cell in cells:
		assert(layer.get_cell_tile_data(cell) != null and layer.get_cell_tile_data(cell).terrain == 0)
		assert(layer.local_to_map(layer.map_to_local(cell)) == cell)
	var scene = PackedScene.new()
	assert(scene.pack(root) == OK)
	checked_save(scene, folder + "grass_dirt_review.tscn")
	assert(ResourceLoader.load(folder + "grass_dirt_review.tscn", "", ResourceLoader.CACHE_MODE_IGNORE) != null)
	root.free()
	return {"projection": projection, "configurations": masks.size(), "painted_cells": cells.size(), "native_neighbors_valid": true, "save_reload": true, "visual_approval": false, "status": "surface_candidate" if surface_art else "technical_fixture_not_production"}

func run() -> void:
	var masks: Array[int] = []
	for mask in range(256):
		if normalize(mask) == mask:
			masks.append(mask)
	assert(masks.size() == 47)
	var surface_art = "--surface-art" in OS.get_cmdline_user_args()
	if surface_art and not prepare_surfaces():
		quit(1)
		return
	var report = {"fixtures": [build(false, masks, surface_art), build(true, masks, surface_art)], "limitations": ["Surface candidates and technical fixtures are not approved artwork.", "Complete rendered seam coverage and visual acceptance remain pending."]}
	for fixture in report.fixtures:
		if fixture.is_empty():
			quit(1)
			return
	DirAccess.make_dir_recursive_absolute(OUTPUT.get_base_dir())
	var file = FileAccess.open(OUTPUT.replace("mask_fixtures", "surface_candidates") if surface_art else OUTPUT, FileAccess.WRITE)
	file.store_string(JSON.stringify(report, "  "))
	print("MASK FIXTURES PASS: square and isometric native terrain resources; no visual approval claimed")
	quit()
