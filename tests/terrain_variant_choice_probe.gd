extends SceneTree

# One map, one variant per cell, in both isometric views.
#
# A terrain with interchangeable frames - the block view's TerrainVariants, the
# autotile view's alternative tiles - picks one per cell by a stable hash of the cell,
# so a rebuild does not shimmer. The two views used two different hashes: the block
# view a private copy of TerrainGeometry's mix under a constant salt, the autotile view
# a multiply-XOR of the absolute coordinate. So the same map, drawn as blocks and drawn
# as tiles, showed different variants on every cell. Both now read
# TerrainGeometry.HashInt under TerrainGeometry.VariantSalt on the window-local cell.
#
# Measured through the layers each view paints: with N variants at atlas x = 0..N-1 in
# both, a cell's atlas x is its variant index, and the two views must agree cell for
# cell - at the origin and with the window shifted.

const BASE := "res://addons/beep_game_builder_cs/"
const Fixture := preload("res://tests/fixtures/iso_variant_tileset.gd")
const VARIANTS := 4
const SIZE := Vector2i(8, 8)

var failures: Array[String] = []

func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		push_error(message)

func _initialize() -> void:
	call_deferred("run")

func variant_grid(layer: TileMapLayer, origin: Vector2i, label: String) -> Array[int]:
	var indices: Array[int] = []
	for y in SIZE.y:
		for x in SIZE.x:
			var cell := origin + Vector2i(x, y)
			check(layer.get_cell_source_id(cell) != -1, "%s painted nothing at %s" % [label, cell])
			indices.append(layer.get_cell_atlas_coords(cell).x)
	return indices

func compare(host: Node2D, origin: Vector2i) -> void:
	var cells: Node = load(BASE + "ecs/grid/GridCellDataComponent.cs").new()
	cells.name = "Cells"
	host.add_child(cells)
	cells.call("FillTerrain", Rect2i(origin, SIZE), "grass")

	var blocks: Node2D = load(BASE + "ecs/terrain/TerrainIsometricRendererComponent.cs").new()
	blocks.name = "Blocks"
	blocks.set("RefreshOnReady", false)
	blocks.set("CellDataPath", NodePath("../Cells"))
	blocks.set("BoundsOrigin", origin)
	blocks.set("BoundsSize", SIZE)
	blocks.set("BlockSheetPath", BASE + "textures/iso/kenney_voxel_blocks.png")
	blocks.set("SheetColumns", 8)
	blocks.set("SheetRows", 7)
	# Frames 0..3 of an 8-column sheet sit at atlas (0,0)..(3,0): x is the index.
	blocks.set("TerrainVariants", PackedStringArray(["grass=0,1,2,3"]))
	host.add_child(blocks)
	blocks.call("Rebuild")

	var tiles: Node2D = load(BASE + "ecs/terrain/TerrainIsometricAutotileRendererComponent.cs").new()
	tiles.name = "Tiles"
	tiles.set("RefreshOnReady", false)
	tiles.set("CellDataPath", NodePath("../Cells"))
	tiles.set("BoundsOrigin", origin)
	tiles.set("BoundsSize", SIZE)
	tiles.set("Tiles", Fixture.build(VARIANTS))
	tiles.set("TerrainSet", 0)
	tiles.set("UseTerrainConnections", false)
	tiles.set("TerrainBindings", PackedStringArray(["grass=0"]))
	host.add_child(tiles)
	tiles.call("Rebuild")

	var ground: TileMapLayer = blocks.get_node_or_null("IsoLevel1")
	var painted: TileMapLayer = tiles.get_node_or_null("IsoTerrain")
	check(ground != null, "the block view built no ground layer")
	check(painted != null, "the autotile view built no terrain layer")
	if ground != null and painted != null:
		var label := "origin %s" % origin
		var a := variant_grid(ground, origin, "block view at " + label)
		var b := variant_grid(painted, origin, "autotile view at " + label)
		var distinct := {}
		for index in a:
			distinct[index] = true
		# With 64 cells and 4 variants a single variant everywhere would make every
		# agreement vacuous; it is also astronomically unlikely from a real hash.
		check(distinct.size() >= 2, "the block view drew one variant everywhere at %s; nothing to compare" % label)
		var disagreements := 0
		for i in a.size():
			if a[i] != b[i]:
				disagreements += 1
		check(disagreements == 0,
			"the two isometric views disagree about the variant on %d of %d cells at %s - they are not hashing the same cell the same way" % [disagreements, a.size(), label])
		# And the choice is stable: painting again gives the same map.
		tiles.call("Rebuild")
		check(variant_grid(painted, origin, "autotile view rebuilt") == b, "the autotile view's variants changed on an identical rebuild")

	for child in host.get_children():
		child.free()

func run() -> void:
	var host := Node2D.new()
	root.add_child(host)
	compare(host, Vector2i.ZERO)
	# A shifted window: both views hash the window-local cell, so they still agree.
	compare(host, Vector2i(5, 3))
	host.free()
	if failures.is_empty():
		print("[terrain-variant-choice] OK")
		quit(0)
	else:
		print("[terrain-variant-choice] FAILED")
		quit(1)
