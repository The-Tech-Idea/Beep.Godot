extends SceneTree

const GENERATOR_SCRIPT := preload("res://addons/beep_game_builder_cs/ecs/terrain/MountainTileMapLayerGeneratorComponent.cs")

const MANIFEST_PATH := "res://tmp/mountain_hill_volcano_role_dev/role_manifest.json"

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var generator := GENERATOR_SCRIPT.new()
	generator.name = "MountainTileMapLayerGenerator"
	root.add_child(generator)
	await process_frame

	generator.set("ManifestPath", MANIFEST_PATH)
	generator.set("CreateLayerIfMissing", true)
	generator.set("CreatedLayerName", "ProbeMountainLayer")
	generator.set("OriginCell", Vector2i(-8, -5))
	generator.set("MountainSize", Vector2i(18, 14))
	generator.set("Seed", 90210)
	generator.set("PropDensity", 0.12)
	generator.set("AddRoadCut", true)

	var painted := int(generator.call("GenerateMountain"))
	if painted <= 0:
		_fail("GenerateMountain should paint at least one cell.")

	var layer := generator.call("GetTileMapLayer") as TileMapLayer
	if layer == null:
		_fail("Generator should create a TileMapLayer when none is assigned.")

	if layer.name != "ProbeMountainLayer":
		_fail("Created TileMapLayer should use CreatedLayerName.")

	if layer.tile_set == null:
		_fail("Created TileMapLayer should receive a runtime TileSet.")

	if layer.get_used_cells().size() <= 0:
		_fail("Created TileMapLayer should contain used cells.")

	var summary := generator.call("GetLastGenerationSummary") as Dictionary
	if int(summary.get("asset_count", 0)) <= 0:
		_fail("Summary should report loaded manifest assets.")

	var categories := summary.get("categories", {}) as Dictionary
	if not categories.has("top_surface"):
		_fail("Loaded manifest should include top_surface assets.")
	if not categories.has("cliff_face"):
		_fail("Loaded manifest should include cliff_face assets.")
	if not categories.has("slope_ramp"):
		_fail("Loaded manifest should include slope_ramp assets.")
	var roles := summary.get("roles", {}) as Dictionary
	if not roles.has("top_center"):
		_fail("Loaded manifest should include top_center role.")
	if not roles.has("cliff_front"):
		_fail("Loaded manifest should include cliff_front role.")
	if not roles.has("road_vertical"):
		_fail("Loaded manifest should include road_vertical role.")

	root.remove_child(generator)
	generator.free()
	print("[mountain-tilemap-layer-generator] OK: created TileMapLayer and painted mountain.")
	quit(0)

func _fail(message: String) -> void:
	push_error("[mountain-tilemap-layer-generator] " + message)
	quit(1)
