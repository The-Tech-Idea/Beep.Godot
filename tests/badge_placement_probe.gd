extends SceneTree

# A badge never leaves the control it marks.
#
# BadgeComponent positioned its badge absolutely, at `Position` defaulting to (0, -8): against the
# host's LEFT edge and eight pixels ABOVE it. Above is the worst direction to escape in — a
# container has already allocated the host's rect, so the badge lands on whatever sits above it —
# and it was on the wrong corner besides. The kit's art notes call the top-right straddle "the
# attention anchor".
#
# The rule this holds, for the component and for anything that copies it: a badge overhangs the
# PLATE it marks, never the control's own bounds. KitChrome.DrawCornerBadge already clamps the
# drawn badges to the control's rect; this is the same rule for the child-Control ones.
#
# The second escape this caught is subtler, and is why the check measures all four sides instead of
# only the top: the offsets were computed from the diameter the component asks for, while a Count
# chip measures itself from its TEXT, so the badge sat 1.9px past the host's right edge. A
# single-count check would have passed against that.

const HOST_SCRIPT := "res://addons/beep_game_builder_cs/ecs/ui/kit/KitPushButton.cs"
const BADGE_SCRIPT := "res://addons/beep_game_builder_cs/ecs/ui/BadgeComponent.cs"

const HOST_SIZE := Vector2(140, 48)
const COUNTS := [1, 7, 42, 999]

var _theme: Theme = null

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	root.size = Vector2i(420, 220)

	# A stub theme, so UiSurface does not warn about a missing palette. The warning is correct --
	# nothing here runs ThemePresetComponent -- and its backtrace would read as a probe failure.
	var host_theme := Theme.new()
	host_theme.set_color("neutral", "BeepSemantic", Color(0.10, 0.11, 0.13, 1.0))
	host_theme.set_color("accent", "BeepSemantic", Color(0.86, 0.54, 0.14, 1.0))
	host_theme.set_color("danger", "BeepSemantic", Color(0.80, 0.25, 0.22, 1.0))
	host_theme.set_color("font_color", "Label", Color(0.94, 0.93, 0.88, 1.0))
	host_theme.set_font_size("font_size", "Label", 16)
	_theme = host_theme

	for count in COUNTS:
		var failure: String = await _check(count)
		if failure != "":
			return _fail(failure)

	print("[badge-placement] OK: the badge stays inside its host at every count tested.")
	quit(0)

func _check(count: int) -> String:
	var host: Control = load(HOST_SCRIPT).new()
	host.name = "Host"
	host.custom_minimum_size = HOST_SIZE
	host.theme = _theme
	root.add_child(host)

	var comp = load(BADGE_SCRIPT).new()
	comp.name = "BadgeDriver"
	comp.set("GenerateControlsWhenPathsEmpty", true)
	comp.set("Count", count)
	host.add_child(comp)

	# Enough passes for a minimum-size change to propagate through the layout.
	for i in range(12):
		await process_frame

	var chip: Control = null
	for c in host.get_children():
		if c is Control:
			chip = c
	if chip == null:
		host.free()
		return "count %d produced no badge control at all" % count

	var r := chip.get_rect()
	var size := host.size
	var escapes := PackedStringArray()
	if r.position.y < -0.01:
		escapes.append("ABOVE by %.2f" % -r.position.y)
	if r.position.x < -0.01:
		escapes.append("left by %.2f" % -r.position.x)
	if r.end.y > size.y + 0.01:
		escapes.append("below by %.2f" % (r.end.y - size.y))
	if r.end.x > size.x + 0.01:
		escapes.append("right by %.2f" % (r.end.x - size.x))

	var report := ""
	if escapes.size() > 0:
		report = "count %d: badge %s escapes its %s host -- %s" % [
			count, str(r), str(size), ", ".join(escapes)]
	else:
		print("[badge-placement-measured] count=%d badge=%s host=%s" % [count, str(r), str(size)])

	host.free()
	return report

func _fail(message: String) -> void:
	push_error("[badge-placement] " + message)
	print("[badge-placement] FAILED: " + message)
	quit(1)
