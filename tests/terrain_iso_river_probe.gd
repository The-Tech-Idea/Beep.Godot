extends SceneTree

const CELLS = preload("res://addons/beep_game_builder_cs/ecs/grid/GridCellDataComponent.cs")
const NAV = preload("res://addons/beep_game_builder_cs/ecs/grid/GridNavigationComponent.cs")
const ORIGIN := Vector2i(-4, 6)
var failures: Array[String] = []

func _initialize() -> void:
	call_deferred("run")

func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		printerr("[terrain-iso-river] FAIL: " + message)

func settle() -> void:
	await process_frame
	await process_frame
	await RenderingServer.frame_post_draw

func run() -> void:
	Engine.max_fps = 120
	root.size = Vector2i(1280, 800)
	var host := Node2D.new()
	root.add_child(host)
	var cells = CELLS.new()
	cells.name = "Cells"
	host.add_child(cells)
	var nav = NAV.new()
	nav.set("CellDataPath", NodePath("../Cells"))
	nav.set("BoundsOrigin", ORIGIN)
	nav.set("BoundsSize", Vector2i(9, 9))
	host.add_child(nav)
	var demo = load("res://addons/beep_game_builder_cs/templates/scenes/terrain/terrain_iso_demo.tscn").instantiate()
	var iso: Node2D = demo.get_node("World/Iso")
	iso.get_parent().remove_child(iso)
	demo.free()
	iso.set("RefreshOnReady", false)
	iso.set("TerrainGeneratorPath", NodePath())
	iso.set("CellDataPath", NodePath("../Cells"))
	iso.set("BoundsOrigin", ORIGIN)
	iso.set("BoundsSize", Vector2i(9, 9))
	# The sea's dials are the world's one look since VIEW-04; the demo scene assigns the shipped
	# one, and this probe drives the renderer without a world, so it authors its own.
	var look: Resource = load("res://addons/beep_game_builder_cs/ecs/terrain/TerrainWaterLook.cs").new()
	look.set("GroundTextureTiles", 9.0)
	look.set("WaterTextureTiles", 4.0)
	iso.set("WaterLook", look)
	var authored_water := Polygon2D.new()
	authored_water.name = "IsoWater"
	var shared_material := ShaderMaterial.new()
	shared_material.shader = load("res://addons/beep_game_builder_cs/shaders/iso_water.gdshader")
	shared_material.set_shader_parameter("map_size", Vector2(23, 31))
	authored_water.material = shared_material
	iso.add_child(authored_water)
	host.add_child(iso)
	var river_cells: Array[Vector2i] = []
	for y in range(1, 8):
		river_cells.append(ORIGIN + Vector2i(4, y))
	river_cells.append(ORIGIN + Vector2i(3, 3))
	river_cells.append(ORIGIN + Vector2i(2, 6))
	for cell in river_cells:
		cells.call("SetTerrainKind", cell, "shallow_water")
		cells.call("SetMetadata", cell, "terrain_water_source", "river")
	iso.call("Rebuild")
	await settle()
	var river: Polygon2D = iso.get_node_or_null("IsoRivers")
	check(river != null, "Rivers still bypass the shared animated water surface")
	if river == null:
		host.free()
		quit(1)
		return
	var sea: Polygon2D = iso.get_node("IsoWater")
	check(sea == authored_water and sea.material != shared_material, "Authored geometry/material ownership was not respected")
	check(shared_material.get_shader_parameter("map_size") == Vector2(23, 31), "Rebuild mutated a shared authored material")
	check(river.polygons.size() == river_cells.size(), "Missing or extra river diamonds")
	check(river.material != sea.material and river.material.shader == sea.material.shader, "River material must be isolated but share the water shader")
	check(sea.material.get_shader_parameter("ground_texture_tiles") == 9.0 and sea.material.get_shader_parameter("water_texture_tiles") == 4.0, "Isometric texture scales were not bound")
	for parameter in ["coast_map", "tex_shallow", "tex_deep", "tex_sand", "map_size", "cell_size", "tile_batch", "ground_texture_tiles", "water_texture_tiles"]:
		check(river.material.get_shader_parameter(parameter) == sea.material.get_shader_parameter(parameter), "Water binding drift: " + parameter)
	check(river.material.get_shader_parameter("foam_strength") == 0.0, "Ocean breakers applied to narrow rivers")
	check(sea.material.get_shader_parameter("foam_strength") == look.get("FoamStrength"), "River settings changed ocean settings")
	var ground: TileMapLayer = iso.get_node("IsoLevel1")
	check(not nav.call("IsBlocked", river_cells[0]), "Default shallow-water wading policy changed")
	var blocked: Array[String] = ["shallow_water"]
	nav.set("BlockedTerrainKinds", blocked)
	for cell in river_cells:
		check(ground.get_cell_source_id(cell) == -1, "Static water atlas tile remains")
		check(iso.call("SurfaceLevel", cell) == 1, "River lost its ground-level surface")
		check(nav.call("IsBlocked", cell), "River became walkable land")
		var surface: Vector2 = iso.call("SurfacePosition", cell)
		check(iso.call("SurfaceCellAt", surface) == cell, "River picking disagrees with its visible surface")
		var covered := false
		for indices in river.polygons:
			var polygon := PackedVector2Array()
			for index in indices:
				polygon.append(river.transform * river.polygon[index])
			if Geometry2D.is_point_in_polygon(surface, polygon):
				covered = true
		check(covered, "Native water polygon missed river cell centre")
	var focus: Vector2 = iso.call("SurfacePosition", ORIGIN + Vector2i(4, 4))
	host.scale = Vector2.ONE * 1.3
	host.position = Vector2(640, 400) - focus * host.scale
	await settle()
	DirAccess.make_dir_recursive_absolute("res://tests/output/iso_river")
	var before := root.get_texture().get_image()
	before.save_png("res://tests/output/iso_river/animated_a.png")
	await create_timer(1.5).timeout
	await settle()
	var after := root.get_texture().get_image()
	after.save_png("res://tests/output/iso_river/animated_b.png")
	var changed := 0
	for y in range(392, 409):
		for x in range(626, 655):
			if before.get_pixel(x, y) != after.get_pixel(x, y):
				changed += 1
	check(changed > 30, "Narrow river is hidden or its water is not animated")
	print("[terrain-iso-river] animated centre pixels: ", changed)
	# A diagnostic solid shader proves actual visible coverage, not just geometry.
	var original: Material = river.material
	var diagnostic := ShaderMaterial.new()
	diagnostic.shader = Shader.new()
	diagnostic.shader.code = "shader_type canvas_item; void fragment(){ COLOR=vec4(1.0,0.0,1.0,1.0); }"
	river.material = diagnostic
	await settle()
	var mask := root.get_texture().get_image()
	for cell in river_cells:
		var pixel := Vector2i(iso.to_global(iso.call("SurfacePosition", cell)))
		var colour := mask.get_pixelv(pixel)
		check(colour.r > 0.95 and colour.b > 0.95 and colour.g < 0.05, "Foreground blocks hide river centre")
	river.material = original
	var removed := river_cells[0]
	cells.call("SetTerrainKind", removed, "grass")
	await settle()
	check(river.polygons.size() == river_cells.size() - 1, "Live drain retained water geometry")
	check(not nav.call("IsBlocked", removed), "Drained river remained blocked")
	iso.hide()
	cells.call("ClearCells")
	await settle()
	iso.show()
	await settle()
	check(not river.visible and river.polygon.is_empty(), "Hidden-view catch-up retained river surface")
	# River height cannot depend on decorative seabed reach. There are no water atlas controls.
	iso.set("SeabedDepth", 1)
	for property in iso.get_property_list():
		check(property.name not in ["ShallowWaterFrame", "DeepWaterFrame"], "Obsolete water frame control remains exposed")
	for y in range(9):
		for x in range(9):
			var cell := ORIGIN + Vector2i(x, y)
			cells.call("SetTerrainKind", cell, "shallow_water")
			cells.call("SetMetadata", cell, "terrain_water_source", "river")
	iso.call("Rebuild")
	await settle()
	check(river.polygons.size() == 81 and river.visible, "Wide rivers depend on seabed depth or water atlas frames")
	check(iso.call("SurfaceLevel", ORIGIN + Vector2i(4, 4)) == 1, "Wide river centre dropped to sea level")
	iso.set("WaterShaderPath", "")
	iso.call("Rebuild")
	check(not river.visible and not sea.visible, "Disabling water left stale river rendering")
	iso.set("WaterShaderPath", "res://addons/beep_game_builder_cs/shaders/iso_water.gdshader")
	var replacement = CELLS.new()
	replacement.name = "Replacement"
	host.add_child(replacement)
	iso.set("CellDataPath", NodePath("../Replacement"))
	iso.call("Rebuild")
	check(not river.visible and river.polygon.is_empty(), "Source replacement retained old rivers")
	replacement.call("SetTerrainKind", ORIGIN, "water")
	replacement.call("SetMetadata", ORIGIN, "terrain_water_source", "river")
	await settle()
	check(river.visible and river.polygons.size() == 1, "Replacement source river was not observed")
	iso.set("CellDataPath", NodePath("../Missing"))
	iso.call("Rebuild")
	check(not river.visible and river.polygon.is_empty() and not iso.get("HasSurface"), "Missing source retained river rendering")
	await check_seabed_depth(iso, cells, nav)
	host.free()
	if failures.is_empty():
		print("[terrain-iso-river] OK")
	quit(0 if failures.is_empty() else 1)

