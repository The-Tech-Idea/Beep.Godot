@tool
class_name BeepUIEffect
extends Node

## BeepUIEffect — unified UI effect component. Ports UIEffectComponent.cs.
## Attach to any node. Two dropdowns control everything:
##   effect — what to play (Slide, Shake, Pulse, Bob, Flash, Glitch, Rotate, Fade, Typewriter, Bounce, Offset)
##   scope  — what to target (Self, Children, Scene, Global)
##
## Per-effect properties are shown/hidden automatically via _validate_property.

enum EffectType { SLIDE, SHAKE, PULSE, BOB, FLASH, GLITCH, ROTATE, FADE, TYPEWRITER, BOUNCE, OFFSET }
enum ScopeType { SELF, CHILDREN, SCENE, GLOBAL }
enum SlideDirection { LEFT, RIGHT, UP, DOWN }
enum FadeDirection { IN, OUT, IN_OUT }
enum RotateAxis { X, Y, Z }

signal effect_started
signal effect_completed
signal effect_looped(loop_count: int)

# ── Core ──
# Setter, not a bare export: _validate_property only re-runs when the object notifies a
# property-list change, so without this the per-effect group promised in the header stayed
# stale until you deselected and reselected the node.
@export var effect: EffectType = EffectType.SLIDE : set = _set_effect
@export var scope: ScopeType = ScopeType.SELF

# ── Timing ──
@export_range(0, 10, 0.05) var duration: float = 0.4
@export_range(0, 5, 0.05) var initial_delay: float = 0.0
@export var easing: Tween.EaseType = Tween.EASE_OUT
@export var transition: Tween.TransitionType = Tween.TRANS_BACK

# ── Playback ──
@export var play_on_ready: bool = true
@export var looping: bool = false
@export_range(0, 10, 0.1) var loop_delay: float = 0.0

# ── Slide ──
@export var slide_dir: SlideDirection = SlideDirection.UP
@export_range(0, 2000, 1) var slide_distance: float = 100.0

# ── Shake ──
@export_range(0, 200, 0.5) var shake_intensity: float = 10.0
@export_range(1, 50) var shake_vibrato: int = 20

# ── Pulse ──
@export_range(0.5, 2, 0.05) var pulse_min_scale: float = 0.95
@export_range(0.5, 2, 0.05) var pulse_max_scale: float = 1.05
@export_range(0, 20) var pulse_loops: int = 0  # 0 = infinite

# ── Bob ──
@export_range(0, 100, 0.5) var bob_height: float = 10.0
@export_range(0.1, 10, 0.1) var bob_speed: float = 2.0

# ── Flash ──
@export var flash_color: Color = Color.WHITE
@export_range(1, 10) var flash_count: int = 2

# ── Glitch ──
@export_range(0, 100, 0.5) var glitch_intensity: float = 5.0
@export_range(1, 50) var glitch_segments: int = 10

# ── Rotate ──
@export_range(-360, 360, 1) var rotate_angle: float = 360.0
@export var rotate_axis: RotateAxis = RotateAxis.Z

# ── Fade ──
@export_range(0, 1, 0.05) var fade_target_alpha: float = 0.0
@export var fade_dir: FadeDirection = FadeDirection.IN

# ── Typewriter ──
@export_range(1, 200, 1) var typewriter_speed: float = 30.0
@export var typewriter_cursor: String = "|"

# ── Bounce ──
@export_range(0, 200, 1) var bounce_height: float = 60.0
@export_range(1, 10) var bounce_count: int = 3

# ── Offset ──
@export var offset_target: Vector2 = Vector2.ZERO

# ── State ──
var _targets: Array[Control] = []
var _active_tweens: Array[Tween] = []
var _pending_timers: Array[Timer] = []
var _pending_tween_count: int = 0
var _is_playing: bool = false
var _loop_count: int = 0
var _process_time: float = 0.0
var _tw_states: Dictionary = {}


func _ready() -> void:
	_resolve_targets()
	if play_on_ready and not Engine.is_editor_hint():
		play.call_deferred()


func _process(delta: float) -> void:
	if not _is_playing:
		return
	if effect == EffectType.BOB:
		_process_time += delta
		var offset: float = sin(_process_time * bob_speed) * bob_height
		for c in _targets:
			if is_instance_valid(c):
				c.offset_transform_position = Vector2(0, offset)
	if effect == EffectType.TYPEWRITER:
		_process_typewriter(delta)


