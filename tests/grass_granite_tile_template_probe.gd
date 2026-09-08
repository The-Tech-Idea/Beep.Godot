extends SceneTree

var BASE := "res://addons/beep_game_builder_cs/generated/terrain_templates/grass_granite_tile_template_v1/"

func _initialize() -> void:
    for argument in OS.get_cmdline_user_args():
        if argument.begins_with("--asset-dir="):
            BASE = argument.trim_prefix("--asset-dir=").trim_suffix("/") + "/"
    call_deferred("run")

func run() -> void:
    root.size = Vector2i(1800, 720)
    RenderingServer.set_default_clear_color(Color("17242b"))
    var scene: Node2D = load(BASE + "terrain_example.tscn").instantiate()
    root.add_child(scene)
    await process_frame
    if not await check_neighborhoods_and_edits(scene):
        quit(1)
        return
    var checked := 0
    for group in scene.get_children():
        var tops: TileMapLayer = group.get_node("PaintableTops")
        var occupied := {}
        for cell in tops.get_used_cells():
            occupied[cell] = true
        for cell in occupied:
            var actual: int = tops.get_cell_tile_data(cell).get_custom_data("connection_mask")
            var expected: int = scene.expected_mask(cell, occupied)
            if actual != expected:
                push_error("Terrain mismatch: %s %s expected %d got %d" % [group.name, cell, expected, actual])
                quit(1)
                return
            checked += 1
    var packed := PackedScene.new()
    if packed.pack(scene) != OK or ResourceSaver.save(packed, BASE + "paintable_examples.tscn") != OK:
        push_error("Could not save baked editable example")
        quit(1)
        return
    if not "--render" in OS.get_cmdline_user_args():
        print("[grass-granite-probe] PASS: %d terrain cells on three layouts; editable TileMapLayer examples saved" % checked)
        quit(0)
        return
    for frame in range(8):
        await process_frame
    await RenderingServer.frame_post_draw
    var screenshot := root.get_texture().get_image()
    if screenshot == null or screenshot.is_empty():
        push_error("Empty render")
        quit(1)
        return
    if screenshot.save_png(BASE + "tilemap_examples_preview.png") != OK:
        quit(1)
        return
    print("[grass-granite-probe] PASS: %d terrain cells; Godot screenshot saved" % checked)
    quit(0)

func check_neighborhoods_and_edits(scene: Node2D) -> bool:
    var layer := TileMapLayer.new()
    layer.tile_set = load(BASE + "grass_granite_tileset.tres")
    layer.visible = false
    root.add_child(layer)
    var seen := {}
    for raw_mask in range(256):
        layer.clear()
        var cells: Array[Vector2i] = [Vector2i.ZERO]
        var occupied := {Vector2i.ZERO: true}
        for bit in range(8):
            if raw_mask & (1 << bit):
                cells.append(scene.DIRS[bit])
                occupied[scene.DIRS[bit]] = true
        layer.set_cells_terrain_connect(cells, 0, 0, false)
        var expected: int = scene.expected_mask(Vector2i.ZERO, occupied)
        var data := layer.get_cell_tile_data(Vector2i.ZERO)
        if data == null or data.get_custom_data("connection_mask") != expected:
            push_error("Neighborhood %d did not choose expected tile %d" % [raw_mask, expected])
            return false
        seen[expected] = true
    if seen.size() != 47:
        push_error("Not all 47 configurations were covered")
        return false
    layer.queue_free()
    var tops: TileMapLayer = scene.get_node("Rectangle/PaintableTops")
    var walls: TileMapLayer = scene.get_node("Rectangle/CliffWalls")
    var new_cell := Vector2i(10, 2)
    tops.set_cells_terrain_connect([new_cell], 0, 0, false)
    await create_timer(0.3).timeout
    for frame in range(4):
        await process_frame
    if walls.get_cell_atlas_coords(new_cell + Vector2i.DOWN) != Vector2i(posmod(new_cell.x,6)*4+3, 1):
        push_error("Painting did not create an isolated cliff cap")
        return false
    tops.erase_cell(new_cell)
    await create_timer(0.3).timeout
    for frame in range(4):
        await process_frame
    if walls.get_cell_source_id(new_cell + Vector2i.DOWN) != -1:
        push_error("Erasing terrain left an orphan cliff")
        return false
    scene.cliff_rows = 2
    for frame in range(4):
        await process_frame
    if walls.get_cell_atlas_coords(Vector2i(1, 5)).y != 0 or walls.get_cell_atlas_coords(Vector2i(1, 6)).y != 1:
        push_error("Cliff height did not update")
        return false
    scene.cliff_rows = 1
    for frame in range(4):
        await process_frame
    print("[grass-granite-probe] PASS: all 256 neighborhoods, 47 variants, paint/erase updates, cliff height updates")
    return true
