extends "res://tests/terrain_library_pack_probe.gd"

const PROFILE_KEY = "terrain_library_elevation"
const PROFILE_IDS = ["base", "quarter", "half", "standard"]
const RISES = [0, 16, 32, 64]
const VALUES = [0.0, 0.2, 0.45, 0.8]

func elevated_pack(iso: bool) -> Resource:
    var pack = make_pack(iso)
    pack.ElevationCustomDataLayer = "elevation_profile"
    pack.Tiles.add_custom_data_layer()
    pack.Tiles.set_custom_data_layer_name(0, pack.ElevationCustomDataLayer)
    pack.Tiles.set_custom_data_layer_type(0, TYPE_STRING)
    var atlas: TileSetAtlasSource = pack.Tiles.get_source(0)
    for i in range(PROFILE_IDS.size()):
        pack.ElevationRisePixels[PROFILE_IDS[i]] = RISES[i]
        pack.ElevationValues[PROFILE_IDS[i]] = VALUES[i]
        for t in range(atlas.get_tiles_count()):
            var coord := atlas.get_tile_id(t)
            var base := atlas.get_tile_data(coord, 0)
            atlas.create_alternative_tile(coord, i + 1)
            var data := atlas.get_tile_data(coord, i + 1)
            data.terrain_set = base.terrain_set
            data.terrain = base.terrain
            data.probability = 0.0
            data.texture_origin = Vector2i(0, RISES[i])
            data.set_custom_data(pack.ElevationCustomDataLayer, PROFILE_IDS[i])
            for b in range(16):
                if base.is_valid_terrain_peering_bit(b): data.set_terrain_peering_bit(b, base.get_terrain_peering_bit(b))
    return pack

