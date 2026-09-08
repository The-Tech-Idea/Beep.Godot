# ENH-16 — Generation stages: allocation pass and shared distance transforms

**Type:** enhancement (generation time on Huge+ maps; off-thread GC pressure) · **Area:** `TerrainWaterStage`, `TerrainElevationStage`, `TerrainClimateStage`, `TerrainBiomeStage`, `TerrainContinentStage`, `TerrainCoherenceStage`, `TerrainScaleConstraintStage`, `TerrainShorelineStage`, `TerrainFeatureStage`, `TerrainGeometry`, `TerrainFieldBuilder` · **Status:** **IMPLEMENTED 2026-09-08** (allocation pass; the cross-order neighbour loops follow in their own commit) · **Effort:** M (took a day) · **Risk:** low - every step gated by the recorded field snapshot

## Outcome

A Huge (128×80, 11 samples per cell) build's managed allocation went from **284,483,064 bytes (271.3 MiB) to 67,433,208 bytes (64.3 MiB) - 23.7 % of the recorded baseline**, under the plan's quarter. Every published layer is byte-identical: `tests/terrain_generation_baseline_probe.gd` hashes 16 layers (12 per-cell, 3 per-sample, starts and shore widths) for seeds 31415/4242/777 at Small (48×48) and Huge, and all 96 hashes match the fixture recorded before the first edit. Verified: `dotnet build` clean; the probe green in the gate; 22 downstream probes green (`terrain_final_topology`, `terrain_exact_recipe`, `grid_terrain_topology`, `grid_terrain_feature`, `grid_terrain_lake_scatter`, `grid_terrain_subsurface`, `terrain_generated_coast`, `terrain_beach_footprint`, `terrain_lake_bank`, `terrain_coastal_grass`, `terrain_generation_job`, `terrain_world_generation`, `terrain_lab_generation`, `terrain_start_scale`, `terrain_resource_scale`, `terrain_identity`, `terrain_live_relief`, `terrain_scratch_lifetime`, `terrain_water_surface`, `terrain_shoreline_contour`, `terrain_sample_values`, plus the new baseline); the scan pins pass through the harness. **Mutations:** the three allocation regressions each trip the ceiling - an iterator-sized heap array per water sample in `TouchesLand` (91.7 MiB), the kind-field clone per coherence pass (83.2 MiB), and a single fresh field-sized array in the water stage (69.0 MiB against a 67.8 MiB ceiling, so the guard has one field of headroom, not ten). Dropping the +y neighbour from `Neighbours4` changes 90 of the 96 layer hashes, so the snapshot sees a real change in a rewritten stage. Two mutations were deliberately expected to pass and did: reversing `Neighbours4`'s order changes nothing, because all five sites are set-valued walks (ocean reachability, continent labels, any-neighbour tests, region membership), and breaking `Smooth`'s ties by kind index instead of encounter order changes nothing across these six worlds, which says the tie never arose here rather than that the order is free - the encounter-order list stays. The scan pins were mutated the same way: 5 of 5 trip (the iterator declared again, a stage cloning a field, a scratch accessor renamed, a stage allocating a field-sized array per call, the iterator called from a stage).

The bill, per stage, measured on the generating thread with `GC.GetAllocatedBytesForCurrentThread` around `TerrainFieldBuilder.BuildPrepared` (the builder is single-threaded, so the per-thread counter is exact) and split by the builder's progress callback:

