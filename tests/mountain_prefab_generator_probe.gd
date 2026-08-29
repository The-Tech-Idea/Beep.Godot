extends SceneTree

const GENERATOR_SCRIPT := preload("res://addons/beep_game_builder_cs/ecs/terrain/MountainPrefabGeneratorComponent.cs")

const MANIFEST_PATH := "res://tmp/mountain_semantic_green_large_levelled_castle/prefab_manifest.json"

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var generator := GENERATOR_SCRIPT.new()
	generator.name = "MountainPrefabGenerator"
	root.add_child(generator)
	await process_frame

	generator.set("PrefabManifestPath", MANIFEST_PATH)
	generator.set("UseSingleBakedPrefabImage", false)
	generator.set("CreateWalkableAreas", true)
	generator.set("CreateAnchorNodes", true)
	generator.set("PrefabScale", 1.0)

	var parts := int(generator.call("GeneratePrefab"))
	if parts <= 0:
		_fail("GeneratePrefab should instantiate layered Sprite2D parts.")

	var summary := generator.call("GetLastGenerationSummary") as Dictionary
	if int(summary.get("part_count", 0)) <= 0:
		_fail("Summary should report generated visual parts.")
	if int(summary.get("walkable_area_count", 0)) <= 0:
		_fail("Summary should report walkable regions.")
	if int(summary.get("anchor_count", 0)) <= 0:
		_fail("Summary should report anchor nodes.")
	if int(summary.get("levels", 0)) < 4:
		_fail("Reference mountain prefab should expose multiple gameplay levels.")
	if int(summary.get("route_edges", 0)) <= 0:
		_fail("Reference mountain prefab should expose the route up the mountain.")

	var levels := generator.call("GetMountainLevels") as Array
	if levels.size() < 4:
		_fail("GetMountainLevels should return the prefab level contract.")

	var regions := generator.call("GetWalkableRegions") as Array
	if regions.size() <= 0:
		_fail("GetWalkableRegions should return placement regions for gameplay.")

	var route := generator.call("GetRouteEdges") as Array
	if route.size() <= 0:
		_fail("GetRouteEdges should return climbable path links.")

	var anchors := generator.call("GetAnchors") as Dictionary
	if not anchors.has("castle_anchor"):
		_fail("GetAnchors should expose castle_anchor.")
	if not anchors.has("player_spawn"):
		_fail("GetAnchors should expose player_spawn.")

	var castle_position := generator.call("GetAnchorPosition", "castle_anchor") as Vector2
	if castle_position == Vector2.ZERO:
		_fail("castle_anchor should resolve to a non-zero local position.")

	var sprite_count := 0
	var walkable_count := 0
	var anchor_count := 0
	for child in generator.get_children():
		if child is Sprite2D:
			sprite_count += 1
			if not child.has_meta("mountain_role"):
				_fail("Generated Sprite2D parts should keep their mountain_role metadata.")
		if child is Area2D and child.has_meta("mountain_walkable"):
			walkable_count += 1
		if child is Marker2D and child.has_meta("mountain_anchor_id"):
			anchor_count += 1

	if sprite_count != parts:
		_fail("Generated Sprite2D count should match GeneratePrefab return value.")
	if walkable_count <= 0:
		_fail("Generated prefab should include walkable Area2D nodes.")
	if anchor_count <= 0:
		_fail("Generated prefab should include anchor Marker2D nodes.")

	root.remove_child(generator)
	generator.free()
	print("[mountain-prefab-generator] OK: generated reference-style mountain prefab with levels, route, and anchors.")
	quit(0)

func _fail(message: String) -> void:
	push_error("[mountain-prefab-generator] " + message)
	quit(1)
