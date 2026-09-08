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
	var grid := make("grid/GridProjectionComponent", "Grid", host, {"DrawGrid": false})
	var nav := make("grid/GridNavigationComponent", "Navigation", host, {"GridPath": NodePath("../Grid"), "BoundsSize": Vector2i(32, 32)})
	var body := CharacterBody2D.new()
	body.position = grid.CellToWorld(Vector2i(4, 4))
	host.add_child(body)
	var definition: Resource = load(ECS + "actors/ActorDefinition.cs").new()
	var actor := make("actors/ActorComponent", "Actor", body, {"ActorId": "flock", "RegistryPath": NodePath("../../Registry"), "Definition": definition})
	var control := make("TopDownController", "Controller", body)
	var flock := make("algorithms/FlockingComponent", "Flock", body, {"FlockGroup": "probe_flock", "SteerLerp": 0.0})
	var knock := make("KnockbackComponent", "Knock", body, {"Strength": 120.0, "Friction": 0.0, "Duration": 0.3})
	var follower := make("grid/GridPathFollowerComponent", "Follower", body, {"GridPath": NodePath("../../Grid"), "NavigationPath": NodePath("../../Navigation"), "SetZIndexFromY": false})
	await physics_frame
	var before := body.position
	body.velocity = Vector2(60, 0)
	flock._PhysicsProcess(1.0 / 60)
	check(body.position == before and body.velocity == Vector2(60, 0), "Flock competed with first actor motor")
	control.IsActive = false
	check(actor.CanDrive(flock), "Flock did not receive actor movement ownership")
	flock._PhysicsProcess(1.0 / 60)
	check(absf(body.position.x - before.x - 1.0) < 0.05, "Flock failed to resume current momentum once")
	knock.ApplyKnockback(body.position + Vector2.RIGHT)
	before = body.position
	knock._PhysicsProcess(1.0 / 60)
	check(body.position == before, "Knockback integrated alongside flock")
	flock._PhysicsProcess(1.0 / 60)
	check(absf(body.position.x - before.x + 2.0) < 0.05, "Flock bypassed shared knockback integration")
	knock.IsActive = false
	follower.MoveToCell(Vector2i(20, 20))
	before = body.position
	flock._PhysicsProcess(1.0 / 60)
	check(body.position == before and not actor.CanDrive(flock), "Flock ignored pending path ownership")
	actor.CancelOrders()
	body.velocity = Vector2(0, 60)
	flock._PhysicsProcess(1.0 / 60)
	check(absf(body.position.y - before.y - 1.0) < 0.05, "Flock resumed stale steering after path cancellation")
	var neighbor := CharacterBody2D.new()
	neighbor.position = body.position + Vector2(10, 0)
	neighbor.velocity = Vector2(0, -60)
	host.add_child(neighbor)
	neighbor.add_to_group("probe_flock")
	make("actors/ActorComponent", "Actor", neighbor, {"ActorId": "neighbor", "RegistryPath": NodePath("../../Registry"), "Definition": definition})
	var loose := CharacterBody2D.new()
	loose.position = body.position + Vector2(-10, 0)
	loose.velocity = Vector2(0, 120)
	host.add_child(loose)
	loose.add_to_group("probe_flock")
	flock.SteerLerp = 1.0
	flock.SeparationWeight = 0.0
	flock.CohesionWeight = 0.0
	flock.AlignmentWeight = 1.0
	flock._PhysicsProcess(1.0 / 60)
	check(body.velocity.y < -149.0, "Indexed flock ignored registered neighbor or included unregistered member")
	flock.UseActorSpatialIndex = false
	flock._PhysicsProcess(1.0 / 60)
	check(body.velocity.y > 149.0, "Explicit mixed-group mode omitted non-actor flock member")
	var other := CharacterBody2D.new()
	host.add_child(other)
	flock.reparent(other)
	flock.set_physics_process(false)
	flock.SteerLerp = 0.0
	before = body.position
	flock._PhysicsProcess(1.0 / 60)
	check(body.position == before and other.position.length() > 0, "Reattached flock moved old body or failed to bind new body")
	host.free()
	print("[actor-flocking] OK" if failures.is_empty() else "[actor-flocking] FAILED")
	quit(0 if failures.is_empty() else 1)
