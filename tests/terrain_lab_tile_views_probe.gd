extends SceneTree

# Drives terrain_generator_lab.tscn the way F6 does: the scene's own map settings,
# its own first generation, and the View / Map size / Generate controls. Asserts on
# what each tile view actually drew, never on counts that stale output also passes.

const LAB := "res://addons/beep_game_builder_cs/templates/scenes/terrain/terrain_generator_lab.tscn"
const PACK_SCRIPT := "res://addons/beep_game_builder_cs/ecs/terrain/TerrainLibraryPack.cs"
const OUTPUT := "res://tests/output/lab_tile_views"
const VIEW_PATH := "HUD/Settings/Scroll/Controls/ViewRow/View"
const SIZE_PATH := "HUD/Settings/Scroll/Controls/MapSizeRow/MapSize"
const GENERATE_PATH := "HUD/Settings/Scroll/Controls/Actions/Generate"
const SEED_PATH := "HUD/Settings/Scroll/Controls/SeedRow/Seed"
# Fixed seeds keep every run on the same maps; Random seed would make the log checks flaky.
const REGENERATION_SEEDS := {1: 27182, 3: 16180}
const STATUS_PATH := "HUD/Status"
const BIOME_DISPLAY_META := "_terrain_tile_biome_display"
const OVERLAY_PATHS := ["Preview/Features", "Preview/RockObjects", "Preview/MapOverlay", "Preview/IsoFeatures",
	"Preview/Diagnostics/Resources"]

const GAME_TILES := 1
const ISOMETRIC_TILES := 3
const TINY := 0
const LARGE := 3

# Screen area the preview can use without the settings panel, title and status line.
const HUD_RIGHT := 390.0
const TOP_MARGIN := 40.0
const BOTTOM_MARGIN := 80.0
const DRAWN_COVERAGE := 0.35
const EMPTY_COVERAGE := 0.08
const GENERATION_TIMEOUT_MS := 180000
# Below the gate's 600 s timeout: the gate can only kill the launcher, so the probe must quit itself.
const WATCHDOG_SECONDS := 540.0

const ERROR_TYPE_WARNING := 1


class ProbeLogger extends Logger:
	var _mutex := Mutex.new()
	var _errors: Array[String] = []
	var _warnings: Array[String] = []

	func _log_error(function: String, file: String, line: int, code: String, rationale: String,
			editor_notify: bool, error_type: int, script_backtraces: Array[ScriptBacktrace]) -> void:
		var text := "%s %s (%s:%d)" % [code, rationale, file, line]
		_mutex.lock()
		if error_type == ERROR_TYPE_WARNING:
			_warnings.append(text)
		else:
			_errors.append(text)
		_mutex.unlock()

	func _log_message(message: String, error: bool) -> void:
		if not error:
			return
		_mutex.lock()
		_errors.append(message.strip_edges())
		_mutex.unlock()

	func take() -> Dictionary:
		_mutex.lock()
		var entries := {"errors": _errors.duplicate(), "warnings": _warnings.duplicate()}
		_errors.clear()
		_warnings.clear()
		_mutex.unlock()
		return entries


var _logger := ProbeLogger.new()
var _failures: Array[String] = []
var _stage := "start"
var _scene: Node
var _world: Node
var _generation_done := false
var _generation_success := false
var _generation_message := ""
var _extents := {}


func _initialize() -> void:
	OS.add_logger(_logger)
	create_timer(WATCHDOG_SECONDS, true, false, true).timeout.connect(_on_watchdog)
	call_deferred("run")


func _on_watchdog() -> void:
	print("FAIL: watchdog fired during '%s'" % _stage)
	OS.remove_logger(_logger)
	quit(2)


func run() -> void:
	root.size = Vector2i(1280, 800)
	if not check(DisplayServer.get_name() != "headless", "this probe measures rendered output; run it with rendering"):
		finish()
		return
	DirAccess.make_dir_recursive_absolute(OUTPUT)

	await pack_assigned_before_first_draw()
	if await open_lab():
		await default_view_cycle()
		await regenerate_in_tile_views()
		await unusable_packs()
		await shrinking_maps()
		await close_lab()
	finish()


func finish() -> void:
	_stage = "finish"
	check_log(true)
	OS.remove_logger(_logger)
	if _failures.is_empty():
		print("[terrain-lab-tile-views] OK")
		quit(0)
	else:
		print("[terrain-lab-tile-views] %d check(s) did not hold" % _failures.size())
		quit(1)