func _exit_tree() -> void:
	stop()


# ════════════════════════════════════════════════════════════════
# Target resolution
# ════════════════════════════════════════════════════════════════

func _resolve_targets() -> void:
	_targets.clear()
	match scope:
		ScopeType.SELF:
			var parent = get_parent()
			if parent is Control:
				_add_target(parent)
		ScopeType.CHILDREN:
			var root = get_parent()
			if root is Control:
				_add_target(root)
				_collect_controls(root, _targets, true)
		ScopeType.SCENE:
			var scene = get_tree().current_scene if is_inside_tree() else null
			if scene != null:
				_collect_controls(scene, _targets)
		ScopeType.GLOBAL:
			if is_inside_tree():
				_collect_controls(get_tree().root, _targets)
	# Animate the offset transform layer, not position/scale. Godot 4.7's
	# offset_transform_* (GH-87081) is a render transform that containers do not
	# overwrite — the same fix theme_applier.gd documents and uses. Animating
	# position/scale directly meant any VBox/HBox/GridContainer re-sorted the control
	# out from under the tween, and BOB (which writes every _process tick) fought the
	# container every frame. Almost everything widget_factory builds is a container.
	#
	# The offsets are relative to the layout position, so "original" is always
	# Vector2.ZERO / Vector2.ONE — there is nothing to capture or restore.
	for c in _targets:
		if is_instance_valid(c):
			c.offset_transform_enabled = true


func _add_target(c: Control) -> void:
	if is_instance_valid(c) and not _targets.has(c):
		_targets.append(c)


func _collect_controls(node: Node, list: Array[Control], skip_root: bool = false) -> void:
	if not skip_root and node is Control:
		list.append(node)
	for child in node.get_children():
		if child is Node:
			_collect_controls(child, list)


# ════════════════════════════════════════════════════════════════
# Public API
# ════════════════════════════════════════════════════════════════

func play() -> void:
	if _targets.is_empty():
		_resolve_targets()
	if _targets.is_empty():
		# Nothing to animate — most often a SELF/CHILDREN scope on a non-Control parent.
		# Say so rather than returning in silence (theme_applier.gd warns in the same case).
		if not Engine.is_editor_hint():
			push_warning("[%s] BeepUIEffect.play() resolved no targets (scope=%d) — nothing will animate. For SELF/CHILDREN the parent must be a Control; otherwise use SCENE/GLOBAL scope." % [name, scope])
		return
	stop()
	_is_playing = true
	_loop_count = 0
	if initial_delay > 0.0:
		_start_timer(initial_delay)
	else:
		_execute_effect()


func _start_timer(delay: float) -> Timer:
	var timer := Timer.new()
	timer.one_shot = true
	timer.wait_time = delay
	timer.timeout.connect(_on_delay_fired.bind(timer))
	add_child(timer)
	_pending_timers.append(timer)
	timer.start()
	return timer


func stop() -> void:
	_is_playing = false
	# BOB drives offset_transform_position directly in _process (no tween), so stopping mid-cycle would
	# otherwise strand the target at its last sine offset — only reset() zeroed it. Neutralise it here.
	if effect == EffectType.BOB:
		for c in _targets:
			if is_instance_valid(c):
				c.offset_transform_position = Vector2.ZERO
	_process_time = 0.0
	_stop_typewriter()
	for t in _active_tweens:
		if is_instance_valid(t):
			t.kill()
	_active_tweens.clear()
	_pending_tween_count = 0
	for timer in _pending_timers:
		if is_instance_valid(timer):
			timer.queue_free()
	_pending_timers.clear()


func reset() -> void:
	stop()
	for c in _targets:
		if is_instance_valid(c):
			# Zero the offset layer rather than restoring captured values — the layout
			# position is untouched throughout, so neutral IS zero/one.
			c.offset_transform_position = Vector2.ZERO
			c.offset_transform_scale = Vector2.ONE
			c.modulate = Color.WHITE


# ════════════════════════════════════════════════════════════════
# Dispatcher
# ════════════════════════════════════════════════════════════════

