extends SceneTree

# One map, one way of cutting its prop sheets, in both projections.
#
# TerrainFeatureSheets owns the four feature sheets and the grid each is cut on.
# Before it existed the two feature renderers each carried their own copy of the
# loader, and the copies disagreed in a way that was a defect rather than a
# divergence: the flat view cut each sheet on its own columns and rows, while the
# isometric view cut EVERY sheet on WoodsColumns/WoodsRows - and exposed no other
# layout to get it right with. A marsh sheet authored on a different grid from the
# trees was therefore sliced correctly in one view and wrongly in the other, off the
# same map.
#
# This measures the slicing through the stamps each view draws. A frame's drawn
# rectangle keeps the frame's aspect ratio (both views scale uniformly to fit a
# tile), so a 256x64 sheet cut as 2x1 draws 2:1 stamps and cut as 4x1 draws 1:1
# stamps. Nothing here needs a display.

const BASE := "res://addons/beep_game_builder_cs/"
const OUTPUT := "res://tests/output/feature_sheets"

var failures: Array[String] = []

func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		push_error(message)

func _initialize() -> void:
	call_deferred("run")

# An opaque sheet, written to disk and handed over as an ABSOLUTE path: a PNG that
# was never imported has no .import beside it, so a res:// load of it fails, while
# TerrainTextures.Load reads an absolute path straight off disk (with mipmaps).
func sheet(file: String, width: int, height: int, colour: Color) -> String:
	var image := Image.create(width, height, false, Image.FORMAT_RGBA8)
	image.fill(colour)
	var path := ProjectSettings.globalize_path(OUTPUT + "/" + file)
	check(image.save_png(path) == OK, "could not write fixture sheet " + file)
	return path

# The aspect ratio every stamp in a view was drawn at. All stamps come from one
# feature kind here, so they must all agree; disagreement is its own failure.
func drawn_aspect(view: Node, label: String) -> float:
	var bounds: Array = view.call("GetStampBounds")
	check(not bounds.is_empty(), "%s drew no marsh stamps at all" % label)
	if bounds.is_empty():
		return NAN
	var first: Rect2 = bounds[0]
	var aspect := first.size.x / first.size.y
	for rect: Rect2 in bounds:
		check(is_equal_approx(rect.size.x / rect.size.y, aspect),
			"%s drew stamps at more than one aspect ratio; the sheet is being cut inconsistently" % label)
	return aspect

func build_views(host: Node2D, woods: String, marsh: String, marsh_columns: int, marsh_rows: int) -> Dictionary:
	# The isometric view needs the projection owner, and that owner needs block art
	# to build at all - the shipped sheet, even though this probe never draws a block.
	var iso: Node2D = load(BASE + "ecs/terrain/TerrainIsometricRendererComponent.cs").new()
	iso.name = "Iso"
	iso.set("RefreshOnReady", false)
	iso.set("CellDataPath", NodePath("../Cells"))
	iso.set("BoundsSize", Vector2i(8, 8))
	iso.set("BlockSheetPath", BASE + "textures/iso/kenney_voxel_blocks.png")
	iso.set("SheetColumns", 8)
	iso.set("SheetRows", 7)
	host.add_child(iso)
	iso.call("Rebuild")

	var views := {}
	for pair in [["Flat", "ecs/terrain/TerrainFeatureRendererComponent.cs"],
			["IsoFeatures", "ecs/terrain/TerrainIsometricFeatureRendererComponent.cs"]]:
		var view: Node2D = load(BASE + pair[1]).new()
		view.name = pair[0]
		view.set("RefreshOnReady", false)
		view.set("BoundsSize", Vector2i(8, 8))
		view.set("Seed", 7)
		view.set("SpritesPerTile", 1)
		view.set("ForestExtraSprites", 0)
		view.set("ScaleJitter", 0.0)
		# Woods on a 4x1 grid, marsh authored separately. The woods layout is what a
		# zero marsh layout inherits, so it must differ from the marsh sheet's own.
		view.set("WoodsSheetPath", woods)
		view.set("WoodsColumns", 4)
		view.set("WoodsRows", 1)
		view.set("MarshSheetPath", marsh)
		view.set("MarshColumns", marsh_columns)
		view.set("MarshRows", marsh_rows)
		if pair[0] == "Flat":
			view.set("CellDataPath", NodePath("../Cells"))
			view.set("TileSize", 64)
		else:
			view.set("IsometricRendererPath", NodePath("../Iso"))
		host.add_child(view)
		view.call("Rebuild")
		views[pair[0]] = view
	return views

func run() -> void:
	DirAccess.make_dir_recursive_absolute(OUTPUT)
	# Same pixel size, different grids: 4 square frames against 2 wide ones.
	var woods := sheet("woods_4x1.png", 256, 64, Color.GREEN)
	var marsh := sheet("marsh_2x1.png", 256, 64, Color.OLIVE)

	var host := Node2D.new()
	root.add_child(host)
	var cells: Node = load(BASE + "ecs/grid/GridCellDataComponent.cs").new()
	cells.name = "Cells"
	host.add_child(cells)
	cells.call("FillTerrain", Rect2i(0, 0, 8, 8), "swamp")
	# Only marsh is placed, so every stamp either view draws is a marsh frame and the
	# aspect ratio of the stamps is the aspect ratio of the frame the sheet was cut into.
	for y in 8:
		for x in 8:
			cells.call("SetMetadata", Vector2i(x, y), "terrain_feature", "marsh")

	# 1. A marsh layout of its own: 2x1 on a 256x64 sheet is a 2:1 frame, in BOTH views.
	var explicit := build_views(host, woods, marsh, 2, 1)
	var flat_aspect := drawn_aspect(explicit["Flat"], "the flat view")
	var iso_aspect := drawn_aspect(explicit["IsoFeatures"], "the isometric view")
	check(is_equal_approx(flat_aspect, 2.0),
		"the flat view cut the 2x1 marsh sheet at aspect %.2f, not 2.0" % flat_aspect)
	check(is_equal_approx(iso_aspect, 2.0),
		"the isometric view cut the 2x1 marsh sheet at aspect %.2f, not 2.0 - it is slicing marsh on the woods grid again" % iso_aspect)
	check(is_equal_approx(flat_aspect, iso_aspect),
		"the two views cut the same marsh sheet differently: flat %.2f, isometric %.2f" % [flat_aspect, iso_aspect])
	for view in explicit.values():
		view.free()
	host.get_node("Iso").free()

	# 2. Zero inherits the woods layout. That is what every isometric sheet used to get,
	#    and it is what keeps terrain_iso_demo.tscn's 8x1 marsh (cut on WoodsColumns = 8,
	#    with no marsh layout of its own) working untouched. Here woods is 4x1, so an
	#    inheriting marsh sheet is cut into four square frames: aspect 1.0.
	var inherited := build_views(host, woods, marsh, 0, 0)
	var inherited_aspect := drawn_aspect(inherited["IsoFeatures"], "the isometric view with a zero marsh layout")
	check(is_equal_approx(inherited_aspect, 1.0),
		"a zero marsh layout should inherit the 4x1 woods grid (aspect 1.0); the isometric view cut it at %.2f" % inherited_aspect)

	host.free()
	if failures.is_empty():
		print("[terrain-feature-sheets] OK")
		quit(0)
	else:
		print("[terrain-feature-sheets] FAILED")
		quit(1)
