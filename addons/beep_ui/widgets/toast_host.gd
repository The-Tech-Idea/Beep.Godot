@tool
extends Control
class_name BeepToastHost

## BeepToastHost — spawn sliding toast messages. A real, working widget.
## Usage: place the host in your UI, then call BeepToastHost.show_toast("Hello")
## or $BeepToastHost.show_toast("Saved", TYPE.SUCCESS).

enum TYPE { INFO, SUCCESS, WARNING, ERROR }

@export var duration: float = 3.0
@export var toast_size: Vector2 = Vector2(300, 44)
@export var max_visible: int = 3

var _active: Array[Panel] = []

static var _instance: BeepToastHost = null


func _ready() -> void:
	if not Engine.is_editor_hint():
		_instance = self
	mouse_filter = Control.MOUSE_FILTER_IGNORE


func _exit_tree() -> void:
	if _instance == self:
		_instance = null


static func show_toast(message: String, type: TYPE = TYPE.INFO) -> void:
	if _instance != null and is_instance_valid(_instance):
		_instance.spawn(message, type)


func spawn(message: String, type: TYPE = TYPE.INFO) -> void:
	if Engine.is_editor_hint():
		return
	var vp_size: Vector2 = get_viewport().get_visible_rect().size
	var toast := Panel.new()
	toast.size = toast_size
	toast.position = Vector2((vp_size.x - toast_size.x) * 0.5, -toast_size.y)

	var bg: Color = Color(0.15, 0.2, 0.3, 0.95)
	match type:
		TYPE.SUCCESS: bg = Color(0.15, 0.6, 0.2, 0.95)
		TYPE.WARNING: bg = Color(0.8, 0.6, 0.1, 0.95)
		TYPE.ERROR: bg = Color(0.8, 0.15, 0.15, 0.95)
	var sb := StyleBoxFlat.new()
	sb.bg_color = bg
	sb.set_corner_radius_all(8)
	toast.add_theme_stylebox_override("panel", sb)

	var icon := "ℹ"
	match type:
		TYPE.SUCCESS: icon = "✓"
		TYPE.WARNING: icon = "⚠"
		TYPE.ERROR: icon = "✕"
	var label := Label.new()
	label.text = "%s   %s" % [icon, message]
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	label.set_anchors_preset(Control.PRESET_FULL_RECT)
	label.add_theme_color_override("font_color", Color.WHITE)
	label.add_theme_font_size_override("font_size", 13)
	toast.add_child(label)

	var y_step: float = toast_size.y + 8
	for t in _active:
		if is_instance_valid(t):
			t.position.y += y_step

	add_child(toast)
	_active.append(toast)
	while _active.size() > max_visible:
		var old = _active.pop_front()
		if is_instance_valid(old):
			old.queue_free()

	var tw := toast.create_tween()
	tw.tween_property(toast, "position:y", 12.0, 0.4).set_ease(Tween.EASE_OUT).set_trans(Tween.TRANS_BACK)
	tw.tween_interval(duration)
	tw.tween_property(toast, "modulate:a", 0.0, 0.3)
	tw.finished.connect(_on_toast_finished.bind(toast))


func _on_toast_finished(toast: Panel) -> void:
	if is_instance_valid(toast):
		toast.queue_free()
	_active.erase(toast)

