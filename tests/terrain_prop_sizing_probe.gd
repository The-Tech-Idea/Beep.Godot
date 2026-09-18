extends "res://tests/terrain_lab_build.gd"

# How big a prop is drawn, in every view, against two rules that TerrainPropSizing owns together:
#
#   1. A prop covers the size its category asks for, in CELLS - trees 1.75-2.25, bushes 0.35-0.65,
#      rocks 0.25-0.65 - so one resource edit resizes every renderer and every projection.
#   2. It is NEVER drawn larger than the art it comes from. Magnifying a sprite past its own
#      resolution cannot add detail, only blur, which is what every 2D strategy engine avoids by
#      authoring sprites at the size they are drawn at and only ever scaling down.
#
# The two meet where the art is too small for the category: then the art's own pixels win. The shipped
# cartoon sheets are exactly that case - a tree frame holds 39x69 to 60x124 visible pixels against the
# 112-144 the Trees range asks for - so this probe measures the cap on the real art, and measures the
# category range against forest_trees.png, whose 314-pixel frames are larger than any size asked of them.

const TREE_KINDS := ["woods", "forest", "jungle"]
const DETAILED_TREES := "res://addons/beep_game_builder_cs/textures/plants/forest_trees.png"

func _initialize() -> void:
	call_deferred("run")

# The largest visible frame in a sheet: the most pixels any one stamp from it can be drawn at.
func art_limit(rules: Resource, path: String, columns: int, rows: int) -> Vector2:
	var texture: Texture2D = load(path)
	if texture == null:
		return Vector2.ZERO
	var largest := Vector2.ZERO
	for index in columns * rows:
		largest = largest.max(Rect2(rules.call("VisibleRegion", texture, columns, rows, index)).size)
	return largest

func bounds_of(view: Node, kinds: Array) -> Array:
	if kinds.is_empty():
		return view.call("GetStampBounds")
	var bounds: Array = []
	for kind in kinds:
		bounds.append_array(view.call("GetStampBoundsOfKind", kind))
	return bounds

# Rule 1, on art large enough to satisfy it: every stamp lands inside the category's range.
func check_range(view: Node, cell_edge: float, low: float, high: float, kinds: Array = []) -> void:
	var bounds := bounds_of(view, kinds)
	assert(not bounds.is_empty(), "No %s props to measure in %s" % [kinds, view.name])
	for rect: Rect2 in bounds:
		var extent := maxf(rect.size.x, rect.size.y) / cell_edge
		assert(extent >= low - 0.001 and extent <= high + 0.001,
			"%s %s: %.3f cells is outside %.3f..%.3f" % [view.name, kinds, extent, low, high])
	print("[terrain-prop-sizing] ", view.name, " ", kinds, ": ", bounds.size(), " props inside ", low, "..", high, " cells")

# Rule 2, on the shipped art: no stamp is larger than the largest frame its sheet holds, and the
# largest stamp actually reaches that art, so the rule cannot pass by drawing everything tiny.
func check_never_magnified(view: Node, limit: Vector2, high_cells: float, cell_edge: float, kinds: Array = []) -> void:
	var bounds := bounds_of(view, kinds)
	assert(not bounds.is_empty(), "No %s props to measure in %s" % [kinds, view.name])
	assert(limit.x > 0.0 and limit.y > 0.0, "%s %s: the sheet reports no visible art" % [view.name, kinds])
	var largest := Vector2.ZERO
	for rect: Rect2 in bounds:
		assert(rect.size.x <= limit.x + 0.5 and rect.size.y <= limit.y + 0.5,
			"%s %s: a %.0fx%.0f stamp is larger than its %.0fx%.0f art" % [view.name, kinds, rect.size.x, rect.size.y, limit.x, limit.y])
		assert(maxf(rect.size.x, rect.size.y) / cell_edge <= high_cells + 0.001,
			"%s %s: %.3f cells is over the category's %.3f" % [view.name, kinds, maxf(rect.size.x, rect.size.y) / cell_edge, high_cells])
		largest = largest.max(rect.size)
	assert(largest.y >= limit.y * 0.9 - 0.5,
		"%s %s: the largest stamp is %.0fx%.0f, far under the %.0fx%.0f the art allows" % [view.name, kinds, largest.x, largest.y, limit.x, limit.y])
	print("[terrain-prop-sizing] ", view.name, " ", kinds, ": ", bounds.size(), " props, largest ", largest, " against art ", limit)

