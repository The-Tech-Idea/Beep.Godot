extends "res://tests/terrain_library_pack_probe.gd"

func run() -> void:
    for iso in [false, true]:
        var host := Node2D.new()
        root.add_child(host)
        current_scene = host
        var pack = make_pack(iso)
        var mapping := TileMapLayer.new()
        mapping.name = "Mapping"
        mapping.tile_set = pack.Tiles
        mapping.position = Vector2(17, 29)
        mapping.scale = Vector2(1.2, 0.8)
        host.add_child(mapping)
        mapping.owner = host
        var structure := Node2D.new()
        structure.name = "AuthoredBridge"
        structure.scale = Vector2(0.75, 1.25)
        var authored := PackedScene.new()
        check(authored.pack(structure) == OK, "Pack structure fixture")
        structure.free()
        var layout = load(ROOT + "TerrainStructureLayout.cs").new()
        layout.Footprint = Vector2i(2, 3)
        layout.Pivot = Vector2(12, 24)
        layout.RisePixels = 32
        layout.SortOffset = 2
        pack.Structures.assign({"bridge": authored})
        pack.StructureLayouts.assign({"bridge": layout})
        var placements = load(ROOT + "TerrainStructureLayerComponent.cs").new()
        placements.name = "Structures"
        placements.LibraryPack = pack
        placements.MappingLayerPath = NodePath("../Mapping")
        placements.CellBounds = Rect2i(-4, -4, 12, 12)
        placements.position = Vector2(-11, 7)
        placements.rotation = 0.2
        host.add_child(placements)
        placements.owner = host
        var anchor := Vector2i(2, 3)
        var placed = placements.Place("bridge_1", "bridge", anchor)
        check(placed != null, "Place authored structure: " + placements.Problem)
        if placed != null:
            check(placed.to_global(layout.Pivot).is_equal_approx(mapping.to_global(mapping.map_to_local(anchor))), "Native anchor/pivot transform")
            check(placed.get_meta("terrain_structure_rise_pixels") == 32, "Rise is metadata, not extra translation")
            check(placed.owner == host and placed.z_index == 2, "Ownership and sorting")
        check(placements.Place("bridge_1", "bridge", anchor) == null, "Duplicate ID rejected")
        check(placements.Place("outside", "bridge", Vector2i(7, 7)) == null, "Whole footprint checked")
        check(placements.Place("unknown", "cave", anchor) == null, "Missing artwork rejected")
        check(placements.get_child_count() == 1, "Invalid placements preserve existing scene")
        check(placements.BeginStructureEdit(), "Begin staged additions")
        var staged = placements.StageStructure("bridge_2", "bridge", Vector2i(0, 0))
        check(staged != null and not placements.has_node("bridge_2"), "Stage does not publish live")
        var pending := PackedScene.new()
        check(pending.pack(host) == OK, "Pack pending scene")
        var pending_copy := pending.instantiate()
        check(pending_copy.get_node("Structures").StructureEditActive, "Pending state persisted")
        check(pending_copy.has_node("Structures/StructureWorkingCopy/bridge_2"), "Pending instance persisted")
        root.add_child(pending_copy)
        check(pending_copy.get_node("Structures").PrepareStructureApply().size() == 1, "Reopened staging validates")
        pending_copy.free()
        var transaction = placements.PrepareStructureApply()
        check(transaction.size() == 1, "Prepare staged batch")
        layout.RisePixels = 64
        check(placements.PrepareStructureApply().is_empty(), "Changed layout rejects Apply")
        layout.RisePixels = 32
        var undo := UndoRedo.new()
        undo.create_action("Structure additions")
        undo.add_do_method(placements.CommitStructures.bind(transaction, true))
        undo.add_undo_method(placements.CommitStructures.bind(transaction, false))
        undo.commit_action()
        check(placements.get_node_or_null("bridge_2") == staged, "Apply same instance")
        undo.undo()
        check(placements.StructureEditActive and staged.get_parent().name == "StructureWorkingCopy", "Undo restores pending instance")
        undo.redo()
        check(placements.get_node_or_null("bridge_2") == staged, "Redo publishes instance")
        undo.clear_history()
        undo.free()
        check(placements.BeginStructureEdit(), "Second staged session")
        placements.StageStructure("bridge_3", "bridge", Vector2i(0, 0))
        var conflict = placements.PrepareStructureApply()
        var foreign := Node2D.new()
        foreign.name = "bridge_3"
        placements.add_child(foreign)
        check(not placements.CommitStructures(conflict, true), "Reject conflicting batch before moving")
        check(placements.has_node("StructureWorkingCopy/bridge_3"), "Conflict preserves pending scene")
        foreign.free()
        placements.DiscardStructures()
        check(placements.has_node("bridge_1") and placements.has_node("bridge_2"), "Discard preserves live instances")
        check(placements.BeginStructureEdit(), "Begin existing-instance move")
        var original_transform: Transform2D = placed.transform
        placed.set_meta("inventory", "keep-latest-value")
        check(placements.StageStructureMove("bridge_1", Vector2i(1, 1)), "Stage existing structure move")
        check(placed.transform == original_transform, "Staging must not move live instance")
        var preview = placements.GetStructureMovePreview()
        check(preview.size() == 3 and preview.segments.size() == 48, "Preview contains six native footprint cells")
        check(placements.to_global(preview.to).is_equal_approx(mapping.to_global(mapping.map_to_local(Vector2i(1, 1)))), "Preview destination uses native transform")
        check(placements.to_global(preview.from).is_equal_approx(mapping.to_global(mapping.map_to_local(anchor))), "Preview source uses native transform")
        var corner_sum := Vector2.ZERO
        for index in range(0, 8, 2): corner_sum += preview.segments[index]
        check((corner_sum / 4).is_equal_approx(preview.to), "Preview corners surround native cell center")
        var expected_corner := Vector2(0, -16) if iso else Vector2(-32, -32)
        check(placements.to_global(preview.segments[0]).is_equal_approx(mapping.to_global(mapping.map_to_local(Vector2i(1, 1)) + expected_corner)), "Preview uses projection-specific cell polygon")
        var move_saved := PackedScene.new()
        check(move_saved.pack(host) == OK, "Pack pending move")
        var move_path := "res://addons/beep_game_builder_cs/generated/test/library/output/move_%s.tscn" % ("iso" if iso else "square")
        check(ResourceSaver.save(move_saved, move_path) == OK, "Save pending move to disk")
        var move_disk: PackedScene = ResourceLoader.load(move_path, "PackedScene", ResourceLoader.CACHE_MODE_IGNORE)
        var move_reopened := move_disk.instantiate()
        root.add_child(move_reopened)
        var move_layer = move_reopened.get_node("Structures")
        check(not move_layer.PrepareStructureMoveApply().is_empty(), "Disk move must remain applicable: " + move_layer.Problem)
        move_reopened.free()
        var move = placements.PrepareStructureMoveApply()
        check(not move.is_empty(), "Prepare move")
        check(placements.CommitStructureMove(move, true), "Apply move")
        check(placements.GetStructureMovePreview().is_empty(), "Apply clears preview geometry")
        check(placed.to_global(layout.Pivot).is_equal_approx(mapping.to_global(mapping.map_to_local(Vector2i(1, 1)))), "Moved native pivot")
        check(placed.get_meta("inventory") == "keep-latest-value", "Move preserves gameplay metadata")
        check(placements.CommitStructureMove(move, false), "Undo move")
        check(not placements.GetStructureMovePreview().is_empty(), "Undo restores preview geometry")
        check(placed.transform.is_equal_approx(original_transform), "Undo restores original transform")
        placements.DiscardStructures()
        check(placements.BeginStructureEdit(), "Begin conflict move")
        check(placements.StageStructureMove("bridge_1", Vector2i(0, 0)), "Stage conflict move")
        placed.position += Vector2(1, 0)
        check(placements.PrepareStructureMoveApply().is_empty(), "External live move must conflict")
        placements.DiscardStructures()
        check(placed.position.is_equal_approx(original_transform.origin + Vector2(1, 0)), "Discard preserves newer live placement")
        placed.transform = original_transform
        var world = load(ROOT + "TerrainWorldComponent.cs").new()
        world.name = "World"
        world.BuildOnReady = false
        world.ParticipatesInSave = false
        world.Projection = pack.Projection
        world.StructureLayerPaths.assign([NodePath("../Structures")])
        host.add_child(world)
        check(placements.visible, "Matching projection shows registered structures")
        check(placements.BeginStructureEdit(), "Begin registered edit")
        placements.StageStructure("guarded", "bridge", Vector2i(0, 0))
        check(world.HasPendingTerrainEdits(), "Structure session participates in world guard")
        world.Projection = 0
        check(world.Projection == pack.Projection and placements.visible, "Pending edit blocks projection switch")
        check(not world.BeginNewWorld(), "Pending edit blocks generation")
        placements.StructureEditActive = false
        check(world.HasPendingTerrainEdits(), "Pending children guard even when active flag is cleared")
        placements.StructureEditActive = true
        var other_world = load(ROOT + "TerrainWorldComponent.cs").new()
        other_world.BuildOnReady = false
        other_world.ParticipatesInSave = false
        host.add_child(other_world)
        check(not other_world.HasPendingTerrainEdits(), "Unregistered world is independent")
        other_world.free()
        placements.DiscardStructures()
        for projection in [0, 1, 2, 3]:
            world.Projection = projection
            check(placements.visible == (projection == pack.Projection), "Projection-specific structure visibility")
        world.Projection = 0
        check(not placements.BeginStructureEdit(), "Hidden projection cannot start editing")
        world.Projection = pack.Projection
        check(not world.HasPendingTerrainEdits(), "Discard releases world guard")
        world.free()
        var saved := PackedScene.new()
        check(saved.pack(host) == OK, "Structure scene persistence")
        var disk_path := "res://addons/beep_game_builder_cs/generated/test/library/output/structure_%s.tscn" % ("iso" if iso else "square")
        check(ResourceSaver.save(saved, disk_path) == OK, "Write disposable structure fixture")
        var restored := saved.instantiate()
        check(restored.has_node("Structures/bridge_1"), "Structure retained after scene roundtrip")
        restored.free()
        host.free()
    if errors.is_empty(): print("TERRAIN LIBRARY STRUCTURES: PASS")
    quit(0 if errors.is_empty() else 1)
