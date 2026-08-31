extends SceneTree

# Every example scene the addon ships must LOAD and actually build something.
#
# This exists because a broken one hid in plain sight. The isometric demo's
# controller node was removed while the node that used it stayed behind, so the
# scene failed to parse - and the only symptom was that other guards, which
# happen to load that scene, started timing out. Nothing said "this scene is
# broken"; the run just stopped finishing.
#
# A scene that ships with an addon is the first thing a consumer opens. If it
# does not run, the addon does not work, whatever the components do in isolation.

const SCENES := "res://addons/beep_game_builder_cs/templates/scenes/terrain/"

var failures: Array[String] = []

func check(ok: bool, message: String) -> void:
	if ok:
		print("  ok    %s" % message)
	else:
		print("  FAIL  %s" % message)
		failures.append(message)

# Anything that counts as the scene having drawn: painted tiles, or a node that
# renders without them (a shader surface, a batched _Draw renderer).
func drawn_evidence(root: Node) -> int:
	var tiles := 0
	for n in root.find_children("*", "TileMapLayer", true, false):
		tiles += n.get_used_cells().size()
	if tiles > 0:
		return tiles
	# No tile layers is legitimate for the painted view, which is one quad.
	return root.find_children("*", "Sprite2D", true, false).size()

func _initialize() -> void:
	var names := DirAccess.get_files_at(SCENES)
	check(names.size() > 0, "the addon ships example scenes (%d files)" % names.size())

	for file_name in names:
		if not file_name.ends_with(".tscn"):
			continue

		var packed = load(SCENES + file_name)
		if packed == null:
			check(false, "%s loads" % file_name)
			continue

		var root = packed.instantiate()
		if root == null:
			check(false, "%s instantiates" % file_name)
			continue

		get_root().add_child(root)
		for i in range(45): await process_frame

		var evidence := drawn_evidence(root)
		check(evidence > 0, "%s builds something (%d drawn)" % [file_name, evidence])
		root.free()
		for i in range(3): await process_frame

	# --- the two scenes whose controllers became data-driven components -----
	var world = load(SCENES + "terrain_15_piece_layers_demo.tscn").instantiate()
	get_root().add_child(world)
	for i in range(40): await process_frame

	var cells = world.find_child("Cells", true, false)
	var kinds := {}
	for y in range(10):
		for x in range(18):
			var k: String = cells.GetTerrainKind(Vector2i(x, y))
			kinds[k] = kinds.get(k, 0) + 1
	for want in ["grass", "water", "desert", "volcano"]:
		check(kinds.get(want, 0) > 0,
			"the cell pattern seeded %s (%d cells)" % [want, kinds.get(want, 0)])
	world.free()

	var kit = load(SCENES + "grid_world_kit_hud_example.tscn").instantiate()
	get_root().add_child(kit)
	for i in range(40): await process_frame

	var dispatch = kit.find_child("Dispatch", true, false)
	check(dispatch != null, "the settlers scene drives a dispatch board")
	if dispatch != null:
		check(dispatch.Tasks.size() == 8,
			"the dispatch board carries its 8 tasks (%d)" % dispatch.Tasks.size())
	kit.free()

	print("\nRESULT: ", "all checks passed" if failures.is_empty() else "%d FAILED" % failures.size())
	quit(1 if failures.size() > 0 else 0)
