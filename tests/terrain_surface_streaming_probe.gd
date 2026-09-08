extends SceneTree

# A 1024x1024 uniform shader surface never materializes the whole map. Above 65,536 cells the
# streamer places NO tiles: it draws one quad per resident chunk into a single Polygon2D child
# of the layer ("SurfaceGeometry"), and one whole-map quad at overview zoom. So this probe reads
# coverage off that node -- the geometry the shader actually draws on -- not off tile cells.
#
# Residency is exact on every update. There is no per-frame budget and nothing pending, which
# is the point: a camera jump is covered the same frame, and zooming in never shows an interval
# where the overview has gone and the detail has not arrived. The assertions say so.

var failures: Array[String] = []
func check(value: bool, message: String) -> void:
	if not value:
		failures.append(message)
		push_error(message)

func geometry(layer: TileMapLayer) -> Polygon2D:
	return layer.get_node_or_null("SurfaceGeometry") as Polygon2D

# Whether the surface geometry covers a layer-local point. Sub-polygons index the shared vertex
# array; an empty index list means the vertex array is the one polygon.
func covers(layer: TileMapLayer, local: Vector2) -> bool:
	var mesh := geometry(layer)
	if mesh == null or mesh.polygon.is_empty(): return false
	var point := local - mesh.position
	if mesh.polygons.is_empty(): return Geometry2D.is_point_in_polygon(point, mesh.polygon)
	for indices in mesh.polygons:
		var outline := PackedVector2Array()
		for index in indices: outline.append(mesh.polygon[index])
		if Geometry2D.is_point_in_polygon(point, outline): return true
	return false

func vertex_count(layer: TileMapLayer) -> int:
	var mesh := geometry(layer)
	return 0 if mesh == null else mesh.polygon.size()

func _initialize() -> void: call_deferred("run")