func check(condition: bool, message: String) -> bool:
	if not condition:
		_failures.append("%s: %s" % [_stage, message])
		print("FAIL: %s: %s" % [_stage, message])
	return condition


# ---- scenarios ---------------------------------------------------------------

## A pack set in the Inspector before F6: the view is drawn for the first time with it.
## Isometric tiles gets a pack that fails validation, so its layer has never had a TileSet.
func pack_assigned_before_first_draw() -> void:
	_stage = "S0 open with packs"
	var iso_pack := make_pack("probe_wrong_projection", GAME_TILES, ISOMETRIC_TILES)
	if not await open_lab(incomplete_pack(GAME_TILES), iso_pack):
		await close_lab()
		return
	var labels := {ISOMETRIC_TILES: "a wrong-projection pack", GAME_TILES: "an incomplete pack"}
	for view in [ISOMETRIC_TILES, GAME_TILES]:
		begin_step()
		await select_view(view)
		await check_failed_view(view, "S0 %s first drawn with %s" % [view_name(view), labels[view]], "View incomplete:", {})
	await close_lab()


func default_view_cycle() -> void:
	for view in [0, 1, 2, 3, 4, 5, 1, 3]:
		_stage = "S1 view %d" % view
		begin_step()
		await select_view(view)
		if view == GAME_TILES or view == ISOMETRIC_TILES:
			await check_drawn_view(view, "S1 %s" % view_name(view))
		else:
			await capture("S1 view %d" % view)
			check_log(true)


func regenerate_in_tile_views() -> void:
	for view in [GAME_TILES, ISOMETRIC_TILES]:
		_stage = "S3 regenerate in %s" % view_name(view)
		await select_view(view)
		begin_step()
		(_scene.get_node(SEED_PATH) as SpinBox).value = REGENERATION_SEEDS[view]
		if await press_and_wait(GENERATE_PATH):
			check(_generation_success, "generation failed: %s" % _generation_message)
		await check_drawn_view(view, "S3 %s regenerated" % view_name(view))


func unusable_packs() -> void:
	for view in [GAME_TILES, ISOMETRIC_TILES]:
		var renderer := renderer_for(view)
		var wrong_projection := GAME_TILES if view == ISOMETRIC_TILES else ISOMETRIC_TILES
		var packs := {
			"wrong-projection pack": make_pack("probe_wrong_projection", wrong_projection, view),
			"incomplete pack": incomplete_pack(view),
		}
		for label in packs:
			var name := "S4 %s %s" % [view_name(view), label]
			_stage = name
			await select_view(view)
			var published := terrain_snapshot(view)
			begin_step()
			renderer.set("LibraryPack", packs[label])
			await select_view(view)
			await check_failed_view(view, "%s redraw" % name, "View incomplete:", published)

			_stage = "%s generate" % name
			begin_step()
			if await press_and_wait(GENERATE_PATH):
				check(not _generation_success, "generation reported success although the view could not draw")
			await check_failed_view(view, "%s generate" % name, "", published)

			_stage = "%s removed" % name
			renderer.set("LibraryPack", null)
			begin_step()
			await select_view(view)
			await check_drawn_view(view, "%s removed" % name)


func shrinking_maps() -> void:
	_stage = "S2 game tiles large"
	await select_view(GAME_TILES)
	await select_size_and_wait(LARGE)
	await check_drawn_view(GAME_TILES, "S2 game tiles large")

	_stage = "S2 game tiles shrunk"
	await select_size_and_wait(TINY)
	await check_drawn_view(GAME_TILES, "S2 game tiles shrunk to tiny")

	_stage = "S2 shrink while original"
	await select_size_and_wait(LARGE)
	await check_drawn_view(GAME_TILES, "S2 game tiles large again")
	await select_view(0)
	await select_size_and_wait(TINY)
	begin_step()
	await select_view(GAME_TILES)
	await check_drawn_view(GAME_TILES, "S2 game tiles after shrinking in original")

	_stage = "S2 isometric tiles"
	await select_view(ISOMETRIC_TILES)
	await select_size_and_wait(LARGE)
	await check_drawn_view(ISOMETRIC_TILES, "S2 isometric tiles large")
	await select_size_and_wait(TINY)
	await check_drawn_view(ISOMETRIC_TILES, "S2 isometric tiles shrunk to tiny")


