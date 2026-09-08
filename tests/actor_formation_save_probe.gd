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

func run() -> void:
	var host := Node2D.new()
	root.add_child(host)
	var registry := make("actors/ActorRegistryComponent", "Registry", host)
	var player := make("actors/PlayerContextComponent", "Player", host, {"RegistryPath": NodePath("../Registry"), "ReadLocalInput": false})
	var grid := make("grid/GridProjectionComponent", "Grid", host, {"DrawGrid": false})
	var nav := make("grid/GridNavigationComponent", "Navigation", host, {"GridPath": NodePath("../Grid"), "BoundsSize": Vector2i(32, 32), "PathExpansionsPerFrame": 16})
	var controller := make("actors/ActorOrdersControllerComponent", "Orders", host, {"PlayerPath": NodePath("../Player"), "GridPath": NodePath("../Grid"), "NavigationPath": NodePath("../Navigation")})
	controller.FormationFinished.connect(func(sent, rejected): finished.append([sent, rejected]))
	var definition: Resource = load(ECS + "actors/ActorDefinition.cs").new()
	var actors: Array[Node] = []
	for i in 3:
		var body := Node2D.new()
		body.position = grid.CellToWorld(Vector2i(1, 1 + i))
		host.add_child(body)
		make("grid/GridPathFollowerComponent", "Follower", body, {"GridPath": NodePath("../../Grid"), "NavigationPath": NodePath("../../Navigation"), "DriveCharacterBody": false, "Speed": 2000.0})
		var actor := make("actors/ActorComponent", "Actor", body, {"ActorId": "unit_%d" % i, "OwnerId": "player_1", "RegistryPath": NodePath("../../Registry"), "Definition": definition})
		actors.append(actor)
		player.SelectActor(actor.ActorId, true)
	controller.MoveSelection(Vector2i(28, 20), false)
	controller.MoveSelection(Vector2i(3, 8), true)
	for frame in 1000:
		controller._Process(0.0)
		nav.ProcessPathRequests()
		if controller.CaptureFormations().dispatched == 1: break
	controller._Process(0.0)
	check(nav.PendingPathRequestCount == 1, "Fixture has no in-flight preflight")
	actors[2].CancelOrders()
	var stopped_position: Vector2 = actors[2].get_parent().global_position
	var partial: Dictionary = controller.CaptureFormations()
	check(partial.dispatched == 1 and partial.plans[0].members.size() == 2 and partial.assigned.size() == 1, "Expected partial dispatch snapshot")
	var saved: Dictionary = JSON.parse_string(JSON.stringify(registry.CaptureState()))
	check(saved.players.player_1.formations.has("../Orders"), "Registry omitted controller plan")
	var invalid: Array[Dictionary] = []
	var bad: Dictionary = partial.duplicate(true)
	bad.plans[0].members[1].id = bad.plans[0].members[0].id
	invalid.append(bad)
	bad = partial.duplicate(true)
	bad.plans[0].candidates[0][0] = 1.5
	invalid.append(bad)
	bad = partial.duplicate(true)
	bad.issuer = "enemy"
	invalid.append(bad)
	bad = partial.duplicate(true)
	bad.dispatched = 7
	invalid.append(bad)
	bad = partial.duplicate(true)
	bad.plans[1].append = false
	invalid.append(bad)
	for malformed in invalid:
		check(not controller.RestoreFormations(malformed), "Malformed formation accepted")
		check(controller.CaptureFormations() == partial and nav.PendingPathRequestCount == 1, "Rejected restore changed live plan/search")
	controller.CancelFormation()
	for actor in actors: actor.CancelOrders()
	registry.RestoreState(saved)
	check(controller.IsFormationPending and controller.QueuedFormationCount == 1, "Queued plans not restored")
	check(nav.PendingPathRequestCount == 0, "Restore persisted a runtime preflight")
	check(actors[0].CaptureActor().orders.size() == 1 and actors[1].CaptureActor().orders.is_empty(), "Partial dispatch duplicated or dropped actor order")
	check(controller.CaptureFormations().assigned == partial.assigned, "Restore lost assigned destination")
	# A second JSON round trip before resuming must retain the stopped tombstone.
	saved = JSON.parse_string(JSON.stringify(registry.CaptureState()))
	registry.RestoreState(saved)
	for frame in 1800:
		await physics_frame
		if not controller.IsFormationPending and not actors[0].HasOrders and not actors[1].HasOrders: break
	check(finished == [[2, 1], [2, 1]], "Restored group completion counts/order incorrect")
	check(actors[2].get_parent().global_position == stopped_position and not actors[2].HasOrders, "Save revived stopped actor")
	var targets := []
	for i in 2:
		var cell: Vector2i = grid.WorldToCell(actors[i].get_parent().global_position)
		check(cell.y >= 7 and cell.y <= 8 and not targets.has(cell), "Restored queued move did not arrive at distinct final targets")
		targets.append(cell)
	var idle: Dictionary = JSON.parse_string(JSON.stringify(registry.CaptureState()))
	controller.MoveSelection(Vector2i(20, 20), false)
	registry.RestoreState(idle)
	check(not controller.IsFormationPending and nav.PendingPathRequestCount == 0, "Idle save retained later group order")
	host.free()
	print("[actor-formation-save] OK" if failures.is_empty() else "[actor-formation-save] FAILED")
	quit(0 if failures.is_empty() else 1)