func run() -> void:
	# Explicit, so the zoom-1 native comparison below fits a 20x12 map of 64 px cells.
	root.size = Vector2i(1440, 900)
	var host := Node2D.new()
	root.add_child(host)
	current_scene = host
	var layer := TileMapLayer.new()
	layer.name = "Surface"
	var tiles := TileSet.new()
	tiles.tile_size = Vector2i(64, 64)
	var source := TileSetAtlasSource.new()
	var image := Image.create(64, 64, false, Image.FORMAT_RGBA8)
	image.fill(Color.WHITE)
	source.texture = ImageTexture.create_from_image(image)
	source.texture_region_size = Vector2i(64, 64)
	source.create_tile(Vector2i.ZERO)
	tiles.add_source(source, 0)
	layer.tile_set = tiles
	layer.rendering_quadrant_size = 1025
	host.add_child(layer)
	var camera := Camera2D.new()
	host.add_child(camera)
	camera.position = Vector2(64 * 64, 64 * 64)
	var stream: Node = load("res://addons/beep_game_builder_cs/ecs/terrain/TerrainSurfaceStreamingComponent.cs").new()
	stream.name = "Streaming"
	layer.add_child(stream)
	stream.Configure(Vector2i(1024, 1024))
	stream.set_process(false)
	var grid: Node = load("res://addons/beep_game_builder_cs/ecs/grid/GridProjectionComponent.cs").new()
	grid.name = "Grid"
	grid.TileMapLayerPath = NodePath("../Surface")
	grid.DrawGrid = false
	grid.TrackMouseCell = false
	host.add_child(grid)
	var cells: Node = load("res://addons/beep_game_builder_cs/ecs/grid/GridCellDataComponent.cs").new()
	cells.name = "Cells"
	host.add_child(cells)
	var demand: Node = load("res://addons/beep_game_builder_cs/ecs/grid/GridCameraDemandComponent.cs").new()
	demand.GridPath = NodePath("../Grid")
	demand.CellDataPath = NodePath("../Cells")
	demand.OverviewSurfacePath = NodePath("../Surface/Streaming")
	demand.BoundsCells = Rect2i(0, 0, 1024, 1024)
	host.add_child(demand)
	await process_frame
	camera.force_update_scroll()
	stream.UpdateResidency()
	check(layer.get_used_cells().is_empty(), "Streaming placed tiles instead of chunk geometry")
	check(stream.ResidentCellCount > 0 and stream.ResidentCellCount < 20000, "1024-square surface materialized the entire map")
	check(covers(layer, layer.map_to_local(Vector2i(64, 64))), "Camera center has no terrain surface on the first update")
	var at_rest: int = stream.ResidentCellCount
	for i in 100: stream.UpdateResidency()
	check(stream.ResidentCellCount == at_rest, "Residency drifted while the camera stood still")
	camera.position = Vector2(64 * 700, 64 * 700)
	camera.force_update_scroll()
	stream.UpdateResidency()
	check(stream.ResidentCellCount > 0 and stream.ResidentCellCount < 20000, "Moving the camera leaked old chunks")
	check(not covers(layer, layer.map_to_local(Vector2i(64, 64))), "Stale surface chunks remained after camera jump")
	check(covers(layer, layer.map_to_local(Vector2i(700, 700))), "New camera center has no terrain surface the frame it moved")
	var shader := Shader.new()
	shader.code = "shader_type canvas_item; varying vec2 p; void vertex(){p=VERTEX+vec2(32.0);} void fragment(){COLOR=vec4(p.x/1024.0,p.y/1024.0,0.3,0.6);}"
	var material := ShaderMaterial.new()
	material.shader = shader
	layer.material = material
	camera.position = Vector2(32768, 32768)
	camera.zoom = Vector2(0.01, 0.01)
	camera.force_update_scroll()
	stream.UpdateResidency()
	check(stream.IsOverviewVisible and stream.ResidentCellCount == 0 and vertex_count(layer) == 4, "Far zoom did not switch to one whole-map quad")
	check(geometry(layer) != null and geometry(layer).material == material, "Overview did not take the layer's current material")
	cells.SetChunkPins(host, [Vector2i(1, 1)])
	demand.RefreshDemand()
	check(demand.IsUsingOverview and demand.DemandedChunkCount == 0 and cells.PinnedChunkCount == 1, "Overview requested detailed data or discarded simulation demand")
	demand.BoundsCells = Rect2i(0, 0, 512, 512)
	demand.RefreshDemand()
	check(not demand.IsUsingOverview and demand.DemandedChunkCount > 0, "Mismatched overview bounds suppressed camera demand")
	demand.BoundsCells = Rect2i(0, 0, 1024, 1024)
	layer.hide()
	demand.RefreshDemand()
	check(not demand.IsUsingOverview, "Hidden overview suppressed camera demand")
	layer.show()
	demand.RefreshDemand()
	check(demand.IsUsingOverview, "Visible overview did not resume data suppression")
	var archive: Node = load("res://addons/beep_game_builder_cs/ecs/grid/GridCellArchiveComponent.cs").new()
	archive.CellDataPath = NodePath("../Cells")
	archive.ArchiveDirectory = "user://tests/overview_budget_%s_%s" % [OS.get_process_id(), Time.get_ticks_usec()]
	archive.MaximumResidentChunks = 1
	archive.AutoEnforceChunkBudget = true
	archive.AutoLoadPinnedChunks = true
	cells.SetTerrainKind(Vector2i(32, 32), "grass")
	cells.SetTerrainKind(Vector2i(512, 512), "forest")
	cells.SetTerrainKind(Vector2i(544, 512), "desert")
	host.add_child(archive)
	var deadline := Time.get_ticks_msec() + 10000
	while (cells.StoredChunkCount > 1 or archive.IsBusy) and Time.get_ticks_msec() < deadline: await process_frame
	check(cells.StoredChunkCount == 1 and cells.IsChunkAvailable(Vector2i(1, 1)), "Overview failed to retire detailed data while preserving simulation")
	for i in 100: stream.UpdateResidency()
	check(stream.ResidentCellCount == 0, "Overview retained detailed geometry for the whole world")
	camera.zoom = Vector2(0.075, 0.075)
	camera.force_update_scroll()
	stream.UpdateResidency()
	check(stream.IsOverviewVisible and stream.ResidentCellCount == 0, "Overview lacked zoom hysteresis")
	camera.zoom = Vector2.ONE
	camera.force_update_scroll()
	stream.UpdateResidency()
	check(not stream.IsOverviewVisible and stream.ResidentCellCount > 0 and covers(layer, layer.to_local(camera.global_position)),
		"Zoom-in left an interval with neither overview nor detail under the camera")
	demand.RefreshDemand()
	check(not demand.IsUsingOverview and demand.DemandedChunkCount > 0 and cells.IsChunkPinned(Vector2i(1, 1)), "Zoom-in did not resume detail demand")
	deadline = Time.get_ticks_msec() + 10000
	while (not cells.IsChunkAvailable(Vector2i(16, 16)) or not cells.IsChunkAvailable(Vector2i(17, 16))) and Time.get_ticks_msec() < deadline: await process_frame
	check(cells.GetTerrainKind(Vector2i(512, 512)) == "forest" and cells.GetTerrainKind(Vector2i(544, 512)) == "desert", "Detail zoom failed to reload archived terrain")
	check(cells.StoredChunkCount == 3 and archive.ResidentChunksOverBudget == 2, "Budget discarded zoom-in demand to force its target")
	archive.AutoEnforceChunkBudget = false
	archive.AutoLoadPinnedChunks = false
	for chunk in [Vector2i(16, 16), Vector2i(17, 16)]: DirAccess.remove_absolute(archive.GetChunkPath(chunk))
	DirAccess.remove_absolute(ProjectSettings.globalize_path(archive.ArchiveDirectory))
	archive.free()
	for i in 180: stream.UpdateResidency()
	check(not stream.IsOverviewVisible and stream.ResidentCellCount > 0, "Zoom-in did not keep streamed detail")
	# Native: the same camera, chunk quads against the one overview quad. At ChunkSize 8 (the
	# smallest the component allows) on a 20x12 map the detail is six quads sharing edges, four
	# of them partial edge chunks, and the overview is one quad. Compared at zoom 1, where a
	# world pixel is a screen pixel, so a one-pixel seam -- a gap, or a shared edge rasterized
	# twice through the 0.6-alpha shader -- lands on pixel centres and differs. (At zoom 0.2 a
	# two-pixel gap fell between pixel centres and went unseen.)
	stream.ChunkSize = 8
	stream.Configure(Vector2i(20, 12))
	stream.set_process(false)
	stream.EnableOverview = false
	camera.position = Vector2(640, 384)
	camera.zoom = Vector2.ONE
	camera.force_update_scroll()
	stream.UpdateResidency()
	check(stream.ResidentChunkCount == 6, "20x12 map at ChunkSize 8 should be six chunk quads, got %d" % stream.ResidentChunkCount)
	if DisplayServer.get_name() != "headless":
		await process_frame
		await RenderingServer.frame_post_draw
		var detail := root.get_texture().get_image()
		# Zoom out to take the overview decision, then back to the same camera without another
		# update, so the overview quad is rendered at zoom 1 too.
		stream.OverviewCellPixels = 16.0
		stream.EnableOverview = true
		camera.zoom = Vector2(0.2, 0.2)
		camera.force_update_scroll()
		stream.UpdateResidency()
		check(stream.IsOverviewVisible, "Overview comparison did not switch to the whole-map quad")
		camera.zoom = Vector2.ONE
		camera.force_update_scroll()
		await process_frame
		await RenderingServer.frame_post_draw
		var overview := root.get_texture().get_image()
		var different := 0
		var colored := 0
		for y in detail.get_height():
			for x in detail.get_width():
				var a := detail.get_pixel(x, y)
				var b := overview.get_pixel(x, y)
				if absf(a.r-b.r) + absf(a.g-b.g) + absf(a.b-b.b) > 0.04: different += 1
				if a.r > 0.1 and a.g > 0.1 and absf(a.r-a.g) > 0.1: colored += 1
		check(colored > 1000, "Overview comparison rendered a blank surface")
		print("[surface streaming] chunk quads vs overview quad: %d differing pixels of %d colored" % [different, colored])
		check(different == 0, "Chunk quads and the overview quad differ in %d pixels: a seam or shifted shader coordinates" % different)
		DirAccess.make_dir_recursive_absolute("res://tests/output/surface_overview")
		detail.save_png("res://tests/output/surface_overview/detail.png")
		overview.save_png("res://tests/output/surface_overview/overview.png")
	stream.ChunkSize = 32
	stream.EnableOverview = true
	stream.OverviewCellPixels = 4.0
	camera.zoom = Vector2.ONE
	# Rotation and partial edge chunks must not produce out-of-bounds geometry.
	stream.Configure(Vector2i(103, 79))
	stream.set_process(false)
	camera.position = Vector2(102 * 64, 78 * 64)
	camera.rotation = 0.4
	camera.force_update_scroll()
	stream.UpdateResidency()
	var mesh := geometry(layer)
	check(mesh != null and mesh.polygon.size() > 0, "Rotated camera at the map corner produced no surface geometry")
	# Geometry-local: the origin is the first cell centre, so the map spans -half .. size*tile - half.
	var map_rect := Rect2(Vector2(-32.01, -32.01), Vector2(103, 79) * 64 + Vector2(0.02, 0.02))
	if mesh != null:
		for vertex in mesh.polygon:
			check(map_rect.has_point(vertex), "Partial edge chunk exceeded finite world bounds at %s" % str(vertex))
	check(covers(layer, layer.map_to_local(Vector2i(102, 78))), "Map corner under the camera has no surface")
	check(not covers(layer, layer.map_to_local(Vector2i.ZERO)), "Opposite corner of a 103x79 map was resident with the camera on this one")
	layer.visible = false
	stream.UpdateResidency()
	check(stream.ResidentCellCount == 0 and vertex_count(layer) == 0, "Hidden renderer retained resident geometry")
	host.free()
	print("[surface streaming] OK: 1024x1024 logical surface, exact per-chunk residency" if failures.is_empty() else "[surface streaming] FAILED")
	quit(0 if failures.is_empty() else 1)
