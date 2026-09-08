extends Node2D

## The shipped construction-site scene: a MODULAR 2.5D building site
## assembled from tileable material swatches and flat geometry, sized to the
## site it is told about - more plank, brick and beam repeats for a bigger
## footprint, never one image stretched.
##
## This is the RENDERING half of the construction effect. The grid layer
## (GridBuildStageVisualComponent) never draws; it instantiates this scene
## and tells it the facts of the site through the IConstructionVisual
## contract - read here by NAME, since GDScript cannot implement a C#
## interface:
##   SiteFootprint (Vector2i)   how many cells the site occupies
##   CellSize      (Vector2)    world pixels per cell
##   StageIndex / StageCount    which listed stage this is (this scene draws
##                              the whole build from BuildProgress and ignores
##                              them; a static per-stage scene would not)
##   SiteState     (String)     "pending" | "queued" | "working" | "complete"
##   SiteMaterials (Array)      [{id, required, delivered}] stock on site
##   BuildProgress (float)      0..1 over the whole build
##   WorkPulse()                a hit of work landed
##
## What it draws follows what real construction sites are read by (see
## docs/grid-system/CONSTRUCTION_VISUALS_RESEARCH.md): things that exist
## only during the build and come down at the end (corner marks, a sign,
## material piles, scaffolding taller than the walls), ground disturbed
## beyond the footprint (the dirt apron), a skeleton before a skin, parts
## rising one after another rather than one clean slice, courses that land
## on their own row lines, a hit effect at the point being worked, and a
## hard swap with a teardown beat at the end - the grid layer shows the
## finished art the instant the job completes, and this scene lives on for
## a moment with SiteState "complete" to sink its scaffold over it.
##
## Projection: 2.5D top-down oblique. The footprint stays on the grid (the
## slab's front edge is the site's own front edge); the front wall rises
## straight up; the right wall and the roof are drawn skewed by ObliqueSkew
## - the cabinet oblique a top-down builder uses, orthographic, no
## vanishing point.
##
## Sequence over BuildProgress, by MaterialKind:
##   wood / steel: slab 0-0.10, frame 0.10-0.50 (posts then beams, one after
##                 another), walls 0.50-0.80 (front columns left to right,
##                 then the right wall), roof 0.80-1.0 (rows from the eave)
##   brick:        slab 0-0.10, courses 0.10-0.80 (a course at a time, front
##                 columns then the right wall), roof 0.80-1.0

const SWATCH_DIR := "res://addons/beep_game_builder_cs/templates/art/construction_stages/swatches/"
const REVEAL_SHADER := preload("res://addons/beep_game_builder_cs/shaders/construction_reveal.gdshader")
const DUST_SCENE := preload("res://addons/beep_game_builder_cs/templates/particles/construction_dust.tscn")
const SPARKS_SCENE := preload("res://addons/beep_game_builder_cs/templates/particles/weld_sparks.tscn")
const AUDIO_DIR := "res://addons/beep_game_builder_cs/audio/combat/"

## "wood", "steel" or "brick" - picks the swatch family and the sequence.
@export var MaterialKind: String = "wood"
## Wall height as a multiple of the cell's height.
@export var WallHeightCells: float = 1.5
## How far the back edge shifts right per unit of depth - the oblique skew
## that exposes the right wall and the roof. 0 is pure front-on.
@export var ObliqueSkew: float = 0.3
## Beam/post thickness as a fraction of the cell's width.
@export var BeamWidthCells: float = 0.12
## Foundation slab thickness as a fraction of the cell's height.
@export var FoundationHeightCells: float = 0.18
## World pixels one wall-swatch repeat covers, as a multiple of cell width.
@export var WallSwatchCells: float = 1.0
## World pixels one roof-swatch repeat covers, as a multiple of cell width.
@export var RoofSwatchCells: float = 1.0
## How far the dirt apron extends past the footprint, in cells.
@export var ApronCells: float = 0.5
## How much taller than the wall the scaffold stands, in cells.
@export var ScaffoldExtraCells: float = 0.35
## How long the scaffold takes to come down once SiteState is "complete".
@export var ScaffoldTeardownSeconds: float = 1.0
## How long the corner marks take to grow in when the site is first staked.
@export var MarkGrowSeconds: float = 1.0
## Per-hit clips; empty uses the shipped Kenney hits for the material.
@export var HitSounds: Array[AudioStream] = []

# IConstructionVisual, by name.
var SiteFootprint: Vector2i = Vector2i(1, 1):
	set(value):
		SiteFootprint = Vector2i(maxi(1, value.x), maxi(1, value.y))
		_rebuild_if_ready()
var CellSize: Vector2 = Vector2(64, 64):
	set(value):
		CellSize = Vector2(maxf(1.0, value.x), maxf(1.0, value.y))
		_rebuild_if_ready()
var StageIndex: int = 0
var StageCount: int = 1
var SiteState: String = "pending":
	set(value):
		if SiteState == value:
			return
		SiteState = value
		if value == "complete":
			_teardown_elapsed = 0.0
		_apply_state()
var SiteMaterials: Array = []:
	set(value):
		SiteMaterials = value
		_apply_piles()
var BuildProgress: float = 0.0:
	set(value):
		BuildProgress = clampf(value, 0.0, 1.0)
		_apply_progress()

