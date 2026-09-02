extends SceneTree

# One resource system, read by both sides.
#
# The map used to generate iron and the economy used to scatter "wood", because
# each kept its own idea of what a resource was: the generator had a private
# array of definitions, and the game had bare strings. Nothing connected them, so
# a world could hold crude_oil that no wallet had heard of.
#
# This drives the whole loop: generate a world, publish its resources as cell
# data, then let the scatter place deposits. What it asserts is that the deposits
# are the MAP's resources, and that what each one is worth came from the shared
# catalog rather than from a copy on the node.

const CELL_DATA := preload("res://addons/beep_game_builder_cs/ecs/terrain/GridCellDataComponent.cs")
const GENERATOR := preload("res://addons/beep_game_builder_cs/ecs/terrain/TerrainGeneratorComponent.cs")
const DATA_LAYERS := preload("res://addons/beep_game_builder_cs/ecs/terrain/TerrainDataLayersComponent.cs")
const PROJECTION := preload("res://addons/beep_game_builder_cs/ecs/terrain/GridProjectionComponent.cs")
const SCATTER := preload("res://addons/beep_game_builder_cs/ecs/terrain/GridResourceScatterComponent.cs")
const CATALOG := preload("res://addons/beep_game_builder_cs/ecs/terrain/ResourceCatalog.cs")
const DEFINITION := preload("res://addons/beep_game_builder_cs/ecs/terrain/ResourceDefinition.cs")

const SIZE := Vector2i(48, 32)

var failures: Array[String] = []

func check(ok: bool, message: String) -> void:
	if ok:
		print("  ok    %s" % message)
	else:
		print("  FAIL  %s" % message)
		failures.append(message)

func make_definition(id: String, terrain: Array[String], amount: int, seconds: float) -> Resource:
	var d: Resource = DEFINITION.new()
	d.set("Id", id)
	d.set("DisplayName", id.capitalize())
	d.set("TerrainKinds", terrain)
	d.set("Amount", amount)
	d.set("GatherSeconds", seconds)
	return d

