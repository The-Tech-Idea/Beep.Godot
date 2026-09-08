extends SceneTree

const BASE := "res://addons/beep_game_builder_cs/ecs/grid/"

func make(kind: String, parent: Node, label: String, properties: Dictionary = {}) -> Node:
	var node: Node = load(BASE + kind + "Component.cs").new()
	node.name = label
	for key in properties: node.set(key, properties[key])
	parent.add_child(node)
	return node

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	var host := Node2D.new()
	root.add_child(host)
	var grid := make("GridProjection", host, "Grid", {"TileSize": Vector2(32, 32), "DrawGrid": false, "TrackMouseCell": false})
	var cells := make("GridCellData", host, "Cells")
	var navigation := make("GridNavigation", host, "Navigation", {"GridPath": NodePath("../Grid"), "CellDataPath": NodePath("../Cells"), "BoundsSize": Vector2i(5, 1)})
	var jobs := make("GridJobQueue", host, "Jobs", {"RemoveCompletedJobs": false})
	make("GridJobEffect", host, "Effects", {"JobQueuePath": NodePath("../Jobs"), "CellDataPath": NodePath("../Cells"), "ClearLandGathersResourceNode": false})
	var body := Node2D.new()
	host.add_child(body)
	body.global_position = grid.call("CellToWorld", Vector2i.ZERO)
	var follower := make("GridPathFollower", body, "Follower", {"GridPath": NodePath("../../Grid"), "NavigationPath": NodePath("../../Navigation"), "DriveCharacterBody": false, "Speed": 32.0})
	follower.AutoAdvancePath = false
	var worker := make("GridWorker", body, "Worker", {"GridPath": NodePath("../../Grid"), "JobQueuePath": NodePath("../../Jobs"), "PathFollowerPath": NodePath("../Follower"), "AutoClaimJobs": false})
	worker.set_process(false)
	var target := Vector2i(4, 0)
	var job: String = jobs.call("AddJob", target, "clear_land", 1.0, 0)
	assert(worker.call("AssignJob", job))
	navigation.ProcessPathRequests()
	follower.call("AdvancePath", 0.1)
	cells.call("SetTerrainKind", Vector2i(1, 0), "water")
	follower.call("AdvancePath", 0.1)
	assert(not follower.get("IsMoving") and not follower.get("HasReachedDestination"))
	worker.call("Tick", 0.1)
	worker.call("AdvanceWork", 100.0)
	assert(worker.get("CurrentJobId") == "" and not worker.get("IsWorking"), "Flooded route started remote work")
	assert(cells.call("GetFlags", target) & 2 == 0, "Failed travel applied a remote clear effect")
	assert(jobs.call("GetJobClaimedBy", job) == "", "Failed worker retained claim")
	cells.call("SetTerrainKind", Vector2i(1, 0), "grass")
	assert(worker.call("AssignJob", job))
	navigation.ProcessPathRequests()
	for step in range(20):
		follower.call("AdvancePath", 0.25)
		worker.call("Tick", 0.01)
		if worker.get("IsWorking"): break
	assert(follower.get("HasReachedDestination") and worker.get("IsWorking"), "Successful arrival did not start work")
	worker.call("AdvanceWork", 2.0)
	assert(cells.call("GetFlags", target) & 2 != 0, "Successful worker did not apply terrain effect")
	var cancelled: String = jobs.call("AddJob", Vector2i.ZERO, "clear_land", 1.0, 0)
	assert(worker.call("AssignJob", cancelled))
	assert(not follower.get("HasReachedDestination"), "New route retained prior arrival")
	follower.call("CancelMove")
	worker.call("Tick", 0.1)
	worker.call("AdvanceWork", 100.0)
	assert(cells.call("GetFlags", Vector2i.ZERO) & 2 == 0 and worker.get("CurrentJobId") == "", "Cancelled travel applied remote work")
	# Work that is already running must stop when its claim changes hands.
	var reassigned: String = jobs.AddJob(target, "inspect", 5.0, 0)
	assert(worker.AssignJob(reassigned))
	navigation.ProcessPathRequests()
	for step in 20:
		follower.AdvancePath(0.25)
		worker.Tick(0.01)
		if worker.IsWorking: break
	assert(worker.IsWorking)
	jobs.ReleaseJob(reassigned, worker.WorkerId)
	assert(jobs.ClaimJob(reassigned, "replacement"))
	worker.AdvanceWork(100.0)
	assert(jobs.GetJobRemainingTurns(reassigned) == 5.0 and jobs.GetJobClaimedBy(reassigned) == "replacement", "Old worker changed new claimant's work")
	assert(worker.CurrentJobId.is_empty() and not worker.IsWorking)
	jobs.CancelJob(reassigned, "fixture")
	# A failed approach must not silently fall back into another worker's reserved cell.
	var held: String = jobs.AddJob(target, "held", 5.0, 0)
	assert(jobs.ClaimJob(held, "other"))
	var fallback: String = jobs.AddJob(target, "fallback", 5.0, 0)
	jobs.SetJobApproachCell(fallback, Vector2i(50, 50))
	worker.AssignJob(fallback)
	for step in 10:
		navigation.ProcessPathRequests()
		follower.AdvancePath(0.1)
		worker.Tick(0.1)
	assert(not worker.IsWorking and worker.CurrentJobId.is_empty(), "Fallback entered an occupied work cell")
	assert(jobs.GetWorkCellReservation(target) == held, "Fallback stole other worker's reservation")
	host.free()
	print("[terrain-worker-arrival] OK")
	quit()
