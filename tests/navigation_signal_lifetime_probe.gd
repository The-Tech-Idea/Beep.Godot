extends SceneTree

var failures: Array[String] = []

func _initialize() -> void:
	call_deferred("run")

func check_connections(nav: Node, expected: int) -> void:
	for signal_name in ["node_added", "node_removed"]:
		var count := 0
		for connection in get_signal_connection_list(signal_name):
			var callback: Callable = connection.callable
			if callback.get_object() != nav:
				continue
			count += 1
			if callback.is_custom() or callback.get_method() != "OnReferenceNodeChanged":
				failures.append("%s retained a managed delegate" % signal_name)
		if count != expected:
			failures.append("%s: expected %d connections, got %d" % [signal_name, expected, count])

func run() -> void:
	var host := Node.new()
	root.add_child(host)
	current_scene = host
	var nav: Node = load("res://addons/beep_game_builder_cs/ecs/grid/GridNavigationComponent.cs").new()
	var target := Vector2i(1, 1)
	for cycle in range(3):
		host.add_child(nav)
		check_connections(nav, 1)
		# Prime a cached missing source, then add a collaborator. Its signal
		# must invalidate the miss and expose the newly blocked cell.
		if nav.call("IsBlocked", target):
			failures.append("Unexpected initial blocked cell")
		var cells: Node = load("res://addons/beep_game_builder_cs/ecs/grid/GridCellDataComponent.cs").new()
		cells.call("SetTerrainKind", target, "water")
		host.add_child(cells)
		if not nav.call("IsBlocked", target):
			failures.append("node_added failed to invalidate the cached missing source")
		host.remove_child(cells)
		if nav.call("IsBlocked", target):
			failures.append("Removed source remained active")
		cells.free()
		host.remove_child(nav)
		check_connections(nav, 0)
	nav.free()
	host.free()
	for failure in failures:
		push_error(failure)
	if failures.is_empty():
		print("[navigation-signal-lifetime] OK: native callbacks, source invalidation, and three enter/exit cycles.")
	quit(0 if failures.is_empty() else 1)
