extends SceneTree

const COMPONENT := preload("res://addons/beep_game_builder_cs/ecs/terrain/ModularMountainPrefabComponent.cs")
const CREATOR_TEMPLATE := preload("res://addons/beep_game_builder_cs/templates/scenes/modular_front_2_5d_mountain_creator.tscn")
const NATURAL_PLATEAU_TEMPLATE := preload("res://addons/beep_game_builder_cs/templates/scenes/natural_plateau_mountain_creator.tscn")
const HILL_TEMPLATE := preload("res://addons/beep_game_builder_cs/templates/scenes/hill_prefab_1_creator.tscn")
const MANIFEST_PATH := "res://addons/beep_game_builder_cs/generated/mountains/low_poly_sandstone/authored_prefabs/modular_front_2_5d/modular_mountain_pack_manifest.json"
const CATALOG_PATH := "res://addons/beep_game_builder_cs/generated/mountains/low_poly_sandstone/authored_prefabs/modular_themes/modular_mountain_theme_catalog.json"
const NATURAL_PLATEAU_MANIFEST_PATH := "res://addons/beep_game_builder_cs/generated/mountains/natural_plateau/authored_prefabs/mountain_prefab_2/mountain_prefab_2_manifest.json"
const NATURAL_PLATEAU_THEME_ROOT := "res://addons/beep_game_builder_cs/generated/mountains/natural_plateau/authored_prefabs/mountain_prefab_2/themes/"
const NATURAL_PLATEAU_CATALOG_PATH := NATURAL_PLATEAU_THEME_ROOT + "mountain_prefab_2_theme_catalog.json"
const HILL_MANIFEST_PATH := "res://addons/beep_game_builder_cs/generated/hills/soft_grass/authored_prefabs/hill_prefab_1/hill_prefab_1_manifest.json"
const ACCESS_RAMP_CATALOG_PATH := "res://addons/beep_game_builder_cs/generated/mountain_access/ramps/mountain_access_ramps_catalog.json"
const THEME_SPECS := [
	{"enum": 1, "id": "grass_granite", "ramps": 0, "plates": 3, "parts": 3},
	{"enum": 2, "id": "grey_rock", "ramps": 0, "plates": 3, "parts": 3},
	{"enum": 3, "id": "volcanic_basalt", "ramps": 0, "plates": 3, "parts": 3},
	{"enum": 5, "id": "meadow_hill", "ramps": 0, "plates": 3, "parts": 3},
	{"enum": 6, "id": "red_rock_mesa", "ramps": 0, "plates": 3, "parts": 3},
	{"enum": 7, "id": "alpine_snow", "ramps": 0, "plates": 3, "parts": 3},
]
const THEME_ROOT := "res://addons/beep_game_builder_cs/generated/mountains/low_poly_sandstone/authored_prefabs/modular_themes/"
const PREFAB_2_THEME_SPECS := [
	{"enum": 0, "id": "sandstone"},
	{"enum": 1, "id": "grass_granite"},
	{"enum": 2, "id": "grey_rock"},
	{"enum": 3, "id": "volcanic_basalt"},
	{"enum": 5, "id": "meadow_hill"},
	{"enum": 6, "id": "red_rock_mesa"},
	{"enum": 7, "id": "alpine_snow"},
]

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var template_instance: Node = CREATOR_TEMPLATE.instantiate()
	template_instance.set("GenerateOnReady", false)
	root.add_child(template_instance)
	await process_frame
	_expect(template_instance.name == "MountainPrefab1Creator", "Template root must identify Mountain Prefab 1.")
	_expect(int(template_instance.get("PrefabStyle")) == 0, "Template must default to Mountain Prefab 1.")
	_expect(int(template_instance.get("MaterialTheme")) == 0, "Template must default to Low Poly Sandstone.")
	root.remove_child(template_instance)
	template_instance.free()

	var natural_template_instance: Node = NATURAL_PLATEAU_TEMPLATE.instantiate()
	natural_template_instance.set("GenerateOnReady", false)
	root.add_child(natural_template_instance)
	await process_frame
	_expect(natural_template_instance.name == "MountainPrefab2NaturalPlateauCreator", "Natural plateau template must identify Mountain Prefab 2.")
	_expect(int(natural_template_instance.get("PrefabStyle")) == 1, "Natural plateau template must select Mountain Prefab 2.")
	_expect(int(natural_template_instance.get("MaterialTheme")) == 1, "Natural plateau template must default to Grass + Granite.")
	_expect(str(natural_template_instance.get("BasePrefabId")) == "natural_plateau_three_level_no_ramps", "Natural plateau template must select the full three-level plateau.")
	root.remove_child(natural_template_instance)
	natural_template_instance.free()

	var hill_template_instance: Node = HILL_TEMPLATE.instantiate()
	hill_template_instance.set("GenerateOnReady", false)
	root.add_child(hill_template_instance)
	await process_frame
	_expect(hill_template_instance.name == "HillPrefab1Creator", "Hill template root must identify Hill Prefab 1.")
	_expect(int(hill_template_instance.get("MaterialTheme")) == 4, "Hill template must use custom manifest mode.")
	_expect(str(hill_template_instance.get("PackManifestPath")) == HILL_MANIFEST_PATH, "Hill template must point at the Hill Prefab 1 manifest.")
	_expect(str(hill_template_instance.get("BasePrefabId")) == "hill_large", "Hill template must select the large hill by default.")
	root.remove_child(hill_template_instance)
	hill_template_instance.free()

	var manifest := _read_json(MANIFEST_PATH)
	_expect(str(manifest.get("style_id", "")) == "mountain_prefab_1", "Default pack must be Mountain Prefab 1.")
	_expect((manifest.get("base_prefabs", []) as Array).size() == 2, "Pack needs one-level and three-level ramp-free mountain bases.")
	_expect((manifest.get("plate_modules", []) as Array).size() == 3, "Pack needs base, middle, and top plate layers.")
	_expect((manifest.get("ramp_modules", []) as Array).is_empty(), "Pack must not include ramp modules.")
	_expect((manifest.get("jump_step_modules", []) as Array).size() == 2, "Pack needs quarter and half manual step plates.")
	_assert_manual_step_heights(manifest, "sandstone")
	var sheets := manifest.get("atlas_sheets", []) as Array
	_expect(sheets.size() == 3, "Pack needs plate, manual step, and one-level atlas sheets.")
	if not sheets.is_empty():
		var plate_regions := (sheets[0] as Dictionary).get("regions", {}) as Dictionary
		_expect(plate_regions.size() == 3, "Plate sheet needs base, middle, and top regions.")

	var component: Node = COMPONENT.new()
	root.add_child(component)
	await process_frame
	component.set("GenerateOnReady", false)
	component.set("MaterialTheme", 4)
	component.set("PackManifestPath", MANIFEST_PATH)
	var generated_part_count := int(component.call("GenerateMountain"))
	var summary := component.call("GetLastGenerationSummary") as Dictionary
	_expect(str(summary.get("prefab_style", "")) == "MountainPrefab1", "Component must report Mountain Prefab 1.")
	_expect(generated_part_count == 3, "Composite sandstone mountain should generate three separate plate layers.")
	_expect(int(summary.get("ramp_count", -1)) == 0, "The component must never place ramps automatically.")
	_expect(_count_generated_role(component, "ramp_module") == 0, "Generated sandstone mountain must be ramp-free.")
	_expect(_count_generated_role(component, "level_plate") == 3, "Composite sandstone mountain should generate three separate plate layers.")

	for spec in THEME_SPECS:
		var manifest_path := THEME_ROOT + str(spec.id) + "/modular_mountain_pack_manifest.json"
		var theme_manifest := _read_json(manifest_path)
		_expect((theme_manifest.get("base_prefabs", []) as Array).size() == 2, "Every material theme needs one-level and three-level bases.")
		_expect(str(theme_manifest.get("style_id", "")) == "mountain_prefab_1", "Theme %s must use Mountain Prefab 1." % spec.id)
		_expect((theme_manifest.get("ramp_modules", []) as Array).size() == int(spec.ramps), "Theme %s has the wrong ramp module count." % spec.id)
		_expect((theme_manifest.get("plate_modules", []) as Array).size() == int(spec.plates), "Theme %s has the wrong plate module count." % spec.id)
		_expect((theme_manifest.get("jump_step_modules", []) as Array).size() == 2, "Theme %s needs quarter and half manual step plates." % spec.id)
		if int(spec.plates) == 3:
			_assert_plate_support(theme_manifest, str(spec.id))
			_assert_manual_step_heights(theme_manifest, str(spec.id))
		component.set("MaterialTheme", int(spec.enum))
		component.set("BasePrefabId", "three_level_wide_no_ramps")
		_expect(int(component.call("GenerateMountain")) == int(spec.parts), "Built-in material theme %s should generate only its mountain structure." % spec.id)
		var theme_summary := component.call("GetLastGenerationSummary") as Dictionary
		_expect(str(theme_summary.get("manifest_path", "")) == manifest_path, "Theme selector should resolve the expected manifest.")
		_expect(int(theme_summary.get("ramp_count", -1)) == 0, "Theme %s must remain ramp-free." % spec.id)
		_expect(_count_generated_role(component, "ramp_module") == 0, "Theme %s must not instantiate ramp sprites." % spec.id)
		if int(spec.plates) == 3:
			_expect(_count_generated_role(component, "level_plate") == 3, "Three-level theme %s should instantiate three separate plates." % spec.id)
		component.set("BasePrefabId", "one_level_wide_no_ramps")
		_expect(int(component.call("GenerateMountain")) == 1, "One-level bases should generate one ramp-free structure.")
		var one_level_summary := component.call("GetLastGenerationSummary") as Dictionary
		_expect(int(one_level_summary.get("level_count", 0)) == 1, "One-level generation should report one walkable level.")
		if int(spec.plates) == 3:
			_expect(_count_generated_role(component, "level_plate") == 1, "One-level theme %s should instantiate only its base plate." % spec.id)

	var catalog := _read_json(CATALOG_PATH)
	_expect(str(catalog.get("style_id", "")) == "mountain_prefab_1", "Theme catalog must use Mountain Prefab 1.")
	_expect((catalog.get("themes", []) as Array).size() == 7, "Mountain Prefab 1 needs seven material variations.")
	if FileAccess.file_exists(ACCESS_RAMP_CATALOG_PATH):
		_assert_access_ramp_catalog(_read_json(ACCESS_RAMP_CATALOG_PATH))

	if FileAccess.file_exists(HILL_MANIFEST_PATH):
		var hill_manifest := _read_json(HILL_MANIFEST_PATH)
		_expect(str(hill_manifest.get("style_id", "")) == "hill_prefab_1", "Hill manifest must use Hill Prefab 1.")
		_expect(bool(hill_manifest.get("visual_only", false)), "Hill prefab must be visual-only.")
		_expect((hill_manifest.get("ramp_modules", []) as Array).is_empty(), "Hill prefab must not include ramp modules.")
		_expect((hill_manifest.get("hill_modules", []) as Array).size() == 6, "Hill prefab must expose all six hill modules.")
		_expect((hill_manifest.get("jump_step_modules", []) as Array).size() == 2, "Hill prefab must expose quarter and half helper plates.")
		var hill_prefabs := hill_manifest.get("base_prefabs", []) as Array
		_expect(hill_prefabs.size() == 2, "Hill prefab manifest must expose large and square declined-edge base prefabs.")
		_expect(str((hill_prefabs[0] as Dictionary).get("id", "")) == "hill_large", "Hill prefab must identify hill_large.")
		_expect(str((hill_prefabs[1] as Dictionary).get("id", "")) == "square_declined_edge_plate", "Hill prefab must identify the square declined-edge plate.")
		component.set("PrefabStyle", 0)
		component.set("MaterialTheme", 4)
		component.set("PackManifestPath", HILL_MANIFEST_PATH)
		component.set("BasePrefabId", "hill_large")
		_expect(int(component.call("GenerateMountain")) == 1, "Hill prefab should generate one visual hill sprite.")
		component.set("BasePrefabId", "square_declined_edge_plate")
		_expect(int(component.call("GenerateMountain")) == 1, "Square declined-edge hill plate should generate one visual sprite.")

	if FileAccess.file_exists(NATURAL_PLATEAU_MANIFEST_PATH):
		var natural_manifest := _read_json(NATURAL_PLATEAU_MANIFEST_PATH)
		_expect(str(natural_manifest.get("style_id", "")) == "mountain_prefab_2", "Natural plateau manifest must use Mountain Prefab 2.")
		_expect(bool(natural_manifest.get("visual_only", false)), "Natural plateau must be marked visual-only.")
		_expect((natural_manifest.get("ramp_modules", []) as Array).is_empty(), "Natural plateau pack must remain ramp-free.")
		_expect((natural_manifest.get("plate_modules", []) as Array).size() == 3, "Natural plateau pack must expose base, middle, and top plate modules.")
		var jump_steps := natural_manifest.get("jump_step_modules", []) as Array
		_expect(jump_steps.size() == 2, "Natural plateau pack must expose quarter and half manual step modules.")
		_expect((natural_manifest.get("sockets", []) as Array).is_empty(), "Natural plateau must not contain placement sockets.")
		component.set("PrefabStyle", 1)
		var natural_prefabs := natural_manifest.get("base_prefabs", []) as Array
		_expect(natural_prefabs.size() == 2, "Natural plateau manifest must contain three-level and one-level variants.")
		_expect(str((natural_prefabs[0] as Dictionary).get("id", "")) == "natural_plateau_three_level_no_ramps", "Natural plateau manifest must identify the full three-level variant.")
		_expect(str((natural_prefabs[1] as Dictionary).get("id", "")) == "natural_plateau_extra_wide", "Natural plateau manifest must identify the one-level base plate variant.")
		_expect(((natural_prefabs[0] as Dictionary).get("plate_assembly", []) as Array).size() == 3, "The three-level natural plateau must be assembled from three plate sprites.")
		_expect(not str((natural_prefabs[1] as Dictionary).get("fill_tile", "")).is_empty(), "One-level plateau must expose its matching fill tile.")
		var jump_step_ids := {}
		var jump_step_heights := {}
		for step in jump_steps:
			var step_data := step as Dictionary
			var step_id := str(step_data.get("id", ""))
			jump_step_ids[step_id] = true
			_expect(float(step_data.get("relative_level_height", 1.0)) < 1.0, "Every jump step must remain lower than the mountain level.")
			_expect(str(step_data.get("placement", "")) == "manual", "Jump steps must remain manually placed.")
			var step_size := step_data.get("image_size", []) as Array
			if step_size.size() >= 2:
				jump_step_heights[step_id] = int(step_size[1])
		_expect(jump_step_ids.has("step_quarter") and jump_step_ids.has("step_half"), "Natural plateau pack needs quarter and half step plates.")
		_expect(int(jump_step_heights.get("step_quarter", 9999)) < int(jump_step_heights.get("step_half", 0)), "Quarter step must be shorter than half step.")
		component.set("BasePrefabId", "natural_plateau_three_level_no_ramps")
		var natural_catalog := _read_json(NATURAL_PLATEAU_CATALOG_PATH)
		_expect(str(natural_catalog.get("style_id", "")) == "mountain_prefab_2", "Natural plateau catalog must use Mountain Prefab 2.")
		_expect((natural_catalog.get("themes", []) as Array).size() == 7, "Mountain Prefab 2 needs seven material variations.")
		for spec in PREFAB_2_THEME_SPECS:
			var natural_theme_path := NATURAL_PLATEAU_THEME_ROOT + str(spec.id) + "/mountain_prefab_2_manifest.json"
			var natural_theme := _read_json(natural_theme_path)
			_expect(str(natural_theme.get("theme_id", "")) == str(spec.id), "Mountain Prefab 2 theme id must match %s." % spec.id)
			_expect((natural_theme.get("base_prefabs", []) as Array).size() == 2, "Prefab 2 theme %s needs three-level and one-level bases." % spec.id)
			_expect((natural_theme.get("plate_modules", []) as Array).size() == 3, "Prefab 2 theme %s needs three plate modules." % spec.id)
			_expect((natural_theme.get("jump_step_modules", []) as Array).size() == 2, "Prefab 2 theme %s needs quarter and half manual steps." % spec.id)
			_expect((natural_theme.get("ramp_modules", []) as Array).is_empty(), "Prefab 2 theme %s must remain ramp-free." % spec.id)
			_assert_manual_step_heights(natural_theme, str(spec.id))
			component.set("MaterialTheme", int(spec.enum))
			component.set("BasePrefabId", "natural_plateau_three_level_no_ramps")
			_expect(int(component.call("GenerateMountain")) == 3, "Prefab 2 theme %s must generate three separate plateau plate sprites." % spec.id)
			var natural_summary := component.call("GetLastGenerationSummary") as Dictionary
			_expect(str(natural_summary.get("prefab_style", "")) == "MountainPrefab2NaturalPlateau", "Component must report Mountain Prefab 2.")
			_expect(str(natural_summary.get("manifest_path", "")) == natural_theme_path, "Prefab 2 selector must resolve theme %s." % spec.id)
			_expect(int(natural_summary.get("ramp_count", -1)) == 0, "Natural plateau component must remain ramp-free.")
			_expect(_count_generated_role(component, "level_plate") == 3, "Prefab 2 theme %s must instantiate three level plates." % spec.id)

	root.remove_child(component)
	component.free()
	if _failed:
		quit(1)
		return
	print("[modular-mountain-prefab] OK: both prefab styles and all material themes generated ramp-free structures.")
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