# ---- checks ------------------------------------------------------------------

func check_drawn_view(view: int, label: String) -> void:
	_stage = label
	var renderer := renderer_for(view)
	var built: Vector2i = _world.get("BuiltSize")
	var origin: Vector2i = renderer.get("BoundsOrigin")
	var report := view_report(renderer)
	if not report.is_empty():
		check(bool(report.get("valid", false)), "view report is not valid: %s" % report)
	check(not status_text().contains("View incomplete"), "status reports an incomplete view: '%s'" % status_text())

	if view == GAME_TILES:
		var layers := biome_layers(renderer)
		check(layers.size() > 0, "no biome layers were drawn")
		var legal := Rect2i(origin, built + Vector2i.ONE)
		for layer in layers:
			check(layer.tile_set != null, "%s has no TileSet" % layer.name)
			var outside := cells_outside(layer, legal)
			check(outside == 0, "%s keeps %d cells outside the %s map" % [layer.name, outside, built])
		var water := renderer.get_node_or_null("TileWater") as TileMapLayer
		if check(water != null, "TileWater is missing"):
			check(cells_outside(water, Rect2i(Vector2i.ZERO, built)) == 0, "TileWater keeps cells outside the map")
	else:
		var layer := renderer.get_node_or_null("IsoTerrain") as TileMapLayer
		if check(layer != null, "IsoTerrain is missing"):
			check(layer.tile_set != null, "IsoTerrain has no TileSet")
			var painted := layer.get_used_cells().size()
			check(painted == built.x * built.y, "IsoTerrain painted %d of %d cells" % [painted, built.x * built.y])
			check(cells_outside(layer, Rect2i(origin, built)) == 0, "IsoTerrain keeps cells outside the map")

	check_grid_binding()
	var extent := preview_extent()
	var coverage := await terrain_coverage(label, extent)
	if coverage >= 0.0:
		check(coverage >= DRAWN_COVERAGE, "terrain covers %.2f of the map area; expected at least %.2f" % [coverage, DRAWN_COVERAGE])
	_extents[view] = extent
	check_log(false)


## A failed build reports why, publishes nothing, and keeps the terrain it last published.
func check_failed_view(view: int, label: String, status_marker: String, published: Dictionary) -> void:
	_stage = label
	var renderer := renderer_for(view)
	var report := view_report(renderer)
	var reason := ""
	if not report.is_empty():
		reason = String(report.get("reason", ""))
		check(not bool(report.get("valid", true)), "view report claims a valid view: %s" % report)
		check(reason.length() > 0, "view report gives no reason: %s" % report)
	var status := status_text()
	if status_marker.length() > 0:
		check(status.contains(status_marker), "status line does not say the view is incomplete: '%s'" % status)
	if reason.length() > 0:
		check(status.contains(reason), "status line does not carry the view's reason '%s': '%s'" % [reason, status])

	var drawn := terrain_snapshot(view)
	check(drawn == published, "the failed build changed the drawn terrain (layers %s, before %s)" % [drawn.keys(), published.keys()])

	# A view with no geometry keeps the framing it had.
	var extent := preview_extent()
	if not extent.has_area() and _extents.has(view):
		extent = _extents[view]
	if extent.has_area():
		var coverage := await terrain_coverage(label, extent)
		if coverage >= 0.0 and published.is_empty():
			check(coverage <= EMPTY_COVERAGE, "terrain covers %.2f of the map area although nothing was published" % coverage)
		elif coverage >= 0.0:
			check(coverage >= DRAWN_COVERAGE, "the kept terrain covers only %.2f of the map area" % coverage)
	check_log(true)


## A drawn tile view gives the gameplay grid its geometry.
func check_grid_binding() -> void:
	var grid := _scene.get_node_or_null("Preview/Grid")
	if not check(grid != null, "gameplay grid is missing"):
		return
	var path: NodePath = grid.get("TileMapLayerPath")
	var bound := grid.get_node_or_null(path) if not path.is_empty() else null
	if check(bound is TileMapLayer, "gameplay grid is not bound to a tile layer (path '%s')" % path):
		check((bound as TileMapLayer).tile_set != null, "gameplay grid is bound to %s, which has no TileSet" % bound.name)


