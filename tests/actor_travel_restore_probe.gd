extends "res://tests/job_execution_probe.gd"

# FIX-13: a rejected actor-travel restore must be reported, not silently swallowed.
# Load() reads RestoreState's result and raises TravelRestoreFailed (plus a warning)
# instead of returning as though the load had succeeded; the live routes stay intact.

var restore_failures := 0

func _on_restore_failed(saved_count: int) -> void:
	restore_failures = saved_count

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
	var config := {"ActorRegistryPath": NodePath("../Registry"), "NavigationPath": NodePath("../Navigation"),
		"GridPath": NodePath("../Grid"), "WorkClockPath": NodePath("../Clock")}
	var travel := make("grid/GridActorTravelComponent", "Travel", host, config)
	travel.TravelRestoreFailed.connect(_on_restore_failed)

	var start: Vector2 = grid.CellToWorld(Vector2i(2, 2))
	var goal := Vector2i(20, 20)
	var step: float = start.distance_to(grid.CellToWorld(Vector2i(3, 2)))
	registry.SpawnActor("worker", "settlement", start, "worker")
	check(registry.TrySleepActor("worker"), "Could not retire travel actor")
	await process_frame
	check(travel.BeginTravel("worker", goal, step), "Dormant travel rejected")
	var saved: Dictionary = travel.CaptureState()
	check(saved.size() == 1, "CaptureState did not record the live route")

	# (1) A restore whose route is now out of bounds must be reported, not silently dropped.
	nav.BoundsSize = Vector2i(4, 4)
	restore_failures = 0
	var game_state = load("res://addons/beep_game_builder_cs/core/GameStateData.cs").new()
	var json := JSON.stringify({"game_data": {"actor_travel": {
		"worker": {"owner": "settlement", "goal": {"x": goal.x, "y": goal.y}, "speed": step}}}})
	check(game_state.FromJsonString(json), "Could not build the saved game state")
	travel.Load(game_state)
	check(restore_failures == 1, "Load did not raise TravelRestoreFailed for a rejected restore (got %d)" % restore_failures)
	check(travel.TravellerCount == 1, "A rejected restore dropped the live routes")

	# (2) A save larger than the pending-request budget is refused, leaving the live routes intact.
	nav.BoundsSize = Vector2i(64, 64)
	nav.MaximumPendingRequests = 1
	var oversized := {}
	for i in 5:
		oversized["ghost_%d" % i] = {"owner": "settlement", "goal": Vector2i(3, 3), "speed": step}
	check(not travel.RestoreState(oversized), "Oversized restore was admitted past the pending-request budget")
	check(travel.TravellerCount == 1, "A budget-rejected restore wiped the live routes")

	host.free()
	print("[actor-travel-restore] OK: rejected restore reports and leaves live routes intact" if failures.is_empty() else "[actor-travel-restore] FAILED")
	quit(0 if failures.is_empty() else 1)
