extends SceneTree
const BASE = "res://addons/beep_game_builder_cs/ecs/grid/"

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	var host := Node2D.new()
	root.add_child(host)
	var cells = load(BASE + "GridCellDataComponent.cs").new()
	cells.name = "Cells"
	host.add_child(cells)
	var grid = load(BASE + "GridProjectionComponent.cs").new()
	grid.name = "Grid"
	grid.set("DrawGrid", false)
	grid.set("TrackMouseCell", false)
	host.add_child(grid)
	var nav = load(BASE + "GridNavigationComponent.cs").new()
	nav.name = "Navigation"
	nav.set("CellDataPath", NodePath("../Cells"))
	nav.set("GridPath", NodePath("../Grid"))
	nav.set("BoundsSize", Vector2i(5, 3))
	host.add_child(nav)
	var body := Node2D.new()
	host.add_child(body)
	var follower = load(BASE + "GridPathFollowerComponent.cs").new()
	follower.set("GridPath", NodePath("../../Grid"))
	follower.set("NavigationPath", NodePath("../../Navigation"))
	follower.set("Speed", 32.0)
	follower.set("SetZIndexFromY", false)
	body.add_child(follower)
	var identity = load(BASE + "GridObjectComponent.cs").new()
	identity.set("GridPath", NodePath("../../Grid"))
	body.add_child(identity)
	follower.AutoAdvancePath = false
	var route: Array[Vector2i] = [Vector2i.ZERO, Vector2i.RIGHT, Vector2i(2, 0)]
	body.global_position = grid.call("CellToWorld", Vector2i.ZERO)
	cells.call("SetMetadata", Vector2i.RIGHT, "terrain_relief", 1)
	assert(not follower.call("SetCellPath", [Vector2i.RIGHT]), "Omitted start cell bypassed cliff validation")
	cells.call("SetMetadata", Vector2i.ZERO, "terrain_ramp_direction", Vector2i.RIGHT)
	assert(follower.call("SetCellPath", [Vector2i.RIGHT]), "Omitted start could not connect through valid ramp")
	follower.call("CancelMove")
	cells.call("ClearCells")
	assert(not follower.call("SetCellPath", [Vector2i(2, 0)]), "Nonadjacent custom route bypassed traversal checks")
	assert(follower.call("SetCellPath", route))
	follower.call("AdvancePath", 0.5)
	var old_from: Vector2 = grid.call("CellToWorld", Vector2i.ZERO)
	var old_to: Vector2 = grid.call("CellToWorld", Vector2i.RIGHT)
	var progress := body.global_position.distance_to(old_from) / old_from.distance_to(old_to)
	assert(absf(progress - 0.25) < 0.001)
	grid.set("Projection", 1)
	grid.call("NotifyGeometryChanged")
	follower.call("AdvancePath", 0.0)
	var expected: Vector2 = grid.call("CellToWorld", Vector2i.ZERO).lerp(grid.call("CellToWorld", Vector2i.RIGHT), progress)
	assert(body.global_position.distance_to(expected) < 0.001, "Projection change lost segment progress")
	var failures: Array[String] = []
	follower.connect("MoveFailed", func(_x, _y, reason): failures.append(reason))
	cells.call("SetTerrainKind", Vector2i.RIGHT, "water")
	var before := body.global_position
	assert(not follower.call("AdvancePath", 0.5))
	assert(not follower.get("IsMoving") and body.global_position == before, "Follower entered flooded edge")
	assert(failures == ["terrain_route_changed"], "Blocked route did not emit one failure")
	cells.call("ClearCells")
	body.global_position = grid.call("CellToWorld", Vector2i.ZERO)
	assert(follower.call("SetCellPath", route))
	follower.connect("WaypointReached", func(_index, _point): follower.call("CancelMove"), CONNECT_ONE_SHOT)
	follower.call("AdvancePath", 0.5)
	assert(not follower.get("IsMoving"), "Waypoint callback cancellation was ignored")
	assert(follower.call("SetCellPath", route))
	for i in range(40):
		follower.call("AdvancePath", 0.25)
	assert(not follower.get("IsMoving") and body.global_position.distance_to(grid.call("CellToWorld", Vector2i(2, 0))) < 0.001, "Valid route did not finish")
	assert(identity.get("Cell") == Vector2i(2, 0), "Arrival left idle unit identity at its starting cell")
	grid.set("Projection", 0)
	grid.call("NotifyGeometryChanged")
	assert(body.global_position.distance_to(grid.call("CellToWorld", Vector2i(2, 0))) < 0.001, "Idle unit did not follow view change")
	# A long simulation step must consume distance across corners, not one tile per frame.
	var bent: Array[Vector2i] = [Vector2i.ZERO, Vector2i.RIGHT, Vector2i(1, 1), Vector2i(2, 1)]
	body.global_position = grid.call("CellToWorld", Vector2i.ZERO)
	follower.set("Speed", 160.0)
	assert(follower.call("SetCellPath", bent))
	follower.call("AdvancePath", 1.0)
	var turn: Vector2 = grid.call("CellToWorld", Vector2i(1, 1))
	var end: Vector2 = grid.call("CellToWorld", Vector2i(2, 1))
	assert(body.global_position.distance_to(turn.lerp(end, 0.5)) < 0.001, "Long step discarded movement or cut the corner")
	var one_step := body.global_position
	body.global_position = grid.call("CellToWorld", Vector2i.ZERO)
	assert(follower.call("SetCellPath", bent))
	for i in range(100):
		follower.call("AdvancePath", 0.01)
	assert(body.global_position.distance_to(one_step) < 0.01, "Movement depends on update rate")
	follower.call("AdvancePath", 0.2)
	assert(follower.get("HasReachedDestination"), "Exact-budget arrival waited an extra frame")
	# Terrain changed by an intermediate arrival must block the next edge in this update.
	body.global_position = grid.call("CellToWorld", Vector2i.ZERO)
	assert(follower.call("SetCellPath", bent))
	follower.connect("WaypointReached", func(index, _point):
		if index == 1:
			cells.call("SetTerrainKind", Vector2i(1, 1), "water"))
	assert(not follower.call("AdvancePath", 10.0))
	assert(body.global_position.distance_to(grid.call("CellToWorld", Vector2i.RIGHT)) < 0.001, "Long step crossed a newly flooded edge")
	assert(not follower.get("HasReachedDestination"))
	host.free()
	print("[terrain-follower-live] OK")
	quit()
