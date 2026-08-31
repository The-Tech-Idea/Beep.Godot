extends SceneTree

# Woods must reach every landmass that can carry them.
#
# The vegetation field is ranked and cut at a percentile, and ranking it once
# over the WHOLE map lets the top slice land almost entirely on one landmass.
# Measured before the ranking was made per-landmass: on a twelve-island chain,
# nine islands had no vegetation at all, and on a 128x80 map a quadrant with 886
# woods-capable tiles grew zero trees while another with 919 grew a quarter of
# them. Nothing was wrong with the eligibility - the global cut simply never
# reached that ground.
#
# Ranking per landmass improved it - nine bare islands became seven - but did
# not close it, and the rest is an OPEN QUESTION rather than a bug to squash
# quietly, which is why this reports and does not assert.
#
# The remainder comes from a disagreement between two systems. Biome bands are
# PERCENTILES of the map's own moisture when quotas are on, so a dry map still
# gets its quota of "grass". Woods are gated on an ABSOLUTE moisture of 0.26,
# written in three places - the eligibility test, the coverage average and
# Choose. On a dry map the whole grass band sits under that number, so the map
# shows grassland that can never carry a tree.
#
# Removing the absolute floor is NOT the fix: tried, and it made things worse
# (seven bare islands became eleven), because the coverage average uses the same
# 0.26 as its anchor and dry cells then drag the average down. The anchor there
# is doing something right - it is what makes a wet map greener than a dry one -
# so the two uses are not the same fact and cannot both be made relative.
#
# The decision worth taking deliberately: should ground the QUOTA calls grass,
# on a map whose absolute moisture is desert-like, carry woods? Whichever way
# that goes, the 0.26 should end up owned in one place instead of three.

# Tundra is deliberately absent. It is woods-capable in the placement stage,
# but it is also the ground most likely to sit under the temperature floor,
# so counting it here would blame the placement for climate doing its job.
const WOODS_CAPABLE := ["grass", "dry_grass"]

## Islands, each with how much woods-capable ground it has and how much it grew.
func islands(gen, size: Vector2i) -> Array:
	var seen := {}
	var out: Array = []
	for y in range(size.y):
		for x in range(size.x):
			var c := Vector2i(x, y)
			if seen.has(c): continue
			var k: String = gen.TerrainKindAt(c)
			if k == "deep_water" or k == "shallow_water" or k == "": continue

			var q: Array[Vector2i] = [c]
			seen[c] = true
			var head := 0
			var capable := 0
			var grown := 0
			while head < q.size():
				var a: Vector2i = q[head]; head += 1
				# Mountains carry no vegetation by design, so they are not
				# ground this expects woods on.
				if WOODS_CAPABLE.has(gen.TerrainKindAt(a)) and int(gen.ReliefAt(a)) < 2:
					capable += 1
				if gen.FeatureAt(a) != "":
					grown += 1
				for d in [Vector2i(1,0), Vector2i(-1,0), Vector2i(0,1), Vector2i(0,-1)]:
					var p: Vector2i = a + d
					if p.x < 0 or p.y < 0 or p.x >= size.x or p.y >= size.y: continue
					if seen.has(p): continue
					var pk: String = gen.TerrainKindAt(p)
					if pk == "deep_water" or pk == "shallow_water" or pk == "": continue
					seen[p] = true
					q.append(p)
			if q.size() >= 20:
				out.append({"cells": q.size(), "capable": capable, "grown": grown})
	return out

func _initialize() -> void:
	var root_node = load("res://tests/examples/terrain_iso_demo.tscn").instantiate()
	get_root().add_child(root_node)
	for i in range(20): await process_frame
	var gen = root_node.find_child("TerrainGenerator", true, false)

	# Landform, island count, coverage, label. Every setting is stated rather
	# than inherited from the scene, because a measurement that quietly changes
	# its own conditions cannot be compared against the one before it - which is
	# a mistake this file has already made once.
	var cases := [
		[2, 12, 0.22, "island chain"],
		[2, 7, 0.30, "archipelago"],
		[0, 3, 0.42, "continents"],
	]

	gen.BoundsSize = Vector2i(128, 80)
	for case in cases:
		gen.Landform = case[0]
		gen.ArchipelagoIslandCount = case[1]
		if case[0] == 0:
			gen.SeaCoverage = 1.0 - float(case[2])
		else:
			gen.LandmassScale = float(case[2])
		gen.GenerateTerrain()

		var found := islands(gen, Vector2i(128, 80))
		# Enough woods-capable ground that an empty result means the placement
		# failed to reach the island, not that the island genuinely has nowhere
		# for a tree to stand.
		var bare: Array = []
		for e in found:
			if int(e.capable) >= 40 and int(e.grown) == 0:
				bare.append(e)

		print("%-13s %d landmasses | %d with 40+ woods-capable tiles and NO woods"
			% [case[3], found.size(), bare.size()])
		for e in bare:
			print("      %d-tile island: %d capable, 0 grown" % [e.cells, e.capable])

	print("RESULT: reported (measurement, not a guard - see the header)")
	quit(0)
