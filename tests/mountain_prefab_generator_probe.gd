extends SceneTree

const GENERATOR_SCRIPT := preload("res://addons/beep_game_builder_cs/ecs/terrain/MountainPrefabGeneratorComponent.cs")

const MANIFEST_PATH := "res://addons/beep_game_builder_cs/generated/mountains/shape_based_mountain_prefab/prefab_manifest.json"

var _failed := false

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var generator := GENERATOR_SCRIPT.new()
	generator.name = "MountainPrefabGenerator"
	root.add_child(generator)
	await process_frame

	generator.set("PrefabManifestPath", MANIFEST_PATH)
	generator.set("UseSingleBakedPrefabImage", false)
	generator.set("SourceMode", 0)
	generator.set("LayoutPreset", 0)
	generator.set("CreateWalkableAreas", true)
	generator.set("CreateRouteConnectorAreas", true)
	generator.set("CreateAnchorNodes", true)
	generator.set("PrefabScale", 1.0)

	var parts := int(generator.call("GeneratePrefab"))
	if parts <= 0:
		_fail("GeneratePrefab should instantiate layered Sprite2D parts.")

	var summary := generator.call("GetLastGenerationSummary") as Dictionary
	if int(summary.get("part_count", 0)) <= 0:
		_fail("Summary should report generated visual parts.")
	if str(summary.get("visual_source_mode", "")) != "prefab_chunks":
		_fail("Reference generator should use named prefab chunks when the manifest provides them.")
	if int(summary.get("prefab_chunks", 0)) < 7:
		_fail("Reference generator should expose the named level and route chunks.")
	if int(summary.get("walkable_area_count", 0)) <= 0:
		_fail("Summary should report walkable regions.")
	if int(summary.get("route_connector_count", 0)) <= 0:
		_fail("Summary should report generated route connector areas.")
	if int(summary.get("route_regions", 0)) <= 0:
		_fail("Summary should report explicit route regions for ramp tiles.")
	if not bool(summary.get("route_connected", false)):
		_fail("Summary should report a connected path through the mountain levels.")
	if int(summary.get("missing_route_edge_count", 0)) != 0:
		_fail("Summary should not report missing route edges.")
	if int(summary.get("anchor_count", 0)) <= 0:
		_fail("Summary should report anchor nodes.")
	if int(summary.get("levels", 0)) < 4:
		_fail("Reference mountain prefab should expose multiple gameplay levels.")
	if int(summary.get("route_edges", 0)) <= 0:
		_fail("Reference mountain prefab should expose the route up the mountain.")

	var levels := generator.call("GetMountainLevels") as Array
	if levels.size() < 4:
		_fail("GetMountainLevels should return the prefab level contract.")
	var castle_height := -1
	var highest_non_castle_height := -1
	for level in levels:
		if str(level.get("id", "")) == "castle_plateau":
			castle_height = int(level.get("height_level", level.get("height", -1)))
		else:
			highest_non_castle_height = maxi(highest_non_castle_height, int(level.get("height_level", level.get("height", -1))))
	if castle_height <= highest_non_castle_height:
		_fail("Castle plateau should be higher than every other walkable floor.")

	var regions := generator.call("GetWalkableRegions") as Array
	if regions.size() <= 0:
		_fail("GetWalkableRegions should return placement regions for gameplay.")

	var chunks := generator.call("GetPrefabChunkAssets") as Array
	if chunks.size() < 7:
		_fail("GetPrefabChunkAssets should return the named reference-style chunk contract.")
	var has_base_chunk := false
	var has_castle_chunk := false
	var has_middle_tile := false
	var has_surrounding_tile := false
	var route_chunk_count := 0
	for chunk in chunks:
		var role := str(chunk.get("role", ""))
		if role.begins_with("level_0_base_level_tile"):
			has_base_chunk = true
		if role.begins_with("level_3_castle_level_tile"):
			has_castle_chunk = true
			if int(chunk.get("height_level", -1)) <= highest_non_castle_height:
				_fail("Castle chunk should be tagged as the highest prefab chunk.")
			if not bool(chunk.get("visual_includes_wall", false)):
				_fail("Castle chunk should include its cliff/support visual.")
		if str(chunk.get("tile_role", "")) == "middle":
			has_middle_tile = true
		var missing_sides := chunk.get("missing_neighbor_sides", [])
		if missing_sides is Array and not missing_sides.is_empty():
			has_surrounding_tile = true
		if str(chunk.get("category", "")) == "route_chunk":
			route_chunk_count += 1
			if int(chunk.get("to_level", -1)) <= int(chunk.get("from_level", -1)):
				_fail("Route chunks should climb from a lower level to a higher level.")
			if not bool(chunk.get("visual_includes_wall", false)):
				_fail("Route chunks should preserve wall/cliff context.")
	if not has_base_chunk:
		_fail("Prefab chunk contract should include the level 0 base chunk.")
	if not has_castle_chunk:
		_fail("Prefab chunk contract should include the level 3 castle support chunk.")
	if not has_middle_tile:
		_fail("Shape-based prefab should include middle cells.")
	if not has_surrounding_tile:
		_fail("Shape-based prefab should include surrounding edge/corner/cap cells.")
	if route_chunk_count < 3:
		_fail("Prefab chunk contract should include all route-up chunks.")

	var route := generator.call("GetRouteEdges") as Array
	if route.size() <= 0:
		_fail("GetRouteEdges should return climbable path links.")

	var route_regions := generator.call("GetRouteRegions") as Array
	if route_regions.size() <= 0:
		_fail("GetRouteRegions should return climbable ramp tile regions.")
	for region in route_regions:
		if not bool(region.get("visual_includes_wall", false)):
			_fail("Route regions should preserve that ramp visuals include wall/cliff height.")
		if int(region.get("to_level", 0)) <= int(region.get("from_level", 0)):
			_fail("Route regions should describe upward height transitions.")
		if float(region.get("to_elevation_px", 0.0)) <= float(region.get("from_elevation_px", 0.0)):
			_fail("Route regions should describe upward elevation transitions.")

	if not bool(generator.call("IsRouteConnected")):
		_fail("IsRouteConnected should confirm the path from base to top.")

	var connectivity := generator.call("GetRouteConnectivitySummary") as Dictionary
	if not bool(connectivity.get("connected", false)):
		_fail("GetRouteConnectivitySummary should confirm connected route data.")

	var anchors := generator.call("GetAnchors") as Dictionary
	if not anchors.has("castle_anchor"):
		_fail("GetAnchors should expose castle_anchor.")
	if not anchors.has("player_spawn"):
		_fail("GetAnchors should expose player_spawn.")

	var castle_position := generator.call("GetAnchorPosition", "castle_anchor") as Vector2
	if castle_position == Vector2.ZERO:
		_fail("castle_anchor should resolve to a non-zero local position.")
	if int(generator.call("GetHeightLevelAtLocalPosition", Vector2(560, 160))) != 3:
		_fail("Castle floor point should resolve to height level 3.")
	if int(generator.call("GetHeightLevelAtLocalPosition", Vector2(310, 464))) != 0:
		_fail("Base floor point should resolve to height level 0.")
	var route_region_at_point := generator.call("GetRouteRegionAtLocalPosition", Vector2(420, 330)) as Dictionary
	if route_region_at_point.is_empty():
		_fail("Ramp path point should resolve to a route region.")
	if int(route_region_at_point.get("to_level", 0)) <= int(route_region_at_point.get("from_level", 0)):
		_fail("Queried ramp route should climb upward.")

	var sprite_count := 0
	var walkable_count := 0
	var anchor_count := 0
	var route_connector_count := 0
	var ramp_region_count := 0
	var route_sprite_count := 0
	var highest_sprite_level := -1
	var castle_sprite_level := -1
	for child in generator.get_children():
		if child is Sprite2D:
			sprite_count += 1
			if not child.has_meta("mountain_role"):
				_fail("Generated Sprite2D parts should keep their mountain_role metadata.")
			if not bool(child.get_meta("mountain_prefab_chunk", false)):
				_fail("Generated Sprite2D parts should come from the named prefab chunk manifest.")
			var sprite_level := int(child.get_meta("mountain_height_level", -1))
			highest_sprite_level = maxi(highest_sprite_level, sprite_level)
			var sprite_role := str(child.get_meta("mountain_role"))
			if sprite_role.begins_with("level_3_castle_level_tile"):
				castle_sprite_level = sprite_level
			if str(child.get_meta("mountain_category", "")) == "route_chunk":
				route_sprite_count += 1
				if int(child.get_meta("mountain_to_level", -1)) <= int(child.get_meta("mountain_from_level", -1)):
					_fail("Generated route chunk Sprite2D should keep upward from/to metadata.")
		if child is Area2D and child.has_meta("mountain_walkable"):
			walkable_count += 1
		if child is Marker2D and child.has_meta("mountain_anchor_id"):
			anchor_count += 1
			if str(child.get_meta("mountain_anchor_id")) == "castle_anchor":
				if int(child.get_meta("mountain_height_level")) <= highest_non_castle_height:
					_fail("castle_anchor Marker2D should keep the higher mountaintop height.")
		if child is Area2D and child.has_meta("mountain_route_from") and child.has_meta("mountain_route_to"):
			route_connector_count += 1
		if child is Area2D and child.has_meta("mountain_route_region_id"):
			ramp_region_count += 1
			if not bool(child.get_meta("mountain_visual_includes_wall")):
				_fail("Generated route region nodes should keep visual wall metadata.")
			if float(child.get_meta("mountain_to_elevation_px")) <= float(child.get_meta("mountain_from_elevation_px")):
				_fail("Generated route region nodes should keep upward elevation metadata.")

	if sprite_count != parts:
		_fail("Generated Sprite2D count should match GeneratePrefab return value.")
	if route_sprite_count < 3:
		_fail("Generated prefab should include route chunk Sprite2D parts.")
	if castle_sprite_level != highest_sprite_level:
		_fail("Castle floor sprite should be the highest visual chunk.")
	if walkable_count <= 0:
		_fail("Generated prefab should include walkable Area2D nodes.")
	if anchor_count <= 0:
		_fail("Generated prefab should include anchor Marker2D nodes.")
	if route_connector_count <= 0:
		_fail("Generated prefab should include route connector Area2D nodes.")
	if ramp_region_count <= 0:
		_fail("Generated prefab should include explicit ramp route region nodes.")

	var save_error := int(generator.call("SaveGeneratedSceneToPath", "res://tmp/mountain_semantic_green_large_levelled_castle/probe_saved_reference_mountain.tscn"))
	if save_error != OK:
		_fail("SaveGeneratedSceneToPath should save a reusable prefab scene.")

	if _failed:
		quit(1)
		return

	root.remove_child(generator)
	generator.free()
	print("[mountain-prefab-generator] OK: generated reference-style mountain prefab with levels, route, and anchors.")
	quit(0)

func _fail(message: String) -> void:
	_failed = true
	push_error("[mountain-prefab-generator] " + message)
