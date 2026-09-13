extends SceneTree

const BASE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/ground_masks/staging/"
const OUTPUT = "res://addons/beep_game_builder_cs/generated/test/library/output/"
const DIRECTIONS = [Vector2i(0,-1), Vector2i(1,0), Vector2i(0,1), Vector2i(-1,0), Vector2i(1,-1), Vector2i(1,1), Vector2i(-1,1), Vector2i(-1,-1)]
var errors: Array[String] = []

func _initialize() -> void:
	call_deferred("run")

func check(ok: bool, message: String) -> void:
	if not ok:
		errors.append(message)
		push_error(message)

func normalize(mask: int) -> int:
	for i in range(4):
		if not mask & (1 << i) or not mask & (1 << ((i + 1) % 4)):
			mask &= ~(1 << (i + 4))
	return mask

func run() -> void:
	var results: Array = []
	for projection in ["square", "isometric"]:
		var iso = projection == "isometric"
		var packed = load(BASE + projection + "/surface_candidate_v1/grass_dirt_review.tscn") as PackedScene
		if packed == null:
			check(false, "Missing candidate " + projection)
			continue
		var scene = packed.instantiate()
		var layer = scene.get_node("Ground") as TileMapLayer
		for parameter in ["surface_texture", "secondary_texture"]:
			var texture = layer.material.get_shader_parameter(parameter) as Texture2D
			check(texture != null and texture.get_size() == Vector2(256,256), "Uncalibrated runtime surface")
			if texture != null:
				check(texture.resource_path.contains("runtime/surfaces_v1/"), "Surface is not a shared runtime resource")
		var bits = [TileSet.CELL_NEIGHBOR_TOP_RIGHT_SIDE, TileSet.CELL_NEIGHBOR_BOTTOM_RIGHT_SIDE, TileSet.CELL_NEIGHBOR_BOTTOM_LEFT_SIDE, TileSet.CELL_NEIGHBOR_TOP_LEFT_SIDE, TileSet.CELL_NEIGHBOR_RIGHT_CORNER, TileSet.CELL_NEIGHBOR_BOTTOM_CORNER, TileSet.CELL_NEIGHBOR_LEFT_CORNER, TileSet.CELL_NEIGHBOR_TOP_CORNER] if iso else [TileSet.CELL_NEIGHBOR_TOP_SIDE, TileSet.CELL_NEIGHBOR_RIGHT_SIDE, TileSet.CELL_NEIGHBOR_BOTTOM_SIDE, TileSet.CELL_NEIGHBOR_LEFT_SIDE, TileSet.CELL_NEIGHBOR_TOP_RIGHT_CORNER, TileSet.CELL_NEIGHBOR_BOTTOM_RIGHT_CORNER, TileSet.CELL_NEIGHBOR_BOTTOM_LEFT_CORNER, TileSet.CELL_NEIGHBOR_TOP_LEFT_CORNER]
		var cases: Array = []
		for mask in range(256):
			if normalize(mask) != mask:
				continue
			layer.clear()
			for y in range(-2, 3):
				for x in range(-2, 3):
					layer.set_cell(Vector2i(x,y), 0, Vector2i(7,5))
			var cells: Array[Vector2i] = [Vector2i.ZERO]
			for b in range(8):
				if mask & (1 << b):
					cells.append(DIRECTIONS[b])
			layer.set_cells_terrain_connect(cells, 0, 0, false)
			var actual = 0
			var data = layer.get_cell_tile_data(Vector2i.ZERO)
			check(data != null, projection + " missing center")
			if data == null:
				continue
			for b in range(8):
				if data.get_terrain_peering_bit(bits[b]) == 0:
					actual |= 1 << b
			check(actual == mask, "%s expected mask %s got %s" % [projection, mask, actual])
			cases.append({"expected": mask, "actual": actual})
		check(cases.size() == 47, "Incomplete case exercise " + projection)
		var widths: Array = []
		for width in [1, 2, 3, 5]:
			layer.clear()
			var cells: Array[Vector2i] = []
			for y in range(-1, width + 1):
				for x in range(-1, 9):
					layer.set_cell(Vector2i(x,y), 0, Vector2i(7,5))
					if y >= 0 and y < width and x >= 0 and x < 8:
						cells.append(Vector2i(x,y))
			layer.set_cells_terrain_connect(cells, 0, 0, false)
			for cell in cells:
				check(layer.get_cell_tile_data(cell) != null and layer.get_cell_tile_data(cell).terrain == 0, "Width gap")
			var erased: Array[Vector2i] = [Vector2i(3,0)]
			layer.set_cells_terrain_connect(erased, 0, -1, false)
			var erased_data = layer.get_cell_tile_data(erased[0])
			check(erased_data == null or erased_data.terrain == -1, "Erasure failed")
			for cell in cells:
				if cell != erased[0]:
					var remaining = layer.get_cell_tile_data(cell)
					check(remaining != null and remaining.terrain == 0, "Erasure damaged a neighboring cell")
			widths.append(width)
		results.append({"projection": projection, "cases": cases, "widths": widths, "erasure": true})
		scene.free()
	DirAccess.make_dir_recursive_absolute(OUTPUT)
	var file = FileAccess.open(OUTPUT + "mask_connections.json", FileAccess.WRITE)
	file.store_string(JSON.stringify({"status": "passed" if errors.is_empty() else "failed", "results": results, "errors": errors, "visualApproval": false}, "  "))
	print("MASK CONNECTIONS ", "PASSED" if errors.is_empty() else "FAILED")
	quit(0 if errors.is_empty() else 1)
