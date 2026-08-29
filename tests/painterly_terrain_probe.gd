extends SceneTree

const TERRAIN_SCRIPT := preload("res://addons/beep_game_builder_cs/ecs/terrain/PainterlyTerrainComponent.cs")

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	await _check_generate_on_ready_can_be_disabled()
	await _check_quality_defaults_are_not_low_res()
	await _check_ground_detail_requires_an_explicit_region()
	await _check_plain_grass_base_stays_green()
	await _check_biome_detail_layers_are_patch_limited()
	await _check_biome_detail_layers_break_up_plain_base()
	await _check_bounded_generation_and_water_alpha()
	print("[painterly-terrain] OK: terrain generation is explicit, layered, bounded, and preserves transparent water.")
	quit(0)

func _check_generate_on_ready_can_be_disabled() -> void:
	var terrain := TERRAIN_SCRIPT.new()
	terrain.name = "Terrain"
	terrain.set("GenerateOnReady", false)
	root.add_child(terrain)
	await process_frame
	await process_frame

	if terrain.get_node_or_null("PainterlyTerrainSprite") != null:
		_fail("GenerateOnReady=false still created a terrain sprite.")

	root.remove_child(terrain)
	terrain.free()

func _check_quality_defaults_are_not_low_res() -> void:
	var terrain := TERRAIN_SCRIPT.new()
	terrain.name = "DefaultQualityTerrain"
	terrain.set("GenerateOnReady", false)
	terrain.set("Mode", 0) # Plain
	terrain.set("Preset", 1) # Desert
	terrain.set("WidthTiles", 24)
	terrain.set("HeightTiles", 16)
	root.add_child(terrain)
	await process_frame

	terrain.call("Rebuild")
	await process_frame

	var sprite := terrain.get_node_or_null("PainterlyTerrainSprite") as Sprite2D
	if sprite == null or sprite.texture == null:
		_fail("Default-quality terrain did not generate a texture.")
	if sprite.texture.get_width() < 192:
		_fail("Default generated terrain resolution is too low: " + str(sprite.texture.get_width()) + "px for 4 tiles.")
	if sprite.scale.x > 1.5:
		_fail("Default terrain scale still magnifies too much and will look blurry: " + str(sprite.scale.x))

	root.remove_child(terrain)
	terrain.free()

func _check_plain_grass_base_stays_green() -> void:
	var terrain := TERRAIN_SCRIPT.new()
	terrain.name = "PlainGreenTerrain"
	terrain.set("GenerateOnReady", false)
	terrain.set("Mode", 0) # Plain
	terrain.set("Preset", 0) # Grassland
	terrain.set("WidthTiles", 4)
	terrain.set("HeightTiles", 4)
	terrain.set("TileSize", 64)
	terrain.set("PixelsPerTile", 32)
	terrain.set("HeightTintStrength", 0.0)
	terrain.set("EnableBiomeDetailLayers", true)
	terrain.set("DefaultGroundDetailMask", 1.0)
	root.add_child(terrain)
	await process_frame

	if bool(terrain.get("UseBundledMaterialTextures")):
		_fail("Painterly terrain defaults should keep bundled material textures opt-in so the first layer stays plain.")

	terrain.call("Rebuild")
	await process_frame

	var sprite := terrain.get_node_or_null("PainterlyTerrainSprite") as Sprite2D
	if sprite == null or sprite.texture == null:
		_fail("Plain grass terrain did not generate a texture.")
	var detail_sprite := terrain.get_node_or_null("PainterlyTerrainDetailSprite") as Sprite2D
	if detail_sprite == null or detail_sprite.texture == null:
		_fail("Plain grass terrain did not generate a separate detail overlay texture.")
	if bool(terrain.get("LastGeneratedSeparateDetailLayer")) != true:
		_fail("Painterly terrain did not report the separate detail layer.")

	var image := sprite.texture.get_image()
	var total := Vector3.ZERO
	var count := 0.0
	var min_luma := 999.0
	var max_luma := -999.0
	for y in range(8, image.get_height(), 16):
		for x in range(8, image.get_width(), 16):
			var c := image.get_pixel(x, y)
			total += Vector3(c.r, c.g, c.b)
			var luma := (c.r * 0.299) + (c.g * 0.587) + (c.b * 0.114)
			min_luma = min(min_luma, luma)
			max_luma = max(max_luma, luma)
			count += 1.0
	var average: Vector3 = total / max(count, 1.0)
	if average.y <= average.x or average.y <= average.z:
		_fail("Plain grass base should remain visibly green; average colour was " + str(average))
	if average.z > 0.22 or average.x / max(average.y, 0.01) > 0.86:
		_fail("Plain grass base became too pale/desaturated; average colour was " + str(average))
	if max_luma - min_luma > 0.01:
		_fail("Plain grass base layer should stay flat; luma variation was " + str(max_luma - min_luma))

	var detail_image := detail_sprite.texture.get_image()
	var visible_detail := 0
	var detail_samples := 0
	var translucent_detail := 0
	for y in range(8, detail_image.get_height(), 16):
		for x in range(8, detail_image.get_width(), 16):
			detail_samples += 1
			var detail := detail_image.get_pixel(x, y)
			if detail.a > 0.01:
				visible_detail += 1
				if detail.a < 0.99:
					translucent_detail += 1
	var default_coverage: float = float(visible_detail) / max(float(detail_samples), 1.0)
	if default_coverage <= 0.01:
		_fail("Default plain grass detail layer should add small local grass marks. Generated pixels: " + str(terrain.get("LastGroundDetailPixelCount")))
	if default_coverage >= 0.35:
		_fail("Default plain grass detail layer covered too much of the scene: " + str(default_coverage))
	if translucent_detail > 0:
		_fail("Grass detail overlay should not use pale translucent alpha pixels; translucent samples: " + str(translucent_detail))

	root.remove_child(terrain)
	terrain.free()

