@tool
extends "res://addons/beep_game_builder_cs/ecs/terrain/TerrainSeaDepthBinding.gd"

const PORT_NAMES = ["north","east","south","west"]
const PORTS = [Vector2i.UP,Vector2i.RIGHT,Vector2i.DOWN,Vector2i.LEFT]
const NEIGHBORS = [Vector2i.UP,Vector2i.RIGHT,Vector2i.DOWN,Vector2i.LEFT,Vector2i(1,-1),Vector2i(1,1),Vector2i(-1,1),Vector2i(-1,-1)]
const PORT_MASKS = [111,207,159,63]
const WIDTH_MASKS = [[111,127,255,239],[207,239,255,223],[159,191,255,223],[63,127,255,191]]
const KINDS = ["narrow","low_bank","middle","high_bank"]
const FLOW_ROWS = [[0,1],[3,2],[1,0],[2,3]]
const FLOW_NAMES = ["north_south","south_north","west_east","east_west"]

func _normalize(mask: int) -> int:
	for i in range(4):
		if not mask&(1<<i) or not mask&(1<<((i+1)%4)): mask &= ~(1<<(i+4))
	return mask

func _mask_coords(mask: int) -> Vector2i:
	var index = 0
	for value in range(256):
		if _normalize(value)!=value: continue
		if value==mask: return Vector2i(index%8,index/8)
		index += 1
	return Vector2i(-1,-1)

func _connector(cell: Vector2i) -> int:
	if not _water.tile_set.get_meta("lake_depth_river_contacts_v1",false): return -1
	var coords = _water.get_cell_atlas_coords(cell)
	var data = _water.get_cell_tile_data(cell)
	if data==null or _water.get_cell_alternative_tile(cell)!=0: return -1
	var index = -1
	var suffix = ""
	if _water.get_cell_source_id(cell)==1 and coords.y==0 and coords.x>=0 and coords.x<8:
		index = coords.x
	elif _water.get_cell_source_id(cell)==3 and _water.tile_set.get_meta("lake_depth_wide_contacts_v1",false) and coords.x>=0 and coords.x<3 and coords.y>=0 and coords.y<8:
		index = coords.y
		suffix = "."+KINDS[coords.x+1]
	if index<0: return -1
	var expected = PORT_NAMES[index/2]+("_inlet" if index%2==0 else "_outlet")+suffix
	return index if data.get_custom_data("flow_profile")==expected else -1

func _connector_kind(cell: Vector2i) -> int:
	return _water.get_cell_atlas_coords(cell).x+1 if _water.get_cell_source_id(cell)==3 else 0

func _supports_depth(cell: Vector2i) -> bool:
	return super._supports_depth(cell) or _connector(cell)>=0

func _depth_region(cell: Vector2i) -> Vector2i:
	var coords = super._depth_region(cell)
	var connector = _connector(cell)
	if connector<0: return coords
	var target = coords.y*8+coords.x
	var index = 0
	for value in range(256):
		if _normalize(value)!=value: continue
		if index==target:
			# A painted shallow mouth continues into its unchanged shallow river.
			# Only the visual lookup changes; native paint and gameplay data do not.
			var port: int = connector/2
			var mask = value | (1<<port)
			var previous = (port+3)%4
			if mask&(1<<((port+1)%4)): mask |= 1<<(port+4)
			if mask&(1<<previous): mask |= 1<<(previous+4)
			return _mask_coords(_normalize(mask))
		index += 1
	return coords

func _river_valid(cell: Vector2i) -> bool:
	if not _water.tile_set.get_meta("lake_depth_river_contacts_v1",false) or _water.get_cell_source_id(cell)!=2: return false
	var coords = _water.get_cell_atlas_coords(cell)
	var data = _water.get_cell_tile_data(cell)
	var max_kind = 3 if _water.tile_set.get_meta("lake_depth_wide_contacts_v1",false) else 0
	return data!=null and coords.x>=0 and coords.x<=max_kind and coords.y>=0 and coords.y<4 and _water.get_cell_alternative_tile(cell)==0 and data.get_custom_data("flow_profile")==FLOW_NAMES[coords.y]+"."+KINDS[coords.x]

