extends SceneTree

# Switching skin must not change a widget's size.
#
# It did. Several kit widgets blank the native control's StyleBoxes and restate the content margins
# themselves, and some computed those margins from the control's CURRENT Size. Godot derives the
# minimum size FROM those margins, so every theme change fed a widget's own size back into its own
# margins and the control ratcheted — visibly bigger each time the genre picker was used, which is
# exactly what a user hits when they sit and switch skins.
#
# The rule this holds, for EVERY widget in the kit rather than the handful that were noticed: a
# widget's minimum size is a function of its font, its genre and its text. Never of its current
# size. Re-applying a theme any number of times is a no-op.

const KIT_DIR := "res://addons/beep_game_builder_cs/ecs/ui/kit/"

# Infrastructure and non-Control types — nothing to instantiate as a widget.
const SKIP := [
	"KitArchetypes", "KitChrome", "KitControl", "KitCore", "KitEdgeRun",
	"KitFonts", "KitLayer", "KitSelect", "KitShadow", "KitSprite", "KitStyleJson",
]

const SWITCHES := 6
const TOLERANCE := 0.5

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	root.size = Vector2i(1000, 700)
	var host := Control.new()
	host.name = "Host"
	host.set_anchors_preset(Control.PRESET_FULL_RECT)
	host.theme = _probe_theme()
	host.set_meta("_beep_kit_genre", "citybuilder")
	root.add_child(host)

	var names := _widget_names()
	if names.size() < 40:
		return _fail("only found %d kit widgets to test; the sweep is not covering the kit" % names.size())

	var unstable := PackedStringArray()
	var tested := 0

	for widget_name in names:
		var script: Script = load(KIT_DIR + widget_name + ".cs")
		if script == null:
			continue
		var made: Variant = script.new()
		if not (made is Control):
			if made is Object and not (made is RefCounted):
				(made as Object).free()
			continue

		var w: Control = made
		w.name = widget_name
		# Content is set BEFORE entering the tree, the way a .tscn does it. Setting it afterwards
		# made _Ready measure an empty widget and the first theme change measure a full one, and
		# that harness artefact looks exactly like the product bug being hunted here.
		if w is OptionButton:
			w.add_item("City Builder")
			w.add_item("Card Game")
			w.select(0)
		elif w is Button:
			w.text = "Play"
		host.add_child(w)
		await process_frame
		await process_frame

		var first := w.get_combined_minimum_size()
		for i in range(SWITCHES):
			# A container gives a widget MORE room than its minimum, and several widgets derived
			# their frame from that granted height and wrote it into the very margins their minimum
			# is computed from. Re-applying the theme without ever growing the control hides that
			# entirely: the loop needs a Size larger than the minimum to feed on. So stretch it
			# first, exactly as a VBoxContainer would, then re-theme.
			w.size = Vector2(first.x + 140.0, first.y + 40.0)
			await process_frame
			w.notification(Control.NOTIFICATION_THEME_CHANGED)
			await process_frame
		var last := w.get_combined_minimum_size()
		tested += 1

		if absf(last.x - first.x) > TOLERANCE or absf(last.y - first.y) > TOLERANCE:
			unstable.append("%s %s -> %s" % [widget_name, str(first), str(last)])

		host.remove_child(w)
		w.queue_free()
		await process_frame

	if not unstable.is_empty():
		return _fail("re-applying the theme resized these widgets: " + ", ".join(unstable))

	print("[kit-theme-switch] OK: %d kit widgets keep their size across %d theme re-applications."
		% [tested, SWITCHES])
	quit(0)

func _widget_names() -> PackedStringArray:
	var names := PackedStringArray()
	var dir := DirAccess.open(KIT_DIR)
	if dir == null:
		return names
	dir.list_dir_begin()
	var file := dir.get_next()
	while file != "":
		if file.ends_with(".cs"):
			var base := file.get_basename()
			if not SKIP.has(base):
				names.append(base)
		file = dir.get_next()
	dir.list_dir_end()
	return names

func _probe_theme() -> Theme:
	var theme := Theme.new()
	theme.set_color("neutral", "BeepSemantic", Color(0.09, 0.10, 0.11, 1.0))
	theme.set_color("accent", "BeepSemantic", Color(0.86, 0.54, 0.14, 1.0))
	theme.set_color("info", "BeepSemantic", Color(0.22, 0.53, 0.75, 1.0))
	theme.set_color("focus", "BeepSemantic", Color(0.54, 0.85, 0.66, 1.0))
	theme.set_color("font_color", "Label", Color(0.94, 0.93, 0.88, 1.0))
	theme.set_font_size("font_size", "Label", 16)
	return theme

func _fail(message: String) -> void:
	# Printed as well as pushed: the failure list is the point of this probe, and digging it out of
	# a stderr stream full of engine warnings is how it gets misread.
	print("[kit-theme-switch] FAIL: " + message)
	push_error("[kit-theme-switch] " + message)
	quit(1)
