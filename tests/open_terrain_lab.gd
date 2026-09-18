extends SceneTree

# Interactive reproduction of the reported map, using the existing lab scene.
func _initialize() -> void:
	call_deferred("open_lab")

func open_lab() -> void:
	root.size = Vector2i(1280, 800)
	var scene: Node = load("res://addons/beep_game_builder_cs/templates/scenes/terrain/terrain_generator_lab.tscn").instantiate()
	var world: Node = scene.get_node("World")
	world.set("MapSize", 4)
	world.set("Seed", 31415)
	# Opens on Cartoon. The lab's view menu owns the style list now, so this takes it from there and
	# lets SelectedView put the menu on the matching entry. It used to read `scene.get("CartoonProfile")`,
	# a property that no longer exists - and because the string form of `get` answers null rather than
	# failing, the lab quietly opened with NO art at all instead of saying so.
	var styles = scene.get("StyleProfiles")
	assert(styles != null and styles.size() > 1, "the lab scene lists no Cartoon style")
	world.set("MapArt", styles[1])
	root.add_child(scene)
