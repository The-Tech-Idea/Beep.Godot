extends SceneTree

const BASE := "res://addons/beep_game_builder_cs/ecs/"
const QUERIES := ["GeneratedTerrainAt", "ResourceAt", "FeatureAt", "ReliefAt", "ContinentAt",
	"IsStartPositionAt", "LiquidResourceAt", "UndergroundResourceAt", "UndergroundRichnessAt",
	"UndergroundDepthAt", "IsWaterAt", "PassableAt", "StartAreaAt"]

func make(kind: String, parent: Node, label: String, properties: Dictionary) -> Node:
	var node: Node = load(BASE + kind + ".cs").new()
	node.name = label
	for key in properties: node.set(key, properties[key])
	parent.add_child(node)
	return node

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	var host := Node2D.new()
	root.add_child(host)
	var generator := make("terrain/TerrainGeneratorComponent", host, "Generator", {"GenerateOnReady": false})
	var layers := make("terrain/TerrainDataLayersComponent", host, "Layers", {
		"RefreshOnReady": false, "MaterializeTileLayers": true})
	var world := make("terrain/TerrainWorldComponent", host, "World", {
		"GeneratorPath": NodePath("../Generator"), "DataLayersPath": NodePath("../Layers"),
		"BuildOnReady": false, "ParticipatesInSave": false, "MapSize": 0,
		"Resources": 1, "ResourceLevel": 2, "Seed": 424242, "StartAreaRadius": 6})
	world.call("NewWorld")
	assert(layers.get_child_count() == 9)
	for layer in layers.get_children():
		assert(layer is TileMapLayer)
		assert(not layer.collision_enabled and not layer.navigation_enabled, "Recipe layer activated a physical world")
		assert(layer.tile_set.get_physics_layers_count() == 0, "Recipe created collision geometry")
		assert(layer.tile_set.get_navigation_layers_count() == 0, "Recipe created navigation geometry")
	assert(layers.get_node(layers.get("TerrainGeneratorPath")) == generator, "World did not bind its recipe source")
	var baseline: Dictionary = {}
	var deposit := Vector2i(-1, -1)
	var reserved := 0
	for y in range(32):
		for x in range(32):
			var at := Vector2i(x, y)
			var values: Array = []
			for query in QUERIES: values.append(layers.call(query, at))
			baseline[at] = values
			if layers.call("UndergroundResourceAt", at) != "": deposit = at
			if int(layers.call("StartAreaAt", at)) > 0: reserved += 1
	assert(deposit.x >= 0, "Fixture has no underground deposit")
	assert(reserved > 0, "Fixture has no start area, so the shifted StartAreaAt comparison proves nothing")
	var origin := Vector2i(-100, 200)
	layers.set("BoundsOrigin", origin)
	layers.call("Rebuild")
	assert(layers.get_node("TerrainData").get_used_rect() == Rect2i(origin, Vector2i(32, 32)))
	for at in baseline:
		for i in range(QUERIES.size()):
			assert(layers.call(QUERIES[i], at + origin) == baseline[at][i], "Shifted data differs: " + QUERIES[i])
	assert(layers.call("GeneratedTerrainAt", Vector2i.ZERO) == "", "Old zero-origin cells remained")
	for start in layers.call("StartCells"):
		assert(Rect2i(origin, Vector2i(32, 32)).has_point(start))
		assert(layers.call("IsStartPositionAt", start))
	var store := make("grid/GridSubsurfaceStoreComponent", host, "Store", {
		"DataLayersPath": NodePath("../Layers"), "ParticipatesInSave": false, "DefaultCellAmount": 100})
	var survey := make("grid/GridProspectingComponent", host, "Survey", {
		"DataLayersPath": NodePath("../Layers"), "ParticipatesInSave": false,
		"AutoConnect": false, "SurveyRadius": 0})
	var shifted := deposit + origin
	assert(store.call("ResourceIdAt", shifted) == layers.call("UndergroundResourceAt", shifted))
	var before: int = store.call("RemainingAt", shifted)
	assert(before > 1)
	assert(store.call("Draw", shifted, 1) == 1 and store.call("RemainingAt", shifted) == before - 1)
	var saved_stock: Dictionary = store.call("CaptureState")
	var original_identity: String = layers.get("UndergroundIdentity")
	assert(not original_identity.is_empty() and saved_stock.underground_identity == original_identity)
	store.set("DataLayersPath", NodePath("../Missing"))
	assert(store.call("RemainingAt", shifted) == 0, "Missing source returned cached stock")
	store.set("DataLayersPath", NodePath("../Layers"))
	assert(store.call("RemainingAt", shifted) == before - 1, "Temporary disconnection erased depletion")
	layers.call("Rebuild")
	assert(layers.get("UndergroundIdentity") == original_identity)
	assert(store.call("RemainingAt", shifted) == before - 1, "Same recipe reset depletion")
	survey.call("Survey", shifted)
	assert(not survey.call("CaptureState").cells.is_empty(), "Survey missed shifted deposit")
	# Rebuild must resolve the current path and clear stale data when it is unavailable.
	layers.set("TerrainGeneratorPath", NodePath("../Missing"))
	layers.call("Rebuild")
	assert(layers.call("UndergroundResourceAt", shifted) == "" and layers.call("StartCells").is_empty())
	store.call("RestoreState", saved_stock)
	assert(store.call("RemainingAt", shifted) == 0, "Pending restore leaked stock before recipe rebuild")
	generator.set("BoundsOrigin", origin)
	world.call("Redraw")
	assert(layers.get("BoundsOrigin") == origin and layers.call("GeneratedTerrainAt", origin) != "")
	assert(store.call("RemainingAt", shifted) == before - 1, "Restore-before-world lost matching depletion")
	generator.set("Seed", 777777)
	layers.call("Rebuild")
	assert(layers.get("UndergroundIdentity") != original_identity, "Changed deposit map retained identity")
	var fresh := make("grid/GridSubsurfaceStoreComponent", host, "Fresh", {
		"DataLayersPath": NodePath("../Layers"), "ParticipatesInSave": false, "DefaultCellAmount": 100})
	store.call("RestoreState", saved_stock)
	assert(store.call("RemainingAt", shifted) == fresh.call("RemainingAt", shifted), "Old save contaminated replacement deposit map")
	assert(store.call("CaptureState").cells.is_empty(), "Mismatched depletion was retained")
	generator.set("Seed", 424242)
	layers.call("Rebuild")
	assert(store.call("RemainingAt", shifted) == before - 1, "Interim reads discarded a pending restore for the matching world")
	var cells := make("grid/GridCellDataComponent", host, "LiveCells", {})
	var nav := make("grid/GridNavigationComponent", host, "Navigation", {
		"CellDataPath": NodePath("../LiveCells"), "BoundsOrigin": origin,
		"BoundsSize": Vector2i(32, 32), "TreatPlacementOccupiedAsBlocked": false})
	var recipe_kind: String = layers.call("GeneratedTerrainAt", origin)
	cells.call("SetTerrainKind", origin, "water")
	assert(nav.call("IsBlocked", origin), "Flooding did not affect live navigation")
	assert(layers.call("GeneratedTerrainAt", origin) == recipe_kind, "Live edit rewrote recipe metadata")
	cells.call("SetTerrainKind", origin, "grass")
	assert(not nav.call("IsBlocked", origin), "Recipe prevented live land from becoming traversable")
	for layer in layers.get_children():
		assert(not layer.collision_enabled and not layer.navigation_enabled)
		assert(layer.tile_set.get_physics_layers_count() == 0 and layer.tile_set.get_navigation_layers_count() == 0)
	host.free()
	print("[terrain-data-origin] OK")
	quit()
