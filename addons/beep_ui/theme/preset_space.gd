@tool
extends BeepPreset

## Space — Deep void / cosmic UI.
## Ports ThemePresetSpace.cs. Near-absolute black surface, star white text,
## nebula purple accent, outer glow shadow. Distinct from SciFi.

func _init() -> void:
	# ── Surface ──
	surface_primary = Color(0.02, 0.02, 0.06, 1.0)
	surface_hover = Color(0.04, 0.04, 0.12, 1.0)
	surface_pressed = Color(0.01, 0.01, 0.04, 1.0)
	surface_disabled = Color(0.02, 0.02, 0.05, 1.0)

	# ── Text ──
	text_primary = Color(0.88, 0.88, 0.95, 1.0)       # star white
	text_hover = Color(1.0, 1.0, 1.0, 1.0)
	text_disabled = Color(0.35, 0.35, 0.45, 1.0)
	text_on_dark = Color(0.65, 0.55, 0.95, 1.0)

	# ── Accent ──
	accent_primary = Color(0.47, 0.42, 0.93, 1.0)     # nebula purple
	accent_secondary = Color(0.65, 0.55, 0.95, 1.0)

	# ── Border ──
	border_normal = Color(0.19, 0.19, 0.67, 1.0)
	border_hover = Color(0.47, 0.42, 0.93, 0.50)      # purple 50%
	border_focus = Color(0.47, 0.42, 0.93, 0.85)      # purple 85%
	border_bevel_light = Color(1, 1, 1, 0)
	border_bevel_dark = Color(1, 1, 1, 0)

	# ── Shadow ──
	shadow_color = Color(0.47, 0.42, 0.93, 0.35)      # purple outer glow

	# ── Background ──
	bg_panel = Color(0.02, 0.02, 0.05, 0.95)
	bg_canvas = Color(0.01, 0.01, 0.03, 1.0)

	# ── Semantic ──
	semantic_success = Color(0.20, 0.55, 0.40, 1.0)
	semantic_danger = Color(0.80, 0.25, 0.30, 1.0)
	semantic_warning = Color(0.85, 0.70, 0.20, 1.0)
	semantic_info = Color(0.47, 0.42, 0.93, 1.0)

	# ── Geometry ──
	corner_radius = 6
	border_width = 1
	pad_h = 16.0
	pad_v = 8.0
	shadow_size_normal = 10
	shadow_size_hover = 18
	shadow_size_pressed = 4
	shadow_size_focus = 20
	shadow_tint_hover = Color(0.47, 0.42, 0.93, 0.55)
	shadow_tint_focus = Color(0.47, 0.42, 0.93, 0.65)

	# ── Animation ──
	anim_hover_scale = 1.03
	anim_hover_scale_dur = 0.15
	anim_press_scale = 0.97
	anim_press_scale_dur = 0.08
	anim_shadow_lift = false
	anim_focus_glow = true