# A part that rises: its node, the progress window it rises over, and
# the shader bounds to run the reveal between.
class RisingPart:
	var node: Polygon2D
	var start: float
	var end: float
	func local(p: float) -> float:
		if end <= start:
			return 1.0 if p >= end else 0.0
		return clampf((p - start) / (end - start), 0.0, 1.0)

var _rising: Array[RisingPart] = []
var _scaffold: Array[Polygon2D] = []
var _marks: Array[Polygon2D] = []
var _sign: Array[Polygon2D] = []
var _piles: Array[Node2D] = []
var _shadow: Polygon2D
var _apron: Polygon2D
var _textures: Dictionary = {}
var _flat_texture: Texture2D
var _built := false
var _age := 0.0
var _teardown_elapsed := -1.0
var _audio: AudioStreamPlayer2D
var _rng := RandomNumberGenerator.new()

# Layout, derived once per rebuild.
var _x0 := 0.0
var _y_back := 0.0
var _y_front := 0.0
var _W := 0.0
var _D := 0.0
var _H := 0.0
var _skew := 0.0
var _beam := 0.0
var _mark_height := 0.0


func _ready() -> void:
	_rng.seed = hash(str(SiteFootprint) + MaterialKind)
	_audio = AudioStreamPlayer2D.new()
	_audio.name = "Hits"
	_audio.max_distance = 1400.0
	add_child(_audio)
	_rebuild()


func _process(delta: float) -> void:
	_age += delta
	if not _built:
		return
	if _age < MarkGrowSeconds + delta:
		_apply_marks()
	if _teardown_elapsed >= 0.0:
		_teardown_elapsed += delta
		_apply_teardown()


func _rebuild_if_ready() -> void:
	if is_inside_tree():
		_rebuild()


# --- build -------------------------------------------------------------------

func _rebuild() -> void:
	for child in get_children():
		if child == _audio:
			continue
		remove_child(child)
		child.queue_free()
	_rising.clear()
	_scaffold.clear()
	_marks.clear()
	_sign.clear()
	_piles.clear()
	_built = false

	_W = SiteFootprint.x * CellSize.x
	_D = SiteFootprint.y * CellSize.y
	_x0 = -CellSize.x * 0.5
	_y_back = -CellSize.y * 0.5
	_y_front = _y_back + _D
	_H = CellSize.y * WallHeightCells
	_skew = _D * ObliqueSkew
	_beam = CellSize.x * BeamWidthCells
	_mark_height = CellSize.y * 0.3

	_build_apron()
	_build_shadow()
	_build_marks()
	_build_sign()
	_build_slab()
	if MaterialKind == "brick":
		_build_brick_courses()
	else:
		_build_frame()
		_build_walls(0.5, 0.8)
	_build_roof()
	_build_scaffold()

	_built = true
	_apply_piles()
	_apply_state()
	_apply_marks()
	_apply_progress()


func _build_apron() -> void:
	var a := CellSize.x * ApronCells
	_apron = _textured_quad("Apron", [
		Vector2(_x0 - a, _y_back - a), Vector2(_x0 + _W + _skew + a, _y_back - a),
		Vector2(_x0 + _W + _skew + a, _y_front + a), Vector2(_x0 - a, _y_front + a)
	], "dirt", CellSize.x, Color.WHITE, -2)


func _build_shadow() -> void:
	_shadow = _flat_polygon("Shadow", PackedVector2Array([
		Vector2(_x0 + _W, _y_front), Vector2(_x0 + _W + _skew, _y_back),
		Vector2(_x0 + _W + _skew, _y_back), Vector2(_x0 + _W, _y_front)
	]), Color(0, 0, 0, 0.22), -1)


func _build_marks() -> void:
	if MaterialKind == "steel":
		_build_blueprint()
		return
	# Four corner marks at the skewed slab's corners - Settlers III's
	# construction marks, Stardew's post-frame - grown in over the first
	# second and covered by the slab once it pours.
	var w := CellSize.x * 0.08
	for corner in [Vector2(_x0, _y_front), Vector2(_x0 + _W, _y_front),
			Vector2(_x0 + _W + _skew, _y_back), Vector2(_x0 + _skew, _y_back)]:
		var mark := _flat_polygon("Mark", PackedVector2Array([
			Vector2(corner.x - w * 0.5, corner.y - _mark_height), Vector2(corner.x + w * 0.5, corner.y - _mark_height),
			Vector2(corner.x + w * 0.5, corner.y), Vector2(corner.x - w * 0.5, corner.y)
		]), Color(0.62, 0.48, 0.30), 2)
		_marks.append(mark)


func _build_blueprint() -> void:
	# The industrial family's planned state is a blueprint of the plan on
	# the ground - the footprint outlined in cyan with a cell grid inside
	# (RimWorld's blueprint, Factorio's ghost, Satisfactory's hologram; the
	# modern-era counterpart of stakes and string). Flat geometry, no
	# texture; it fades in over MarkGrowSeconds like the marks do and is
	# covered by the slab once it pours.
	var line_w := CellSize.x * 0.03
	var col := Color(0.2, 0.8, 1.0, 0.7)
	var faint := Color(0.2, 0.8, 1.0, 0.35)
	var corners := [Vector2(_x0, _y_front), Vector2(_x0 + _W, _y_front),
		Vector2(_x0 + _W + _skew, _y_back), Vector2(_x0 + _skew, _y_back)]
	for i in range(4):
		_marks.append(_line_quad("PlanEdge%d" % i, corners[i], corners[(i + 1) % 4], line_w, col, 1))
	for i in range(1, SiteFootprint.x):
		var xa := _x0 + i * CellSize.x
		_marks.append(_line_quad("PlanCol%d" % i, Vector2(xa, _y_front), Vector2(xa + _skew, _y_back), line_w * 0.7, faint, 1))
	for j in range(1, SiteFootprint.y):
		var t := float(j) / float(SiteFootprint.y)
		var y := _y_front - _D * t
		_marks.append(_line_quad("PlanRow%d" % j, Vector2(_x0 + _skew * t, y), Vector2(_x0 + _W + _skew * t, y), line_w * 0.7, faint, 1))


