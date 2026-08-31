extends SceneTree
func _initialize() -> void:
	var root_node = load("res://tests/examples/terrain_generator_lab.tscn").instantiate()
	get_root().add_child(root_node)
	for i in range(45): await process_frame
	for n in root_node.find_children("*", "OptionButton", true, false):
		if n.item_count == 3 and n.get_item_text(1).to_lower().contains("tile"):
			n.selected = 1
			n.item_selected.emit(1)
			break
	for i in range(45): await process_frame
	var tr = root_node.find_child("TileRenderer", true, false)
	print("tile renderer BoundsSize=%s AtlasTileSize=%s" % [tr.BoundsSize, tr.AtlasTileSize])
	var gen = root_node.find_child("TerrainGenerator", true, false)
	print("generator BoundsSize=%s" % gen.BoundsSize)
	for n in root_node.find_children("*", "Sprite2D", true, false):
		if n.name == "TileWater":
			print("water sprite scale=%s pos=%s" % [n.scale, n.position])
	quit(0)