func _execute_effect() -> void:
	effect_started.emit()
	# Drop dead tweens first. Finished tweens were never removed — only stop() cleared
	# the list — so a looping effect appended one per target per cycle on top of every
	# dead tween from every prior cycle and grew without bound. theme_applier.gd prunes
	# its own map for the same reason.
	_active_tweens = _active_tweens.filter(func(t): return is_instance_valid(t) and t.is_running())
	# Remember how many tweens existed before the effect spawns new ones, so we
	# only wire completion for the tweens spawned by THIS invocation.
	var tween_base := _active_tweens.size()
	for c in _targets:
		if not is_instance_valid(c):
			continue
		# The offset layer must be on for any of the transform effects to render.
		# Re-asserted here because targets can be re-resolved after _ready.
		c.offset_transform_enabled = true
		match effect:
			EffectType.SLIDE: _play_slide(c)
			EffectType.SHAKE: _play_shake(c)
			EffectType.PULSE: _play_pulse(c)
			EffectType.BOB: _process_time = 0.0
			EffectType.FLASH: _play_flash(c)
			EffectType.GLITCH: _play_glitch(c)
			EffectType.ROTATE: _play_rotate(c)
			EffectType.FADE: _play_fade(c)
			EffectType.TYPEWRITER: _play_typewriter(c)
			EffectType.BOUNCE: _play_bounce(c)
			EffectType.OFFSET: _play_offset(c)
	if effect != EffectType.BOB and _active_tweens.size() > tween_base:
		# Count every tween spawned by this invocation and only fire "completed"
		# once all of them finish. Connecting only to the last tween would mark
		# the effect done early when tween durations differ.
		_pending_tween_count = _active_tweens.size() - tween_base
		for i in range(tween_base, _active_tweens.size()):
			_active_tweens[i].finished.connect(_on_tween_finished)


func _on_all_completed() -> void:
	if looping and _is_playing:
		_loop_count += 1
		effect_looped.emit(_loop_count)
		if loop_delay > 0.0:
			_start_timer(loop_delay)
		else:
			_execute_effect()
	else:
		_is_playing = false
		effect_completed.emit()


func _on_tween_finished() -> void:
	if _pending_tween_count > 0:
		_pending_tween_count -= 1
	if _pending_tween_count == 0 and _is_playing:
		_on_all_completed()


func _on_delay_fired(timer: Timer) -> void:
	_pending_timers.erase(timer)
	if is_instance_valid(timer):
		timer.queue_free()
	if _is_playing:
		_execute_effect()


# ════════════════════════════════════════════════════════════════
# Effects
# ════════════════════════════════════════════════════════════════

func _play_slide(c: Control) -> void:
	var offset: Vector2 = Vector2.ZERO
	match slide_dir:
		SlideDirection.LEFT: offset = Vector2(-slide_distance, 0)
		SlideDirection.RIGHT: offset = Vector2(slide_distance, 0)
		SlideDirection.UP: offset = Vector2(0, -slide_distance)
		SlideDirection.DOWN: offset = Vector2(0, slide_distance)
	c.offset_transform_position = offset
	c.modulate = Color(1, 1, 1, 0)
	var t := c.create_tween().set_parallel(true)
	t.tween_property(c, "offset_transform_position", Vector2.ZERO, duration).set_ease(easing).set_trans(transition)
	t.tween_property(c, "modulate:a", 1.0, duration * 0.6)
	_active_tweens.append(t)


func _play_shake(c: Control) -> void:
	var steps := shake_vibrato
	var t := c.create_tween()
	for i in steps:
		var fraction: float = float(i + 1) / float(steps)
		var decay: float = 1.0 - fraction
		var x_off: float = (randf() * 2.0 - 1.0) * shake_intensity * decay
		var y_off: float = (randf() * 2.0 - 1.0) * shake_intensity * decay
		t.tween_property(c, "offset_transform_position", Vector2(x_off, y_off), duration / float(steps))
	t.tween_property(c, "offset_transform_position", Vector2.ZERO, duration / float(steps))
	_active_tweens.append(t)


