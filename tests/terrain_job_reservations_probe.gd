extends SceneTree
const BASE := "res://addons/beep_game_builder_cs/ecs/grid/"
var failures: Array[String] = []
func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		push_error(message)
func _initialize() -> void: run.call_deferred()
func run() -> void:
	var host := Node.new()
	root.add_child(host)
	var cells: Node = load(BASE + "GridCellDataComponent.cs").new()
	cells.name = "Cells"
	host.add_child(cells)
	var jobs: Node = load(BASE + "GridJobQueueComponent.cs").new()
	jobs.ChunkCellDataPath = NodePath("../Cells")
	jobs.RemoveCompletedJobs = false
	jobs.RemoveCancelledJobs = false
	host.add_child(jobs)
	var a: String = jobs.AddJob(Vector2i(2, 2), "build", 5.0, 0)
	var b: String = jobs.AddJob(Vector2i(2, 2), "inspect", 5.0, 10)
	var c: String = jobs.AddJob(Vector2i(3, 2), "clear", 5.0, 0)
	check(not jobs.ClaimJob(a, " "), "Anonymous worker acquired an unreleasable claim")
	check(jobs.ClaimJob(a, "one"), "First worker could not claim")
	check(not jobs.ClaimJob(c, "one"), "Worker held two jobs")
	check(not jobs.ClaimJob(b, "two"), "Two jobs occupied the same standing cell")
	check(jobs.ClaimNextJob("two", Vector2i.ZERO, []) == c, "Automatic claim did not skip occupied high-priority work")
	check(not jobs.SetJobApproachCell(a, Vector2i(20, 20)), "Active approach changed underneath moving worker")
	check(not jobs.TryReserveWorkCell(a, "intruder", Vector2i(8, 8)), "Another worker moved the reservation")
	check(not jobs.TryReserveWorkCell(a, "one", Vector2i(3, 2)), "Fallback overwrote another reservation")
	check(jobs.GetWorkCellReservation(Vector2i(2, 2)) == a, "Failed transfer lost original reservation")
	check(jobs.TryReserveWorkCell(a, "one", Vector2i(96, 96)), "Free fallback could not be reserved")
	check(cells.IsChunkPinned(Vector2i(3, 3)), "Fallback work chunk was not pinned")
	check(jobs.ClaimJob(b, "three"), "Transfer did not release former standing cell")
	check(jobs.ReleaseJob(a, "one"), "Claim release failed")
	check(not cells.IsChunkPinned(Vector2i(3, 3)), "Released fallback retained terrain pin")
	check(jobs.GetWorkCellReservation(Vector2i(96, 96)).is_empty(), "Release leaked work reservation")
	jobs.CompleteJob(c, "two")
	check(jobs.FindClaimedJobId("two").is_empty(), "Completion retained worker claim")
	jobs.CancelJob(b, "cancelled")
	check(jobs.GetWorkCellReservation(Vector2i(2, 2)).is_empty(), "Cancellation retained cell claim")
	check(jobs.ClaimJob(a, "one"), "Released job could not be reclaimed")
	jobs.ClearJobs()
	check(jobs.FindClaimedJobId("one").is_empty() and jobs.GetWorkCellReservation(Vector2i(2, 2)).is_empty(), "Clear retained reservation indexes")
	# Claimed restore is deterministic and cannot create duplicate workers or standing cells.
	jobs.RequeueClaimedJobsOnLoad = false
	var records := [
		{"id": "a", "cell": Vector2i(1, 1), "reserved_cell": Vector2i(33, 33), "state": "Claimed", "claimed_by": "owner"},
		{"id": "b", "cell": Vector2i(2, 1), "reserved_cell": Vector2i(33, 33), "state": "Claimed", "claimed_by": "other"},
		{"id": "c", "cell": Vector2i(3, 1), "state": "Claimed", "claimed_by": "owner"}]
	jobs.LoadJobs(records, true)
	check(jobs.ClaimedCount == 1 and jobs.QueuedCount == 2, "Restore accepted conflicting claims")
	check(jobs.GetWorkCellReservation(Vector2i(33, 33)) == "a", "Restore lost fallback reservation")
	var saved: Array = jobs.GetJobs()
	jobs.LoadJobs(saved, true)
	check(jobs.GetReservedWorkCell("a") == Vector2i(33, 33), "Fallback reservation did not round-trip")
	jobs.RequeueClaimedJobsOnLoad = true
	jobs.LoadJobs(saved, true)
	check(jobs.ClaimedCount == 0 and jobs.GetWorkCellReservation(Vector2i(33, 33)).is_empty(), "Requeued restore kept reservations")
	# Nested observers see the reservation before JobClaimed and its release before JobReleased.
	var attempted := [false]
	var on_claim := func(id, _worker):
		if id == "a": attempted[0] = jobs.ClaimJob("a", "observer")
	jobs.JobClaimed.connect(on_claim)
	check(jobs.ClaimJob("a", "owner") and not attempted[0], "Claim callback raced reservation publication")
	jobs.JobClaimed.disconnect(on_claim)
	var on_release := func(id, _worker): jobs.ClaimJob(id, "observer")
	jobs.JobReleased.connect(on_release)
	jobs.ReleaseJob("a", "owner")
	check(jobs.FindClaimedJobId("observer") == "a", "Release callback saw a stale reservation")
	jobs.JobReleased.disconnect(on_release)
	jobs.ClearJobs()
	var cancelled: String = jobs.AddJob(Vector2i.ZERO, "cancel_on_claim", 1.0, 0)
	var cancel_on_claim := func(id, _worker): jobs.CancelJob(id, "observer")
	jobs.JobClaimed.connect(cancel_on_claim)
	check(not jobs.ClaimJob(cancelled, "owner"), "Claim reported success after synchronous cancellation")
	check(jobs.FindClaimedJobId("owner").is_empty(), "Cancelled callback leaked claim")
	jobs.JobClaimed.disconnect(cancel_on_claim)
	# A terminal callback can replace the queue; outer cleanup must not delete its new claim.
	jobs.RemoveCompletedJobs = true
	jobs.RemoveCancelledJobs = true
	jobs.RequeueClaimedJobsOnLoad = false
	for terminal in ["JobCompleted", "JobCancelled"]:
		jobs.ClearJobs()
		var replaced: String = jobs.AddJob(Vector2i.ZERO, "replace", 1.0, 0)
		check(jobs.ClaimJob(replaced, "old"), "Callback fixture claim failed")
		var on_terminal := func(id, _who):
			jobs.LoadJobs([{"id": id, "cell": Vector2i(4, 4), "state": "Claimed", "claimed_by": "new"}], true)
		jobs.connect(terminal, on_terminal)
		if terminal == "JobCompleted": jobs.CompleteJob(replaced, "old")
		else: jobs.CancelJob(replaced, "fixture")
		jobs.disconnect(terminal, on_terminal)
		check(jobs.FindClaimedJobId("new") == replaced and jobs.HasJob(replaced), "Terminal callback replacement was deleted by old cleanup")
	# Distance arithmetic must not overflow across large finite coordinates.
	jobs.ClearJobs()
	jobs.AddJob(Vector2i(-2147483647, 0), "far", 1.0, 0)
	var near: String = jobs.AddJob(Vector2i(2147483645, 0), "near", 1.0, 0)
	check(jobs.ClaimNextJob("owner", Vector2i(2147483646, 0), []) == near, "Large coordinates overflowed nearest-job choice")
	host.free()
	print("[terrain-job-reservations] OK" if failures.is_empty() else "[terrain-job-reservations] FAILED")
	quit(0 if failures.is_empty() else 1)
