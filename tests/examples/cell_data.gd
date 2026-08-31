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
	get_root().add_child(root_node)
	for i in range(60): await process_frame

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
			var got: String = cells.TerrainAt(c)
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

	# --- collision and navigation, per GROUND, not per policy ----------------
	#
	# The map must not decide whether water stops anyone. It states what the
	# ground is; each ground gets its own physics and navigation layer, and the
	# developer's collision mask decides what that means - a swimmer masks steep
	# only, a walker masks water and steep.
	#
	# So the property to hold is separation: every cell is solid and navigable on
	# its OWN ground's layer and on no other. If water leaked onto the land layer
	# a walker and a swimmer could not be told apart.
	const LAND := 0
	const WATER := 1
	const STEEP := 2

	var data_layer = cells.find_child("TerrainData", true, false)
	check(data_layer != null, "the cell data has a terrain layer carrying the body")
	if data_layer != null:
		var right := 0
		var missing_collision := 0
		var missing_nav := 0
		var leaked := 0
		for y in range(0, size.y, 3):
			for x in range(0, size.x, 3):
				var c := Vector2i(x, y)
				var kind: String = gen.TerrainKindAt(c)
				if kind.is_empty():
					continue
				var td = data_layer.get_cell_tile_data(c)
				if td == null:
					continue

				var ground := LAND
				if kind in ["deep_water", "shallow_water", "water"]:
					ground = WATER
				elif kind == "rock":
					ground = STEEP

				if td.get_collision_polygons_count(ground) == 0: missing_collision += 1
				elif td.get_navigation_polygon(ground) == null: missing_nav += 1
				else: right += 1

				# and nowhere else
				for other in [LAND, WATER, STEEP]:
					if other != ground and td.get_collision_polygons_count(other) > 0:
						leaked += 1

		check(right > 0 and missing_collision == 0,
			"every cell is solid on its own ground's layer (%d ok, %d missing)"
				% [right, missing_collision])
		check(missing_nav == 0,
			"every cell is navigable on its own ground's layer (%d missing)" % missing_nav)
		check(leaked == 0,
			"no cell is solid on another ground's layer (%d leaked)" % leaked)
		check(data_layer.collision_enabled and data_layer.navigation_enabled,
			"the layer actually serves its collision and navigation")

	# --- and it survives a switch of view ------------------------------------
	var picker = root_node.find_child("View", true, false)
	if picker != null:
		picker.selected = 2          # isometric: no flat terrain tiles at all
		root_node.Generate()
		for i in range(20): await process_frame

		var still := 0
		for y in range(0, size.y, 4):
			for x in range(0, size.x, 4):
				var c := Vector2i(x, y)
				if cells.TerrainAt(c) == gen.TerrainKindAt(c):
					still += 1
		check(still > 100, "cell data still answers in the isometric view (%d sampled)" % still)

	print("\nRESULT: ", "all checks passed" if failures.is_empty() else "%d FAILED" % failures.size())
	quit(1 if failures.size() > 0 else 0)
