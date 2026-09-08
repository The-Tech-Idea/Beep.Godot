extends SceneTree

# The genre's ARTWORK reaches the plate, carries the palette, and stops at the classes it covers.
#
#  1. The sprite is actually drawn. KitSprite tints its art per palette colour and keeps the result
#     on the drawing control, under CACHE_META — so the presence of that metadata after a frame is
#     a direct, binary record that the sprite branch ran and produced a plate. Remove the genre's
#     `Material` declaration and the metadata is never written.
#
#  2. The palette still owns the colour. The art is neutral grey and is re-tinted; if the tint were
#     skipped, or a coloured source variant imported, the plate would render grey whatever the skin
#     says. That is the failure the removed per-theme texture system could not avoid, and it is why
#     the plate's chroma and hue are measured off the rendered pixels rather than trusted.
#
#  3. The art covers only the classes it declares. The first version of this change gave Chip a
#     nine-slice and the rarity and level chips visibly stopped being pills; the entry was removed,
#     so a chip must record no plate at all.
#
#     Note what 3 does NOT prove. KitSprite.FitsSilhouette is the rule that would reject a
#     non-rectangular silhouette, and it is not what decides this case — the Chip class has no entry
#     at all, so TryPlate refuses before reaching it. With the one material this build ships and the
#     one genre that opts in, every covered class is already Round, so FitsSilhouette never changes
#     an outcome and is not under test. It would first bite on rpg, whose buttons and panels are
#     Chamfer while its slots are Round.
#
# WHY NOT MEASURE THE SHADING. The first version of this probe asserted a top-to-bottom luminance
# gradient across the face, on the reading that the procedural plate is flat. It is not: the carved
# register draws a seven-band vertical shade, and mutation-testing showed the procedural button
# measuring a LARGER gradient (0.1152) than the artwork (0.0828) — the guard passed with the feature
# removed. What actually separates the two is the SHAPE of the shading, continuous against seven
# discrete steps, and pinning that means pinning a stair-step count against antialiasing noise. The
# metadata states the same fact exactly.

const KIT_PUSH_BUTTON_SCRIPT := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitPushButton.cs")
const KIT_CHIP_SCRIPT := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitChip.cs")

# Where KitSprite parks the tinted plates a control has drawn with. Named here because this probe is
# the reason the key may not be renamed without a matching change.
const CACHE_META := "_beep_kit_sprite_plates"

# How far the plate may drift from the palette's own hue before it is reading as the art's grey.
# Measured as the spread between the largest and smallest channel, normalised: grey collapses to 0,
# and the stub theme's amber measures ~0.79.
const MIN_CHROMA := 0.20

