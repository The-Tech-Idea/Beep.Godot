extends SceneTree

# Bushes among the trees (the understory), on live cells authored so every rule has a tile to fail on:
# columns of woods, forest, jungle, marsh, oasis and bare grass, crossed by a row of water, drawn by
# the flat feature renderer with the cartoon tree and bush sheets.
#   - bushes stand on woods AND forest tiles only - never jungle, marsh, oasis, bare ground or water -
#     at most BushesPerWoodsTile per tile, each inside the Bushes size range;
#   - adding bushes moves no tree: every tree stamp is where a renderer without bushes put it;
#   - bushes take the clearings: their mean distance to the nearest trunk of their own tile stays
#     above CLEARING_CELLS, which bushes scattered without regard to the trees fall below;
#   - BushesPerWoodsTile 0 draws none, and a MapArt's own Bushes are drawn instead of the sheet.

const BASE := "res://addons/beep_game_builder_cs/"
const SIZE := Vector2i(24, 13)
const TILE := 64.0
const PER_TILE := 2
const ANCHOR := Vector2(0.5, 0.97)
const WATER_ROW := 6
const TREES := BASE + "textures/map_art/cartoon_trees.png"
const BUSHES := BASE + "textures/map_art/cartoon_bushes.png"
# Four columns of each, left to right: feature, and the ground it grows on.
const BANDS := [["woods", "grass"], ["forest", "grass"], ["jungle", "jungle"], ["marsh", "swamp"], ["oasis", "desert"], ["", "grass"]]
# Measured on this fixture: bushes placed after the trees and kept from them average 0.578 cells to
# the nearest trunk; the same bushes scattered as if the trees were not there average 0.418.
const CLEARING_CELLS := 0.5

var failures: Array[String] = []

func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		push_error(message)

func _initialize() -> void:
	call_deferred("run")

func renderer(bushes: String) -> Node:
	var view: Node = load(BASE + "ecs/terrain/TerrainFeatureRendererComponent.cs").new()
	view.set("RefreshOnReady", false)
	view.set("BoundsSize", SIZE)
	view.set("TileSize", int(TILE))
	view.set("WoodsSheetPath", TREES)
	view.set("WoodsColumns", 4)
	view.set("WoodsRows", 2)
	view.set("BushesSheetPath", bushes)
	view.set("BushesColumns", 4)
	view.set("BushesRows", 2)
	view.set("BushesPerWoodsTile", PER_TILE)
	view.set("SpriteAnchor", ANCHOR)
	root.get_node("Host").add_child(view)
	view.set("CellDataPath", view.get_path_to(root.get_node("Host/Cells")))
	view.call("Rebuild")
	return view

# Ground anchors, in cells: the point SpriteAnchor pins to the ground.
func anchors_of(view: Node, kinds: Array) -> Array[Vector2]:
	var points: Array[Vector2] = []
	for kind in kinds:
		for rect: Rect2 in view.call("GetStampBoundsOfKind", kind):
			points.append((rect.position + rect.size * ANCHOR) / TILE)
	return points

func cell_of(point: Vector2) -> Vector2i:
	return Vector2i(floori(point.x), floori(point.y))