# VIEW-05: the seabed shelves by distance from the WATERLINE - the coast field the sea over it is
# drawn from - not by four-neighbour steps out from land.
#
# The field measures on its own fine sample grid, so the numbers are not the ones a sketch on paper
# gives: a cell diagonally off a single land cell reads about 1.13 tiles, against 0.63 across a
# side. The two metrics therefore agree about that cell, and an earlier version of this guard,
# written on the 0.71 a corner-to-centre measurement suggests, passed against the old sweep. They
# separate on a DIAGONAL COAST, which is what the second fixture below measures.
func check_seabed_depth(iso: Node2D, cells: Node, nav: Node) -> void:
	iso.set("CellDataPath", NodePath("../Cells"))
	iso.set("SeabedDepth", 5)
	cells.call("ClearCells")
	for y in range(9):
		for x in range(9):
			cells.call("SetTerrainKind", ORIGIN + Vector2i(x, y), "grass")
	# A 3x3 sea pocket in the middle of the land: its own corners are diagonal to land.
	var pocket: Array[Vector2i] = []
	for y in range(3, 6):
		for x in range(3, 6):
			var cell: Vector2i = ORIGIN + Vector2i(x, y)
			pocket.append(cell)
			cells.call("SetTerrainKind", cell, "deep_water")
	iso.call("Rebuild")
	await settle()
	var seabed: TileMapLayer = iso.get_node_or_null("IsoSeabed")
	check(seabed != null, "The block view drew no seabed for an inland sea")
	if seabed == null:
		return
	# Every cell of a 3x3 pocket touches the shore, across a side or across a corner, and every one
	# of those distances ceils into the first band - so the whole pocket beds and none of it is
	# left bare.
	for cell in pocket:
		check(seabed.get_cell_source_id(cell) != -1, "Pocket cell %s has no seabed" % cell)
	# The case the two metrics disagree on: a DIAGONAL coast. Four-neighbour steps out from land
	# count a cell two diagonal cells offshore as four steps deep; the waterline it is actually
	# measured from is about 2.5 tiles away, which is the distance the water over it shades by.
	# Gravel against rock - two bands apart, a shelf the sea's own tint does not have.
	cells.call("ClearCells")
	for y in range(9):
		for x in range(9):
			cells.call("SetTerrainKind", ORIGIN + Vector2i(x, y), "deep_water" if x + y > 4 else "grass")
	iso.call("Rebuild")
	await settle()
	var offshore: Vector2i = ORIGIN + Vector2i(4, 4)
	check(seabed.get_cell_source_id(offshore) != -1, "The cell offshore of a diagonal coast has no seabed")
	check(seabed.get_cell_atlas_coords(offshore) == seabed.get_cell_atlas_coords(ORIGIN + Vector2i(3, 3)),
		"Diagonal-coast beds step by four-neighbour count, not by distance from the waterline: %s against %s"
			% [seabed.get_cell_atlas_coords(offshore), seabed.get_cell_atlas_coords(ORIGIN + Vector2i(3, 3))])
	# Open sea past the field's range carries no depth, so the bed stops rather than tiling the ocean.
	cells.call("ClearCells")
	for y in range(9):
		for x in range(9):
			cells.call("SetTerrainKind", ORIGIN + Vector2i(x, y), "deep_water")
	iso.call("Rebuild")
	await settle()
	var bedded := 0
	for y in range(9):
		for x in range(9):
			if seabed.get_cell_source_id(ORIGIN + Vector2i(x, y)) != -1:
				bedded += 1
	check(bedded == 0, "Open water with no shore in reach still drew %d bed tiles" % bedded)
