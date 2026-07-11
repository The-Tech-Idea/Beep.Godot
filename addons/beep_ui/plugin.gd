@tool
extends EditorPlugin

const ThemeStudioDock := preload("res://addons/beep_ui/editor/theme_studio.gd")

var _dock: Control

func _enter_tree() -> void:
	_dock = ThemeStudioDock.new()
	_dock.name = "Beep UI"
	add_control_to_dock(DOCK_SLOT_RIGHT_UL, _dock)
	print("[Beep UI] enabled.")

func _exit_tree() -> void:
	if _dock:
		remove_control_from_docks(_dock)
		_dock.queue_free()
		_dock = null
