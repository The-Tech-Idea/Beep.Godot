extends SceneTree

# Dual-grid masks must stay local to a terrain boundary.
#
# This used to author a 2x2 patch of water into GridCellDataComponent and check
# the masks around it. The transition layer reads the TERRAIN ENGINE now - one
# source for every view - so there is no authored copy to poke. It therefore
# tests the same logic against a REAL generated world, finding the cases in the
# map instead of building them: a mask over open water must be solid, a mask well
# inland must be empty, and the atlas mapping is pure and checked directly.

const CELL_DATA_SCRIPT := preload("res://addons/beep_game_builder_cs/ecs/terrain/GridCellDataComponent.cs")
const GENERATOR_SCRIPT := preload("res://addons/beep_game_builder_cs/ecs/terrain/TerrainGeneratorComponent.cs")
const TRANSITION_SCRIPT := preload("res://addons/beep_game_builder_cs/ecs/terrain/TerrainTransitionLayerComponent.cs")
const SIZE := Vector2i(64, 40)

var gen: Node

func is_water(cell: Vector2i) -> bool:
	var k: String = str(gen.call("TerrainKindAt", cell))
	return k == "deep_water" or k == "shallow_water" or k == "water"

# The four cells whose corners meet at this point, which is what the mask reads.
func quad_all(cell: Vector2i, want_water: bool) -> bool:
	for dy in [-1, 0]:
		for dx in [-1, 0]:
			var at := cell + Vector2i(dx, dy)
			if at.x < 0 or at.y < 0 or at.x >= SIZE.x or at.y >= SIZE.y:
				return false
			if is_water(at) != want_water:
				return false
	return true

func find_quad(want_water: bool) -> Vector2i:
	for y in range(1, SIZE.y):
		for x in range(1, SIZE.x):
			var cell := Vector2i(x, y)
			if quad_all(cell, want_water):
				return cell
	return Vector2i(-1, -1)

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var cells := CELL_DATA_SCRIPT.new()
	cells.name = "Cells"
	root.add_child(cells)

	gen = GENERATOR_SCRIPT.new()
	gen.name = "Generator"
	gen.set("CellDataPath", NodePath("../Cells"))
	gen.set("BoundsSize", SIZE)
	gen.set("Seed", 8675309)
	root.add_child(gen)
	await process_frame
	gen.call("GenerateTerrain")

	var transition := TRANSITION_SCRIPT.new()
	transition.name = "WaterTransitions"
	transition.set("TerrainGeneratorPath", NodePath("../Generator"))
	transition.set("TransitionTerrainKind", "water")
	# This probe drives AtlasCoordinatesForMask/DualGridMaskAt directly and
	# never wires a DisplayLayerPath - it does not want a real Rebuild. Without
	# this, RefreshOnReady's default fires one anyway, and it now correctly
	# warns that it has no display layer to draw into.
	transition.set("RefreshOnReady", false)
	root.add_child(transition)
	await process_frame

	# The atlas mapping is pure - no terrain needed, so it is checked exactly.
	if transition.call("AtlasCoordinatesForMask", 0) != Vector2i(0, 3):
		_fail("Canonical 15-piece atlas must use cell 12 for the empty mask.")
	if transition.call("AtlasCoordinatesForMask", 15) != Vector2i(2, 1):
		_fail("Canonical 15-piece atlas must use cell 6 for the solid interior mask.")
	if transition.call("AtlasCoordinatesForMask", 8) != Vector2i(1, 3):
		_fail("Canonical 15-piece atlas lower-right corner mapping changed.")

	var open_water := find_quad(true)
	if open_water.x < 0:
		_fail("The generated world had no open water to measure a solid mask on.")
		return
	if int(transition.call("DualGridMaskAt", open_water)) != 15:
		_fail("Open water at %s should produce mask 15, got %d."
			% [str(open_water), int(transition.call("DualGridMaskAt", open_water))])

	var inland := find_quad(false)
	if inland.x < 0:
		_fail("The generated world had no dry land to measure an empty mask on.")
		return
	if int(transition.call("DualGridMaskAt", inland)) != 0:
		_fail("Dry land at %s should produce mask 0, got %d."
			% [str(inland), int(transition.call("DualGridMaskAt", inland))])

	# A boundary must be neither: the mask is what makes a coast a coast.
	var partial := 0
	for y in range(1, SIZE.y):
		for x in range(1, SIZE.x):
			var m: int = int(transition.call("DualGridMaskAt", Vector2i(x, y)))
			if m > 0 and m < 15:
				partial += 1
	if partial == 0:
		_fail("No partial masks anywhere: the coastline produced no transition tiles.")

	print("[grid-terrain-transition] OK: dual-grid masks stay local to terrain boundaries (%d transition cells)." % partial)
	quit(0)

func _fail(message: String) -> void:
	push_error("[grid-terrain-transition] " + message)
	quit(1)
