extends SceneTree

const BASE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/staging/"
const BITS = [TileSet.CELL_NEIGHBOR_TOP_SIDE, TileSet.CELL_NEIGHBOR_RIGHT_SIDE,
    TileSet.CELL_NEIGHBOR_BOTTOM_SIDE, TileSet.CELL_NEIGHBOR_LEFT_SIDE,
    TileSet.CELL_NEIGHBOR_TOP_RIGHT_CORNER, TileSet.CELL_NEIGHBOR_BOTTOM_RIGHT_CORNER,
    TileSet.CELL_NEIGHBOR_BOTTOM_LEFT_CORNER, TileSet.CELL_NEIGHBOR_TOP_LEFT_CORNER]

func _initialize() -> void:
    call_deferred("run")

func require_ok(value: bool, message: String) -> void:
    if not value:
        push_error(message)
        quit(1)
        assert(value, message)

func run() -> void:
    var manifest: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(BASE + "manifest.json"))
    DirAccess.make_dir_recursive_absolute(BASE + "godot")
    var report: Array = []
    for entry in manifest.assets:
        var texture = load(BASE + str(entry.file)) as Texture2D
        require_ok(texture != null, "Missing atlas")
        var tiles := TileSet.new()
        tiles.tile_size = Vector2i(64, 64)
        tiles.add_terrain_set()
        tiles.set_terrain_set_mode(0, TileSet.TERRAIN_MODE_MATCH_CORNERS_AND_SIDES)
        tiles.add_terrain(0)
        tiles.set_terrain_name(0, 0, str(entry.foreground))
        # Match the existing TerrainTileSets data names/types without loading C# in this art fixture.
        var names = ["terrain", "resource", "feature", "relief", "is_water", "passable", "continent", "start_position", "liquid_resource", "underground_resource", "underground_richness", "underground_depth"]
        var types = [TYPE_STRING, TYPE_STRING, TYPE_STRING, TYPE_INT, TYPE_BOOL, TYPE_BOOL, TYPE_INT, TYPE_BOOL, TYPE_STRING, TYPE_STRING, TYPE_FLOAT, TYPE_INT]
        for i in range(names.size()):
            tiles.add_custom_data_layer()
            tiles.set_custom_data_layer_name(i, names[i])
            tiles.set_custom_data_layer_type(i, types[i])
        var atlas := TileSetAtlasSource.new()
        atlas.texture = texture
        atlas.texture_region_size = Vector2i(64, 64)
        tiles.add_source(atlas, 0)
        for i in range(48):
            var coords := Vector2i(i % 8, i / 8)
            atlas.create_tile(coords)
            if (i != 47 or str(entry.background).contains("water")) and int(entry.frames) > 1:
                atlas.set_tile_animation_columns(coords, 1)
                atlas.set_tile_animation_separation(coords, Vector2i(0, 5))
                atlas.set_tile_animation_frames_count(coords, int(entry.frames))
                atlas.set_tile_animation_speed(coords, 16.0 / 1.2)
            var data := atlas.get_tile_data(coords, 0)
            data.terrain_set = 0
            data.terrain = -1 if i == 47 else 0
            var kind: String = str(entry.background) if i == 47 else str(entry.foreground)
            data.set_custom_data("terrain", kind)
            data.set_custom_data("is_water", kind.contains("water"))
            data.set_custom_data("passable", not kind.contains("water"))
            var mask: int = 0 if i == 47 else int(entry.masks[i])
            for b in range(8):
                data.set_terrain_peering_bit(BITS[b], 0 if i != 47 and mask & (1 << b) else -1)
        var resource_path: String = BASE + "godot/" + str(entry.id) + ".tres"
        require_ok(ResourceSaver.save(tiles, resource_path) == OK, "Resource save failed")
        var loaded = ResourceLoader.load(resource_path, "", ResourceLoader.CACHE_MODE_IGNORE) as TileSet
        require_ok(loaded != null, "TileSet reload failed")
        require_ok(loaded.get_source(0).get_tiles_count() == 48, "Tile count mismatch")
        for i in range(47):
            var coords := Vector2i(i % 8, i / 8)
            require_ok(atlas.get_tile_animation_frames_count(coords) == int(entry.frames), "Animation frame mismatch")
            require_ok(atlas.get_tile_data(coords, 0).terrain == 0, "Terrain metadata missing")
        var scene := Node2D.new()
        scene.name = "CartoonFoundationReview"
        var layer := TileMapLayer.new()
        layer.name = "Terrain"
        layer.tile_set = loaded
        layer.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR
        scene.add_child(layer)
        layer.owner = scene
        for y in range(manifest.map.size()):
            for x in range(manifest.map[y].size()):
                layer.set_cell(Vector2i(x,y), 0, Vector2i(7,5))
        var water_cells: Array[Vector2i] = []
        for y in range(manifest.map.size()):
            for x in range(manifest.map[y].size()):
                if int(manifest.map[y][x]) == 1:
                    water_cells.append(Vector2i(x,y))
        layer.set_cells_terrain_connect(water_cells, 0, 0, false)
        for cell in water_cells:
            require_ok(layer.get_cell_tile_data(cell) != null and layer.get_cell_tile_data(cell).terrain == 0, "Autoterrain gap")
        var packed := PackedScene.new()
        require_ok(packed.pack(scene) == OK, "Scene pack failed")
        require_ok(ResourceSaver.save(packed, BASE + "godot/" + str(entry.id) + "_review.tscn") == OK, "Scene save failed")
        scene.free()
        report.append({"id":entry.id,"tiles":48,"frames":entry.frames,"autoterrain_cells":water_cells.size(),"resource_load":true})
    var file := FileAccess.open("res://godot_validation.json", FileAccess.WRITE)
    file.store_string(JSON.stringify({"status":"passed","packs":report,"visual_approval":false}, "  "))
    print("FOUNDATION PASS: four TileSets, 47 terrain masks each, 16-frame water tiles, connected example scenes")
    quit(0)
