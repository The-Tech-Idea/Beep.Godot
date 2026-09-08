extends "res://tests/job_execution_probe.gd"

func arrive(worker: Node, navigation: Node) -> void:
	var follower: Node = worker.get_parent().get_node("Follower")
	for i in 30:
		navigation.ProcessPathRequests()
		follower.AdvancePath(0.1)
		worker.Tick(0.1)
		if worker.IsWorking: break
	check(worker.IsWorking and worker.HasWorldExecution, "Arrival did not hand off work")

func run() -> void:
	var host := Node2D.new()
	root.add_child(host)
	current_scene = host
	var actors := Node2D.new()
	actors.name = "Actors"
	host.add_child(actors)
	var grid := make("grid/GridProjectionComponent", "Grid", host, {"DrawGrid": false, "TrackMouseCell": false})
	var nav := make("grid/GridNavigationComponent", "Navigation", host, {"GridPath": NodePath("../Grid")})
	var definition: Resource = load(ECS + "actors/ActorDefinition.cs").new()
	definition.Id = "worker"
	definition.Capabilities = 9
	definition.Scene = load("res://tests/job_execution_worker.tscn")
	definition.SimulationPolicy = 1
	var registry := make("actors/ActorRegistryComponent", "Registry", host,
		{"ActorsRootPath": NodePath("../Actors"), "Definitions": [definition]})
	make("actors/PlayerContextComponent", "Player", host,
		{"RegistryPath": NodePath("../Registry"), "PlayerId": "settlement", "ReadLocalInput": false})
	var queue := make("grid/GridJobQueueComponent", "Jobs", host,
		{"OwnerId": "settlement", "ActorRegistryPath": NodePath("../Registry"), "RemoveCompletedJobs": false, "RequeueClaimedJobsOnLoad": false})
	var clock := make("grid/GridWorkClockComponent", "Clock", host, {"ScheduledWorkBudgetMilliseconds": 0.0})
	clock.set_process(false)
	var executor := make("grid/GridJobExecutionComponent", "Executor", host,
		{"JobQueuePath": NodePath("../Jobs"), "ActorRegistryPath": NodePath("../Registry"), "GridPath": NodePath("../Grid"), "WorkClockPath": NodePath("../Clock")})
	var actor: Node = registry.SpawnActor("worker", "settlement", grid.CellToWorld(Vector2i.ZERO), "worker")
	var worker: Node = actor.get_parent().get_node("Worker")
	worker.set_process(false)
	var id: String = queue.AddJob(Vector2i(2, 0), "inspect", 8.0, 0)
	check(worker.AssignJob(id), "Worker could not claim job")
	check(not worker.CanSuspendActor() and not registry.TrySleepActor("worker"), "Travelling worker retired before arrival")
	arrive(worker, nav)
	clock.AdvanceTurns(2.0)
	worker.AdvanceWork(100.0)
	check(queue.GetJobRemainingTurns(id) == 6.0 and clock.ScheduledWorkCount == 1,
		"Scene and world both advanced work or created duplicate deadlines")
	check(registry.TrySleepActor("worker"), "Delegated working actor could not retire")
	await process_frame
	check(not is_instance_valid(worker) and queue.GetJobClaimedBy(id) == "worker", "Scene retirement released world claim")
	clock.AdvanceTurns(2.0)
	actor = registry.WakeActor("worker")
	worker = actor.get_parent().get_node("Worker")
	worker.set_process(false)
	check(worker.IsWorking and worker.HasWorldExecution and worker.WorkRemainingTurns == 4.0,
		"Wake restored stale scene progress over world work")
	var actor_saved: Dictionary = registry.CaptureState()
	var jobs_saved: Array = queue.GetJobs()
	var executor_saved: Dictionary = executor.CaptureState()
	worker.WorkerCompletedJob.connect(func(_worker, _job): completions += 1)
	clock.AdvanceTurns(4.0)
	check(completions == 1 and worker.CurrentJobId.is_empty() and executor.ExecutionCount == 0,
		"World completion did not finish visible worker once")
	var next: String = queue.AddJob(Vector2i(2, 0), "build", 2.0, 0)
	check(worker.AssignJob(next), "Worker could not take another job")
	arrive(worker, nav)
	registry.RestoreState(actor_saved)
	queue.LoadJobs(jobs_saved, true)
	check(executor.RestoreState(executor_saved), "Could not restore over a different world job")
	check(worker.CurrentJobId == id and worker.WorkRemainingTurns == 4.0 and worker.HasWorldExecution,
		"Live worker did not reconcile to restored world job")
	clock.AdvanceTurns(4.0)
	check(worker.CurrentJobId.is_empty() and executor.ExecutionCount == 0, "Restored live job did not complete")
	next = queue.AddJob(Vector2i(2, 0), "build", 2.0, 0)
	check(worker.AssignJob(next), "Restored worker could not take subsequent job")
	arrive(worker, nav)
	check(registry.TrySleepActor("worker"), "Second retirement failed")
	await process_frame
	clock.AdvanceTurns(3.0)
	actor = registry.WakeActor("worker")
	worker = actor.get_parent().get_node("Worker")
	check(not worker.IsWorking and worker.CurrentJobId.is_empty() and executor.ExecutionCount == 0,
		"Wake resurrected a job completed while unloaded")
	var ordered_job: String = queue.AddJob(Vector2i(2, 0), "inspect", 2.0, 0)
	var command: Resource = load(ECS + "actors/ActorCommand.cs").new()
	command.Action = 3
	command.IssuerId = "settlement"
	command.Recipients = ["worker"]
	command.JobId = ordered_job
	check(registry.Submit(command) == 1, "Owned work command was rejected")
	for i in 30:
		await physics_frame
		nav.ProcessPathRequests()
		worker.get_parent().get_node("Follower").AdvancePath(0.1)
		worker.Tick(0.1)
		if worker.IsWorking: break
	check(worker.HasWorldExecution and actor.HasOrders, "Explicit work order did not delegate execution")
	check(not registry.TrySleepActor("worker"), "Active order residency protections were bypassed")
	clock.AdvanceTurns(2.0)
	check(not actor.HasOrders and worker.CurrentJobId.is_empty(), "World completion did not finish explicit actor order")
	host.free()
	print("[worker-execution-binding] OK: travel handoff, exclusive clock, retirement, current progress and completion" if failures.is_empty() else "[worker-execution-binding] FAILED")
	quit(0 if failures.is_empty() else 1)