func _line_quad(name: String, a: Vector2, b: Vector2, width: float, color: Color, z: int) -> Polygon2D:
	var d := (b - a).normalized()
	var n := Vector2(-d.y, d.x) * width * 0.5
	return _flat_polygon(name, PackedVector2Array([a + n, b + n, b - n, a - n]), color, z)


func _build_sign() -> void:
	# The site sign exists only while the site is planned and waiting -
	# Settlers II's Baustellenschild, Stardew's upgrade sign - and goes the
	# moment work starts.
	var a := CellSize.x * ApronCells
	var base := Vector2(_x0 - a * 0.7, _y_front + a * 0.6)
	var post_w := CellSize.x * 0.06
	var post_h := CellSize.y * 0.5
	var board_w := CellSize.x * 0.5
	var board_h := CellSize.y * 0.3
	_sign.append(_flat_polygon("SignPost", PackedVector2Array([
		Vector2(base.x - post_w * 0.5, base.y - post_h), Vector2(base.x + post_w * 0.5, base.y - post_h),
		Vector2(base.x + post_w * 0.5, base.y), Vector2(base.x - post_w * 0.5, base.y)
	]), Color(0.40, 0.30, 0.18), 3))
	_sign.append(_flat_polygon("SignBoard", PackedVector2Array([
		Vector2(base.x - board_w * 0.5, base.y - post_h - board_h), Vector2(base.x + board_w * 0.5, base.y - post_h - board_h),
		Vector2(base.x + board_w * 0.5, base.y - post_h + board_h * 0.1), Vector2(base.x - board_w * 0.5, base.y - post_h + board_h * 0.1)
	]), Color(0.86, 0.80, 0.62), 3))
	_sign.append(_flat_polygon("SignStripe", PackedVector2Array([
		Vector2(base.x - board_w * 0.42, base.y - post_h - board_h * 0.55), Vector2(base.x + board_w * 0.42, base.y - post_h - board_h * 0.55),
		Vector2(base.x + board_w * 0.42, base.y - post_h - board_h * 0.30), Vector2(base.x - board_w * 0.42, base.y - post_h - board_h * 0.30)
	]), Color(0.78, 0.30, 0.16), 3))


func _build_slab() -> void:
	var slab_h := CellSize.y * FoundationHeightCells
	var wet := Color(0.42, 0.42, 0.44)
	var wet_px := CellSize.y * 0.08
	# The slab pours back to front - its top is tiled in absolute local
	# space, so it rises up the screen, which on the ground plane reads as
	# the pour spreading from the back edge to the front.
	var top := _textured_quad("FoundationTop", [
		Vector2(_x0 + _skew, _y_back), Vector2(_x0 + _W + _skew, _y_back), Vector2(_x0 + _W, _y_front), Vector2(_x0, _y_front)
	], "concrete", CellSize.x * RoofSwatchCells, Color.WHITE, 0)
	_rise(top, 0.0, 0.10, 0.0, wet_px, wet)
	var front := _textured_quad("FoundationFront", [
		Vector2(_x0, _y_front), Vector2(_x0 + _W, _y_front), Vector2(_x0 + _W, _y_front + slab_h), Vector2(_x0, _y_front + slab_h)
	], "concrete", CellSize.x * RoofSwatchCells, Color(0.72, 0.72, 0.72), 0)
	_rise(front, 0.06, 0.10, 0.0, wet_px, wet)
	var right := _textured_quad("FoundationRight", [
		Vector2(_x0 + _W, _y_front), Vector2(_x0 + _W + _skew, _y_back),
		Vector2(_x0 + _W + _skew, _y_back + slab_h), Vector2(_x0 + _W, _y_front + slab_h)
	], "concrete", CellSize.x * RoofSwatchCells, Color(0.6, 0.6, 0.6), 0)
	_rise(right, 0.04, 0.10, 0.0, wet_px, wet)


func _build_frame() -> void:
	var family := _family()
	var beam_tex: String = family["beam"]
	var cols := SiteFootprint.x
	var parts: Array[Polygon2D] = []

	for i in range(cols + 1):
		parts.append(_post("PostFront%d" % i, _x0 + i * CellSize.x, _y_front, _H, beam_tex))
	parts.append(_post("PostBackRight", _x0 + _W + _skew, _y_back, _H, beam_tex))
	parts.append(_hbeam("BaseFront", _x0, _x0 + _W, _y_front - _beam, beam_tex))
	parts.append(_hbeam("TopFront", _x0, _x0 + _W, _y_front - _H, beam_tex))
	parts.append(_skew_beam("BaseRight", _x0 + _W, _y_front - _beam, beam_tex))
	parts.append(_skew_beam("TopRight", _x0 + _W, _y_front - _H, beam_tex))
	parts.append(_hbeam("MidFront", _x0, _x0 + _W, _y_front - _H * 0.5, beam_tex))
	parts.append(_hbeam("TopBack", _x0 + _skew, _x0 + _W + _skew, _y_back - _H, beam_tex))
	parts.append(_skew_beam("TopLeft", _x0, _y_front - _H, beam_tex))

	# One after another, in work order, each overlapping the next a little
	# - Manor Lords' beams popping in phases, Settlers II's skeleton.
	_stagger(parts, 0.10, 0.50, 1.5)


