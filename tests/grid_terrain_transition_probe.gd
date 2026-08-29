extends SceneTree

const CELL_DATA_SCRIPT := preload("res://addons/beep_game_builder_cs/ecs/terrain/GridCellDataComponent.cs")
const TRANSITION_SCRIPT := preload("res://addons/beep_game_builder_cs/ecs/terrain/GridTerrainTransitionLayerComponent.cs")

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var cells := CELL_DATA_SCRIPT.new()
	cells.name = "Cells"
	cells.set("DefaultTerrainKind", "grass")
	root.add_child(cells)
	await process_frame

	cells.call("SetTerrainKind", Vector2i(0, 0), "water")
	cells.call("SetTerrainKind", Vector2i(1, 0), "water")
	cells.call("SetTerrainKind", Vector2i(0, 1), "water")
	cells.call("SetTerrainKind", Vector2i(1, 1), "water")

	var transition := TRANSITION_SCRIPT.new()
	transition.name = "WaterTransitions"
	transition.set("CellDataPath", NodePath("../Cells"))
	transition.set("TransitionTerrainKind", "water")
	root.add_child(transition)
	await process_frame

	if int(transition.call("DualGridMaskAt", Vector2i(1, 1))) != 15:
		_fail("Water interior should produce mask 15.")
	if int(transition.call("DualGridMaskAt", Vector2i(0, 0))) != 8:
		_fail("Single lower-right water quadrant should produce mask 8.")
	if int(transition.call("DualGridMaskAt", Vector2i(2, 2))) != 1:
		_fail("Single upper-left water quadrant should produce mask 1.")
	if int(transition.call("DualGridMaskAt", Vector2i(4, 4))) != 0:
		_fail("Unrelated grass area should produce mask 0.")
	if transition.call("AtlasCoordinatesForMask", 0) != Vector2i(0, 3):
		_fail("Canonical 15-piece atlas must use cell 12 for the empty mask.")
	if transition.call("AtlasCoordinatesForMask", 15) != Vector2i(2, 1):
		_fail("Canonical 15-piece atlas must use cell 6 for the solid interior mask.")
	if transition.call("AtlasCoordinatesForMask", 8) != Vector2i(1, 3):
		_fail("Canonical 15-piece atlas lower-right corner mapping changed.")

	cells.call("SetTerrainKind", Vector2i(3, 3), "deep_water")
	if int(transition.call("DualGridMaskAt", Vector2i(4, 4))) != 1:
		_fail("Deep water must participate in the water terrain layer.")

	root.remove_child(transition)
	transition.free()
	root.remove_child(cells)
	cells.free()
	print("[grid-terrain-transition] OK: dual-grid masks stay local to terrain boundaries.")
	quit(0)

func _fail(message: String) -> void:
	push_error("[grid-terrain-transition] " + message)
	quit(1)
