extends SceneTree

const GENERATOR_SCRIPT := preload("res://addons/beep_game_builder_cs/ecs/terrain/MountainPrefabGeneratorComponent.cs")
const MANIFEST_PATH := "res://addons/beep_game_builder_cs/generated/mountains/low_poly_sandstone/authored_prefabs/two_level_transition_prefab_manifest.json"

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var generator := GENERATOR_SCRIPT.new()
	generator.name = "LowPolyMountainPrefabGenerator"
	root.add_child(generator)
	await process_frame

	generator.set("PrefabManifestPath", MANIFEST_PATH)
	generator.set("SourceMode", 0)
	generator.set("LayoutPreset", 0)
	generator.set("CreateWalkableAreas", true)
	generator.set("CreateRouteConnectorAreas", true)
	generator.set("CreateAnchorNodes", true)

	var parts := int(generator.call("GeneratePrefab"))
	var summary := generator.call("GetLastGenerationSummary") as Dictionary
	_expect(parts == 1, "Integrated transition should instantiate as one coherent prefab sprite.")
	_expect(str(summary.get("visual_source_mode", "")) == "prefab_chunks", "Prefab should use chunk mode.")
	_expect(int(summary.get("prefab_chunks", 0)) == 1, "Summary should expose the single integrated prefab.")
	_expect(int(summary.get("levels", 0)) == 2, "Socket proof should expose two height levels.")
	_expect(int(summary.get("walkable_area_count", 0)) == 2, "Each floor should have a walkable polygon.")
	_expect(int(summary.get("route_regions", 0)) == 2, "Ground entrance and elevation change should have ramp polygons.")
	_expect(int(summary.get("route_connector_count", 0)) == 2, "Both ramp polygons should become Area2D nodes.")
	_expect(bool(summary.get("route_connected", false)), "Routes should connect level 0 to the summit.")
	_expect(int(summary.get("missing_route_edge_count", -1)) == 0, "All route edges should resolve.")
	_expect(int(summary.get("anchor_count", 0)) == 4, "Entrance, player, summit, and castle anchors should be generated.")

	var sprite_count := 0
	var integrated_prefab_count := 0
	var highest_sprite_level := -1
	for child in generator.get_children():
		if child is not Sprite2D:
			continue
		sprite_count += 1
		if str(child.get_meta("mountain_category", "")) == "height_aware_prefab":
			integrated_prefab_count += 1
		highest_sprite_level = maxi(highest_sprite_level, int(child.get_meta("mountain_height_level", -1)))

	_expect(sprite_count == parts, "Generated Sprite2D count should match GeneratePrefab.")
	_expect(integrated_prefab_count == 1, "The visual should be the integrated height-aware prefab.")
	_expect(highest_sprite_level == 1, "The upper floor should be height level 1.")

	var anchors := generator.call("GetAnchors") as Dictionary
	_expect(anchors.has("entrance"), "Ground entrance anchor should exist.")
	_expect(anchors.has("player_spawn"), "Player spawn anchor should exist.")
	_expect(anchors.has("castle_anchor"), "Castle anchor should exist on the summit.")
	var castle_position := generator.call("GetAnchorPosition", "castle_anchor") as Vector2
	_expect(castle_position != Vector2.ZERO, "Castle anchor should resolve to a non-zero position.")
	_expect(int(generator.call("GetHeightLevelAtLocalPosition", castle_position)) == 1, "Castle anchor should resolve to upper-floor height.")

	if _failed:
		quit(1)
		return

	root.remove_child(generator)
	generator.free()
	print("[low-poly-mountain-prefab] OK: one coherent sprite exposes two floors and one connected ramp.")
	quit(0)


func _expect(condition: bool, message: String) -> void:
	if condition:
		return
	_failed = true
	push_error("[low-poly-mountain-prefab] " + message)
