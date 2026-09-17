extends SceneTree

# FEAT-14: how far every cell is from the nearest start, what that does to the underground, and the
# neutral sites between the starts - checked against the map, not only against the reports.
#
# Far country: a 112x80 Continents world, seed 31415, TWO starts and abundant Oil And Gas resources,
# read in full at scaling 0 and again at scaling 1. Oil And Gas, because its fields lie under the
# shelf and the dry ground as well as the rock: Historical deposits avoid the grassland a start is
# chosen on, which leaves too few near a start to compare means over. Nothing upstream of the
# start-area stage reads the scaling, so the two builds are the same world.
#   - scaling 0 measures nothing: StartDistanceAt is -1 on every cell, and there is no neutral report;
#   - scaling 1: every start reads 0, and every cell reads the rounded Euclidean distance to its
#     nearest start, computed here by brute force;
#   - every deposit keeps its kind and depth, and its richness is the scaling-0 richness times
#     lerp(0.5, 1.5, d / farthest), clamped to [0.05, 1]; no deposit appears or disappears and the
#     surface resources do not move - the pass scales, it never places;
#   - the far country is richer RELATIVE TO THE SAME MAP UNSCALED: the ratio of mean richness within
#     10 cells of a start to the mean beyond 30 falls. The plan's plain "near mean below far mean"
#     cannot fail on this map - its deposits are already richer far from the starts, and the check
#     still passed with the curve inverted - so the comparison is against the map's own baseline.
#
# Crowded starts: a 64x48 Continents world with its six starts, radius 10 areas and a kit whose
# Neutral entries are horses (surface), iron (underground) and an id no catalog holds. Six starts on
# that map put the band between them through the areas themselves, so the area filter is exercised:
#   - every neutral placement is outside every area and the kit's gap, dry, not mountainous, not
#     lava, in the band where its two nearest starts are within 2 cells, and on the map;
#   - no resource is placed more than Count x starts times, a shortfall is reported, the unknown id
#     is reported, and the diagnostics count what the report lists;
#   - a one-start map places nothing between its starts and says why.

const BASE := "res://addons/beep_game_builder_cs/ecs/terrain/"
const FAR_SIZE := Vector2i(112, 80)
const CROWDED_SIZE := Vector2i(64, 48)
const RESOURCES_OIL_AND_GAS := 1
const BAND_CELLS := 2.0
const AREA_GAP := 1
const MINIMUM_RICHNESS := 0.05

var failures: Array[String] = []

func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		push_error(message)

func _initialize() -> void:
	call_deferred("run")

func make_generator(size: Vector2i, resources: int) -> Node:
	var generator: Node = load(BASE + "TerrainGeneratorComponent.cs").new()
	generator.set("BoundsSize", size)
	generator.set("Seed", 31415)
	generator.set("UseClimateBiomeMaps", true)
	generator.set("UseScaleRules", true)
	root.add_child(generator)
	generator.call("ApplyMapSetup", 0, 1, 1, 1, 1, resources)
	return generator

# One build read in full, so later setting changes cannot mix two worlds into one reading.
func read_world(generator: Node, size: Vector2i) -> Dictionary:
	var world := {"distance": {}, "deposit": {}, "richness": {}, "depth": {}, "resource": {}}
	for y in size.y:
		for x in size.x:
			var cell := Vector2i(x, y)
			world["distance"][cell] = int(generator.call("StartDistanceAt", cell))
			world["deposit"][cell] = str(generator.call("UndergroundResourceAt", cell))
			world["richness"][cell] = float(generator.call("UndergroundRichnessAt", cell))
			world["depth"][cell] = int(generator.call("UndergroundDepthAt", cell))
			world["resource"][cell] = str(generator.call("ResourceAt", cell))
	world["starts"] = generator.call("GetStartPositions")
	world["neutral"] = generator.call("GetNeutralSiteReport")
	return world

func nearest_two(cell: Vector2i, starts: Array) -> Vector2:
	var first := INF
	var second := INF
	for start: Vector2i in starts:
		var d := Vector2(cell - start).length()
		if d < first:
			second = first
			first = d
		elif d < second:
			second = d
	return Vector2(first, second)

