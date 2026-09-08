extends SceneTree
const ECS := "res://addons/beep_game_builder_cs/ecs/"
var failures: Array[String] = []
var actor: Node
var follower: Node
func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		push_error(message)
func make(path: String, parent: Node, name_: String, properties: Dictionary = {}) -> Node:
	var node: Node = load(ECS + path + ".cs").new()
	node.name = name_
	for key in properties: node.set(key, properties[key])
	parent.add_child(node)
	node.set_physics_process(false)
	return node
func advance(frames: int) -> void:
	for i in frames:
		await physics_frame
		actor._PhysicsProcess(0.1)
		follower.AdvancePath(0.1)
func _initialize() -> void: run.call_deferred()
func run() -> void:
	var host := Node2D.new()
	root.add_child(host)
	var registry := make("actors/ActorRegistryComponent", host, "Registry")
	var player := make("actors/PlayerContextComponent", host, "Player", {"RegistryPath": NodePath("../Registry"), "ReadLocalInput": false})
	make("actors/PlayerContextComponent", host, "Other", {"RegistryPath": NodePath("../Registry"), "PlayerId": "other", "ReadLocalInput": false})
	var grid := make("grid/GridProjectionComponent", host, "Grid", {"TileSize": Vector2(32, 32), "DrawGrid": false, "TrackMouseCell": false})
	var cells := make("grid/GridCellDataComponent", host, "Cells")
	cells.FillTerrain(Rect2i(0, 0, 64, 24), "grass")
	var navigation := make("grid/GridNavigationComponent", host, "Navigation", {"GridPath": NodePath("../Grid"), "CellDataPath": NodePath("../Cells"), "BoundsSize": Vector2i(64, 24)})
	var definition: Resource = load(ECS + "actors/ActorDefinition.cs").new()
	definition.Capabilities = 1
	var bodies: Array[Node2D] = []
	var actors: Array[Node] = []
	for i in 3:
		var body := Node2D.new()
		host.add_child(body)
		body.global_position = grid.CellToWorld(Vector2i(4 + i * 20, 8))
		make("HealthComponent", body, "Health")
		var path := make("grid/GridPathFollowerComponent", body, "Follower", {"GridPath": NodePath("../../Grid"), "NavigationPath": NodePath("../../Navigation"), "DriveCharacterBody": false, "Speed": 160.0})
		var member := make("actors/ActorComponent", body, "Actor", {"ActorId": "u%d" % i, "OwnerId": "player_1" if i < 2 else "other", "Definition": definition, "RegistryPath": NodePath("../../Registry")})
		bodies.append(body)
		actors.append(member)
		if i == 0:
			actor = member
			follower = path
	var command: Resource = load(ECS + "actors/ActorCommand.cs").new()
	command.IssuerId = "player_1"
	command.Recipients = ["u0"]
	command.Action = 5
	command.TargetActorId = "u1"
	command.FollowDistance = 64.0
	check(player.PossessedActorId.is_empty() and registry.Submit(command) == 1, "Avatar-free follow rejected")
	command.Recipients = ["u1"]
	command.TargetActorId = "u0"
	check(registry.Submit(command) == 0, "Queued follow chain allowed a cycle before execution")
	command.Recipients = ["u0"]
	command.FollowDistance = 1.0
	command.TargetActorId = "u2"
	await advance(70)
	var distance := bodies[0].global_position.distance_to(bodies[1].global_position)
	check(actor.HasOrders and not follower.IsMoving and distance > 32 and distance <= 64, "Follow did not settle at its copied stand-off distance")
	command.Recipients = ["u1"]
	command.TargetActorId = "u0"
	command.FollowDistance = 64.0
	check(registry.Submit(command) == 0 and not actors[1].HasOrders, "Circular follow chain accepted")
	command.Recipients = ["u0"]
	var resting := bodies[0].global_position
	bodies[1].position.x += 4
	await advance(15)
	check(bodies[0].global_position == resting, "Small leader motion caused follow jitter")
	var state: Dictionary = JSON.parse_string(JSON.stringify(registry.CaptureState()))
	actor.CancelOrders()
	registry.RestoreState(state)
	bodies[1].global_position = grid.CellToWorld(Vector2i(28, 8))
	await advance(40)
	check(actor.HasOrders and bodies[0].position.distance_to(bodies[1].position) <= 64, "Restored follow did not track a moving leader")
	cells.FillTerrain(Rect2i(32, 0, 1, 24), "water")
	bodies[1].global_position = grid.CellToWorld(Vector2i(44, 8))
	await advance(50)
	check(actor.HasOrders and grid.WorldToCell(bodies[0].position).x < 32 and navigation.PendingPathRequestCount <= 1, "Follow crossed water, lost its order, or flooded navigation")
	cells.FillTerrain(Rect2i(32, 0, 1, 24), "grass")
	await advance(80)
	check(bodies[0].position.distance_to(bodies[1].position) <= 64, "Follow did not recover after barrier removal")
	command.TargetActorId = "u0"
	command.FollowDistance = 64.0
	check(registry.Submit(command) == 0 and actor.HasOrders, "Self-follow replaced a valid order")
	command.TargetActorId = "u2"
	check(registry.Submit(command) == 0 and actor.HasOrders, "Foreign follow replaced a valid order")
	command.TargetActorId = "u1"
	command.FollowDistance = NAN
	check(registry.Submit(command) == 0, "Nonfinite follow distance accepted")
	var completed: Array = []
	actor.CommandFinished.connect(func(action, success, reason): completed.append([action, success, reason]))
	registry.TransferOwnership("u1", "other")
	await advance(2)
	check(not actor.HasOrders and not follower.IsMoving and completed == [[5, false, "follow_target_unavailable"]], "Ownership loss left a follow route alive")
	registry.TransferOwnership("u1", "player_1")
	command.FollowDistance = 64.0
	check(registry.Submit(command) == 1, "Follow could not restart")
	await advance(2)
	command.Action = 0
	command.TargetCell = Vector2i(38, 8)
	check(registry.Submit(command) == 1, "Manual move did not replace follow")
	bodies[1].global_position = grid.CellToWorld(Vector2i(60, 8))
	await advance(35)
	check(not actor.HasOrders and bodies[0].position.distance_to(grid.CellToWorld(Vector2i(38, 8))) < 1, "Follow overrode replacement movement")
	command.Action = 5
	check(registry.Submit(command) == 1, "Final follow fixture rejected")
	await advance(2)
	bodies[1].free()
	await advance(2)
	check(not actor.HasOrders and not follower.IsMoving and navigation.PendingPathRequestCount == 0, "Removed leader leaked movement/search")
	host.free()
	print("[actor-follow] OK" if failures.is_empty() else "[actor-follow] FAILED")
	quit(0 if failures.is_empty() else 1)
