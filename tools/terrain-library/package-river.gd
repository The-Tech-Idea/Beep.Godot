extends SceneTree
const BASE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/river_staging/"

func _initialize() -> void:
    call_deferred("run")

func run() -> void:
    var manifest: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(BASE + "manifest.json"))
    var tiles := TileSet.new()
    tiles.tile_size = Vector2i(64, 64)
    var names = ["terrain", "is_water", "passable", "flow_profile"]
    var types = [TYPE_STRING, TYPE_BOOL, TYPE_BOOL, TYPE_STRING]
    for i in range(names.size()):
        tiles.add_custom_data_layer()
        tiles.set_custom_data_layer_name(i, names[i])
        tiles.set_custom_data_layer_type(i, types[i])
    var river := TileSetAtlasSource.new()
    river.texture = load(BASE + str(manifest.file))
    river.texture_region_size = Vector2i(64, 64)
    tiles.add_source(river, 0)
    var coords_by_name := {}
    for entry in manifest.profiles:
        var coords := Vector2i(int(entry.atlas[0]), int(entry.atlas[1]))
        river.create_tile(coords)
        river.set_tile_animation_columns(coords, 1)
        river.set_tile_animation_separation(coords, Vector2i(0, 2))
        river.set_tile_animation_frames_count(coords, 16)
        river.set_tile_animation_speed(coords, 16.0 / 1.2)
        var data := river.get_tile_data(coords, 0)
        data.set_custom_data("terrain", "river")
        data.set_custom_data("is_water", true)
        data.set_custom_data("passable", false)
        data.set_custom_data("flow_profile", str(entry.id))
        coords_by_name[str(entry.id)] = coords
    var grass := TileSetAtlasSource.new()
    grass.texture = load(BASE + "runtime/grass_64.png")
    grass.texture_region_size = Vector2i(64, 64)
    tiles.add_source(grass, 1)
    grass.create_tile(Vector2i.ZERO)
    grass.get_tile_data(Vector2i.ZERO, 0).set_custom_data("terrain", "grass")
    grass.get_tile_data(Vector2i.ZERO, 0).set_custom_data("passable", true)
    DirAccess.make_dir_recursive_absolute(BASE + "godot")
    assert(ResourceSaver.save(tiles, BASE + "godot/river_current.tres") == OK)
    var loaded = ResourceLoader.load(BASE + "godot/river_current.tres", "", ResourceLoader.CACHE_MODE_IGNORE) as TileSet
    assert(loaded.get_source(0).get_tiles_count() == 12)
    for coords in coords_by_name.values():
        assert(loaded.get_source(0).get_tile_animation_frames_count(coords) == 16)
    var root_node := Node2D.new()
    root_node.name = "RiverCurrentReview"
    var layer := TileMapLayer.new()
    layer.name = "DirectionalRiver"
    layer.tile_set = loaded
    root_node.add_child(layer)
    layer.owner = root_node
    for y in range(9):
        for x in range(10):
            layer.set_cell(Vector2i(x,y), 1, Vector2i.ZERO)
    # Deliberate directional placement: terrain-connect alone cannot choose current direction.
    for cell in manifest.map:
        layer.set_cell(Vector2i(int(cell.x), int(cell.y)), 0, coords_by_name[str(cell.profile)])
    var packed := PackedScene.new()
    assert(packed.pack(root_node) == OK)
    assert(ResourceSaver.save(packed, BASE + "godot/river_current_review.tscn") == OK)
    root_node.free()
    var file := FileAccess.open("res://godot_validation.json", FileAccess.WRITE)
    file.store_string(JSON.stringify({"status":"passed","profiles":12,"frames":16,"visual_approval":false}, "  "))
    print("RIVER PASS: 12 directional tiles, 16 frames, explicit current placement")
    quit(0)