func _build_walls(from: float, to: float) -> void:
	var family := _family()
	var wall_tex: String = family["wall"]
	var wall_px := CellSize.x * WallSwatchCells
	var rows: int = family["rows"]
	var cut: Color = family["cut"]
	var parts: Array[Polygon2D] = []
	var cols := SiteFootprint.x
	# Front wall as one quad per footprint column, all sharing absolute
	# UVs so they stay one continuous surface - the columns are what let
	# the work run left to right.
	for i in range(cols):
		var xa := _x0 + i * CellSize.x
		var xb := xa + CellSize.x
		parts.append(_textured_quad("WallFront%d" % i, [
			Vector2(xa, _y_front - _H), Vector2(xb, _y_front - _H), Vector2(xb, _y_front), Vector2(xa, _y_front)
		], wall_tex, wall_px, Color.WHITE, 1))
	parts.append(_right_wall("WallRight", _H, wall_tex, wall_px))

	var row_px := wall_px / rows if rows > 0 else 0.0
	var n := parts.size()
	var slot := (to - from) / n
	for i in range(n):
		# Columns in work order, each overlapping the next, with a little
		# deterministic jitter so the top of the work is uneven across the
		# face - a course further along here, one behind there - instead of
		# a lockstep staircase. Row quantisation keeps every column on a
		# course line regardless.
		var jitter := (_rng.randf() - 0.5) * slot * 0.6
		var start := clampf(from + i * slot + jitter, from, to - slot * 0.5)
		var end := minf(to, start + slot * 1.5)
		_rise(parts[i], start, end, row_px, CellSize.y * 0.09, cut)


func _build_brick_courses() -> void:
	# Brick has no frame: the courses ARE the structure, a course at a time.
	_build_walls(0.10, 0.80)


func _build_roof() -> void:
	var family := _family()
	var roof_tex: String = family["roof"]
	var roof_px := CellSize.x * RoofSwatchCells
	var rows: int = family["roof_rows"]
	# Tiled in the roof's OWN space: u along the eave, v along the depth
	# edge, so the courses follow the skewed face and the reveal sweeps
	# from the eave to the back - real shingling order.
	var pts := PackedVector2Array([
		Vector2(_x0, _y_front - _H), Vector2(_x0 + _W, _y_front - _H),
		Vector2(_x0 + _W + _skew, _y_back - _H), Vector2(_x0 + _skew, _y_back - _H)
	])
	var depth_len := Vector2(_skew, _D).length()
	var ts := _tex_size(roof_tex)
	var su := ts.x / roof_px
	var sv := ts.y / roof_px
	var uvs := PackedVector2Array([
		Vector2(0, 0), Vector2(_W * su, 0),
		Vector2(_W * su, -depth_len * sv), Vector2(0, -depth_len * sv)
	])
	var roof := _polygon("Roof", pts, uvs, _texture(roof_tex), Color.WHITE, 10)
	var row_px := roof_px / rows if rows > 0 else 0.0
	_rise_custom(roof, 0.80, 1.0, 0.0, -depth_len, roof_px, 0, row_px, 0.0, Color.WHITE)