func check_log(allow_warnings: bool) -> void:
	var entries := _logger.take()
	for error in entries.errors:
		check(false, "error logged: %s" % error)
	for warning in entries.warnings:
		if not allow_warnings:
			check(false, "warning logged: %s" % warning)


## Starts a step: warnings left by the previous step are dropped, errors never are.
func begin_step() -> void:
	check_log(true)


## Fraction of the map's screen rectangle that is not background, with props hidden,
## so it measures the terrain renderer alone. -1 when the rectangle cannot be measured.
func terrain_coverage(label: String, extent: Rect2) -> float:
	var hidden: Array[CanvasItem] = []
	for path in OVERLAY_PATHS:
		var node := _scene.get_node_or_null(path) as CanvasItem
		if node != null and node.visible:
			node.visible = false
			hidden.append(node)
	await process_frame
	await RenderingServer.frame_post_draw
	var image := root.get_texture().get_image()
	for node in hidden:
		node.visible = true
	image.save_png("%s/%s.png" % [OUTPUT, label.validate_filename()])

	var preview := _scene.get_node("Preview") as Node2D
	var screen: Rect2 = preview.get_global_transform_with_canvas() * extent
	var usable := Rect2(HUD_RIGHT, TOP_MARGIN, image.get_width() - HUD_RIGHT, image.get_height() - TOP_MARGIN - BOTTOM_MARGIN)
	var region := screen.intersection(usable)
	if not check(region.get_area() >= 40000.0 and region.get_area() >= screen.get_area() * 0.25,
			"cannot measure: map rectangle %s is not on screen" % screen):
		return -1.0

	var background := image.get_pixel(image.get_width() - 8, 6)
	for y in range(2, 11):
		for x in range(image.get_width() - 12, image.get_width() - 3):
			if not same_color(image.get_pixel(x, y), background):
				check(false, "cannot measure: the background sample at the top-right corner is not uniform")
				return -1.0

	var total := 0
	var covered := 0
	for y in range(int(region.position.y), int(region.end.y), 3):
		for x in range(int(region.position.x), int(region.end.x), 3):
			total += 1
			if not same_color(image.get_pixel(x, y), background):
				covered += 1
	return float(covered) / float(maxi(total, 1))


func same_color(a: Color, b: Color) -> bool:
	return absf(a.r - b.r) < 0.025 and absf(a.g - b.g) < 0.025 and absf(a.b - b.b) < 0.025


func capture(label: String) -> void:
	await process_frame
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png("%s/%s.png" % [OUTPUT, label.validate_filename()])


# ---- lab driving ---------------------------------------------------------------

func open_lab(tile_pack: Resource = null, iso_pack: Resource = null) -> bool:
	_extents.clear()
	_scene = load(LAB).instantiate()
	_world = _scene.get_node("World")
	if tile_pack != null:
		_scene.get_node("Preview/TileRenderer").set("LibraryPack", tile_pack)
	if iso_pack != null:
		_scene.get_node("Preview/IsoAutotile").set("LibraryPack", iso_pack)
	_world.connect("GenerationFinished", _on_generation_finished)
	_generation_done = false
	root.add_child(_scene)
	var built := await wait_generation()
	if built:
		check(_generation_success, "the lab's first generation failed: %s" % _generation_message)
	check_log(false)
	return built and _generation_success


func close_lab() -> void:
	if _scene != null and is_instance_valid(_scene):
		_scene.queue_free()
	_scene = null
	_world = null
	await process_frame
	await process_frame


func _on_generation_finished(success: bool, message: String) -> void:
	_generation_done = true
	_generation_success = success
	_generation_message = message


func wait_generation() -> bool:
	var deadline := Time.get_ticks_msec() + GENERATION_TIMEOUT_MS
	while not _generation_done and Time.get_ticks_msec() < deadline:
		await process_frame
	check(_generation_done, "generation did not finish within %d s" % (GENERATION_TIMEOUT_MS / 1000))
	await process_frame
	await process_frame
	return _generation_done


func select_view(index: int) -> void:
	var view := _scene.get_node(VIEW_PATH) as OptionButton
	view.select(index)
	view.item_selected.emit(index)
	await process_frame
	await process_frame


func press_and_wait(button_path: String) -> bool:
	_generation_done = false
	(_scene.get_node(button_path) as BaseButton).pressed.emit()
	return await wait_generation()


