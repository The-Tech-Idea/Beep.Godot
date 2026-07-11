@tool
extends BeepPreset

## Steampunk — Victorian brass & copper industrial UI.
## Ports ThemePresetSteampunk.cs. Brass surface, copper accent, ornate 6px corners,
## beveled 3D border, chunky shadow. Gears and steam age feel.

func _init() -> void:
	# ── Surface ──
	surface_primary = Color(0.55, 0.42, 0.08, 1.0)    # brass
	surface_hover = Color(0.65, 0.50, 0.15, 1.0)
	surface_pressed = Color(0.42, 0.32, 0.05, 1.0)
	surface_disabled = Color(0.38, 0.30, 0.12, 1.0)

	# ── Text ──
	text_primary = Color(0.18, 0.12, 0.04, 1.0)
	text_hover = Color(0.08, 0.04, 0.0, 1.0)
	text_disabled = Color(0.45, 0.38, 0.28, 1.0)
	text_on_dark = Color(0.92, 0.82, 0.60, 1.0)

	# ── Accent ──
	accent_primary = Color(0.80, 0.50, 0.22, 1.0)     # copper
	accent_secondary = Color(0.65, 0.38, 0.15, 1.0)

	# ── Border ──
	border_normal = Color(0.38, 0.28, 0.06, 1.0)      # dark brass
	border_hover = Color(0.80, 0.50, 0.22, 0.60)      # copper 60%
	border_focus = Color(0.80, 0.50, 0.22, 0.90)      # copper 90%
	border_bevel_light = Color(0.75, 0.60, 0.20, 1.0) # bright brass
	border_bevel_dark = Color(0.30, 0.22, 0.06, 1.0)  # dark edge

	# ── Shadow ──
	shadow_color = Color(0.0, 0.0, 0.0, 0.55)

	# ── Background ──
	bg_panel = Color(0.48, 0.36, 0.10, 0.95)
	bg_canvas = Color(0.38, 0.28, 0.08, 1.0)

	# ── Semantic ──
	semantic_success = Color(0.30, 0.45, 0.20, 1.0)
	semantic_danger = Color(0.55, 0.20, 0.15, 1.0)
	semantic_warning = Color(0.70, 0.50, 0.15, 1.0)
	semantic_info = Color(0.80, 0.50, 0.22, 1.0)

	# ── Geometry ──
	corner_radius = 6
	border_width = 3
	pad_h = 16.0
	pad_v = 8.0
	shadow_size_normal = 6
	shadow_size_hover = 10
	shadow_size_pressed = 1
	shadow_size_focus = 8
	shadow_tint_hover = Color(0.0, 0.0, 0.0, 0.65)
	shadow_tint_focus = Color(0.80, 0.50, 0.22, 0.35)

	# ── Animation ──
	anim_hover_scale = 1.03
	anim_hover_scale_dur = 0.18
	anim_press_scale = 0.97
	anim_press_scale_dur = 0.10
	anim_shadow_lift = true
	anim_focus_glow = true