| Stage | Before (bytes) | After (bytes) | What it was |
|---|---:|---:|---|
| Water | 71,598,536 | 14,901,728 | iterator per visited sample in two BFSs, `DistanceTo` field + queue + `Negate`, basin scores, candidate list, `queued`, `Queue<int>` growth; now first touch of `CoastDistance` and three scratch arrays |
| Biomes | 45,993,216 | 64 | `TouchesLand` iterator per water sample |
| Biome coherence | 29,077,424 | 1,322,176 | `(string[])Terrain.Clone()` per pass, `seen` + region list + queue per absorb pass; now the byte scratch |
| Shorelines | 22,464,096 | 161,280 | two masks + two `double[]` transforms; now one mask and one float-stored transform in shared scratch |
| Elevation | 22,302,928 | 6,195,312 | `DistanceTo` field + queue + `Negate`, then a copy into `CoastDistance`; now `Elevation` and `Relief` themselves |
| Erosion | 19,825,136 | 9,912,768 | `flowsTo`/`order`/`flow`/`settled`; now first touch of the second int and float scratch |
| Rivers | 16,702,512 | 152 | the same three drainage arrays again plus a sorted copy |
| Diagnostics | 13,569,448 | 3,656,856 | `CountComponents` allocating two label/stack fields; what remains is output packing and the underground digest |
| Relief | 8,389,568 | 64 | two `Percentile` calls, each a `List<float>` and a sort |
| Landmass | 2,659,376 | 1,412,880 | `eligible` and `claimed` masks; `claimed` was the land mask by another name |
| Terrain constraints | 1,980,888 | 217,160 | `ClearOrphanWaterSamples`'s sample-grid `seen` and region list |
| Continents | 274,984 | 41,048 | iterator and `Queue<int>` on the tile grid |
| Buffer, Climate, Shading, Gameplay cells, Resources, Features, Subsurface, Start positions, Feature constraints | 29,680,528 | 29,606,928 | the domain arrays and the tile-grid stages, unchanged by design |
| **Total** | **284,483,064** | **67,433,208** | |

What remains is the floor: 39.7 MB of domain arrays the field is made of (land, footprint, water, kinds, elevation, relief, coast distance, temperature, moisture, shade), 22.3 MB of shared scratch touched once (two `int[]`, two `float[]`, a `bool[]`, a `byte[]`), 3.7 MB of output packing, and under 2 MB of dictionaries and priority queues.

What landed, and how each was kept bit-identical:

1. **`TerrainGeometry.Neighbours4(index, w, h, Span<int>)`** fills the four neighbours in the iterator's own order (-x, +x, -y, +y) and replaced the iterator at its four BFS sites and at `ClearOrphanWaterSamples`, which already walked in that order. Callers `stackalloc` the span once, outside their loops. The iterator is gone.
2. **`TerrainGenerationBuffer` scratch** - `IntScratchA/B`, `FloatScratchA/B`, `BoolScratch`, `ByteScratch` - lazily allocated like the rest and released with `ReleaseGenerationScratch`. A stage assumes nothing about their contents on entry and reads none across a stage boundary. Who holds what: Landmass (`eligible` in Bool); Water (`CoastDistance` for the pre-lake distance, IntA as queue then candidates, FloatA scores, Bool `queued`, IntA again as the ocean BFS queue); Elevation (IntA queue; FloatA for the sorted heights); Erosion and Rivers (IntA/IntB/FloatA drainage, FloatB `settled` and sorted flow); Coherence (Byte snapshot; Bool `seen`, IntA queue-as-region); Shorelines (Bool mask, FloatA transform); Continents and orphan clearing (IntA queue, Bool); Features (FloatA sorted stand); Finish (IntA/IntB labels and stack).
3. **`DistanceTo(source, sourceValue, w, h, distance, queue)`** writes into a caller-owned field and asks "distance to where land is false" directly. `Negate` is gone - its only two callers were these, and it existed only because `DistanceTo` could not be asked the inverted question. The water stage writes its pre-lake distance into `CoastDistance`; the elevation stage overwrites it from the final coast, as it always did, so the copy into `CoastDistance` went too.
4. **`SortedSelection(values, mask, destination)` + `RankedValue`** replace `Percentile`: the relief and feature stages each take their two cutoffs from one sort. Sorted output is unique for a total order, so the sort implementation is immaterial. The water-surface smoke's pin that block ranking equals full-mask ranking now goes through `SortedSelection`.
5. **`Smooth`** snapshots each pass as a byte rainfall index (0 = not a rainfall kind) instead of cloning the string field, and votes over a stack-allocated count table. The winner used to be chosen by iterating a `Dictionary` in insertion order with a strict `>`, so ties went to the kind met first in the 3×3 scan; a `met` list reproduces that order exactly. `RainfallKinds` is now the one list, and the `Rainfall` set is built from it.
6. **`AbsorbOnce`, `ClassifyWaterBodies`, `TerrainContinentStage`, `ClearOrphanWaterSamples`** run their BFS over `IntScratchA`, where the FIFO run a search fills *is* the region in visiting order - the same order the old `region.Add(index)` at dequeue produced.
7. **Shorelines** fill one mask twice (ocean, then lake) and run each transform through `TerrainEuclideanDistance.Squared(..., float[] result)`, a new overload that computes in double per row and column and stores floats. Every value is a whole squared step count, exact below 2^24 (a straight run of 4096 samples) and monotone beyond, which is all a band test against a threshold of at most (4 tiles × 24 samples)² needs. The renderer's signed coast field keeps the double form because it draws the value. Ocean sand is written before the lake test runs; neither test reads terrain, so the result equals the old single combined condition.
8. **Landmass** kept a `claimed` mask beside `world.Land` that `Apply` clears and `Grow` alone sets, in the same statement - the mask *was* the land; it reads `Land` now. `eligible` lives in the bool scratch.
9. **`CountComponents`** in `Finish` labels over the scratch instead of two fresh fields.

