extends SceneTree
# Do the projections cover the SAME cells? Same world, same extent, so the set of
# cells each view draws should match the map.
const LAB := "res://addons/beep_game_builder_cs/templates/scenes/terrain/terrain_generator_lab.tscn"
const VIEWS := {0: "Painted", 1: "Tiles", 2: "Isometric"}

func layers_under(node: Node, out: Array) -> void:
	if node is TileMapLayer:
		out.append(node)
	for c in node.get_children():
		layers_under(c, out)

func _initialize() -> void: call_deferred("_run")

func _run() -> void:
	var root_node = load(LAB).instantiate()
	get_root().add_child(root_node)
	for i in range(10): await process_frame
	var world = root_node.get_node_or_null("World")
	var preview = root_node.get_node_or_null("Preview")

	for v in VIEWS:
		world.set("Projection", v)
		world.call("Build")
		for i in range(25): await process_frame

		var size: Vector2i = world.get("BuiltSize")
		var total := size.x * size.y
		print("--- %s  (map %dx%d = %d cells)" % [VIEWS[v], size.x, size.y, total])
		for child in preview.get_children():
			if not child.visible:
				continue
			var found: Array = []
			layers_under(child, found)
			var union := {}
			var tiles := 0
			for l in found:
				for c in l.get_used_cells():
					union[c] = true
					tiles += 1
			if found.size() > 0:
				print("    %-16s layers=%-3d tiles=%-6d distinct_cells=%-6d coverage=%.1f%%"
					% [child.name, found.size(), tiles, union.size(), 100.0 * union.size() / total])
	quit(0)
