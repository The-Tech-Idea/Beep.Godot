extends SceneTree

# The isometric shader surface at 1024x1024: no detailed geometry at overview zoom, bounded
# chunk geometry the same frame the camera zooms in, and chunk diamonds that tessellate exactly
# -- their union is the overview diamond, with no seam where two chunks meet. The streamer
# places no tiles; coverage is read from its "SurfaceGeometry" Polygon2D.

var failures: Array[String] = []

func check(value: bool, message: String) -> void:
	if not value:
		failures.append(message)
		push_error(message)

func geometry(layer: TileMapLayer) -> Polygon2D:
	return layer.get_node_or_null("SurfaceGeometry") as Polygon2D

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

func _initialize() -> void: run.call_deferred()

func run() -> void:
	create_timer(30).timeout.connect(func(): quit(1))
	# Explicit, so the zoom-1 native comparison below fits a 20x12 isometric map (1024x512 px).
	root.size = Vector2i(1200, 700)
	var host := Node2D.new()
	root.add_child(host)
	var layer := TileMapLayer.new()
	var tiles := TileSet.new()
	tiles.tile_size = Vector2i(64, 32)
	tiles.tile_shape = TileSet.TILE_SHAPE_ISOMETRIC
	tiles.tile_layout = TileSet.TILE_LAYOUT_DIAMOND_DOWN
	tiles.tile_offset_axis = TileSet.TILE_OFFSET_AXIS_HORIZONTAL
	var image := Image.create(64, 32, false, Image.FORMAT_RGBA8)
	for y in 32:
		for x in 64:
			if absf((x + 0.5 - 32) / 32.0) + absf((y + 0.5 - 16) / 16.0) <= 1:
				image.set_pixel(x, y, Color.WHITE)
	var atlas := TileSetAtlasSource.new()
	atlas.texture = ImageTexture.create_from_image(image)
	atlas.texture_region_size = Vector2i(64, 32)
	atlas.create_tile(Vector2i.ZERO)
	tiles.add_source(atlas, 0)
	layer.tile_set = tiles
	layer.rendering_quadrant_size = 1025
	var shader := Shader.new()
	shader.code = "shader_type canvas_item; varying vec2 p; void vertex(){p=VERTEX;} void fragment(){COLOR=vec4(0.5+p.x/2048.0,p.y/1024.0,0.2,0.6*texture(TEXTURE,UV).a);}"
	var material := ShaderMaterial.new()
	material.shader = shader
	layer.material = material
	host.add_child(layer)
	var camera := Camera2D.new()
	host.add_child(camera)
	var stream: Node = load("res://addons/beep_game_builder_cs/ecs/terrain/TerrainSurfaceStreamingComponent.cs").new()
	layer.add_child(stream)
	stream.Configure(Vector2i(1024, 1024))
	stream.set_process(false)
	camera.position = layer.map_to_local(Vector2i(512, 512))
	camera.zoom = Vector2(0.005, 0.005)
	await process_frame
	camera.force_update_scroll()
	stream.UpdateResidency()
	check(stream.IsOverviewVisible and stream.ResidentCellCount == 0 and vertex_count(layer) == 4, "Million-cell isometric overview allocated detailed geometry")
	check(layer.get_used_cells().is_empty(), "Isometric streaming placed tiles instead of chunk geometry")
	check(stream.CanSupplyOverview(root, Rect2i(0, 0, 1024, 1024)), "Isometric overview cannot release camera data demand")
	var overview_outline: PackedVector2Array = geometry(layer).polygon if geometry(layer) != null else PackedVector2Array()
	camera.zoom = Vector2.ONE
	camera.force_update_scroll()
	stream.UpdateResidency()
	check(not stream.IsOverviewVisible and stream.ResidentCellCount > 0 and stream.ResidentCellCount < 30000, "Isometric zoom-in did not switch to bounded detail the same frame")
	check(covers(layer, layer.to_local(camera.global_position)), "Isometric camera center has no surface after zoom-in")
	# Every chunk diamond lies inside the map diamond. Inflated a hair: chunk vertices on the
	# map's edge sit exactly on it, and a point on the boundary is not "inside".
	var mesh := geometry(layer)
	if mesh != null and overview_outline.size() == 4:
		var centre := Vector2.ZERO
		for v in overview_outline: centre += v / overview_outline.size()
		var inflated := PackedVector2Array()
		for v in overview_outline: inflated.append(centre + (v - centre) * 1.001)
		for vertex in mesh.polygon:
			check(Geometry2D.is_point_in_polygon(vertex, inflated), "Isometric chunk geometry left the map diamond at %s" % str(vertex))
	var at_rest: int = stream.ResidentCellCount
	for i in 100: stream.UpdateResidency()
	check(stream.ResidentCellCount == at_rest, "Isometric residency drifted while the camera stood still")
	# Native comparison on a 20x12 map at ChunkSize 8 (the smallest the component allows): six
	# chunk diamonds, four of them partial edge chunks, against the one overview diamond -- same
	# camera, same origin, at zoom 1 so a one-pixel seam lands on pixel centres.
	stream.ChunkSize = 8
	stream.Configure(Vector2i(20, 12))
	stream.set_process(false)
	stream.EnableOverview = false
	camera.position = (layer.map_to_local(Vector2i.ZERO) + layer.map_to_local(Vector2i(19, 11))) / 2
	camera.zoom = Vector2.ONE
	camera.force_update_scroll()
	stream.UpdateResidency()
	check(stream.ResidentChunkCount == 6, "20x12 map at ChunkSize 8 should be six chunk diamonds, got %d" % stream.ResidentChunkCount)
	if DisplayServer.get_name() != "headless":
		await process_frame
		await RenderingServer.frame_post_draw
		var detail := root.get_texture().get_image()
		check(not stream.IsOverviewVisible, "Detail comparison did not use chunk geometry")
		stream.OverviewCellPixels = 16
		# Zoom out to take the overview decision, then back to the same camera without another
		# update, so the overview diamond is rendered at zoom 1 too.
		stream.EnableOverview = true
		camera.zoom = Vector2(0.2, 0.2)
		camera.force_update_scroll()
		stream.UpdateResidency()
		camera.zoom = Vector2.ONE
		camera.force_update_scroll()
		await process_frame
		await RenderingServer.frame_post_draw
		var overview := root.get_texture().get_image()
		check(stream.IsOverviewVisible, "Overview comparison did not use the whole-map diamond")
		var different := 0
		var colored := 0
		for y in detail.get_height():
			for x in detail.get_width():
				var a := detail.get_pixel(x, y)
				var b := overview.get_pixel(x, y)
				if absf(a.r-b.r) + absf(a.g-b.g) + absf(a.b-b.b) > 0.04: different += 1
				if absf(a.r-a.b) > 0.1: colored += 1
		check(colored > 1000, "Isometric comparison was blank")
		print("[iso-surface-overview] chunk diamonds vs overview diamond: %d differing pixels of %d colored" % [different, colored])
		check(different == 0, "Isometric chunk diamonds and the overview diamond differ in %d pixels: a seam or shifted shader coordinates" % different)
		DirAccess.make_dir_recursive_absolute("res://tests/output/iso_surface")
		detail.save_png("res://tests/output/iso_surface/detail.png")
		overview.save_png("res://tests/output/iso_surface/overview.png")
	host.free()
	print("[iso-surface-overview] OK" if failures.is_empty() else "[iso-surface-overview] FAILED")
	quit(0 if failures.is_empty() else 1)
