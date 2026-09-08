extends SceneTree

const ECS := "res://addons/beep_game_builder_cs/ecs/"
var failures: Array[String] = []

func make(path: String, label: String, parent: Node, properties: Dictionary = {}) -> Node:
	var node: Node = load(ECS + path + ".cs").new()
	node.name = label
	for key in properties: node.set(key, properties[key])
	parent.add_child(node)
	return node

func _initialize() -> void: call_deferred("run")

func run() -> void:
	var host := Node2D.new()
	root.add_child(host)
	current_scene = host
	var registry := make("actors/ActorRegistryComponent", "Registry", host)
	make("actors/PlayerContextComponent", "Player", host, {"RegistryPath": NodePath("../Registry"), "ReadLocalInput": false})
	var grid := make("grid/GridProjectionComponent", "Grid", host, {"DrawGrid": false, "TrackMouseCell": false})
	make("grid/GridNavigationComponent", "Navigation", host, {"GridPath": NodePath("../Grid"), "BoundsSize": Vector2i(1024, 1024)})
	var definition: Resource = load(ECS + "actors/ActorDefinition.cs").new()
	var before := Time.get_ticks_msec()
	for i in 1000:
		var body := Node2D.new()
		body.name = "Unit%d" % i
		body.position = grid.CellToWorld(Vector2i(i % 40 + 2, i / 40 + 2))
		host.add_child(body)
		if i < 200:
			make("grid/GridPathFollowerComponent", "Follower", body, {"GridPath": NodePath("../../Grid"), "NavigationPath": NodePath("../../Navigation"), "DriveCharacterBody": false, "Speed": 500.0, "SetZIndexFromY": false})
		make("actors/ActorComponent", "Actor", body, {"ActorId": "unit_%d" % i, "OwnerId": "player_1", "RegistryPath": NodePath("../../Registry"), "Definition": definition})
	var spawn_ms := Time.get_ticks_msec() - before
	var command_script = load(ECS + "actors/ActorCommand.cs")
	for i in 200:
		var command: Resource = command_script.new()
		command.IssuerId = "player_1"
		command.Recipients = ["unit_%d" % i]
		command.TargetCell = Vector2i(i % 40 + 2, i / 40 + 8)
		if registry.Submit(command) != 1: failures.append("Order rejected for %d" % i)
	var travel_started := Time.get_ticks_msec()
	for i in 300:
		await physics_frame
		var active := false
		for unit in 200:
			if registry.FindActor("unit_%d" % unit).HasOrders:
				active = true
				break
		if not active: break
	var travel_ms := Time.get_ticks_msec() - travel_started
	var arrived := 0
	for i in 200:
		var body: Node2D = registry.FindActor("unit_%d" % i).get_parent()
		if body.position.distance_to(grid.CellToWorld(Vector2i(i % 40 + 2, i / 40 + 8))) < 1: arrived += 1
	if arrived != 200: failures.append("Only %d of 200 routes completed" % arrived)
	if registry.ActorCount != 1000: failures.append("Registry count changed")
	before = Time.get_ticks_msec()
	var saved: Dictionary = registry.CaptureState()
	var save_ms := Time.get_ticks_msec() - before
	if saved.actors.size() != 1000: failures.append("Save lost actors")
	print("[actor scale] 1000 identities, %d/200 moving actors arrived; spawn=%d ms, capture=%d ms, scheduled travel=%d ms. Functional test, not a full-detail FPS benchmark." % [arrived, spawn_ms, save_ms, travel_ms])
	for failure in failures: push_error(failure)
	host.free()
	quit(0 if failures.is_empty() else 1)
