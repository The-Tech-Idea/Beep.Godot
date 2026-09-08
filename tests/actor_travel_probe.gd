extends "res://tests/job_execution_probe.gd"

func run() -> void:
	var host := Node2D.new()
	root.add_child(host)
	current_scene = host
	var actors := Node2D.new()
	actors.name = "Actors"
	host.add_child(actors)
	var prototype := Node2D.new()
	var identity: Node = load(ECS + "actors/ActorComponent.cs").new()
	prototype.add_child(identity)
	identity.owner = prototype
	var packed := PackedScene.new()
	packed.pack(prototype)
	prototype.free()
	var definition: Resource = load(ECS + "actors/ActorDefinition.cs").new()
	definition.Id = "worker"
	definition.Scene = packed
	definition.SimulationPolicy = 1
	var grid := make("grid/GridProjectionComponent", "Grid", host, {"DrawGrid": false, "TrackMouseCell": false})
	var registry := make("actors/ActorRegistryComponent", "Registry", host,
		{"ActorsRootPath": NodePath("../Actors"), "Definitions": [definition]})
	make("actors/PlayerContextComponent", "Player", host,
		{"RegistryPath": NodePath("../Registry"), "PlayerId": "settlement", "ReadLocalInput": false})
	var nav := make("grid/GridNavigationComponent", "Navigation", host, {"GridPath": NodePath("../Grid")})
	var clock := make("grid/GridWorkClockComponent", "Clock", host)
	clock.set_process(false)
	var config := {"ActorRegistryPath": NodePath("../Registry"), "NavigationPath": NodePath("../Navigation"), "GridPath": NodePath("../Grid"), "WorkClockPath": NodePath("../Clock")}
	var travel := make("grid/GridActorTravelComponent", "Travel", host, config)
	var other := make("grid/GridActorTravelComponent", "Other", host, config)
	var start: Vector2 = grid.CellToWorld(Vector2i(2, 2))
	var goal := Vector2i(6, 2)
	var step: float = start.distance_to(grid.CellToWorld(Vector2i(3, 2)))
	registry.SpawnActor("worker", "settlement", start, "worker")
	check(registry.TrySleepActor("worker"), "Could not retire travel actor")
	await process_frame
	travel.TravelFinished.connect(func(_id, arrived, _reason):
		if arrived: completions += 1)
	check(travel.BeginTravel("worker", goal, step), "Dormant travel rejected")
	check(not other.BeginTravel("worker", goal, step), "Duplicate movement owner accepted")
	check(not other.RestoreState(travel.CaptureState()), "Restore bypassed movement ownership")
	clock.AdvanceTurns(1.0)
	check(registry.GetActorPosition("worker") == start, "Pending path teleported actor")
	for i in range(100):
		nav.ProcessPathRequests()
		if nav.PendingPathRequestCount == 0: break
	clock.AdvanceTurns(0.5)
	var position: Vector2 = registry.GetActorPosition("worker")
	check(is_equal_approx(position.distance_to(start), step * 0.5), "Travel ignored world units per turn")
	check(registry.FindActor("worker") == null, "Travel instantiated a scene")
	var saved: Dictionary = travel.CaptureState()
	check(travel.RestoreState(saved), "Travel could not restore and replan")
	for i in range(100):
		nav.ProcessPathRequests()
		if nav.PendingPathRequestCount == 0: break
	clock.AdvanceTurns(10.0)
	check(completions == 1 and travel.TravellerCount == 0, "Arrival was missing or duplicated")
	check(registry.GetActorPosition("worker").is_equal_approx(grid.CellToWorld(goal)), "Arrival position wrong")
	check(travel.BeginTravel("worker", Vector2i(2, 2), step), "Return trip rejected")
	registry.WakeActor("worker")
	check(travel.TravellerCount == 0 and nav.PendingPathRequestCount == 0, "Wake retained dormant travel")
	clock.AdvanceTurns(10.0)
	check(completions == 1, "Cancelled trip reported arrival")
	host.free()
	print("[actor-travel] OK: incremental dormant travel, ownership, restore, arrival and wake" if failures.is_empty() else "[actor-travel] FAILED")
	quit(0 if failures.is_empty() else 1)
