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
	var grid := make("grid/GridProjectionComponent", "Grid", host, {"DrawGrid": false})
	var nav := make("grid/GridNavigationComponent", "Navigation", host, {"GridPath": NodePath("../Grid"), "BoundsSize": Vector2i(32, 32), "PathExpansionsPerFrame": 16})
	var definition: Resource = load(ECS + "actors/ActorDefinition.cs").new()
	var body := CharacterBody2D.new()
	body.position = grid.CellToWorld(Vector2i(4, 4))
	host.add_child(body)
	var actor := make("actors/ActorComponent", "Actor", body, {"ActorId": "unit", "OwnerId": "player_1", "RegistryPath": NodePath("../../Registry"), "Definition": definition})
	var control := make("TopDownController", "Controller", body)
	var dash := make("DashComponent", "Dash", body, {"DashSpeed": 600.0, "DashDuration": 0.3, "GrantIFrames": false})
	var knock := make("KnockbackComponent", "Knockback", body, {"Strength": 300.0, "Friction": 0.0, "Duration": 0.3})
	var follower := make("grid/GridPathFollowerComponent", "Follower", body, {"GridPath": NodePath("../../Grid"), "NavigationPath": NodePath("../../Navigation"), "SetZIndexFromY": false})
	var added := not InputMap.has_action("dash")
	if added: InputMap.add_action("dash")
	Input.action_press("dash")
	dash._PhysicsProcess(1.0 / 60)
	check(not dash.IsDashing, "Unpossessed actor consumed global dash input")
	Input.action_release("dash")
	if added: InputMap.erase_action("dash")
	actor.SetIntent(Vector2.RIGHT, Vector2.RIGHT, false, false)
	actor.SetDashIntent(true)
	dash._PhysicsProcess(1.0 / 60)
	check(dash.IsDashing, "Actor dash intent not consumed")
	body.velocity = Vector2(0, 150)
	for frame in 3:
		var before := body.global_position
		control._PhysicsProcess(1.0 / 60)
		check(absf(body.global_position.x - before.x - 10.0) < 0.05, "Dash overwritten or body integrated twice")
		check(absf(body.velocity.x - 600.0) < 0.01, "Controller replaced dash velocity")
		check(absf(body.velocity.y) < 0.01, "Top-down horizontal dash retained perpendicular drift")
		dash._PhysicsProcess(1.0 / 60)
	check(not dash.TryDash(Vector2.LEFT), "Dash ignored cooldown")
	knock.ApplyKnockback(body.global_position + Vector2.RIGHT)
	for frame in 3:
		var before := body.global_position
		knock._PhysicsProcess(1.0 / 60)
		check(body.global_position == before, "Knockback integrated alongside active controller")
		control._PhysicsProcess(1.0 / 60)
		check(absf(body.global_position.x - before.x + 5.0) < 0.05, "Knockback accumulated, lost priority or integrated twice")
	actor.CancelOrders()
	check(not dash.IsDashing, "Replacing orders did not cancel dash")
	actor.ClearIntent()
	control.IsActive = false
	var before := body.global_position
	knock._PhysicsProcess(1.0 / 60)
	check(absf(body.global_position.x - before.x + 5.0) < 0.05, "Controller-less knockback stopped integrating")
	follower.MoveToCell(Vector2i(28, 28))
	check(not dash.TryDash(Vector2.RIGHT), "Dash competed with scheduled path follower")
	before = body.global_position
	knock._PhysicsProcess(1.0 / 60)
	check(body.global_position == before, "Knockback integrated alongside pending follower")
	follower.AdvancePath(1.0 / 60)
	check(absf(body.global_position.x - before.x + 5.0) < 0.05 and not follower.HasReachedDestination, "Follower lost knockback or reported false arrival")
	control.IsActive = true
	follower.DriveCharacterBody = false
	before = body.global_position
	knock._PhysicsProcess(1.0 / 60)
	follower.AdvancePath(1.0 / 60)
	control._PhysicsProcess(1.0 / 60)
	check(absf(body.global_position.x - before.x + 5.0) < 0.05, "Direct follower or suspended controller stole knockback integration")
	follower.CancelMove()
	var new_body := CharacterBody2D.new()
	new_body.position = Vector2(900, 900)
	host.add_child(new_body)
	dash.reparent(new_body)
	dash.set_physics_process(false)
	check(dash.TryDash(Vector2.UP), "Reattached dash retained old body/cooldown")
	dash.CancelDash()
	knock.reparent(new_body)
	knock.set_physics_process(false)
	before = new_body.global_position
	knock.ApplyKnockback(before + Vector2.RIGHT)
	knock._PhysicsProcess(1.0 / 60)
	check(absf(new_body.global_position.x - before.x + 5.0) < 0.05, "Reattached knockback moved old body")
	var protection: Node = load("res://tests/CoreGameplaySmoke.cs").new()
	host.add_child(protection)
	check(protection.RunDashProtectionCheck(), "Dash protection erased another buff or survived cancellation")
	host.free()
	print("[actor-motion-abilities] OK" if failures.is_empty() else "[actor-motion-abilities] FAILED")
	quit(0 if failures.is_empty() else 1)