# A one-node scene, packed in memory, distinguishable by its root's name.
func named_scene(root_name: String) -> PackedScene:
	var root := Node2D.new()
	root.name = root_name
	var packed := PackedScene.new()
	packed.pack(root)
	root.free()
	return packed

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var cells: Node = CELL_DATA.new()
	cells.name = "Cells"
	root.add_child(cells)

	var grid: Node = PROJECTION.new()
	grid.name = "Grid"
	root.add_child(grid)

	var generator: Node = GENERATOR.new()
	generator.name = "Generator"
	generator.set("CellDataPath", NodePath("../Cells"))
	generator.set("BoundsSize", SIZE)
	generator.set("Seed", 20260901)
	generator.set("ResourceDensity", 2.0)
	root.add_child(generator)
	await process_frame
	generator.call("GenerateTerrain")

	var layers: Node = DATA_LAYERS.new()
	layers.name = "DataLayers"
	layers.set("TerrainGeneratorPath", NodePath("../Generator"))
	layers.set("BoundsSize", SIZE)
	layers.set("RefreshOnReady", false)
	root.add_child(layers)
	await process_frame
	layers.call("Rebuild")

	# What the map actually put down, so the catalog below is built against real
	# ids rather than ones assumed to be there.
	var on_map := {}
	for y in SIZE.y:
		for x in SIZE.x:
			var id: String = str(layers.call("ResourceAt", Vector2i(x, y)))
			if id != "":
				on_map[id] = int(on_map.get(id, 0)) + 1
	check(on_map.size() >= 2, "the generated map holds resources (%s)" % str(on_map))
	if on_map.is_empty():
		_finish()
		return

	# A catalog covering SOME of what the map holds. The ones left out are the
	# point: a resource the game has no use for must not become a deposit.
	var known: Array = on_map.keys()
	known.sort()
	var listed: Array = known.slice(0, max(1, known.size() / 2))
	var omitted: Array = known.slice(max(1, known.size() / 2))

	var catalog: Resource = CATALOG.new()
	var definitions: Array[Resource] = []
	var expected_amount := {}
	var amount := 3
	for id in listed:
		var terrain: Array[String] = []
		definitions.append(make_definition(str(id), terrain, amount, 2.5))
		expected_amount[str(id)] = amount
		amount += 1
	# The catalog's own scene for the FIRST listed resource must win over the
	# scatter's one blanket ResourceScene - every other listed resource has no
	# scene of its own, so it falls back to the blanket instead.
	var catalog_scene_id := ""
	if definitions.size() > 0:
		catalog_scene_id = str(listed[0])
		definitions[0].set("NodeScene", named_scene("FromCatalog"))
	catalog.set("Resources", definitions)

	var scatter: Node = SCATTER.new()
	scatter.name = "Scatter"
	scatter.set("GridPath", NodePath("../Grid"))
	scatter.set("ResourceRootPath", NodePath("."))
	scatter.set("CellDataPath", NodePath("../Cells"))
	scatter.set("DataLayersPath", NodePath("../DataLayers"))
	scatter.set("Catalog", catalog)
	scatter.set("ResourceScene", named_scene("FromBlanket"))
	scatter.set("BoundsOrigin", Vector2i.ZERO)
	scatter.set("BoundsSize", SIZE)
	scatter.set("MaxNodes", 4096)
	scatter.set("GenerateOnReady", false)
	scatter.set("AvoidBlockedTerrainKinds", false)
	scatter.set("AvoidCellDataBlocked", false)
	root.add_child(scatter)
	await process_frame
	var placed: int = int(scatter.call("RebuildScatter"))
	check(placed > 0, "the scatter placed deposits from the map (%d)" % placed)

	var matched := 0
	var mismatched := 0
	var wrong_rules := 0
	var placed_ids := {}
	var catalog_scene_used := 0
	var catalog_scene_missed := 0
	var blanket_scene_used := 0
	var blanket_scene_missed := 0
	for child in scatter.get_children():
		var node := find_resource_node(child)
		if node == null:
			continue
		var cell: Vector2i = node.get("Cell")
		var id: String = str(node.get("ResourceId"))
		placed_ids[id] = true
		if str(layers.call("ResourceAt", cell)) == id:
			matched += 1
		else:
			mismatched += 1
		if int(node.get("Amount")) != int(expected_amount.get(id, -1)):
			wrong_rules += 1

		var root_name := String(child.name)
		if id == catalog_scene_id:
			if root_name.begins_with("FromCatalog_"): catalog_scene_used += 1
			else: catalog_scene_missed += 1
		elif expected_amount.has(id):
			if root_name.begins_with("FromBlanket_"): blanket_scene_used += 1
			else: blanket_scene_missed += 1

	check(mismatched == 0,
		"every deposit sits on the cell the map gave that resource (%d matched, %d wrong)"
			% [matched, mismatched])
	check(wrong_rules == 0,
		"every deposit took its amount from the catalog, not from the node (%d wrong)" % wrong_rules)
	check(catalog_scene_used > 0 and catalog_scene_missed == 0,
		"'%s' deposits use the catalog's own NodeScene, not the blanket one (%d used, %d missed)"
			% [catalog_scene_id, catalog_scene_used, catalog_scene_missed])
	check(blanket_scene_used > 0 and blanket_scene_missed == 0,
		"deposits with no catalog scene fall back to the blanket ResourceScene (%d used, %d missed)"
			% [blanket_scene_used, blanket_scene_missed])

	# The half left out of the catalog is drawn on the map but must not be
	# harvestable - that is what "the game decides what a resource is worth" means.
	var leaked: Array = []
	for id in omitted:
		if placed_ids.has(str(id)):
			leaked.append(str(id))
	check(leaked.is_empty(),
		"a resource the catalog does not list is never made a deposit%s"
			% ("" if leaked.is_empty() else ": " + ", ".join(leaked)))

	_finish()

func find_resource_node(node: Node) -> Node:
	if node.get("ResourceId") != null and node.get("Cell") != null:
		return node
	for child in node.get_children():
		var found := find_resource_node(child)
		if found != null:
			return found
	return null

func _finish() -> void:
	print("\nRESULT: ", "all checks passed" if failures.is_empty() else "%d FAILED" % failures.size())
	quit(1 if failures.size() > 0 else 0)