func run() -> void:
	var host := Node2D.new()
	host.name = "Host"
	root.add_child(host)
	var cells: Node = load(BASE + "ecs/grid/GridCellDataComponent.cs").new()
	cells.name = "Cells"
	host.add_child(cells)
	var feature_at := {}
	for y in SIZE.y:
		for x in SIZE.x:
			var cell := Vector2i(x, y)
			if y == WATER_ROW:
				cells.call("SetTerrainKind", cell, "shallow_water")
				feature_at[cell] = "water"
				continue
			var band: Array = BANDS[x / 4]
			cells.call("SetTerrainKind", cell, band[1])
			if band[0] != "":
				cells.call("SetMetadata", cell, "terrain_feature", band[0])
			feature_at[cell] = band[0]
	var sizing: Resource = load(BASE + "textures/terrain/terrain_prop_sizing.tres")
	var bush_range: Vector2 = sizing.get("Bushes")

	var with_bushes := renderer(BUSHES)
	var without_bushes := renderer("")

	var trees_with := anchors_of(with_bushes, ["woods", "forest", "jungle", "oasis"])
	var trees_without := anchors_of(without_bushes, ["woods", "forest", "jungle", "oasis"])
	trees_with.sort()
	trees_without.sort()
	check(trees_with.size() > 0, "the fixture drew no trees")
	check(trees_with == trees_without, "adding bushes moved or dropped trees (%d with bushes, %d without)" % [trees_with.size(), trees_without.size()])
	check(without_bushes.call("GetStampBoundsOfKind", "bush").is_empty(), "a renderer with no bush art drew bushes")

	var trees_by_cell := {}
	for point in trees_with:
		trees_by_cell[cell_of(point)] = trees_by_cell.get(cell_of(point), []) + [point]
	var per_cell := {}
	var by_feature := {}
	var clearance_total := 0.0
	var clearance_count := 0
	for rect: Rect2 in with_bushes.call("GetStampBoundsOfKind", "bush"):
		var point: Vector2 = (rect.position + rect.size * ANCHOR) / TILE
		var cell := cell_of(point)
		var feature: String = feature_at.get(cell, "outside")
		by_feature[feature] = by_feature.get(feature, 0) + 1
		per_cell[cell] = per_cell.get(cell, 0) + 1
		check(feature == "woods" or feature == "forest", "a bush stands on a '%s' tile at %s" % [feature, cell])
		var extent := maxf(rect.size.x, rect.size.y) / TILE
		check(extent >= bush_range.x - 0.001 and extent <= bush_range.y + 0.001,
			"a bush is %.3f cells, outside %.3f..%.3f" % [extent, bush_range.x, bush_range.y])
		var nearest := INF
		for tree: Vector2 in trees_by_cell.get(cell, []):
			nearest = minf(nearest, tree.distance_to(point))
		if nearest < INF:
			clearance_total += nearest
			clearance_count += 1
	for cell in per_cell:
		check(per_cell[cell] <= PER_TILE, "tile %s holds %d bushes, more than %d" % [cell, per_cell[cell], PER_TILE])
	check(by_feature.get("woods", 0) > 0 and by_feature.get("forest", 0) > 0,
		"bushes must stand in both woods and forest: %s" % by_feature)
	check(clearance_count > 0, "no bush shares a tile with a tree, so clearance was not measured")
	var clearance := clearance_total / maxi(1, clearance_count)
	check(clearance > CLEARING_CELLS, "bushes crowd the trunks: mean distance to the nearest tree %.3f cells" % clearance)
	print("[terrain-understory] %d trees, bushes by tile %s, mean clearance %.3f cells" % [trees_with.size(), by_feature, clearance])

	# Zero per tile draws none, whatever art is assigned.
	with_bushes.set("BushesPerWoodsTile", 0)
	with_bushes.call("Rebuild")
	check(with_bushes.call("GetStampBoundsOfKind", "bush").is_empty(), "BushesPerWoodsTile 0 still drew bushes")

	# A style's own bushes win over the sheet: a 10x40 sprite draws four times taller than wide.
	var image := Image.create(10, 40, false, Image.FORMAT_RGBA8)
	image.fill(Color.WHITE)
	var art: Resource = load(BASE + "ecs/terrain/TerrainMapArt.cs").new()
	art.get("Bushes").append(ImageTexture.create_from_image(image))
	with_bushes.set("MapArt", art)
	with_bushes.set("BushesPerWoodsTile", PER_TILE)
	with_bushes.call("Rebuild")
	var styled: Array = with_bushes.call("GetStampBoundsOfKind", "bush")
	check(not styled.is_empty(), "a MapArt with Bushes drew no bushes")
	for rect: Rect2 in styled:
		check(is_equal_approx(rect.size.y / rect.size.x, 4.0), "a styled bush is %s, not the MapArt's 1:4 sprite" % rect.size)

	host.free()
	print("[terrain-understory] OK" if failures.is_empty() else "[terrain-understory] FAILED")
	quit(0 if failures.is_empty() else 1)
