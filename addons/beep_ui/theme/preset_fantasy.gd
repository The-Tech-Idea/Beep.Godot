@tool
extends BeepPreset

## Fantasy — Medieval parchment & gold UI.
## Ports ThemePresetFantasy.cs. Parchment cream surface, ornate gold trim, magic
## purple accent, decorative 8px corners. RPG quest journal feel.

func _init() -> void:
	# ── Surface ──
	surface_primary = Color(0.96, 0.90, 0.78, 1.0)
	surface_hover = Color(1.0, 0.96, 0.86, 1.0)
	surface_pressed = Color(0.85, 0.78, 0.65, 1.0)
	surface_disabled = Color(0.70, 0.65, 0.55, 1.0)

	# ── Text ──
	text_primary = Color(0.24, 0.16, 0.06, 1.0)
	text_hover = Color(0.14, 0.08, 0.0, 1.0)
	text_disabled = Color(0.55, 0.45, 0.35, 1.0)
	text_on_dark = Color(0.96, 0.90, 0.78, 1.0)

	# ── Accent ──
	accent_primary = Color(0.55, 0.42, 0.08, 1.0)     # gold
	accent_secondary = Color(0.60, 0.25, 0.65, 1.0)   # magic purple

	# ── Border ──
	border_normal = Color(0.55, 0.42, 0.08, 1.0)      # gold
	border_hover = Color(0.70, 0.55, 0.15, 1.0)       # bright gold
	border_focus = Color(0.60, 0.25, 0.65, 0.80)      # purple glow
	border_bevel_light = Color(1, 1, 1, 0)
	border_bevel_dark = Color(1, 1, 1, 0)

	# ── Shadow ──
	shadow_color = Color(0.24, 0.16, 0.06, 0.25)      # warm brown

	# ── Background ──
	bg_panel = Color(0.90, 0.83, 0.70, 0.95)
	bg_canvas = Color(0.80, 0.73, 0.60, 1.0)

	# ── Semantic ──
	semantic_success = Color(0.30, 0.55, 0.25, 1.0)
	semantic_danger = Color(0.70, 0.20, 0.15, 1.0)
	semantic_warning = Color(0.85, 0.65, 0.15, 1.0)
	semantic_info = Color(0.60, 0.25, 0.65, 1.0)

	# ── Geometry ──
	corner_radius = 8
	border_width = 2
	pad_h = 18.0
	pad_v = 9.0
	shadow_size_normal = 4
	shadow_size_hover = 8
	shadow_size_pressed = 1
	shadow_size_focus = 8
	shadow_tint_hover = Color(0.55, 0.42, 0.08, 0.35)  # gold glow
	shadow_tint_focus = Color(0.60, 0.25, 0.65, 0.30)

	# ── Animation ──
	anim_hover_scale = 1.03
	anim_hover_scale_dur = 0.20
	anim_press_scale = 0.97
	anim_press_scale_dur = 0.10
	anim_shadow_lift = true
	anim_focus_glow = true
