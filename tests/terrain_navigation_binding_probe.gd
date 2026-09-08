extends SceneTree

const BASE := "res://addons/beep_game_builder_cs/ecs/"

func make(kind: String, parent: Node, label: String, properties: Dictionary = {}) -> Node:
	var node: Node = load(BASE + "grid/" + kind + ".cs").new()
	node.name = label
	for key in properties:
		node.set(key, properties[key])
	parent.add_child(node)
	return node

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	var host := Node2D.new()
	root.add_child(host)
	var grid := make("GridProjectionComponent", host, "Grid", {
		"DrawGrid": false, "TrackMouseCell": false})
	var a := make("GridPlacementComponent", host, "A")
	var b := make("GridPlacementComponent", host, "B")
	var cells := make("GridCellDataComponent", host, "Cells")
	var replacement := make("GridCellDataComponent", host, "Replacement")
	var roads_a := make("GridRoadComponent", host, "RoadsA", {"DrawRoads": false})
	make("GridRoadComponent", host, "RoadsB", {"DrawRoads": false})
	var nav := make("GridNavigationComponent", host, "Navigation", {
		"GridPath": NodePath("../Grid"), "PlacementPath": NodePath("../A"),
		"CellDataPath": NodePath("../Cells"), "UseBounds": false})
	var target := Vector2i(2, 0)
	a.call("SetOccupied", target, true)
	assert(nav.call("IsBlocked", target), "Initial placement source ignored")
	nav.set("PlacementPath", NodePath("../B"))
	assert(not nav.call("IsBlocked", target), "Navigation retained previous placement source")
	b.call("SetOccupied", target, true)
	assert(nav.call("IsBlocked", target), "Replacement occupancy ignored")
	b.call("SetOccupied", target, false)
	roads_a.call("SetRoad", target, "dirt_path", 0.5)
	nav.set("RoadPath", NodePath("../RoadsA"))
	assert(is_equal_approx(nav.call("TraversalCost", Vector2i(1, 0), target), 0.5))
	nav.set("RoadPath", NodePath("../RoadsB"))
	assert(is_equal_approx(nav.call("TraversalCost", Vector2i(1, 0), target), 1.0), "Old road cost retained")
	# Replacing a node at the same path must not retain the still-live, renamed node.
	cells.call("SetTerrainKind", target, "water")
	assert(nav.call("IsBlocked", target))
	cells.name = "OldCells"
	replacement.name = "Cells"
	assert(not nav.call("IsBlocked", target), "Navigation retained replaced cell node")
	var start: Vector2 = grid.call("CellToWorld", Vector2i.ZERO)
	var goal: Vector2 = grid.call("CellToWorld", target)
	assert(not nav.call("FindWorldPath", start, goal).is_empty())
	grid.set("TileMapLayerPath", NodePath("../MissingSurface"))
	assert(nav.call("FindWorldPath", start, goal).is_empty(), "Missing surface returned invalid path points")
	grid.set("TileMapLayerPath", NodePath(""))
	assert(nav.call("FindWorldPath", Vector2(NAN, 0), goal).is_empty(), "Non-finite input accepted")
	host.free()
	print("[terrain-navigation-binding] OK")
	quit()
