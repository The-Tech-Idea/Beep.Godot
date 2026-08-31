extends SceneTree

# Peak materials must sit on peaks.
#
# Rock, gravel and snow are what high ground is made of. On level ground they are
# bare stone at sea level, which is not a biome - and not a cosmetic complaint
# either, because the renderer draws those tiles flat and grey in the middle of a
# meadow.
#
# It got there by a route worth remembering. The coherence stage dissolves biome
# regions too small for their landmass, and rock and gravel are valid
# destinations so a snow cap on a peak has somewhere to go. Nothing restricted
# that to peaks, so a FLAT region beside a rocky summit was absorbed into rock
# too. The tile reduction then preserved it rather than fixing it, because it
# takes a tile's terrain from the samples in its own relief band - and those flat
# samples were carrying rock.
#
# Measured before the fix, on a twelve-island chain: three islands came out 66 to
# 70% rock while only 9 to 11% of them was raised at all.
#
# The bar is ZERO rather than a small share. A peak material on flat ground has
# no legitimate cause, so any at all means the rule has sprung a leak somewhere.

const PEAK_MATERIALS := ["rock", "gravel", "snow"]

func flat_peaks(gen, size: Vector2i) -> Dictionary:
	var flat := 0
	var raised := 0
	for y in range(size.y):
		for x in range(size.x):
			var c := Vector2i(x, y)
			if not PEAK_MATERIALS.has(gen.TerrainKindAt(c)):
				continue
			if int(gen.ReliefAt(c)) > 0:
				raised += 1
			else:
				flat += 1
	return {"flat": flat, "raised": raised}

func _initialize() -> void:
	var root_node = load("res://addons/beep_game_builder_cs/templates/scenes/terrain/terrain_iso_demo.tscn").instantiate()
	get_root().add_child(root_node)
	for i in range(20): await process_frame
	var gen = root_node.find_child("TerrainGenerator", true, false)
	var failed := 0

	# Landform, island count, coverage, label. The island chain is the case that
	# exposed this: many small landmasses, each with a peak or two, so nearly
	# every biome region on them is small enough to be dissolved.
	var cases := [
		[2, 12, 0.22, "island chain"],
		[2, 7, 0.30, "archipelago"],
		[0, 3, 0.42, "continents"],
		[1, 1, 0.66, "pangaea"],
	]

	gen.BoundsSize = Vector2i(128, 80)
	for case in cases:
		gen.Landform = case[0]
		gen.ArchipelagoIslandCount = case[1]
		gen.LandmassScale = float(case[2])
		gen.GenerateTerrain()

		var m: Dictionary = flat_peaks(gen, Vector2i(128, 80))
		var ok: bool = int(m.flat) == 0
		if not ok:
			failed += 1
		print("%-13s peak material: %d on peaks, %d on FLAT ground  %s"
			% [case[3], m.raised, m.flat, "ok" if ok else "FAIL"])

	print("RESULT: ", "all checks passed" if failed == 0 else "%d FAILED" % failed)
	quit(1 if failed > 0 else 0)