# Each section returns true from its last line. A runtime error aborts a GDScript function without
# recording a failure, so a section that did not finish would otherwise still end in OK.
func far_country() -> bool:
	var generator := make_generator(FAR_SIZE, 2)
	generator.set("StartPositionCount", 2)
	generator.set("ResourceSet", RESOURCES_OIL_AND_GAS)

	var unscaled := read_world(generator, FAR_SIZE)
	var measured := 0
	for cell in unscaled["distance"]:
		if unscaled["distance"][cell] != -1:
			measured += 1
	check(measured == 0, "scaling 0 measured a distance on %d cells" % measured)
	check(unscaled["neutral"]["placements"].is_empty() and unscaled["neutral"]["problems"].is_empty(),
		"a kit without Neutral entries reported neutral sites: %s" % unscaled["neutral"])

	generator.set("StartDistanceScaling", 1.0)
	var scaled := read_world(generator, FAR_SIZE)
	var starts: Array = scaled["starts"]
	check(starts.size() == 2, "the far-country fixture must hold two starts, it holds %d" % starts.size())
	check(starts == unscaled["starts"], "the scaling moved a start")
	for start: Vector2i in starts:
		check(scaled["distance"][start] == 0, "start %s reads %d, not 0" % [start, scaled["distance"][start]])

	var farthest := 1
	var wrong_distance := 0
	for cell: Vector2i in scaled["distance"]:
		var expected := roundi(nearest_two(cell, starts).x)
		if scaled["distance"][cell] != expected:
			wrong_distance += 1
			if wrong_distance <= 3:
				check(false, "cell %s reads distance %d, the nearest start is %d away" % [cell, scaled["distance"][cell], expected])
		farthest = maxi(farthest, expected)
	check(wrong_distance == 0, "%d cells read the wrong distance" % wrong_distance)

	var deposits := 0
	var wrong_richness := 0
	var near_total := 0.0
	var near_unscaled := 0.0
	var near_count := 0
	var far_total := 0.0
	var far_unscaled := 0.0
	var far_count := 0
	for cell: Vector2i in scaled["deposit"]:
		check(scaled["resource"][cell] == unscaled["resource"][cell], "the scaling changed the surface resource at %s" % cell)
		check(scaled["deposit"][cell] == unscaled["deposit"][cell] and scaled["depth"][cell] == unscaled["depth"][cell],
			"the scaling changed the deposit at %s: %s depth %d, was %s depth %d"
			% [cell, scaled["deposit"][cell], scaled["depth"][cell], unscaled["deposit"][cell], unscaled["depth"][cell]])
		if scaled["deposit"][cell] == "":
			continue
		deposits += 1
		var distance: int = scaled["distance"][cell]
		var factor := lerpf(0.5, 1.5, float(distance) / farthest)
		var expected := clampf(float(unscaled["richness"][cell]) * factor, MINIMUM_RICHNESS, 1.0)
		if absf(float(scaled["richness"][cell]) - expected) > 0.00001:
			wrong_richness += 1
			if wrong_richness <= 3:
				check(false, "deposit at %s (%d cells out) has richness %f, expected %f from %f x %f"
					% [cell, distance, scaled["richness"][cell], expected, unscaled["richness"][cell], factor])
		if distance <= 10:
			near_total += scaled["richness"][cell]
			near_unscaled += unscaled["richness"][cell]
			near_count += 1
		elif distance > 30:
			far_total += scaled["richness"][cell]
			far_unscaled += unscaled["richness"][cell]
			far_count += 1
	check(wrong_richness == 0, "%d of %d deposits carry the wrong richness" % [wrong_richness, deposits])
	check(near_count >= 10 and far_count >= 10,
		"the fixture needs deposits near and far from the starts (%d within 10, %d beyond 30)" % [near_count, far_count])
	if near_count > 0 and far_count > 0 and far_total > 0.0 and far_unscaled > 0.0:
		var ratio_scaled := (near_total / near_count) / (far_total / far_count)
		var ratio_unscaled := (near_unscaled / near_count) / (far_unscaled / far_count)
		check(ratio_scaled < ratio_unscaled,
			"scaling did not make the far country richer relative to the starts: near/far mean richness %.3f scaled, %.3f unscaled"
			% [ratio_scaled, ratio_unscaled])
		print("[terrain-start-distance] farthest %d cells; %d deposits; near/far mean richness %.3f scaled, %.3f unscaled (%d near, %d far)"
			% [farthest, deposits, ratio_scaled, ratio_unscaled, near_count, far_count])
	generator.free()
	return true