func _play_pulse(c: Control) -> void:
	var half: float = duration / 2.0
	if pulse_loops <= 0:
		var t := c.create_tween().set_loops()
		t.tween_property(c, "offset_transform_scale", Vector2(pulse_max_scale, pulse_max_scale), half).set_ease(Tween.EASE_OUT).set_trans(Tween.TRANS_SINE)
		t.tween_property(c, "offset_transform_scale", Vector2(pulse_min_scale, pulse_min_scale), half).set_ease(Tween.EASE_IN).set_trans(Tween.TRANS_SINE)
		_active_tweens.append(t)
	else:
		var t := c.create_tween()
		for i in pulse_loops:
			t.tween_property(c, "offset_transform_scale", Vector2(pulse_max_scale, pulse_max_scale), half).set_ease(Tween.EASE_OUT).set_trans(Tween.TRANS_SINE)
			t.tween_property(c, "offset_transform_scale", Vector2(pulse_min_scale, pulse_min_scale), half).set_ease(Tween.EASE_IN).set_trans(Tween.TRANS_SINE)
		t.tween_property(c, "offset_transform_scale", Vector2.ONE, half)
		_active_tweens.append(t)


func _play_flash(c: Control) -> void:
	var orig_color: Color = c.modulate
	var fd: float = duration / float(flash_count * 2)
	var t := c.create_tween()
	for i in flash_count:
		t.tween_property(c, "modulate", flash_color, fd)
		t.tween_property(c, "modulate", orig_color, fd)
	_active_tweens.append(t)


func _play_glitch(c: Control) -> void:
	var seg: float = duration / float(glitch_segments)
	var t := c.create_tween()
	for i in glitch_segments:
		var x_off: float = (randf() * 2.0 - 1.0) * glitch_intensity
		var y_off: float = (randf() * 2.0 - 1.0) * glitch_intensity
		var s_off: float = 1.0 + (randf() * 2.0 - 1.0) * glitch_intensity * 0.01
		var r_off: float = (randf() * 2.0 - 1.0) * glitch_intensity * 0.02
		t.tween_property(c, "offset_transform_position", Vector2(x_off, y_off), seg * 0.5)
		t.tween_property(c, "offset_transform_scale", Vector2(s_off, s_off), seg * 0.5)
		t.tween_property(c, "offset_transform_rotation", r_off, seg * 0.5)
		if i < glitch_segments - 1:
			t.tween_property(c, "offset_transform_position", Vector2.ZERO, seg * 0.5)
			t.tween_property(c, "offset_transform_scale", Vector2.ONE, seg * 0.5)
			t.tween_property(c, "offset_transform_rotation", 0.0, seg * 0.5)
	t.tween_property(c, "offset_transform_position", Vector2.ZERO, seg)
	t.tween_property(c, "offset_transform_scale", Vector2.ONE, seg)
	t.tween_property(c, "offset_transform_rotation", 0.0, seg)
	_active_tweens.append(t)


func _play_rotate(c: Control) -> void:
	match rotate_axis:
		RotateAxis.X:
			var t := c.create_tween()
			t.tween_property(c, "offset_transform_scale:y", 0.0, duration * 0.5)
			t.tween_property(c, "offset_transform_scale:y", 1.0, duration * 0.5)
			_active_tweens.append(t)
		RotateAxis.Y:
			var t := c.create_tween()
			t.tween_property(c, "offset_transform_scale:x", 0.0, duration * 0.5)
			t.tween_property(c, "offset_transform_scale:x", 1.0, duration * 0.5)
			_active_tweens.append(t)
		RotateAxis.Z:
			var radians: float = deg_to_rad(rotate_angle)
			var t := c.create_tween()
			t.tween_property(c, "offset_transform_rotation", radians, duration).set_ease(easing).set_trans(transition)
			_active_tweens.append(t)


func _play_fade(c: Control) -> void:
	match fade_dir:
		FadeDirection.IN: c.modulate = Color(1, 1, 1, 0)
		FadeDirection.OUT: c.modulate = Color(1, 1, 1, 1)
		FadeDirection.IN_OUT: c.modulate = Color(1, 1, 1, 0)
	var t := c.create_tween()
	if fade_dir == FadeDirection.IN_OUT:
		t.tween_property(c, "modulate:a", 1.0, duration * 0.5).set_ease(easing).set_trans(transition)
		t.tween_property(c, "modulate:a", fade_target_alpha, duration * 0.5).set_ease(easing).set_trans(transition)
	else:
		t.tween_property(c, "modulate:a", fade_target_alpha, duration).set_ease(easing).set_trans(transition)
	_active_tweens.append(t)