func _assert_plate_support(manifest: Dictionary, theme_id: String) -> void:
	var plate_widths := {}
	for module in manifest.get("plate_modules", []) as Array:
		var item := module as Dictionary
		var size := item.get("image_size", []) as Array
		if size.size() >= 2:
			plate_widths[str(item.get("id", ""))] = int(size[0])
	var base_width := int(plate_widths.get("plate_base", 0))
	var middle_width := int(plate_widths.get("plate_middle", 0))
	var top_width := int(plate_widths.get("plate_top", 0))
	_expect(base_width > 0 and middle_width > 0 and top_width > 0, "All plate layers need valid widths for %s." % theme_id)
	_expect(base_width > middle_width and middle_width > top_width, "Plate footprints must decrease progressively for %s." % theme_id)


func _assert_manual_step_heights(manifest: Dictionary, theme_id: String) -> void:
	var top_height := 0
	for module in manifest.get("plate_modules", []) as Array:
		var item := module as Dictionary
		if str(item.get("id", "")) == "plate_top":
			var size := item.get("image_size", []) as Array
			if size.size() >= 2:
				top_height = int(size[1])
	var step_heights := {}
	for module in manifest.get("jump_step_modules", []) as Array:
		var item := module as Dictionary
		var size := item.get("image_size", []) as Array
		if size.size() >= 2:
			step_heights[str(item.get("id", ""))] = int(size[1])
		_expect(not str(item.get("alias_file", "")).is_empty(), "Theme %s manual step should expose a plate alias file." % theme_id)
	var quarter_height := int(step_heights.get("step_quarter", 0))
	var half_height := int(step_heights.get("step_half", 0))
	_expect(top_height > 0, "Theme %s needs a measurable top plate height." % theme_id)
	_expect(quarter_height > 0 and half_height > 0, "Theme %s needs quarter and half step heights." % theme_id)
	_expect(quarter_height < half_height, "Theme %s quarter plate must be shorter than half plate." % theme_id)
	_expect(abs((float(quarter_height) / float(top_height)) - 0.25) < 0.05, "Theme %s quarter plate must be near one-quarter top height." % theme_id)
	_expect(abs((float(half_height) / float(top_height)) - 0.5) < 0.05, "Theme %s half plate must be near one-half top height." % theme_id)


