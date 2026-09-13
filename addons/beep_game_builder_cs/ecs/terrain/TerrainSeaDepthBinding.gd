@tool
extends Node

@export_node_path("TileMapLayer") var water_path: NodePath
@export_node_path("TileMapLayer") var depth_path: NodePath
@export_range(1,2048,1) var max_field_side: int = 1024
var last_error: String = ""
var field_origin: Vector2i = Vector2i.ZERO
var field_size: Vector2i = Vector2i.ONE
var mapped_cells: int = 0
var field_uploads: int = 0
var _water: TileMapLayer
var _depth: TileMapLayer
var _dirty: bool = true
var _queued: bool = false
var _state: Array = []

func _surface_role() -> String:
	return "sea_surface"

func _depth_role() -> String:
	return "sea_depth"

func _supports_depth(cell: Vector2i) -> bool:
	var data = _water.get_cell_tile_data(cell)
	return data!=null and data.terrain_set==0 and data.terrain==0 and _water.get_cell_source_id(cell)==0 and _water.get_cell_alternative_tile(cell)==0

func _depth_region(cell: Vector2i) -> Vector2i:
	return _depth.get_cell_atlas_coords(cell)

func _mark_dirty() -> void:
	_dirty = true

func _bind() -> void:
	var water = get_node_or_null(water_path) as TileMapLayer
	var depth = get_node_or_null(depth_path) as TileMapLayer
	if water==_water and depth==_depth: return
	for layer in [_water,_depth]:
		if is_instance_valid(layer) and layer.changed.is_connected(_mark_dirty): layer.changed.disconnect(_mark_dirty)
	_water = water
	_depth = depth
	for layer in [_water,_depth]:
		if is_instance_valid(layer) and not layer.changed.is_connected(_mark_dirty): layer.changed.connect(_mark_dirty)
	_dirty = true

func _process(_delta: float) -> void:
	_bind()
	if _water!=null and _depth!=null:
		var state = [_water.tile_map_data,_depth.tile_map_data,_water.global_transform,_depth.global_transform,_water.material,_depth.material,max_field_side]
		if state!=_state:
			_state = state
			_dirty = true
	if _dirty and not _queued:
		_queued = true
		call_deferred("refresh")

func _get_configuration_warnings() -> PackedStringArray:
	return PackedStringArray([last_error]) if not last_error.is_empty() else PackedStringArray()

func _report(error: String) -> String:
	if _water!=null and _water.material is ShaderMaterial:
		var material = _water.material as ShaderMaterial
		if material.resource_local_to_scene:
			material.set_shader_parameter("depth_field_enabled",error.is_empty())
	if last_error!=error:
		last_error = error
		update_configuration_warnings()
	return error