func _check_ground_detail_requires_an_explicit_region() -> void:
	var terrain := TERRAIN_SCRIPT.new()
	terrain.name = "UnmaskedTerrain"
	terrain.set("GenerateOnReady", false)
	terrain.set("Mode", 0) # Plain
	terrain.set("Preset", 0) # Grassland
	terrain.set("WidthTiles", 4)
	terrain.set("HeightTiles", 4)
	terrain.set("EnableBiomeDetailLayers", true)
	root.add_child(terrain)
	await process_frame

	terrain.call("Rebuild")
	await process_frame
	var detail_sprite := terrain.get_node_or_null("PainterlyTerrainDetailSprite") as Sprite2D
	if detail_sprite == null or detail_sprite.texture == null:
		_fail("Unmasked terrain did not generate an explicit detail layer.")
	var image := detail_sprite.texture.get_image()
	for y in range(0, image.get_height(), 8):
		for x in range(0, image.get_width(), 8):
			if image.get_pixel(x, y).a > 0.0:
				_fail("Biome detail appeared without a detail region mask.")

	root.remove_child(terrain)
	terrain.free()

func _check_biome_detail_layers_break_up_plain_base() -> void:
	var terrain := TERRAIN_SCRIPT.new()
	terrain.name = "LayeredDesertTerrain"
	terrain.set("GenerateOnReady", false)
	terrain.set("Mode", 0) # Plain
	terrain.set("Preset", 1) # Desert
	terrain.set("WidthTiles", 4)
	terrain.set("HeightTiles", 4)
	terrain.set("TileSize", 64)
	terrain.set("PixelsPerTile", 32)
	terrain.set("UseBundledMaterialTextures", false)
	terrain.set("GrainStrength", 0.0)
	terrain.set("HeightTintStrength", 0.0)
	terrain.set("PostSharpenStrength", 0.0)
	terrain.set("EnableBiomeDetailLayers", true)
	terrain.set("DefaultGroundDetailMask", 1.0)
	terrain.set("BiomeDetailStrength", 1.0)
	terrain.set("BiomeDetailDensity", 1.35)
	terrain.set("BiomeDetailCoverage", 1.0)
	terrain.set("DuneLayerStrength", 0.65)
	terrain.set("PebbleLayerStrength", 0.45)
	root.add_child(terrain)
	await process_frame

	terrain.call("Rebuild")
	await process_frame

	var sprite := terrain.get_node_or_null("PainterlyTerrainSprite") as Sprite2D
	if sprite == null or sprite.texture == null:
		_fail("Layered desert terrain did not generate a texture.")
	var detail_sprite := terrain.get_node_or_null("PainterlyTerrainDetailSprite") as Sprite2D
	if detail_sprite == null or detail_sprite.texture == null:
		_fail("Layered desert terrain did not generate a separate detail overlay.")

	var base_image := sprite.texture.get_image()
	var detail_image := detail_sprite.texture.get_image()
	var base_colours := {}
	var min_luma := 999.0
	var max_luma := -999.0
	var sampled_colours := {}
	var visible_detail_pixels := 0
	for y in range(0, detail_image.get_height(), 8):
		for x in range(0, detail_image.get_width(), 8):
			var b := base_image.get_pixel(x, y)
			base_colours[str(round(b.r * 255.0)) + "," + str(round(b.g * 255.0)) + "," + str(round(b.b * 255.0))] = true
			var c := detail_image.get_pixel(x, y)
			if c.a <= 0.005:
				continue
			visible_detail_pixels += 1
			var luma := (c.r * 0.299) + (c.g * 0.587) + (c.b * 0.114)
			min_luma = min(min_luma, luma)
			max_luma = max(max_luma, luma)
			sampled_colours[str(round(c.r * 255.0)) + "," + str(round(c.g * 255.0)) + "," + str(round(c.b * 255.0))] = true

	if base_colours.size() > 2:
		_fail("Biome base layer should stay plain while details render above it; sampled base colours: " + str(base_colours.size()))
	if visible_detail_pixels < 4:
		_fail("Biome detail overlay did not produce enough visible detail pixels: " + str(visible_detail_pixels))
	if sampled_colours.size() < 2:
		_fail("Biome detail layers did not add enough visible detail variation: " + str(sampled_colours.size()))
	if max_luma - min_luma < 0.045:
		_fail("Biome detail layers did not add enough light/dark terrain shape: " + str(max_luma - min_luma))

	root.remove_child(terrain)
	terrain.free()

