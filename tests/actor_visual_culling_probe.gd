extends SceneTree

var failures: Array[String] = []

class Counter extends Node:
	var ticks := 0
	func _process(_delta: float) -> void: ticks += 1

func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		push_error(message)

func settle() -> void:
	for i in 6:
		await process_frame
		if DisplayServer.get_name() != "headless": await RenderingServer.frame_post_draw

func _initialize() -> void: run.call_deferred()

func run() -> void:
	var host := Node2D.new()
	root.add_child(host)
	var body := CharacterBody2D.new()
	host.add_child(body)
	var simulation := Counter.new()
	body.add_child(simulation)
	var visuals: Node2D = load("res://addons/beep_game_builder_cs/templates/scenes/actors/actor_visuals.tscn").instantiate()
	body.add_child(visuals)
	var presentation := Counter.new()
	visuals.add_child(presentation)
	var sprite: AnimatedSprite2D = visuals.get_node("AnimatedSprite2D")
	var frames := SpriteFrames.new()
	frames.add_animation("idle")
	for color in [Color.GREEN, Color.YELLOW]:
		var pixels := Image.create_empty(32, 48, false, Image.FORMAT_RGBA8)
		pixels.fill(color)
		frames.add_frame("idle", ImageTexture.create_from_image(pixels))
	sprite.sprite_frames = frames
	sprite.play("idle")
	var player: AnimationPlayer = visuals.get_node("AnimationPlayer")
	var library := AnimationLibrary.new()
	var animation := Animation.new()
	animation.length = 100.0
	library.add_animation("idle", animation)
	player.add_animation_library("", library)
	player.play("idle")
	var camera := Camera2D.new()
	host.add_child(camera)
	camera.force_update_scroll()
	var enabler: VisibleOnScreenEnabler2D = visuals.get_node("Visibility")
	check(enabler.get_node(enabler.enable_node_path) == visuals, "Visibility enabler targets gameplay body instead of visual root")
	check(not visuals.is_ancestor_of(simulation) and visuals.is_ancestor_of(sprite), "Visual and simulation ownership are mixed")
	if DisplayServer.get_name() != "headless":
		await settle()
		check(enabler.is_on_screen() and presentation.ticks > 0, "Native culling did not enable visible presentation")
		camera.position = Vector2(10000, 10000)
		camera.force_update_scroll()
		await settle()
		check(not enabler.is_on_screen() and visuals.process_mode == Node.PROCESS_MODE_DISABLED, "Offscreen visual root still processes")
		var before := presentation.ticks
		var sim_before := simulation.ticks
		var phase := player.current_animation_position
		await settle()
		check(presentation.ticks == before and is_equal_approx(player.current_animation_position, phase), "Offscreen visual animation kept advancing")
		check(simulation.ticks > sim_before and body.process_mode != Node.PROCESS_MODE_DISABLED, "Visual culling suspended simulation")
		camera.position = Vector2.ZERO
		camera.force_update_scroll()
		await settle()
		check(enabler.is_on_screen() and presentation.ticks > before and player.current_animation_position > phase, "Returning camera did not resume native animation")
		paused = true
		before = presentation.ticks
		await settle()
		check(presentation.ticks == before, "Visible presentation ignored SceneTree pause")
		paused = false
		await settle()
		var other := CharacterBody2D.new()
		other.position = Vector2(10000, 10000)
		host.add_child(other)
		visuals.reparent(other, false)
		await settle()
		check(visuals.process_mode == Node.PROCESS_MODE_DISABLED and simulation.ticks > sim_before, "Reattached native enabler retained old visibility/body ownership")
	else:
		print("[actor-visual-culling] native visibility checks require a rendering backend; authored structure checked")
	host.free()
	print("[actor-visual-culling] OK" if failures.is_empty() else "[actor-visual-culling] FAILED")
	quit(0 if failures.is_empty() else 1)