func _play_bounce(c: Control) -> void:
	var per: float = duration / float(bounce_count)
	var t := c.create_tween()
	for i in bounce_count:
		var h: float = bounce_height * (1.0 - float(i) / float(bounce_count))
		t.tween_property(c, "offset_transform_position:y", -h, per * 0.4).set_ease(Tween.EASE_OUT)
		t.tween_property(c, "offset_transform_position:y", 0.0, per * 0.6).set_ease(Tween.EASE_IN).set_trans(Tween.TRANS_BOUNCE)
	_active_tweens.append(t)


func _play_offset(c: Control) -> void:
	# offset_target is now relative to the layout position rather than an absolute
	# screen position — which is what "offset" always implied, and the only reading
	# that survives a container re-sorting its children.
	var t := c.create_tween()
	t.tween_property(c, "offset_transform_position", offset_target, duration).set_ease(easing).set_trans(transition)
	_active_tweens.append(t)


# ── Typewriter ──

func _play_typewriter(c: Control) -> void:
	var full_text := ""
	var is_rich := false
	if c is RichTextLabel:
		full_text = (c as RichTextLabel).text
		is_rich = true
	elif c is Label:
		full_text = (c as Label).text
	else:
		return
	if full_text.is_empty():
		return
	var cur := "|" if typewriter_cursor.is_empty() else typewriter_cursor
	_tw_states[c] = {"text": full_text, "cursor": cur, "elapsed": 0.0, "rich": is_rich}


func _process_typewriter(delta: float) -> void:
	# Retire each entry the moment it finishes reveal-ing, and emit completion once the last one is
	# done. Previously nothing removed finished entries, so the label's full text was re-assigned every
	# frame forever and effect_completed never fired (TYPEWRITER spawns no tween, so the tween-based
	# completion path in _execute_effect never applied to it).
	var completed: Array = []
	for c in _tw_states.keys():
		if not is_instance_valid(c):
			completed.append(c)
			continue
		var state: Dictionary = _tw_states[c]
		state["elapsed"] = float(state["elapsed"]) + delta
		var total: int = String(state["text"]).length()
		var visible_n: int = clampi(int(float(state["elapsed"]) * typewriter_speed), 0, total)
		var done: bool = visible_n >= total
		var shown: String = String(state["text"]).substr(0, visible_n)
		var cur: String = String(state["cursor"]) if not done else ""
		if state["rich"] and c is RichTextLabel:
			(c as RichTextLabel).text = shown + cur
		elif c is Label:
			(c as Label).text = shown + cur
		if done:
			completed.append(c)
	for c in completed:
		_tw_states.erase(c)
	if _tw_states.is_empty() and _is_playing and effect == EffectType.TYPEWRITER:
		_on_all_completed()


func _stop_typewriter() -> void:
	_tw_states.clear()


# ════════════════════════════════════════════════════════════════
# Conditional inspector property visibility (port of _ValidateProperty)
# ════════════════════════════════════════════════════════════════

func _set_effect(value: EffectType) -> void:
	effect = value
	# Ask the inspector to re-query the property list so _validate_property below runs
	# again for the newly-selected effect.
	notify_property_list_changed()


func _validate_property(property: Dictionary) -> void:
	var n: String = property["name"]
	var groups := {
		"slide_": effect == EffectType.SLIDE,
		"shake_": effect == EffectType.SHAKE,
		"pulse_": effect == EffectType.PULSE,
		"bob_": effect == EffectType.BOB,
		"flash_": effect == EffectType.FLASH,
		"glitch_": effect == EffectType.GLITCH,
		"rotate_": effect == EffectType.ROTATE,
		"fade_": effect == EffectType.FADE,
		"typewriter_": effect == EffectType.TYPEWRITER,
		"bounce_": effect == EffectType.BOUNCE,
		"offset_": effect == EffectType.OFFSET,
	}
	for prefix in groups:
		if n.begins_with(prefix):
			property["usage"] = PROPERTY_USAGE_DEFAULT if groups[prefix] else PROPERTY_USAGE_NO_EDITOR
			return