func _check_biome_detail_layers_are_patch_limited() -> void:
	var terrain := TERRAIN_SCRIPT.new()
	terrain.name = "PatchLimitedGrassTerrain"
	terrain.set("GenerateOnReady", false)
	terrain.set("Mode", 0) # Plain
	terrain.set("Preset", 0) # Grassland
	terrain.set("WidthTiles", 24)
	terrain.set("HeightTiles", 16)
	terrain.set("TileSize", 64)
	terrain.set("PixelsPerTile", 16)
	terrain.set("UseBundledMaterialTextures", false)
	terrain.set("GrainStrength", 0.0)
	terrain.set("EnableBiomeDetailLayers", true)
	terrain.set("GroundDetailRegionMode", 1) # ProceduralPatches
	terrain.set("BiomeDetailStrength", 1.0)
	terrain.set("BiomeDetailDensity", 1.0)
	terrain.set("BiomeDetailCoverage", 0.65)
	terrain.set("BiomeDetailPatchScale", 0.18)
	root.add_child(terrain)
	await process_frame
	if int(terrain.get("GroundDetailRegionMode")) != 1:
		_fail("Procedural detail-region mode was not applied.")

	terrain.call("Rebuild")
	await process_frame

	var sprite := terrain.get_node_or_null("PainterlyTerrainSprite") as Sprite2D
	var detail_sprite := terrain.get_node_or_null("PainterlyTerrainDetailSprite") as Sprite2D
	if sprite == null or sprite.texture == null:
		_fail("Patch-limited grass terrain did not generate a base texture.")
	if detail_sprite == null or detail_sprite.texture == null:
		_fail("Patch-limited grass terrain did not generate a detail overlay.")

	var base_image := sprite.texture.get_image()
	var detail_image := detail_sprite.texture.get_image()
	var visible_detail := 0
	var samples := 0
	var base_colours := {}
	for y in range(4, detail_image.get_height(), 8):
		for x in range(4, detail_image.get_width(), 8):
			samples += 1
			var b := base_image.get_pixel(x, y)
			base_colours[str(round(b.r * 255.0)) + "," + str(round(b.g * 255.0)) + "," + str(round(b.b * 255.0))] = true
			if detail_image.get_pixel(x, y).a > 0.01:
				visible_detail += 1

	var coverage: float = float(terrain.get("LastGroundDetailPixelCount")) / max(float(detail_image.get_width() * detail_image.get_height()), 1.0)
	if base_colours.size() > 1:
		_fail("Patch-limited biome details changed the plain grass base layer: " + str(base_colours.size()))
	if coverage <= 0.002:
		_fail("Patch-limited biome details did not create visible local patches. Generated pixels: " + str(terrain.get("LastGroundDetailPixelCount")))
	if coverage >= 0.45:
		_fail("Patch-limited biome details covered too much of the scene: " + str(coverage))

	root.remove_child(terrain)
	terrain.free()

