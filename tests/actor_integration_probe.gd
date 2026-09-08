extends SceneTree

const ECS := "res://addons/beep_game_builder_cs/ecs/"
var failures: Array[String] = []

func check(value: bool, message: String) -> void:
	if not value:
		failures.append(message)
		push_error(message)

func make(path: String, label: String, parent: Node, properties: Dictionary = {}) -> Node:
	var node: Node = load(ECS + path + ".cs").new()
	node.name = label
	for key in properties: node.set(key, properties[key])
	parent.add_child(node)
	return node

func _initialize() -> void:
	create_timer(30.0).timeout.connect(func(): push_error("Actor integration timed out"); quit(1))
	call_deferred("run")

func run() -> void:
	var host := Node2D.new()
	host.name = "ActorTest"
	root.add_child(host)
	current_scene = host
	var registry := make("actors/ActorRegistryComponent", "Registry", host)
	var player := make("actors/PlayerContextComponent", "Player", host, {"RegistryPath": NodePath("../Registry"), "PlayerId": "owner", "ControlMode": 1, "ReadLocalInput": false})
	make("actors/PlayerContextComponent", "Other", host, {"RegistryPath": NodePath("../Registry"), "PlayerId": "other", "FactionId": "other", "ReadLocalInput": false})
	var grid := make("grid/GridProjectionComponent", "Grid", host, {"TileSize": Vector2(32, 32), "DrawGrid": false, "TrackMouseCell": false})
	var navigation := make("grid/GridNavigationComponent", "Navigation", host, {"GridPath": NodePath("../Grid"), "BoundsOrigin": Vector2i(-10, -10), "BoundsSize": Vector2i(30, 30)})
	var jobs := make("grid/GridJobQueueComponent", "Jobs", host, {"OwnerId": "owner", "ActorRegistryPath": NodePath("../Registry")})
	var definition: Resource = load(ECS + "actors/ActorDefinition.cs").new()
	definition.set("Id", "worker")
	definition.set("Capabilities", 9)
	var bodies: Array[Node2D] = []
	var actors: Array[Node] = []
	var healths: Array[Node] = []
	for i in 3:
		var body := CharacterBody2D.new()
		body.name = "Unit%d" % i
		host.add_child(body)
		body.position = Vector2(i * 32, 0)
		var health := make("HealthComponent", "Health", body, {"MaxHealth": 100.0, "CurrentHealth": 30.0 + i * 20, "ParticipatesInSave": true})
		make("grid/GridPathFollowerComponent", "Follower", body, {"GridPath": NodePath("../../Grid"), "NavigationPath": NodePath("../../Navigation"), "Speed": 300.0, "DriveCharacterBody": false, "SetZIndexFromY": false})
		make("grid/GridWorkerComponent", "Worker", body, {"GridPath": NodePath("../../Grid"), "JobQueuePath": NodePath("../../Jobs"), "PathFollowerPath": NodePath("../Follower"), "AutoClaimJobs": false})
		var actor := make("actors/ActorComponent", "Actor", body, {"ActorId": "u%d" % i, "OwnerId": "owner" if i < 2 else "other", "Definition": definition, "RegistryPath": NodePath("../../Registry")})
		bodies.append(body)
		actors.append(actor)
		healths.append(health)
	check(registry.get("ActorCount") == 3, "Registry lost actors")
	check(player.call("GetOwnedActors").size() == 2, "No-avatar player roster is wrong")
	check(not player.call("Possess", "u0"), "Order-only player unexpectedly possessed an avatar")
	check(not player.call("SelectActor", "u2", false), "Enemy selected for commands")
	check(player.call("SelectRectangle", Rect2(-100, -100, 300, 300), false) == 2, "Box selection did not filter ownership")
	check(player.call("IssueOrder", 0, Vector2i(4, 3), "", false, "") == 2, "Move command was not routed to both owned actors")
	for i in 65: await physics_frame
	check(bodies[0].position.distance_to(grid.call("CellToWorld", Vector2i(4, 3))) < 1, "First unit did not reach destination")
	check(bodies[1].position.distance_to(grid.call("CellToWorld", Vector2i(4, 3))) < 1, "Second unit did not reach destination")
	check(bodies[2].position == Vector2(64, 0), "Non-owned unit moved")
	var command: Resource = load(ECS + "actors/ActorCommand.cs").new()
	command.set("IssuerId", "owner")
	command.set("Recipients", ["u0", "u2"])
	command.set("TargetCell", Vector2i(9, 9))
	check(registry.call("Submit", command) == 0, "Mixed-owner command accepted")
	check(not actors[0].get("HasOrders"), "Rejected command partially changed recipients")
	var saved: Dictionary = registry.call("CaptureState")
	for health in healths: health.call("Revive", 100.0)
	registry.call("RestoreState", saved)
	for i in 3: check(is_equal_approx(healths[i].get("CurrentHealth"), 30.0 + i * 20), "Per-actor health overwritten")
	# Restore a live route through JSON, not just an in-memory Variant snapshot.
	player.call("SelectActor", "u0", false)
	player.call("IssueOrder", 0, Vector2i(12, 8), "", false, "")
	for i in 8: await physics_frame
	var route_save: Dictionary = JSON.parse_string(JSON.stringify(registry.call("CaptureState")))
	actors[0].call("CancelOrders")
	bodies[0].position = Vector2.ZERO
	registry.call("RestoreState", route_save)
	for i in 90: await physics_frame
	check(bodies[0].position.distance_to(grid.call("CellToWorld", Vector2i(12, 8))) < 1, "Saved route did not resume")
	var results: Array = []
	actors[0].connect("CommandFinished", func(action, success, reason): results.append([action, success, reason]))
	var job: String = jobs.call("AddJob", Vector2i(11, 8), "prepare", 0.05, 0)
	check(player.call("IssueOrder", 3, Vector2i.ZERO, "", false, job) == 1, "Work command rejected")
	for i in 40: await physics_frame
	check(results.size() == 1 and results[0][1], "Work completion was not reported exactly once")
	results.clear()
	job = jobs.call("AddJob", Vector2i(1, 1), "prepare", 1.0, 0)
	player.call("IssueOrder", 3, Vector2i.ZERO, "", false, job)
	for i in 4: await physics_frame
	bodies[0].get_node("Follower").call("CancelMove")
	for i in 6: await physics_frame
	check(results.size() == 1 and not results[0][1], "Interrupted work incorrectly reported success")
	check(not jobs.call("CanWorkerClaim", "u2"), "Foreign worker can claim owned jobs")
	player.call("SelectActor", "u1", true)
	check(registry.call("TransferOwnership", "u1", "other"), "Ownership transfer rejected")
	check(player.call("GetSelectedActors").size() == 1, "Transferred unit retained command selection")
	check(not registry.call("AreHostile", "owner", "other"), "Neutral factions automatically became hostile")
	registry.call("SetHostile", "faction_1", "other", true)
	check(registry.call("AreHostile", "owner", "other"), "Explicit faction hostility missing")
	bodies[0].position = Vector2(-65, -33)
	await physics_frame
	await physics_frame
	check(registry.call("QueryActors", Rect2(-70, -40, 20, 20), "owner", false).size() == 1, "Spatial query failed at negative coordinates")
	registry.set("SpatialCellSize", 16.0)
	check(registry.call("QueryActors", Rect2(-70, -40, 20, 20), "owner", false).size() == 1, "Spatial resize lost actors")
	var depot: Node = load("res://tests/actor_test_depot.gd").new()
	depot.name = "Depot"
	host.add_child(depot)
	var hauler := make("grid/GridHaulerComponent", "Hauler", bodies[1], {"PathFollowerPath": NodePath("../Follower"), "DepotStoragePath": NodePath("../../Depot"), "DepotCell": Vector2i(3, 3), "RegisterOnReady": false})
	# Authoring order must not determine whether a route restores before its hauler.
	bodies[1].move_child(hauler, 0)
	check(hauler.call("RequestHaul", Vector2i(10, 10), "wood", 12), "Haul request failed")
	for i in 4: await physics_frame
	var haul_save: Dictionary = JSON.parse_string(JSON.stringify(actors[1].call("CaptureActor")))
	actors[1].call("CancelOrders")
	for i in 140: await physics_frame
	check(hauler.call("Stored", "wood") == 12 and depot.Stored("wood") == 0, "Cancelled haul remotely delivered or lost cargo")
	actors[1].call("RestoreActor", haul_save)
	for i in 240: await physics_frame
	check(hauler.call("Stored", "wood") == 0 and depot.Stored("wood") == 12, "Restored haul lost or duplicated cargo")
	# A direct controller consumes only its possessed actor's intent, never shared NPC input.
	for body in bodies: make("TopDownController", "DirectMotor", body, {"Speed": 180.0})
	player.set("ControlMode", 0)
	player.set("ReadLocalInput", true)
	check(player.call("Possess", "u0"), "Direct player cannot possess its actor")
	var start: Vector2 = bodies[0].position
	var other_start: Vector2 = bodies[1].position
	if not InputMap.has_action("move_right"): InputMap.add_action("move_right")
	var input := InputEventAction.new()
	input.action = "move_right"
	input.pressed = true
	Input.parse_input_event(input)
	for i in 20: await physics_frame
	check(bodies[0].position.x > start.x + 10, "Possessed actor did not consume input")
	check(bodies[1].position == other_start, "Unpossessed actor consumed local player input")
	input = InputEventAction.new()
	input.action = "move_right"
	Input.parse_input_event(input)
	for i in 15: await physics_frame
	check(bodies[0].velocity.length() < 1, "Released movement remained latched")
	check(not navigation.call("IsBlocked", Vector2i.ZERO), "Empty navigation unexpectedly blocked")
	var late_cells := make("grid/GridCellDataComponent", "LateCells", host)
	late_cells.call("SetTerrainKind", Vector2i.ZERO, "water")
	check(navigation.call("IsBlocked", Vector2i.ZERO), "Cached absent cell source ignored newly added terrain")
	host.remove_child(late_cells)
	check(not navigation.call("IsBlocked", Vector2i.ZERO), "Removed fallback terrain source remained bound")
	late_cells.free()
	healths[0].set("CurrentHealth", 0.0)
	healths[0].emit_signal("Died")
	check(player.get("PossessedActorId") == "", "Dead actor retained possession")
	check(not player.call("SelectActor", "u0", false), "Dead actor can still be selected for orders")
	job = jobs.call("AddJob", Vector2i.ZERO, "prepare", 1.0, 0)
	check(not bodies[0].get_node("Worker").call("CanAcceptJob", job), "Dead worker can accept new jobs")
	host.free()
	print("[actors] OK" if failures.is_empty() else "[actors] FAILED: " + str(failures))
	quit(0 if failures.is_empty() else 1)
