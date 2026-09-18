extends SceneTree

# FIX-15: what the climate makes of the ground, measured on the generated field (TerrainClimateShareSmoke).
#
# The recipe is Oilfield Days' shape - Continents, land 0.6, climate maps and scale rules on - with the
# game's own span rule, span = kilometres / 10000 at 6 cells a kilometre, unless a check says otherwise. The
# lab maps keep the scale rules' own span: a standard 64x64 (seed 2027) and a large 96x60 (seed 31415).
# Climate shares are of INLAND cells: land that is not beach sand, whose width is the beach setting's, not
# the climate's. Woodland shares are of all land.
#
#   - Size does not make desert. A temperate, normal-rainfall map is at most 10% desert and at least 70%
#     grass and dry grass at 32, 64, 96 and 144 cells, two seeds each, and its desert share does not rise
#     with size. With the coastal reach fixed at 6.5 cells, 144 cells came out 44-52% desert.
#   - Temperature reaches the ground, through the dry belt. On both lab maps a hot, normal-rainfall world is
#     at least 15% desert and at least 15 points more desert than a temperate one; on the basin, hot is at
#     least 30 points more desert and dry grass than temperate. With the belt where it was before FIX-15
#     (31 degrees, peak 0.20), hot lab maps had no desert at all and the hot basin was 10% desert and dry
#     grass against 12% temperate.
#   - Rainfall reaches water, not the ground. On both lab maps and the basin, temperate: arid is at most 5
#     points more desert than normal, has fewer lakes than normal, and rivers grow arid < normal < wet. With
#     Rainfall shifting every tile's moisture (-0.35 arid) instead, arid was 98-100% desert on the lab maps
#     and 33% on the basin; with Rainfall not scaling water, lakes and rivers did not move.
#   - A whole world keeps an interior. At span one, land 12 or more cells from water is at least 15 points
#     drier (desert and dry grass) than land within 3 cells of it. With a 60,000 km coastal reach, seed
#     31415's interior was 18% dry against 11% on its coast.
#   - Woods follow their own field, not the ranking blocks. On the basin (two seeds) and a standard map,
#     at most three times chance of woodland edges lie on a TerrainFeatureStage block boundary, chance
#     being one edge in BlockTiles (4%). With the stand field scaled to the landmasses, a hundred cells a
#     wavelength, 16-25% did; with a 40-cell wavelength, 13-20%. This is the check on stands grown too
#     coarse. The count of stands is not: every block ranks its own share of woodland, which holds the
#     count up - both coarse fields still made 30-40 stands against 56-60.
#   - Woodland is what shipped strategy maps grow: Civilization VI's forest caps of 14, 18 and 22% of land
#     for arid, normal and wet rainfall. On the basin, two seeds: normal 15-21%, arid 11-17%, wet 19-25%,
#     and arid at least 2 points under normal and wet at least 2 over it. With Rainfall not scaling
#     vegetation the basin grew 17.2-17.5% at every rainfall; with a reference share of 0.08 or 0.26 it
#     grew 7.3-7.4% or 25.2-25.3% at normal.
#   - Woodland comes in forests. On the basin at normal rainfall, two seeds: a median stand of at least 18
#     cells and at least 70% of the woodland in stands of 25 cells or more. With a 6-cell wavelength the
#     median was 12-13 cells and 37-47% of the woodland was in such stands. Stands also shrink with
#     coverage, which the share check bounds.

const INLAND_EXCLUDES := ["sand"]
const SEEDS := [31415, 2027]
const BASIN := Vector2i(144, 144)
const BASIN_SEEDS := [12345, 2027]
const LAB_MAPS := [[Vector2i(64, 64), 2027, "the standard lab map"], [Vector2i(96, 60), 31415, "the large lab map"]]
const TEMPERATE := 1
const HOT := 2
const ARID := 0
const NORMAL := 1
const WET := 2

var failures: Array[String] = []
var smoke: Node
var basin_woodland := {}

func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		push_error(message)

func _initialize() -> void:
	call_deferred("run")

func game_span(size: int) -> float:
	return (size / 6.0) / 10000.0

# Inland shares of the climate's ground kinds on one map, with its lake and river coverage. A span of zero
# keeps the scale rules' own span.
func shares_on(size: Vector2i, temperature: int, rainfall: int, seed: int, span: float) -> Dictionary:
	var counts: Dictionary = smoke.call("Shares", size, temperature, rainfall, seed, span)
	var inland: int = counts["land"]
	for kind in INLAND_EXCLUDES:
		inland -= int(counts.get(kind, 0))
	var result := {"inland": inland, "lakes": float(counts["lake_coverage"]), "rivers": float(counts["river_coverage"])}
	for kind in ["desert", "dry_grass", "grass", "jungle"]:
		result[kind] = float(counts.get(kind, 0)) / maxi(1, inland)
	return result

