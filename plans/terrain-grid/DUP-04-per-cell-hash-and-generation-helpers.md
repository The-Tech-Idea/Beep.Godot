# DUP-04 — Finish the per-cell hash consolidation; share the small generation helpers

**Type:** duplication fix · **Area:** `TerrainIsometricRendererComponent`, `TerrainIsometricAutotileRendererComponent`, `GridResourceScatterComponent`, generation stages, `TerrainShorelineField` · **Status:** proposed 2026-09-08 · **Effort:** S (½–1 day) · **Risk:** low

## Finding

### Hashes that survived the `TerrainGeometry.Hash01` consolidation

`TerrainGeometry.Hash01` (`TerrainGeometry.cs:467-473`) documents that the Wang mix "was copy-pasted, byte-for-byte, into eight separate files before being pulled here", and `HashInt` (480-489) notes "one private copy survived … in the mountain tile painter". Three more survived:

| Site | Hash |
|---|---|
| `TerrainIsometricRendererComponent.VariantFor` 607-610 | private FNV-style mix seeded `2166136261u` |
| `TerrainIsometricAutotileRendererComponent.PaintAssignedTiles` 435 | `(x * 73856093u) ^ (y * 19349663u)` inline |
| `GridResourceScatterComponent.RandomAmount` | its own `seed * 31 + x * 17 + y` mix into `RandomNumberGenerator` |

Different hashes for the same purpose (stable per-cell variant choice) mean the same seed picks different variants in the isometric view than in the autotile view of one map.

### Small helpers duplicated across stages

| Helper | Copies |
|---|---|
| `Negate(bool[])` | `TerrainWaterStage:157`, `TerrainElevationStage:390` |
| `CountTrue(bool[])` | `TerrainWaterStage:165` (+ inline loops in `TerrainCoherenceStage:104-109`) |
| percentile of a sorted/selected field | `TerrainFeatureStage.RankedValue:78-83` vs `TerrainGeometry.Percentile:426-443` (the latter allocates a `List<float>` and sorts per call; the feature stage calls it twice per map, then re-sorts per block) |
| "most common kind" vote | `TerrainTileReductionStage.MostCommon:357-370`, `TerrainScaleConstraintStage.DominantLand:251-280` and `NeighbourLand:447-485`, `TerrainCoherenceStage` winner loops 129-136 / 230-237, `TerrainShorelineStage:617-635` |
| 4-neighbour offset unrolled as `side == 0 ? 1 : side == 1 ? -1 : 0` | 8 loops (`Landmass`, `ScaleConstraint`×3, `Coherence`, `Erosion.Diffuse`, `Regions`, `ClearOrphanWaterSamples`) beside the allocating iterator `TerrainGeometry.Neighbours` |
| signed coast distance build | `TerrainShorelineField.Build:554-573` (lab-only; used solely by `shoreline_contour_lab.gd`) re-implements what `TerrainCoastField.BuildLivePixels`/`TerrainEuclideanDistance.Signed` already do |

## Design

- Replace the three hashes with `TerrainGeometry.Hash01`/`HashInt`. Seeds change → variant layouts of existing maps change; acceptable under the no-legacy rule, and a version note goes in `docs/terrain-engine/TerrainGeometry.md`.
- `TerrainGeometry` gains: `Negate`, `CountTrue`, `ForEachNeighbour4(int index, int width, int height, Action<int>)` (non-allocating; the stages' unrolled loops call it), `Percentile(ReadOnlySpan<float> sorted, float p)` (the `RankedValue` shape) and `MostCommon(Dictionary<string,int>, string fallback)`.
- `TerrainShorelineField` becomes a thin adapter over `TerrainCoastField` (keeps `SampleDistance` for the lab) or the lab reads `TerrainCoastField.Pixels` directly and the class is removed — removal is the owner's call; the plan proposes the adapter.

## Guards

- Pin: no literal `2166136261u`, `73856093u`, `19349663u` outside `TerrainGeometry.cs`. Mutation: restore one → fails.
- Determinism probe (extends `grid_terrain_topology_probe`): build seed 31415 twice in isometric and autotile views; assert identical variant indices per cell **across both views** for the same kind. Mutation: put the FNV hash back → the two views disagree.
- Pin: `private static bool[] Negate(` declared nowhere under `ecs/terrain/`.

## Dependencies / collisions

None; purely internal to `ecs/terrain/` plus one method in `GridResourceScatterComponent`.

## Out of scope

Noise (`FastNoiseLite`) usage, generation output beyond variant/jitter choice.
