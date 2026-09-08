extends SceneTree
const ECS := "res://addons/beep_game_builder_cs/ecs/"
const MAPS := ["id_map", "shade_map", "coast_map", "lake_map", "lake_width_map"]
var failures: Array[String] = []
func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		push_error(message)
func maps(painter: Node) -> Dictionary:
	var result := {}
	var material: ShaderMaterial = painter.get_node("SplatSurface").material
	for slot in MAPS:
		var texture: Texture2D = material.get_shader_parameter(slot)
		result[slot] = texture.get_image().get_data()
	return result
func _initialize() -> void: run.call_deferred()
func run() -> void:
	var host := Node2D.new()
	root.add_child(host)
	var cells: Node = load(ECS + "grid/GridCellDataComponent.cs").new()
	cells.name = "Cells"
	host.add_child(cells)
	var generator: Node = load(ECS + "terrain/TerrainGeneratorComponent.cs").new()
	generator.CellDataPath = NodePath("../Cells")
	generator.BoundsSize = Vector2i(64, 32)
	generator.GenerateOnReady = false
	host.add_child(generator)
	generator.GenerateTerrain()
	var painter: Node = load(ECS + "terrain/TerrainPaintedRendererComponent.cs").new()
	painter.CellDataPath = NodePath("../Cells")
	painter.BoundsSize = Vector2i(64, 32)
	painter.RefreshOnReady = false
	host.add_child(painter)
	painter.Rebuild()
	var before := maps(painter)
	var original_id_texture: Texture2D = painter.get_node("SplatSurface").material.get_shader_parameter("id_map")
	var archive: Node = load(ECS + "grid/GridCellArchiveComponent.cs").new()
	archive.CellDataPath = NodePath("../Cells")
	archive.ArchiveDirectory = "user://tests/painted_archive_%s_%s" % [OS.get_process_id(), Time.get_ticks_usec()]
	host.add_child(archive)
	check(archive.SaveChunk(Vector2i.ZERO) and archive.EvictSavedChunk(Vector2i.ZERO), "Painted archive fixture failed")
	painter.Rebuild()
	var retired := maps(painter)
	check(painter.get_node("SplatSurface").material.get_shader_parameter("id_map") == original_id_texture, "Residency-only change rebuilt the terrain texture")
	for slot in MAPS: check(before[slot] == retired[slot], "Eviction changed painted " + slot)
	var at := Vector2i(40, 10)
	cells.SetTerrainKind(at, "mud" if cells.GetTerrainKind(at) != "mud" else "grass")
	painter.Rebuild()
	var edited := maps(painter)
	check(edited.id_map != before.id_map, "Resident edits were frozen by archived neighboring data")
	for y in 32:
		for x in 32:
			var offset := (y * 64 + x) * 4
			check(edited.id_map.slice(offset, offset + 4) == before.id_map.slice(offset, offset + 4), "Archived appearance was overwritten during a resident edit")
	check(archive.LoadChunk(Vector2i.ZERO), "Painted archive reload failed")
	# The reloaded chunk decodes into new record and water-patch objects with identical content.
	# Identical content must not rebuild the terrain textures: on a huge map that rebuild is a
	# whole-map id/shade/coast pass every time an actor walks into an archived shoreline chunk.
	var edited_id_texture: Texture2D = painter.get_node("SplatSurface").material.get_shader_parameter("id_map")
	painter.Rebuild()
	var restored := maps(painter)
	for slot in MAPS: check(edited[slot] == restored[slot], "Reload changed retained painted " + slot)
	check(painter.get_node("SplatSurface").material.get_shader_parameter("id_map") == edited_id_texture,
		"Reloading identical archived content rebuilt the terrain texture")
	# An inland land-to-land edit changes id texels and nothing about the coast: the coast and
	# lake textures stay the very same instances. (Every generated cell carries a uniform, all-dry
	# patch; an edit that does not flip wetness keeps it, so the coast inputs are unchanged.)
	var material: ShaderMaterial = painter.get_node("SplatSurface").material
	# A generated land cell whose water patch is uniform and dry: GridTerrainWaterPatch encodes a
	# one-bit dry patch as [1, 0], "AQA=" in Base64. The land-to-land edit must keep that patch -
	# it draws no boundary - which is exactly what leaves the coast and lake inputs unchanged.
	var inland := Vector2i(-1, -1)
	for y in 32:
		if inland.x >= 0: break
		for x in range(32, 64):
			var record: Dictionary = cells.GetCell(Vector2i(x, y))
			var k: String = record.get("terrain", "")
			if record.get("water_surface", "") == "AQA=" and not (k == "deep_water" or k == "shallow_water" or k == "water"):
				inland = Vector2i(x, y)
				break
	check(inland.x >= 0, "Fixture has no land cell with a uniform dry water patch")
	var coast_before_inland: Texture2D = material.get_shader_parameter("coast_map")
	var lake_before_inland: Texture2D = material.get_shader_parameter("lake_map")
	var ids_before_inland: PackedByteArray = maps(painter).id_map
	cells.SetTerrainKind(inland, "mud" if cells.GetTerrainKind(inland) != "mud" else "gravel")
	painter.Rebuild()
	check(cells.GetCell(inland).get("water_surface", "") == "AQA=", "Land-to-land edit dropped the cell's uniform water patch")
	check(material.get_shader_parameter("coast_map") == coast_before_inland, "Inland edit rebuilt the coast texture")
	check(material.get_shader_parameter("lake_map") == lake_before_inland, "Inland edit rebuilt the lake texture")
	check(maps(painter).id_map != ids_before_inland, "Inland edit did not reach the id texels")
	# A shoreline edit moves the coast. The windowed update must equal a whole rebuild exactly:
	# changing the coast range and restoring it forces two whole rebuilds of the same inputs.
	var shore := Vector2i(-1, -1)
	for y in 32:
		if shore.x >= 0: break
		for x in range(33, 63):
			var k: String = cells.GetTerrainKind(Vector2i(x, y))
			var right: String = cells.GetTerrainKind(Vector2i(x + 1, y))
			var wet := k == "deep_water" or k == "shallow_water" or k == "water"
			var right_wet := right == "deep_water" or right == "shallow_water" or right == "water"
			if not wet and right_wet:
				shore = Vector2i(x, y)
				break
	check(shore.x >= 0, "Fixture has no shoreline land cell in the resident half")
	cells.SetTerrainKind(shore, "shallow_water")
	painter.Rebuild()
	var windowed: PackedByteArray = maps(painter).coast_map
	check(material.get_shader_parameter("coast_map") != coast_before_inland, "Shoreline edit left the coast texture unchanged")
	var range_tiles: float = painter.CoastRangeTiles
	painter.CoastRangeTiles = range_tiles + 1.0
	painter.Rebuild()
	painter.CoastRangeTiles = range_tiles
	painter.Rebuild()
	check(maps(painter).coast_map == windowed, "Windowed coast update differs from a whole rebuild of the same inputs")
	# New-world identity must discard every retained visual sample.
	cells.ClearCells()
	cells.FillTerrain(Rect2i(0, 0, 64, 32), "desert")
	painter.Rebuild()
	var replaced := maps(painter)
	check(replaced.id_map != restored.id_map, "World replacement reused old visual samples")
	# Rebinding cannot use the previous world's appearance for an unobserved archive.
	var other: Node = load(ECS + "grid/GridCellDataComponent.cs").new()
	other.name = "Other"
	host.add_child(other)
	other.SetChunkAvailable(Vector2i.ZERO, false)
	painter.CellDataPath = NodePath("../Other")
	painter.Rebuild()
	check(not painter.get_node("SplatSurface").visible, "Unknown world displayed stale samples from another source")
	other.SetChunkAvailable(Vector2i.ZERO, true)
	painter.Rebuild()
	check(painter.get_node("SplatSurface").visible, "Renderer did not recover when source became ready")
	DirAccess.remove_absolute(archive.GetChunkPath(Vector2i.ZERO))
	DirAccess.remove_absolute(ProjectSettings.globalize_path(archive.ArchiveDirectory))
	host.free()
	print("[terrain-painted-archive] OK" if failures.is_empty() else "[terrain-painted-archive] FAILED")
	quit(0 if failures.is_empty() else 1)
