@tool
extends BeepPreset

## Cartoon — Bright playful UI.
## Ports ThemePresetCartoon.cs. Large 20px pill corners, 4px solid black outline,
## hard drop shadow, bouncy hover, squashy press.

func _init() -> void:
	# ── Surface ──
	surface_primary = Color(1.0, 0.72, 0.0, 1.0)
	surface_hover = Color(1.0, 0.79, 0.20, 1.0)
	surface_pressed = Color(0.90, 0.65, 0.0, 1.0)
	surface_disabled = Color(0.75, 0.63, 0.48, 1.0)

	# ── Text ──
	text_primary = Color(0.18, 0.11, 0.0, 1.0)
	text_hover = Color(0.0, 0.0, 0.0, 1.0)
	text_disabled = Color(0.55, 0.45, 0.33, 1.0)
	text_on_dark = Color(1.0, 1.0, 1.0, 1.0)

	# ── Accent ──
	accent_primary = Color(1.0, 0.42, 0.21, 1.0)
	accent_secondary = Color(1.0, 0.55, 0.35, 1.0)

	# ── Border ──
	border_normal = Color(0.18, 0.11, 0.0, 1.0)
	border_hover = Color(0.0, 0.0, 0.0, 1.0)
	border_focus = Color(1.0, 0.42, 0.21, 1.0)
	border_bevel_light = Color(1, 1, 1, 0)
	border_bevel_dark = Color(1, 1, 1, 0)

	# ── Shadow ──
	shadow_color = Color(0.0, 0.0, 0.0, 0.80)

	# ── Background ──
	bg_panel = Color(1.0, 0.95, 0.84, 1.0)
	bg_canvas = Color(1.0, 0.91, 0.75, 1.0)

	# ── Semantic ──
	semantic_success = Color(0.22, 0.80, 0.30, 1.0)
	semantic_danger = Color(0.95, 0.20, 0.20, 1.0)
	semantic_warning = Color(1.0, 0.75, 0.15, 1.0)
	semantic_info = Color(0.24, 0.60, 0.95, 1.0)

	# ── Geometry ──
	corner_radius = 20
	border_width = 4
	pad_h = 24.0
	pad_v = 12.0
	shadow_size_normal = 0
	shadow_size_hover = 0
	shadow_size_pressed = 0
	shadow_size_focus = 0
	shadow_tint_hover = Color(0.0, 0.0, 0.0, 0.90)

	# ── Animation ──
	anim_hover_scale = 1.08
	anim_hover_scale_dur = 0.18
	anim_press_scale = 0.92
	anim_press_scale_dur = 0.06
	anim_shadow_lift = true
	anim_focus_glow = true
