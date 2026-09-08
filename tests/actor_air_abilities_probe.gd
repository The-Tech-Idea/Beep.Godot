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
	var player := make("actors/PlayerContextComponent", "Player", host, {"RegistryPath": NodePath("../Registry"), "ControlMode": 0})
	var definition: Resource = load(ECS + "actors/ActorDefinition.cs").new()
	var body := CharacterBody2D.new()
	body.position = Vector2(100, 100)
	host.add_child(body)
	var actor := make("actors/ActorComponent", "Actor", body, {"ActorId": "unit", "OwnerId": "player_1", "RegistryPath": NodePath("../../Registry"), "Definition": definition})
	var control := make("PlatformerController", "Controller", body)
	var glide := make("GlideComponent", "Glide", body, {"GlideAction": "parachute"})
	var hover := make("HoverComponent", "Hover", body, {"HoverAction": "jetpack", "MaxHoverTime": 0.05, "HoverCooldown": 0.0})
	var added := []
	for action in ["parachute", "jetpack"]:
		if not InputMap.has_action(action):
			InputMap.add_action(action)
			added.append(action)
		Input.action_press(action)
	body.velocity = Vector2(0, 200)
	glide._PhysicsProcess(1.0 / 60)
	hover._PhysicsProcess(1.0 / 60)
	check(not glide.IsGliding and not hover.IsHovering, "Unpossessed actor used global air-ability input")
	check(player.Possess("unit"), "Possession failed")
	for action in ["parachute", "jetpack"]:
		var event := InputEventAction.new()
		event.action = action
		event.pressed = true
		player._UnhandledInput(event)
	player._PhysicsProcess(1.0 / 60)
	check(actor.IsAbilityHeld("parachute") and actor.IsAbilityHeld("jetpack"), "Custom ability actions did not route through player")
	actor.SetIntent(Vector2(0.5, 0), Vector2.RIGHT, false, false)
	body.velocity = Vector2(0, 200)
	glide._PhysicsProcess(1.0 / 60)
	check(glide.IsGliding, "Actor glide did not start")
	control._PhysicsProcess(1.0 / 60)
	check(absf(body.velocity.y - 40) < 0.01 and absf(body.velocity.x - 10) < 0.01, "Motor overwrote glide descent or air acceleration")
	hover._PhysicsProcess(1.0 / 60)
	control._PhysicsProcess(1.0 / 60)
	check(hover.IsHovering and absf(body.velocity.y - 30) < 0.01, "Motor overwrote hover limit")
	for frame in 10: hover._PhysicsProcess(1.0 / 60)
	check(not hover.IsHovering, "Hover exceeded airborne time budget")
	for action in ["parachute", "jetpack"]: Input.action_release(action)
	player._PhysicsProcess(1.0 / 60)
	glide._PhysicsProcess(1.0 / 60)
	check(not glide.IsGliding and not actor.IsAbilityHeld("parachute"), "Consumed release left ability held")
	actor.SetAbilityHeld("parachute", true)
	body.velocity = Vector2(0, 200)
	glide._PhysicsProcess(1.0 / 60)
	actor.CancelOrders()
	check(not glide.IsGliding and not hover.IsHovering, "Order replacement retained air ability")
	actor.ClearIntent()
	var replacement := CharacterBody2D.new()
	host.add_child(replacement)
	var other := make("actors/ActorComponent", "Actor", replacement, {"ActorId": "other", "OwnerId": "player_1", "RegistryPath": NodePath("../../Registry"), "Definition": definition})
	glide.reparent(replacement)
	hover.reparent(replacement)
	glide.set_physics_process(false)
	hover.set_physics_process(false)
	other.SetAbilityHeld("parachute", true)
	other.SetAbilityHeld("jetpack", true)
	replacement.velocity = Vector2(0, 200)
	glide._PhysicsProcess(1.0 / 60)
	hover._PhysicsProcess(1.0 / 60)
	check(glide.IsGliding and hover.IsHovering, "Reattached ability retained old body or exhausted hover budget")
	check(not other.CanBecomeDormant(), "Active air ability allowed dormancy")
	glide.IsActive = false
	hover.IsActive = false
	check(not glide.IsGliding and not hover.IsHovering, "Deactivated abilities remained effective")
	for action in added: InputMap.erase_action(action)
	host.free()
	print("[actor-air-abilities] OK" if failures.is_empty() else "[actor-air-abilities] FAILED")
	quit(0 if failures.is_empty() else 1)
