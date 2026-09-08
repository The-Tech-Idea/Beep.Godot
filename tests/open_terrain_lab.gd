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
	world.set("MapArt", scene.get("CartoonProfile"))
	root.add_child(scene)
