extends SceneTree

const ECS := "res://addons/beep_game_builder_cs/ecs/"
var failures: Array[String] = []
var ended := 0

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

func collider(parent: Node, shape: Shape2D) -> CollisionShape2D:
	var node := CollisionShape2D.new()
	node.shape = shape
	parent.add_child(node)
	return node

func _initialize() -> void: run.call_deferred()

func run() -> void:
	var host := Node2D.new()
	root.add_child(host)
	make("actors/ActorRegistryComponent", "Registry", host)
	var ground := StaticBody2D.new()
	ground.position = Vector2(300, 120)
	var ground_shape := RectangleShape2D.new()
	ground_shape.size = Vector2(1000, 20)
	collider(ground, ground_shape)
	host.add_child(ground)
	var shared := RectangleShape2D.new()
	shared.size = Vector2(20, 40)
	var body := CharacterBody2D.new()
	body.position = Vector2(100, 80)
	var shape := collider(body, shared)
	host.add_child(body)
	var definition: Resource = load(ECS + "actors/ActorDefinition.cs").new()
	var actor := make("actors/ActorComponent", "Actor", body, {"ActorId": "unit", "RegistryPath": NodePath("../../Registry"), "Definition": definition})
	var control := make("PlatformerController", "Controller", body)
	var slide := make("SlideComponent", "Slide", body, {"SlideAction": "sneak"})
	slide.SlideEnded.connect(func(): ended += 1)
	var other := CharacterBody2D.new()
	other.position = Vector2(400, 80)
	var other_shape := collider(other, shared)
	host.add_child(other)
	await physics_frame
	await process_frame
	for frame in 10:
		body.velocity = Vector2(0, 300)
		body.move_and_slide()
		other.velocity = Vector2(0, 300)
		other.move_and_slide()
	check(body.is_on_floor(), "Fixture failed to establish native floor contact")
	var added := not InputMap.has_action("sneak")
	if added: InputMap.add_action("sneak")
	Input.action_press("sneak")
	body.velocity = Vector2(300, 0)
	slide._PhysicsProcess(1.0 / 60)
	check(not slide.IsSliding, "Unpossessed actor read global slide input")
	actor.SetAbilityHeld("sneak", true)
	var feet := shape.global_position.y + shared.size.y / 2
	slide._PhysicsProcess(1.0 / 60)
	check(slide.IsSliding, "Actor-held custom slide action did not start")
	check(shape.shape != shared and shared.size == Vector2(20, 40) and other_shape.shape == shared, "Slide mutated the shared collision resource")
	check(absf(shape.global_position.y + shape.shape.size.y / 2 - feet) < 0.01, "Slide moved the feet when shrinking")
	await physics_frame
	var before := body.position.x
	control._PhysicsProcess(1.0 / 60)
	check(absf(body.velocity.x - 300) < 0.01 and absf(body.position.x - before - 5) < 0.01, "Controller overwrote slide or integrated it twice: velocity=%s displacement=%s" % [body.velocity, body.position.x - before])
	slide._PhysicsProcess(1.0 / 60)
	control._PhysicsProcess(1.0 / 60)
	check(absf(body.velocity.x - 290) < 0.01, "Slide deceleration did not use its own momentum")
	slide._PhysicsProcess(1.0)
	check(not slide.IsSliding and shape.shape == shared and shape.position == Vector2.ZERO and ended == 1, "Slide expiry did not restore authored geometry once")
	slide._PhysicsProcess(1.0 / 60)
	check(not slide.IsSliding, "Held slide automatically retriggered")
	actor.SetAbilityHeld("sneak", false)
	slide._PhysicsProcess(1.0 / 60)
	body.velocity = Vector2(300, 0)
	check(slide.TrySlide(), "Explicit NPC slide failed")
	var roof := StaticBody2D.new()
	roof.position = body.position + Vector2(0, -14)
	var roof_shape := RectangleShape2D.new()
	roof_shape.size = Vector2(80, 8)
	collider(roof, roof_shape)
	host.add_child(roof)
	await physics_frame
	await process_frame
	actor.CancelOrders()
	slide._PhysicsProcess(1.0 / 60)
	check(not slide.IsSliding and slide.IsCollisionReduced, "Slide stood up through an obstructing ceiling")
	check(not actor.CanBecomeDormant(), "Reduced collision actor was allowed to unload")
	body.position.x += 150
	slide._PhysicsProcess(1.0 / 60)
	check(not slide.IsCollisionReduced and shape.shape == shared, "Standing clearance did not restore collision")
	body.velocity = Vector2(300, 0)
	check(slide.TrySlide(), "Slide failed before reattachment")
	slide.reparent(other)
	slide.set_physics_process(false)
	check(shape.shape == shared and shape.position == Vector2.ZERO and not slide.IsSliding, "Reattachment left old body shrunk or slide active")
	other.velocity = Vector2(300, 0)
	check(slide.TrySlide() and other_shape.shape != shared, "Reattached slide did not bind the new collision")
	slide.IsActive = false
	slide._PhysicsProcess(1.0 / 60)
	check(not slide.IsSliding and other_shape.shape == shared, "Deactivation retained slide velocity or reduced collision")
	slide.IsActive = true
	other.velocity = Vector2(300, 0)
	check(slide.TrySlide(), "Slide could not restart after deactivation")
	roof.position = other.position + Vector2(0, -14)
	await physics_frame
	await process_frame
	slide.CancelSlide()
	slide._PhysicsProcess(1.0 / 60)
	check(slide.IsCollisionReduced, "Reattached body ignored ceiling clearance")
	other.add_collision_exception_with(roof)
	slide._PhysicsProcess(1.0 / 60)
	check(not slide.IsCollisionReduced and other_shape.shape == shared, "Standing clearance ignored a body collision exception")
	Input.action_release("sneak")
	if added: InputMap.erase_action("sneak")
	host.free()
	print("[actor-slide] OK" if failures.is_empty() else "[actor-slide] FAILED")
	quit(0 if failures.is_empty() else 1)
