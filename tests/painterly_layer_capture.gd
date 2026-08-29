extends SceneTree

const TERRAIN_SCRIPT := preload("res://addons/beep_game_builder_cs/ecs/terrain/PainterlyTerrainComponent.cs")

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var terrain := TERRAIN_SCRIPT.new()
	terrain.name = "LayerCaptureTerrain"
	terrain.set("GenerateOnReady", false)
	terrain.set("Mode", 0) # Plain
	terrain.set("Preset", 0) # Grassland
	terrain.set("WidthTiles", 12)
	terrain.set("HeightTiles", 8)
	terrain.set("TileSize", 64)
	terrain.set("PixelsPerTile", 64)
	terrain.set("UseBundledMaterialTextures", false)
	terrain.set("RenderDetailAsOverlay", true)
	root.add_child(terrain)
	await process_frame

	terrain.call("Rebuild")
	await process_frame

	var base_sprite := terrain.get_node_or_null("PainterlyTerrainSprite") as Sprite2D
	var detail_sprite := terrain.get_node_or_null("PainterlyTerrainDetailSprite") as Sprite2D
	if base_sprite == null or base_sprite.texture == null:
		_fail("PainterlyTerrainSprite was not generated.")
	if detail_sprite == null or detail_sprite.texture == null:
		_fail("PainterlyTerrainDetailSprite was not generated.")

	var base_path := "res://tmp/painterly_grass_base_layer.png"
	var detail_path := "res://tmp/painterly_grass_detail_overlay.png"
	var base_error := base_sprite.texture.get_image().save_png(base_path)
	var detail_error := detail_sprite.texture.get_image().save_png(detail_path)
	if base_error != OK:
		_fail("Could not save base layer: " + str(base_error))
	if detail_error != OK:
		_fail("Could not save detail overlay: " + str(detail_error))

	print("[painterly-layer-capture] OK: saved " + base_path + " and " + detail_path)
	quit(0)

func _fail(message: String) -> void:
	push_error("[painterly-layer-capture] " + message)
	quit(1)
