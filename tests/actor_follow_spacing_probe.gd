extends SceneTree
const ECS := "res://addons/beep_game_builder_cs/ecs/"
var failures: Array[String] = []
var members: Array[Node] = []
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
		for member in members:
			member._PhysicsProcess(0.1)
			member.get_parent().get_node("Follower").AdvancePath(0.1)
func verify_spacing(leader: Node2D) -> void:
	var bounds: Array[Rect2] = [Rect2(leader.global_position - Vector2(12, 12), Vector2(24, 24))]
	for member in members:
		var size: Vector2 = member.Definition.Footprint + Vector2(4, 4)
		var rect := Rect2(member.get_parent().global_position - size * 0.5, size)
		for occupied in bounds: check(not rect.intersects(occupied), "Followers settled with overlapping footprints")
		bounds.append(rect)
		check(not member.get_parent().get_node("Follower").IsMoving, "Follower did not settle")
func _initialize() -> void: run.call_deferred()
func run() -> void:
	var host := Node2D.new()
	root.add_child(host)
	var registry := make("actors/ActorRegistryComponent", host, "Registry")
	make("actors/PlayerContextComponent", host, "Player", {"RegistryPath": NodePath("../Registry"), "ReadLocalInput": false})
	var grid := make("grid/GridProjectionComponent", host, "Grid", {"TileSize": Vector2(32, 32), "DrawGrid": false, "TrackMouseCell": false})
	var cells := make("grid/GridCellDataComponent", host, "Cells")
	cells.FillTerrain(Rect2i(0, 0, 40, 32), "grass")
	make("grid/GridNavigationComponent", host, "Navigation", {"GridPath": NodePath("../Grid"), "CellDataPath": NodePath("../Cells"), "BoundsSize": Vector2i(40, 32)})
	var leader := Node2D.new()
	host.add_child(leader)
	leader.position = grid.CellToWorld(Vector2i(16, 16))
	make("actors/ActorComponent", leader, "Actor", {"ActorId": "leader", "OwnerId": "player_1", "RegistryPath": NodePath("../../Registry")})
	for i in 6:
		var body := Node2D.new()
		host.add_child(body)
		body.position = leader.position + Vector2(32, 0)
		var definition: Resource = load(ECS + "actors/ActorDefinition.cs").new()
		definition.Capabilities = 1
		definition.Footprint = Vector2(36, 28) if i == 5 else Vector2(20, 20)
		make("grid/GridPathFollowerComponent", body, "Follower", {"GridPath": NodePath("../../Grid"), "NavigationPath": NodePath("../../Navigation"), "DriveCharacterBody": false, "Speed": 160.0})
		members.append(make("actors/ActorComponent", body, "Actor", {"ActorId": "u%d" % i, "OwnerId": "player_1", "Definition": definition, "RegistryPath": NodePath("../../Registry")}))
	var command: Resource = load(ECS + "actors/ActorCommand.cs").new()
	command.IssuerId = "player_1"
	command.Action = 5
	command.TargetActorId = "leader"
	command.FollowDistance = 128.0
	for member in members:
		command.Recipients = [member.ActorId]
		check(registry.Submit(command) == 1, "Follow setup rejected")
	await advance(100)
	verify_spacing(leader)
	check(registry.FollowRestReservationCount == 6, "Rest reservations did not cover followers")
	var positions: Array[Vector2] = []
	for member in members: positions.append(member.get_parent().position)
	leader.position.x += 4
	await advance(20)
	for i in members.size(): check(members[i].get_parent().position == positions[i], "Small leader movement caused spacing jitter")
	var saved: Dictionary = JSON.parse_string(JSON.stringify(registry.CaptureState()))
	for member in members: member.CancelOrders()
	check(registry.FollowRestReservationCount == 0, "Cancel leaked rest reservations")
	registry.RestoreState(saved)
	await advance(80)
	verify_spacing(leader)
	leader.position = grid.CellToWorld(Vector2i(24, 16))
	await advance(100)
	verify_spacing(leader)
	# Removing one body must immediately free its disposable destination.
	var removed: Node = members.pop_back()
	removed.get_parent().free()
	check(registry.FollowRestReservationCount == 5, "Removed follower retained its reservation")
	leader.free()
	await advance(2)
	check(registry.FollowRestReservationCount == 0, "Removed leader left reservations behind")
	host.free()
	print("[actor-follow-spacing] OK" if failures.is_empty() else "[actor-follow-spacing] FAILED")
	quit(0 if failures.is_empty() else 1)
