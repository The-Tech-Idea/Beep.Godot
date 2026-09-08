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
	var cell := Vector2i(4, 5)
	registry.SpawnActor("worker", "settlement", grid.CellToWorld(cell), "worker")
	var queue := make("grid/GridJobQueueComponent", "Jobs", host,
		{"OwnerId": "settlement", "ActorRegistryPath": NodePath("../Registry"), "RemoveCompletedJobs": false, "RequeueClaimedJobsOnLoad": false})
	var clock := make("grid/GridWorkClockComponent", "Clock", host, {"ScheduledWorkBudgetMilliseconds": 0.0})
	clock.set_process(false)
	var config := {"JobQueuePath": NodePath("../Jobs"), "ActorRegistryPath": NodePath("../Registry"), "GridPath": NodePath("../Grid"), "WorkClockPath": NodePath("../Clock")}
	var executor := make("grid/GridJobExecutionComponent", "Executor", host, config)
	var other := make("grid/GridJobExecutionComponent", "Other", host, config)
	var id: String = queue.AddJob(cell, "build", 4.0, 0)
	check(queue.ClaimJob(id, "worker") and executor.BeginWork("worker", id, 1.0), "Could not start world-owned work")
	check(not other.BeginWork("worker", id, 1.0), "Two executors accepted one job")
	check(queue.AdvanceClaimedWork(id, "worker", cell, 1.0, 1.0) == 0, "Scene-owned executor bypassed execution lock")
	clock.AdvanceTurns(1.0)
	check(queue.GetJobRemainingTurns(id) == 3.0, "World clock did not advance authoritative progress")
	check(registry.TrySleepActor("worker"), "Scene-independent executor prevented scene retirement")
	await process_frame
	check(registry.FindActor("worker") == null, "Worker scene remained loaded")
	clock.AdvanceTurns(0.1)
	var jobs_saved: Array = queue.GetJobs()
	var execution_saved: Dictionary = executor.CaptureState()
	host.remove_child(executor)
	clock.AdvanceTurns(10.0)
	check(queue.GetJobRemainingTurns(id) == 3.0 and clock.ScheduledWorkCount == 0,
		"Detached world executor continued running")
	host.add_child(executor)
	check(executor.CaptureState() == execution_saved and clock.ScheduledWorkCount == 1,
		"Reattached executor lost pending time or duplicated its deadline")
	var bad: Dictionary = execution_saved.duplicate(true)
	bad.worker.speed = -1.0
	check(not executor.RestoreState(bad) and executor.CaptureState() == execution_saved,
		"Invalid execution restore changed live records")
	queue.JobCompleted.connect(func(_id, _worker): completions += 1)
	clock.AdvanceTurns(4.0)
	check(completions == 1 and executor.ExecutionCount == 0 and queue.GetJobProgress01(id) == 1.0,
		"Unloaded worker did not finish exactly once")
	queue.LoadJobs(jobs_saved, true)
	executor.free()
	executor = make("grid/GridJobExecutionComponent", "Executor", host, config)
	check(executor.RestoreState(execution_saved), "Fresh executor could not restore pending work")
	clock.AdvanceTurns(3.0)
	check(completions == 2 and executor.ExecutionCount == 0, "Restored executor lost work or duplicated completion")
	var next: String = queue.AddJob(cell, "inspect", 5.0, 0)
	check(queue.ClaimJob(next, "worker") and executor.BeginWork("worker", next, 1.0), "Dormant worker could not begin next work phase")
	registry.RemoveActor("worker")
	check(executor.ExecutionCount == 0 and queue.GetJobState(next) == 0 and clock.ScheduledWorkCount == 0,
		"Permanent actor deletion left execution or claims")
	clock.AdvanceTurns(20.0)
	check(completions == 2, "Deleted worker completed abandoned job")
	var replacement: Node = registry.SpawnActor("worker", "settlement", grid.CellToWorld(cell + Vector2i.ONE), "replacement")
	check(queue.ClaimJob(next, "replacement") and not executor.BeginWork("replacement", next, 1.0),
		"Executor accepted work before arrival")
	replacement.get_parent().global_position = grid.CellToWorld(cell)
	check(executor.BeginWork("replacement", next, 1.0), "Executor rejected arrived worker")
	replacement.get_parent().global_position = grid.CellToWorld(cell + Vector2i.ONE)
	clock.AdvanceTurns(0.5)
	check(executor.ExecutionCount == 0 and queue.GetJobState(next) == 0 and completions == 2,
		"Worker leaving site continued work or retained claim")
	host.free()
	print("[job-execution-service] OK: world clock, residency, exclusive execution, restore and destruction" if failures.is_empty() else "[job-execution-service] FAILED")
	quit(0 if failures.is_empty() else 1)