func _assert_access_ramp_catalog(catalog: Dictionary) -> void:
	_expect(str(catalog.get("catalog_id", "")) == "mountain_access_ramps", "Access ramp catalog must identify the mountain access ramp pack.")
	var styles := catalog.get("styles", []) as Array
	_expect(styles.size() == 2, "Access ramp catalog must expose Prefab 1 and Prefab 2 ramp packs.")
	var expected_directions := {"front": true, "left": true, "right": true}
	for style in styles:
		var style_data := style as Dictionary
		var themes := style_data.get("themes", []) as Array
		_expect(themes.size() == 7, "Access ramp style %s needs seven material variations." % str(style_data.get("style_id", "")))
		for theme in themes:
			var theme_data := theme as Dictionary
			_expect(int(theme_data.get("ramp_count", 0)) == 9, "Access ramp theme %s must expose nine narrow ramps." % str(theme_data.get("theme_id", "")))
			var manifest_path := "res://" + str(theme_data.get("manifest", ""))
			var manifest := _read_json(manifest_path)
			var ramps := manifest.get("ramp_modules", []) as Array
			_expect(ramps.size() == 9, "Access ramp manifest %s must contain nine ramp modules." % str(theme_data.get("theme_id", "")))
			var front_heights := {}
			var seen_directions := {}
			for ramp in ramps:
				var ramp_data := ramp as Dictionary
				var direction := str(ramp_data.get("direction", ""))
				seen_directions[direction] = true
				_expect(expected_directions.has(direction), "Access ramp has an unsupported direction: %s." % direction)
				_expect(str(ramp_data.get("placement", "")) == "manual", "Access ramp %s must be manually placed." % str(ramp_data.get("id", "")))
				_expect(bool(ramp_data.get("narrow_profile", false)), "Access ramp %s must be marked as a narrow profile." % str(ramp_data.get("id", "")))
				var size := ramp_data.get("image_size", []) as Array
				_expect(size.size() >= 2, "Access ramp %s needs image dimensions." % str(ramp_data.get("id", "")))
				if size.size() >= 2:
					_expect(int(size[0]) <= 160, "Access ramp %s is too wide for game-scale placement." % str(ramp_data.get("id", "")))
					if direction == "front":
						front_heights[str(ramp_data.get("height_class", ""))] = int(size[1])
			_expect(seen_directions.has("front") and seen_directions.has("left") and seen_directions.has("right"), "Access ramp theme %s must include front, left, and right ramps." % str(theme_data.get("theme_id", "")))
			_expect(int(front_heights.get("quarter", 9999)) < int(front_heights.get("half", 0)), "Quarter front ramp must be shorter than half front ramp.")
			_expect(int(front_heights.get("half", 9999)) < int(front_heights.get("full", 0)), "Half front ramp must be shorter than full front ramp.")


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
