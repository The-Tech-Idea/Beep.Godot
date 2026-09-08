extends SceneTree

const ECS := "res://addons/beep_game_builder_cs/ecs/"
var failures: Array[String] = []
var completions := 0
func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		push_error(message)
func make(path: String, label: String, parent: Node, properties: Dictionary = {}) -> Node:
	var node: Node = load(ECS + path + ".cs").new()
	node.name = label
	for key in properties: node.set(key, properties[key])
	parent.add_child(node)
	return node
func _initialize() -> void:
	create_timer(30).timeout.connect(func(): push_error("Job execution timed out"); quit(1))
	run.call_deferred()
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
	var registry := make("actors/ActorRegistryComponent", "Registry", host,
		{"ActorsRootPath": NodePath("../Actors"), "Definitions": [definition]})
	make("actors/PlayerContextComponent", "Player", host,
		{"RegistryPath": NodePath("../Registry"), "PlayerId": "settlement", "ReadLocalInput": false})
	registry.SpawnActor("worker", "settlement", Vector2.ZERO, "worker")
	check(registry.TrySleepActor("worker"), "Actor did not retire")
	await process_frame
	var queue := make("grid/GridJobQueueComponent", "Jobs", host,
		{"OwnerId": "settlement", "ActorRegistryPath": NodePath("../Registry"), "RemoveCompletedJobs": false, "RequeueClaimedJobsOnLoad": false})
	var cell := Vector2i(4, 5)
	var id: String = queue.AddJob(cell, "build", 8.0, 0)
	check(queue.ClaimJob(id, "worker"), "Persistent ownership did not allow scene-independent claim")
	check(queue.AdvanceClaimedWork(id, "stranger", cell, 1.0, 1.0) == 0, "Wrong worker advanced job")
	check(queue.AdvanceClaimedWork(id, "worker", cell + Vector2i.ONE, 1.0, 1.0) == 0, "Wrong work cell advanced job")
	check(queue.AdvanceClaimedWork(id, "worker", cell, NAN, 1.0) == 0, "Nonfinite time advanced job")
	check(queue.AdvanceClaimedWork(id, "worker", cell, 2.0, 2.0) == 1 and queue.GetJobRemainingTurns(id) == 4.0,
		"Scene-independent work did not use queue-owned progress")
	var saved: Array = queue.GetJobs()
	queue.LoadJobs(saved, true)
	queue.JobCompleted.connect(func(_id, _worker): completions += 1)
	check(queue.AdvanceClaimedWork(id, "worker", cell, 20.0, 1.0) == 2, "Restored job did not finish")
	check(queue.GetJobRemainingTurns(id) == 0.0 and completions == 1, "Completion did not preserve progress or fire once")
	check(queue.AdvanceClaimedWork(id, "worker", cell, 20.0, 1.0) == 0 and completions == 1, "Completed job executed twice")
	var next: String = queue.AddJob(cell, "inspect", 8.0, 0)
	check(queue.ClaimJob(next, "worker"), "Completion retained worker/cell reservation")
	registry.RemoveActor("worker")
	check(queue.AdvanceClaimedWork(next, "worker", cell, 1.0, 1.0) == 0, "Deleted owned worker continued work")
	host.free()
	print("[job-execution] OK: persistent identity, ownership, progress, restore and exactly-once completion" if failures.is_empty() else "[job-execution] FAILED")
	quit(0 if failures.is_empty() else 1)
