extends SceneTree

const ROOT = "res://addons/beep_game_builder_cs/ecs/terrain/"
var errors: Array[String] = []

func check(ok: bool, reason: String) -> void:
    if not ok:
        errors.append(reason)
        push_error(reason)

func make_pack(iso: bool) -> Resource:
    var pack = load(ROOT + "TerrainLibraryPack.cs").new()
    pack.PackId = "test.iso" if iso else "test.square"
    pack.Projection = 3 if iso else 1
    var tiles := TileSet.new()
    tiles.tile_shape = TileSet.TILE_SHAPE_ISOMETRIC if iso else TileSet.TILE_SHAPE_SQUARE
    tiles.tile_size = Vector2i(64, 32) if iso else Vector2i(64, 64)
    tiles.add_terrain_set()
    tiles.set_terrain_set_mode(0, TileSet.TERRAIN_MODE_MATCH_CORNERS_AND_SIDES)
    tiles.add_terrain(0)
    tiles.set_terrain_name(0, 0, "grass")
    var atlas := TileSetAtlasSource.new()
    var img := Image.create(512, 384, false, Image.FORMAT_RGBA8)
    img.fill(Color.GREEN)
    atlas.texture = ImageTexture.create_from_image(img)
    atlas.texture_region_size = Vector2i(64, 64)
    tiles.add_source(atlas, 0)
    atlas.create_tile(Vector2i(7, 5))
    var bg := atlas.get_tile_data(Vector2i(7, 5), 0)
    bg.terrain_set = 0
    bg.terrain = -1
    var bits: Array[int] = []
    for b in range(16):
        if bg.is_valid_terrain_peering_bit(b): bits.append(b)
    check(bits.size() == 8, "Expected eight valid bits")
    var count := 0
    for mask in range(256):
        var legal := true
        for b in range(8):
            if bits[b] % 4 != (2 if iso else 0) and mask & (1 << b):
                if not mask & (1 << ((b + 7) % 8)) or not mask & (1 << ((b + 1) % 8)):
                    legal = false
        if not legal: continue
        var coord := Vector2i(count % 8, count / 8)
        atlas.create_tile(coord)
        var data := atlas.get_tile_data(coord, 0)
        data.terrain_set = 0
        data.terrain = 0
        for b in range(8): data.set_terrain_peering_bit(bits[b], 0 if mask & (1 << b) else -1)
        count += 1
    check(count == 47, "Expected 47 legal masks")
    pack.Tiles = tiles
    pack.TerrainBindings.assign({"grass": 0, "dirt": -1})
    pack.BackgroundSource = 0
    pack.BackgroundAtlas = Vector2i(7, 5)
    return pack

func _initialize() -> void:
    call_deferred("run")

