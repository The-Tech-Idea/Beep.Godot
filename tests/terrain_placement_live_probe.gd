extends SceneTree

const BASE := "res://addons/beep_game_builder_cs/ecs/grid/"

func make(kind: String, parent: Node, label: String, values: Dictionary = {}) -> Node:
	var node: Node = load(BASE + kind + "Component.cs").new()
	node.name = label
	for key in values: node.set(key, values[key])
	parent.add_child(node)
	return node

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	var host := Node2D.new()
	root.add_child(host)
	var grid := make("GridProjection", host, "Grid", {"TileSize": Vector2(64, 32)})
	var cells := make("GridCellData", host, "Cells")
	make("GridNavigation", host, "Navigation", {"BoundsOrigin": Vector2i(-2, -2), "BoundsSize": Vector2i(4, 4)})
	var objects := Node2D.new()
	objects.name = "Objects"
	host.add_child(objects)
	var placement := make("GridPlacement", host, "Placement", {"GridPath": NodePath("../Grid"),
		"CellDataPath": NodePath("../Cells"), "NavigationPath": NodePath("../Navigation"),
		"PlacementRootPath": NodePath("../Objects"), "UseMouseInput": false, "SetZIndexFromY": false,
		"Footprint": Vector2i(2, 1), "KeepPlacingAfterConfirm": false})
	var source := Node2D.new()
	var packed := PackedScene.new()
	assert(packed.pack(source) == OK)
	source.free()
	placement.set("PlacementScene", packed)
	var anchor := Vector2i(-1, 0)
	for projection in [0, 1]:
		grid.set("Projection", projection)
		placement.call("BeginPlacement", "hut")
		assert(placement.call("MovePreviewToCell", anchor))
		await process_frame
		assert(placement.get("CurrentCell") == anchor, "Programmatic preview was overwritten by mouse polling")
		cells.call("SetTerrainKind", Vector2i.ZERO, "water")
		assert(placement.call("ConfirmPlacement") == null, "Flooded footprint trusted stale preview validity")
		assert(objects.get_child_count() == 0 and not placement.call("IsOccupied", anchor))
		cells.call("SetTerrainKind", Vector2i.ZERO, "grass")
		cells.call("SetMetadata", Vector2i.ZERO, "terrain_relief", 1)
		assert(not placement.call("CanPlace", anchor), "Building spans incompatible relief levels")
		placement.set("RequireLevelFootprint", false)
		assert(placement.call("CanPlace", anchor), "Explicit slope-spanning policy was ignored")
		placement.set("RequireLevelFootprint", true)
		cells.call("SetMetadata", Vector2i.ZERO, "terrain_relief", 0)
		assert(not placement.call("CanPlace", Vector2i(1, 0)), "Footprint extends outside navigation bounds")
		assert(not placement.call("CanPlace", Vector2i(-2147483648, 0)))
		placement.set("CellDataPath", NodePath("../MissingCells"))
		assert(not placement.call("CanPlace", anchor), "Missing explicit live cells allowed placement")
		placement.set("CellDataPath", NodePath("../Cells"))
		placement.set("PlacementRootPath", NodePath("../MissingObjects"))
		assert(not placement.call("CanPlace", anchor), "Broken explicit root fell back to parent")
		placement.set("PlacementRootPath", NodePath("../Objects"))
		# No preview refresh here: confirmation must revalidate in both directions.
		var placed: Node2D = placement.call("ConfirmPlacement")
		assert(placed != null and placed.get_parent() == objects)
		assert(placed.global_position.is_equal_approx(grid.call("CellToWorld", anchor)))
		assert(placement.call("IsOccupied", anchor))
		placed.free()
		assert(not placement.call("IsOccupied", anchor), "Demolition leaked footprint occupancy")
	host.free()
	print("[terrain-placement-live] OK")
	quit()
