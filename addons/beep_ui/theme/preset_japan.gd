@tool
extends BeepPreset

## Japan — Minimal elegant Japanese UI.
## Ports ThemePresetJapan.cs. Clean white surface, vermillion red accent, dark
## charcoal text, subtle 4px corners, minimal shadow. Wabi-sabi restraint.

func _init() -> void:
	# ── Surface ──
	surface_primary = Color(1.0, 1.0, 1.0, 1.0)
	surface_hover = Color(0.96, 0.94, 0.93, 1.0)
	surface_pressed = Color(0.88, 0.85, 0.83, 1.0)
	surface_disabled = Color(0.90, 0.88, 0.86, 1.0)

	# ── Text ──
	text_primary = Color(0.15, 0.13, 0.12, 1.0)
	text_hover = Color(0.0, 0.0, 0.0, 1.0)
	text_disabled = Color(0.65, 0.62, 0.59, 1.0)
	text_on_dark = Color(1.0, 1.0, 1.0, 1.0)

	# ── Accent ──
	accent_primary = Color(0.74, 0.0, 0.18, 1.0)      # vermillion
	accent_secondary = Color(0.55, 0.0, 0.12, 1.0)

	# ── Border ──
	border_normal = Color(0.83, 0.77, 0.72, 1.0)
	border_hover = Color(0.74, 0.0, 0.18, 0.40)       # vermillion 40%
	border_focus = Color(0.74, 0.0, 0.18, 0.80)       # vermillion 80%
	border_bevel_light = Color(1, 1, 1, 0)
	border_bevel_dark = Color(1, 1, 1, 0)

	# ── Shadow ──
	shadow_color = Color(0.0, 0.0, 0.0, 0.08)         # minimal

	# ── Background ──
	bg_panel = Color(0.97, 0.96, 0.94, 0.95)
	bg_canvas = Color(0.93, 0.91, 0.88, 1.0)

	# ── Semantic ──
	semantic_success = Color(0.25, 0.50, 0.30, 1.0)
	semantic_danger = Color(0.74, 0.0, 0.18, 1.0)
	semantic_warning = Color(0.80, 0.55, 0.15, 1.0)
	semantic_info = Color(0.20, 0.40, 0.60, 1.0)

	# ── Geometry ──
	corner_radius = 4
	border_width = 1
	pad_h = 20.0
	pad_v = 10.0
	shadow_size_normal = 4
	shadow_size_hover = 8
	shadow_size_pressed = 1
	shadow_size_focus = 6
	shadow_tint_hover = Color(0.0, 0.0, 0.0, 0.12)
	shadow_tint_focus = Color(0.74, 0.0, 0.18, 0.15)

	# ── Animation ──
	anim_hover_scale = 1.02
	anim_hover_scale_dur = 0.25
	anim_press_scale = 0.98
	anim_press_scale_dur = 0.12
	anim_shadow_lift = true
	anim_focus_glow = true
