extends SceneTree

const BASE := "res://addons/beep_game_builder_cs/ecs/terrain/"
const QUERIES := ["GeneratedTerrainAt", "ResourceAt", "FeatureAt", "ReliefAt", "ContinentAt",
	"IsStartPositionAt", "LiquidResourceAt", "UndergroundResourceAt", "UndergroundRichnessAt",
	"UndergroundDepthAt", "IsWaterAt", "PassableAt", "StartAreaAt", "StartDistanceAt"]
var failures: Array[String] = []

func _initialize() -> void: run.call_deferred()

func check(value: bool, message: String) -> void:
	if not value:
		failures.append(message)
		push_error(message)

func run() -> void:
	var host := Node.new()
	root.add_child(host)
	var generator: Node = load(BASE + "TerrainGeneratorComponent.cs").new()
	generator.name = "Generator"
	generator.BoundsSize = Vector2i(32, 24)
	generator.TopologySamplesPerCell = 4
	generator.ResourceSet = 1
	generator.ResourceDensity = 4.0
	generator.StartAreaRadius = 6
	generator.StartDistanceScaling = 1.0
	host.add_child(generator)
	var compact: Node = load(BASE + "TerrainDataLayersComponent.cs").new()
	compact.name = "Compact"
	compact.RefreshOnReady = false
	compact.TerrainGeneratorPath = NodePath("../Generator")
	compact.BoundsSize = Vector2i(32, 24)
	compact.BoundsOrigin = Vector2i(-50, 70)
	host.add_child(compact)
	var native: Node = load(BASE + "TerrainDataLayersComponent.cs").new()
	native.name = "Native"
	native.RefreshOnReady = false
	native.TerrainGeneratorPath = NodePath("../Generator")
	native.BoundsSize = compact.BoundsSize
	native.BoundsOrigin = compact.BoundsOrigin
	native.MaterializeTileLayers = true
	host.add_child(native)
	compact.Rebuild()
	native.Rebuild()
	check(compact.get_child_count() == 0, "Default runtime data allocated tile layers")
	check(native.get_child_count() == 9, "Explicit native metadata view is missing")
	check(compact.UndergroundIdentity == native.UndergroundIdentity, "Storage choice changed subsurface identity")
	var deposits := 0
	var reserved := 0
	var measured := 0
	for y in range(-1, 25):
		for x in range(-1, 33):
			var cell := Vector2i(x, y) + Vector2i(-50, 70)
			for query in QUERIES:
				check(compact.call(query, cell) == native.call(query, cell), "Storage query differs: %s at %s" % [query, cell])
			if compact.UndergroundResourceAt(cell) != "": deposits += 1
			if compact.StartAreaAt(cell) > 0: reserved += 1
			var inside := x >= 0 and y >= 0 and x < 32 and y < 24
			if compact.StartDistanceAt(cell) >= 0: measured += 1
			check(inside or compact.StartDistanceAt(cell) == -1, "Off-map cell %s reads a start distance" % cell)
	check(deposits > 0, "Comparison fixture has no subsurface data")
	check(reserved > 0, "Comparison fixture has no start area")
	check(measured == 32 * 24, "Comparison fixture measured start distance on %d of %d cells" % [measured, 32 * 24])
	# Start order, not just the start set, is the same in both storage modes.
	check(compact.StartCells() == native.StartCells(), "Start order differs between storage modes")
	var deposit := Vector2i.ZERO
	for y in range(24):
		for x in range(32):
			var cell: Vector2i = Vector2i(x, y) + compact.BoundsOrigin
			if compact.UndergroundResourceAt(cell) != "": deposit = cell
	var store: Node = load("res://addons/beep_game_builder_cs/ecs/grid/GridSubsurfaceStoreComponent.cs").new()
	store.DataLayersPath = NodePath("../Compact")
	host.add_child(store)
	var initial: int = store.RemainingAt(deposit)
	var drawn: int = store.Draw(deposit, 1)
	check(initial > 0 and drawn == 1, "Depletion fixture did not extract a deposit")
	var depleted: Dictionary = store.CaptureState()
	compact.Rebuild()
	check(store.RemainingAt(deposit) == initial - 1, "Metadata rebuild reset depletion")
	store.RestoreState(depleted)
	check(store.RemainingAt(deposit) == initial - 1, "New identity failed depletion save/restore")
	var expected_starts: Array = Array(native.StartCells())
	for start in compact.StartCells(): check(start in expected_starts, "Start cells differ")
	check(compact.StartCells().size() == expected_starts.size(), "Start count differs")
	var identity: String = compact.UndergroundIdentity
	var old_value: String = compact.GeneratedTerrainAt(Vector2i(-40, 80))
	compact.BoundsOrigin = Vector2i.ZERO
	check(compact.GeneratedTerrainAt(Vector2i(-40, 80)) == old_value, "Unpublished bounds changed stored data")
	compact.BoundsOrigin = native.BoundsOrigin
	compact.MaterializeTileLayers = true
	compact.Rebuild()
	check(compact.get_child_count() == 9, "Could not materialize native data")
	compact.MaterializeTileLayers = false
	compact.Rebuild()
	check(compact.get_child_count() == 0 and compact.UndergroundIdentity == identity, "Retiring native data changed identity or retained nodes")
	compact.TerrainGeneratorPath = NodePath("../Missing")
	compact.Rebuild()
	check(compact.UndergroundIdentity == "" and compact.StartCells().is_empty(), "Missing generator retained stale metadata")
	for query in ["GeneratedTerrainAt", "ResourceAt", "FeatureAt", "UndergroundResourceAt"]:
		check(compact.call(query, Vector2i(-40, 80)) == "", "Missing generator retained query data")
	host.free()
	await process_frame
	print("[terrain-data-storage] OK" if failures.is_empty() else "[terrain-data-storage] FAILED")
	quit(0 if failures.is_empty() else 1)
