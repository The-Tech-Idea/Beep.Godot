extends SceneTree

var failures: Array[String] = []

func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		push_error(message)

func settle(count: int = 20) -> void:
	for i in count: await physics_frame

func _initialize() -> void: run.call_deferred()

func run() -> void:
	var scene: Node = load("res://addons/beep_game_builder_cs/templates/scenes/actors/actor_platformer_lab.tscn").instantiate()
	root.add_child(scene)
	await settle()
	var body: CharacterBody2D = scene.get_node("Adventurer")
	var actor: Node = body.get_node("Actor")
	var player: Node = scene.get_node("Player")
	var animation: Node = body.get_node("Animation")
	var sprite: AnimatedSprite2D = body.get_node("Visuals/AnimatedSprite2D")
	check(player.PossessedActorId == "adventurer_1", "Authored player did not possess its owned actor")
	check(body.is_on_floor() and animation.CurrentClip == "idle", "Authored actor did not settle into grounded idle")
	check(InputMap.has_action("move_left") and InputMap.has_action("jump"), "Standalone scene omitted input setup")
	# Exercise the actual player input adapter, not a test movement controller.
	Input.action_press("move_right")
	var event := InputEventAction.new()
	event.action = "move_right"
	event.pressed = true
	player._UnhandledInput(event)
	var start := body.position
	await settle()
	check(body.position.x > start.x + 30 and animation.CurrentClip == "move", "Possessed actor did not move/animate through player input")
	check(sprite.sprite_frames.get_frame_count("move") == 2, "Authored walk frames missing")
	Input.action_release("move_right")
	await settle()
	player.set_physics_process(false)
	actor.SetIntent(Vector2.LEFT, Vector2.LEFT, false, true)
	await settle(6)
	check(body.position.y < -20 and animation.CurrentClip == "jump" and sprite.flip_h, "Jump pose/facing did not follow actor motion")
	actor.SetIntent(Vector2.ZERO, Vector2.RIGHT, false, false)
	await settle(70)
	check(body.is_on_floor(), "Actor failed to land")
	var camera: Camera2D = body.get_node("Camera2D")
	if DisplayServer.get_name() != "headless":
		camera.position.x = 4000
		camera.force_update_scroll()
		await settle()
		check(body.get_node("Visuals").process_mode == Node.PROCESS_MODE_DISABLED, "Authored visual culling did not activate")
		start = body.position
		actor.SetIntent(Vector2.RIGHT, Vector2.RIGHT, false, false)
		await settle()
		check(body.position.x > start.x + 30, "Culled actor stopped simulating")
		actor.SetIntent(Vector2.ZERO, Vector2.RIGHT, false, false)
		camera.position.x = 0
		camera.force_update_scroll()
		await settle()
		check(body.get_node("Visuals").process_mode != Node.PROCESS_MODE_DISABLED, "Visuals failed to resume")
		await RenderingServer.frame_post_draw
		DirAccess.make_dir_recursive_absolute("res://tests/output/actors")
		root.get_texture().get_image().save_png("res://tests/output/actors/platformer.png")
	scene.free()
	print("[actor-platformer-scene] OK" if failures.is_empty() else "[actor-platformer-scene] FAILED")
	quit(0 if failures.is_empty() else 1)
