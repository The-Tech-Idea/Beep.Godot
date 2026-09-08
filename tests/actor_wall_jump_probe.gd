extends SceneTree

const ECS := "res://addons/beep_game_builder_cs/ecs/"
var failures: Array[String] = []
var events := {"wall": 0, "jump": 0}

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
	var body := CharacterBody2D.new()
	body.position = Vector2(100, 100)
	host.add_child(body)
	var definition: Resource = load(ECS + "actors/ActorDefinition.cs").new()
	var actor := make("actors/ActorComponent", "Actor", body, {"ActorId": "unit", "OwnerId": "player_1", "RegistryPath": NodePath("../../Registry"), "Definition": definition})
	var jump := make("JumpComponent", "Jump", body)
	var control := make("PlatformerController", "Controller", body)
	var wall := make("WallJumpComponent", "WallJump", body)
	wall.WallJumped.connect(func(_direction): events.wall += 1)
	jump.Jumped.connect(func(_remaining): events.jump += 1)
	jump.DoubleJumped.connect(func(): events.jump += 1)
	control.Jumped.connect(func(): events.jump += 1)
	var obstacle := StaticBody2D.new()
	obstacle.position = Vector2(110, 100)
	var collision := CollisionShape2D.new()
	var shape := RectangleShape2D.new()
	shape.size = Vector2(8, 200)
	collision.shape = shape
	obstacle.add_child(collision)
	host.add_child(obstacle)
	await physics_frame
	await process_frame
	check(wall.process_physics_priority < jump.process_physics_priority and jump.process_physics_priority < control.process_physics_priority, "Wall input does not precede jump/controller")
	var added := not InputMap.has_action("jump")
	if added: InputMap.add_action("jump")
	Input.action_press("jump")
	body.velocity = Vector2(0, 200)
	wall._PhysicsProcess(1.0 / 60)
	check(wall.IsWallSliding and events.wall == 0, "Unpossessed wall jump consumed global input or missed wall")
	control._PhysicsProcess(1.0 / 60)
	check(absf(body.velocity.y - wall.EffectiveWallSlideSpeed) < 0.01, "Gravity overwrote wall-slide limit")
	Input.action_release("jump")
	if added: InputMap.erase_action("jump")
	actor.SetIntent(Vector2.RIGHT, Vector2.RIGHT, false, true)
	wall._PhysicsProcess(1.0 / 60)
	jump._PhysicsProcess(1.0 / 60)
	control._PhysicsProcess(1.0 / 60)
	check(events.wall == 1 and events.jump == 0, "One input fired wall and normal jump")
	check(absf(body.velocity.x + wall.EffectiveWallJumpForceX) < 0.01 and absf(body.velocity.y - wall.EffectiveWallJumpForceY) < 0.01, "Controller overwrote wall-jump kick")
	check(not actor.ConsumeJump(), "Wall jump left input pending")
	await physics_frame
	await process_frame
	wall._PhysicsProcess(1.0 / 60)
	control._PhysicsProcess(1.0 / 60)
	check(body.velocity.x == -wall.EffectiveWallJumpForceX and body.velocity.y > wall.EffectiveWallJumpForceY, "Lock lost horizontal kick or froze gravity")
	wall._PhysicsProcess(1.0)
	actor.SetIntent(Vector2.ZERO, Vector2.RIGHT, false, false)
	body.position = Vector2(100, 100)
	body.velocity = Vector2(0, 200)
	wall._PhysicsProcess(1.0 / 60)
	body.position = Vector2(50, 100)
	actor.SetIntent(Vector2.ZERO, Vector2.RIGHT, false, true)
	wall._PhysicsProcess(1.0 / 60)
	check(body.velocity.x == -wall.EffectiveWallJumpForceX, "Wall coyote jump lost last contact direction")
	actor.CancelOrders()
	check(not wall.IsWallJumpLocked and not wall.JumpedThisFrame, "Order replacement left wall-jump motion active")
	actor.SetIntent(Vector2.ZERO, Vector2.RIGHT, false, false)
	actor.SetIntent(Vector2.ZERO, Vector2.RIGHT, false, true)
	wall._PhysicsProcess(1.0 / 60)
	check(actor.ConsumeJump(), "No-contact wall ability stole normal jump input")
	# Same-frame remove/add must not adopt queued-for-deletion generated rays.
	body.remove_child(wall)
	body.add_child(wall)
	wall.set_physics_process(false)
	var new_left: Node = body.get_node("WallRayLeft")
	check(not new_left.is_queued_for_deletion() and not wall.IsWallJumpLocked, "Reattachment reused retired rays/state")
	var replacement := CharacterBody2D.new()
	host.add_child(replacement)
	var authored := RayCast2D.new()
	authored.name = "WallRayLeft"
	authored.target_position = Vector2(-40, 0)
	replacement.add_child(authored)
	wall.reparent(replacement)
	wall.set_physics_process(false)
	check(replacement.get_node("WallRayLeft") == authored and authored.target_position == Vector2(-40, 0), "Reattachment replaced authored ray")
	replacement.remove_child(wall)
	check(not authored.is_queued_for_deletion(), "Removing component deleted authored ray")
	wall.free()
	await process_frame
	check(not body.has_node("WallRayLeft") and not body.has_node("WallRayRight"), "Generated rays remained on old body")
	host.free()
	print("[actor-wall-jump] OK" if failures.is_empty() else "[actor-wall-jump] FAILED")
	quit(0 if failures.is_empty() else 1)