func run() -> void:
	var rules: Resource = load("res://addons/beep_game_builder_cs/textures/terrain/terrain_prop_sizing.tres")
	for canvas in [64, 256]:
		var image := Image.create(canvas, canvas, false, Image.FORMAT_RGBA8)
		image.fill(Color.TRANSPARENT)
		image.fill_rect(Rect2i(10, 20, 20, 40), Color.WHITE)
		var texture := ImageTexture.create_from_image(image)
		assert(rules.call("VisibleRegion", texture, 1, 1, 0) == Rect2(10, 20, 20, 40), "Transparent padding affects visible size")
	for kind in ["woods", "oasis", "marsh", "bush", "small_rock", "large_rock"]:
		assert(is_finite(rules.call("SizeInCells", kind, NAN)))
		assert(rules.call("SizeInCells", kind, -100.0) > 0)
	# The two rules, on the resource alone: art with pixels to spare takes the category size; art with
	# too few takes its own. 64 pixels a cell, trees at 1.75-2.25 cells.
	var roomy: Vector2 = rules.call("DrawnPixels", Vector2(314, 314), 64.0, "woods", 1.0)
	assert(absf(maxf(roomy.x, roomy.y) / 64.0 - rules.call("SizeInCells", "woods", 1.0)) < 0.001,
		"art with pixels to spare did not take the category's size: %s" % roomy)
	var tight: Vector2 = rules.call("DrawnPixels", Vector2(58, 69), 64.0, "woods", 1.0)
	assert(tight == Vector2(58, 69), "art smaller than the category was magnified to %s" % tight)

	var scene: Node = load("res://addons/beep_game_builder_cs/templates/scenes/terrain/terrain_generator_lab.tscn").instantiate()
	var world: Node = scene.get_node("World")
	world.set("MapSize", 0)
	root.add_child(scene)
	var build := await await_lab_build(world)
	assert(build.success, "the lab's first build did not succeed: %s" % build.message)
	var cells: Node = scene.get_node("Preview/Cells")
	var before := var_to_bytes(cells.call("GetCells"))
	var flat: Node = scene.get_node("Preview/Features")
	var iso: Node = scene.get_node("Preview/IsoFeatures")
	var rocks: Node = scene.get_node("Preview/RockObjects")
	# Each view against ITS OWN sheet: the isometric view binds isometric tree art, which is larger than
	# the flat view's, so one limit for both would measure the wrong thing.
	var shipped_trees := art_limit(rules, flat.get("WoodsSheetPath"), flat.get("WoodsColumns"), flat.get("WoodsRows"))
	var shipped_bushes := art_limit(rules, flat.get("BushesSheetPath"), flat.get("BushesColumns"), flat.get("BushesRows"))
	var shipped_iso_trees := art_limit(rules, iso.get("WoodsSheetPath"), iso.get("WoodsColumns"), iso.get("WoodsRows"))
	print("[terrain-prop-sizing] art: flat trees %s, bushes %s; isometric trees %s" % [shipped_trees, shipped_bushes, shipped_iso_trees])

	for projection in [0, 1, 2, 3]:
		world.set("Projection", projection)
		world.call("Redraw")
		await process_frame
		assert(flat.get("PropSizing") == rules and iso.get("PropSizing") == rules)
		if projection < 2:
			check_never_magnified(flat, shipped_trees, 2.25, 64.0, TREE_KINDS)
			check_never_magnified(flat, shipped_bushes, 0.65, 64.0, ["bush"])
			check_range(rocks, 64.0, 0.0, 0.65)
		elif projection == 2:
			var corners: PackedVector2Array = scene.get_node("Preview/Iso").call("SurfaceCorners", Vector2i(16, 8))
			check_never_magnified(iso, shipped_iso_trees, 2.25, corners[0].distance_to(corners[1]))
		assert(var_to_bytes(cells.call("GetCells")) == before)

	# Art with pixels to spare takes the category size instead, in the flat view and the isometric one.
	for view in [flat, iso]:
		view.set("WoodsSheetPath", DETAILED_TREES)
		view.set("WoodsColumns", 4)
		view.set("WoodsRows", 4)
	for projection in [0, 2]:
		world.set("Projection", projection)
		world.call("Redraw")
		await process_frame
		if projection == 0:
			check_range(flat, 64.0, 1.75, 2.25, TREE_KINDS)
		else:
			var corners: PackedVector2Array = scene.get_node("Preview/Iso").call("SurfaceCorners", Vector2i(16, 8))
			check_range(iso, corners[0].distance_to(corners[1]), 1.75, 2.25)

	# One resource edit changes every renderer, independent of presentation.
	var original: Vector2 = rules.get("Trees")
	rules.set("Trees", Vector2(1.25, 1.25))
	for projection in [0, 2]:
		world.set("Projection", projection)
		world.call("Redraw")
		await process_frame
		if projection == 0:
			check_range(flat, 64.0, 1.25, 1.25, TREE_KINDS)
		else:
			var corners: PackedVector2Array = scene.get_node("Preview/Iso").call("SurfaceCorners", Vector2i(16, 8))
			check_range(iso, corners[0].distance_to(corners[1]), 1.25, 1.25)
	rules.set("Trees", original)
	scene.free()
	print("[terrain-prop-sizing] OK")
	quit()