func _check_bounded_generation_and_water_alpha() -> void:
	var terrain := TERRAIN_SCRIPT.new()
	terrain.name = "Terrain"
	terrain.set("GenerateOnReady", false)
	terrain.set("Mode", 0) # Plain
	terrain.set("Preset", 4) # Sea
	terrain.set("WidthTiles", 8)
	terrain.set("HeightTiles", 6)
	terrain.set("TileSize", 64)
	terrain.set("PixelsPerTile", 32)
	terrain.set("MaxGeneratedPixels", 768)
	terrain.set("WaterAlpha", 0.72)
	terrain.set("ShallowWaterAlpha", 0.58)
	root.add_child(terrain)
	await process_frame

	terrain.call("Rebuild")
	await process_frame

	var sprite := terrain.get_node_or_null("PainterlyTerrainSprite") as Sprite2D
	if sprite == null:
		_fail("Rebuild did not create PainterlyTerrainSprite.")
	if sprite.texture == null:
		_fail("PainterlyTerrainSprite has no generated texture.")
	if terrain.get_children().filter(func(c): return c.name == "PainterlyTerrainSprite").size() != 1:
		_fail("Repeated terrain rebuilds must reuse the same PainterlyTerrainSprite.")

	var tex := sprite.texture
	var pixels := tex.get_width() * tex.get_height()
	if pixels > 768:
		_fail("Generated texture exceeded MaxGeneratedPixels: " + str(pixels))
	if int(terrain.get("LastGeneratedPixelCount")) != pixels:
		_fail("LastGeneratedPixelCount did not match generated texture size.")
	if int(terrain.get("LastGeneratedPixelsPerTile")) != tex.get_width() / 8:
		_fail("LastGeneratedPixelsPerTile did not expose the effective capped resolution.")
	if terrain.get("LastGenerationWasCapped") != true:
		_fail("LastGenerationWasCapped did not report that MaxGeneratedPixels reduced output resolution.")
	var applied_scale: Vector2 = terrain.get("LastAppliedTerrainScale")
	if applied_scale.x <= 1.0:
		_fail("LastAppliedTerrainScale did not expose the upscale caused by the safety cap.")

	var water_sprite := terrain.get_node_or_null("PainterlyTerrainWaterSprite") as Sprite2D
	if water_sprite == null or water_sprite.texture == null:
		_fail("Sea preset did not create a separate water layer texture.")
	var image := water_sprite.texture.get_image()
	var alpha := image.get_pixel(0, 0).a
	if alpha >= 0.99:
		_fail("Sea preset should preserve transparent water overlay pixels; alpha was " + str(alpha))

	terrain.call("Rebuild")
	await process_frame
	if terrain.get_children().filter(func(c): return c.name == "PainterlyTerrainSprite").size() != 1:
		_fail("Second Rebuild created a duplicate PainterlyTerrainSprite.")
	if terrain.get_children().filter(func(c): return c.name == "PainterlyTerrainDetailSprite").size() != 1:
		_fail("Second Rebuild created a duplicate PainterlyTerrainDetailSprite.")

	root.remove_child(terrain)
	terrain.free()

func _fail(message: String) -> void:
	push_error("[painterly-terrain] " + message)
	quit(1)
