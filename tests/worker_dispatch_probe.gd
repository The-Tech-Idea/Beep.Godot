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
	var clock := make("grid/GridWorkClockComponent", "Clock", host, {"ScheduledWorkBudgetMilliseconds": 0.0})
	clock.set_process(false)
	var queue := make("grid/GridJobQueueComponent", "Jobs", host,
		{"OwnerId": "settlement", "ActorRegistryPath": NodePath("../Registry"), "RemoveCompletedJobs": false, "RequeueClaimedJobsOnLoad": false})
	var common := {"ActorRegistryPath": NodePath("../Registry"), "GridPath": NodePath("../Grid"), "WorkClockPath": NodePath("../Clock")}
	var travel_config := common.duplicate()
	travel_config.NavigationPath = NodePath("../Navigation")
	var travel := make("grid/GridActorTravelComponent", "Travel", host, travel_config)
	var execution_config := common.duplicate()
	execution_config.JobQueuePath = NodePath("../Jobs")
	var executor := make("grid/GridJobExecutionComponent", "Executor", host, execution_config)
	var config := execution_config.duplicate()
	config.TravelPath = NodePath("../Travel")
	config.ExecutionPath = NodePath("../Executor")
	config.AllowedJobKinds = ["build"]
	var dispatcher := make("grid/GridWorkerDispatchComponent", "Dispatcher", host, config)
	var other := make("grid/GridWorkerDispatchComponent", "Other", host, config)
	var origin := Vector2i(2, 2)
	var step: float = grid.CellToWorld(origin).distance_to(grid.CellToWorld(origin + Vector2i.RIGHT))
	registry.SpawnActor("worker", "settlement", grid.CellToWorld(origin), "worker")
	check(dispatcher.RegisterWorker("worker", step, 1.0), "Worker registration failed")
	check(other.RegisterWorker("worker", step, 1.0), "Second dispatcher registration failed")
	check(not dispatcher.RegisterWorker("missing", step, 1.0), "Unknown identity registered")
	var excluded: String = queue.AddJob(Vector2i(3, 2), "harvest", 1.0, 100)
	var first: String = queue.AddJob(Vector2i(4, 2), "build", 1.0, 2)
	var second: String = queue.AddJob(Vector2i(6, 2), "build", 1.0, 1)
	dispatcher.DispatchJobs()
	check(queue.ClaimedCount == 0, "Dispatcher took control of a loaded actor")
	check(registry.TrySleepActor("worker"), "Worker did not become dormant")
	await process_frame
	dispatcher.AssignmentFinished.connect(func(_id, _job, completed, _reason):
		if completed: completions += 1)
	dispatcher.DispatchJobs()
	other.DispatchJobs()
	check(dispatcher.GetWorkerJob("worker") == first and other.AssignmentCount == 0,
		"Dispatchers duplicated a claim or ignored priority")
	var dispatch_saved: Dictionary = dispatcher.CaptureState()
	var registry_saved: Dictionary = registry.CaptureState()
	var jobs_saved: Array = queue.GetJobs()
	var travel_saved: Dictionary = travel.CaptureState()
	var execution_saved: Dictionary = executor.CaptureState()
	check(not other.RestoreState(dispatch_saved), "Restore stole another dispatcher's assignment")
	var bad: Dictionary = dispatch_saved.duplicate(true)
	bad.workers.worker.move_speed = -1.0
	check(not dispatcher.RestoreState(bad) and dispatcher.CaptureState() == dispatch_saved,
		"Invalid restore mutated dispatcher")
	for i in range(48):
		nav.ProcessPathRequests()
		clock.AdvanceTurns(0.25)
	check(queue.GetJobProgress01(first) == 1.0 and queue.GetJobProgress01(second) == 1.0,
		"Dormant worker did not travel and complete consecutive jobs")
	check(queue.GetJobState(excluded) == 0 and completions == 2,
		"Job-kind filter or completion count failed")
	check(registry.FindActor("worker") == null and actors.get_child_count() == 0,
		"Offscreen dispatch instantiated a worker scene")
	check(executor.ExecutionCount == 0 and travel.TravellerCount == 0, "Finished work retained services")
	other.UnregisterWorker("worker")
	dispatcher.free()
	registry.RestoreState(registry_saved)
	queue.LoadJobs(jobs_saved, true)
	check(travel.RestoreState(travel_saved) and executor.RestoreState(execution_saved), "Could not restore assignment dependencies")
	dispatcher = make("grid/GridWorkerDispatchComponent", "Dispatcher", host, config)
	check(dispatcher.RestoreState(dispatch_saved), "Fresh dispatcher did not restore travelling assignment")
	bad = dispatch_saved.duplicate(true)
	bad.workers.clear()
	check(not dispatcher.RestoreState(bad) and dispatcher.AssignmentCount == 1,
		"Restore silently orphaned running assignment")
	for i in range(48):
		nav.ProcessPathRequests()
		clock.AdvanceTurns(0.25)
	check(queue.GetJobProgress01(first) == 1.0 and queue.GetJobProgress01(second) == 1.0,
		"Restored travel failed to chain into work and next job")
	dispatcher.FailedJobRetryTurns = 20.0
	var unreachable: String = queue.AddJob(Vector2i(1000, 1000), "build", 1.0, 5)
	var reachable: String = queue.AddJob(Vector2i(8, 2), "build", 1.0, 1)
	var attempts := [0]
	queue.JobClaimed.connect(func(id, _worker):
		if id == unreachable: attempts[0] += 1)
	dispatcher.DispatchJobs()
	check(queue.GetJobState(unreachable) == 0 and dispatcher.AssignmentCount == 0,
		"Rejected route retained job reservation")
	var retry_saved: Dictionary = dispatcher.CaptureState()
	check(dispatcher.RestoreState(retry_saved) and dispatcher.CaptureState() == retry_saved,
		"Retry delay was lost on restore")
	dispatcher.DispatchJobs()
	check(dispatcher.GetWorkerJob("worker") == reachable, "Unreachable job starved reachable work")
	for i in range(24):
		nav.ProcessPathRequests()
		clock.AdvanceTurns(0.25)
	check(queue.GetJobProgress01(reachable) == 1.0 and attempts[0] == 1,
		"Reachable job failed or unreachable job retried early")
	clock.AdvanceTurns(20.0)
	check(attempts[0] == 2, "Failed job was never retried")
	dispatcher.DispatchJobs()
	check(attempts[0] == 2, "Large clock tick consumed the newly created retry delay")
	queue.CancelJob(unreachable, "test_cleanup")
	var wake_job: String = queue.AddJob(Vector2i(12, 2), "build", 1.0, 5)
	dispatcher.DispatchJobs()
	registry.WakeActor("worker")
	check(queue.GetJobState(wake_job) == 0 and dispatcher.AssignmentCount == 0 and travel.TravellerCount == 0,
		"Wake during travel retained a job or route")
	check(registry.TrySleepActor("worker"), "Worker could not sleep again")
	await process_frame
	dispatcher.DispatchJobs()
	dispatcher.UnregisterWorker("worker")
	check(queue.GetJobState(wake_job) == 0 and travel.TravellerCount == 0, "Unregister retained claim")
	other.UnregisterWorker("worker")
	queue.CancelJob(wake_job, "test_cleanup")
	var work_cell: Vector2i = grid.WorldToCell(registry.GetActorPosition("worker"))
	var long_job: String = queue.AddJob(work_cell, "build", 4.0, 5)
	check(dispatcher.RegisterWorker("worker", step, 1.0), "Could not register for work cleanup")
	dispatcher.DispatchJobs()
	for i in range(100):
		nav.ProcessPathRequests()
		if nav.PendingPathRequestCount == 0: break
	clock.AdvanceTurns(0.25)
	check(executor.GetWorkerJob("worker") == long_job, "Arrival did not bind executor")
	clock.AdvanceTurns(0.5)
	dispatch_saved = dispatcher.CaptureState()
	registry_saved = registry.CaptureState()
	jobs_saved = queue.GetJobs()
	travel_saved = travel.CaptureState()
	execution_saved = executor.CaptureState()
	dispatcher.UnregisterWorker("worker")
	var remaining: float = queue.GetJobRemainingTurns(long_job)
	check(remaining > 0.0 and remaining < 4.0 and queue.GetJobState(long_job) == 0,
		"Unregister during work lost progress or retained the claim")
	clock.AdvanceTurns(10.0)
	check(queue.GetJobRemainingTurns(long_job) == remaining and executor.ExecutionCount == 0,
		"Unregistered worker continued executing")
	registry.RestoreState(registry_saved)
	queue.LoadJobs(jobs_saved, true)
	check(travel.RestoreState(travel_saved) and executor.RestoreState(execution_saved)
		and dispatcher.RestoreState(dispatch_saved), "Could not restore working assignment")
	check(dispatcher.GetWorkerJob("worker") == long_job and executor.ExecutionCount == 1,
		"Working assignment did not reconnect")
	clock.AdvanceTurns(4.0)
	check(queue.GetJobProgress01(long_job) == 1.0 and dispatcher.AssignmentCount == 0,
		"Restored work did not finish")
	var deletion_job: String = queue.AddJob(work_cell + Vector2i(2, 0), "build", 4.0, 5)
	dispatcher.DispatchJobs()
	registry.RemoveActor("worker")
	check(dispatcher.WorkerCount == 0 and queue.GetJobState(deletion_job) == 0,
		"Actor deletion retained assignment")
	host.free()
	print("[worker-dispatch] OK: dormant claim/travel/work, consecutive jobs, filters, ownership and cleanup" if failures.is_empty() else "[worker-dispatch] FAILED")
	quit(0 if failures.is_empty() else 1)