func run() -> void:
    var candidate = load("res://addons/beep_game_builder_cs/generated/dev/cartoon/water/staging/godot/grass_dirt_library_pack.tres")
    check(candidate != null, "Candidate engine binding failed to load")
    if candidate != null: check(candidate.Validate(1) == "", "Candidate pack invalid: " + candidate.Validate(1))
    var host := Node2D.new()
    root.add_child(host)
    var cells = load("res://addons/beep_game_builder_cs/ecs/grid/GridCellDataComponent.cs").new()
    cells.name = "Cells"
    host.add_child(cells)
    for y in range(8):
        for x in range(8): cells.SetTerrainKind(Vector2i(x + 3, y - 2), "dirt" if (x + y) % 3 == 0 else "grass")
    for iso in [false, true]:
        var pack = make_pack(iso)
        var view := 3 if iso else 1
        check(pack.Validate(view) == "", "Valid pack rejected: " + pack.Validate(view))
        check(pack.Validate(1 if iso else 3) != "", "Wrong projection accepted")
        var renderer = load(ROOT + ("TerrainIsometricAutotileRendererComponent.cs" if iso else "TerrainTileRendererComponent.cs")).new()
        renderer.RefreshOnReady = false
        renderer.LibraryPack = pack
        renderer.BoundsOrigin = Vector2i(3, -2)
        renderer.BoundsSize = Vector2i(8, 8)
        host.add_child(renderer)
        renderer.CellDataPath = renderer.get_path_to(cells)
        renderer.Rebuild()
        var layer: TileMapLayer = renderer.get_node("IsoTerrain" if iso else "LibraryTerrain")
        if iso:
            var published := layer.tile_map_data.duplicate()
            renderer.RequestRebuild()
            renderer.CancelRebuild()
            check(layer.tile_map_data == published, "Cancellation discarded published tiles")
            renderer.RequestRebuild()
            for frame in range(30):
                if not renderer.IsRebuilding: break
                await process_frame
            check(not renderer.IsRebuilding, "Time-sliced build did not finish")
        check(layer.get_used_cells().size() == 64, "Missing rendered cells")
        check(layer.position == Vector2.ZERO, "Unexpected dual-grid offset")
        for cell in layer.get_used_cells():
            var expected := -1 if cells.GetTerrainKind(cell) == "dirt" else 0
            check(layer.get_cell_tile_data(cell).terrain == expected, "Terrain mismatch at " + str(cell))
            check(layer.local_to_map(layer.map_to_local(cell)) == cell, "Picking roundtrip failed")
        for edit in [Vector2i(6, 1), Vector2i(3, -2), Vector2i(10, 5)]:
            cells.SetTerrainKind(edit, "grass" if cells.GetTerrainKind(edit) == "dirt" else "dirt")
            await process_frame
            await process_frame
            var incremental := layer.tile_map_data.duplicate()
            check(renderer.LibraryCellsUpdated <= 9, "Single edit rebuilt more than its neighborhood")
            renderer.Rebuild()
            check(incremental == layer.tile_map_data, "Incremental differs from full rebuild: " + str(edit))
        var before := layer.tile_map_data.duplicate()
        var invalid = make_pack(not iso)
        renderer.LibraryPack = invalid
        renderer.Rebuild()
        check(layer.tile_map_data == before, "Invalid pack replaced working display")
        renderer.LibraryPack = pack
        var previous: String = cells.GetTerrainKind(Vector2i(3, -2))
        cells.SetTerrainKind(Vector2i(3, -2), "lava")
        renderer.Rebuild()
        check(layer.tile_map_data == before, "Unmapped terrain replaced working display")
        cells.SetTerrainKind(Vector2i(3, -2), previous)
        pack.Tiles.get_source(0).remove_tile(Vector2i.ZERO)
        check(pack.Validate(view).contains("missing connection"), "Missing mask accepted")
        renderer.free()
    var legacy = load(ROOT + "TerrainTileRendererComponent.cs").new()
    legacy.RefreshOnReady = false
    legacy.BoundsSize = Vector2i(8, 8)
    legacy.BoundsOrigin = Vector2i(3, -2)
    legacy.BaseAtlasPath = "res://addons/beep_game_builder_cs/textures/tiles/water_15piece.png"
    legacy.GrassAtlasPath = "res://addons/beep_game_builder_cs/textures/tiles/grass_15piece.png"
    host.add_child(legacy)
    legacy.CellDataPath = legacy.get_path_to(cells)
    legacy.Rebuild()
    var base_layer: TileMapLayer = legacy.get_node("BaseTiles")
    check(base_layer.position == Vector2(-32, -32), "Legacy half-cell offset changed")
    check(base_layer.get_used_cells().size() == 81, "Legacy dual-grid border changed")
    var legacy_bytes := base_layer.tile_map_data.duplicate()
    legacy.LibraryPack = candidate
    legacy.Rebuild()
    check(legacy.has_node("LibraryTerrain"), "Could not select candidate over legacy renderer")
    legacy.LibraryPack = null
    legacy.Rebuild()
    check(legacy.get_node("BaseTiles").tile_map_data == legacy_bytes, "Removing pack did not restore legacy rendering")
    check(not legacy.get_node("LibraryTerrain").visible, "Pack layer remained visible in legacy mode")
    host.free()
    print("TERRAIN LIBRARY PACK: ", "PASS" if errors.is_empty() else "FAIL", " (square + isometric)")
    quit(0 if errors.is_empty() else 1)
