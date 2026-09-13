extends SceneTree

const BASE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/river_sections_v1/"
const GRASS = "res://addons/beep_game_builder_cs/generated/dev/cartoon/ground_masks/staging/runtime/surfaces_v1/grass_256.res"
var errors: Array[String] = []

func _initialize() -> void:
	call_deferred("run")

func check(ok: bool,message: String) -> void:
	if not ok:
		errors.append(message)
		push_error(message)

func run() -> void:
	var manifest = JSON.parse_string(FileAccess.get_file_as_string(BASE+"manifest.json"))
	for projection in ["square","isometric"]:
		var iso = projection=="isometric"
		var folder = BASE+projection+"/"
		var size = Vector2i(64,32 if iso else 64)
		var tiles = TileSet.new()
		tiles.tile_size = size
		if iso:
			tiles.tile_shape = TileSet.TILE_SHAPE_ISOMETRIC
			tiles.tile_layout = TileSet.TILE_LAYOUT_DIAMOND_DOWN
		tiles.add_custom_data_layer()
		tiles.set_custom_data_layer_name(0,"flow_profile")
		tiles.set_custom_data_layer_type(0,TYPE_STRING)
		var texture = ImageTexture.create_from_image(Image.load_from_file(folder+"river_sections_16.png"))
		check(ResourceSaver.save(texture,folder+"river_sections_16.res")==OK,"Cannot save section texture")
		var atlas = TileSetAtlasSource.new()
		atlas.texture = load(folder+"river_sections_16.res")
		atlas.texture_region_size = size
		tiles.add_source(atlas,0)
		var coords_by_id = {}
		for entry in manifest.profiles:
			var coords = Vector2i(entry.atlas[0],entry.atlas[1])
			atlas.create_tile(coords)
			atlas.set_tile_animation_columns(coords,1)
			atlas.set_tile_animation_separation(coords,Vector2i(0,3))
			atlas.set_tile_animation_frames_count(coords,16)
			atlas.set_tile_animation_speed(coords,16.0/1.2)
			atlas.get_tile_data(coords,0).set_custom_data("flow_profile",entry.id)
			coords_by_id[entry.id] = coords
		var grass_image = Image.create(size.x,size.y,false,Image.FORMAT_RGBA8)
		grass_image.fill(Color.BLUE)
		if iso:
			for y in range(size.y):
				for x in range(size.x):
					if abs((x+0.5-32)/32.0)+abs((y+0.5-16)/16.0)>1:
						grass_image.set_pixel(x,y,Color.TRANSPARENT)
		var grass = TileSetAtlasSource.new()
		grass.texture = ImageTexture.create_from_image(grass_image)
		grass.texture_region_size = size
		tiles.add_source(grass,1)
		grass.create_tile(Vector2i.ZERO)
		check(ResourceSaver.save(tiles,folder+"river_sections.tres")==OK,"Cannot save section TileSet")
		var scene = Node2D.new()
		scene.name = "RiverWidthsCandidate"
		var layer = TileMapLayer.new()
		layer.name = "Water"
		layer.tile_set = load(folder+"river_sections.tres")
		layer.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
		var material = ShaderMaterial.new()
		material.shader = load("res://addons/beep_game_builder_cs/shaders/terrain_mask_surface_cartoon.gdshader")
		material.set_shader_parameter("secondary_enabled",true)
		material.set_shader_parameter("secondary_texture",load(GRASS))
		layer.material = material
		scene.add_child(layer)
		layer.owner = scene
		var plane = load("res://addons/beep_game_builder_cs/ecs/terrain/TerrainSurfacePlane.gd").new()
		plane.name = "SurfacePlane"
		plane.projection = 1 if iso else 0
		plane.cell_size = Vector2(size)
		plane.layer_paths.assign([NodePath("../Water")])
		scene.add_child(plane)
		plane.owner = scene
		for y in range(8):
			for x in range(20):
				layer.set_cell(Vector2i(x,y),1,Vector2i.ZERO)
		var cursor = 1
		for example in manifest.widthExamples:
			for index in range(example.sections.size()):
				for y in range(8):
					layer.set_cell(Vector2i(cursor+index,y),0,coords_by_id["north_south."+str(example.sections[index])])
			cursor += int(example.width)+2
		var packed = PackedScene.new()
		check(packed.pack(scene)==OK,"Cannot pack width review")
		check(ResourceSaver.save(packed,folder+"river_widths_review.tscn")==OK,"Cannot save width review")
		scene.free()
	print("RIVER SECTIONS ","PASSED" if errors.is_empty() else "FAILED")
	quit(0 if errors.is_empty() else 1)
