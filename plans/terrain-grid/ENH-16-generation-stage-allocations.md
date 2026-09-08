# ENH-16 — Generation stages: allocation pass and shared distance transforms

**Type:** enhancement (generation time on Huge+ maps; off-thread GC pressure) · **Area:** `TerrainWaterStage`, `TerrainElevationStage`, `TerrainClimateStage`, `TerrainBiomeStage`, `TerrainContinentStage`, `TerrainCoherenceStage`, `TerrainScaleConstraintStage`, `TerrainShorelineStage`, `TerrainFeatureStage`, `TerrainGeometry`, `TerrainFieldBuilder` · **Status:** proposed 2026-09-08 · **Effort:** M (2 days) · **Risk:** low if the determinism probes gate every step

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