func crowded_starts() -> bool:
	var generator := make_generator(CROWDED_SIZE, 2)
	var kit: Resource = load(BASE + "TerrainStartKit.cs").new()
	kit.set("AreaGap", AREA_GAP)
	for id in ["horses", "iron", "no_such_resource"]:
		var entry: Resource = load(BASE + "TerrainStartKitEntry.cs").new()
		entry.set("ResourceId", id)
		entry.set("Count", 2)
		entry.set("Scope", 1)
		kit.get("Entries").append(entry)
	generator.set("StartKit", kit)
	generator.set("StartAreaRadius", 10)
	generator.set("StartDistanceScaling", 1.0)

	var starts: Array = generator.call("GetStartPositions")
	var report: Dictionary = generator.call("GetNeutralSiteReport")
	var diagnostics: Dictionary = generator.call("GetGenerationDiagnostics")
	var area := {}
	for y in CROWDED_SIZE.y:
		for x in CROWDED_SIZE.x:
			area[Vector2i(x, y)] = int(generator.call("StartAreaAt", Vector2i(x, y)))
	check(starts.size() >= 4, "the crowded fixture must hold several starts, it holds %d" % starts.size())

	# The band this probe computes for itself must cross the areas, or dropping the stage's area
	# filter could not put a site inside one and this fixture would not notice. Area cells are
	# claimable ground by construction, so each of these would be a candidate without the filter.
	var band_in_areas := 0
	for cell: Vector2i in area:
		var two := nearest_two(cell, starts)
		if two.y - two.x <= BAND_CELLS and area[cell] != 0:
			band_in_areas += 1
	check(band_in_areas > 0, "no band cell lies inside an area, so the area filter is not exercised")

	var placed := {}
	for placement: Dictionary in report["placements"]:
		var id: String = placement["resource"]
		var cell: Vector2i = placement["cell"]
		placed[id] = placed.get(id, 0) + 1
		var two := nearest_two(cell, starts)
		check(two.y - two.x <= BAND_CELLS + 0.0001,
			"neutral %s at %s is not between starts: nearest %.2f, next %.2f" % [id, cell, two.x, two.y])
		check(not within_gap_of_area(cell, area), "neutral %s at %s is in or beside a start area" % [id, cell])
		check(generator.call("WaterSourceAt", cell) == "", "neutral %s at %s is on water" % [id, cell])
		check(int(generator.call("ReliefAt", cell)) != 2, "neutral %s at %s is on a mountain" % [id, cell])
		check(generator.call("TerrainKindAt", cell) != "lava", "neutral %s at %s is on lava" % [id, cell])
		var on_map: String = generator.call("UndergroundResourceAt" if id == "iron" else "ResourceAt", cell)
		check(on_map == id, "neutral %s at %s is not on the map (the map says '%s')" % [id, cell, on_map])
	var problems: Array = report["problems"]
	for id in ["horses", "iron"]:
		var count: int = placed.get(id, 0)
		check(count > 0, "no neutral %s was placed, so the checks above are vacuous for it" % id)
		check(count <= 2 * starts.size(), "neutral %s placed %d times for %d starts at Count 2" % [id, count, starts.size()])
		check(count == 2 * starts.size() or ("missing:" + id) in problems,
			"neutral %s placed %d of %d and reported no shortfall" % [id, count, 2 * starts.size()])
	check("unknown_resource:no_such_resource" in problems, "the unknown neutral id was not reported: %s" % [problems])
	check(diagnostics["neutral_placements"] == report["placements"].size(),
		"diagnostics count %d neutral placements, the report lists %d" % [diagnostics["neutral_placements"], report["placements"].size()])
	print("[terrain-start-distance] crowded: %d starts, placed %s, problems %s, band cells inside areas %d"
		% [starts.size(), placed, problems, band_in_areas])

	generator.set("StartPositionCount", 1)
	var lonely: Dictionary = generator.call("GetNeutralSiteReport")
	check(lonely["placements"].is_empty(), "a one-start map placed %d neutral sites" % lonely["placements"].size())
	check("neutral_needs_two_starts:horses" in lonely["problems"],
		"a one-start map did not say why it placed no neutral horses: %s" % [lonely["problems"]])
	generator.free()
	return true

func within_gap_of_area(cell: Vector2i, area: Dictionary) -> bool:
	for dy in range(-AREA_GAP, AREA_GAP + 1):
		for dx in range(-AREA_GAP, AREA_GAP + 1):
			if area.get(cell + Vector2i(dx, dy), 0) != 0:
				return true
	return false

# What the kit stamps underground stands on ground that holds it, and a kit entry with a scope that
# is neither PerPlayer nor Neutral fails capture by name instead of being skipped by both passes.
func kit_invariants() -> bool:
	var smoke: Node = load("res://tests/TerrainStartKitSmoke.cs").new()
	root.add_child(smoke)
	var error: String = smoke.call("UndefinedScopeError")
	check(error.contains("horses") and error.contains("7"), "an undefined kit scope was accepted, or its error names neither the entry nor the scope: '%s'" % error)
	var support: Dictionary = smoke.call("DepositSupport", CROWDED_SIZE, 31415)
	check(support["player_iron"] > 0 and support["neutral_iron"] > 0,
		"the deposit fixture stamped no kit deposits to check: %s" % support)
	check(support["unsupported"] == 0, "%d underground cells lie under ground their resource does not support: %s" % [support["unsupported"], support])
	print("[terrain-start-distance] kit deposits %s" % support)
	smoke.free()
	return true

func run() -> void:
	check(far_country() == true, "the far-country checks did not run to the end")
	check(crowded_starts() == true, "the crowded-start checks did not run to the end")
	check(kit_invariants() == true, "the start-kit invariant checks did not run to the end")
	print("[terrain-start-distance] OK" if failures.is_empty() else "[terrain-start-distance] FAILED")
	quit(0 if failures.is_empty() else 1)
