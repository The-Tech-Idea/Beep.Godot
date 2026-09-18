extends SceneTree

const DIR := "res://addons/beep_game_builder_cs/textures/map_art/"
const SCENES := "res://addons/beep_game_builder_cs/templates/scenes/terrain/"

func _initialize() -> void:
	call_deferred("run")

func region(texture: Texture2D, rect: Rect2) -> AtlasTexture:
	var result := AtlasTexture.new()
	result.atlas = texture
	result.region = rect
	result.filter_clip = true
	return result

func save(resource: Resource, path: String) -> void:
	assert(ResourceSaver.save(resource, path) == OK, path)

## Every frame of a sheet laid out in `columns` x `rows`, or one row of it.
func frames(texture: Texture2D, frame: Vector2i, columns: int, rows: int,
		first_row := 0, row_count := -1) -> Array[Texture2D]:
	var list: Array[Texture2D] = []
	var last: int = rows if row_count < 0 else first_row + row_count
	for row in range(first_row, last):
		for column in columns:
			list.append(region(texture, Rect2(column * frame.x, row * frame.y, frame.x, frame.y)))
	return list

## The style's own prop art: trees 4x2 of 64x128, bushes and rocks 4x2 of 32x32, the sizes the
## owner's sheets are named for. Rocks split by row - the top row is hill scatter, the bottom
## mountain scatter - so the two reliefs do not draw the same stone.
func apply_props(profile: Resource, prefix: String) -> void:
	var trees: Texture2D = load(DIR + prefix + "_trees.png")
	var bushes: Texture2D = load(DIR + prefix + "_bushes.png")
	var rocks: Texture2D = load(DIR + prefix + "_rocks.png")
	assert(trees != null and bushes != null and rocks != null, "missing prop sheets for " + prefix)
	profile.set("Trees", frames(trees, Vector2i(64, 128), 4, 2))
	profile.set("Bushes", frames(bushes, Vector2i(32, 32), 4, 2))
	profile.set("SmallRocks", frames(rocks, Vector2i(32, 32), 4, 2, 0, 1))
	profile.set("LargeRocks", frames(rocks, Vector2i(32, 32), 4, 2, 1, 1))

func run() -> void:
	var atlas: Texture2D = load(DIR + "pixel_terrain_atlas.png")
	assert(atlas.get_size() == Vector2(1254, 1254))
	var pixel: Resource = load("res://addons/beep_game_builder_cs/ecs/terrain/TerrainMapArt.cs").new()
	pixel.set("DisplayName", "Pixel Art")
	pixel.set("PixelArt", true)
	pixel.set("GrassColor", Color(0.43, 0.53, 0.29))
	pixel.set("DryGrassColor", Color(0.56, 0.59, 0.33))
	pixel.set("BeachColor", Color(0.76, 0.64, 0.43))
	pixel.set("ShallowWaterColor", Color(0.30, 0.57, 0.68))
	pixel.set("DeepWaterColor", Color(0.16, 0.38, 0.53))
	pixel.set("PixelsPerCell", 64)
	pixel.set("GroundDetail", 0.24)
	# THE SAME 1.6 CELLS AS EVERY OTHER STYLE. This wrote 4.8 - a 310-pixel atlas tile over 4.8 cells
	# of 64 pixels, one art pixel to one screen pixel, "which is what pixel art is drawn at". That is
	# true of the pixels and wrong about the picture: at 1:1 the objects painted into the atlas come
	# out at their authored size, so its starfish drew 22 pixels across and its shells 12 to 18,
	# beside a bush that TerrainPropSizing draws at 22 to 42. Ground read as scattered objects, which
	# is what the owner reported on 2026-09-18 - and what moving cartoon to 1.6 on 2026-09-16 fixed,
	# for cartoon only. Three times finer puts that starfish at 7 pixels, where it is texture.
	pixel.set("TextureRepeatCells", 1.6)
	pixel.set("GroundGrainStrength", 0.0)
	pixel.set("BlendWidth", 0.08)
	var grounds: Array[Texture2D] = []
	for rect: Rect2 in [Rect2(0, 0, 313, 314), Rect2(313, 0, 314, 314), Rect2(627, 0, 314, 314),
		Rect2(941, 0, 313, 314), Rect2(313, 314, 314, 314), Rect2(0, 314, 313, 314),
		Rect2(627, 314, 314, 314), Rect2(627, 314, 314, 314)]:
		grounds.append(region(atlas, rect.grow(-2)))
	pixel.set("GroundTextures", grounds)
	# The atlas still supplies the marsh reeds; the trees, bushes and rocks come from the style's own
	# sheets, which is what makes a style a style. This used to bind ONE tree region for every tree
	# on the map, from pixel_tree.png.
	pixel.set("Marsh", [region(atlas, Rect2(386, 1036, 183, 182))] as Array[Texture2D])
	apply_props(pixel, "pixel_art")
	save(pixel, DIR + "pixel_art.tres")

	# The other three styles: the same ground, at the same calibration, each with its own props. A
	# style is a resource, listed in TerrainLabComponent.StyleProfiles, so adding one is adding a
	# file here and an entry there - not another property and another branch.
	for style: Array in [["Cartoon", "cartoon"], ["Isometric Art", "isometric"], ["Low Poly", "low_poly"]]:
		var profile: Resource = load("res://addons/beep_game_builder_cs/ecs/terrain/TerrainMapArt.cs").new()
		profile.set("DisplayName", style[0])
		profile.set("GroundDetail", 0.16)
		profile.set("GroundTextures", grounds)
		profile.set("TextureRepeatCells", 1.6)
		apply_props(profile, style[1])
		save(profile, DIR + style[1] + ".tres")
	await build_roads(atlas)
	print("[terrain-style-assets] OK")
	quit()

