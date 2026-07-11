@tool
extends BeepPreset

## SciFi — Dark cyberpunk UI.
## Ports ThemePresetSciFi.cs. Angled corners (sharp top, rounded bottom),
## neon cyan glow borders, dark indigo surface, pulsing focus glow.

func _init() -> void:
	# ── Surface ──
	surface_primary = Color(0.05, 0.05, 0.13, 1.0)
	surface_hover = Color(0.08, 0.08, 0.23, 1.0)
	surface_pressed = Color(0.03, 0.03, 0.10, 1.0)
	surface_disabled = Color(0.02, 0.02, 0.09, 1.0)

	# ── Text ──
	text_primary = Color(0.0, 0.90, 1.0, 1.0)         # cyan
	text_hover = Color(0.0, 1.0, 1.0, 1.0)
	text_disabled = Color(0.25, 0.25, 0.38, 1.0)
	text_on_dark = Color(0.0, 0.90, 1.0, 1.0)

	# ── Accent ──
	accent_primary = Color(0.0, 0.90, 1.0, 1.0)
	accent_secondary = Color(0.0, 0.72, 0.83, 1.0)

	# ── Border ──
	border_normal = Color(0.0, 0.90, 1.0, 0.27)       # cyan 27%
	border_hover = Color(0.0, 0.90, 1.0, 0.60)        # cyan 60%
	border_focus = Color(0.0, 0.90, 1.0, 0.85)        # cyan 85%
	border_bevel_light = Color(1, 1, 1, 0)
	border_bevel_dark = Color(1, 1, 1, 0)

	# ── Shadow ──
	shadow_color = Color(0.0, 0.90, 1.0, 0.30)        # cyan glow

	# ── Background ──
	bg_panel = Color(0.04, 0.04, 0.10, 0.95)
	bg_canvas = Color(0.02, 0.02, 0.08, 1.0)

	# ── Semantic ──
	semantic_success = Color(0.0, 0.85, 0.55, 1.0)
	semantic_danger = Color(1.0, 0.20, 0.30, 1.0)
	semantic_warning = Color(1.0, 0.75, 0.0, 1.0)
	semantic_info = Color(0.0, 0.90, 1.0, 1.0)

	# ── Geometry ──
	corner_radius = 2
	border_width = 2
	pad_h = 20.0
	pad_v = 10.0
	shadow_size_normal = 12
	shadow_size_hover = 18
	shadow_size_pressed = 6
	shadow_size_focus = 20
	shadow_tint_hover = Color(0.0, 0.90, 1.0, 0.50)
	shadow_tint_focus = Color(0.0, 0.90, 1.0, 0.60)

	# ── Animation ──
	anim_hover_scale = 1.03
	anim_hover_scale_dur = 0.12
	anim_press_scale = 0.97
	anim_press_scale_dur = 0.06
	anim_shadow_lift = false
	anim_focus_glow = true
