extends Node2D

## Demo of the construction-site effect family: four builds placed with
## RequiredMaterials, so each site runs the whole sequence a real game's
## site does - staked plot with a sign and empty pile spots (pending),
## materials arriving and piling up, the job appearing, a worker (the
## addon's own shipped "grid_worker_unit.tscn" truck; any IWorker would do)
## standing on the approach cell in front of the site while the slab pours,
## the frame and scaffold go up, walls and roof rise a course at a time
## with a hit effect and sound at the point being worked, then the hard
## swap to the finished building while the scaffold comes down.
##
## The grid layer draws none of it: GridBuildStageVisualComponent tells the
## family's construction-site scene the facts (footprint, cell size, state,
## materials, progress, work pulses) through IConstructionVisual and the
## scene renders them. GridBuildProgressBarComponent drives an authored
## KitMeter over each site (stalled look while nobody holds the job).
## GridWorkerBuildActivityEffectComponent bursts the worker's own dust
## while it works.
##
## The four sites - a wood workshop (2x2), a brick shed (2x2), a steel shed
## (3x2) and a LARGE wood workshop (4x4) - use ONE scene per material
## family; the scenes size themselves to the site they are told about.
##
## Deliveries are simulated here on a timer straight into each site's
## GridStorageComponent; in a full project an ITransporter hauler fills it
## exactly like any other cargo hold.

const JOB_QUEUE := preload("res://addons/beep_game_builder_cs/ecs/grid/GridJobQueueComponent.cs")
const GRID_PROJECTION := preload("res://addons/beep_game_builder_cs/ecs/grid/GridProjectionComponent.cs")
const GRID_NAVIGATION := preload("res://addons/beep_game_builder_cs/ecs/grid/GridNavigationComponent.cs")
const BUILD_CATALOG := preload("res://addons/beep_game_builder_cs/ecs/grid/GridBuildCatalogComponent.cs")
const BUILD_SITE := preload("res://addons/beep_game_builder_cs/ecs/grid/GridBuildSiteComponent.cs")
const BUILD_DEFINITION := preload("res://addons/beep_game_builder_cs/ecs/grid/GridBuildDefinition.cs")
const STORAGE := preload("res://addons/beep_game_builder_cs/ecs/grid/GridStorageComponent.cs")
const PROGRESS_BAR := preload("res://addons/beep_game_builder_cs/ecs/grid/GridBuildProgressBarComponent.cs")
const STAGE_VISUAL := preload("res://addons/beep_game_builder_cs/ecs/grid/GridBuildStageVisualComponent.cs")
const ACTIVITY_EFFECT := preload("res://addons/beep_game_builder_cs/ecs/grid/GridWorkerBuildActivityEffectComponent.cs")
const PARTICLE_COMPONENT := preload("res://addons/beep_game_builder_cs/ecs/ParticleComponent.cs")
const WORKER_UNIT_SCENE := preload("res://addons/beep_game_builder_cs/templates/scenes/grid_worker_unit.tscn")
const CONSTRUCTION_DUST := preload("res://addons/beep_game_builder_cs/templates/particles/construction_dust.tscn")

const STAGE_DIR := "res://addons/beep_game_builder_cs/templates/art/construction_stages/"

const WOOD_CELL := Vector2i(2, 4)
const BRICK_CELL := Vector2i(7, 4)
const STEEL_CELL := Vector2i(12, 4)
const WOOD_LARGE_CELL := Vector2i(18, 4)
const WORKER_A_START := Vector2i(0, 1)
const WORKER_B_START := Vector2i(7, 1)
const WORKER_C_START := Vector2i(13, 1)
const WORKER_D_START := Vector2i(20, 1)
# Turns of work, not seconds. With no game clock in this standalone demo the
# grid work clock self-ticks at one turn per second, so six turns still reads
# as the same six-second build it always did.
const BUILD_TURNS := 6
const DELIVERY_INTERVAL := 0.35

var jobs: Node
var site: Node
var grid: Node
var catalog: Node
var _deliveries: Array = []
var _crews: Dictionary = {}

