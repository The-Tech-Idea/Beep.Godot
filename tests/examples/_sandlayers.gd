extends SceneTree
func _initialize() -> void:
	var root_node = load("res://tests/examples/terrain_tilemap_demo.tscn").instantiate()
	get_root().add_child(root_node)
	for i in range(45): await process_frame
	print("--- tile view layers ---")
	for n in root_node.find_children("*", "TileMapLayer", true, false):
		var used: int = n.get_used_cells().size()
		print("  %-16s z=%d cells=%d" % [n.name, n.z_index, used])
	quit(0)
