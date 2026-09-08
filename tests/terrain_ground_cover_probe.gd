extends SceneTree

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	var scene: Node = load("res://addons/beep_game_builder_cs/templates/scenes/terrain/terrain_generator_lab.tscn").instantiate()
	scene.get_node("World").set("MapSize", 0)
	scene.get_node("World").set("Seed", 31415)
	root.add_child(scene)
	await process_frame
	await process_frame
	var generator: Node = scene.get_node("Preview/TerrainGenerator")
	var cells: Node = scene.get_node("Preview/Cells")
	var rocks: Node = scene.get_node("Preview/RockObjects")
	var gray := 0
	var raised := 0
	var land := 0
	for y in 32:
		for x in 32:
			var at := Vector2i(x, y)
			var kind: String = cells.call("GetTerrainKind", at)
			if generator.call("WaterSourceAt", at) == "": land += 1
			if kind in ["rock", "gravel"]: gray += 1
			if generator.call("ReliefAt", at) > 0: raised += 1
	assert(gray == 0, "Grassland relief still replaces ground with gray terrain")
	assert(raised > 0, "The map was flattened instead of separating ground and relief")
	assert(rocks.get("StampCount") > 0 and rocks.get("StampCount") < raised, "Expected sparse rock objects over raised ground")
	var trees: Node = scene.get_node("Preview/Features")
	var tree_bounds: Array = trees.call("GetStampBounds")
	var rock_bounds: Array = rocks.call("GetStampBounds")
	assert(not tree_bounds.is_empty())
	var smallest_tree := INF
	var largest_rock := 0.0
	for bounds: Rect2 in tree_bounds:
		smallest_tree = minf(smallest_tree, maxf(bounds.size.x, bounds.size.y))
	for bounds: Rect2 in rock_bounds:
		largest_rock = maxf(largest_rock, maxf(bounds.size.x, bounds.size.y))
	assert(largest_rock < smallest_tree * 0.65, "Rock props dominate the tree scale")
	assert(trees.get("SpritesPerTile") == 1 and trees.get("ForestExtraSprites") == 1, "Trees were shrunk into dense miniature icons")
	print("[terrain-ground-cover] tree_min_px=", smallest_tree, " rock_max_px=", largest_rock)
	var preview: Node2D = scene.get_node("Preview")
	var old_scale := preview.scale
	preview.scale = Vector2.ONE * 2.0
	assert(trees.call("GetStampBounds") == tree_bounds and rocks.call("GetStampBounds") == rock_bounds, "Camera zoom changed world-space prop scale")
	preview.scale = old_scale
	print("[terrain-ground-cover] land=", land, " raised=", raised, " gray_ground=", gray, " rock_objects=", rocks.get("StampCount"))
	var snapshot := var_to_bytes(cells.call("GetCells"))
	var original_count: int = rocks.get("StampCount")
	rocks.call("Rebuild")
	assert(rocks.get("StampCount") == original_count, "Rock scatter is not deterministic")
	rocks.set("HillsCoverage", 0.0)
	rocks.set("MountainsCoverage", 0.0)
	rocks.call("Rebuild")
	assert(rocks.get("StampCount") == 0, "Zero rock coverage was ignored")
	rocks.set("HillsCoverage", 0.35)
	rocks.set("MountainsCoverage", 0.55)
	rocks.call("Rebuild")
	assert(rocks.get("StampCount") == original_count)
	assert(var_to_bytes(cells.call("GetCells")) == snapshot, "Rock rendering rewrote the live map")
	for slot in ["HillsTextures", "MountainsTextures"]:
		var textures: Array = rocks.get(slot)
		assert(textures.size() == 2)
		for texture in textures:
			assert(texture is Texture2D and texture.get_image().has_mipmaps())
			assert(texture.resource_path.begins_with("res://addons/beep_game_builder_cs/textures/rocks/Rock"))
	scene.free()
	print("[terrain-ground-cover] OK")
	quit()
