extends SceneTree

var failures: Array[String] = []

func _initialize() -> void:
	call_deferred("run")

func check_connections(node: Node, expected: int, phase: String) -> void:
	for signal_name in ["child_entered_tree", "child_exiting_tree"]:
		var count = node.get_signal_connection_list(signal_name).size()
		if count != expected:
			failures.append("%s: %s has %d connections, expected %d" % [phase, signal_name, count, expected])

func run() -> void:
	for component in ["TerrainMapOverlayComponent", "TerrainResourceRendererComponent"]:
		var container = Node.new()
		root.add_child(container)
		var resources = Node.new()
		resources.name = "Resources"
		container.add_child(resources)
		var descendant = Node.new()
		resources.add_child(descendant)
		var view = load("res://addons/beep_game_builder_cs/ecs/terrain/%s.cs" % component).new()
		view.set("ResourceRootPath", NodePath("../Resources"))
		view.set("RefreshOnReady", false)
		view.visible = false
		container.add_child(view)
		check_connections(resources, 1, component + " initial root")
		check_connections(descendant, 1, component + " initial descendant")
		for cycle in range(3):
			view.call("OnBeforeSerialize")
			check_connections(resources, 0, component + " before reload")
			check_connections(descendant, 0, component + " before reload descendant")
			view.call("OnAfterDeserialize")
			await process_frame
			check_connections(resources, 1, component + " restored")
			check_connections(descendant, 1, component + " restored descendant")
		view.free()
		check_connections(resources, 0, component + " freed")
		check_connections(descendant, 0, component + " freed descendant")
		container.free()
	for failure in failures:
		push_error(failure)
	if failures.is_empty():
		print("[terrain-resource-reload] OK: both views release and restore subtree subscriptions across three serialization cycles.")
	quit(0 if failures.is_empty() else 1)