Where this differs from the plan, and why:

- **Retaining the generated ocean/lake distances for the painted view's first coast build: not done - the premise has gone.** The plan cites `TerrainShorelineStage:607-608`; the stage has since been rewritten (54 lines) to run its transforms on the *sample* grid at `SamplesPerCell` purely to place sand, while `TerrainPaintedCoastJob` computes its field at its own `detail` sub-cell resolution from live cell water plus water patches. Different inputs and different resolutions: seeding the first render from the generated transform would make the first frame differ from every later live rebuild, which is the inconsistency the plan itself wanted to avoid. The generated transform is consumed and discarded in scratch.
- **Tile-grid `Regions` stays a list of lists.** Measured, the whole "Terrain constraints" stage allocated 1.98 MB before and 0.22 MB after; the sample-grid orphan sweep was the cost, and the five `Regions` calls sit inside the remaining 0.2 MB (0.07 % of the original bill). A callback API over a `ReadOnlySpan` delegate for that would be complexity with no bytes behind it.
- **The determinism probe is new, not DUP-13's.** DUP-13 is not implemented and its probe did not exist. `TerrainGenerationBaselineSmoke.cs` builds a world through `ApplyMapSetup` with coherence, scale rules and lake shores on (so every stage is in the hash), hashes each layer with SHA-256 through the public `GeneratedTerrainField` surface, and the probe compares against `tests/fixtures/terrain_generation_baseline.json`; `-- --record` rewrites the fixture and says so loudly. The allocation ceiling is a literal in the probe, not a fixture value, so re-recording the hashes can never move it.
- **Erosion and Rivers were not in the plan's table** but carried 13 % of the bill in drainage arrays; with the scratch in place they cost nothing to move.
- **The float-stored shoreline transform was needed for the last three points.** With a `double[]` scratch the bill was 79,823,688 bytes (28.1 %); the plan's quarter was not reachable while one stage kept a private 9.9 MB field.
- **The five unrolled neighbour loops in the *other* order** (+x, -x, +y, -y: `Landmass.Grow`, `Erosion.Diffuse`, `Coherence.AbsorbOnce`, `ScaleConstraint.NeighbourLand` and `.Regions`, plus the iso renderer's) are untouched here. They allocate nothing, and moving them onto `Neighbours4` changes tie-breaks, float sums and growth order - generated output. That is the DUP-04 leftover, and it lands as its own commit after this one, with the fixture re-recorded and the change quantified.

Noted, not caused here: `tests/terrain_iso_river_probe.gd` awaits `RenderingServer.frame_post_draw` and never returns headless; it is in no gate.

## Finding

`TerrainGeometry.LabelComponents` documents the class of cost: "walking neighbours through an iterator allocated millions of short-lived objects and was the single largest cost in generation." The same iterator and similar per-cell allocations remain in the stages that were not on that hot path at the time:

| Site | Allocation |
|---|---|
| `TerrainWaterStage.ClassifyWaterBodies:135`, `CarveLakeBasins:101`, `TerrainContinentStage:676`, `TerrainBiomeStage.TouchesLand:549` | `TerrainGeometry.Neighbours(...)` — a `yield` iterator object per visited cell (BFS over the whole sample field, twice) |
| `TerrainCoherenceStage.Smooth:266` | `(string[])world.Terrain.Clone()` per pass — a full field copy per coherence pass (2–3 passes) |
| `TerrainCoherenceStage.AbsorbOnce`, `TerrainScaleConstraintStage.Regions:490-535` | `new List<int>()` per region; `Regions` returns `List<List<int>>` for every matching region, run 5× (lakes, relief, rivers, features, landmasses) |
| `TerrainGeometry.Percentile:426-443` | `List<float>` + sort per call (called 4× in elevation/feature stages) |
| `TerrainGeometry.DistanceTo` | full `int[]` + `int[]` queue per call — `TerrainWaterStage:41` and `TerrainElevationStage:312` each compute "distance from water" (different water masks, so both are needed — but `Negate` allocates a third array each time) |
| `TerrainShorelineStage:607-608` | two full `double[]` Euclidean transforms (ocean, lake) — later `TerrainPaintedCoastJob` recomputes the coast distance at render time from the live snapshot (by design: live edits); the *generated* transform could seed the first render |
| `TerrainLandmassStage.Grow:344-410` | `PriorityQueue` per seed (fine) but `coast` `FastNoiseLite` per call and `weights`/`massRadii` arrays — fine; note only |

Generation already runs off-thread (`TerrainGenerationJob`) with cancellation checks every 4096 cells; the allocations show up as GC pauses on the **main** thread (the `System.GC.LOHThreshold` work this session was about exactly that).

## Design

- `TerrainGeometry.ForEachNeighbour4(index, width, height, Action<int>)` / a `stackalloc Span<int>` filler (`Neighbours4(index, w, h, Span<int> out) → count`) replacing the iterator in all four BFS sites (DUP-04 lists it).
- `TerrainCoherenceStage.Smooth`: double-buffer two `string[]` allocated once in the builder's scratch (`TerrainGenerationBuffer` gets a `Scratch` block: `int[] Labels`, `int[] Stack`, `bool[] Mask`, `string[] KindsAlt`, `float[] Floats`) and swap — no clone per pass.
- `Regions` → a callback form `ForEachRegion(world, predicate, Action<ReadOnlySpan<int>> body)` over the shared `Labels`/`Stack` scratch (the `LabelComponents` shape), no per-region lists.
- `Percentile` over a reusable `float[]` scratch with `Array.Sort(arr, 0, count)`.
- `DistanceTo(source, invert: bool)` avoids `Negate`; the water and elevation stages share the queue scratch.
- `TerrainGenerationBuffer` keeps the generated ocean/lake distance fields (already computed by the shoreline stage) so `GeneratedTerrainField` can expose them and the painted view's **first** coast build reuses them instead of recomputing from the snapshot (later live edits still go through `TerrainPaintedCoastJob`).

## Guards (fail first)

- **Determinism gate:** the three-seed field snapshot (DUP-13's probe) byte-identical after every step. Mutation of a stage's traversal order (e.g. reversed neighbour order) → mismatch; this proves the probe is sensitive.
- Timing probe: Huge (128×80) generation allocates < 25 % of today's managed bytes (measure with `GC.GetTotalAllocatedBytes` around `TerrainFieldBuilder.Build`; record today's number first). Mutation: restore the iterator in one BFS → allocation assertion fails.
- Existing `grid_terrain_topology_probe`/`feature_probe`/`lake_scatter_probe` stay green.

## Dependencies / collisions

Terrain-only; no collision. DUP-04 introduces the helpers this uses.

## Out of scope

Algorithmic changes to any stage (landmass growth, erosion, biome tables), out-of-core fields (FEAT-07).
