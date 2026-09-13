extends SceneTree

const BASE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/elevation/staging/isometric_granite_faces_v1/"

func _initialize() -> void:
	var errors = []
	var tiles = TileSet.new()
	tiles.tile_shape = TileSet.TILE_SHAPE_ISOMETRIC
	tiles.tile_layout = TileSet.TILE_LAYOUT_DIAMOND_DOWN
	tiles.tile_size = Vector2i(64,32)
	var fields = [["art_face",TYPE_STRING],["art_sequence_index",TYPE_INT],["visual_rise_pixels",TYPE_INT],["art_material",TYPE_STRING]]
	for i in range(fields.size()):
		tiles.add_custom_data_layer()
		tiles.set_custom_data_layer_name(i,fields[i][0])
		tiles.set_custom_data_layer_type(i,fields[i][1])
	for face in range(2):
		var name = "light" if face==0 else "shade"
		for index in range(4):
			var source = TileSetAtlasSource.new()
			source.texture = load(BASE+"runtime/%s_module_%d.tres" % [name,index])
			source.texture_region_size = Vector2i(32,80)
			tiles.add_source(source,face*4+index)
			source.create_tile(Vector2i.ZERO)
			var data = source.get_tile_data(Vector2i.ZERO,0)
			data.texture_origin = Vector2i(16 if face==0 else -16,24)
			data.set_custom_data("art_face",name)
			data.set_custom_data("art_sequence_index",index)
			data.set_custom_data("visual_rise_pixels",64)
			data.set_custom_data("art_material","grass_granite")
		var pattern = TileMapPattern.new()
		for i in range(4):
			pattern.set_cell(Vector2i(i,0) if face==0 else Vector2i(0,i),i if face==0 else 7-i,Vector2i.ZERO,0)
		tiles.add_pattern(pattern)
		if ResourceSaver.save(pattern,BASE+"runtime/"+name+"_span_pattern.tres")!=OK: errors.append("Pattern save failed")
	if ResourceSaver.save(tiles,BASE+"runtime/face_tiles.tres")!=OK: errors.append("TileSet save failed")
	var scene = load(BASE+"faces_review.tscn").instantiate()
	scene.name = "NativeGraniteFaceReview"
	for name in ["LightFace","ShadeFace"]:
		var old = scene.get_node(name)
		scene.remove_child(old)
		old.free()
	for face in range(2):
		var layer = TileMapLayer.new()
		layer.name = "LightFace" if face==0 else "ShadeFace"
		layer.tile_set = load(BASE+"runtime/face_tiles.tres")
		layer.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
		layer.y_sort_enabled = true
		layer.set_pattern(Vector2i(0,3) if face==0 else Vector2i(3,0),layer.tile_set.get_pattern(face))
		scene.add_child(layer)
		layer.owner = scene
	var packed = PackedScene.new()
	var result = packed.pack(scene)
	if result==OK: result = ResourceSaver.save(packed,BASE+"native_faces_review.tscn")
	if result!=OK: errors.append("Scene save failed")
	scene.free()
	var manifest = JSON.parse_string(FileAccess.get_file_as_string(BASE+"manifest.json"))
	manifest.nativeAuthoring = {"tileSet":"runtime/face_tiles.tres","scene":"native_faces_review.tscn","patterns":["runtime/light_span_pattern.tres","runtime/shade_span_pattern.tres"],"layers":["LightFace","ShadeFace","PlateauSurface"],"matchingMode":"authored_structural_patterns","requiresCSharp":false,"navigationFromVisualRise":false,"arbitrarySequenceValidated":false}
	var file = FileAccess.open(BASE+"manifest.json",FileAccess.WRITE)
	file.store_string(JSON.stringify(manifest,"  "))
	print("NATIVE ISOMETRIC FACE TILES ","PASSED" if errors.is_empty() else "FAILED")
	quit(0 if errors.is_empty() else 1)