func _build_scaffold() -> void:
	# Poles at every cell boundary a little outside the front and right
	# walls, taller than the wall so the silhouette changes shape; ledgers
	# at two lifts; a brace per bay. Flat vertex-coloured geometry - a
	# 10px pole is mush with a tiled texture on it. Present from the frame
	# on, sunk back into the ground when the site completes (Cities:
	# Skylines' teardown, Factorio's scaffold-down).
	var m := CellSize.x * 0.15
	var pole_w := CellSize.x * 0.07
	var Hs := _H + CellSize.y * ScaffoldExtraCells
	var wood := MaterialKind != "steel"
	var pole_c := Color(0.62, 0.48, 0.30) if wood else Color(0.55, 0.58, 0.62)
	var ledger_c := Color(0.70, 0.56, 0.36) if wood else Color(0.66, 0.69, 0.73)
	var cols := SiteFootprint.x
	var rows := SiteFootprint.y

	var front_x: Array[float] = []
	for i in range(cols + 1):
		front_x.append(_x0 + i * CellSize.x)
	var y_pole := _y_front + m
	for x in front_x:
		_scaffold.append(_flat_polygon("ScaffoldPole", PackedVector2Array([
			Vector2(x - pole_w * 0.5, y_pole - Hs), Vector2(x + pole_w * 0.5, y_pole - Hs),
			Vector2(x + pole_w * 0.5, y_pole), Vector2(x - pole_w * 0.5, y_pole)
		]), pole_c, 7))
	for j in range(1, rows + 1):
		var t := float(j) / float(rows)
		var x := _x0 + _W + m + _skew * t
		var y := _y_front - _D * t + m * (1.0 - t)
		_scaffold.append(_flat_polygon("ScaffoldPoleRight", PackedVector2Array([
			Vector2(x - pole_w * 0.5, y - Hs), Vector2(x + pole_w * 0.5, y - Hs),
			Vector2(x + pole_w * 0.5, y), Vector2(x - pole_w * 0.5, y)
		]), pole_c, 7))
	for k in range(1, 3):
		var lift := _H * 0.5 * k
		var y := y_pole - lift
		_scaffold.append(_flat_polygon("Ledger", PackedVector2Array([
			Vector2(_x0 - pole_w, y - pole_w * 0.6), Vector2(_x0 + _W + m + pole_w, y - pole_w * 0.6),
			Vector2(_x0 + _W + m + pole_w, y + pole_w * 0.6), Vector2(_x0 - pole_w, y + pole_w * 0.6)
		]), ledger_c, 6))
		_scaffold.append(_flat_polygon("LedgerRight", PackedVector2Array([
			Vector2(_x0 + _W + m, y - pole_w * 0.6), Vector2(_x0 + _W + m + _skew, _y_back - lift - pole_w * 0.6),
			Vector2(_x0 + _W + m + _skew, _y_back - lift + pole_w * 0.6), Vector2(_x0 + _W + m, y + pole_w * 0.6)
		]), ledger_c.darkened(0.15), 6))
	for i in range(cols):
		var xa := front_x[i]
		var xb := front_x[i + 1]
		var ya := y_pole - pole_w
		var yb := y_pole - _H * 0.5 + pole_w
		_scaffold.append(_flat_polygon("Brace", PackedVector2Array([
			Vector2(xa, ya - pole_w * 0.4), Vector2(xb, yb - pole_w * 0.4),
			Vector2(xb, yb + pole_w * 0.4), Vector2(xa, ya + pole_w * 0.4)
		]), pole_c.darkened(0.2), 6))

	for pole in _scaffold:
		_attach_reveal(pole, 1.0, 0, 0.0, 0.0, Color.WHITE)
		var b := _bounds(pole.polygon)
		var mat := pole.material as ShaderMaterial
		mat.set_shader_parameter("reveal_bottom", b.end.y)
		mat.set_shader_parameter("reveal_top", b.position.y)
		mat.set_shader_parameter("reveal_progress", 1.0)


# --- part builders -----------------------------------------------------------

func _post(name: String, x: float, y_bottom: float, H: float, tex: String) -> Polygon2D:
	# The beam swatch turned on end: its length runs up the post. UV.x is
	# the length axis, so the reveal runs on UV.x (v_axis 1).
	var pts := PackedVector2Array([
		Vector2(x - _beam * 0.5, y_bottom - H), Vector2(x + _beam * 0.5, y_bottom - H),
		Vector2(x + _beam * 0.5, y_bottom), Vector2(x - _beam * 0.5, y_bottom)
	])
	var ts := _tex_size(tex)
	var stretch := _beam * 4.0
	var uvs := PackedVector2Array()
	for v in pts:
		uvs.append(Vector2(v.y / stretch * ts.x, (v.x - (x - _beam * 0.5)) / _beam * ts.y))
	var p := _polygon(name, pts, uvs, _texture(tex), Color.WHITE, 5)
	p.set_meta("v_axis", 1)
	p.set_meta("v_scale", stretch)
	return p


func _hbeam(name: String, x_from: float, x_to: float, y_top: float, tex: String) -> Polygon2D:
	var pts := PackedVector2Array([
		Vector2(x_from, y_top), Vector2(x_to, y_top), Vector2(x_to, y_top + _beam), Vector2(x_from, y_top + _beam)
	])
	var ts := _tex_size(tex)
	var stretch := _beam * 4.0
	var uvs := PackedVector2Array()
	for v in pts:
		uvs.append(Vector2(v.x / stretch * ts.x, (v.y - y_top) / _beam * ts.y))
	var p := _polygon(name, pts, uvs, _texture(tex), Color.WHITE, 5)
	# A horizontal beam is laid left to right: reveal along its length.
	p.set_meta("v_axis", 1)
	p.set_meta("v_scale", -stretch)
	return p


func _skew_beam(name: String, x: float, y_top: float, tex: String) -> Polygon2D:
	# A beam running along the right wall's depth: front end at (x, y_top),
	# back end shifted by (skew, -D). Shaded a little darker, as a side.
	var pts := PackedVector2Array([
		Vector2(x, y_top), Vector2(x + _skew, y_top - _D),
		Vector2(x + _skew, y_top - _D + _beam), Vector2(x, y_top + _beam)
	])
	var ts := _tex_size(tex)
	var length := Vector2(_skew, _D).length()
	var stretch := _beam * 4.0
	var u_end := length / stretch * ts.x
	var uvs := PackedVector2Array([Vector2(0, 0), Vector2(u_end, 0), Vector2(u_end, ts.y), Vector2(0, ts.y)])
	var p := _polygon(name, pts, uvs, _texture(tex), Color(0.82, 0.82, 0.82), 5)
	# Front end first: negative scale so the front is v = 0 and the back
	# v = -length - bounds along the beam, not the polygon's screen box.
	p.set_meta("v_axis", 1)
	p.set_meta("v_scale", -stretch)
	p.set_meta("v_bottom", 0.0)
	p.set_meta("v_top", -length)
	return p


