extends SceneTree

const ECS := "res://addons/beep_game_builder_cs/ecs/"
var failures: Array[String] = []
var finished: Array = []

func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		push_error(message)

func make(path: String, label: String, parent: Node, properties: Dictionary = {}) -> Node:
	var node: Node = load(ECS + path + ".cs").new()
	node.name = label
	for key in properties: node.set(key, properties[key])
	parent.add_child(node)
	return node

func _initialize() -> void: run.call_deferred()

func drain(controller: Node, nav: Node) -> void:
	for frame in 4000:
		controller._Process(0.0)
		nav.ProcessPathRequests()
		if not controller.IsFormationPending: return
	check(false, "Formation did not finish")

func run() -> void:
	var host := Node2D.new()
	root.add_child(host)
	var registry := make("actors/ActorRegistryComponent", "Registry", host)
	var player := make("actors/PlayerContextComponent", "Player", host, {"RegistryPath": NodePath("../Registry"), "ReadLocalInput": false})
	var grid := make("grid/GridProjectionComponent", "Grid", host, {"DrawGrid": false})
	var nav := make("grid/GridNavigationComponent", "Navigation", host, {"GridPath": NodePath("../Grid"), "BoundsSize": Vector2i(12, 12), "PathExpansionsPerFrame": 16})
	for y in 12: nav.SetBlocked(Vector2i(4, y), true)
	var controller := make("actors/ActorOrdersControllerComponent", "Orders", host, {"PlayerPath": NodePath("../Player"), "GridPath": NodePath("../Grid"), "NavigationPath": NodePath("../Navigation"), "FormationSearchRadius": 4})
	controller.FormationFinished.connect(func(sent, rejected): finished.append([sent, rejected]))
	var definition: Resource = load(ECS + "actors/ActorDefinition.cs").new()
	var actors: Array[Node] = []
	for i in 2:
		var body := Node2D.new()
		body.position = grid.CellToWorld(Vector2i(1, 2 + i))
		host.add_child(body)
		make("grid/GridPathFollowerComponent", "Follower", body, {"GridPath": NodePath("../../Grid"), "NavigationPath": NodePath("../../Navigation"), "DriveCharacterBody": false})
		var actor := make("actors/ActorComponent", "Actor", body, {"ActorId": "unit_%d" % i, "OwnerId": "player_1", "RegistryPath": NodePath("../../Registry"), "Definition": definition})
		actors.append(actor)
		player.SelectActor(actor.ActorId, true)
	check(controller.MoveSelection(Vector2i(6, 6), false) == 2, "Group not accepted")
	check(not actors[0].HasOrders, "Preflight dispatched synchronously")
	drain(controller, nav)
	check(finished == [[2, 0]], "Reachable fallback not assigned to both actors")
	var targets := []
	for actor in actors:
		var orders: Array = actor.CaptureActor().orders
		check(orders.size() == 1, "Expected one dispatched move")
		if orders.size() == 1:
			var target := Vector2i(orders[0].x, orders[0].y)
			check(target.x < 4, "Assigned across impassable barrier")
			check(not targets.has(target), "Duplicate formation destination")
			targets.append(target)
		actor.CancelOrders()
	controller.MoveSelection(Vector2i(6, 6), false)
	controller._Process(0.0)
	check(nav.PendingPathRequestCount == 1, "Expected scheduled preflight")
	actors[0].CancelOrders()
	drain(controller, nav)
	check(not actors[0].HasOrders and finished.back() == [1, 1], "Delayed formation overwrote stop command")
	for actor in actors: actor.CancelOrders()
	controller.MoveSelection(Vector2i(6, 6), false)
	controller._Process(0.0)
	controller.MoveSelection(Vector2i(2, 8), false)
	drain(controller, nav)
	check(finished.back() == [2, 0], "Replacement failed")
	for actor in actors:
		var order: Dictionary = actor.CaptureActor().orders[0]
		check(order.y >= 7, "Old formation dispatched after replacement")
		actor.CancelOrders()
	controller.MoveSelection(Vector2i(6, 6), false)
	controller._Process(0.0)
	host.remove_child(controller)
	check(nav.PendingPathRequestCount == 0, "Removed controller leaked search")
	host.add_child(controller)
	check(not controller.IsFormationPending, "Reattached controller retained stale formation")
	controller.FormationSearchRadius = 1
	controller.MoveSelection(Vector2i(8, 8), false)
	drain(controller, nav)
	check(finished.back() == [0, 2], "Unreachable formation was dispatched")
	controller.FormationSearchRadius = 4
	controller.MoveSelection(Vector2i(6, 6), false)
	controller._Process(0.0)
	nav.ProcessPathRequests()
	nav.SetBlocked(Vector2i(2, 5), true)
	drain(controller, nav)
	check(finished.back()[1] >= 1, "Stale terrain preflight was accepted")
	for actor in actors: actor.CancelOrders()
	controller.MoveSelection(Vector2i(2, 8), false)
	drain(controller, nav)
	var destinations := []
	for actor in actors:
		var order: Dictionary = actor.CaptureActor().orders[0]
		destinations.append(Vector2i(order.x, order.y))
	for frame in 300:
		await physics_frame
		if not actors[0].HasOrders and not actors[1].HasOrders: break
	for i in 2:
		check(actors[i].get_parent().global_position.distance_to(grid.CellToWorld(destinations[i])) < 1, "Dispatched actor did not reach assigned cell")
	controller.MaximumQueuedFormations = 2
	var finished_before := finished.size()
	check(controller.MoveSelection(Vector2i(2, 2), false) == 2, "Initial queued group rejected")
	controller._Process(0.0)
	check(controller.MoveSelection(Vector2i(2, 8), true) == 2, "First append rejected")
	check(controller.MoveSelection(Vector2i(2, 4), true) == 2, "Second append rejected")
	check(controller.QueuedFormationCount == 2 and nav.PendingPathRequestCount == 1, "Append replaced active search or started extra searches")
	check(controller.MoveSelection(Vector2i(2, 10), true) == 0, "Formation queue limit ignored")
	player.ClearSelection()
	for frame in 1400:
		await physics_frame
		if not controller.IsFormationPending and not actors[0].HasOrders and not actors[1].HasOrders: break
	check(finished.size() == finished_before + 3, "Queued batches did not complete exactly once")
	for result in finished.slice(finished_before): check(result == [2, 0], "Own earlier move invalidated queued group")
	check(controller.QueuedFormationCount == 0 and not controller.IsFormationPending, "Queue did not drain")
	for actor in actors:
		var cell: Vector2i = grid.WorldToCell(actor.get_parent().global_position)
		check(cell.y >= 3 and cell.y <= 4, "Queued destination order changed")
		player.SelectActor(actor.ActorId, true)
	controller.MoveSelection(Vector2i(2, 10), false)
	controller.MoveSelection(Vector2i(1, 1), true)
	actors[0].CancelOrders()
	var stopped_position: Vector2 = actors[0].get_parent().global_position
	for frame in 1000:
		await physics_frame
		if not controller.IsFormationPending and not actors[1].HasOrders: break
	check(actors[0].get_parent().global_position == stopped_position and not actors[0].HasOrders, "Queued plans resurrected stopped actor")
	check(finished.back() == [1, 1], "Stop did not reject actor across queued batches")
	var interrupt := func(id, _action):
		if id == "unit_0": actors[0].CancelOrders()
	registry.CommandAccepted.connect(interrupt)
	stopped_position = actors[0].get_parent().global_position
	controller.MoveSelection(Vector2i(2, 6), false)
	controller.MoveSelection(Vector2i(2, 3), true)
	for frame in 1000:
		await physics_frame
		if not controller.IsFormationPending and not actors[1].HasOrders: break
	registry.CommandAccepted.disconnect(interrupt)
	check(actors[0].get_parent().global_position == stopped_position and finished.back() == [1, 1], "Reentrant stop was mistaken for the planner's own dispatch")
	controller.MoveSelection(Vector2i(2, 10), false)
	controller.MoveSelection(Vector2i(1, 1), true)
	controller.MoveSelection(Vector2i(2, 6), false)
	check(controller.QueuedFormationCount == 0, "Replacement retained queued batches")
	controller.CancelFormation()
	controller.MoveSelection(Vector2i(1, 1), false)
	controller._Process(0.0)
	host.remove_child(player)
	for frame in 1000:
		nav.ProcessPathRequests()
		if nav.PendingPathRequestCount == 0: break
	controller._Process(0.0)
	check(not controller.IsFormationPending and not actors[0].HasOrders and not actors[1].HasOrders, "Removed player still dispatched a formation")
	player.free()
	host.free()
	print("[actor-formation] OK" if failures.is_empty() else "[actor-formation] FAILED")
	quit(0 if failures.is_empty() else 1)
