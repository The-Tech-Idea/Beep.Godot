extends SceneTree

# The map must have the number of separate landmasses it was asked for, and
# each one must be a compact BODY rather than a web. Both were broken: a
# thresholded-noise field gave one blob whatever was asked, and the seeded
# growth that replaced it merged its masses back together until the separation
# test asked about foreign claims rather than about cell ownership.

func masses(gen, size: Vector2i) -> Array:
	var seen := {}
	var out: Array = []
	for y in range(size.y):
		for x in range(size.x):
			var c := Vector2i(x, y)
			if seen.has(c): continue
			var k: String = gen.TerrainKindAt(c)
			if k == "deep_water" or k == "shallow_water" or k == "": continue
			var q: Array[Vector2i] = [c]; seen[c] = true
			var head := 0
			var lo := c
			var hi := c
			while head < q.size():
				var a: Vector2i = q[head]; head += 1
				lo = Vector2i(min(lo.x, a.x), min(lo.y, a.y))
				hi = Vector2i(max(hi.x, a.x), max(hi.y, a.y))
				for d in [Vector2i(1,0), Vector2i(-1,0), Vector2i(0,1), Vector2i(0,-1)]:
					var p: Vector2i = a + d
					if p.x < 0 or p.y < 0 or p.x >= size.x or p.y >= size.y: continue
					if seen.has(p): continue
					var pk: String = gen.TerrainKindAt(p)
					if pk == "deep_water" or pk == "shallow_water" or pk == "": continue
					seen[p] = true; q.append(p)
			if q.size() >= 20:
				var box: int = (hi.x - lo.x + 1) * (hi.y - lo.y + 1)
				out.append({"cells": q.size(), "fill": float(q.size()) / float(box)})
	out.sort_custom(func(a, b): return a.cells > b.cells)
	return out

func _initialize() -> void:
	var root_node = load("res://addons/beep_game_builder_cs/templates/scenes/terrain/terrain_iso_demo.tscn").instantiate()
	get_root().add_child(root_node)
	for i in range(20): await process_frame
	var gen = root_node.find_child("TerrainGenerator", true, false)
	gen.Landform = 2

	# The coverage the Archipelago preset actually ships. The demo scene sits at
	# 0.70, and 70% land in twelve masses on a 48x48 map - each pair separated by
	# a four-tile channel the beach cannot bridge - does not fit. Asserting a
	# count the geometry forbids would only be testing the arithmetic of the
	# failure path.
	gen.LandmassScale = 0.30
	var failed := 0

	for size in [Vector2i(48, 48), Vector2i(64, 64), Vector2i(128, 80)]:
		gen.BoundsSize = size
		for want in [4, 8, 12]:
			gen.ArchipelagoIslandCount = want
			gen.GenerateTerrain()
			var m := masses(gen, size)

			# The map must also REPORT what it was asked for, not only what it
			# managed. Where the two differ the caller has to be able to see it -
			# 11 of 12 is a different fact from 11.
			var diag: Dictionary = gen.GetGenerationDiagnostics()
			var reported: int = diag.get("requested_landmass_count", -1)
			if reported != want:
				print("  reported request %d, asked %d  FAIL" % [reported, want])
				failed += 1
			# Starts at the worst possible value, not the best. Seeding this at
			# 1.0 means a map that produced NO landmasses scores a perfect fill,
			# and the fill check passes on an empty ocean - it would be caught by
			# the count above today, but only because no expectation here is
			# zero, which is not something this check should depend on.
			var worst: float = 0.0 if m.is_empty() else 1.0
			for entry in m:
				worst = min(worst, entry.fill)
			# 48x48 has room for eleven of the twelve, not twelve. Each mass is
			# about 58 tiles - a 8.5-tile island - and each pair needs a
			# four-tile channel between them, because anything narrower is
			# bridged by the beach and the two read as one landmass. That is a
			# 12.5-tile pitch across a lattice whose columns are 12 tiles wide,
			# so one island has nowhere to go. The generator grows the ones that
			# fit, gives the last one's share to them, and REPORTS eleven of
			# twelve rather than claiming twelve - which is checked above.
			#
			# The expectation is exact rather than "at least": a drop to nine
			# would be a regression and has to fail here.
			var expect: int = want
			if size == Vector2i(48, 48) and want == 12:
				expect = 11

			# The fill bar sits between the shapes that are wrong and the shapes
			# that are merely irregular, and both ends are measured rather than
			# guessed. It has to REJECT the two failures it exists for: a
			# thresholded-noise web filled 30-35% of its bounding box, and an
			# island eaten by its own lake left a ring at 24%. It has to ACCEPT
			# real landmasses, which measure 44-70% here - a curved or elongated
			# island is not a defect. 0.40 clears both by a margin; tightening it
			# to 0.45 failed a legitimate 44% island.
			var ok: bool = m.size() == expect and worst >= 0.40
			if not ok: failed += 1
			var note: String = "" if expect == want else "  (%d fit)" % expect
			print("%s asked %2d -> %2d masses, worst fill %d%%%s  %s"
				% [size, want, m.size(), int(worst * 100.0), note, "ok" if ok else "FAIL"])

	# The Mainland/Archipelago overlap: Mainland must never ask for MORE
	# landmasses than Archipelago at the same ArchipelagoIslandCount - continents
	# are meant to be few and large, islands many and small. Reading the axis
	# unscaled gave both modes the SAME requested count, and only the land
	# coverage told them apart. Only the REQUESTED count is checked here - it
	# is a pure function of the settings, not of what the geometry manages to
	# fit - so the map stays small and LandmassScale is not tuned per count.
	#
	# The two floor at the same minimum of 2, so at ArchipelagoIslandCount's
	# own minimum they tie there and nowhere else: Archipelago cannot ask for
	# fewer than 2 either, and Mainland asking for fewer would reintroduce the
	# single-mass-filling-the-map failure this generator was rewritten to stop
	# producing. So the general check is "never more", and strict "fewer" is
	# asserted at the shipped default (4) - the concrete case this fix targets.
	gen.BoundsSize = Vector2i(32, 32)
	gen.LandmassScale = 0.5
	for count in [2, 4, 6, 8, 10, 12]:
		gen.ArchipelagoIslandCount = count
		gen.Landform = 0
		gen.GenerateTerrain()
		var mainland_requested: int = gen.GetGenerationDiagnostics().get("requested_landmass_count", -1)
		gen.Landform = 2
		gen.GenerateTerrain()
		var archipelago_requested: int = gen.GetGenerationDiagnostics().get("requested_landmass_count", -1)
		var overlap_ok: bool = mainland_requested <= archipelago_requested
		if count == 4: overlap_ok = overlap_ok and mainland_requested < archipelago_requested
		if not overlap_ok: failed += 1
		print("island count %2d -> mainland asks %d, archipelago asks %d  %s"
			% [count, mainland_requested, archipelago_requested, "ok" if overlap_ok else "FAIL"])

	print("RESULT: ", "all checks passed" if failed == 0 else "%d FAILED" % failed)
	quit(1 if failed > 0 else 0)