func select_size_and_wait(index: int) -> bool:
	_generation_done = false
	var sizes := _scene.get_node(SIZE_PATH) as OptionButton
	sizes.select(index)
	sizes.item_selected.emit(index)
	var built := await wait_generation()
	if built:
		check(_generation_success, "generation at map size %d failed: %s" % [index, _generation_message])
	return built


func renderer_for(view: int) -> Node:
	return _scene.get_node("Preview/TileRenderer" if view == GAME_TILES else "Preview/IsoAutotile")


func view_name(view: int) -> String:
	return "game tiles" if view == GAME_TILES else "isometric tiles"


# Calls into C# go through untyped values: a call that throws returns nil, and assigning
# nil to a typed variable would abort the check function before its log was read.
func view_report(renderer: Node) -> Dictionary:
	if not check(renderer.has_method("GetPaintDiagnostics"), "%s has no GetPaintDiagnostics report" % renderer.name):
		return {}
	var report = renderer.call("GetPaintDiagnostics")
	if not check(report is Dictionary, "%s.GetPaintDiagnostics() returned no report" % renderer.name):
		return {}
	return report


func preview_extent() -> Rect2:
	var extent = _world.call("PreviewExtent")
	if not check(extent is Rect2, "PreviewExtent() returned no rectangle"):
		return Rect2()
	return extent


func status_text() -> String:
	return (_scene.get_node(STATUS_PATH) as Label).text


func biome_layers(renderer: Node) -> Array[TileMapLayer]:
	var layers: Array[TileMapLayer] = []
	for child in renderer.get_children():
		if child is TileMapLayer and not child.is_queued_for_deletion() and child.get_meta(BIOME_DISPLAY_META, false):
			layers.append(child)
	return layers


## The tile data of every non-empty layer the view draws, by layer name.
func terrain_snapshot(view: int) -> Dictionary:
	var snapshot := {}
	for child in renderer_for(view).get_children():
		if child is TileMapLayer and not child.is_queued_for_deletion() and child.name != "LogicalGrid" \
				and child.get_used_cells().size() > 0:
			snapshot[String(child.name)] = child.tile_map_data
	return snapshot


func cells_outside(layer: TileMapLayer, legal: Rect2i) -> int:
	var outside := 0
	for cell in layer.get_used_cells():
		if not legal.has_point(cell):
			outside += 1
	return outside


# ---- packs that cannot draw a generated lab map ---------------------------------

## A valid pack that binds only grass, so the first water or sand cell cannot be drawn.
func incomplete_pack(view: int) -> Resource:
	return make_pack("probe_grass_only", view, view)


func make_pack(pack_id: String, declared_view: int, art_view: int) -> Resource:
	var isometric := art_view == ISOMETRIC_TILES
	var cell := Vector2i(64, 32) if isometric else Vector2i(64, 64)
	var tiles := TileSet.new()
	tiles.tile_shape = TileSet.TILE_SHAPE_ISOMETRIC if isometric else TileSet.TILE_SHAPE_SQUARE
	if isometric:
		tiles.tile_layout = TileSet.TILE_LAYOUT_DIAMOND_DOWN
	tiles.tile_size = cell
	tiles.add_terrain_set()
	tiles.set_terrain_set_mode(0, TileSet.TERRAIN_MODE_MATCH_CORNERS_AND_SIDES)
	tiles.add_terrain(0)
	tiles.set_terrain_name(0, 0, "grass")

	var image := Image.create_empty(cell.x, cell.y, false, Image.FORMAT_RGBA8)
	image.fill(Color(0.35, 0.6, 0.3))
	var atlas := TileSetAtlasSource.new()
	atlas.texture = ImageTexture.create_from_image(image)
	atlas.texture_region_size = cell
	atlas.create_tile(Vector2i.ZERO)
	tiles.add_source(atlas, 0)
	var data := atlas.get_tile_data(Vector2i.ZERO, 0)
	data.terrain_set = 0
	data.terrain = 0

	var pack: Resource = load(PACK_SCRIPT).new()
	pack.set("PackId", pack_id)
	pack.set("Version", "1.0.0")
	pack.set("Projection", declared_view)
	pack.set("Tiles", tiles)
	pack.set("TerrainSet", 0)
	var bindings: Dictionary[String, int] = {"grass": 0}
	pack.set("TerrainBindings", bindings)
	pack.set("RequireCompleteBinaryConnections", false)
	return pack
