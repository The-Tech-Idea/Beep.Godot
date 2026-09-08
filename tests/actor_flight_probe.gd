extends SceneTree

const ECS := "res://addons/beep_game_builder_cs/ecs/"
var failures: Array[String] = []

func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		push_error(message)

func make(path: String, label: String, parent: Node, properties: Dictionary = {}) -> Node:
	var node: Node = load(ECS + path + ".cs").new()
	node.name = label
	for key in properties: node.set(key, properties[key])
	parent.add_child(node)
	node.set_physics_process(false)
	return node

func _initialize() -> void: run.call_deferred()

func run() -> void:
	var host := Node2D.new()
	root.add_child(host)
	make("actors/ActorRegistryComponent", "Registry", host)
	make("actors/PlayerContextComponent", "Player", host, {"RegistryPath": NodePath("../Registry"), "ReadLocalInput": false})
	make("grid/GridProjectionComponent", "Grid", host, {"DrawGrid": false})
	make("grid/GridNavigationComponent", "Navigation", host, {"GridPath": NodePath("../Grid"), "BoundsSize": Vector2i(32, 32)})
	var actors: Array[Node] = []
	var flights: Array[Node] = []
	var definition: Resource = load(ECS + "actors/ActorDefinition.cs").new()
	for i in 2:
		var body := CharacterBody2D.new()
		body.position = Vector2(200 + i * 100, 200)
		host.add_child(body)
		actors.append(make("actors/ActorComponent", "Actor", body, {"ActorId": "flight_%d" % i, "OwnerId": "player_1", "RegistryPath": NodePath("../../Registry"), "Definition": definition}))
		flights.append(make("FlyComponent", "Flight", body, {"EnableBanking": false, "MaxSpeed": 300.0, "Acceleration": 60000.0, "Friction": 60000.0}))
	var added := []
	for action in ["move_right", "dash"]:
		if not InputMap.has_action(action):
			InputMap.add_action(action)
			added.append(action)
		Input.action_press(action)
	for flight in flights:
		flight._PhysicsProcess(1.0 / 60)
		check(flight.get_parent().velocity == Vector2.ZERO and not flight.IsBoosting, "Unpossessed flight used global input")
	for action in ["move_right", "dash"]: Input.action_release(action)
	for action in added: InputMap.erase_action(action)
	check(flights[1].TryBoost() and not actors[1].CanBecomeDormant(), "Idle boost could unload before movement")
	flights[1]._PhysicsProcess(3.0)
	check(actors[1].CanBecomeDormant(), "Expired boost prevented ambient dormancy")
	actors[0].SetIntent(Vector2(0.5, 0), Vector2.RIGHT, false, false)
	flights[0]._PhysicsProcess(1.0 / 60)
	check(absf(flights[0].get_parent().velocity.x - 150) < 0.01, "Flight lost analog actor movement")
	actors[0].SetDashIntent(true)
	flights[0]._PhysicsProcess(1.0 / 60)
	check(flights[0].IsBoosting and absf(flights[0].get_parent().velocity.x - 225) < 0.01, "Actor boost intent not applied")
	check(not flights[1].IsBoosting, "Boost leaked to another actor")
	flights[0]._PhysicsProcess(3.0)
	check(not flights[0].IsBoosting, "Held intent retriggered boost")
	var body: Node = actors[0].get_parent()
	var dash := make("DashComponent", "Dash", body, {"GrantIFrames": false})
	actors[0].SetDashIntent(false)
	actors[0].SetDashIntent(true)
	flights[0]._PhysicsProcess(1.0 / 60)
	check(not flights[0].IsBoosting, "Flight consumed dedicated dash intent")
	dash._PhysicsProcess(1.0 / 60)
	check(dash.IsDashing, "Dedicated dash did not receive shared intent")
	dash.CancelDash()
	var follower := make("grid/GridPathFollowerComponent", "Follower", body, {"GridPath": NodePath("../../Grid"), "NavigationPath": NodePath("../../Navigation"), "SetZIndexFromY": false})
	follower.MoveToCell(Vector2i(20, 20))
	check(not flights[0].TryBoost(), "Boost competed with grid movement")
	var before: Vector2 = body.global_position
	flights[0]._PhysicsProcess(1.0 / 60)
	check(body.global_position == before, "Flight integrated while follower owned actor")
	follower.CancelMove()
	var replacement := CharacterBody2D.new()
	host.add_child(replacement)
	flights[0].reparent(replacement)
	flights[0].set_physics_process(false)
	check(flights[0].TryBoost(), "Reattached flight retained prior actor/body state")
	host.free()
	print("[actor-flight] OK" if failures.is_empty() else "[actor-flight] FAILED")
	quit(0 if failures.is_empty() else 1)
