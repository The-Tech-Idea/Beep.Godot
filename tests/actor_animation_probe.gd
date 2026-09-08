extends SceneTree

const ECS := "res://addons/beep_game_builder_cs/ecs/"
var failures: Array[String] = []

class WorkerStub extends Node:
	var IsWorking := true
	var CurrentJobId := "job"

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
	node.set_process(false)
	return node

func texture(size: Vector2i) -> ImageTexture:
	var pixels := Image.create_empty(size.x, size.y, false, Image.FORMAT_RGBA8)
	pixels.fill(Color.GREEN)
	return ImageTexture.create_from_image(pixels)

func frames() -> SpriteFrames:
	var result := SpriteFrames.new()
	for clip in ["idle", "idle_left", "move_left", "move_down", "move_down_right", "attack_left", "hurt", "death", "work", "jump", "fall", "dash", "slide", "glide", "hover", "fly", "interact"]:
		result.add_animation(clip)
		result.add_frame(clip, texture(Vector2i(8, 16)))
		result.add_frame(clip, texture(Vector2i(16, 32)))
		result.set_animation_loop(clip, clip not in ["attack_left", "hurt", "death", "interact"])
	return result

func _initialize() -> void: run.call_deferred()

func run() -> void:
	var host := Node2D.new()
	root.add_child(host)
	make("actors/ActorRegistryComponent", "Registry", host)
	var body := CharacterBody2D.new()
	host.add_child(body)
	var definition: Resource = load(ECS + "actors/ActorDefinition.cs").new()
	definition.VisualSize = Vector2(32, 48)
	var actor := make("actors/ActorComponent", "Actor", body, {"ActorId": "unit", "RegistryPath": NodePath("../../Registry"), "Definition": definition})
	var sprite := AnimatedSprite2D.new()
	sprite.name = "AnimatedSprite2D"
	sprite.sprite_frames = frames()
	sprite.animation = "idle"
	body.add_child(sprite)
	var health := make("HealthComponent", "Health", body)
	var attack := make("AttackComponent", "Attack", body)
	var profile: Resource = load(ECS + "actors/ActorAnimationProfile.cs").new()
	var topdown: Resource = load("res://addons/beep_game_builder_cs/templates/scenes/actors/animation_topdown.tres")
	var platformer: Resource = load("res://addons/beep_game_builder_cs/templates/scenes/actors/animation_platformer.tres")
	check(topdown.Directions == 2 and not topdown.UseGroundedStates and platformer.Directions == 1 and platformer.UseGroundedStates, "Authored animation profiles did not load")
	profile.Directions = 2
	var animation := make("actors/ActorAnimationComponent", "Animation", body, {"Profile": profile})
	make("actors/ActorPresentationComponent", "Presentation", body, {"SpritePath": NodePath("../AnimatedSprite2D")})
	animation._PhysicsProcess(1.0 / 60)
	check(animation.CurrentClip == "idle" and sprite.is_playing(), "Native sprite idle did not start")
	var scale := sprite.scale
	# Actor JSON saves preserve direction, remaining action time and native sprite phase.
	body.position.x -= 2
	animation._PhysicsProcess(0.01)
	animation.RequestAction(2, 0.5)
	animation._PhysicsProcess(0.1)
	sprite.set_frame_and_progress(1, 0.35)
	sprite.pause()
	var animation_save: Dictionary = JSON.parse_string(JSON.stringify(actor.CaptureActor()))
	body.position = Vector2(900, 900)
	animation.RefreshBindings()
	actor.RestoreActor(animation_save)
	check(animation.Facing == Vector2.LEFT and animation.CurrentClip == "attack_left", "Animation save lost facing/action")
	check(sprite.frame == 1 and is_equal_approx(sprite.frame_progress, 0.35) and not sprite.is_playing(), "Animation save lost paused sprite phase")
	animation._PhysicsProcess(0.2)
	check(animation.CurrentClip == "attack_left" and sprite.frame == 1, "Restored action restarted or expired early")
	animation._PhysicsProcess(0.21)
	animation._PhysicsProcess(0.01)
	check(animation.CurrentClip == "idle_left", "Restored action timer did not expire or load created movement")
	animation.RefreshBindings()
	animation._PhysicsProcess(0.01)
	sprite.frame = 1
	check(scale == Vector2(1.5, 1.5) and sprite.scale == scale and absf(sprite.position.y + 16 * scale.y) < 0.01, "Animated fitting changed scale or detached feet")
	body.position.x -= 10
	animation._PhysicsProcess(1.0 / 60)
	check(animation.CurrentClip == "move_left", "Movement did not choose the directional clip")
	sprite.set_frame_and_progress(1, 0.4)
	body.position.x -= 10
	animation._PhysicsProcess(1.0 / 60)
	check(sprite.frame == 1 and absf(sprite.frame_progress - 0.4) < 0.001, "Repeated movement restarted native playback")
	actor.SetIntent(Vector2.RIGHT, Vector2.RIGHT, false, false)
	animation._PhysicsProcess(1.0 / 60)
	check(animation.CurrentClip == "idle_left", "Blocked/no-displacement actor animated walking or lost facing")
	profile.Directions = 3
	body.position += Vector2(10, 10)
	animation._PhysicsProcess(0.01)
	check(animation.CurrentClip == "move_down_right", "Eight-way movement chose the wrong direction")
	profile.Directions = 2
	attack.Attack(body.global_position + Vector2(-20, 0))
	animation._PhysicsProcess(0.01)
	check(animation.CurrentClip == "attack_left", "Real attack signal did not drive animation")
	sprite.frame = 1
	attack._Process(1.0)
	attack.Attack(body.global_position + Vector2(-20, 0))
	animation._PhysicsProcess(0.01)
	check(sprite.frame == 0, "A new attack did not restart the action clip")
	health.emit_signal("Damaged", 1.0, 99.0)
	animation._PhysicsProcess(0.01)
	check(animation.CurrentClip == "hurt", "Hurt did not override attack or use non-directional fallback")
	health.CurrentHealth = 0
	animation._PhysicsProcess(0.01)
	check(animation.CurrentClip == "death", "Death did not override transient actions")
	sprite.pause()
	sprite.frame = 1
	animation._PhysicsProcess(0.01)
	check(not sprite.is_playing() and sprite.frame == 1, "Death clip was restarted every tick")
	health.CurrentHealth = 100
	animation._PhysicsProcess(1.0)
	profile.UseGroundedStates = true
	body.velocity = Vector2(0, -100)
	animation._PhysicsProcess(0.01)
	check(animation.CurrentClip == "jump", "Side-view ascent was not selected")
	body.velocity = Vector2(0, 100)
	animation._PhysicsProcess(0.01)
	check(animation.CurrentClip == "fall", "Side-view descent was not selected")
	profile.UseGroundedStates = false
	profile.Directions = 1
	animation._PhysicsProcess(0.01)
	check(sprite.flip_h, "Horizontal facing did not flip only the sprite")
	check(body.scale == Vector2.ONE, "Presentation changed body/collision scale")
	# A real worker remains idle while its path request is pending, then works at arrival.
	var grid := make("grid/GridProjectionComponent", "Grid", host, {"DrawGrid": false, "TrackMouseCell": false})
	var navigation := make("grid/GridNavigationComponent", "Navigation", host, {"GridPath": NodePath("../Grid"), "BoundsSize": Vector2i(5, 1)})
	var jobs := make("grid/GridJobQueueComponent", "Jobs", host)
	body.position = grid.CellToWorld(Vector2i.ZERO)
	var follower := make("grid/GridPathFollowerComponent", "Follower", body, {"GridPath": NodePath("../../Grid"), "NavigationPath": NodePath("../../Navigation"), "DriveCharacterBody": false})
	var worker := make("grid/GridWorkerComponent", "Worker", body, {"GridPath": NodePath("../../Grid"), "JobQueuePath": NodePath("../../Jobs"), "PathFollowerPath": NodePath("../Follower"), "AutoClaimJobs": false})
	animation.RefreshBindings()
	var job: String = jobs.AddJob(Vector2i.ZERO, "clear_land", 100.0, 0)
	check(worker.AssignJob(job), "Worker fixture did not accept job")
	animation._PhysicsProcess(0.01)
	check(animation.CurrentClip == "idle", "Pending path was displayed as working")
	navigation.ProcessPathRequests()
	follower.AdvancePath(0.1)
	worker.Tick(0.1)
	animation._PhysicsProcess(0.01)
	check(worker.IsWorking and animation.CurrentClip == "work", "Work animation did not follow real worker state")
	worker.AdvanceWork(200.0)
	# Rebind to an authored native AnimationPlayer.
	var player := AnimationPlayer.new()
	player.name = "AnimationPlayer"
	var library := AnimationLibrary.new()
	for clip in ["idle", "move", "attack", "hurt", "death"]:
		var native := Animation.new()
		native.length = 1.0
		native.loop_mode = Animation.LOOP_LINEAR if clip in ["idle", "move"] else Animation.LOOP_NONE
		library.add_animation(clip, native)
	player.add_animation_library("", library)
	body.add_child(player)
	animation.AnimationTargetPath = NodePath("../AnimationPlayer")
	animation.RefreshBindings()
	animation._PhysicsProcess(0.01)
	check(player.current_animation == "idle", "AnimationPlayer idle failed")
	body.position.x += 1
	animation._PhysicsProcess(0.01)
	player.advance(0.1)
	body.position.x += 1
	animation._PhysicsProcess(0.01)
	check(player.current_animation == "move" and player.current_animation_position > 0.09, "AnimationPlayer movement restarted playback")
	animation.RequestAction(2, 0.6)
	animation._PhysicsProcess(0.01)
	player.advance(0.23)
	var player_save: Dictionary = JSON.parse_string(JSON.stringify(actor.CaptureActor()))
	player.stop()
	actor.RestoreActor(player_save)
	check(player.current_animation == "attack" and is_equal_approx(player.current_animation_position, 0.23), "AnimationPlayer save lost playback position")
	animation._PhysicsProcess(0.01)
	check(is_equal_approx(player.current_animation_position, 0.23), "First restored tick restarted AnimationPlayer")
	player.pause()
	player_save = JSON.parse_string(JSON.stringify(actor.CaptureActor()))
	player.stop()
	actor.RestoreActor(player_save)
	check(not player.is_playing() and is_equal_approx(player.current_animation_position, 0.23), "Paused AnimationPlayer save lost phase")
	# The tree owns transitions; the adapter only requests existing root states.
	var tree := AnimationTree.new()
	tree.name = "AnimationTree"
	tree.anim_player = NodePath("../AnimationPlayer")
	var machine := AnimationNodeStateMachine.new()
	for clip in ["idle", "move"]:
		var node := AnimationNodeAnimation.new()
		node.animation = clip
		machine.add_node(clip, node)
	machine.add_transition("idle", "move", AnimationNodeStateMachineTransition.new())
	machine.add_transition("move", "idle", AnimationNodeStateMachineTransition.new())
	tree.tree_root = machine
	tree.callback_mode_process = AnimationMixer.ANIMATION_CALLBACK_MODE_PROCESS_MANUAL
	body.add_child(tree)
	tree.active = true
	animation.AnimationTargetPath = NodePath("../AnimationTree")
	animation.RefreshBindings()
	animation._PhysicsProcess(0.01)
	tree.advance(0.01)
	var playback: AnimationNodeStateMachinePlayback = tree.get("parameters/playback")
	check(playback.get_current_node() == "idle", "AnimationTree did not start its authored state")
	body.position.x += 1
	animation._PhysicsProcess(0.01)
	tree.advance(0.01)
	check(playback.get_current_node() == "move", "AnimationTree did not travel to its movement state")
	var tree_save: Dictionary = JSON.parse_string(JSON.stringify(actor.CaptureActor()))
	playback.start("idle", true)
	tree.advance(0.01)
	actor.RestoreActor(tree_save)
	tree.advance(0.01)
	check(playback.get_current_node() == "move", "AnimationTree save did not resume its selected root state")
	animation.IsActive = false
	var before := body.is_physics_processing()
	animation._PhysicsProcess(0.01)
	check(body.is_physics_processing() == before, "Animation culling changed simulation processing")
	# Reattachment disconnects the old body's gameplay signals.
	var other := Node2D.new()
	host.add_child(other)
	var other_sprite := AnimatedSprite2D.new()
	other_sprite.name = "AnimatedSprite2D"
	other_sprite.sprite_frames = frames()
	other_sprite.animation = "idle"
	other.add_child(other_sprite)
	var other_health := make("HealthComponent", "Health", other)
	animation.AnimationTargetPath = NodePath("../AnimatedSprite2D")
	animation.IsActive = true
	animation.reparent(other)
	animation.set_physics_process(false)
	health.emit_signal("Damaged", 1.0, 99.0)
	animation._PhysicsProcess(0.01)
	check(animation.CurrentClip == "idle", "Reattached adapter retained old health subscription")
	other_health.emit_signal("Damaged", 1.0, 99.0)
	animation._PhysicsProcess(0.01)
	check(animation.CurrentClip == "hurt", "Reattached adapter did not bind new health")
	var custom_worker := WorkerStub.new()
	custom_worker.name = "CustomWorker"
	other.add_child(custom_worker)
	animation.WorkerPath = NodePath("../CustomWorker")
	animation.RefreshBindings()
	animation._PhysicsProcess(0.01)
	check(animation.CurrentClip == "work", "Custom worker port did not drive work animation")
	custom_worker.IsWorking = false
	animation._PhysicsProcess(0.01)
	check(animation.CurrentClip == "idle", "Custom worker remained visually working after completion")
	host.free()
	print("[actor-animation] OK" if failures.is_empty() else "[actor-animation] FAILED")
	quit(0 if failures.is_empty() else 1)