func _right_wall(name: String, h: float, tex: String, wall_px: float) -> Polygon2D:
	# The side face is a parallelogram; its swatch runs along the depth
	# edge and up the wall, so the texture (and the reveal) follow the skew.
	var pts := PackedVector2Array([
		Vector2(_x0 + _W, _y_front - h), Vector2(_x0 + _W + _skew, _y_back - h),
		Vector2(_x0 + _W + _skew, _y_back), Vector2(_x0 + _W, _y_front)
	])
	var side_len := Vector2(_skew, _D).length()
	var ts := _tex_size(tex)
	var su := ts.x / wall_px
	var sv := ts.y / wall_px
	var uvs := PackedVector2Array([
		Vector2(0, -h * sv), Vector2(side_len * su, -h * sv), Vector2(side_len * su, 0), Vector2(0, 0)
	])
	var p := _polygon(name, pts, uvs, _texture(tex), Color(0.78, 0.78, 0.78), 0)
	p.set_meta("v_scale", wall_px)
	p.set_meta("v_bottom", 0.0)
	p.set_meta("v_top", -h)
	return p


func _textured_quad(name: String, pts: Array, tex: String, world_px: float, tint: Color, z: int) -> Polygon2D:
	# Swatch repeats every world_px pixels on both axes, from absolute
	# local coordinates, so adjacent parts sharing a surface line up.
	var packed := PackedVector2Array(pts)
	var ts := _tex_size(tex)
	var uvs := PackedVector2Array()
	for v in packed:
		uvs.append(Vector2(v.x / world_px * ts.x, v.y / world_px * ts.y))
	var p := _polygon(name, packed, uvs, _texture(tex), tint, z)
	p.set_meta("v_scale", world_px)
	return p


func _flat_polygon(name: String, pts: PackedVector2Array, color: Color, z: int) -> Polygon2D:
	# Untextured geometry still carries pixel UVs on a white texture, so the
	# same reveal shader can run over it in local space (v_scale 1).
	var p := _polygon(name, pts, pts, _flat(), color, z)
	p.set_meta("v_scale", 1.0)
	return p


func _polygon(name: String, pts: PackedVector2Array, uvs: PackedVector2Array, tex: Texture2D, tint: Color, z: int) -> Polygon2D:
	var p := Polygon2D.new()
	p.name = name
	p.polygon = pts
	p.uv = uvs
	p.texture = tex
	p.texture_repeat = CanvasItem.TEXTURE_REPEAT_ENABLED
	p.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
	p.color = tint
	p.z_index = z
	add_child(p)
	return p


# --- reveal wiring -----------------------------------------------------------

func _rise(part: Polygon2D, start: float, end: float, row_px: float, cut_px: float, cut: Color) -> void:
	# Bounds along the part's own v axis, from the metadata its builder left.
	var v_scale: float = part.get_meta("v_scale", 1.0)
	var v_axis: int = part.get_meta("v_axis", 0)
	var bottom: float
	var top: float
	if part.has_meta("v_bottom"):
		bottom = part.get_meta("v_bottom")
		top = part.get_meta("v_top")
	else:
		var b := _bounds(part.polygon)
		if v_axis == 1:
			# Length axis: v = local x * sign(v_scale).
			if v_scale < 0.0:
				bottom = -b.position.x
				top = -b.end.x
			else:
				bottom = b.end.y
				top = b.position.y
		else:
			bottom = b.end.y
			top = b.position.y
	# The sign of v_scale is the axis direction the bounds were computed
	# for; it must reach the shader as is.
	_rise_custom(part, start, end, bottom, top, v_scale, v_axis, row_px, cut_px, cut)


func _rise_custom(part: Polygon2D, start: float, end: float, bottom: float, top: float,
		v_scale: float, v_axis: int, row_px: float, cut_px: float, cut: Color) -> void:
	_attach_reveal(part, v_scale, v_axis, row_px, cut_px, cut)
	var mat := part.material as ShaderMaterial
	mat.set_shader_parameter("reveal_bottom", bottom)
	mat.set_shader_parameter("reveal_top", top)
	mat.set_shader_parameter("reveal_progress", 0.0)
	var rp := RisingPart.new()
	rp.node = part
	rp.start = start
	rp.end = end
	_rising.append(rp)


func _attach_reveal(part: Polygon2D, v_scale: float, v_axis: int, row_px: float, cut_px: float, cut: Color) -> void:
	var mat := ShaderMaterial.new()
	mat.shader = REVEAL_SHADER
	mat.set_shader_parameter("v_scale", v_scale)
	mat.set_shader_parameter("v_axis", v_axis)
	mat.set_shader_parameter("row_v", row_px)
	mat.set_shader_parameter("cut_v", cut_px)
	mat.set_shader_parameter("cut_color", cut)
	part.material = mat


func _stagger(parts: Array[Polygon2D], from: float, to: float, overlap: float) -> void:
	var n := parts.size()
	if n == 0:
		return
	var slot := (to - from) / n
	var cut: Color = _family()["frame_cut"]
	for i in range(n):
		var start := from + i * slot
		var end := minf(to, start + slot * overlap)
		_rise(parts[i], start, end, 0.0, CellSize.y * 0.06, cut)


# --- applying the facts -------------------------------------------------------

