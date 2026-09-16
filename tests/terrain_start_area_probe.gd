extends SceneTree

# FEAT-09: every start gets a reserved, validated, kitted area - and the facts hold on the map,
# not just in the report. Small (48x48), Continents, seed 31415, radius 10, default kit plus a
# CRITICAL wheat entry on a one-cell-wide distance band (exactly 2 cells out), so the band can
# rarely hold two wheat and the kit's relaxation order has to do real work.
#   - every area cell is dry, not mountainous, not lava, and 4-connected to its own start;
#   - no two areas are 8-adjacent (AreaGap 1);
#   - the headquarters footprint is level and lies inside its own area;
#   - each report's cell count matches the map; usable starts have >= ExitCount exits;
#   - a usable start holds its critical wheat; diagnostics count usable starts as the reports do;
#   - relaxation was used at least once (the band makes it necessary);
#   - TerrainDataLayersComponent, runtime and materialised, answers StartAreaAt as the generator
#     does and lists StartCells in the generator's start order.

const BASE := "res://addons/beep_game_builder_cs/ecs/terrain/"
const SIZE := Vector2i(48, 48)
const RADIUS := 10
const MIN_CELLS := 80
const LAYER_ORIGIN := Vector2i(5, 3)

var failures: Array[String] = []

func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		push_error(message)

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	var generator: Node = load(BASE + "TerrainGeneratorComponent.cs").new()
	generator.set("BoundsSize", SIZE)
	generator.set("Seed", 31415)
	generator.set("UseClimateBiomeMaps", true)
	generator.set("UseScaleRules", true)
	root.add_child(generator)
	generator.call("ApplyMapSetup", 0, 1, 1, 1, 1, 1)
	var kit: Resource = load(BASE + "TerrainStartKit.cs").new()
	var wheat: Resource = load(BASE + "TerrainStartKitEntry.cs").new()
	wheat.set("ResourceId", "wheat")
	wheat.set("Count", 2)
	wheat.set("MinDistance", 2)
	wheat.set("MaxDistance", 2)
	wheat.set("Critical", true)
	kit.get("Entries").append(wheat)
	# The default minimum (60% of the radius disc, 189 cells) is more than six starts on a 48x48
	# map have room for. 80 leaves this map with usable AND too-small areas, so both are measured.
	kit.set("MinAreaCells", MIN_CELLS)
	generator.set("StartKit", kit)
	generator.set("StartAreaRadius", RADIUS)

	var starts: Array = generator.call("GetStartPositions")
	var reports: Array = generator.call("GetStartAreaReports")
	var diagnostics: Dictionary = generator.call("GetGenerationDiagnostics")
	check(starts.size() > 0 and reports.size() == starts.size(), "one report per start (%d starts, %d reports)" % [starts.size(), reports.size()])

	var area := {}
	var counts := {}
	for y in SIZE.y:
		for x in SIZE.x:
			var cell := Vector2i(x, y)
			var id: int = generator.call("StartAreaAt", cell)
			if id == 0:
				continue
			area[cell] = id
			counts[id] = counts.get(id, 0) + 1
			check(generator.call("WaterSourceAt", cell) == "", "area cell %s is water" % cell)
			check(int(generator.call("ReliefAt", cell)) != 2, "area cell %s is mountainous" % cell)
			check(generator.call("TerrainKindAt", cell) != "lava", "area cell %s is lava" % cell)
			for dy in [-1, 0, 1]:
				for dx in [-1, 0, 1]:
					var other: int = generator.call("StartAreaAt", cell + Vector2i(dx, dy)) if Rect2i(Vector2i.ZERO, SIZE).has_point(cell + Vector2i(dx, dy)) else 0
					check(other == 0 or other == id, "areas %d and %d are 8-adjacent at %s" % [id, other, cell])

	var usable := 0
	var relaxed := 0
	for report in reports:
		var index: int = report["index"]
		var origin: Vector2i = report["origin"]
		check(origin == starts[index], "report %d origin differs from its start" % index)
		check(counts.get(index + 1, 0) == report["cell_count"], "report %d counts %d cells, the map has %d" % [index, report["cell_count"], counts.get(index + 1, 0)])
		var footprint: Vector2i = report["footprint"]
		var anchor_relief: int = generator.call("ReliefAt", origin)
		for fy in footprint.y:
			for fx in footprint.x:
				var cell := origin + Vector2i(fx, fy)
				check(int(generator.call("ReliefAt", cell)) == anchor_relief, "start %d footprint cell %s is not level" % [index, cell])
				check(area.get(cell, 0) == index + 1, "start %d footprint cell %s is outside its area" % [index, cell])
		# 4-connectivity: a flood from the origin over this area's cells reaches all of them.
		var seen := {origin: true}
		var frontier := [origin]
		while not frontier.is_empty():
			var at: Vector2i = frontier.pop_back()
			for step in [Vector2i.LEFT, Vector2i.RIGHT, Vector2i.UP, Vector2i.DOWN]:
				var next: Vector2i = at + step
				if area.get(next, 0) == index + 1 and not seen.has(next):
					seen[next] = true
					frontier.append(next)
		check(seen.size() == counts.get(index + 1, 0), "start %d area is not 4-connected (%d of %d reached)" % [index, seen.size(), counts.get(index + 1, 0)])
		var wheat_placed := 0
		for placement in report["placements"]:
			if placement["resource"] == "wheat":
				wheat_placed += 1
				check(generator.call("ResourceAt", placement["cell"]) == "wheat", "start %d wheat placement %s is not on the map" % [index, placement["cell"]])
				check(area.get(placement["cell"], 0) == index + 1, "start %d wheat placed outside its area" % index)
				if int(placement["relaxation"]) > 0:
					relaxed += 1
		check(("area_too_small" in report["problems"]) == (report["cell_count"] < MIN_CELLS),
			"start %d: area_too_small reported %s for %d cells against a minimum of %d" % [index, "area_too_small" in report["problems"], report["cell_count"], MIN_CELLS])
		if report["usable"]:
			usable += 1
			check(report["exits"] >= 2, "usable start %d has %d exits" % [index, report["exits"]])
			check(wheat_placed == 2, "usable start %d holds %d of its 2 critical wheat" % [index, wheat_placed])
		print("[terrain-start-area] start %d at %s: %d cells, %d exits, wheat %d, problems %s" % [index, origin, report["cell_count"], report["exits"], wheat_placed, report["problems"]])

	check(diagnostics["start_area_count"] == reports.size(), "diagnostics count %d areas" % diagnostics["start_area_count"])
	check(diagnostics["start_area_usable_count"] == usable, "diagnostics count %d usable, the reports %d" % [diagnostics["start_area_usable_count"], usable])
	check(usable > 0 and usable < reports.size(), "the fixture map must hold usable and unusable starts (%d of %d usable)" % [usable, reports.size()])
	check(relaxed > 0, "no wheat needed relaxation - the one-cell band no longer exercises the relaxation order")

	# The data layers publish the same areas and the same start ORDER in both modes, offset by
	# their BoundsOrigin: StartCells()[k] is start k, not whatever order a set or GetUsedCells gave.
	var layers: Node = load(BASE + "TerrainDataLayersComponent.cs").new()
	layers.set("RefreshOnReady", false)
	layers.set("BoundsOrigin", LAYER_ORIGIN)
	layers.set("BoundsSize", SIZE)
	root.add_child(layers)
	layers.set("TerrainGeneratorPath", layers.get_path_to(generator))
	for materialize in [false, true]:
		layers.set("MaterializeTileLayers", materialize)
		layers.call("Rebuild")
		var mode := "materialized" if materialize else "runtime"
		if materialize:
			# GetUsedCells answers in insertion order, which Rebuild makes the start order. Re-set
			# the start tiles in REVERSE, as an edit to the authored layer would, so only reading
			# each tile's start_index can still give start k at element k.
			var start_layer: TileMapLayer = layers.get("StartLayer")
			var used: Array[Vector2i] = start_layer.get_used_cells()
			var tiles := []
			for used_cell in used:
				tiles.append([used_cell, start_layer.get_cell_source_id(used_cell), start_layer.get_cell_atlas_coords(used_cell)])
			start_layer.clear()
			# clear() only marks cells for erasure; flush it, or re-setting a cell revives the
			# pending entry in its old slot and the insertion order never changes.
			start_layer.update_internals()
			tiles.reverse()
			for tile in tiles:
				start_layer.set_cell(tile[0], tile[1], tile[2])
		var cells: Array = layers.call("StartCells")
		check(cells.size() == starts.size(), "%s StartCells has %d cells for %d starts" % [mode, cells.size(), starts.size()])
		for k in mini(cells.size(), starts.size()):
			check(cells[k] == LAYER_ORIGIN + starts[k], "%s StartCells[%d] is %s, start %d is %s" % [mode, k, cells[k], k, LAYER_ORIGIN + starts[k]])
		var disagreements := 0
		for y in SIZE.y:
			for x in SIZE.x:
				var cell := Vector2i(x, y)
				if int(layers.call("StartAreaAt", LAYER_ORIGIN + cell)) != int(generator.call("StartAreaAt", cell)):
					disagreements += 1
		check(disagreements == 0, "%s StartAreaAt disagrees with the generator on %d cells" % [mode, disagreements])
	layers.free()

	generator.free()
	print("[terrain-start-area] usable %d of %d, relaxed placements %d" % [usable, reports.size(), relaxed])
	print("[terrain-start-area] OK" if failures.is_empty() else "[terrain-start-area] FAILED")
	quit(0 if failures.is_empty() else 1)
