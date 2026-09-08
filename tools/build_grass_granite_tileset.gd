extends SceneTree

var BASE := "res://addons/beep_game_builder_cs/generated/terrain_templates/grass_granite_tile_template_v1/"
const BITS := [TileSet.CELL_NEIGHBOR_RIGHT_SIDE, TileSet.CELL_NEIGHBOR_BOTTOM_SIDE,
    TileSet.CELL_NEIGHBOR_LEFT_SIDE, TileSet.CELL_NEIGHBOR_TOP_SIDE,
    TileSet.CELL_NEIGHBOR_TOP_RIGHT_CORNER, TileSet.CELL_NEIGHBOR_BOTTOM_RIGHT_CORNER,
    TileSet.CELL_NEIGHBOR_BOTTOM_LEFT_CORNER, TileSet.CELL_NEIGHBOR_TOP_LEFT_CORNER]

func _initialize() -> void:
    for argument in OS.get_cmdline_user_args():
        if argument.begins_with("--asset-dir="):
            BASE = argument.trim_prefix("--asset-dir=").trim_suffix("/") + "/"
    var manifest: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(BASE + "tile_manifest.json"))
    var tiles := TileSet.new()
    tiles.tile_size = Vector2i(64, 64)
    tiles.add_terrain_set()
    tiles.set_terrain_set_mode(0, TileSet.TERRAIN_MODE_MATCH_CORNERS_AND_SIDES)
    tiles.add_terrain(0)
    tiles.set_terrain_name(0, 0, "Grass + Granite")
    tiles.set_terrain_color(0, 0, Color("7c9137"))
    tiles.add_custom_data_layer()
    tiles.set_custom_data_layer_name(0, "connection_mask")
    tiles.set_custom_data_layer_type(0, TYPE_INT)
    var tops := TileSetAtlasSource.new()
    tops.texture = load(BASE + "terrain_tiles.png")
    tops.texture_region_size = Vector2i(64, 64)
    tiles.add_source(tops, 0)
    for entry in manifest["terrain_tiles"]:
        var coords := Vector2i(int(entry["atlas"][0]), int(entry["atlas"][1]))
        var mask := int(entry["mask"])
        tops.create_tile(coords)
        var data := tops.get_tile_data(coords, 0)
        data.terrain_set = 0
        data.terrain = 0
        data.set_custom_data("connection_mask", mask)
        for bit in range(8):
            data.set_terrain_peering_bit(BITS[bit], 0 if mask & (1 << bit) else -1)
    var walls := TileSetAtlasSource.new()
    walls.texture = load(BASE + "cliff_tiles.png")
    walls.texture_region_size = Vector2i(64, 80)
    tiles.add_source(walls, 1)
    for y in range(2):
        for x in range(24):
            walls.create_tile(Vector2i(x, y))
            walls.get_tile_data(Vector2i(x,y),0).texture_origin = Vector2i(0,-8)
    var error := ResourceSaver.save(tiles, BASE + "grass_granite_tileset.tres")
    if error != OK:
        push_error("Could not save TileSet: %d" % error)
        quit(1)
        return
    print("[grass-granite-build] PASS: 47 terrain configurations, 48 natural cliff tiles, terrain peering bits assigned")
    quit(0)
