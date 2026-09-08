extends SceneTree

# A build with RequiredMaterials must not create its job - so a generic
# worker cannot claim and complete it - until every required resource has
# been physically delivered into a GridStorageComponent found on the placed
# node. Once stocked, the materials are consumed and the job is queued like
# any other job kind - a GridWorkerComponent with "build" in its
# AllowedJobKinds (or no filter at all) claims and completes it through the
# SAME pull-based GridJobQueueComponent every other job kind uses. A build
# with no RequiredMaterials is untouched: job created immediately.
#
# Construction has no dispatch path of its own - no separate IBuilder
# registry, no push-on-ready. GridBuildSiteComponent only ever queues a job;
# GridWorkerComponent is the one and only thing that claims and finishes it,
# exactly like a "gather" or "clear_land" job.

const BUILD_DEFINITION := preload("res://addons/beep_game_builder_cs/ecs/grid/GridBuildDefinition.cs")
const BUILD_CATALOG := preload("res://addons/beep_game_builder_cs/ecs/grid/GridBuildCatalogComponent.cs")
const BUILD_SITE := preload("res://addons/beep_game_builder_cs/ecs/grid/GridBuildSiteComponent.cs")
const JOB_QUEUE := preload("res://addons/beep_game_builder_cs/ecs/grid/GridJobQueueComponent.cs")
const STORAGE := preload("res://addons/beep_game_builder_cs/ecs/grid/GridStorageComponent.cs")
const RESOURCE_AMOUNT := preload("res://addons/beep_game_builder_cs/ecs/grid/GridResourceAmount.cs")
const HAULER := preload("res://addons/beep_game_builder_cs/ecs/grid/GridHaulerComponent.cs")
const GRID_PROJECTION := preload("res://addons/beep_game_builder_cs/ecs/grid/GridProjectionComponent.cs")
const GRID_NAVIGATION := preload("res://addons/beep_game_builder_cs/ecs/grid/GridNavigationComponent.cs")
const GRID_PATH_FOLLOWER := preload("res://addons/beep_game_builder_cs/ecs/grid/GridPathFollowerComponent.cs")
const GRID_WORKER := preload("res://addons/beep_game_builder_cs/ecs/grid/GridWorkerComponent.cs")

var failures: Array[String] = []

func check(ok: bool, message: String) -> void:
	if ok:
		print("  ok    %s" % message)
	else:
		print("  FAIL  %s" % message)
		failures.append(message)

func make_definition(build_id: String, required: Array) -> Resource:
	var d: Resource = BUILD_DEFINITION.new()
	d.set("BuildId", build_id)
	d.set("BuildTurns", 1)
	d.set("JobKind", "build")
	var materials: Array[Resource] = []
	for pair in required:
		var amount: Resource = RESOURCE_AMOUNT.new()
		amount.set("ResourceId", pair[0])
		amount.set("Amount", pair[1])
		materials.append(amount)
	d.set("RequiredMaterials", materials)
	return d

func _initialize() -> void:
	call_deferred("_run")

# Drives real process frames until the worker's own WorkerCompletedJob signal
# has fired `target_count` times, or the frame budget runs out - the same
# idiom the rest of this probe already uses for the shipped crane, now for a
# plain GridWorkerComponent. Watching the signal (not just "worker is idle
# again") matters: idle is also the worker's state before it has ever
# claimed anything, so an idle check alone would pass instantly without the
# worker doing any work at all.
func _await_completion_count(completions: Array, target_count: int, max_frames: int) -> bool:
	for i in range(max_frames):
		if completions.size() >= target_count:
			return true
		await process_frame
	return completions.size() >= target_count

