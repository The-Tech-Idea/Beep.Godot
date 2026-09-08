extends SceneTree

# IWorker: a formal, minimal contract (WorkerId, IsWorking, CurrentJobId) any
# custom worker - human, crane, robot, GDScript duck-typed - can answer, so a
# system built on top of it (the construction-in-progress effect family)
# works for ALL of them, not just GridWorkerComponent. Job progress is a
# fact GridJobQueueComponent owns (ReportProgress/GetJobProgress01), fed by
# whoever executes the job - never derived by a structure-side effect going
# to find "the worker".
#
# The grid layer never draws. The structure-side effects only ORCHESTRATE:
# GridBuildProgressBarComponent instantiates an authored Range scene and
# sets its value; GridBuildStageVisualComponent instantiates the build's
# stage scenes and tells them the site through IConstructionVisual
# (SiteFootprint, CellSize, BuildProgress - read by name, so a pure GDScript
# scene answers it). This probe proves both with a duck-typed GDScript stage
# and a plain Godot ProgressBar, no kit and no C# on the presenting side.

const JOB_QUEUE := preload("res://addons/beep_game_builder_cs/ecs/grid/GridJobQueueComponent.cs")
const GRID_PROJECTION := preload("res://addons/beep_game_builder_cs/ecs/grid/GridProjectionComponent.cs")
const GRID_NAVIGATION := preload("res://addons/beep_game_builder_cs/ecs/grid/GridNavigationComponent.cs")
const GRID_PATH_FOLLOWER := preload("res://addons/beep_game_builder_cs/ecs/grid/GridPathFollowerComponent.cs")
const GRID_WORKER := preload("res://addons/beep_game_builder_cs/ecs/grid/GridWorkerComponent.cs")
const BUILD_CATALOG := preload("res://addons/beep_game_builder_cs/ecs/grid/GridBuildCatalogComponent.cs")
const BUILD_SITE := preload("res://addons/beep_game_builder_cs/ecs/grid/GridBuildSiteComponent.cs")
const BUILD_DEFINITION := preload("res://addons/beep_game_builder_cs/ecs/grid/GridBuildDefinition.cs")
const STORAGE := preload("res://addons/beep_game_builder_cs/ecs/grid/GridStorageComponent.cs")
const PROGRESS_BAR := preload("res://addons/beep_game_builder_cs/ecs/grid/GridBuildProgressBarComponent.cs")
const STAGE_VISUAL := preload("res://addons/beep_game_builder_cs/ecs/grid/GridBuildStageVisualComponent.cs")
const ACTIVITY_EFFECT := preload("res://addons/beep_game_builder_cs/ecs/grid/GridWorkerBuildActivityEffectComponent.cs")
const PARTICLE_COMPONENT := preload("res://addons/beep_game_builder_cs/ecs/ParticleComponent.cs")

# A construction-stage scene in PURE GDSCRIPT answering IConstructionVisual
# by name - what a project's own stage art looks like to the grid layer.
const PROBE_STAGE_SOURCE := """
extends Node2D
var SiteFootprint: Vector2i = Vector2i(1, 1)
var CellSize: Vector2 = Vector2.ZERO
var StageIndex: int = -1
var StageCount: int = -1
var SiteState: String = ""
var SiteMaterials: Array = []
var BuildProgress: float = -1.0
var pulses: int = 0
func WorkPulse() -> void:
	pulses += 1
"""

# A worker written in PURE GDSCRIPT - no interface, no C# - just the IWorker
# contract's members by name, executing a job through the SAME job-queue
# ledger and the SAME ReportProgress a real GridWorkerComponent uses. Proves
# job progress (and everything built on it) is genuinely worker-agnostic,
# not secretly GridWorkerComponent-specific.
class MachineWorker extends Node:
	var WorkerId := "crane_1"
	var IsWorking := false
	var CurrentJobId := ""

var failures: Array[String] = []

func check(ok: bool, message: String) -> void:
	if ok:
		print("  ok    %s" % message)
	else:
		print("  FAIL  %s" % message)
		failures.append(message)

func almost(a: float, b: float, tolerance: float = 0.01) -> bool:
	return absf(a - b) <= tolerance

func make_definition(build_id: String, stages: Array, footprint: Vector2i, materials: Array = []) -> Resource:
	var d: Resource = BUILD_DEFINITION.new()
	d.set("BuildId", build_id)
	d.set("BuildTurns", 10)
	d.set("JobKind", "build")
	d.set("Footprint", footprint)
	d.set("ConstructionStages", stages)
	d.set("RequiredMaterials", materials)
	return d