# The face colour the stub theme declares, and what the button's plate must still be made of.
const FACE := Color(0.86, 0.54, 0.14, 1.0)

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	root.size = Vector2i(420, 240)
	root.content_scale_size = root.size

	var bg := ColorRect.new()
	bg.color = Color(0.02, 0.03, 0.03, 1.0)
	bg.set_anchors_preset(Control.PRESET_FULL_RECT)
	root.add_child(bg)

	var host := Control.new()
	host.name = "Host"
	host.set_anchors_preset(Control.PRESET_FULL_RECT)
	host.theme = _probe_theme()
	# citybuilder is the genre that declares Material = "ui_pack".
	host.set_meta("_beep_kit_genre", "citybuilder")
	bg.add_child(host)

	var button := KIT_PUSH_BUTTON_SCRIPT.new() as Button
	button.name = "Plate"
	# No label: the plate is what is being measured, and centred glyphs land inside the sample band.
	button.text = ""
	button.position = Vector2(30, 30)
	button.size = Vector2(180, 56)
	host.add_child(button)

	var chip: Control = KIT_CHIP_SCRIPT.new()
	chip.name = "Pill"
	chip.position = Vector2(30, 140)
	chip.size = Vector2(150, 40)
	host.add_child(chip)
	chip.set("Text", "")

	await process_frame
	await process_frame
	await process_frame
	await process_frame
	RenderingServer.force_draw(false)

	var image := root.get_texture().get_image()
	if image == null or image.is_empty():
		return _fail("Viewport image is empty.")

	# 1 -- the artwork was resolved, tinted and drawn.
	if not button.has_meta(CACHE_META):
		return _fail("KitPushButton drew no sprite plate under a genre that declares a sprite "
			+ "material, so the artwork never reached KitChrome.DrawPlate.")
	var plates: Dictionary = button.get_meta(CACHE_META)
	if plates.is_empty():
		return _fail("KitPushButton's sprite cache is empty; a plate was expected.")
	var first: Variant = plates.values()[0]
	if not (first is Texture2D):
		return _fail("KitPushButton's sprite cache holds a %s, not a texture."
			% type_string(typeof(first)))

	# 2 -- and it is the palette's colour, not the art's grey.
	var button_rect := button.get_global_rect()
	var plate := _average_colour(image, _face_band(button_rect, 0.30, 0.55))
	var chroma := _chroma(plate)
	if chroma < MIN_CHROMA:
		return _fail(("KitPushButton's plate has drained to grey (chroma %.4f, needs %.2f): the "
			+ "artwork is not taking the palette's face colour %s.") % [chroma, MIN_CHROMA, str(FACE)])
	if not _same_hue(plate, FACE):
		return _fail("KitPushButton's plate is %s, which is not the palette's %s tinted."
			% [str(plate), str(FACE)])

	# 3 -- a chip is a pill, and the material declares no artwork for that class.
	if chip.has_meta(CACHE_META):
		return _fail("KitChip drew a sprite plate. Every genre's chip is a pill and a nine-slice "
			+ "is a rounded rectangle, so the Chip class carries no artwork.")

	print("[kit-sprite-measured] plates=%d chroma=%.4f plate=%s" % [plates.size(), chroma, str(plate)])
	print("[kit-sprite-material] OK: the genre's artwork carries the plate, takes the palette, "
		+ "and leaves the pill alone.")
	quit(0)

func _face_band(r: Rect2, from: float, to: float) -> Rect2:
	var inset := r.size.x * 0.18
	return Rect2(r.position + Vector2(inset, r.size.y * from),
		Vector2(r.size.x - inset * 2.0, max(1.0, r.size.y * (to - from))))

func _chroma(c: Color) -> float:
	var hi: float = max(c.r, max(c.g, c.b))
	var lo: float = min(c.r, min(c.g, c.b))
	if hi <= 0.001:
		return 0.0
	return (hi - lo) / hi

# Same hue, allowing the artwork to have moved the brightness. Channels are compared after
# normalising each colour by its own largest channel, which is what "the same colour, lighter or
# darker" means.
func _same_hue(a: Color, b: Color) -> bool:
	var ha: float = max(a.r, max(a.g, a.b))
	var hb: float = max(b.r, max(b.g, b.b))
	if ha <= 0.001 or hb <= 0.001:
		return false
	return (absf(a.r / ha - b.r / hb) < 0.14
		and absf(a.g / ha - b.g / hb) < 0.14
		and absf(a.b / ha - b.b / hb) < 0.14)

func _average_colour(image: Image, rect: Rect2) -> Color:
	var left := clampi(int(floor(rect.position.x)), 0, image.get_width() - 1)
	var right := clampi(int(ceil(rect.end.x)), 0, image.get_width() - 1)
	var top := clampi(int(floor(rect.position.y)), 0, image.get_height() - 1)
	var bottom := clampi(int(ceil(rect.end.y)), 0, image.get_height() - 1)
	var r := 0.0
	var g := 0.0
	var b := 0.0
	var count := 0
	for y in range(top, bottom + 1):
		for x in range(left, right + 1):
			var c := image.get_pixel(x, y)
			r += c.r
			g += c.g
			b += c.b
			count += 1
	if count == 0:
		return Color(0, 0, 0, 1)
	return Color(r / float(count), g / float(count), b / float(count), 1.0)

func _fail(message: String) -> void:
	push_error("[kit-sprite-material] " + message)
	print("[kit-sprite-material] FAILED: " + message)
	quit(1)

func _probe_theme() -> Theme:
	var theme := Theme.new()
	theme.set_color("neutral", "BeepSemantic", Color(0.035, 0.055, 0.055, 1.0))
	theme.set_color("accent", "BeepSemantic", FACE)
	theme.set_color("success", "BeepSemantic", Color(0.28, 0.62, 0.40, 1.0))
	theme.set_color("font_color", "Label", Color(0.94, 0.93, 0.88, 1.0))
	theme.set_font_size("font_size", "Label", 16)
	return theme
