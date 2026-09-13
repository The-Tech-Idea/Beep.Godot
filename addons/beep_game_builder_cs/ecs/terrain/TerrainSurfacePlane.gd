@tool
extends Node2D

@export_enum("Square", "Isometric") var projection: int = 0
@export_range(1, 64, 1) var repeat_cells: int = 4
@export var cell_size: Vector2 = Vector2(64, 64)
@export var layer_paths: Array[NodePath] = []
var last_error: String = ""
var _materials: Dictionary = {}
var _states: Dictionary = {}

func _process(_delta: float) -> void:
	var error = refresh()
	if error != last_error:
		last_error = error
		update_configuration_warnings()

func _get_configuration_warnings() -> PackedStringArray:
	return PackedStringArray([last_error]) if not last_error.is_empty() else PackedStringArray()

func refresh() -> String:
	if repeat_cells < 1 or cell_size.x <= 0 or cell_size.y <= 0:
		return "Surface repeats and cell dimensions must be positive."
	var u = Vector2(cell_size.x, 0) * repeat_cells
	var v = Vector2(0, cell_size.y) * repeat_cells
	if projection == 1:
		u = Vector2(cell_size.x, cell_size.y) * repeat_cells * 0.5
		v = Vector2(-cell_size.x, cell_size.y) * repeat_cells * 0.5
	var basis = global_transform * Transform2D(u, v, Vector2.ZERO)
	if abs(basis.determinant()) < 0.000001:
		return "Surface plane transform is singular; previous material coordinates are retained."
	var targets: Array[CanvasItem] = []
	for path in layer_paths:
		var layer = get_node_or_null(path) as CanvasItem
		if layer == null:
			return "Surface layer is missing: " + str(path)
		if layer is TileMapLayer:
			if layer.tile_set == null:
				return "Surface layer is missing its TileSet: " + str(path)
			var expected = TileSet.TILE_SHAPE_ISOMETRIC if projection == 1 else TileSet.TILE_SHAPE_SQUARE
			if layer.tile_set.tile_shape != expected or Vector2(layer.tile_set.tile_size) != cell_size:
				return "Surface plane projection/cell dimensions do not match: " + str(path)
		elif not (layer is Sprite2D or layer is AnimatedSprite2D):
			return "Surface binding must be a TileMapLayer or explicit sprite: " + str(path)
		var material = layer.material as ShaderMaterial
		if material == null or material.shader == null:
			return "Surface layer requires a masked surface ShaderMaterial: " + str(path)
		var uniforms = {}
		for uniform in material.shader.get_shader_uniform_list():
			uniforms[str(uniform.name)] = true
		for required in ["surface_origin", "surface_u", "surface_v", "surface_enabled"]:
			if not uniforms.has(required):
				return "Surface shader is missing " + required + ": " + str(path)
		targets.append(layer)
	# Validate every target first, so a bad binding cannot partly update the group.
	var inverse = basis.affine_inverse()
	var surface_u = Vector2(inverse.x.x, inverse.y.x)
	var surface_v = Vector2(inverse.x.y, inverse.y.y)
	var material_state = [basis, repeat_cells]
	var live_ids = {}
	for layer in targets:
		var id = layer.get_instance_id()
		live_ids[id] = true
		if not _materials.has(id) or layer.material != _materials[id]:
			var owned = layer.material.duplicate() as ShaderMaterial
			owned.resource_local_to_scene = true
			layer.material = owned
			_materials[id] = owned
			_states.erase(id)
		if _states.get(id) != material_state:
			var material = _materials[id] as ShaderMaterial
			material.set_shader_parameter("surface_origin", basis.origin)
			material.set_shader_parameter("surface_u", surface_u)
			material.set_shader_parameter("surface_v", surface_v)
			# Water fields use physical cell coordinates independently of texture repeat.
			for uniform in material.shader.get_shader_uniform_list():
				if str(uniform.name) == "surface_repeat_cells":
					material.set_shader_parameter("surface_repeat_cells", float(repeat_cells))
			_states[id] = material_state
	for id in _materials.keys():
		if not live_ids.has(id):
			_materials.erase(id)
			_states.erase(id)
	return ""
