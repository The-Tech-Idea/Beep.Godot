extends SceneTree

const COMPONENT := preload("res://addons/beep_game_builder_cs/ecs/terrain/ModularMountainPrefabComponent.cs")
const MANIFEST_PATH := "res://addons/beep_game_builder_cs/generated/mountains/low_poly_sandstone/authored_prefabs/modular_front_2_5d/modular_mountain_pack_manifest.json"
const THEME_SPECS := [
	{"enum": 1, "id": "grass_granite", "ramps": 3, "plates": 0},
	{"enum": 2, "id": "grey_rock", "ramps": 3, "plates": 0},
	{"enum": 3, "id": "volcanic_basalt", "ramps": 3, "plates": 0},
	{"enum": 5, "id": "meadow_hill", "ramps": 3, "plates": 3},
	{"enum": 6, "id": "red_rock_mesa", "ramps": 3, "plates": 3},
	{"enum": 7, "id": "alpine_snow", "ramps": 3, "plates": 3},
]
const THEME_ROOT := "res://addons/beep_game_builder_cs/generated/mountains/low_poly_sandstone/authored_prefabs/modular_themes/"

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var manifest := _read_json(MANIFEST_PATH)
	_expect((manifest.get("base_prefabs", []) as Array).size() == 2, "Pack needs one-level and three-level ramp-free mountain bases.")
	_expect((manifest.get("ramp_modules", []) as Array).size() == 3, "Pack needs left, front, and right ramps.")
	var sheets := manifest.get("atlas_sheets", []) as Array
	_expect(sheets.size() == 1, "Pack needs one atlas sheet for the one-level mountain.")
	if not sheets.is_empty():
		var regions := (sheets[0] as Dictionary).get("regions", {}) as Dictionary
		_expect(regions.size() == 4, "One-level sheet needs one plateau and three ramp regions.")

	var component: Node = COMPONENT.new()
	root.add_child(component)
	await process_frame
	component.set("GenerateOnReady", false)
	component.set("MaterialTheme", 4)
	component.set("PackManifestPath", MANIFEST_PATH)
	component.set("EntranceRamp", 2)
	component.set("Level0To1Ramp", 1)
	component.set("Level1To2Ramp", 2)
	var ramp_count := int(component.call("GenerateMountain"))
	var summary := component.call("GetLastGenerationSummary") as Dictionary
	_expect(ramp_count == 3, "Default modular mountain should create three independently selected ramps.")
	_expect(int(summary.get("socket_count", 0)) == 7, "All authored mountain sockets should be available.")
	_expect(component.call("GetSocketPosition", "entry_front") as Vector2 != Vector2.ZERO, "Front entry socket should resolve.")
	_expect(component.call("GetSocketPosition", "level_1_to_2_right") as Vector2 != Vector2.ZERO, "Top-right transition socket should resolve.")

	component.set("EntranceRamp", 0)
	component.set("Level0To1Ramp", 2)
	component.set("Level1To2Ramp", 1)
	_expect(int(component.call("GenerateMountain")) == 2, "None must remove the entrance without affecting internal ramp choices.")

	for spec in THEME_SPECS:
		var manifest_path := THEME_ROOT + str(spec.id) + "/modular_mountain_pack_manifest.json"
		var theme_manifest := _read_json(manifest_path)
		_expect((theme_manifest.get("base_prefabs", []) as Array).size() == 2, "Every material theme needs one-level and three-level bases.")
		_expect((theme_manifest.get("ramp_modules", []) as Array).size() == int(spec.ramps), "Theme %s has the wrong ramp module count." % spec.id)
		_expect((theme_manifest.get("plate_modules", []) as Array).size() == int(spec.plates), "Theme %s has the wrong plate module count." % spec.id)
		if int(spec.plates) == 3:
			_assert_plate_support_and_ramp_fit(theme_manifest, str(spec.id))
		component.set("MaterialTheme", int(spec.enum))
		component.set("BasePrefabId", "three_level_wide_no_ramps")
		component.set("EntranceRamp", 2)
		component.set("Level0To1Ramp", 1)
		component.set("Level1To2Ramp", 2)
		_expect(int(component.call("GenerateMountain")) == 3, "Built-in material theme %s should generate all selected ramps." % spec.id)
		var theme_summary := component.call("GetLastGenerationSummary") as Dictionary
		_expect(str(theme_summary.get("manifest_path", "")) == manifest_path, "Theme selector should resolve the expected manifest.")
		if int(spec.plates) == 3:
			_expect(_count_generated_role(component, "level_plate") == 3, "Three-level theme %s should instantiate three separate plates." % spec.id)
		component.set("BasePrefabId", "one_level_wide_no_ramps")
		_expect(int(component.call("GenerateMountain")) == 1, "One-level bases should ignore unavailable upper-level ramp choices.")
		var one_level_summary := component.call("GetLastGenerationSummary") as Dictionary
		_expect(int(one_level_summary.get("level_count", 0)) == 1, "One-level generation should report one walkable level.")
		if int(spec.plates) == 3:
			_expect(_count_generated_role(component, "level_plate") == 1, "One-level theme %s should instantiate only its base plate." % spec.id)

	root.remove_child(component)
	component.free()
	if _failed:
		quit(1)
		return
	print("[modular-mountain-prefab] OK: ramp-free base and independent socket ramps generated.")
	quit(0)


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


func _assert_plate_support_and_ramp_fit(manifest: Dictionary, theme_id: String) -> void:
	var ramp_widths := {}
	for module in manifest.get("ramp_modules", []) as Array:
		var item := module as Dictionary
		var size := item.get("image_size", []) as Array
		if size.size() >= 2:
			ramp_widths[str(item.get("id", ""))] = int(size[0])
	var plate_widths := {}
	for module in manifest.get("plate_modules", []) as Array:
		var item := module as Dictionary
		var size := item.get("image_size", []) as Array
		if size.size() >= 2:
			plate_widths[str(item.get("id", ""))] = int(size[0])
	var base_width := int(plate_widths.get("plate_base", 0))
	var middle_width := int(plate_widths.get("plate_middle", 0))
	var top_width := int(plate_widths.get("plate_top", 0))
	_expect(middle_width >= base_width * 0.78, "Middle plate must retain at least 78%% of the base width for %s." % theme_id)
	_expect(top_width >= base_width * 0.60, "Top plate must retain at least 60%% of the base width for %s." % theme_id)
	_expect(base_width > middle_width and middle_width > top_width, "Plate footprints must decrease progressively for %s." % theme_id)
	_expect(int(ramp_widths.get("ramp_left", 0)) == int(ramp_widths.get("ramp_right", 0)), "Mirrored ramps must use one size for %s." % theme_id)
	_expect(int(ramp_widths.get("ramp_left", 0)) <= top_width * 0.35, "Reusable ramp is too wide for the summit in %s." % theme_id)
	for module in manifest.get("ramp_modules", []) as Array:
		_expect(str((module as Dictionary).get("size_class", "")) == "small", "Every ramp must use the small size class for %s." % theme_id)


func _count_generated_role(component: Node, role: String) -> int:
	var count := 0
	for child in component.get_children():
		if str(child.get_meta("mountain_role", "")) == role:
			count += 1
	return count


func _expect(condition: bool, message: String) -> void:
	if condition:
		return
	_failed = true
	push_error("[modular-mountain-prefab] " + message)
