extends SceneTree

const BASE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/river_junctions_v1/"
const SECTIONS = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/river_sections_v1/"
const PORTS = {"north": Vector2i.UP, "east": Vector2i.RIGHT, "south": Vector2i.DOWN, "west": Vector2i.LEFT}
var errors: Array[String] = []

func _initialize() -> void:
	call_deferred("run")

func check(ok: bool, message: String) -> void:
	if not ok:
		errors.append(message)
		push_error(message)

func run() -> void:
	var manifest = JSON.parse_string(FileAccess.get_file_as_string(BASE+"manifest.json"))
	for projection in ["square", "isometric"]:
		var folder = BASE+projection+"/"
		var scene = load(SECTIONS+projection+"/river_widths_review.tscn").instantiate()
		var layer: TileMapLayer = scene.get_node("Water")
		var tiles: TileSet = layer.tile_set.duplicate(true)
		var straight = tiles.get_source(0)
		tiles.remove_source(0)
		tiles.add_source(straight,2)
		var texture = ImageTexture.create_from_image(Image.load_from_file(folder+"river_junctions_16.png"))
		check(ResourceSaver.save(texture,folder+"river_junctions_16.res")==OK,"Save junction texture")
		var atlas = TileSetAtlasSource.new()
		atlas.texture = load(folder+"river_junctions_16.res")
		atlas.texture_region_size = tiles.tile_size
		tiles.add_source(atlas,0)
		for entry in manifest.profiles:
			var coords = Vector2i(entry.atlas[0],entry.atlas[1])
			atlas.create_tile(coords)
			atlas.set_tile_animation_columns(coords,1)
			atlas.set_tile_animation_separation(coords,Vector2i(0,3))
			atlas.set_tile_animation_frames_count(coords,16)
			atlas.set_tile_animation_speed(coords,16.0/1.2)
			atlas.get_tile_data(coords,0).set_custom_data("flow_profile",entry.id)
		check(ResourceSaver.save(tiles,folder+"river_junctions.tres")==OK,"Save junction TileSet")
		layer.tile_set = load(folder+"river_junctions.tres")
		layer.clear()
		for index in range(manifest.profiles.size()):
			var entry = manifest.profiles[index]
			var center = Vector2i((index%8)*6+2,(index/8)*6+2)
			for y in range(-2,3):
				for x in range(-2,3):
					layer.set_cell(center+Vector2i(x,y),1,Vector2i.ZERO,0)
			layer.set_cell(center,0,Vector2i(entry.atlas[0],entry.atlas[1]),0)
			for port in PORTS:
				if not port in entry.inflows and not port in entry.outflows:
					continue
				var incoming = port in entry.inflows
				var direction = 0
				match port:
					"north": direction = 0 if incoming else 1
					"south": direction = 1 if incoming else 0
					"west": direction = 2 if incoming else 3
					"east": direction = 3 if incoming else 2
				for distance in [1,2]:
					layer.set_cell(center+PORTS[port]*distance,2,Vector2i(0,direction),0)
		for cell in layer.get_used_cells():
			check(layer.get_cell_tile_data(cell)!=null,"Missing junction showcase cell")
		scene.name = "DirectedJunctionCandidates"
		var packed = PackedScene.new()
		check(packed.pack(scene)==OK,"Pack junction review")
		check(ResourceSaver.save(packed,folder+"river_junctions_review.tscn")==OK,"Save junction review")
		scene.free()
	print("RIVER JUNCTIONS ","PASSED" if errors.is_empty() else "FAILED")
	quit(0 if errors.is_empty() else 1)