func _apply_progress() -> void:
	if not _built:
		return
	var p := BuildProgress
	for rp in _rising:
		if is_instance_valid(rp.node):
			(rp.node.material as ShaderMaterial).set_shader_parameter("reveal_progress", rp.local(p))
	# The scaffold goes up when the frame starts and comes down at the end.
	var scaffold_up := p >= 0.10 and SiteState != "pending"
	for s in _scaffold:
		if is_instance_valid(s):
			s.visible = scaffold_up
	_apply_marks()
	# Ground shadow grows with what is built.
	if is_instance_valid(_shadow):
		var built_h := _H * clampf((p - 0.10) / 0.70, 0.0, 1.0)
		var sh := built_h * 0.35
		_shadow.polygon = PackedVector2Array([
			Vector2(_x0 + _W, _y_front), Vector2(_x0 + _W + _skew, _y_back),
			Vector2(_x0 + _W + _skew + sh, _y_back), Vector2(_x0 + _W + sh, _y_front)
		])
		_shadow.visible = sh > 0.5
	_apply_piles()


func _apply_state() -> void:
	if not _built:
		return
	var planned := SiteState == "pending" or SiteState == "queued"
	for s in _sign:
		if is_instance_valid(s):
			s.visible = planned
	_apply_progress()


func _apply_marks() -> void:
	if not _built:
		return
	# Marks grow in over the first second and are covered once the slab pours.
	var grow := clampf(_age / maxf(0.01, MarkGrowSeconds), 0.0, 1.0)
	var show := BuildProgress < 0.02
	if MaterialKind == "steel":
		# The blueprint draws itself in rather than growing up.
		for m in _marks:
			if is_instance_valid(m):
				m.visible = show
				m.modulate = Color(1, 1, 1, grow)
		return
	for m in _marks:
		if not is_instance_valid(m):
			continue
		m.visible = show
		var b := _bounds(m.polygon)
		var cx := (b.position.x + b.end.x) * 0.5
		var w := b.size.x
		var bottom := b.end.y
		var h := _mark_height * grow
		m.polygon = PackedVector2Array([
			Vector2(cx - w * 0.5, bottom - h), Vector2(cx + w * 0.5, bottom - h),
			Vector2(cx + w * 0.5, bottom), Vector2(cx - w * 0.5, bottom)
		])


func _apply_teardown() -> void:
	var t := clampf(_teardown_elapsed / maxf(0.01, ScaffoldTeardownSeconds), 0.0, 1.0)
	for s in _scaffold:
		if is_instance_valid(s):
			(s.material as ShaderMaterial).set_shader_parameter("reveal_progress", 1.0 - t)
	for pile in _piles:
		if is_instance_valid(pile):
			pile.visible = false
	if is_instance_valid(_apron):
		_apron.modulate = Color(1, 1, 1, 1.0 - t)


var _pile_signature := ""

func _apply_piles() -> void:
	if not _built:
		return
	# Rebuilt only when what would be drawn changes - a layer count per
	# pile - not on every progress push.
	var layers_per_pile: Array[int] = []
	var n := mini(4, SiteMaterials.size())
	if SiteState != "complete":
		for i in range(n):
			var entry: Dictionary = SiteMaterials[i]
			var required := maxi(1, int(entry.get("required", 1)))
			var delivered := int(entry.get("delivered", 0))
			var fraction := clampf(float(delivered) / float(required), 0.0, 1.0)
			if SiteState == "working":
				# The stock was built into the structure when the job
				# started; what is left on the ground is what the work has
				# not used yet.
				fraction = 1.0 - BuildProgress
			layers_per_pile.append(int(ceil(fraction * 4.0)))
	var signature := str(layers_per_pile)
	if signature == _pile_signature:
		return
	_pile_signature = signature

	for pile in _piles:
		if is_instance_valid(pile):
			remove_child(pile)
			pile.queue_free()
	_piles.clear()
	if SiteState == "complete":
		return
	var a := CellSize.x * ApronCells
	# Pile spots on the apron corners, clear of the approach cell in front
	# of the site's middle: front-left, front-right, then beside each.
	var spots := [
		Vector2(_x0 - a * 0.5 + CellSize.x * 0.45, _y_front + a * 0.55),
		Vector2(_x0 + _W - CellSize.x * 0.45, _y_front + a * 0.55),
		Vector2(_x0 - a * 0.5 + CellSize.x * 0.45, _y_front - CellSize.y * 0.6),
		Vector2(_x0 + _W + _skew + a * 0.4, _y_back + CellSize.y * 0.4),
	]
	for i in range(n):
		var entry: Dictionary = SiteMaterials[i]
		var layers: int = layers_per_pile[i]
		if layers <= 0:
			continue
		var pile := Node2D.new()
		pile.name = "Pile%d" % i
		pile.position = spots[i]
		pile.z_index = 2
		add_child(pile)
		_piles.append(pile)
		var tex := _pile_swatch(str(entry.get("id", "")))
		var w := CellSize.x * 0.62
		var h := CellSize.y * 0.13
		for k in range(layers):
			var y_top := -k * h * 0.85
			var inset := k * w * 0.06
			var quad := Polygon2D.new()
			quad.name = "Layer%d" % k
			var pts := PackedVector2Array([
				Vector2(-w * 0.5 + inset, y_top - h), Vector2(w * 0.5 - inset, y_top - h),
				Vector2(w * 0.5 - inset, y_top), Vector2(-w * 0.5 + inset, y_top)
			])
			quad.polygon = pts
			var ts := _tex_size(tex)
			var uvs := PackedVector2Array()
			for v in pts:
				uvs.append(Vector2(v.x / (CellSize.x * 0.5) * ts.x, v.y / (CellSize.x * 0.5) * ts.y))
			quad.uv = uvs
			quad.texture = _texture(tex)
			quad.texture_repeat = CanvasItem.TEXTURE_REPEAT_ENABLED
			quad.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
			quad.color = Color(1, 1, 1).darkened(0.08 * (layers - 1 - k))
			pile.add_child(quad)