func build_roads(atlas: Texture2D) -> void:
	assert(DisplayServer.get_name() != "headless", "Road atlas baking requires a rendering device")
	var tiles := TileSet.new()
	tiles.tile_size = Vector2i(64, 64)
	tiles.add_terrain_set()
	tiles.set_terrain_set_mode(0, TileSet.TERRAIN_MODE_MATCH_SIDES)
	var blank := GradientTexture2D.new()
	blank.width = 1024
	blank.height = 192
	blank.gradient = Gradient.new()
	blank.gradient.colors = PackedColorArray([Color.WHITE, Color.WHITE])
	var source := TileSetAtlasSource.new()
	source.texture = blank
	source.texture_region_size = Vector2i(64, 64)
	source.use_texture_padding = false
	tiles.add_source(source, 0)
	var names := ["Brick paving", "Sand path", "Mud track"]
	var sides := [TileSet.CELL_NEIGHBOR_TOP_SIDE, TileSet.CELL_NEIGHBOR_RIGHT_SIDE,
		TileSet.CELL_NEIGHBOR_BOTTOM_SIDE, TileSet.CELL_NEIGHBOR_LEFT_SIDE]
	for kind in 3:
		tiles.add_terrain(0)
		tiles.set_terrain_name(0, kind, names[kind])
		for mask in 16:
			var at := Vector2i(mask, kind)
			source.create_tile(at)
			var data := source.get_tile_data(at, 0)
			data.terrain_set = 0
			data.terrain = kind
			for side in 4:
				data.set_terrain_peering_bit(sides[side], kind if (mask & (1 << side)) != 0 else -1)
	var scene := Node2D.new()
	scene.name = "RoadTileCatalog"
	for style in 2:
		var material := ShaderMaterial.new()
		material.shader = load("res://addons/beep_game_builder_cs/shaders/terrain_roads.gdshader")
		material.set_shader_parameter("brick_texture", load(DIR + "brick.png"))
		material.set_shader_parameter("terrain_texture", atlas)
		material.set_shader_parameter("pixel_art", style == 0)
		save(material, DIR + ("pixel" if style == 0 else "cartoon") + "_roads.tres")
		# Bake native canvas rendering once. Runtime tiles need no custom shader.
		var viewport := SubViewport.new()
		viewport.size = Vector2i(1024, 192)
		viewport.transparent_bg = true
		viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
		root.add_child(viewport)
		var surface := TextureRect.new()
		surface.texture = blank
		surface.size = Vector2(1024, 192)
		surface.material = material
		viewport.add_child(surface)
		await process_frame
		await RenderingServer.frame_post_draw
		var image := viewport.get_texture().get_image()
		var prefix: String = "pixel" if style == 0 else "cartoon"
		assert(image.save_png(DIR + prefix + "_roads.png") == OK)
		image.generate_mipmaps()
		var baked := ImageTexture.create_from_image(image)
		save(baked, DIR + prefix + "_roads_texture.res")
		var styled_tiles := tiles.duplicate(true) as TileSet
		(styled_tiles.get_source(0) as TileSetAtlasSource).texture = baked
		save(styled_tiles, DIR + prefix + "_road_tileset.tres")
		viewport.free()
		var title := Label.new()
		title.name = "PixelTitle" if style == 0 else "CartoonTitle"
		title.text = "Pixel Art" if style == 0 else "Cartoon"
		title.position = Vector2(35 + style * 600, 10)
		scene.add_child(title)
		title.owner = scene
		var layer := TileMapLayer.new()
		layer.name = "PixelRoads" if style == 0 else "CartoonRoads"
		layer.position = Vector2(66 + style * 600, 94)
		layer.tile_set = styled_tiles
		layer.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST_WITH_MIPMAPS if style == 0 else CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
		scene.add_child(layer)
		layer.owner = scene
		for kind in 3:
			var cells: Array[Vector2i] = []
			for x in 7: cells.append(Vector2i(x, kind * 3))
			for x in [0, 2, 4, 6]: cells.append(Vector2i(x, kind * 3 + 1))
			cells.append(Vector2i(4, kind * 3 - 1))
			layer.set_cells_terrain_connect(cells, 0, kind, false)
			var label := Label.new()
			label.name = "Material" + str(style) + str(kind)
			label.text = names[kind]
			label.position = Vector2(35 + style * 600, 62 + kind * 192)
			scene.add_child(label)
			label.owner = scene
	var packed := PackedScene.new()
	assert(packed.pack(scene) == OK)
	save(packed, SCENES + "terrain_road_tiles_demo.tscn")
	scene.free()
