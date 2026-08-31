extends SceneTree
func _initialize() -> void:
	var root_node = load("res://tests/examples/terrain_iso_demo.tscn").instantiate()
	get_root().add_child(root_node)
	for i in range(20): await process_frame
	var gen = root_node.find_child("TerrainGenerator", true, false)
	for case in [[Vector2i(48,48), 1, 0.70, "demo-ish island"], [Vector2i(64,64), 0, 0.42, "lab continents"]]:
		gen.BoundsSize = case[0]
		gen.Landform = case[1]
		gen.LandmassScale = float(case[2])
		gen.GenerateTerrain()
		var hist := {}
		for y in range(case[0].y):
			for x in range(case[0].x):
				var k: String = gen.TerrainKindAt(Vector2i(x, y))
				hist[k] = hist.get(k, 0) + 1
		print("%s BeachWidth=%.2f -> sand %d | %s" % [case[3], gen.BeachWidth, hist.get("sand", 0), str(hist)])
	quit(0)
