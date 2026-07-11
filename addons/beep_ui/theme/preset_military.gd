@tool
extends BeepPreset

## Military — Tactical / camo UI.
## Ports ThemePresetMilitary.cs. Olive drab surface, khaki brass accent, sharp
## square corners, hard utilitarian shadow. Tactical operations display feel.

func _init() -> void:
	# ── Surface ──
	surface_primary = Color(0.29, 0.30, 0.24, 1.0)
	surface_hover = Color(0.35, 0.36, 0.29, 1.0)
	surface_pressed = Color(0.22, 0.23, 0.18, 1.0)
	surface_disabled = Color(0.18, 0.19, 0.16, 1.0)

	# ── Text ──
	text_primary = Color(0.88, 0.86, 0.80, 1.0)
	text_hover = Color(0.95, 0.93, 0.88, 1.0)
	text_disabled = Color(0.45, 0.44, 0.40, 1.0)
	text_on_dark = Color(0.83, 0.66, 0.26, 1.0)

	# ── Accent ──
	accent_primary = Color(0.83, 0.66, 0.26, 1.0)     # brass
	accent_secondary = Color(0.65, 0.50, 0.18, 1.0)

	# ── Border ──
	border_normal = Color(0.16, 0.18, 0.11, 1.0)
	border_hover = Color(0.83, 0.66, 0.26, 0.50)      # brass 50%
	border_focus = Color(0.83, 0.66, 0.26, 0.85)      # brass 85%
	border_bevel_light = Color(1, 1, 1, 0)
	border_bevel_dark = Color(1, 1, 1, 0)

	# ── Shadow ──
	shadow_color = Color(0.0, 0.0, 0.0, 0.50)

	# ── Background ──
	bg_panel = Color(0.22, 0.23, 0.18, 0.95)
	bg_canvas = Color(0.15, 0.16, 0.12, 1.0)

	# ── Semantic ──
	semantic_success = Color(0.30, 0.45, 0.25, 1.0)
	semantic_danger = Color(0.70, 0.22, 0.18, 1.0)
	semantic_warning = Color(0.83, 0.66, 0.26, 1.0)
	semantic_info = Color(0.28, 0.50, 0.65, 1.0)

	# ── Geometry ──
	corner_radius = 2
	border_width = 2
	pad_h = 14.0
	pad_v = 7.0
	shadow_size_normal = 4
	shadow_size_hover = 6
	shadow_size_pressed = 0
	shadow_size_focus = 4
	shadow_tint_hover = Color(0.0, 0.0, 0.0, 0.60)

	# ── Animation ──
	anim_hover_scale = 1.02
	anim_hover_scale_dur = 0.08
	anim_press_scale = 0.98
	anim_press_scale_dur = 0.04
	anim_shadow_lift = false
	anim_focus_glow = true