# --- work ----------------------------------------------------------------------

func WorkPulse() -> void:
	if not _built:
		return
	var at := _contact_point()
	var scene: PackedScene = SPARKS_SCENE if MaterialKind == "steel" else DUST_SCENE
	var fx: GPUParticles2D = scene.instantiate()
	fx.position = at
	fx.z_index = 9
	fx.one_shot = true
	if MaterialKind == "wood":
		fx.modulate = Color(0.95, 0.85, 0.62)
	add_child(fx)
	fx.emitting = true
	fx.finished.connect(fx.queue_free)
	_play_hit()


func _contact_point() -> Vector2:
	var p := BuildProgress
	for rp in _rising:
		if not is_instance_valid(rp.node):
			continue
		var l := rp.local(p)
		if l > 0.0 and l < 1.0:
			var b := _bounds(rp.node.polygon)
			return Vector2((b.position.x + b.end.x) * 0.5, b.end.y - b.size.y * l)
	return Vector2(_x0 + _W * 0.5, _y_front - _H * clampf((p - 0.1) / 0.7, 0.0, 1.0))


func _play_hit() -> void:
	if not is_instance_valid(_audio):
		return
	var clips := HitSounds
	if clips.is_empty():
		clips = _default_hit_sounds()
	if clips.is_empty():
		return
	_audio.stream = clips[_rng.randi_range(0, clips.size() - 1)]
	_audio.pitch_scale = _rng.randf_range(0.92, 1.08) * (0.8 if MaterialKind == "brick" else 1.0)
	_audio.play()


var _default_hits: Array[AudioStream] = []

func _default_hit_sounds() -> Array[AudioStream]:
	if not _default_hits.is_empty():
		return _default_hits
	var prefix := "hit_metal_" if MaterialKind == "steel" else "hit_"
	for i in range(5):
		var path := AUDIO_DIR + "%s%03d.ogg" % [prefix, i]
		var stream := load(path) as AudioStream
		if stream != null:
			_default_hits.append(stream)
	return _default_hits


# --- swatches ------------------------------------------------------------------

func _family() -> Dictionary:
	match MaterialKind:
		"steel":
			# The newest steel is hot: the band at the work line glows weld-
			# orange rather than showing wet mortar or raw timber.
			return {"wall": "steel_panel", "beam": "steel_beam", "roof": "roof_metal",
				"rows": 1, "roof_rows": 1, "cut": Color(1.0, 0.72, 0.35), "frame_cut": Color(1.0, 0.80, 0.50)}
		"brick":
			return {"wall": "brick", "beam": "wood_beam", "roof": "roof_tile",
				"rows": 4, "roof_rows": 4, "cut": Color(0.74, 0.70, 0.64), "frame_cut": Color(0.88, 0.76, 0.50)}
		_:
			return {"wall": "wood_planks", "beam": "wood_beam", "roof": "roof_shingles",
				"rows": 4, "roof_rows": 4, "cut": Color(0.82, 0.66, 0.38), "frame_cut": Color(0.90, 0.78, 0.52)}


func _pile_swatch(resource_id: String) -> String:
	var id := resource_id.to_lower()
	if id.contains("brick") or id.contains("stone") or id.contains("clay"):
		return "brick"
	if id.contains("steel") or id.contains("iron") or id.contains("metal"):
		return "steel_beam"
	if id.contains("wood") or id.contains("plank") or id.contains("log") or id.contains("timber"):
		return "wood_beam"
	if id.contains("sand") or id.contains("gravel") or id.contains("concrete") or id.contains("cement"):
		return "concrete"
	return _family()["beam"]


func _texture(swatch: String) -> Texture2D:
	if _textures.has(swatch):
		return _textures[swatch]
	# Loaded through the import pipeline like any other texture - an
	# exported game ships the imported .ctex, not the PNG. Mipmaps come
	# from the swatch's .import settings; a tiled swatch shimmers when
	# minified without them.
	var tex := load(SWATCH_DIR + swatch + ".png") as Texture2D
	if tex == null:
		push_error("construction_stage: could not load swatch '%s'." % swatch)
		return _flat()
	_textures[swatch] = tex
	return tex


func _flat() -> Texture2D:
	# 1x1, so a Polygon2D's pixel UVs normalise to pixels and the reveal's
	# v_scale of 1 means "local pixels".
	if _flat_texture == null:
		var img := Image.create(1, 1, false, Image.FORMAT_RGBA8)
		img.fill(Color.WHITE)
		_flat_texture = ImageTexture.create_from_image(img)
	return _flat_texture


func _tex_size(swatch: String) -> Vector2:
	var tex := _texture(swatch)
	return tex.get_size() if tex != null else Vector2(256, 256)


static func _bounds(pts: PackedVector2Array) -> Rect2:
	if pts.is_empty():
		return Rect2()
	var r := Rect2(pts[0], Vector2.ZERO)
	for v in pts:
		r = r.expand(v)
	return r
