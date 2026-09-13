extends SceneTree

const BASE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/ground_masks/staging/"
var errors: Array[String] = []

func _initialize() -> void:
	call_deferred("run")

func check(ok: bool, reason: String) -> void:
	if not ok:
		errors.append(reason)
		push_error(reason)

func run() -> void:
	for projection in ["square", "isometric"]:
		var scene = load(BASE + projection + "/surface_candidate_v1/grass_dirt_review.tscn").instantiate()
		root.add_child(scene)
		var plane = scene.get_node("SurfacePlane")
		var layer = scene.get_node("Ground")
		var original_material = layer.material
		check(plane.refresh() == "", "Valid plane rejected")
		check(layer.material != original_material, "Shared input material was mutated")
		var owned = layer.material
		check(plane.refresh() == "" and layer.material == owned, "Refresh duplicates materials repeatedly")
		var sprite = Sprite2D.new()
		sprite.name = "SpriteSurface"
		sprite.material = original_material
		scene.add_child(sprite)
		plane.layer_paths.append(NodePath("../SpriteSurface"))
		check(plane.refresh()=="", "Explicit sprite binding rejected")
		check(sprite.material!=original_material, "Sprite binding mutated shared material")
		var unsupported = Node2D.new()
		unsupported.name = "UnsupportedSurface"
		scene.add_child(unsupported)
		plane.layer_paths.append(NodePath("../UnsupportedSurface"))
		check(plane.refresh().contains("explicit sprite"), "Unsupported canvas target accepted")
		plane.layer_paths.pop_back()
		var before = owned.get_shader_parameter("surface_origin")
		plane.layer_paths.append(NodePath("../Missing"))
		scene.position = Vector2(50, 80)
		check(plane.refresh().contains("missing"), "Missing layer accepted")
		check(owned.get_shader_parameter("surface_origin") == before, "Invalid group partly updated")
		plane.layer_paths.pop_back()
		check(plane.refresh() == "", "Recovered group rejected")
		check(owned.get_shader_parameter("surface_origin") == scene.global_position, "Origin did not follow map")
		before = owned.get_shader_parameter("surface_origin")
		scene.transform = Transform2D(Vector2.ZERO, Vector2.DOWN, scene.position)
		check(plane.refresh().contains("singular"), "Singular plane accepted")
		check(owned.get_shader_parameter("surface_origin") == before, "Singular plane changed coordinates")
		scene.transform = Transform2D(0, scene.position)
		plane.projection = 1 - plane.projection
		check(plane.refresh().contains("projection"), "Wrong projection accepted")
		scene.free()
	print("SURFACE PLANE ", "PASSED" if errors.is_empty() else "FAILED")
	quit(0 if errors.is_empty() else 1)
