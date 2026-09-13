@tool
extends "res://addons/beep_game_builder_cs/ecs/terrain/TerrainSeaDepthBinding.gd"

const PORTS = [Vector2i.UP,Vector2i.RIGHT,Vector2i.DOWN,Vector2i.LEFT]
const NAMES = ["north","east","south","west"]
const FLOW_NAMES = ["north_south","east_west","south_north","west_east"]
const FLOW_ROWS = [0,3,1,2]
const KINDS = ["narrow","low_bank","middle","high_bank"]
const MASKS = [[111,127,255,239],[207,239,255,223],[159,191,255,223],[63,127,255,191]]
const NEIGHBORS = [Vector2i.UP,Vector2i.RIGHT,Vector2i.DOWN,Vector2i.LEFT,Vector2i(1,-1),Vector2i(1,1),Vector2i(-1,1),Vector2i(-1,-1)]

func _normalize(mask: int) -> int:
	for i in range(4):
		if not mask&(1<<i) or not mask&(1<<((i+1)%4)): mask &= ~(1<<(i+4))
	return mask

func _contact(cell: Vector2i) -> Dictionary:
	if not _water.tile_set.get_meta("sea_depth_river_contacts_v1",false) or _water.get_cell_alternative_tile(cell)!=0: return {}
	var source = _water.get_cell_source_id(cell)
	var coords = _water.get_cell_atlas_coords(cell)
	var port = -1
	var kind = 0
	if source==1 and coords.y==0 and coords.x>=0 and coords.x<4:
		port = coords.x
	elif source==3 and coords.x>=0 and coords.x<3 and coords.y>=0 and coords.y<4:
		port = coords.y
		kind = coords.x+1
	if port<0: return {}
	var data = _water.get_cell_tile_data(cell)
	var profile = NAMES[port]+"_inlet"+("."+KINDS[kind] if kind>0 else "")
	if data==null or data.get_custom_data("flow_profile")!=profile: return {}
	return {"port":port,"kind":kind,"mask":MASKS[port][kind]}

func _supports_depth(cell: Vector2i) -> bool:
	return super._supports_depth(cell) or not _contact(cell).is_empty()

func _occupied(cell: Vector2i) -> bool:
	return super._supports_depth(cell) or not _contact(cell).is_empty() or (_water.get_cell_source_id(cell)==2 and _water.get_cell_tile_data(cell)!=null)

func _contact_error(cell: Vector2i,contact: Dictionary) -> String:
	var port: int = contact.port
	var kind: int = contact.kind
	var outside: Vector2i = cell+PORTS[port]
	var data = _water.get_cell_tile_data(outside)
	if _water.get_cell_source_id(outside)!=2 or _water.get_cell_alternative_tile(outside)!=0 or _water.get_cell_atlas_coords(outside)!=Vector2i(kind,FLOW_ROWS[port]) or data==null or data.get_custom_data("flow_profile")!=FLOW_NAMES[port]+"."+KINDS[kind]:
		return "Sea mouth needs the matching directed river section at "+str(outside)+"."
	if not super._supports_depth(cell-PORTS[port]): return "Sea mouth needs ordinary sea water on its inner side."
	var across = Vector2i.RIGHT if port%2==0 else Vector2i.DOWN
	if kind==1 or kind==2:
		var next = _contact(cell+across)
		if next.is_empty() or next.port!=port or not next.kind in [2,3]: return "Sea mouth is missing its next middle/high-bank section."
	if kind==2 or kind==3:
		var previous = _contact(cell-across)
		if previous.is_empty() or previous.port!=port or not previous.kind in [1,2]: return "Sea mouth is missing its previous low-bank/middle section."
	var mask = 0
	for b in range(8):
		if _occupied(cell+NEIGHBORS[b]): mask |= 1<<b
	if _normalize(mask)!=contact.mask: return "Sea mouth neighborhood differs from the authored bank profile."
	return ""

func refresh() -> String:
	var error = super.refresh()
	if not error.is_empty(): return error
	for cell in _water.get_used_cells():
		if not _water.get_cell_source_id(cell) in [1,3]: continue
		var contact = _contact(cell)
		if contact.is_empty(): return _report("Sea mouth profile is missing or has an unauthored transform.")
		error = _contact_error(cell,contact)
		if not error.is_empty(): return _report(error)
	return _report("")
