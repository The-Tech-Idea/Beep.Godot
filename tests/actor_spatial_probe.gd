extends SceneTree

const ECS := "res://addons/beep_game_builder_cs/ecs/actors/"
var failures: Array[String] = []

func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		push_error(message)

func _initialize() -> void: run.call_deferred()

func run() -> void:
	var host := Node2D.new()
	root.add_child(host)
	var registry: Node = load(ECS + "ActorRegistryComponent.cs").new()
	registry.name = "Registry"
	registry.SpatialCellSize = 32.0
	host.add_child(registry)
	var bodies: Array[Node2D] = []
	var actors: Array[Node] = []
	for i in 1000:
		var body := Node2D.new()
		body.position = Vector2((i % 40 - 20) * 64, (i / 40 - 12) * 64)
		host.add_child(body)
		var actor: Node = load(ECS + "ActorComponent.cs").new()
		actor.ActorId = "unit_%04d" % i
		actor.OwnerId = "a" if i % 2 == 0 else "b"
		actor.RegistryPath = NodePath("../../Registry")
		body.add_child(actor)
		actor.set_physics_process(false)
		bodies.append(body)
		actors.append(actor)
	var areas := [Rect2(-16, -16, 32, 32), Rect2(-130, -130, 260, 260), Rect2(150, 150, -300, -300), Rect2(-1000000, -1000000, 2000000, 2000000)]
	for area in areas:
		for owner in ["", "a", "b"]:
			var expected: Array[String] = []
			for i in bodies.size():
				if area.abs().has_point(bodies[i].position) and (owner == "" or actors[i].OwnerId == owner):
					expected.append(actors[i].ActorId)
			expected.sort()
			var actual: Array = registry.QueryActors(area, owner, false)
			check(actual == expected, "Spatial query differs from exhaustive reference")
	registry.QueryActors(Rect2(-16, -16, 32, 32), "", false)
	check(registry.LastQueryBucketVisits == 4 and registry.LastQueryCandidateCount <= 4, "Local query scanned distant actors/buckets")
	registry.QueryActors(Rect2(-1000000, -1000000, 2000000, 2000000), "", false)
	check(registry.LastQueryBucketVisits == 1000 and registry.LastQueryCandidateCount == 1000, "Overview query iterated empty coordinates")
	bodies[0].position = Vector2(1, 1)
	actors[0]._PhysicsProcess(0.0)
	check("unit_0000" in registry.QueryActors(Rect2(0, 0, 4, 4), "", false), "Moving actor did not update spatial membership")
	registry.SpatialCellSize = 128.0
	check("unit_0000" in registry.QueryActors(Rect2(0, 0, 4, 4), "", false), "Spatial resize lost actor membership")
	check(registry.QueryActors(Rect2(Vector2.INF, Vector2.ONE), "", false).is_empty(), "Nonfinite area returned actors")
	registry.SpatialCellSize = 32.0
	var moving := CharacterBody2D.new()
	moving.position = Vector2(31, 10)
	host.add_child(moving)
	var mover_actor: Node = load(ECS + "ActorComponent.cs").new()
	mover_actor.ActorId = "moving"
	mover_actor.RegistryPath = NodePath("../../Registry")
	moving.add_child(mover_actor)
	mover_actor.set_physics_process(false)
	var controller: Node = load("res://addons/beep_game_builder_cs/ecs/TopDownController.cs").new()
	controller.Speed = 120.0
	controller.Acceleration = 100000.0
	moving.add_child(controller)
	controller.set_physics_process(false)
	mover_actor.SetIntent(Vector2.RIGHT, Vector2.RIGHT, false, false)
	await physics_frame
	controller._PhysicsProcess(1.0 / 60)
	check("moving" in registry.QueryActors(Rect2(32, 8, 8, 4), "", false), "Native movement left actor in previous bucket until next tick")
	controller.IsActive = false
	var follower: Node = load("res://addons/beep_game_builder_cs/ecs/grid/GridPathFollowerComponent.cs").new()
	follower.DriveCharacterBody = false
	follower.SetZIndexFromY = false
	follower.Speed = 120.0
	moving.add_child(follower)
	follower.AutoAdvancePath = false
	var observed: Array[bool] = []
	follower.WaypointReached.connect(func(_index, point):
		observed.append("moving" in registry.QueryActors(Rect2(point - Vector2.ONE, Vector2(2, 2)), "", false)))
	follower.SetWorldPath([Vector2(70, 10), Vector2(105, 10)])
	follower.AdvancePath(1.0)
	check(observed.size() == 2 and observed.all(func(value): return value), "Arrival callback observed stale spatial buckets")
	check("moving" in registry.QueryActors(Rect2(104, 9, 2, 2), "", false), "Direct path completion left stale spatial membership")
	moving.position = Vector2(-2000, -2000)
	mover_actor.SynchronizePosition()
	check("moving" in registry.QueryActors(Rect2(-2001, -2001, 2, 2), "", false), "Custom teleport synchronization failed")
	host.free()
	print("[actor-spatial] OK" if failures.is_empty() else "[actor-spatial] FAILED")
	quit(0 if failures.is_empty() else 1)