func _run() -> void:
	var jobs: Node = JOB_QUEUE.new()
	jobs.name = "Jobs"
	root.add_child(jobs)

	var catalog: Node = BUILD_CATALOG.new()
	catalog.name = "Catalog"
	catalog.set("Builds", [make_definition("workshop", [["wood", 5]])])
	root.add_child(catalog)

	var site: Node = BUILD_SITE.new()
	site.name = "Site"
	site.set("BuildCatalogPath", NodePath("../Catalog"))
	site.set("JobQueuePath", NodePath("../Jobs"))
	root.add_child(site)
	await process_frame

	# --- a real GridWorkerComponent, wired with a real grid/navigation/path
	# follower, exactly the same rig any gather/clear/till worker uses. No
	# separate construction dispatcher exists any more - this is the only
	# thing in the scene that can ever claim a "build" job.
	var grid: Node = GRID_PROJECTION.new()
	grid.name = "Grid"
	grid.set("Projection", 0) # TopDown
	grid.set("TileSize", Vector2(32, 32))
	root.add_child(grid)

	var nav: Node = GRID_NAVIGATION.new()
	nav.name = "Navigation"
	nav.set("BoundsSize", Vector2i(12, 12))
	nav.set("Diagonals", 0) # Never
	root.add_child(nav)

	var worker_body := Node2D.new()
	worker_body.name = "Crew"
	root.add_child(worker_body)
	worker_body.global_position = grid.call("CellToWorld", Vector2i(0, 0))

	var follower: Node = GRID_PATH_FOLLOWER.new()
	follower.name = "PathFollower"
	follower.set("GridPath", NodePath("../../Grid"))
	follower.set("NavigationPath", NodePath("../../Navigation"))
	follower.set("Speed", 400.0)
	follower.set("DriveCharacterBody", false)
	follower.set("SetZIndexFromY", false)
	worker_body.add_child(follower)

	var worker: Node = GRID_WORKER.new()
	worker.name = "Worker"
	worker.set("WorkerId", "crew_1")
	worker.set("JobQueuePath", NodePath("../../Jobs"))
	worker.set("GridPath", NodePath("../../Grid"))
	worker.set("PathFollowerPath", NodePath("../PathFollower"))
	worker.set("ClaimIntervalSeconds", 0.05)
	worker_body.add_child(worker)

	var worker_completions: Array = []
	var worker_failures: Array = []
	worker.connect("WorkerCompletedJob", func(_worker_id: String, job_id: String) -> void:
		worker_completions.append(job_id))
	worker.connect("WorkerFailedJob", func(_worker_id: String, job_id: String, reason: String) -> void:
		worker_failures.append([job_id, reason]))
	await process_frame

	# --- gated on delivery ---
	var placed := Node2D.new()
	placed.name = "Workshop"
	var storage: Node = STORAGE.new()
	storage.name = "Storage"
	placed.add_child(storage)
	root.add_child(placed)
	await process_frame

	var accepted: bool = site.call("RegisterPlacedBuild", "workshop", placed, Vector2i(4, 4))
	check(accepted, "a build with RequiredMaterials is accepted, not rejected, before delivery")
	check(int(jobs.get("QueuedCount")) == 0, "no job exists yet - a worker cannot claim what is not there")
	check(int(site.get("PendingMaterialsCount")) == 1, "the site reports itself awaiting materials")

	storage.call("Load", "wood", 2)
	check(int(jobs.get("QueuedCount")) == 0, "a partial delivery still does not start the job")

	storage.call("Load", "wood", 3)
	check(int(site.get("PendingMaterialsCount")) == 0, "the site stops awaiting materials once fully stocked")
	check(int(storage.call("Stored", "wood")) == 0, "delivered materials are consumed into the build, not left sitting in the hold")
	check(int(jobs.get("QueuedCount")) == 1, "the job is queued the instant the site is fully stocked - no push dispatch, just the ordinary queue")

	var workshop_completed: bool = await _await_completion_count(worker_completions, 1, 600)
	check(workshop_completed, "the plain GridWorkerComponent claimed the queued build job and finished working it")
	check(str(placed.get_meta("grid_build_site_state")) == "complete", "the placed node flips to complete once its worker finishes")

	var jobs_after_workshop: int = int(jobs.get("QueuedCount")) + int(jobs.get("ClaimedCount")) + int(jobs.get("CompletedCount"))
	check(jobs_after_workshop == 0, "the worker claimed and completed the job through the ordinary ledger, leaving nothing queued or claimed behind")

	# --- rejected when there is nowhere to deliver to ---
	var bare := Node2D.new()
	bare.name = "BareWorkshop"
	root.add_child(bare)
	await process_frame
	var rejections: Array = []
	var reject_callable := func(build_id: String, x: int, y: int, reason: String) -> void:
		rejections.append(reason)
	site.connect("BuildSiteRejected", reject_callable)
	var bare_accepted: bool = site.call("RegisterPlacedBuild", "workshop", bare, Vector2i(9, 9))
	check(not bare_accepted and rejections.size() == 1 and rejections[0] == "missing_material_storage",
		"a build that needs materials but has nowhere to receive them is rejected, not silently stuck forever")

	# --- unchanged path: no RequiredMaterials still starts immediately, and
	# the same worker (idle again after the workshop) claims and finishes it -
	# proving one generic worker can carry arbitrarily many build jobs, one
	# after another, with nothing constructed for it beyond a job kind.
	catalog.set("Builds", [make_definition("hut", [])])
	var hut := Node2D.new()
	hut.name = "Hut"
	root.add_child(hut)
	await process_frame
	var hut_accepted: bool = site.call("RegisterPlacedBuild", "hut", hut, Vector2i(1, 1))
	check(hut_accepted and int(jobs.get("QueuedCount")) == 1,
		"a build with no RequiredMaterials still creates its job immediately, unchanged")

	var hut_completed: bool = await _await_completion_count(worker_completions, 2, 600)
	check(hut_completed, "the same worker picks up the hut's job once it returns to idle")
	check(str(hut.get_meta("grid_build_site_state")) == "complete", "the hut flips to complete once the worker finishes it")

	# --- proving the actual connection: a REAL ITransporter delivering into a
	# real build site, not this probe simulating delivery with a bare Load()
	# call, and then a REAL GridWorkerComponent finishing the job that
	# delivery unlocked - no simulated shortcut anywhere in the chain.
	# GridStorageComponent is a standard ILoadPort, so nothing bespoke was
	# built for building to receive from hauling - the site is just another
	# destination a hauler's DepotStoragePath can point at, the same as any
	# tank or silo. Driven through TryDeliverCargo directly rather than
	# RequestHaul, matching how the transport layer's own probe
	# (grid_terrain_subsurface_probe.gd) tests a hauler's delivery half in
	# isolation, without standing up real grid navigation for it to drive on.
	catalog.set("Builds", [make_definition("warehouse", [["stone", 4]])])
	var warehouse := Node2D.new()
	warehouse.name = "Warehouse"
	var warehouse_storage: Node = STORAGE.new()
	warehouse_storage.name = "Storage"
	warehouse.add_child(warehouse_storage)
	root.add_child(warehouse)
	await process_frame

	var warehouse_accepted: bool = site.call("RegisterPlacedBuild", "warehouse", warehouse, Vector2i(6, 6))
	check(warehouse_accepted and int(site.get("PendingMaterialsCount")) == 1,
		"the warehouse is likewise accepted and awaiting its stone")

	var quarry_truck_body := Node2D.new()
	quarry_truck_body.name = "QuarryTruck"
	root.add_child(quarry_truck_body)
	var quarry_truck: Node = HAULER.new()
	quarry_truck.set("RegisterOnReady", false)
	quarry_truck.set("DepotStoragePath", NodePath("../../Warehouse/Storage"))
	quarry_truck_body.add_child(quarry_truck)
	await process_frame

	check(int(quarry_truck.call("Load", "stone", 4)) == 4, "the truck's own hold takes on cargo picked up elsewhere")
	var delivered: bool = bool(quarry_truck.call("TryDeliverCargo"))
	check(delivered and int(warehouse_storage.call("Stored", "stone")) == 0,
		"TryDeliverCargo hands the cargo through GridPorts.Transfer into the warehouse's real ILoadPort")
	check(int(site.get("PendingMaterialsCount")) == 0,
		"the warehouse's own StorageChanged signal - fired by the REAL transfer, not a direct call from this probe - is what tells the site it is stocked")
	check(int(jobs.get("QueuedCount")) == 1,
		"a real transporter's delivery closes the loop all the way to a claimable build job, with no simulated shortcut anywhere in the chain")

	var warehouse_completed: bool = await _await_completion_count(worker_completions, 3, 600)
	check(warehouse_completed, "the same plain worker claims and finishes the warehouse job the real delivery unlocked")
	check(str(warehouse.get_meta("grid_build_site_state")) == "complete",
		"the warehouse flips to complete - delivery, materials-gating, queuing, and construction all real, end to end, with only a worker doing the building")

	check(worker_failures.is_empty(), "the worker never failed a build job along the way (failures: %s)" % [worker_failures])

	print("RESULT: ", "all checks passed" if failures.is_empty() else "%d FAILED" % failures.size())
	quit(1 if failures.size() > 0 else 0)