func run() -> void:
    for iso in [false, true]:
        var pack = elevated_pack(iso)
        check(pack.Validate(pack.Projection) == "", "Elevation pack invalid: " + pack.Validate(pack.Projection))
        pack.ElevationValues["quarter"] = 16.0
        check(pack.Validate(pack.Projection) != "", "Pixel height accepted as logical elevation")
        pack.ElevationValues["quarter"] = VALUES[1]
        var atlas: TileSetAtlasSource = pack.Tiles.get_source(0)
        atlas.get_tile_data(Vector2i.ZERO, 1).probability = 1.0
        check(pack.Validate(pack.Projection) != "", "Random elevation alternative accepted")
        atlas.get_tile_data(Vector2i.ZERO, 1).probability = 0.0
        atlas.get_tile_data(Vector2i.ZERO, 0).probability = 0.0
        check(pack.Validate(pack.Projection) != "", "All-zero probability connection accepted")
        atlas.get_tile_data(Vector2i.ZERO, 0).probability = 1.0
        var incomplete = elevated_pack(iso)
        incomplete.Tiles.get_source(0).remove_alternative_tile(Vector2i.ZERO, 2)
        check(incomplete.Validate(incomplete.Projection) != "", "Missing elevation connection accepted")
        var host := Node2D.new()
        root.add_child(host)
        current_scene = host
        var cells = load("res://addons/beep_game_builder_cs/ecs/grid/GridCellDataComponent.cs").new()
        cells.name = "Cells"
        host.add_child(cells)
        cells.owner = host
        cells.FillTerrain(Rect2i(0, 0, 4, 4), "dirt")
        var point := Vector2i(1, 1)
        cells.SetMetadata(point, "terrain_elevation", 0.37)
        cells.SetMetadata(point, "terrain_relief", 1)
        cells.SetFlags(point, 5)
        var renderer = load(ROOT + ("TerrainIsometricAutotileRendererComponent.cs" if iso else "TerrainTileRendererComponent.cs")).new()
        renderer.name = "Renderer"
        renderer.RefreshOnReady = false
        renderer.LibraryPack = pack
        renderer.BoundsSize = Vector2i(4, 4)
        host.add_child(renderer)
        renderer.owner = host
        renderer.CellDataPath = renderer.get_path_to(cells)
        renderer.Rebuild()
        var display: TileMapLayer = renderer.get_node("IsoTerrain" if iso else "LibraryTerrain")
        var position := display.map_to_local(point)
        var session = load(ROOT + "TerrainLibraryEditSession.cs").new()
        session.name = "TerrainEditSession"
        renderer.add_child(session)
        session.owner = host
        check(session.Begin() == "", "Elevation Begin failed: " + session.Problem)
        var working: TileMapLayer = session.get_node("WorkingTerrain")
        check(not working.collision_enabled and not working.navigation_enabled, "Working copy enabled gameplay surfaces")
        var initial := working.tile_map_data.duplicate()
        pack.ElevationValues["quarter"] = 0.3
        check(session.PrepareApply().is_empty(), "Changed elevation mapping accepted during session")
        pack.ElevationValues["quarter"] = VALUES[1]
        check(session.PrepareElevationPaint(Rect2i(9, 9, 1, 1), "quarter").is_empty(), "Outside elevation region accepted")
        check(session.PrepareElevationPaint(Rect2i(point, Vector2i.ONE), "unknown").is_empty(), "Unknown elevation accepted")
        var paint: Dictionary = session.PrepareElevationPaint(Rect2i(point, Vector2i.ONE), "quarter")
        check(not paint.is_empty(), "Could not stage elevation: " + session.Problem)
        if paint.is_empty():
            host.free()
            continue
        check(working.tile_map_data == initial, "Preparing elevation mutated working layer")
        working.tile_map_data = paint.after
        check(is_equal_approx(cells.GetMetadata(point, "terrain_elevation"), 0.37), "Staging changed live elevation")
        var transaction: Dictionary = session.PrepareApply()
        check(not transaction.is_empty(), "Elevation Apply validation failed: " + session.Problem)
        if transaction.is_empty():
            host.free()
            continue
        check(transaction.after.size() == 1, "Elevation-only edit not isolated")
        session.Commit(transaction, true)
        check(cells.GetMetadata(point, PROFILE_KEY) == "quarter", "Profile not committed")
        check(is_equal_approx(cells.GetMetadata(point, "terrain_elevation"), 0.2), "Explicit normalized mapping not used")
        check(cells.GetMetadata(point, "terrain_relief") == 1 and cells.GetFlags(point) == 5, "Elevation altered relief or gameplay flags")
        check(display.get_cell_tile_data(point).get_custom_data("elevation_profile") == "quarter", "Renderer did not consume elevation profile")
        check(display.get_cell_tile_data(point).texture_origin.y == 16, "Authored pivot was changed")
        check(display.map_to_local(point) == position, "Elevation changed logical grid placement")
        session.Commit(transaction, false)
        check(is_equal_approx(cells.GetMetadata(point, "terrain_elevation"), 0.37), "Undo lost original elevation")
        session.Commit(transaction, true)
        check(session.Begin() == "", "Combined Begin failed")
        working.set_cells_terrain_connect([point], 0, 0, false)
        var combined: Dictionary = session.PrepareApply()
        check(not combined.is_empty(), "Ground edit on raised cell failed: " + session.Problem)
        if not combined.is_empty(): session.Commit(combined, true)
        check(cells.GetTerrainKind(point) == "grass" and cells.GetMetadata(point, PROFILE_KEY) == "quarter", "Ground painting reset height")
        check(display.get_cell_tile_data(point).get_custom_data("elevation_profile") == "quarter", "Ground repaint lost elevation artwork")
        cells.SetMetadata(point, PROFILE_KEY, "half")
        cells.SetMetadata(point, "terrain_elevation", VALUES[2])
        await process_frame
        await process_frame
        check(display.get_cell_tile_data(point).get_custom_data("elevation_profile") == "half", "Incremental elevation did not update")
        var incremental := display.tile_map_data.duplicate()
        renderer.Rebuild()
        check(incremental == display.tile_map_data, "Incremental elevation differs from rebuild")
        var other = load(ROOT + ("TerrainTileRendererComponent.cs" if iso else "TerrainIsometricAutotileRendererComponent.cs")).new()
        other.RefreshOnReady = false
        other.LibraryPack = elevated_pack(not iso)
        other.BoundsSize = Vector2i(4, 4)
        host.add_child(other)
        other.CellDataPath = other.get_path_to(cells)
        other.Rebuild()
        var other_display: TileMapLayer = other.get_node("LibraryTerrain" if iso else "IsoTerrain")
        check(other_display.get_cell_tile_data(point).get_custom_data("elevation_profile") == "half", "Other projection lost logical profile")
        cells.SetMetadata(point, PROFILE_KEY, "missing_profile")
        renderer.Rebuild()
        check(display.tile_map_data == incremental, "Missing profile destroyed working display")
        cells.SetMetadata(point, PROFILE_KEY, "half")
        check(session.Begin() == "", "Persistence Begin failed")
        var staged: Dictionary = session.PrepareElevationPaint(Rect2i(point, Vector2i.ONE), "standard")
        working.tile_map_data = staged.after
        var live: Array = cells.GetCells()
        var scene := PackedScene.new()
        check(scene.pack(host) == OK, "Could not pack elevated session")
        var path := "res://addons/beep_game_builder_cs/generated/test/library/output/elevation_%s.tscn" % ("iso" if iso else "square")
        check(ResourceSaver.save(scene, path) == OK, "Could not save elevated session")
        var reopened: Node = ResourceLoader.load(path, "PackedScene", ResourceLoader.CACHE_MODE_IGNORE).instantiate()
        host.free()
        root.add_child(reopened)
        current_scene = reopened
        var restored_cells = reopened.get_node("Cells")
        restored_cells.LoadCells(live, true)
        var restored = reopened.get_node("Renderer/TerrainEditSession")
        restored_cells.SetMetadata(point, "terrain_elevation", 0.46)
        check(restored.PrepareApply().is_empty(), "A real elevation conflict was ignored after reload")
        restored_cells.SetMetadata(point, "terrain_elevation", VALUES[2])
        var restored_patch: Dictionary = restored.PrepareApply()
        check(not restored_patch.is_empty(), "Reloaded elevation cannot apply: " + restored.Problem)
        if not restored_patch.is_empty(): restored.Commit(restored_patch, true)
        check(restored_cells.GetMetadata(point, PROFILE_KEY) == "standard", "Elevation paint lost on save/reload")
        check(is_equal_approx(restored_cells.GetMetadata(point, "terrain_elevation"), 0.8), "Reloaded elevation mapping wrong")
        reopened.free()
        current_scene = null
    print("TERRAIN LIBRARY ELEVATION: ", "PASS" if errors.is_empty() else "FAIL")
    quit(0 if errors.is_empty() else 1)