func _connector_error(cell: Vector2i,index: int) -> String:
	var port: int = index/2
	var kind = _connector_kind(cell)
	var outside: Vector2i = cell+PORTS[port]
	var inside: Vector2i = cell-PORTS[port]
	if not _river_valid(outside) or _water.get_cell_atlas_coords(outside)!=Vector2i(kind,FLOW_ROWS[port][index%2]):
		return "Lake connector requires the explicit matching inlet/outlet river flow at "+str(outside)+"."
	if not super._supports_depth(inside): return "Lake connector needs lake water on its inner side."
	var across = Vector2i.RIGHT if port%2==0 else Vector2i.DOWN
	if kind==1 or kind==2:
		if _connector(cell+across)!=index or not _connector_kind(cell+across) in [2,3]: return "Lake mouth needs its matching middle/high-bank neighbor."
	if kind==2 or kind==3:
		if _connector(cell-across)!=index or not _connector_kind(cell-across) in [1,2]: return "Lake mouth needs its matching low-bank/middle neighbor."
	var observed = 0
	for b in range(8):
		var neighbor: Vector2i = cell+NEIGHBORS[b]
		if super._supports_depth(neighbor) or _connector(neighbor)>=0 or _river_valid(neighbor): observed |= 1<<b
	if _normalize(observed)!=WIDTH_MASKS[port][kind]: return "Lake connector bank neighborhood does not match its authored profile."
	return ""

# The cell lookup and validation are shared; lake artwork and motion are not.
func _surface_role() -> String:
	return "lake_surface"

func _depth_role() -> String:
	return "lake_depth"

func refresh() -> String:
	var error = super.refresh()
	if not error.is_empty(): return error
	# Native cell regions avoid depending on Godot's padded runtime atlas UVs.
	var entries: Dictionary = {}
	var bounds = Rect2i()
	for cell in _water.get_used_cells():
		var data = _water.get_cell_tile_data(cell)
		if data==null: return _report("Lake shore tile is missing.")
		var coords = _water.get_cell_atlas_coords(cell)
		var role = 0
		if _water.get_cell_source_id(cell) in [1,3]:
			var connector = _connector(cell)
			if connector<0: return _report("Lake connector profile is missing or incompatible.")
			var contact_error = _connector_error(cell,connector)
			if not contact_error.is_empty(): return _report(contact_error)
			coords = _mask_coords(WIDTH_MASKS[connector/2][_connector_kind(cell)])
			role = 2+connector/2
		elif _water.get_cell_source_id(cell)==2:
			if not _river_valid(cell): return _report("River profile is not authored for this lake depth contact pack.")
			coords = _mask_coords(255)
			role = 1
		elif _water.get_cell_source_id(cell)==0:
			if data.terrain==-1: continue
			if data.terrain_set!=0 or data.terrain!=0 or _water.get_cell_alternative_tile(cell)!=0: return _report("Unsupported lake terrain or unauthored tile transform.")
		else:
			return _report("Unsupported lake source.")
		if coords.x<0 or coords.x>=8 or coords.y<0 or coords.y>=6:
			return _report("Lake shore mask region is outside the prepared band.")
		bounds = Rect2i(cell,Vector2i.ONE) if entries.is_empty() else bounds.merge(Rect2i(cell,Vector2i.ONE))
		entries[cell] = {"coords":coords,"role":role}
	if bounds.size.x>max_field_side or bounds.size.y>max_field_side:
		return _report("Lake shore field exceeds the configured limit; chunking is not implemented.")
	var size = bounds.size if not entries.is_empty() else Vector2i.ONE
	var image = Image.create(size.x,size.y,false,Image.FORMAT_RGBA8)
	image.fill(Color(0,0,0,1))
	for cell in entries:
		var coords: Vector2i = entries[cell].coords
		image.set_pixelv(cell-bounds.position,Color(coords.x/255.0,coords.y/255.0,1,entries[cell].role/255.0))
	_water.material.set_shader_parameter("shore_field_texture",ImageTexture.create_from_image(image))
	_water.material.set_shader_parameter("shore_field_origin",Vector2(bounds.position))
	_water.material.set_shader_parameter("shore_field_size",Vector2(size))
	return _report("")