func refresh() -> String:
	_bind()
	_queued = false
	_dirty = false
	if _water==null or _depth==null or _water==_depth: return _report("Coastal depth needs separate Water and Depth layers.")
	_water.update_internals()
	_depth.update_internals()
	if _water.tile_set==null or _depth.tile_set==null: return _report("Missing prepared depth/sea TileSet.")
	if _water.tile_set.get_meta("terrain_library_role","")!=_surface_role() or _depth.tile_set.get_meta("terrain_library_role","")!=_depth_role():
		return _report("Depth requires compatible "+_surface_role()+" and "+_depth_role()+" resources.")
	if _water.tile_set.tile_size!=_depth.tile_set.tile_size or _water.tile_set.tile_shape!=_depth.tile_set.tile_shape or _water.tile_set.tile_layout!=_depth.tile_set.tile_layout:
		return _report("Depth and sea projections or cells do not match.")
	if not _water.global_transform.is_equal_approx(_depth.global_transform): return _report("Depth and sea transforms do not match.")
	var material = _water.material as ShaderMaterial
	var depth_material = _depth.material as ShaderMaterial
	if material==null or depth_material==null or material.shader==null or depth_material.shader==null or not material.resource_local_to_scene or not depth_material.resource_local_to_scene:
		return _report("Coastal depth needs the supplied scene-local materials.")
	var uniforms: Dictionary = {}
	for uniform in material.shader.get_shader_uniform_list(): uniforms[str(uniform.name)] = true
	for required in ["depth_field_enabled","depth_field_texture","depth_mask_texture","depth_field_origin","depth_field_size","depth_cell_bias"]:
		if not uniforms.has(required): return _report("Sea shader is missing "+required+".")
	var depth_uniform = false
	for uniform in depth_material.shader.get_shader_uniform_list():
		if str(uniform.name)=="depth_permitted": depth_uniform = true
	if not depth_uniform: return _report("Depth shader is missing its overlay permission control.")
	if depth_material.get_shader_parameter("depth_permitted")!=false: depth_material.set_shader_parameter("depth_permitted",false)
	if max_field_side<1: return _report("Depth field size limit must be positive.")
	var atlas = _depth.tile_set.get_source(0) as TileSetAtlasSource
	var size = _depth.tile_set.tile_size
	if atlas==null or atlas.texture==null or atlas.texture.get_size()!=Vector2(size.x*8,size.y*6*16):
		return _report("Depth mask atlas does not match the prepared layout.")
	var entries: Dictionary = {}
	var bounds = Rect2i()
	for cell in _depth.get_used_cells():
		var data = _depth.get_cell_tile_data(cell)
		if data==null: return _report("Missing depth tile at "+str(cell)+".")
		if data.terrain==-1: continue
		if data.terrain_set!=0 or data.terrain!=0 or _depth.get_cell_source_id(cell)!=0 or _depth.get_cell_alternative_tile(cell)!=0:
			return _report("Unsupported depth tile at "+str(cell)+".")
		if not _supports_depth(cell):
			return _report("Depth overlaps land, missing water or a river connector at "+str(cell)+".")
		var coords = _depth_region(cell)
		if coords.x<0 or coords.x>=8 or coords.y<0 or coords.y>=6:
			return _report("Depth region is outside the prepared mask band.")
		bounds = Rect2i(cell,Vector2i.ONE) if entries.is_empty() else bounds.merge(Rect2i(cell,Vector2i.ONE))
		entries[cell] = coords
	if bounds.size.x>max_field_side or bounds.size.y>max_field_side:
		return _report("Depth field exceeds the configured size limit; chunked depth fields are not implemented.")
	field_origin = bounds.position
	field_size = bounds.size if not entries.is_empty() else Vector2i.ONE
	mapped_cells = entries.size()
	var image = Image.create(field_size.x,field_size.y,false,Image.FORMAT_RGBA8)
	image.fill(Color(0,0,0,1))
	for cell in entries:
		var coords: Vector2i = entries[cell]
		image.set_pixelv(cell-field_origin,Color(coords.x/255.0,coords.y/255.0,1.0,1.0))
	material.set_shader_parameter("depth_field_texture",ImageTexture.create_from_image(image))
	material.set_shader_parameter("depth_mask_texture",atlas.texture)
	material.set_shader_parameter("depth_field_origin",Vector2(field_origin))
	material.set_shader_parameter("depth_field_size",Vector2(field_size))
	var center = _water.to_global(_water.map_to_local(Vector2i.ZERO)) - Vector2(material.get_shader_parameter("surface_origin"))
	var u: Vector2 = material.get_shader_parameter("surface_u")
	var v: Vector2 = material.get_shader_parameter("surface_v")
	var repeats: float = material.get_shader_parameter("surface_repeat_cells")
	material.set_shader_parameter("depth_cell_bias",Vector2(0.5,0.5)-Vector2(center.dot(u),center.dot(v))*repeats)
	material.set_shader_parameter("depth_mask_cell_size",Vector2(size))
	material.set_shader_parameter("depth_mask_texture_size",atlas.texture.get_size())
	field_uploads += 1
	_dirty = false
	return _report("")