func pack(node: Node) -> PackedScene:
	var packed := PackedScene.new()
	var result := packed.pack(node)
	node.free()
	assert(result == OK, "Could not pack probe scene")
	return packed

var _probe_stage_script: GDScript

func probe_stage_node() -> Node2D:
	if _probe_stage_script == null:
		_probe_stage_script = GDScript.new()
		_probe_stage_script.source_code = PROBE_STAGE_SOURCE
		_probe_stage_script.reload()
	var n := Node2D.new()
	n.set_script(_probe_stage_script)
	return n

# Components refresh on a real-time interval, and headless frames are not
# 1/60 s - a frame-count wait passes or fails by luck. Wait by the clock.
func settle(seconds: float = 0.08) -> void:
	var target := Time.get_ticks_msec() + int(seconds * 1000)
	while Time.get_ticks_msec() < target:
		await process_frame

func await_state(predicate: Callable, description: String) -> void:
	var deadline := Time.get_ticks_msec() + 5000
	while not predicate.call() and Time.get_ticks_msec() < deadline:
		await process_frame
	check(predicate.call(), "interval-refreshed visuals reached " + description)

func find_range_under(node: Node) -> Node:
	if node is Range:
		return node
	for child in node.get_children():
		var found := find_range_under(child)
		if found != null:
			return found
	return null

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var jobs: Node = JOB_QUEUE.new()
	jobs.name = "Jobs"
	jobs.set("RemoveCompletedJobs", false)
	root.add_child(jobs)
	await process_frame

	# --- part A: progress is a fact the queue owns, computed from RemainingTurns/WorkTurns ---
	var job_id: String = jobs.call("AddJob", Vector2i(0, 0), "build", 10.0, 0)
	check(almost(float(jobs.call("GetJobProgress01", job_id)), 0.0), "a freshly created job reports 0% progress")

	jobs.call("ReportProgress", job_id, 5.0)
	check(almost(float(jobs.call("GetJobProgress01", job_id)), 0.5), "reporting half the work remaining reads back as 50% progress")

	jobs.call("ReportProgress", job_id, 0.0)
	check(almost(float(jobs.call("GetJobProgress01", job_id)), 1.0), "reporting zero seconds remaining reads back as 100% progress")

	# falsify: an input that WOULD read outside [0,1] if the clamp were
	# missing - proves the guard can actually fail, not just pass by luck.
	jobs.call("ReportProgress", job_id, -5.0)
	check(almost(float(jobs.call("GetJobProgress01", job_id)), 1.0), "falsified: negative remaining-seconds still clamps to 100%, not >100%")
	jobs.call("ReportProgress", job_id, 25.0)
	check(almost(float(jobs.call("GetJobProgress01", job_id)), 0.0), "falsified: remaining-seconds far past the total still clamps to 0%, not <0%")

	check(almost(float(jobs.call("GetJobProgress01", "no_such_job")), 0.0), "an unknown job id reads as 0% progress, not an error")

	jobs.call("CancelJob", job_id, "cleanup")

	# --- part B: a real, ticking GridWorkerComponent reports progress as it works ---
	var grid: Node = GRID_PROJECTION.new()
	grid.name = "Grid"
	grid.set("TileSize", Vector2(32, 32))
	root.add_child(grid)

	var nav: Node = GRID_NAVIGATION.new()
	nav.name = "Navigation"
	nav.set("BoundsSize", Vector2i(12, 12))
	root.add_child(nav)

	var worker_body := Node2D.new()
	worker_body.name = "Crew"
	root.add_child(worker_body)
	worker_body.global_position = grid.call("CellToWorld", Vector2i(0, 0))

	var follower: Node = GRID_PATH_FOLLOWER.new()
	follower.name = "PathFollower"
	follower.set("GridPath", NodePath("../../Grid"))
	follower.set("NavigationPath", NodePath("../../Navigation"))
	follower.set("Speed", 800.0)
	follower.set("DriveCharacterBody", false)
	follower.set("SetZIndexFromY", false)
	worker_body.add_child(follower)

	var worker: Node = GRID_WORKER.new()
	worker.name = "Worker"
	worker.set("WorkerId", "worker_1")
	worker.set("JobQueuePath", NodePath("../../Jobs"))
	worker.set("GridPath", NodePath("../../Grid"))
	worker.set("PathFollowerPath", NodePath("../PathFollower"))
	worker.set("ClaimIntervalSeconds", 0.05)
	worker_body.add_child(worker)

	var particle: Node = PARTICLE_COMPONENT.new()
	particle.name = "Particle"
	# A repeating activity effect needs the emitter kept alive between
	# bursts; ParticleComponent's default frees it after the first.
	particle.set("AutoQueueFree", false)
	worker_body.add_child(particle)

	var activity: Node = ACTIVITY_EFFECT.new()
	activity.name = "Activity"
	activity.set("JobQueuePath", NodePath("../../Jobs"))
	activity.set("BurstIntervalSeconds", 0.05)
	worker_body.add_child(activity)

	var bursts: Array = []
	particle.connect("BurstPlayed", func() -> void: bursts.append(true))
	await process_frame

	check(bool(worker.get("IsWorking")) == false, "a fresh worker is not IWorker.IsWorking while idle")
	check(str(worker.get("CurrentJobId")) == "", "a fresh worker's IWorker.CurrentJobId is empty while idle")

	var near_job: String = jobs.call("AddJob", Vector2i(1, 0), "build", 1.0, 0)
	var mid_progress: Array = []
	var completed: Array = []
	worker.connect("WorkerCompletedJob", func(_worker_id: String, worker_job_id: String) -> void: completed.append(worker_job_id))

	var saw_working := false
	for i in range(300):
		await process_frame
		if bool(worker.get("IsWorking")) and str(worker.get("CurrentJobId")) == near_job:
			saw_working = true
			var p: float = float(jobs.call("GetJobProgress01", near_job))
			if p > 0.0:
				mid_progress.append(p)
		if completed.size() > 0:
			break

	check(saw_working, "the real worker actually entered IWorker.IsWorking for the claimed build job")
	check(mid_progress.size() > 0, "GridJobQueueComponent.GetJobProgress01 advanced mid-job, fed by the real worker's own Tick - not just at start/end")
	var out_of_range: Array = mid_progress.filter(func(p: float) -> bool: return p <= 0.0 or p >= 1.0)
	check(out_of_range.is_empty(), "every one of %d mid-job progress samples stayed strictly between 0 and 1 (violations: %s)" % [mid_progress.size(), out_of_range])
	check(completed.size() == 1 and completed[0] == near_job, "the real worker completed the exact job it was working")
	check(almost(float(jobs.call("GetJobProgress01", near_job)), 1.0), "the completed job reads back as 100% progress")
	check(bursts.size() > 0, "the sibling ParticleComponent actually burst while the worker was IsWorking on a 'build' job")
	check(bool(activity.get("IsEffectActive")) == false, "the activity effect turns itself off once the worker returns to idle")

	# --- part C: a duck-typed, non-C# worker reports progress through the SAME mechanism ---
	var machine := MachineWorker.new()
	machine.name = "Crane"
	root.add_child(machine)
	await process_frame

	var machine_job: String = jobs.call("AddJob", Vector2i(5, 5), "build", 8.0, 0)
	check(bool(jobs.call("ClaimJob", machine_job, machine.WorkerId)), "the duck-typed machine worker claims a job through the ordinary queue API")
	machine.IsWorking = true
	machine.CurrentJobId = machine_job

	jobs.call("ReportProgress", machine_job, 6.0)
	check(almost(float(jobs.call("GetJobProgress01", machine_job)), 0.25), "the machine worker's own ReportProgress call advances the SAME job-owned progress fact")
	jobs.call("ReportProgress", machine_job, 2.0)
	check(almost(float(jobs.call("GetJobProgress01", machine_job)), 0.75), "progress keeps advancing as the machine worker keeps reporting")

	check(bool(jobs.call("CompleteJob", machine_job, machine.WorkerId)), "the machine worker completes its job through the ordinary queue API, with no GridWorkerComponent involved anywhere")
	machine.IsWorking = false
	machine.CurrentJobId = ""

	worker_body.queue_free()
	machine.queue_free()
	await process_frame

	# --- parts D-F: the structure-side effects, driven purely by job progress -
	# no worker rig needed, proving they never have to go "find the worker" ---
	var catalog: Node = BUILD_CATALOG.new()
	catalog.name = "Catalog"
	root.add_child(catalog)

	var site: Node = BUILD_SITE.new()
	var cells: Node = load("res://addons/beep_game_builder_cs/ecs/grid/GridCellDataComponent.cs").new()
	cells.name = "Cells"
	root.add_child(cells)
	site.name = "Site"
	site.set("ChunkCellDataPath", NodePath("../Cells"))
	site.set("BuildCatalogPath", NodePath("../Catalog"))
	site.set("JobQueuePath", NodePath("../Jobs"))
	root.add_child(site)

	# The shipped default bar scene (a KitMeter) ...
	var bar: Node = PROGRESS_BAR.new()
	bar.name = "ProgressBar"
	bar.set("BuildSitePath", NodePath("../Site"))
	bar.set("JobQueuePath", NodePath("../Jobs"))
	bar.set("RefreshIntervalSeconds", 0.01)
	root.add_child(bar)

	# ... and a plain Godot ProgressBar authored 0..100, no kit anywhere -
	# the component must drive either by its own Range min/max.
	var plain_scene_root := ProgressBar.new()
	plain_scene_root.min_value = 0.0
	plain_scene_root.max_value = 100.0
	plain_scene_root.size = Vector2(40, 6)
	var plain_bar: Node = PROGRESS_BAR.new()
	plain_bar.name = "PlainProgressBar"
	plain_bar.set("BuildSitePath", NodePath("../Site"))
	plain_bar.set("JobQueuePath", NodePath("../Jobs"))
	plain_bar.set("RefreshIntervalSeconds", 0.01)
	plain_bar.set("ProgressBarScene", pack(plain_scene_root))
	root.add_child(plain_bar)

	var stage0 := pack(probe_stage_node())
	var stage1 := pack(probe_stage_node())
	var stage2 := pack(probe_stage_node())
	var stage_visual: Node = STAGE_VISUAL.new()
	stage_visual.name = "StageVisual"
	stage_visual.set("BuildSitePath", NodePath("../Site"))
	stage_visual.set("BuildCatalogPath", NodePath("../Catalog"))
	stage_visual.set("JobQueuePath", NodePath("../Jobs"))
	stage_visual.set("GridPath", NodePath("../Grid"))
	stage_visual.set("RefreshIntervalSeconds", 0.01)
	stage_visual.set("WorkPulseIntervalSeconds", 0.1)
	stage_visual.set("TeardownSeconds", 0.3)
	root.add_child(stage_visual)
	await process_frame

	# A build that needs two planks delivered before its job can exist -
	# the site's whole life, from staked plot to teardown.
	catalog.set("Builds", [make_definition("tower", [stage0, stage1, stage2], Vector2i(3, 2),
		[{"ResourceId": "planks", "Amount": 2}])])
	# The placed node itself answers the contract too - a build's own Scene
	# root that wants to present its state and progress.
	var placed := probe_stage_node()
	placed.name = "Tower"
	root.add_child(placed)
	var storage: Node = STORAGE.new()
	storage.name = "Storage"
	placed.add_child(storage)
	await process_frame

	var accepted: bool = site.call("RegisterPlacedBuild", "tower", placed, Vector2i(3, 3))
	check(accepted, "the structure-side effects' shared build site accepts the placement")
	check(placed.visible == false, "a build with ConstructionStages has its finished art HIDDEN from placement - the stage scenes are its under-construction look, and no real game shows the finished building through its own site")
	check(int(jobs.get("QueuedCount")) == 0, "no job exists yet - the site is waiting for its materials")
	check(cells.IsChunkPinned(Vector2i.ZERO), "material-waiting construction pins terrain before a job exists")

	# --- pending: the site exists before the job does ---
	await settle()
	var pending_stages: Array = stage_visual.call("StagesForPlaced", placed)
	check(pending_stages.size() == 3, "the site visual is created on BuildSiteAwaitingMaterials - the delivery beat happens BEFORE the job, in a state the old design never drew")
	var s0: Node = pending_stages[0] if pending_stages.size() > 0 else null
	check(s0 != null and str(s0.get("SiteState")) == "pending", "the stage scene is told SiteState 'pending'")
	check(s0 != null and s0.get("SiteFootprint") == Vector2i(3, 2), "a duck-typed GDScript stage scene was told the build's SiteFootprint (3x2)")
	check(s0 != null and s0.get("CellSize") == Vector2(32, 32), "the stage scene was told the grid's CellSize (32x32)")
	check(s0 != null and int(s0.get("StageIndex")) == 0 and int(s0.get("StageCount")) == 3, "the stage scene is told which listed stage it is (0 of 3)")
	var mats: Array = s0.get("SiteMaterials") if s0 != null else []
	check(mats.size() == 1 and str(mats[0].get("id", "")) == "planks" and int(mats[0].get("required", 0)) == 2 and int(mats[0].get("delivered", -1)) == 0,
		"SiteMaterials comes from RequiredMaterials (planks x2, 0 delivered) - the physical stock, never Costs")
	var site_root: Node = stage_visual.call("SiteRootForPlaced", placed)
	check(site_root != null and site_root.get_parent() == stage_visual, "stage scenes live under a site root that is a child of the component, outside the placed node's modulate")
	check(s0 != null and s0.get_parent() == site_root, "the stage scene's parent is that site root, not the placed node")
	check(site_root != null and site_root.global_position == placed.global_position, "the site root sits exactly on the placed node")
	var expected_z := int(round(placed.global_position.y + (2 - 0.5) * 32.0))
	check(site_root != null and site_root.z_as_relative == false and site_root.z_index == expected_z,
		"the site root's z is the footprint's FRONT edge in the absolute-y domain units use (%d), so a worker in front draws over the wall" % expected_z)
	check(str(placed.get("SiteState")) == "pending" and placed.get("SiteFootprint") == Vector2i(3, 2), "the placed node itself, answering the contract, was configured the same way")

	# --- deliveries arrive: the stock on site is what the scene is told ---
	storage.call("Load", "planks", 1)
	await settle()
	mats = s0.get("SiteMaterials")
	check(mats.size() == 1 and int(mats[0].get("delivered", -1)) == 1, "one plank delivered reads back as delivered 1 while pending")
	check(int(jobs.get("QueuedCount")) == 0, "one of two planks is not enough - still no job")

	storage.call("Load", "planks", 1)
	await settle()
	var tower_job_id := ""
	for entry in jobs.call("GetJobs"):
		if int(entry.get("cell", Vector2i(-1, -1)).x) == 3 and int(entry.get("cell", Vector2i(-1, -1)).y) == 3:
			tower_job_id = str(entry.get("id", ""))
	check(tower_job_id != "", "the last delivery creates the build job")
	check(jobs.call("GetJobApproachCell", tower_job_id) == Vector2i(4, 5), "the job owns an approach cell just in FRONT of the 3x2 footprint at (3,3): (4,5) - outside the blocked footprint, where a worker can actually stand")
	check(str(jobs.call("GetJobClaimedBy", tower_job_id)) == "", "queued visual fixture has no worker claim")
	# Job creation and the presenters' interval refresh need not land in the same frame.
	await await_state(func() -> bool:
		return str(stage_visual.call("SiteStateForJob", tower_job_id)) == "queued" \
			and s0 != null and str(s0.get("SiteState")) == "queued" \
			and bool(bar.call("IsStalled", tower_job_id)), "queued/stalled after delivery")
	check(str(stage_visual.call("SiteStateForJob", tower_job_id)) == "queued" and str(s0.get("SiteState")) == "queued", "with a job nobody holds, the site is 'queued'")
	check(int(stage_visual.call("VisibleStageIndex", tower_job_id)) == 0, "the stage visual shows stage 0 at 0% progress")

	# --- the bar: stalled while nobody holds the job ---
	check(int(bar.get("ActiveBarCount")) == 1, "GridBuildProgressBarComponent tracked the new build site from its BuildSiteCreated signal alone")
	check(almost(float(bar.call("ProgressForJob", tower_job_id)), 0.0), "the progress bar starts at 0%")
	var bar_node: Node = bar.call("BarForJob", tower_job_id)
	check(bar_node != null and bar_node is Range, "the shipped default bar scene loaded and roots a Range")
	check(bar_node != null and bar_node.get_parent() == bar, "the bar is an overlay under its own component, not under the placed node")
	check(find_range_under(placed) == null, "nothing under the placed node is a Range - an effect applied to the building's art can never reach the bar")
	check(bar_node != null and bar_node.get("mouse_filter") == Control.MOUSE_FILTER_IGNORE, "the bar never takes the click meant for the world under it")
	check(bool(bar.call("IsStalled", tower_job_id)), "the bar shows the STALLED look while the job is queued with no worker - an unstaffed site must read differently from a slow one")
	var plain_node: Node = plain_bar.call("BarForJob", tower_job_id)
	check(plain_node != null and plain_node.get_script() == null and plain_node is ProgressBar, "a plain Godot ProgressBar scene (no kit, no script) is accepted as the bar")

	# --- a worker takes the job: working, progress, pulses ---
	check(bool(jobs.call("ClaimJob", tower_job_id, "crew_1")), "a worker claims the job")
	await settle()
	check(str(s0.get("SiteState")) == "working", "the site is 'working' once the job is held")
	check(not bool(bar.call("IsStalled", tower_job_id)), "the stalled look clears once a worker holds the job")
	var pulses_before: int = int(s0.get("pulses"))
	await settle(0.3)
	check(int(s0.get("pulses")) == pulses_before, "no work landed (progress did not advance) - no WorkPulse")
	var remaining := 10.0
	var pulse_deadline := Time.get_ticks_msec() + 400
	while Time.get_ticks_msec() < pulse_deadline:
		remaining = maxf(6.0, remaining - 0.02)
		jobs.call("ReportProgress", tower_job_id, remaining)
		await process_frame
	check(int(s0.get("pulses")) > pulses_before, "WorkPulse reaches the visible stage scene while progress advances - a hit of work the scene can show at the point being worked, whatever the worker is")

	jobs.call("ReportProgress", tower_job_id, 3.5) # 65% of 10s
	await settle()
	check(almost(float(bar.call("ProgressForJob", tower_job_id)), 0.65, 0.02), "the progress bar tracks a mid-build progress change")
	check(plain_node != null and almost(float(plain_node.get("value")), 65.0, 2.0), "the plain 0..100 ProgressBar reads 65, driven through its own min/max, not assumed 0..1")
	check(int(stage_visual.call("VisibleStageIndex", tower_job_id)) == 1, "the stage visual shows stage 1 (floor(0.65*3)=1) mid-build")
	var s1: Node = stage_visual.call("StageForJob", tower_job_id, 1)
	check(s1 != null and almost(float(s1.get("BuildProgress")), 0.65, 0.02), "the visible stage scene's BuildProgress tracks the same mid-build progress")
	check(almost(float(placed.get("BuildProgress")), 0.65, 0.02), "the placed node's BuildProgress tracks it too")
	mats = s1.get("SiteMaterials") if s1 != null else []
	check(mats.size() == 1 and int(mats[0].get("delivered", -1)) == 2, "once the job started the stock reads as fully delivered - it was built in at StartBuildJob; the scene shows it being used up by progress")

	jobs.call("ReportProgress", tower_job_id, 1.0) # 90% - 1s of 10s remaining
	await settle()
	check(int(stage_visual.call("VisibleStageIndex", tower_job_id)) == 2, "the stage visual shows the final stage (floor(0.9*3)=2) near completion")

	# --- completion: hard swap now, teardown beat after ---
	jobs.call("CompleteJob", tower_job_id, "crew_1")
	await process_frame
	check(placed.visible == true, "the finished art is shown the instant the job completes - a hard swap, never a fade")
	check(cells.IsChunkPinned(Vector2i.ZERO), "finished structure retains its terrain after job completion")
	check(int(bar.get("ActiveBarCount")) == 0, "the progress bar removes its tracked entry once the build completes")
	check(str(stage_visual.call("SiteStateForJob", tower_job_id)) == "complete", "the site is told 'complete'")
	check(int(stage_visual.call("VisibleStageIndex", tower_job_id)) == 2, "the stage scenes are KEPT for the teardown beat - the scaffold comes down over the finished building")
	check(almost(float(placed.get("BuildProgress")), 1.0), "the placed node is told BuildProgress = 1 on completion, so a finished scene can turn its lights on")
	await settle(0.15)
	check(int(stage_visual.call("VisibleStageIndex", tower_job_id)) == 2, "halfway through TeardownSeconds (0.3 s) the stage scenes are still up")
	await settle(0.4)
	check(int(stage_visual.call("VisibleStageIndex", tower_job_id)) == -1, "after TeardownSeconds the stage scenes are freed")

	placed.free()
	check(cells.PinnedChunkCount == 0, "demolition releases finished structure terrain")
	print("RESULT: ", "all checks passed" if failures.is_empty() else "%d FAILED" % failures.size())
	quit(1 if failures.size() > 0 else 0)
