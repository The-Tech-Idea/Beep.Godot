extends SceneTree

const GENERATOR_SCRIPT := preload("res://addons/beep_game_builder_cs/ecs/terrain/MountainPrefabGeneratorComponent.cs")
const PACK_PATH := "res://addons/beep_game_builder_cs/generated/mountains/low_poly_sandstone/authored_prefabs/front_2_5d/front_2_5d_variant_pack_manifest.json"

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var pack := _read_json(PACK_PATH)
	_expect(str(pack.get("projection", "")) == "front_2_5d", "Variant pack must declare the front-facing projection.")
	var variants := pack.get("variants", []) as Array
	_expect(variants.size() == 2, "Front-facing pack should contain two-level and three-level prefabs.")

	for variant_value in variants:
		var variant := variant_value as Dictionary
		await _probe_variant(variant)

	if _failed:
		quit(1)
		return

	print("[low-poly-front-2.5d-pack] OK: both front-facing prefabs have coherent levels and connected ramps.")
	quit(0)


func _probe_variant(variant: Dictionary) -> void:
	var manifest_path := PACK_PATH.get_base_dir().path_join(str(variant.get("manifest", "")))
	var manifest := _read_json(manifest_path)
	var expected_levels := int(variant.get("levels", 0))
	_expect(str(manifest.get("projection", "")) == "front_2_5d", "Variant projection should remain front-facing.")
	_expect(int(manifest.get("level_count", 0)) == expected_levels, "Variant level_count should match the catalog.")
	_expect(str(manifest.get("entrance_direction", "")) == "front_center", "Every front-facing prefab needs a visible bottom-center entrance.")
	_assert_nested_support(manifest, expected_levels)

	var generator: Node = GENERATOR_SCRIPT.new()
	generator.name = str(variant.get("id", "Front25DVariant"))
	root.add_child(generator)
	await process_frame
	generator.set("PrefabManifestPath", manifest_path)
	generator.set("SourceMode", 0)
	generator.set("CreateWalkableAreas", true)
	generator.set("CreateRouteConnectorAreas", true)
	generator.set("CreateAnchorNodes", true)

	var parts := int(generator.call("GeneratePrefab"))
	var summary := generator.call("GetLastGenerationSummary") as Dictionary
	_expect(parts == 1, "Each directional mountain must instantiate as one unified sprite.")
	_expect(int(summary.get("levels", 0)) == expected_levels, "Generator should expose every authored height level.")
	_expect(int(summary.get("walkable_area_count", 0)) == expected_levels, "Every level needs one walkable polygon.")
	_expect(int(summary.get("route_regions", 0)) == expected_levels, "Entry and internal ramps should cover every ascent.")
	_expect(int(summary.get("route_connector_count", 0)) == expected_levels, "Every ramp should become an Area2D connector.")
	_expect(bool(summary.get("route_connected", false)), "Internal route graph should connect Level 0 to the summit.")
	_expect(int(summary.get("missing_route_edge_count", -1)) == 0, "All authored route edges should resolve.")

	var entrance := generator.call("GetAnchorPosition", "entrance") as Vector2
	var summit := generator.call("GetAnchorPosition", "summit") as Vector2
	_expect(entrance != Vector2.ZERO, "Entrance anchor should resolve.")
	_expect(summit != Vector2.ZERO, "Summit anchor should resolve.")
	_expect(int(generator.call("GetHeightLevelAtLocalPosition", summit)) == expected_levels - 1, "Summit must resolve to the highest floor.")

	root.remove_child(generator)
	generator.free()


func _assert_nested_support(manifest: Dictionary, expected_levels: int) -> void:
	var support := manifest.get("nested_support", {}) as Dictionary
	_expect(bool(support.get("strict_nested_footprints", false)), "Front-facing levels must use strict nested footprints.")
	var minimum_margin := int(support.get("minimum_side_margin_px", 0))
	_expect(minimum_margin >= 32, "Nested support must reserve at least 32 pixels per side.")
	var transitions := support.get("transitions", []) as Array
	_expect(transitions.size() == expected_levels - 1, "Every upper level needs a lower-level support transition.")
	for transition_value in transitions:
		var transition := transition_value as Dictionary
		_expect(int(transition.get("left_margin_px", -1)) >= minimum_margin, "Upper level hangs past its lower support on the left.")
		_expect(int(transition.get("right_margin_px", -1)) >= minimum_margin, "Upper level hangs past its lower support on the right.")


func _read_json(path: String) -> Dictionary:
	var file := FileAccess.open(path, FileAccess.READ)
	if file == null:
		_expect(false, "Could not open " + path)
		return {}
	var parsed = JSON.parse_string(file.get_as_text())
	if not parsed is Dictionary:
		_expect(false, "Invalid JSON object in " + path)
		return {}
	return parsed as Dictionary


func _expect(condition: bool, message: String) -> void:
	if condition:
		return
	_failed = true
	push_error("[low-poly-front-2.5d-pack] " + message)
