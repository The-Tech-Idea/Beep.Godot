extends SceneTree

var _failures: Array[String] = []

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	await process_frame
	_check_project_settings()
	_check_theme_applier()
	_check_data_binder_host()
	await _check_tween_component()
	if _failures.is_empty():
		print("[headless-smoke] OK: project settings, beep_ui preset property, data binder directions, and tween preset endpoints validated.")
		quit(0)
	else:
		for failure in _failures:
			push_error("[headless-smoke] " + failure)
		quit(1)

func _fail(message: String) -> void:
	_failures.append(message)

func _check_project_settings() -> void:
	if ProjectSettings.has_setting("godot_mcp/bridge/token"):
		_fail("project.godot must not persist godot_mcp/bridge/token")
	if ProjectSettings.get_setting("godot_mcp/security/allow_editor_writes", true) != false:
		_fail("godot_mcp/security/allow_editor_writes should default to false")
	if ProjectSettings.get_setting("godot_mcp/security/allow_runtime_writes", true) != false:
		_fail("godot_mcp/security/allow_runtime_writes should default to false")

func _check_theme_applier() -> void:
	var script := load("res://addons/beep_ui/theme/theme_applier.gd")
	if script == null:
		_fail("Could not load beep_ui theme_applier.gd")
		return
	var target := Control.new()
	target.name = "ThemeTarget"
	root.add_child(target)
	var applier: Node = script.new()
	target.add_child(applier)
	var preset_hint := ""
	for prop in applier.get_property_list():
		if prop.get("name") == "preset" and str(prop.get("hint_string")) != "":
			if prop.get("hint") == PROPERTY_HINT_ENUM:
				preset_hint = str(prop.get("hint_string"))
	if preset_hint == "":
		_fail("theme_applier did not expose a dynamic enum preset property")
	for required in ["Modern", "SciFi", "Cyberpunk"]:
		if not preset_hint.contains(required):
			_fail("theme_applier preset hint is missing " + required)
	target.queue_free()

func _check_data_binder_host() -> void:
	var smoke_script := load("res://tests/DataBinderHostSmoke.cs")
	if smoke_script == null:
		_fail("Could not load DataBinderHostSmoke.cs")
		return
	var smoke: Node = smoke_script.new()
	root.add_child(smoke)
	if not bool(smoke.call("Run")):
		_fail("DataBinderHost smoke failed: " + str(smoke.get("Failure")))
	smoke.queue_free()

func _check_tween_component() -> void:
	var script := load("res://addons/beep_game_builder_cs/ecs/TweenComponent.cs")
	if script == null:
		_fail("Could not load TweenComponent.cs")
		return
	for target_kind in ["Control", "Node2D"]:
		for preset in range(22):
			var parent: Node = _control_target() if target_kind == "Control" else _node2d_target()
			root.add_child(parent)
			var component: Node = script.new()
			component.set("PlayOnReady", false)
			component.set("Duration", 0.001)
			component.set("Animation", preset)
			parent.add_child(component)
			component.call("Play")
			if preset in [14, 16]:
				await process_frame
			else:
				await create_timer(_tween_wait_seconds(preset)).timeout
				_check_tween_endpoint(parent, preset, target_kind)
			component.call("Stop")
			component.queue_free()
			parent.queue_free()

func _tween_wait_seconds(preset: int) -> float:
	match preset:
		13:
			return 0.32
		17:
			return 0.18
		_:
			return 0.08

func _check_tween_endpoint(parent: Node, preset: int, target_kind: String) -> void:
	var scale_prop: String = "offset_transform_scale" if parent is Control else "scale"
	var position_prop: String = "offset_transform_position" if parent is Control else "position"
	var rotation_prop: String = "offset_transform_rotation" if parent is Control else "rotation"
	var label := "%s preset %s" % [target_kind, str(preset)]
	match preset:
		0, 6:
			_expect_vec2(parent.get(scale_prop), Vector2.ONE, label + " scale")
		1, 7:
			_expect_vec2(parent.get(scale_prop), Vector2.ZERO, label + " scale")
		2:
			_expect_float(parent.modulate.a, 1.0, label + " alpha")
		3:
			_expect_float(parent.modulate.a, 0.0, label + " alpha")
		4:
			_expect_float(parent.get(position_prop).x, 0.0, label + " x")
		5:
			_expect_float(parent.get(position_prop).x, 200.0, label + " x")
		8:
			_expect_vec2(parent.get(scale_prop), Vector2.ONE * 1.15, label + " scale")
		9:
			_expect_vec2(parent.get(scale_prop), Vector2.ONE * 0.85, label + " scale")
		10:
			_expect_float(parent.get(rotation_prop), 0.0, label + " rotation")
			_expect_float(parent.modulate.a, 1.0, label + " alpha")
		11:
			_expect_float(parent.get(rotation_prop), deg_to_rad(90.0), label + " rotation")
			_expect_float(parent.modulate.a, 0.0, label + " alpha")
		12, 19:
			_expect_vec2(parent.get(scale_prop), Vector2.ONE, label + " scale")
		13:
			_expect_vec2(parent.get(position_prop), Vector2.ZERO, label + " position")
		15, 21:
			_expect_float(parent.get(scale_prop).x, 1.0, label + " scale x")
		17:
			_expect_float(parent.get(scale_prop).x, 1.2, label + " scale x")
			_expect_float(parent.get(scale_prop).y, 0.75, label + " scale y")
		18:
			_expect_vec2(parent.get(scale_prop), Vector2.ONE * 1.03, label + " scale")
			_expect_float(parent.get(rotation_prop), 0.0, label + " rotation")
		20:
			_expect_vec2(parent.get(scale_prop), Vector2.ONE, label + " scale")
			_expect_float(parent.get(rotation_prop), 0.0, label + " rotation")
			_expect_float(parent.modulate.a, 1.0, label + " alpha")

func _expect_vec2(actual: Vector2, expected: Vector2, label: String) -> void:
	if not actual.is_finite():
		_fail(label + " is not finite: " + str(actual))
	if actual.distance_to(expected) > 0.04:
		_fail("%s expected %s but got %s" % [label, str(expected), str(actual)])

func _expect_float(actual: float, expected: float, label: String) -> void:
	if not is_finite(actual):
		_fail(label + " is not finite: " + str(actual))
	if absf(actual - expected) > 0.04:
		_fail("%s expected %.3f but got %.3f" % [label, expected, actual])

func _control_target() -> Control:
	var control := Control.new()
	control.name = "TweenControlTarget"
	control.size = Vector2(100, 50)
	return control

func _node2d_target() -> Node2D:
	var node := Node2D.new()
	node.name = "TweenNode2DTarget"
	return node
