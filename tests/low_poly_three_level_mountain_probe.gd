extends SceneTree

const GENERATOR_SCRIPT := preload("res://addons/beep_game_builder_cs/ecs/terrain/MountainPrefabGeneratorComponent.cs")
const MANIFEST_PATH := "res://addons/beep_game_builder_cs/generated/mountains/low_poly_sandstone/authored_prefabs/three_level_mountain_prefab_manifest.json"

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var generator := GENERATOR_SCRIPT.new()
	generator.name = "LowPolyThreeLevelMountainGenerator"
	root.add_child(generator)
	await process_frame

	generator.set("PrefabManifestPath", MANIFEST_PATH)
	generator.set("SourceMode", 0)
	generator.set("CreateWalkableAreas", true)
	generator.set("CreateRouteConnectorAreas", true)
	generator.set("CreateAnchorNodes", true)

	var parts := int(generator.call("GeneratePrefab"))
	var summary := generator.call("GetLastGenerationSummary") as Dictionary
	_expect(parts == 1, "Approved mountain should instantiate as one coherent sprite.")
	_expect(str(summary.get("visual_source_mode", "")) == "prefab_chunks", "Mountain should use its prefab chunk manifest.")
	_expect(int(summary.get("levels", 0)) == 3, "Mountain should expose three height levels.")
	_expect(int(summary.get("walkable_area_count", 0)) == 3, "Each level should have one walkable polygon.")
	_expect(int(summary.get("route_regions", 0)) == 3, "Ground entrance and both internal ramps should expose climbable polygons.")
	_expect(int(summary.get("route_connector_count", 0)) == 3, "All three ramp polygons should become Area2D nodes.")
	_expect(bool(summary.get("route_connected", false)), "Route graph should connect the base to the summit.")
	_expect(int(summary.get("missing_route_edge_count", -1)) == 0, "No route edge should be missing.")
	_expect(int(summary.get("anchor_count", 0)) == 5, "Entrance, player, middle, summit, and castle anchors should exist.")

	var levels := generator.call("GetMountainLevels") as Array
	_expect(levels.size() == 3, "GetMountainLevels should return levels 0, 1, and 2.")
	var routes := generator.call("GetRouteEdges") as Array
	_expect(routes.size() == 2, "GetRouteEdges should return the complete two-ramp route.")
	var castle_position := generator.call("GetAnchorPosition", "castle_anchor") as Vector2
	_expect(castle_position != Vector2.ZERO, "Castle anchor should resolve on the summit.")
	_expect(int(generator.call("GetHeightLevelAtLocalPosition", castle_position)) == 2, "Castle anchor should resolve to height level 2.")

	if _failed:
		quit(1)
		return

	root.remove_child(generator)
	generator.free()
	print("[low-poly-three-level-mountain] OK: one sprite, three floors, two connected ramps, castle-ready summit.")
	quit(0)


func _expect(condition: bool, message: String) -> void:
	if condition:
		return
	_failed = true
	push_error("[low-poly-three-level-mountain] " + message)