func shares(size: int, temperature: int, rainfall: int, seed: int) -> Dictionary:
	return shares_on(Vector2i(size, size), temperature, rainfall, seed, game_span(size))

# The temperate basin's woodland at one rainfall, generated once for all the checks that read it.
func woodland_on_basin(rainfall: int, seed: int) -> Dictionary:
	var key := "%d/%d" % [rainfall, seed]
	if not basin_woodland.has(key):
		basin_woodland[key] = smoke.call("Woodland", BASIN, TEMPERATE, rainfall, seed, game_span(BASIN.x))
		print("[terrain-climate-share] woodland on the basin, rainfall %d seed %d: %s" % [rainfall, seed, basin_woodland[key]])
	return basin_woodland[key]

func wooded(woodland: Dictionary) -> float:
	return float(woodland["woods"] + woodland["forest"]) / maxi(1, woodland["land"])

func size_does_not_make_desert() -> bool:
	for seed in SEEDS:
		var smallest := -1.0
		for size in [32, 64, 96, 144]:
			var at := shares(size, TEMPERATE, NORMAL, seed)
			print("[terrain-climate-share] temperate/normal %dx%d seed %d: %s" % [size, size, seed, at])
			check(at["inland"] > 100, "the %d-cell fixture has only %d inland cells (seed %d)" % [size, at["inland"], seed])
			check(at["desert"] <= 0.10, "a temperate %d-cell map is %.0f%% desert (seed %d)" % [size, 100.0 * at["desert"], seed])
			check(at["grass"] + at["dry_grass"] >= 0.70, "a temperate %d-cell map is only %.0f%% grass and dry grass (seed %d)" % [size, 100.0 * (at["grass"] + at["dry_grass"]), seed])
			if size == 32:
				smallest = at["desert"]
			elif size == 144:
				check(at["desert"] <= smallest + 0.02, "desert rose with map size: %.0f%% at 32 cells, %.0f%% at 144 (seed %d)" % [100.0 * smallest, 100.0 * at["desert"], seed])
	return true

func temperature_reaches_the_ground() -> bool:
	for lab in LAB_MAPS:
		var temperate := shares_on(lab[0], TEMPERATE, NORMAL, lab[1], 0.0)
		var hot := shares_on(lab[0], HOT, NORMAL, lab[1], 0.0)
		print("[terrain-climate-share] %s: temperate %s, hot %s" % [lab[2], temperate, hot])
		check(hot["inland"] > 100, "%s has only %d inland cells" % [lab[2], hot["inland"]])
		check(hot["desert"] >= 0.15, "a hot world on %s is only %.0f%% desert" % [lab[2], 100.0 * hot["desert"]])
		check(hot["desert"] >= temperate["desert"] + 0.15, "a hot world on %s is %.0f%% desert against %.0f%% temperate" % [lab[2], 100.0 * hot["desert"], 100.0 * temperate["desert"]])
	var basin_temperate := shares(BASIN.x, TEMPERATE, NORMAL, BASIN_SEEDS[0])
	var basin_hot := shares(BASIN.x, HOT, NORMAL, BASIN_SEEDS[0])
	print("[terrain-climate-share] the basin: temperate %s, hot %s" % [basin_temperate, basin_hot])
	var temperate_dry: float = basin_temperate["desert"] + basin_temperate["dry_grass"]
	var hot_dry: float = basin_hot["desert"] + basin_hot["dry_grass"]
	check(hot_dry >= temperate_dry + 0.30, "a hot basin is %.0f%% desert and dry grass against %.0f%% temperate" % [100.0 * hot_dry, 100.0 * temperate_dry])
	return true

func rainfall_reaches_water_not_ground() -> bool:
	var maps: Array = []
	for lab in LAB_MAPS:
		maps.append([lab[0], lab[1], 0.0, lab[2]])
	maps.append([BASIN, BASIN_SEEDS[0], game_span(BASIN.x), "the basin"])
	for map in maps:
		var arid := shares_on(map[0], TEMPERATE, ARID, map[1], map[2])
		var normal := shares_on(map[0], TEMPERATE, NORMAL, map[1], map[2])
		var wet := shares_on(map[0], TEMPERATE, WET, map[1], map[2])
		print("[terrain-climate-share] rainfall on %s: arid %s, normal %s, wet %s" % [map[3], arid, normal, wet])
		check(arid["desert"] <= normal["desert"] + 0.05, "an arid temperate world on %s is %.0f%% desert against %.0f%% at normal rainfall" % [map[3], 100.0 * arid["desert"], 100.0 * normal["desert"]])
		check(arid["lakes"] < normal["lakes"], "an arid world on %s has %.2f%% lakes against %.2f%% at normal rainfall" % [map[3], 100.0 * arid["lakes"], 100.0 * normal["lakes"]])
		check(arid["rivers"] < normal["rivers"] and normal["rivers"] < wet["rivers"], "rivers on %s do not grow with rainfall: %.2f%%, %.2f%%, %.2f%%" % [map[3], 100.0 * arid["rivers"], 100.0 * normal["rivers"], 100.0 * wet["rivers"]])
	return true

