extends SceneTree
const BASE := "res://addons/beep_game_builder_cs/ecs/grid/"
var failures: Array[String] = []
func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		push_error(message)
func make(kind: String, parent: Node, label: String, properties: Dictionary = {}) -> Node:
	var node: Node = load(BASE + kind + "Component.cs").new()
	node.name = label
	for key in properties: node.set(key, properties[key])
	parent.add_child(node)
	return node
func _initialize() -> void: run.call_deferred()
func run() -> void:
	var host := Node2D.new()
	root.add_child(host)
	var grid := make("GridProjection", host, "Grid", {"TileSize": Vector2(32, 32), "DrawGrid": false, "TrackMouseCell": false})
	var cells := make("GridCellData", host, "Cells")
	var nav := make("GridNavigation", host, "Navigation", {"GridPath": NodePath("../Grid"), "CellDataPath": NodePath("../Cells"), "BoundsSize": Vector2i(160, 64), "LoadMissingTerrain": true})
	var body := Node2D.new()
	host.add_child(body)
	body.global_position = grid.CellToWorld(Vector2i.ZERO)
	var follower := make("GridPathFollower", body, "Follower", {"GridPath": NodePath("../../Grid"), "NavigationPath": NodePath("../../Navigation"), "DriveCharacterBody": false, "Speed": 32.0})
	follower.AutoAdvancePath = false
	var route: Array[Vector2i] = []
	for x in range(97): route.append(Vector2i(x, 0))
	for x in range(95, 15, -1): route.append(Vector2i(x, 0))
	for x in range(17, 121): route.append(Vector2i(x, 0))
	check(follower.SetCellPath(route), "Looping route was rejected")
	check(follower.PinnedRouteChunkCount == 4, "Route did not pin all needed chunks")
	follower.AdvancePath(64.0)
	check(grid.WorldToCell(body.global_position) == Vector2i(64, 0), "Fixture did not reach first outbound leg")
	check(cells.IsChunkPinned(Vector2i.ZERO), "Chunk was released despite a later return leg")
	var other := Node.new()
	host.add_child(other)
	cells.SetChunkPins(other, [Vector2i.ZERO])
	follower.AdvancePath(128.0)
	check(grid.WorldToCell(body.global_position) == Vector2i(32, 0), "Fixture did not complete return leg")
	check(follower.PinnedRouteChunkCount == 3, "Passed route chunk was not retired")
	check(cells.GetChunkPinCount(Vector2i.ZERO) == 1, "Retirement removed another owner's demand")
	cells.ReleaseChunkPins(other)
	check(cells.SetChunkAvailable(Vector2i.ZERO, false), "Passed terrain could not be unloaded")
	follower.AdvancePath(100.0)
	check(follower.HasReachedDestination and follower.PinnedRouteChunkCount == 0, "Unloaded history interrupted the remaining route")
	check(cells.GetPinnedChunks().is_empty(), "Finished route leaked demand")
	cells.SetChunkAvailable(Vector2i.ZERO, true)
	# Keep all four chunks while traversing a diagonal across their shared corner.
	body.global_position = grid.CellToWorld(Vector2i(30, 30))
	check(follower.SetCellPath([Vector2i(30, 30), Vector2i(31, 31), Vector2i(32, 32), Vector2i(33, 33), Vector2i(34, 34)]), "Diagonal fixture rejected")
	follower.AdvancePath(1.5)
	check(follower.PinnedRouteChunkCount == 4, "Diagonal side cells were released before crossing")
	follower.AdvancePath(1.5)
	check(follower.PinnedRouteChunkCount == 1 and cells.IsChunkPinned(Vector2i(1, 1)), "Completed corner crossing retained old side chunks")
	check(cells.SetChunkAvailable(Vector2i(0, 1), false) and cells.SetChunkAvailable(Vector2i(1, 0), false), "Passed diagonal side chunks could not unload")
	follower.AdvancePath(10.0)
	check(follower.HasReachedDestination and cells.GetPinnedChunks().is_empty(), "Diagonal retirement broke arrival")
	# A replacement starts a fresh lifetime index instead of retaining prior route history.
	check(follower.SetCellPath([Vector2i(34, 34), Vector2i(35, 34)]), "Replacement fixture rejected")
	check(follower.PinnedRouteChunkCount == 1, "Replacement retained old expiry entries")
	follower.CancelMove()
	check(follower.PinnedRouteChunkCount == 0 and cells.GetPinnedChunks().is_empty(), "Cancellation leaked route expiry state")
	cells.SetChunkAvailable(Vector2i(1, 0), true)
	var character := CharacterBody2D.new()
	host.add_child(character)
	character.global_position = grid.CellToWorld(Vector2i(30, 0))
	var motor := make("GridPathFollower", character, "Motor", {"GridPath": NodePath("../../Grid"), "NavigationPath": NodePath("../../Navigation"), "Speed": 1024.0})
	motor.AutoAdvancePath = false
	check(motor.SetCellPath([Vector2i(30, 0), Vector2i(31, 0), Vector2i(32, 0), Vector2i(33, 0), Vector2i(34, 0)]), "Character route rejected")
	var retired_while_moving := false
	for i in 300:
		await physics_frame
		motor.AdvancePath(1.0 / 60.0)
		if motor.IsMoving and motor.PinnedRouteChunkCount == 1: retired_while_moving = true
		if motor.HasReachedDestination: break
	if not motor.HasReachedDestination:
		print("[retirement] character=", character.global_position, " waypoint=", motor.CurrentWaypointIndex, " failure=", motor.LastMoveFailure, " pins=", motor.PinnedRouteChunkCount)
	check(retired_while_moving and motor.HasReachedDestination, "CharacterBody movement did not retire chunks before arrival")
	check(cells.GetPinnedChunks().is_empty(), "Character arrival retained route pins")
	var collider := CollisionShape2D.new()
	var shape := RectangleShape2D.new()
	shape.size = Vector2(8, 8)
	collider.shape = shape
	character.add_child(collider)
	var wall := StaticBody2D.new()
	var wall_collider := CollisionShape2D.new()
	var wall_shape := RectangleShape2D.new()
	wall_shape.size = Vector2(8, 64)
	wall_collider.shape = wall_shape
	wall.add_child(wall_collider)
	host.add_child(wall)
	character.global_position = grid.CellToWorld(Vector2i(30, 0))
	wall.global_position = character.global_position + Vector2(16, 0)
	check(motor.SetCellPath([Vector2i(30, 0), Vector2i(31, 0)]), "Collision route rejected")
	for i in 20:
		await physics_frame
		motor.AdvancePath(1.0 / 60.0)
	check(not motor.HasReachedDestination and character.global_position.x < wall.global_position.x, "Arrival clamp bypassed native collision")
	wall.queue_free()
	for i in 40:
		await physics_frame
		motor.AdvancePath(1.0 / 60.0)
		if motor.HasReachedDestination: break
	check(motor.HasReachedDestination and cells.GetPinnedChunks().is_empty(), "Removing obstacle did not resume arrival and release pins")
	host.free()
	print("[terrain-route-retirement] OK" if failures.is_empty() else "[terrain-route-retirement] FAILED")
	quit(0 if failures.is_empty() else 1)