func _ready() -> void:
	var bg := ColorRect.new()
	bg.color = Color(0.16, 0.42, 0.2)
	bg.size = Vector2(4000, 4000)
	bg.position = Vector2(-2000, -2000)
	add_child(bg)

	grid = GRID_PROJECTION.new()
	grid.name = "Grid"
	grid.set("TileSize", Vector2(64, 64))
	add_child(grid)

	var nav: Node = GRID_NAVIGATION.new()
	nav.name = "Navigation"
	nav.set("BoundsSize", Vector2i(30, 12))
	add_child(nav)

	jobs = JOB_QUEUE.new()
	jobs.name = "Jobs"
	add_child(jobs)

	catalog = BUILD_CATALOG.new()
	catalog.name = "Catalog"
	catalog.set("Builds", [
		_build_def("wood_workshop", "Wood Workshop", Vector2i(2, 2), "wood", [["wood_planks", 4]]),
		_build_def("brick_shed", "Brick Shed", Vector2i(2, 2), "brick", [["bricks", 4], ["wood_planks", 2]]),
		_build_def("steel_shed", "Steel Shed", Vector2i(3, 2), "steel", [["steel_beams", 5]]),
		# The same wood scene as wood_workshop, on a footprint twice the
		# size - the scene is told the site and tiles its swatches to fit.
		_build_def("wood_workshop_large", "Wood Workshop (Large)", Vector2i(4, 4), "wood", [["wood_planks", 8], ["gravel", 3]]),
	])
	add_child(catalog)

	site = BUILD_SITE.new()
	site.name = "Site"
	site.set("BuildCatalogPath", NodePath("../Catalog"))
	site.set("JobQueuePath", NodePath("../Jobs"))
	add_child(site)

	var bar: Node = PROGRESS_BAR.new()
	bar.name = "ProgressBar"
	bar.set("BuildSitePath", NodePath("../Site"))
	bar.set("JobQueuePath", NodePath("../Jobs"))
	bar.set("RefreshIntervalSeconds", 0.1)
	bar.set("BarOffset", Vector2(0, -150))
	add_child(bar)

	var stage_visual: Node = STAGE_VISUAL.new()
	stage_visual.name = "StageVisual"
	stage_visual.set("BuildSitePath", NodePath("../Site"))
	stage_visual.set("BuildCatalogPath", NodePath("../Catalog"))
	stage_visual.set("JobQueuePath", NodePath("../Jobs"))
	stage_visual.set("GridPath", NodePath("../Grid"))
	stage_visual.set("RefreshIntervalSeconds", 0.1)
	add_child(stage_visual)

	# Each site has its own crew, dispatched to it explicitly when its job
	# appears (GridWorkerComponent.AssignJob - what a dispatch board does).
	# Left to the pull model, whichever worker ticks first claims whichever
	# job appears first, and two trucks sent across each other's row meet
	# head-on and block each other for good.
	_crews["wood_workshop"] = _spawn_worker("WorkerA", "carpenter_a", WORKER_A_START)
	_crews["brick_shed"] = _spawn_worker("WorkerB", "mason_b", WORKER_B_START)
	_crews["steel_shed"] = _spawn_worker("WorkerC", "welder_c", WORKER_C_START)
	_crews["wood_workshop_large"] = _spawn_worker("WorkerD", "carpenter_d", WORKER_D_START)

	var camera := Camera2D.new()
	var mid_cell := Vector2i((WOOD_CELL.x + WOOD_LARGE_CELL.x + 3) / 2, WOOD_CELL.y)
	camera.position = grid.call("CellToWorld", mid_cell) + Vector2(0, -20)
	camera.zoom = Vector2(0.6, 0.6)
	add_child(camera)
	camera.make_current()

	await get_tree().process_frame

	site.connect("BuildSiteCreated", func(build_id: String, job_id: String, _placed: Node2D, _x: int, _y: int) -> void:
		var unit: Node = _crews.get(build_id)
		if unit == null:
			return
		var worker: Node = unit.get_node("GridWorker")
		if not worker.call("AssignJob", job_id):
			push_error("[construction-demo] %s could not be assigned job %s." % [worker.get("WorkerId"), job_id])
	)

	# In a full project GridPlacementComponent instantiates
	# GridBuildDefinition.Scene as the permanent finished building; this demo
	# registers bare nodes directly, so on completion it leaves the family's
	# site scene behind told "complete" at full progress - which is the
	# finished building with its site torn down.
	site.connect("BuildSiteCompleted", func(build_id: String, _job_id: String, completed_placed: Node2D, _x: int, _y: int) -> void:
		var def: Resource = catalog.call("FindBuild", build_id)
		if def == null:
			push_error("[construction-demo] no build definition for '%s'." % build_id)
			return
		var stages: Array = def.get("ConstructionStages")
		var finished: Node2D = (stages[0] as PackedScene).instantiate()
		finished.set("SiteFootprint", def.get("Footprint"))
		finished.set("CellSize", grid.get("EffectiveTileSize"))
		finished.set("BuildProgress", 1.0)
		finished.set("SiteState", "complete")
		completed_placed.add_child(finished)
	)

	_place("Workshop", "wood_workshop", WOOD_CELL)
	_place("Shed", "brick_shed", BRICK_CELL)
	_place("SteelShed", "steel_shed", STEEL_CELL)
	_place("WorkshopLarge", "wood_workshop_large", WOOD_LARGE_CELL)

	# Deliveries: one unit per site per DELIVERY_INTERVAL until each site
	# has what it needs, so the piles fill up and the job appears.
	var timer := Timer.new()
	timer.name = "Deliveries"
	timer.wait_time = DELIVERY_INTERVAL
	timer.timeout.connect(_deliver_one_round)
	add_child(timer)
	timer.start()


