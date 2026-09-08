extends SceneTree

const ECS := "res://addons/beep_game_builder_cs/ecs/"
var failures: Array[String] = []
var slide_starts := 0
var wall_jumps := 0
var glide_starts := 0
var jumps := 0

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

func obstacle(parent: Node, position: Vector2, size: Vector2) -> StaticBody2D:
	var node := StaticBody2D.new()
	node.position = position
	var shape := RectangleShape2D.new()
	shape.size = size
	collider(node, shape)
	parent.add_child(node)
	return node

func snapshot(actor: Node) -> Dictionary:
	return JSON.parse_string(JSON.stringify(actor.CaptureActor()))

func ability(saved: Dictionary, component: String, key: String) -> Dictionary:
	return JSON.parse_string(saved.components[component]).game_data["ability." + key]

func _initialize() -> void: run.call_deferred()

func run() -> void:
	var host := Node2D.new()
	root.add_child(host)
	make("actors/ActorRegistryComponent", "Registry", host)
	obstacle(host, Vector2(300, 120), Vector2(1000, 20))
	var definition: Resource = load(ECS + "actors/ActorDefinition.cs").new()
	var shared := RectangleShape2D.new()
	shared.size = Vector2(20, 40)
	var body := CharacterBody2D.new()
	body.position = Vector2(100, 80)
	var shape := collider(body, shared)
	host.add_child(body)
	var actor := make("actors/ActorComponent", "Actor", body, {"ActorId": "unit", "RegistryPath": NodePath("../../Registry"), "Definition": definition})
	var control := make("PlatformerController", "Controller", body)
	var slide := make("SlideComponent", "Slide", body)
	slide.SlideStarted.connect(func(): slide_starts += 1)
	control.Jumped.connect(func(): jumps += 1)
	await physics_frame
	await process_frame
	for frame in 10:
		body.velocity = Vector2(0, 300)
		body.move_and_slide()
	check(body.is_on_floor(), "Slide fixture did not reach ground")
	var idle := snapshot(actor)
	body.velocity = Vector2(300, 0)
	check(slide.TrySlide(), "Slide fixture did not start")
	slide._PhysicsProcess(0.1)
	var sliding := snapshot(actor)
	for repeat in 3:
		actor.RestoreActor(sliding)
		check(slide.IsSliding and shape.shape.size == Vector2(20, 20) and shape.position == Vector2(0, 10), "Slide load compounded collision reduction")
	check(slide_starts == 1 and shared.size == Vector2(20, 40), "Slide load replayed activation or modified shared shape")
	check(absf(ability(snapshot(actor), "Slide", "slide").remaining - 0.5) < 0.001, "Slide load restarted duration")
	await physics_frame
	control._PhysicsProcess(1.0 / 60)
	check(absf(body.velocity.x - 240) < 0.01, "Saved slide momentum was overwritten")
	var roof := obstacle(host, body.position + Vector2(0, -14), Vector2(80, 8))
	await physics_frame
	await process_frame
	actor.CancelOrders()
	slide._PhysicsProcess(1.0 / 60)
	var crouched := snapshot(actor)
	check(slide.IsCollisionReduced and not slide.IsSliding, "Fixture did not retain low-ceiling collision")
	var clone_body := CharacterBody2D.new()
	var clone_shape := collider(clone_body, shared)
	host.add_child(clone_body)
	var clone := make("actors/ActorComponent", "Actor", clone_body, {"ActorId": "clone", "RegistryPath": NodePath("../../Registry"), "Definition": definition})
	make("PlatformerController", "Controller", clone_body)
	var clone_slide := make("SlideComponent", "Slide", clone_body)
	clone.RestoreActor(crouched)
	body.position.x -= 500
	await physics_frame
	await process_frame
	clone_slide._PhysicsProcess(1.0 / 60)
	check(clone_slide.IsCollisionReduced and not clone_slide.IsSliding and clone_shape.shape != shared, "Fresh actor did not restore pending standing clearance")
	clone_body.position.x += 200
	clone_slide._PhysicsProcess(1.0 / 60)
	check(not clone_slide.IsCollisionReduced and clone_shape.shape == shared and clone_shape.position == Vector2.ZERO, "Loaded crouch did not restore authored shape after clearing ceiling")
	roof.free()
	actor.RestoreActor(idle)
	check(shape.shape == shared and shape.position == Vector2.ZERO and not slide.IsSliding, "Idle load retained previous collision reduction")
	# Built-in jump coyote time and buffered input survive without a JumpComponent.
	await physics_frame
	control._PhysicsProcess(1.0 / 60)
	body.position.y = -100
	body.velocity = Vector2(0, -100)
	body.move_and_slide()
	control._PhysicsProcess(0.03)
	var coyote := snapshot(actor)
	check(ability(coyote, "Controller", "platformer").coyote > 0, "Fixture did not retain coyote time")
	control._PhysicsProcess(0.2)
	actor.RestoreActor(coyote)
	actor.SetIntent(Vector2.ZERO, Vector2.RIGHT, false, true)
	control._PhysicsProcess(1.0 / 60)
	check(jumps == 1 and body.velocity.y == control.EffectiveJumpVelocity, "Load lost built-in coyote jump")
	actor.ClearIntent()
	control._PhysicsProcess(0.2)
	actor.SetIntent(Vector2.ZERO, Vector2.RIGHT, false, true)
	control._PhysicsProcess(1.0 / 60)
	var buffered := snapshot(actor)
	check(ability(buffered, "Controller", "platformer").buffer > 0, "Fixture did not queue a jump")
	actor.RestoreActor(buffered)
	body.position = Vector2(100, 80)
	for frame in 10:
		body.velocity = Vector2(0, 300)
		body.move_and_slide()
	control._PhysicsProcess(1.0 / 60)
	control._PhysicsProcess(1.0 / 60)
	check(jumps == 2, "Loaded buffered jump did not fire on landing")
	# Native ray contact, then save inside the launch signal's pre-integration window.
	var wall_body := CharacterBody2D.new()
	wall_body.position = Vector2(900, 0)
	host.add_child(wall_body)
	var wall_actor := make("actors/ActorComponent", "Actor", wall_body, {"ActorId": "wall", "RegistryPath": NodePath("../../Registry"), "Definition": definition})
	var wall_control := make("PlatformerController", "Controller", wall_body)
	var wall := make("WallJumpComponent", "Wall", wall_body)
	wall.WallJumped.connect(func(_direction): wall_jumps += 1)
	obstacle(host, Vector2(910, 0), Vector2(8, 200))
	await physics_frame
	await process_frame
	wall_body.velocity = Vector2(0, 200)
	wall._PhysicsProcess(1.0 / 60)
	var sticking := snapshot(wall_actor)
	check(wall.IsWallSliding, "Wall fixture missed contact")
	wall.CancelWallMotion()
	wall_actor.RestoreActor(sticking)
	wall_body.position.x = 800
	wall_actor.SetIntent(Vector2.ZERO, Vector2.RIGHT, false, true)
	wall._PhysicsProcess(1.0 / 60)
	var launching := snapshot(wall_actor)
	check(wall_jumps == 1 and ability(launching, "Wall", "wall_jump").launch_pending, "Saved stick time did not support the wall jump")
	wall_actor.RestoreActor(launching)
	await physics_frame
	wall_control._PhysicsProcess(1.0 / 60)
	check(wall_body.velocity == Vector2(-350, -400), "Deferred saved wall launch lost its kick")
	var integrated := snapshot(wall_actor)
	check(not ability(integrated, "Wall", "wall_jump").launch_pending, "Integrated wall launch remained pending")
	wall_actor.RestoreActor(integrated)
	await physics_frame
	wall._PhysicsProcess(1.0 / 60)
	wall_control._PhysicsProcess(1.0 / 60)
	check(wall_body.velocity.x == -350 and wall_body.velocity.y > -400 and wall_jumps == 1, "Load replayed wall launch instead of continuing gravity/lock")
	var glider_body := CharacterBody2D.new()
	glider_body.position = Vector2(1200, -100)
	host.add_child(glider_body)
	var glider := make("actors/ActorComponent", "Actor", glider_body, {"ActorId": "glider", "RegistryPath": NodePath("../../Registry"), "Definition": definition})
	var glide_control := make("PlatformerController", "Controller", glider_body)
	var glide := make("GlideComponent", "Glide", glider_body)
	glide.GlideStarted.connect(func(): glide_starts += 1)
	glider.SetIntent(Vector2.RIGHT, Vector2.RIGHT, false, true)
	glider_body.velocity = Vector2(100, 200)
	glide._PhysicsProcess(1.0 / 60)
	var gliding := snapshot(glider)
	glide.CancelGlide()
	glider.RestoreActor(gliding)
	glide_control._PhysicsProcess(1.0 / 60)
	check(glide.IsGliding and glider_body.velocity == Vector2(110, 40) and glide_starts == 1, "Load lost glide velocity or replayed its activation")
	glide._PhysicsProcess(1.0 / 60)
	check(not glide.IsGliding, "Saved glide replayed held input")
	host.free()
	print("[actor-contact-ability-save] OK" if failures.is_empty() else "[actor-contact-ability-save] FAILED")
	quit(0 if failures.is_empty() else 1)
