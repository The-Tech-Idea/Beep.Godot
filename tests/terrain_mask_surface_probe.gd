extends SceneTree

func _initialize() -> void:
    var failed := false
    for projection in ["square", "isometric"]:
        var material = load("res://addons/beep_game_builder_cs/materials/terrain_mask_%s.tres" % projection) as ShaderMaterial
        if material == null or material.shader == null:
            push_error("Missing mask material: " + projection)
            failed = true
            continue
        if material.get_shader_parameter("surface_enabled") != false:
            push_error("Surface must be opt-in")
            failed = true
        var u: Vector2 = material.get_shader_parameter("surface_u")
        var v: Vector2 = material.get_shader_parameter("surface_v")
        var x_axis := Vector2(256, 0) if projection == "square" else Vector2(128, 64)
        var y_axis := Vector2(0, 256) if projection == "square" else Vector2(-128, 64)
        if not Vector2(x_axis.dot(u), x_axis.dot(v)).is_equal_approx(Vector2(1, 0)) or not Vector2(y_axis.dot(u), y_axis.dot(v)).is_equal_approx(Vector2(0, 1)):
            push_error("Incorrect projected texture basis")
            failed = true
        if not material.resource_local_to_scene:
            push_error("Scene material settings must be independent")
            failed = true
    print("TERRAIN MASK SURFACE: ", "FAIL" if failed else "PASS (resource and coordinate contracts only)")
    quit(1 if failed else 0)
