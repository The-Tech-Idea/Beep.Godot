extends RefCounted

const DIRECTIONS = [Vector2i.UP,Vector2i.RIGHT,Vector2i.DOWN,Vector2i.LEFT]

static func validate_route(placements: Array, external_ports: Dictionary = {}) -> PackedStringArray:
	var issues = PackedStringArray()
	var cells = {}
	for placement in placements:
		if not placement is Dictionary or not placement.get("cell") is Vector2i or not placement.get("ports") is Array:
			issues.append("Each placement requires a logical cell and explicit ports.")
			continue
		var cell: Vector2i = placement.cell
		if cells.has(cell):
			issues.append("Duplicate flow placement at "+str(cell))
			continue
		var ports = {}
		for port in placement.ports:
			if port == null or not port.has_method("connection_issues"):
				issues.append("Missing connector at "+str(cell))
				continue
			var validation = port.validate()
			if not validation.is_empty():
				issues.append_array(validation)
				continue
			if ports.has(port.direction):
				issues.append("Ambiguous ports on the same side at "+str(cell))
			ports[port.direction] = port
		if ports.is_empty(): issues.append("No authored ports at "+str(cell))
		cells[cell] = ports
	if not issues.is_empty(): return issues
	for cell in cells:
		for direction in cells[cell]:
			var neighbor: Vector2i = cell+DIRECTIONS[direction]
			var opposite: int = (direction+2)%4
			if not cells.has(neighbor):
				if not direction in external_ports.get(cell,[]):
					issues.append("Unconnected port at %s side %s; declare an external endpoint or supply artwork." % [cell,direction])
				continue
			if not cells[neighbor].has(opposite):
				issues.append("Missing facing port between %s and %s." % [cell,neighbor])
				continue
			var compatibility = cells[cell][direction].connection_issues(cells[neighbor][opposite])
			for issue in compatibility:
				issues.append("%s -> %s: %s" % [cell,neighbor,issue])
	for cell in external_ports:
		for direction in external_ports[cell]:
			if not cells.has(cell) or not cells[cell].has(direction):
				issues.append("External endpoint does not match an authored port: "+str(cell))
	return issues
