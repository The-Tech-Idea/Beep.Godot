extends SceneTree

const NAV = preload("res://addons/beep_game_builder_cs/ecs/grid/GridNavigationComponent.cs")
const CELLS = preload("res://addons/beep_game_builder_cs/ecs/grid/GridCellDataComponent.cs")

func _initialize() -> void:
	create_timer(60).timeout.connect(func(): push_error("Edge parity timed out"); quit(1))
	run.call_deferred()

func blocked(mask: int, cell: Vector2i) -> bool:
	return (mask & (1 << (cell.y * 3 + cell.x))) != 0

func run() -> void:
	var host := Node.new()
	root.add_child(host)
	var cells = CELLS.new()
	cells.name = "Cells"
	host.add_child(cells)
	var nav = NAV.new()
	nav.CellDataPath = NodePath("../Cells")
	nav.BoundsSize = Vector2i(3, 3)
	nav.TerrainCostMultipliers = {}
	host.add_child(nav)
	var origin := Vector2i.ONE
	var checks := 0
	for mask in 512:
		for y in 3:
			for x in 3:
				nav.SetBlocked(Vector2i(x, y), blocked(mask, Vector2i(x, y)))
		for policy in 3:
			nav.Diagonals = policy
			for flags in 4:
				nav.AllowBlockedStart = (flags & 1) != 0
				nav.AllowBlockedGoal = (flags & 2) != 0
				for y in 3:
					for x in 3:
						var target := Vector2i(x, y)
						if target == origin: continue
						var diagonal := x != 1 and y != 1
						var expected: bool = (nav.AllowBlockedStart or not blocked(mask, origin)) and (nav.AllowBlockedGoal or not blocked(mask, target))
						if diagonal:
							expected = expected and policy != 0
							if policy == 2:
								expected = expected and not blocked(mask, Vector2i(x, 1)) and not blocked(mask, Vector2i(1, y))
						if nav.CanTraverse(origin, target) != expected:
							push_error("Edge mismatch mask=%s policy=%s flags=%s target=%s" % [mask, policy, flags, target])
							quit(1)
							return
						if mask % 73 == 0 and (nav.FindCellPath(origin, target).size() == 2) != expected:
							push_error("A* and edge legality disagree")
							quit(1)
							return
						checks += 1
		if mask % 32 == 0: await process_frame
	nav.ClearBlocked()
	var target := Vector2i(2, 1)
	cells.SetTerrainKind(target, "custom_obstacle")
	nav.AllowBlockedGoal = false
	nav.BlockedTerrainKinds = [" deep-water "]
	if not nav.CanTraverse(origin, target):
		push_error("Initial authored rules rejected unrelated terrain")
		quit(1)
		return
	nav.BlockedTerrainKinds[0] = " Custom-Obstacle "
	if nav.CanTraverse(origin, target):
		push_error("In-place authored rule edit did not invalidate normalized rules")
		quit(1)
		return
	nav.BlockedTerrainKinds.clear()
	if not nav.CanTraverse(origin, target):
		push_error("Cleared authored rules retained stale blocking")
		quit(1)
		return
	nav.BlockedTerrainKinds.append("CUSTOM OBSTACLE")
	if nav.CanTraverse(origin, target):
		push_error("Appended authored rule was ignored")
		quit(1)
		return
	nav.TreatBlockedTerrainKindsAsBlocked = false
	if not nav.CanTraverse(origin, target):
		push_error("Disabled kind blocking retained cached rules")
		quit(1)
		return
	nav.UseBounds = false
	if nav.CanTraverse(origin, origin) or nav.CanTraverse(origin, Vector2i(4, 4)) or nav.CanTraverse(Vector2i(2147483647, 0), Vector2i(-2147483648, 0)):
		push_error("Non-adjacent edge accepted")
		quit(1)
		return
	host.free()
	print("[terrain-navigation-edges] ", checks, " blocked/corner/endpoint combinations and sampled A* parity OK")
	quit(0)
