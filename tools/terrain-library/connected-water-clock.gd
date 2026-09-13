extends RefCounted

static func validate(tiles: TileSet) -> String:
	if tiles==null or tiles.get_source_count()==0: return "Missing water atlas sources"
	for source_index in range(tiles.get_source_count()):
		var source_id = tiles.get_source_id(source_index)
		var atlas = tiles.get_source(source_id) as TileSetAtlasSource
		if atlas==null or atlas.texture==null: return "Water clock requires textured atlas sources"
		var height = atlas.texture.get_height()
		if height%16!=0: return "Water atlas height must contain 16 equal frame bands"
		var band: int = height/16
		if atlas.get_tiles_count()==0: return "Water atlas contains no tiles"
		for index in range(atlas.get_tiles_count()):
			var coords = atlas.get_tile_id(index)
			var count = atlas.get_tile_animation_frames_count(coords)
			if count!=1 and count!=16: return "Water clock requires static tiles or 16-frame animations"
			if count==16 and abs(atlas.get_tile_animation_speed(coords)-16.0/1.2)>0.001: return "Water clock requires a 1.2-second loop"
			var first = atlas.get_tile_texture_region(coords,0)
			if first.position.y<0 or first.end.y>band: return "First water frame is outside band zero"
			if count==1:
				var pixels = atlas.texture.get_image()
				var expected = pixels.get_region(first).get_data()
				for frame in range(1,16):
					if pixels.get_region(Rect2i(first.position+Vector2i(0,band*frame),first.size)).get_data()!=expected: return "Static tile pixels differ between water clock bands"
				continue
			for frame in range(16):
				var region = atlas.get_tile_texture_region(coords,frame)
				if region!=Rect2i(first.position+Vector2i(0,band*frame),first.size): return "Water frames must occupy matching vertical bands"
	return ""

static func enable(layer: TileMapLayer) -> String:
	if layer==null or not layer.material is ShaderMaterial: return "Water clock requires a shader material"
	var error = validate(layer.tile_set)
	if not error.is_empty(): return error
	var material = layer.material as ShaderMaterial
	if material.shader==null: return "Water clock shader is missing"
	var supported = false
	for uniform in material.shader.get_shader_uniform_list():
		if uniform.name=="connected_water_clock": supported=true
	if not supported: return "Material does not support the connected water clock"
	material.set_shader_parameter("connected_water_clock",true)
	return ""
