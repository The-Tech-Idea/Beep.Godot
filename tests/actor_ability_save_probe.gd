extends SceneTree

const ECS := "res://addons/beep_game_builder_cs/ecs/"
var failures: Array[String] = []
var dash_events := 0
var hover_events := 0
var knock_events := 0

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

func snapshot(actor: Node) -> Dictionary:
	return JSON.parse_string(JSON.stringify(actor.CaptureActor()))

func ability(saved: Dictionary, component: String, key: String) -> Dictionary:
	return JSON.parse_string(saved.components[component]).game_data["ability." + key]

func _initialize() -> void: run.call_deferred()

func run() -> void:
	var host := Node2D.new()
	root.add_child(host)
	make("actors/ActorRegistryComponent", "Registry", host)
	var body := CharacterBody2D.new()
	body.position = Vector2(100, 80)
	var shape := CollisionShape2D.new()
	shape.shape = RectangleShape2D.new()
	shape.shape.size = Vector2(20, 40)
	body.add_child(shape)
	host.add_child(body)
	var ground := StaticBody2D.new()
	ground.position = Vector2(300, 120)
	var floor_shape := CollisionShape2D.new()
	floor_shape.shape = RectangleShape2D.new()
	floor_shape.shape.size = Vector2(1000, 20)
	ground.add_child(floor_shape)
	host.add_child(ground)
	var definition: Resource = load(ECS + "actors/ActorDefinition.cs").new()
	var actor := make("actors/ActorComponent", "Actor", body, {"ActorId": "unit", "RegistryPath": NodePath("../../Registry"), "Definition": definition})
	var jump := make("JumpComponent", "Jump", body)
	var control := make("PlatformerController", "Controller", body)
	var dash := make("DashComponent", "Dash", body, {"DashDuration": 0.3, "DashCooldown": 1.0})
	var hover := make("HoverComponent", "Hover", body, {"MaxHoverTime": 0.4})
	var knock := make("KnockbackComponent", "Knock", body)
	dash.DashStarted.connect(func(_direction): dash_events += 1)
	hover.HoverStarted.connect(func(): hover_events += 1)
	knock.KnockedBack.connect(func(_direction, _strength): knock_events += 1)
	await physics_frame
	await process_frame
	for frame in 10:
		body.velocity = Vector2(0, 300)
		body.move_and_slide()
	check(body.is_on_floor(), "Fixture did not establish floor contact")
	actor.SetIntent(Vector2.ZERO, Vector2.RIGHT, false, true)
	jump._PhysicsProcess(1.0 / 60)
	body.move_and_slide()
	var jumping := snapshot(actor)
	check(not jumping.on_floor and ability(jumping, "Jump", "jump").remaining == 1, "Fixture did not capture an airborne single-jump state")
	# Load the airborne save over a body whose native contact flag now says grounded.
	body.position = Vector2(100, 80)
	for frame in 10:
		body.velocity = Vector2(0, 300)
		body.move_and_slide()
	check(body.is_on_floor(), "Fixture did not restore stale ground contact")
	actor.RestoreActor(jumping)
	check(body.velocity == Vector2(jumping.velocity_x, jumping.velocity_y), "Actor restore lost native velocity")
	actor.SetIntent(Vector2.ZERO, Vector2.RIGHT, false, true)
	jump._PhysicsProcess(1.0 / 60)
	check(ability(snapshot(actor), "Jump", "jump").remaining == 0, "Stale native floor contact refilled saved jump count")
	control._PhysicsProcess(1.0 / 60)
	actor.ClearIntent()
	body.velocity = Vector2(120, 170)
	check(dash.TryDash(Vector2.RIGHT), "Dash fixture did not activate")
	dash._PhysicsProcess(0.1)
	var dashing := snapshot(actor)
	dash.CancelDash()
	dash.ResetCooldown()
	actor.RestoreActor(dashing)
	check(dash.IsDashing and dash.IsInvincible and dash.IsOnCooldown and dash_events == 1, "Dash restore lost protection/cooldown or replayed activation")
	check(absf(ability(snapshot(actor), "Dash", "dash").remaining - 0.2) < 0.001, "Dash restore restarted its duration")
	dash._PhysicsProcess(0.21)
	var cooling := snapshot(actor)
	dash.ResetCooldown()
	actor.RestoreActor(cooling)
	check(not dash.TryDash(Vector2.LEFT) and not dash.IsInvincible, "Load cleared dash cooldown or retained expired protection")
	dash.IsActive = false
	body.position.y = -100
	body.velocity = Vector2(0, 200)
	control._PhysicsProcess(1.0 / 60)
	actor.SetAbilityHeld("jump", true)
	hover._PhysicsProcess(0.25)
	var hovering := snapshot(actor)
	check(hover.IsHovering and absf(ability(hovering, "Hover", "hover").elapsed - 0.25) < 0.001, "Hover fixture did not consume airborne budget")
	hover._PhysicsProcess(0.2)
	hover._PhysicsProcess(0.01)
	actor.RestoreActor(hovering)
	check(not actor.IsAbilityHeld("jump"), "Load replayed held hardware input")
	actor.SetAbilityHeld("jump", true)
	hover._PhysicsProcess(0.2)
	hover._PhysicsProcess(0.01)
	check(not hover.IsHovering and hover_events == 1, "Hover load granted a fresh budget or replayed activation")
	actor.ClearIntent()
	knock.ApplyKnockback(body.global_position + Vector2.RIGHT)
	knock._PhysicsProcess(0.05)
	var knocked := snapshot(actor)
	knock._PhysicsProcess(1.0)
	actor.RestoreActor(knocked)
	check(knock.IsKnockedBack and absf(knock.CurrentImpulse.x + 170) < 0.01 and knock_events == 1, "Knockback restore lost decayed impulse or replayed hit")
	var flyer_body := CharacterBody2D.new()
	flyer_body.position = Vector2(700, -100)
	var bank_sprite := Sprite2D.new()
	flyer_body.add_child(bank_sprite)
	host.add_child(flyer_body)
	var flyer := make("actors/ActorComponent", "Actor", flyer_body, {"ActorId": "flyer", "RegistryPath": NodePath("../../Registry"), "Definition": definition})
	var flight := make("FlyComponent", "Flight", flyer_body)
	flyer.SetIntent(Vector2.RIGHT, Vector2.RIGHT, false, false)
	check(flight.TryBoost(), "Flight fixture did not activate")
	flight._PhysicsProcess(0.3)
	var flying := snapshot(flyer)
	var replacement_body := CharacterBody2D.new()
	var replacement_sprite := Sprite2D.new()
	replacement_body.add_child(replacement_sprite)
	host.add_child(replacement_body)
	var replacement := make("actors/ActorComponent", "Actor", replacement_body, {"ActorId": "replacement", "RegistryPath": NodePath("../../Registry"), "Definition": definition})
	var replacement_flight := make("FlyComponent", "Flight", replacement_body)
	replacement.RestoreActor(flying)
	check(replacement_flight.IsBoosting and replacement_body.velocity == flyer_body.velocity and replacement_sprite.skew == bank_sprite.skew, "Fresh actor did not restore boost, velocity and visual bank")
	flight._PhysicsProcess(3.0)
	flyer.RestoreActor(flying)
	check(flight.IsBoosting and absf(ability(snapshot(flyer), "Flight", "flight").boost - 1.7) < 0.001, "Flight restore lost remaining boost")
	flight._PhysicsProcess(1.8)
	check(not flight.IsBoosting, "Flight load extended boost duration")
	var validation: Node = load("res://tests/CoreGameplaySmoke.cs").new()
	host.add_child(validation)
	check(validation.RunAbilitySaveValidationCheck(), "Malformed ability data was accepted or mutated live state")
	host.free()
	print("[actor-ability-save] OK" if failures.is_empty() else "[actor-ability-save] FAILED")
	quit(0 if failures.is_empty() else 1)
