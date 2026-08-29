extends SceneTree

const GENERATOR_SCRIPT := preload("res://addons/beep_game_builder_cs/ecs/terrain/TextureElevationTileSetGeneratorComponent.cs")

const ATLAS_PATH := "res://tmp/generated_forest_elevation_probe.png"
const TILESET_PATH := "res://tmp/generated_forest_elevation_probe.tres"
const MANIFEST_PATH := "res://tmp/generated_forest_elevation_probe.json"
const REFERENCE_SHEET_PATH := "C:/Users/f_ald/source/repos/The-Tech-Idea/Art/TileSets/ForestTileSet/Tilemap_color5.png"

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var generator := GENERATOR_SCRIPT.new()
	generator.name = "TextureElevationTileSetGenerator"
	root.add_child(generator)
	await process_frame

	generator.set("TopTexturePath", REFERENCE_SHEET_PATH)
	generator.set("CliffColumnTexturePath", REFERENCE_SHEET_PATH)
	generator.set("SideCliffTexturePath", REFERENCE_SHEET_PATH)
	generator.set("TopSourceOrigin", Vector2i(320, 0))
	generator.set("TopSourceSize", Vector2i(192, 128))
	generator.set("CliffColumnSourceOrigin", Vector2i(320, 192))
	generator.set("CliffColumnSourceSize", Vector2i(192, 189))
	generator.set("SideCliffSourceOrigin", Vector2i(0, 192))
	generator.set("SideCliffSourceSize", Vector2i(192, 192))
	generator.set("OutputAtlasPath", ATLAS_PATH)
	generator.set("OutputTileSetPath", TILESET_PATH)
	generator.set("OutputManifestPath", MANIFEST_PATH)
	generator.set("Scenario", 1) # Mountain
	generator.set("TileWidth", 128)
	generator.set("TopHeight", 64)
	generator.set("CliffHeight", 128)
	generator.set("UseDirectTextureSampling", true)
	generator.set("UseForestReferenceSheetLayout", true)
	generator.set("TextureRepeatsPerTile", 1.0)
	generator.set("SaveTileSetResource", false)
	generator.set("SaveManifest", true)
	generator.set("PreserveCliffSourceLayout", true)
	generator.set("PreserveReferenceSheetOutputLayout", true)

	var result := str(generator.call("GenerateElevationTileSet"))
	if result != ATLAS_PATH:
		_fail("Generator should return the atlas path.")

	if not FileAccess.file_exists(ATLAS_PATH):
		_fail("Generated elevated atlas PNG is missing.")
	if not FileAccess.file_exists(MANIFEST_PATH):
		_fail("Generated manifest JSON is missing.")

	var atlas := Image.load_from_file(ProjectSettings.globalize_path(ATLAS_PATH))
	if atlas.is_empty():
		_fail("Generated elevated atlas could not be loaded.")
	if atlas.get_width() != 576 or atlas.get_height() != 384:
		_fail("Reference-layout atlas should preserve the source sheet dimensions.")

	if _alpha_coverage(atlas, Rect2i(320, 0, 128, 128), 0.80) < 0.55:
		_fail("top_full should contain an opaque walkable plateau surface.")

	if _alpha_coverage(atlas, Rect2i(320, 192, 128, 192), 0.70) < 0.45:
		_fail("cliff_front_full should contain an opaque cliff wall.")

	if _alpha_coverage(atlas, Rect2i(0, 256, 128, 128), 0.45) < 0.30:
		_fail("side cliff should contain a visible side wall.")

	var text := FileAccess.get_file_as_string(MANIFEST_PATH)
	if not text.contains("\"layout\": \"forest_reference_sheet\""):
		_fail("Manifest should identify the preserved forest reference layout.")
	if not text.contains("\"id\": \"front_columns_full\"") or not text.contains("\"walkable\": false"):
		_fail("Manifest should describe blocked cliff tiles.")

	root.remove_child(generator)
	generator.free()
	print("[texture-elevation-tileset-generator] OK: generated elevated terrain atlas with cliffs and ramps.")
	quit(0)

func _fail(message: String) -> void:
	push_error("[texture-elevation-tileset-generator] " + message)
	quit(1)

func _alpha_coverage(image: Image, rect: Rect2i, threshold: float) -> float:
	var hits := 0
	var total := 0
	for y in range(rect.position.y, rect.position.y + rect.size.y):
		for x in range(rect.position.x, rect.position.x + rect.size.x):
			total += 1
			if image.get_pixel(x, y).a >= threshold:
				hits += 1
	return float(hits) / float(max(1, total))
