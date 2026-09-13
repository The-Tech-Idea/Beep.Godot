@tool
extends Node

@export_node_path("TileMapLayer") var water_path: NodePath
@export_node_path("TileMapLayer") var depth_path: NodePath
var last_error: String = ""
var _water: TileMapLayer
var _depth: TileMapLayer
var _dirty: bool = true
var _queued: bool = false
var _transforms: Array = []
var _cell_data: Array = []

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
	if is_instance_valid(_water) and is_instance_valid(_depth):
		# Runtime cell erasure does not consistently emit changed in the target
		# build. Compare native data snapshots; scan peering bits only on changes.
		var cell_data = [_water.tile_map_data,_depth.tile_map_data]
		if cell_data!=_cell_data:
			_cell_data = cell_data
			_dirty = true
		var transforms = [_water.global_transform,_depth.global_transform]
		if transforms!=_transforms:
			_transforms = transforms
			_dirty = true
	if _dirty and not _queued:
		_queued = true
		call_deferred("refresh")

func _get_configuration_warnings() -> PackedStringArray:
	return PackedStringArray([last_error]) if not last_error.is_empty() else PackedStringArray()

func placement_error() -> String:
	if _water==null or _depth==null or _water==_depth: return "Depth requires separate water and depth layers."
	if _water.tile_set==null or _depth.tile_set==null: return "Depth layers require prepared TileSets."
	if _water.tile_set.get_meta("terrain_library_role","")!="sea_surface" or _depth.tile_set.get_meta("terrain_library_role","")!="sea_depth":
		return "Depth requires the shared sea surface and depth resources."
	if _water.tile_set.tile_size!=_depth.tile_set.tile_size or _water.tile_set.tile_shape!=_depth.tile_set.tile_shape or _water.tile_set.tile_layout!=_depth.tile_set.tile_layout:
		return "Depth and water projections or cell dimensions differ."
	if not _water.global_transform.is_equal_approx(_depth.global_transform): return "Depth and water layer transforms must match."
	for cell in _depth.get_used_cells():
		if _depth.get_cell_tile_data(cell)==null: return "Depth tile resource is missing at "+str(cell)+"."
		var data = _water.get_cell_tile_data(cell)
		if data==null or data.terrain_set!=0 or data.terrain!=0:
			return "Depth overlaps missing sea, land or a connector at "+str(cell)+". Paint depth only over open sea."
		for bit in range(16):
			if data.is_valid_terrain_peering_bit(bit) and data.get_terrain_peering_bit(bit)!=0:
				return "Depth overlaps a shoreline at "+str(cell)+". Coastal depth connectors are not authored yet."
	return ""

func refresh() -> String:
	_bind()
	_queued = false
	# Erasure cleanup may still be queued when the change signal is delivered.
	# Flush once for the batch before querying native used cells and terrain data.
	if _water!=null: _water.update_internals()
	if _depth!=null: _depth.update_internals()
	_dirty = false
	var error = placement_error()
	if _depth!=null:
		var material = _depth.material as ShaderMaterial
		var permitted_uniform = false
		if material!=null and material.shader!=null:
			for uniform in material.shader.get_shader_uniform_list():
				if str(uniform.name)=="depth_permitted": permitted_uniform = true
		if not permitted_uniform or not material.resource_local_to_scene:
			error = "Depth requires its scene-local shared sea material and depth_permitted uniform."
		else:
			# Suppress only depth pixels. Paint data, visibility, collision and navigation
			# remain untouched; fixing placement restores the overlay automatically.
			if material.get_shader_parameter("depth_permitted")!=error.is_empty():
				material.set_shader_parameter("depth_permitted",error.is_empty())
	if error!=last_error:
		last_error = error
		update_configuration_warnings()
	return error
