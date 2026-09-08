extends SceneTree

const BASE := "res://addons/beep_game_builder_cs/ecs/"
var failures: Array[String] = []

func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		push_error(message)

func component(path: String, parent: Node, node_name: String) -> Node:
	var node: Node = load(BASE + path + ".cs").new()
	node.name = node_name
	parent.add_child(node)
	return node

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	var host := Node2D.new()
	root.add_child(host)
	var cells := component("grid/GridCellDataComponent", host, "Cells")
	var replacement := component("grid/GridCellDataComponent", host, "Replacement")
	var generator := component("terrain/TerrainGeneratorComponent", host, "Generator")
	var grid := component("grid/GridProjectionComponent", host, "Grid")
	grid.set("DrawGrid", false)
	grid.set("TrackMouseCell", false)
	var navigation := component("grid/GridNavigationComponent", host, "Navigation")
	var icons: Node = load(BASE + "terrain/TerrainResourceRendererComponent.cs").new()
	icons.name = "ResourceIcons"
	icons.set("RefreshOnReady", false)
	icons.set("TerrainGeneratorPath", NodePath("../Generator"))
	host.add_child(icons)
	generator.set("CellDataPath", NodePath("../Cells"))
	var views := Node2D.new()
	views.name = "Views"
	host.add_child(views)
	var renderers: Array[Node] = []
	for type_name in ["TerrainPaintedRendererComponent", "TerrainTileRendererComponent", "TerrainIsometricRendererComponent", "TerrainIsometricAutotileRendererComponent", "TerrainFeatureRendererComponent", "TerrainReliefRendererComponent"]:
		var view: Node = load(BASE + "terrain/" + type_name + ".cs").new()
		view.name = type_name
		view.set("RefreshOnReady", false)
		views.add_child(view)
		renderers.append(view)
	renderers[1].set("GrassAtlasPath", "res://addons/beep_game_builder_cs/textures/tiles/grass_15piece.png")
	renderers[1].set("WaterAtlasPath", "res://addons/beep_game_builder_cs/textures/tiles/water_15piece.png")
	renderers[4].set("WoodsSheetPath", "res://addons/beep_game_builder_cs/textures/plants/forest_trees.png")
	var world: Node = load(BASE + "terrain/TerrainWorldComponent.cs").new()
	world.set("BuildOnReady", false)
	world.set("ParticipatesInSave", false)
	world.set("MapSize", 0)
	world.set("GeneratorPath", NodePath("../Generator"))
	world.set("GridPath", NodePath("../Grid"))
	world.set("NavigationPath", NodePath("../Navigation"))
	world.set("ResourceRendererPath", NodePath("../ResourceIcons"))
	world.set("PaintedRendererPath", NodePath("../Views/TerrainPaintedRendererComponent"))
	world.set("TileRendererPath", NodePath("../Views/TerrainTileRendererComponent"))
	world.set("IsometricRendererPath", NodePath("../Views/TerrainIsometricRendererComponent"))
	world.set("IsometricAutotileRendererPath", NodePath("../Views/TerrainIsometricAutotileRendererComponent"))
	world.set("FeaturesPath", NodePath("../Views/TerrainFeatureRendererComponent"))
	world.set("ReliefRendererPath", NodePath("../Views/TerrainReliefRendererComponent"))
	host.add_child(world)
	world.call("NewWorld")
	check(icons.get_node(icons.get("GridPath")) == grid, "World did not bind resource icons to gameplay grid")
	check(renderers[5].get_node(renderers[5].get("GridPath")) == grid, "Relief uses a separate grid")
	var building := Node2D.new()
	host.add_child(building)
	var identity: Node = load(BASE + "grid/GridObjectComponent.cs").new()
	identity.set("Cell", Vector2i(5, 5))
	identity.set("GridPath", NodePath("../../Grid"))
	building.add_child(identity)
	check(building.global_position.distance_to(grid.call("CellToWorld", Vector2i(5, 5))) < 0.001, "Stationary object did not bind to its cell")
	check(navigation.get("BoundsSize") == Vector2i(32, 32), "World did not propagate navigation bounds")
	check(navigation.get_node(navigation.get("CellDataPath")) == cells, "Navigation uses a different cell store")
	check(grid.get_node(grid.get("TileMapLayerPath")) == renderers[0].call("GetTerrainLayer"), "Painted view did not bind native grid")
	for view in renderers:
		check(view.get_node(view.get("CellDataPath")) == cells, "World did not bind " + str(view.name))
	var edit := Vector2i(12, 12)
	cells.call("SetTerrainKind", edit, "water")
	check(navigation.call("IsBlocked", edit), "World live water is not blocked in navigation")
	world.set("Seed", 123456)
	world.call("Redraw")
	check(cells.call("GetTerrainKind", edit) == "water", "Redraw regenerated live cells")
	var material: ShaderMaterial = renderers[0].get_node("SplatSurface").material
	check(roundi(material.get_shader_parameter("id_map").get_image().get_pixelv(edit).r * 255) == 12, "World painted recipe instead of live water")
	world.set("Projection", 1)
	renderers[1].position = Vector2(111, -23)
	world.call("Redraw")
	check(building.global_position.distance_to(grid.call("CellToWorld", Vector2i(5, 5))) < 0.001, "World switch left stationary object in previous projection")
	var logical: TileMapLayer = renderers[1].get_node("LogicalGrid")
	check(grid.get_node(grid.get("TileMapLayerPath")) == logical, "Dual-grid view bound display corners instead of logical cells")
	var expected := logical.to_global(logical.map_to_local(edit))
	check(grid.call("CellToWorld", edit).distance_to(expected) < 0.001, "Dual-grid placement is shifted")
	check(grid.call("WorldToCell", expected) == edit, "Dual-grid picking disagrees with placement")
	check(navigation.call("IsBlocked", edit), "Switching view changed water blocking")
	world.set("Projection", 0)
	world.call("Redraw")
	check(cells.call("GetTerrainKind", edit) == "water", "Projection switch changed live cells")
	world.set("CellDataPath", NodePath("../Replacement"))
	world.call("NewWorld")
	check(replacement.get("CellCount") == 1024, "Generator kept writing to old cell store")
	check(cells.call("GetTerrainKind", edit) == "water", "Source replacement overwrote old map")
	check(navigation.get_node(navigation.get("CellDataPath")) == replacement, "Navigation retained previous cell store")
	for view in renderers:
		check(view.get_node(view.get("CellDataPath")) == replacement, "View retained old source: " + str(view.name))
	replacement.call("SetTerrainKind", edit, "water")
	world.call("RestoreWorld")
	check(replacement.call("GetTerrainKind", edit) == "water", "RestoreWorld overwrote live edits")
	world.set("GridPath", NodePath("../MissingGrid"))
	world.call("Redraw")
	check(navigation.get_node_or_null(navigation.get("GridPath")) != grid,
		"Missing explicit grid left navigation bound to previous view")
	check(navigation.call("FindWorldPath", Vector2.ZERO, Vector2(64, 0)).is_empty(),
		"Missing explicit world grid still produced a world path")
	world.set("GridPath", NodePath("../Grid"))
	world.call("Redraw")
	check(navigation.get_node(navigation.get("GridPath")) == grid, "Navigation did not recover its grid")
	var origin := Vector2i(-9, 14)
	generator.set("BoundsOrigin", origin)
	world.call("NewWorld")
	check(navigation.get("BoundsOrigin") == origin, "Navigation lost logical origin")
	check(renderers[0].get("BoundsOrigin") == origin, "Painted view lost logical origin")
	check(renderers[4].get("BoundsOrigin") == origin, "Features lost logical origin")
	check(renderers[4].get_node(renderers[4].get("GridPath")) == grid, "Features use a separate gameplay grid")
	var starts = generator.call("GetStartPositions")
	var start_cell: Vector2i = (starts[0] if not starts.is_empty() else Vector2i(16, 16)) + origin
	check(world.call("StartPositionGlobal").distance_to(grid.call("CellToWorld", start_cell)) < 0.001,
		"Camera start does not match shifted gameplay cell")
	var extent: Rect2 = world.call("WorldExtent")
	check(extent.position == Vector2(origin * 64) and extent.size == Vector2(2048, 2048),
		"Camera extent ignored shifted painted bounds")
	world.set("Projection", 1)
	world.call("Redraw")
	check(renderers[1].get("BoundsOrigin") == origin, "Tiled view lost logical origin")
	check(world.call("StartPositionGlobal").distance_to(grid.call("CellToWorld", start_cell)) < 0.001,
		"Camera start does not match shifted tiled cell")
	await process_frame
	await process_frame
	host.free()
	print("[terrain-world-live] OK" if failures.is_empty() else "[terrain-world-live] FAILED")
	quit(0 if failures.is_empty() else 1)