func whole_world_keeps_an_interior() -> bool:
	for seed in SEEDS:
		var dryness: Dictionary = smoke.call("CoastAndInteriorDryness", Vector2i(128, 128), seed)
		print("[terrain-climate-share] whole world seed %d: %s" % [seed, dryness])
		check(dryness["coast_cells"] > 100 and dryness["interior_cells"] > 100, "the whole-world fixture has too little coast or interior to compare (seed %d): %s" % [seed, dryness])
		check(dryness["interior"] >= dryness["coast"] + 0.15, "a whole world's interior is not drier than its coast (seed %d): %s" % [seed, dryness])
	return true

func woods_follow_their_own_field() -> bool:
	var cases := []
	for seed in BASIN_SEEDS:
		cases.append(["Oilfield Days' basin", seed, woodland_on_basin(NORMAL, seed)])
	var standard: Dictionary = smoke.call("Woodland", LAB_MAPS[0][0], TEMPERATE, NORMAL, LAB_MAPS[0][1], 0.0)
	print("[terrain-climate-share] woodland on %s, seed %d: %s" % [LAB_MAPS[0][2], LAB_MAPS[0][1], standard])
	cases.append([LAB_MAPS[0][2], LAB_MAPS[0][1], standard])
	for case in cases:
		var woodland: Dictionary = case[2]
		check(woodland["edges"] > 200, "%s (seed %d) has too few woodland edges to judge: %s" % [case[0], case[1], woodland])
		for axis in ["block_edge_horizontal", "block_edge_vertical"]:
			check(woodland[axis] <= 3.0 * woodland["block_edge_chance"], "on %s (seed %d) %.0f%% of woodland edges lie on ranking-block boundaries (%s), where chance puts %.0f%%" % [case[0], case[1], 100.0 * woodland[axis], axis, 100.0 * woodland["block_edge_chance"]])
	return true

func woodland_is_what_strategy_maps_grow() -> bool:
	for seed in BASIN_SEEDS:
		var arid := wooded(woodland_on_basin(ARID, seed))
		var normal := wooded(woodland_on_basin(NORMAL, seed))
		var wet := wooded(woodland_on_basin(WET, seed))
		check(normal >= 0.15 and normal <= 0.21, "the basin at normal rainfall is %.1f%% woodland (seed %d)" % [100.0 * normal, seed])
		check(arid >= 0.11 and arid <= 0.17, "the basin when arid is %.1f%% woodland (seed %d)" % [100.0 * arid, seed])
		check(wet >= 0.19 and wet <= 0.25, "the basin when wet is %.1f%% woodland (seed %d)" % [100.0 * wet, seed])
		check(arid <= normal - 0.02 and wet >= normal + 0.02, "rainfall barely moves the basin's woodland: %.1f%%, %.1f%%, %.1f%% (seed %d)" % [100.0 * arid, 100.0 * normal, 100.0 * wet, seed])
	return true

func woodland_comes_in_forests() -> bool:
	for seed in BASIN_SEEDS:
		var woodland := woodland_on_basin(NORMAL, seed)
		check(woodland["median_stand"] >= 18, "the basin's median stand is %d cells (seed %d)" % [woodland["median_stand"], seed])
		check(woodland["in_large_stands"] >= 0.70, "only %.0f%% of the basin's woodland is in stands of %d cells or more (seed %d)" % [100.0 * woodland["in_large_stands"], woodland["large_stand_tiles"], seed])
	return true

func run() -> void:
	smoke = load("res://tests/TerrainClimateShareSmoke.cs").new()
	root.add_child(smoke)
	check(size_does_not_make_desert() == true, "the map-size checks did not run to the end")
	check(temperature_reaches_the_ground() == true, "the temperature checks did not run to the end")
	check(rainfall_reaches_water_not_ground() == true, "the rainfall checks did not run to the end")
	check(whole_world_keeps_an_interior() == true, "the whole-world checks did not run to the end")
	check(woods_follow_their_own_field() == true, "the woodland edge checks did not run to the end")
	check(woodland_is_what_strategy_maps_grow() == true, "the woodland share checks did not run to the end")
	check(woodland_comes_in_forests() == true, "the woodland stand checks did not run to the end")
	smoke.free()
	print("[terrain-climate-share] OK" if failures.is_empty() else "[terrain-climate-share] FAILED")
	quit(0 if failures.is_empty() else 1)
