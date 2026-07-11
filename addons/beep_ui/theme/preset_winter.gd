@tool
extends BeepPreset

## Winter — Frost / snowy UI.
## Ports ThemePresetWinter.cs. Crisp frost white surface, ice blue accent, subtle
## light blue border, large soft 16px corners, gentle blue glow shadow.

func _init() -> void:
	# ── Surface ──
	surface_primary = Color(0.91, 0.94, 1.0, 1.0)
	surface_hover = Color(0.95, 0.97, 1.0, 1.0)
	surface_pressed = Color(0.78, 0.83, 0.92, 1.0)
	surface_disabled = Color(0.75, 0.78, 0.85, 1.0)

	# ── Text ──
	text_primary = Color(0.15, 0.22, 0.35, 1.0)
	text_hover = Color(0.08, 0.14, 0.25, 1.0)
	text_disabled = Color(0.55, 0.60, 0.70, 1.0)
	text_on_dark = Color(1.0, 1.0, 1.0, 1.0)

	# ── Accent ──
	accent_primary = Color(0.29, 0.56, 0.85, 1.0)     # ice blue
	accent_secondary = Color(0.47, 0.68, 0.90, 1.0)

	# ── Border ──
	border_normal = Color(0.75, 0.85, 0.94, 1.0)
	border_hover = Color(0.29, 0.56, 0.85, 0.40)      # blue 40%
	border_focus = Color(0.29, 0.56, 0.85, 0.80)      # blue 80%
	border_bevel_light = Color(1, 1, 1, 0)
	border_bevel_dark = Color(1, 1, 1, 0)

	# ── Shadow ──
	shadow_color = Color(0.29, 0.56, 0.85, 0.15)      # subtle blue

	# ── Background ──
	bg_panel = Color(0.96, 0.97, 1.0, 0.95)
	bg_canvas = Color(0.88, 0.92, 0.98, 1.0)

	# ── Semantic ──
	semantic_success = Color(0.25, 0.60, 0.35, 1.0)
	semantic_danger = Color(0.85, 0.25, 0.30, 1.0)
	semantic_warning = Color(0.90, 0.70, 0.20, 1.0)
	semantic_info = Color(0.29, 0.56, 0.85, 1.0)

	# ── Geometry ──
	corner_radius = 16
	border_width = 1
	pad_h = 16.0
	pad_v = 9.0
	shadow_size_normal = 8
	shadow_size_hover = 14
	shadow_size_pressed = 3
	shadow_size_focus = 12
	shadow_tint_hover = Color(0.29, 0.56, 0.85, 0.25)
	shadow_tint_focus = Color(0.29, 0.56, 0.85, 0.20)

	# ── Animation ──
	anim_hover_scale = 1.03
	anim_hover_scale_dur = 0.20
	anim_press_scale = 0.97
	anim_press_scale_dur = 0.10
	anim_shadow_lift = true
	anim_focus_glow = true
