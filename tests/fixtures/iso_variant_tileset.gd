# The smallest authored isometric TileSet TerrainIsometricAutotileRendererComponent
# accepts with UseTerrainConnections off: one terrain set, one terrain, and N
# alternative tiles for it at atlas coords (0,0)..(N-1,0) - so a painted cell's atlas x
# IS its variant index, and a probe can read which variant a cell was given straight
# off the layer.
#
# Shared by terrain_variant_choice_probe.gd and terrain_autotile_staleness_probe.gd.
extends RefCounted

static func build(variants: int, cell: Vector2i = Vector2i(64, 32)) -> TileSet:
	var image := Image.create(cell.x * variants, cell.y, false, Image.FORMAT_RGBA8)
	for i in variants:
		image.fill_rect(Rect2i(i * cell.x, 0, cell.x, cell.y), Color(float(i + 1) / variants, 0.5, 0.5))
	var atlas := TileSetAtlasSource.new()
	atlas.texture = ImageTexture.create_from_image(image)
	atlas.texture_region_size = cell

	var tiles := TileSet.new()
	tiles.tile_shape = TileSet.TILE_SHAPE_ISOMETRIC
	tiles.tile_layout = TileSet.TILE_LAYOUT_DIAMOND_DOWN
	tiles.tile_offset_axis = TileSet.TILE_OFFSET_AXIS_HORIZONTAL
	tiles.tile_size = cell
	tiles.add_terrain_set()
	tiles.set_terrain_set_mode(0, TileSet.TERRAIN_MODE_MATCH_CORNERS_AND_SIDES)
	tiles.add_terrain(0)
	# The atlas joins the set BEFORE its tiles claim a terrain: TileData validates
	# terrain_set against the owning TileSet.
	tiles.add_source(atlas, 0)
	for i in variants:
		atlas.create_tile(Vector2i(i, 0))
		var data := atlas.get_tile_data(Vector2i(i, 0), 0)
		data.terrain_set = 0
		data.terrain = 0
	return tiles
