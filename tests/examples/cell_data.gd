extends SceneTree

# A cell must be able to say what it is, through Godot's own tile data.
#
# The point of this is that a game asks the MAP, not the generator. The
# generator is a build-time thing; a saved scene has tile layers and nothing
# else, so if the answers do not live in tile data they do not survive the save.
#
# It also has to be independent of which view is drawn. The tile view spreads its
# ground over fourteen biome layers, the isometric view stacks its own, and the
# painted view has no terrain tiles at all - so an answer read off the drawing
# layers would change, or vanish, when the player switched projection.

var failures: Array[String] = []

func check(ok: bool, message: String) -> void:
	if ok:
		print("  ok    %s" % message)
	else:
		print("  FAIL  %s" % message)
		failures.append(message)

func _initialize() -> void:
	var root_node = load("res://addons/beep_game_builder_cs/templates/scenes/terrain/terrain_generator_lab.tscn").instantiate()
	root_node.get_node("Preview/CellData").MaterializeTileLayers = true
	get_root().add_child(root_node)
	await process_frame
	await process_frame
	var deadline := Time.get_ticks_msec() + 30000
	while root_node.get_node("World").IsGenerating and Time.get_ticks_msec() < deadline: await process_frame

	var preview = root_node.find_child("Preview", true, false)
	var gen = preview.find_child("TerrainGenerator", true, false)
	var cells = preview.find_child("CellData", true, false)

	check(cells != null, "the world carries a cell data component")
	if cells == null or gen == null:
		print("\nRESULT: 1 FAILED")
		quit(1)
		return

	var size: Vector2i = gen.BoundsSize

	# --- terrain, against the generator that produced it ---------------------
	var checked := 0
	var wrong := 0
	var kinds := {}
	for y in range(size.y):
		for x in range(size.x):
			var c := Vector2i(x, y)
			var want: String = gen.TerrainKindAt(c)
			if want.is_empty():
				continue
			var got: String = cells.GeneratedTerrainAt(c)
			kinds[want] = kinds.get(want, 0) + 1
			checked += 1
			if got != want:
				wrong += 1
				if wrong <= 3:
					print("      %s: tile data says '%s', generator says '%s'" % [c, got, want])

	check(checked > 1000, "%d cells carry terrain data" % checked)
	check(wrong == 0, "every cell's terrain matches the generator (%d wrong)" % wrong)
	check(kinds.size() >= 3, "the map has %d distinct terrains to tell apart" % kinds.size())

	# --- water and passability are DERIVED, so they must agree ---------------
	var water_wrong := 0
	for y in range(size.y):
		for x in range(size.x):
			var c := Vector2i(x, y)
			var kind: String = gen.TerrainKindAt(c)
			if kind.is_empty():
				continue
			var expect_water: bool = kind in ["deep_water", "shallow_water", "water"]
			if cells.IsWaterAt(c) != expect_water:
				water_wrong += 1
	check(water_wrong == 0, "is_water agrees with the terrain kind (%d wrong)" % water_wrong)

	# --- resources: the headline ask -----------------------------------------
	var found := 0
	var resource_wrong := 0
	for y in range(size.y):
		for x in range(size.x):
			var c := Vector2i(x, y)
			var want: String = gen.ResourceAt(c)
			var got: String = cells.ResourceAt(c)
			if not want.is_empty():
				found += 1
				if got != want:
					resource_wrong += 1
					if resource_wrong <= 3:
						print("      %s: tile data says '%s', generator says '%s'" % [c, got, want])
			elif not got.is_empty():
				resource_wrong += 1

	check(found > 0, "the map placed %d resources" % found)
	check(resource_wrong == 0, "every resource cell reports its resource (%d wrong)" % resource_wrong)

	# Metadata must not create a second physical/navigation world.
	var data_layer = cells.find_child("TerrainData", true, false)
	check(data_layer != null, "explicit native mode exposes terrain TileData")
	if data_layer != null:
		for layer in cells.get_children():
			check(not layer.collision_enabled and not layer.navigation_enabled,
				"%s is physically inert" % layer.name)
			check(layer.tile_set.get_physics_layers_count() == 0 and layer.tile_set.get_navigation_layers_count() == 0,
				"%s has no physics/navigation geometry" % layer.name)

	# --- and it survives a switch of view ------------------------------------
	var picker = root_node.find_child("View", true, false)
	if picker != null:
		picker.selected = 2          # isometric: no flat terrain tiles at all
		picker.item_selected.emit(2)
		await process_frame

		var still := 0
		for y in range(0, size.y, 4):
			for x in range(0, size.x, 4):
				var c := Vector2i(x, y)
				if cells.GeneratedTerrainAt(c) == gen.TerrainKindAt(c):
					still += 1
		check(still > 100, "cell data still answers in the isometric view (%d sampled)" % still)

	print("\nRESULT: ", "all checks passed" if failures.is_empty() else "%d FAILED" % failures.size())
	quit(1 if failures.size() > 0 else 0)
