extends SceneTree
func _initialize() -> void:
	var root_node = load("res://tests/examples/terrain_generator_lab.tscn").instantiate()
	get_root().add_child(root_node)
	for i in range(45): await process_frame
	# The View dropdown: 0 painted, 1 game tiles, 2 isometric.
	for n in root_node.find_children("*", "OptionButton", true, false):
		if n.item_count == 3 and n.get_item_text(1).to_lower().contains("tile"):
			n.selected = 1
			n.item_selected.emit(1)
			break
	for i in range(45): await process_frame
	var img := get_root().get_texture().get_image()
	img.save_png(OS.get_environment("SHOT") + "/lab_tiles.png")
	print("saved lab_tiles.png")
	quit(0)
