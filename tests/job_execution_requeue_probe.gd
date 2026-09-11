extends "res://tests/job_execution_probe.gd"

# FIX-07: a job that was Claimed and world-executed at save time is requeued on load when
# RequeueClaimedJobsOnLoad is set - which is the DEFAULT (sized for the actor ghost-worker case).
# Before the fix, GridJobExecutionComponent.RestoreState required the job still Claimed and so dropped
# the whole world-owned execution, parking the worker Idle with its progress lost. This probe drives
# the real harness (grid + dormant worker + clock + queue + executor) with the DEFAULT requeue policy
# and asserts the execution survives save/load: the job is re-claimed under the saved worker at its
# reserved cell, the executor rebinds it, progress is kept, and it completes exactly once.
# The sibling job_execution_service_probe.gd covers the same flow with requeue OFF; this one is the
# requeue-ON case the fix is about. Mirrors that probe's setup verbatim, then diverges at the restore.
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
	# The DEFAULT policy - this is exactly what broke world-execution restore before FIX-07.
	var queue := make("grid/GridJobQueueComponent", "Jobs", host,
		{"OwnerId": "settlement", "ActorRegistryPath": NodePath("../Registry"), "RemoveCompletedJobs": false, "RequeueClaimedJobsOnLoad": true})
	var clock := make("grid/GridWorkClockComponent", "Clock", host, {"ScheduledWorkBudgetMilliseconds": 0.0})
	clock.set_process(false)
	var config := {"JobQueuePath": NodePath("../Jobs"), "ActorRegistryPath": NodePath("../Registry"), "GridPath": NodePath("../Grid"), "WorkClockPath": NodePath("../Clock")}
	var executor := make("grid/GridJobExecutionComponent", "Executor", host, config)

	# Start world-owned work, advance it partway, retire the worker to dormant, then capture.
	var id: String = queue.AddJob(cell, "build", 4.0, 0)
	check(queue.ClaimJob(id, "worker") and executor.BeginWork("worker", id, 1.0), "Could not start world-owned work")
	check(registry.TrySleepActor("worker"), "Worker did not retire to dormant")
	await process_frame
	check(registry.FindActor("worker") == null, "Worker scene remained loaded")
	clock.AdvanceTurns(1.0)
	var remaining_before: float = queue.GetJobRemainingTurns(id)
	check(remaining_before == 3.0, "World clock did not advance authoritative progress")
	var jobs_saved: Array = queue.GetJobs()
	var execution_saved: Dictionary = executor.CaptureState()

	# The bug: LoadJobs requeues the claim under the default policy, and a naive RestoreState then
	# drops the world-owned job. Free the executor, requeue, and restore into a fresh one.
	executor.free()
	queue.LoadJobs(jobs_saved, true)
	check(queue.GetJobState(id) == 0, "LoadJobs did not requeue the claimed job (state should be Queued)")
	check(queue.GetJobClaimedBy(id) == "", "Requeued job kept its claimant")

	var fresh := make("grid/GridJobExecutionComponent", "Executor2", host, config)
	check(fresh.RestoreState(execution_saved), "World-owned execution was dropped by requeue-on-load (FIX-07)")
	# The re-claim reproduced the saved Claimed + worker + reserved cell, and the executor rebound it.
	check(queue.GetJobState(id) == 1, "Restored job is not Claimed again (state should be Claimed)")
	check(queue.GetJobClaimedBy(id) == "worker", "Restored job not claimed by the saved worker")
	check(queue.GetReservedWorkCell(id) == cell, "Restored job lost its reserved work cell")
	check(fresh.GetWorkerJob("worker") == id, "Executor did not rebind the restored worker to its job")
	check(queue.GetJobRemainingTurns(id) == remaining_before, "Restored job lost its progress")

	# Progress continues to completion exactly once - the work was truly restored, not just re-claimed.
	queue.JobCompleted.connect(func(_id, _worker): completions += 1)
	clock.AdvanceTurns(3.0)
	check(completions == 1 and fresh.ExecutionCount == 0, "Restored world job did not finish exactly once")

	host.free()
	print("[job-execution-requeue] OK: a world-owned job survives requeue-on-load restore" if failures.is_empty() else "[job-execution-requeue] FAILED")
	quit(0 if failures.is_empty() else 1)
