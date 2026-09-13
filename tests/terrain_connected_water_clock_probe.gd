extends SceneTree

const CLOCK = preload("res://tools/terrain-library/connected-water-clock.gd")
var errors = []

func check(ok: bool,message: String) -> void:
	if not ok: errors.append(message)

func _initialize() -> void:
	for projection in ["square","isometric"]:
		var original = load("res://addons/beep_game_builder_cs/generated/dev/cartoon/water/lake_mouth_sections_v1/"+projection+"/lake_mouth_sections.tres") as TileSet
		check(CLOCK.validate(original).is_empty(),"Valid atlas rejected")
		var bad = original.duplicate(true) as TileSet
		bad.get_source(2).set_tile_animation_frames_count(Vector2i.ZERO,15)
		check(not CLOCK.validate(bad).is_empty(),"Wrong frame count accepted")
		bad = original.duplicate(true)
		bad.get_source(2).set_tile_animation_speed(Vector2i.ZERO,8.0)
		check(not CLOCK.validate(bad).is_empty(),"Wrong period accepted")
		bad = original.duplicate(true)
		var atlas = bad.get_source(0) as TileSetAtlasSource
		var tested = false
		for index in range(atlas.get_tiles_count()):
			var coords = atlas.get_tile_id(index)
			if atlas.get_tile_animation_frames_count(coords)!=1: continue
			var image = atlas.texture.get_image().duplicate() as Image
			var first = atlas.get_tile_texture_region(coords,0)
			image.set_pixel(first.position.x,first.position.y+int(image.get_height()/16),Color.RED)
			atlas.texture = ImageTexture.create_from_image(image)
			tested = true
			break
		check(tested and not CLOCK.validate(bad).is_empty(),"Changing static background accepted")
		check(CLOCK.validate(original).is_empty(),"Validation mutated original resources")
	var horizontal = TileSet.new()
	horizontal.tile_size = Vector2i(8,8)
	var source = TileSetAtlasSource.new()
	source.texture = ImageTexture.create_from_image(Image.create(16,256,false,Image.FORMAT_RGBA8))
	source.texture_region_size = Vector2i(8,8)
	horizontal.add_source(source)
	source.create_tile(Vector2i.ZERO)
	source.set_tile_animation_columns(Vector2i.ZERO,2)
	source.set_tile_animation_frames_count(Vector2i.ZERO,16)
	source.set_tile_animation_speed(Vector2i.ZERO,16.0/1.2)
	check(not CLOCK.validate(horizontal).is_empty(),"Horizontal frame packing accepted")
	var layer = TileMapLayer.new()
	layer.tile_set = horizontal
	var material = ShaderMaterial.new()
	material.shader = load("res://addons/beep_game_builder_cs/shaders/terrain_lake_depth.gdshader")
	layer.material = material
	check(not CLOCK.enable(layer).is_empty(),"Invalid clock enabled")
	check(material.get_shader_parameter("connected_water_clock")!=true,"Rejected material was modified")
	layer.free()
	for error in errors: push_error(error)
	print("CONNECTED WATER CLOCK VALIDATION ","PASSED" if errors.is_empty() else "FAILED")
	quit(0 if errors.is_empty() else 1)