func _build_def(build_id: String, display_name: String, footprint: Vector2i, material_kind: String, materials: Array) -> Resource:
	var def: Resource = BUILD_DEFINITION.new()
	def.set("BuildId", build_id)
	def.set("DisplayName", display_name)
	def.set("BuildTurns", BUILD_TURNS)
	def.set("JobKind", "build")
	def.set("Footprint", footprint)
	var required: Array = []
	for m in materials:
		required.append({"ResourceId": m[0], "Amount": m[1]})
	def.set("RequiredMaterials", required)
	var stages: Array[PackedScene] = [load(STAGE_DIR + "%s_construction_site.tscn" % material_kind)]
	def.set("ConstructionStages", stages)
	return def


func _place(node_name: String, build_id: String, cell: Vector2i) -> void:
	var placed := Node2D.new()
	placed.name = node_name
	add_child(placed)
	placed.position = grid.call("CellToWorld", cell)
	# The finished building sorts by its front edge in the same absolute
	# domain the units use (their own y).
	var def: Resource = catalog.call("FindBuild", build_id)
	var footprint: Vector2i = def.get("Footprint")
	placed.z_as_relative = false
	placed.z_index = int(placed.position.y + (footprint.y - 0.5) * 64.0)
	var storage: Node = STORAGE.new()
	storage.name = "Storage"
	storage.set("Capacity", 100)
	placed.add_child(storage)
	await get_tree().process_frame
	if not site.call("RegisterPlacedBuild", build_id, placed, cell):
		push_error("[construction-demo] %s RegisterPlacedBuild was rejected." % build_id)
		return
	var required: Array = def.get("RequiredMaterials")
	_deliveries.append({"storage": storage, "required": required})


func _deliver_one_round() -> void:
	var remaining: Array = []
	for delivery in _deliveries:
		var storage: Node = delivery["storage"]
		if not is_instance_valid(storage):
			continue
		var delivered_any := false
		for entry in delivery["required"]:
			var id: String = entry["ResourceId"]
			var amount: int = entry["Amount"]
			if int(storage.call("Stored", id)) < amount:
				storage.call("Load", id, 1)
				delivered_any = true
				break
		if delivered_any:
			remaining.append(delivery)
	_deliveries = remaining


func _spawn_worker(node_name: String, worker_id: String, start_cell: Vector2i) -> Node:
	var unit: Node2D = WORKER_UNIT_SCENE.instantiate()
	unit.name = node_name
	add_child(unit)
	unit.position = grid.call("CellToWorld", start_cell)

	var follower: Node = unit.get_node("PathFollower")
	# get_path_to is called ON THE NODE WHOSE NodePath EXPORT THIS BECOMES -
	# GridPathFollowerComponent/GridWorkerComponent resolve GridPath/JobQueuePath
	# relative to themselves, not relative to `unit`, so the path must be
	# computed from `follower`/`worker`'s own tree position, not the root.
	follower.set("GridPath", follower.get_path_to(grid))
	follower.set("NavigationPath", follower.get_path_to(get_node("Navigation")))

	var worker: Node = unit.get_node("GridWorker")
	worker.set("WorkerId", worker_id)
	worker.set("JobQueuePath", worker.get_path_to(jobs))
	worker.set("GridPath", worker.get_path_to(grid))
	# Dispatched per site (see BuildSiteCreated above), not self-claiming.
	worker.set("AutoClaimJobs", false)

	var particle: Node = PARTICLE_COMPONENT.new()
	particle.name = "Particle"
	# The shipped default burst and dust_puff.tscn both use a 512px dirt
	# texture at scales meant for a bigger world; at 64px cells either one
	# swallows the building. construction_dust.tscn is the same puff sized
	# for a grid builder.
	particle.set("ParticleScene", CONSTRUCTION_DUST)
	# ParticleComponent is a plain Node, so its emitter has no canvas parent
	# and sits at world (0,0) unless it follows the worker; and its default
	# AutoQueueFree frees the emitter after the first burst, which would
	# leave a repeating activity effect firing exactly once.
	particle.set("FollowParent", true)
	particle.set("AutoQueueFree", false)
	unit.add_child(particle)

	var activity: Node = ACTIVITY_EFFECT.new()
	activity.name = "Activity"
	activity.set("BurstIntervalSeconds", 0.6)
	unit.add_child(activity)
	# get_path_to needs `activity` to already be inside the tree, so this is
	# set only after add_child, unlike follower/worker above (already in the
	# tree as part of the instanced WORKER_UNIT_SCENE).
	activity.set("JobQueuePath", activity.get_path_to(jobs))

	return unit
