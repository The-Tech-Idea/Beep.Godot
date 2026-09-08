# DUP-04 — Finish the per-cell hash consolidation; share the small generation helpers

**Type:** duplication fix · **Area:** `TerrainIsometricRendererComponent`, `TerrainIsometricAutotileRendererComponent`, `GridResourceScatterComponent`, generation stages, `TerrainShorelineField` · **Status:** **IMPLEMENTED 2026-09-08 (hashes and helpers; the neighbour loops landed the same day with ENH-16)** · **Effort:** S (took ~half a day) · **Risk:** low

## Outcome

Landed on `TerrainGeometry`: `VariantSalt`, `Negate`, `CountTrue`, `RankedValue` and `MostCommon`, with `Percentile` routed through `RankedValue`. Verified: `dotnet build` clean; the five probes that pin exact generation output (`terrain_final_topology`, `terrain_exact_recipe`, `grid_terrain_topology`, `grid_terrain_feature`, `terrain_generated_coast`) unchanged; `TerrainWaterSurfaceSmoke`'s pin that `RankedValue == Percentile` still holds with the method moved; the new `tests/terrain_variant_choice_probe.gd` green in the gate; **5 of 5 mutations trip a guard** (2 on the scan pin, 3 on the probe).

What the three hashes became, and what changed on screen:

1. **The block view's `VariantFor` is bit-identical.** Its private copy was this file's own Wang mix with the FNV offset basis `2166136261` as a constant salt. That salt is now `TerrainGeometry.VariantSalt`, so the same cell picks the same frame as before — the consolidation moved the mix, not the picture.
2. **The autotile view changes, on purpose.** It used a different hash (`x·73856093 ^ y·19349663`) on the *absolute* cell, so one map's two isometric views disagreed about the variant of every cell; measured with the probe's fixture, 56 of 64 cells. It now uses `HashInt` under `VariantSalt` on the **window-local** cell, matching the block view at any `BoundsOrigin`. Only which alternative a cell shows moves; no geometry does. Nothing in the tree pins autotile alternatives.
3. **The scatter's amount roll** allocated a `RandomNumberGenerator` per deposit under a private seed formula; it is one `HashInt` now. Amounts per seed change; nothing pins them (`GridPlacementSmoke` authors `MinAmount == MaxAmount`). This is the one edit under `ecs/grid/` — four lines in one private method.

The helper replacements were checked for exact equivalence before landing: every inline "most common" vote used a strict `>` over the same insertion-ordered dictionary (ties to the first key counted), both percentile copies used the same clamp-and-round, and `Percentile`'s empty-input result is unchanged. The probe measures the views' agreement through the *atlas x* of the tiles each paints — with N variants at atlas `(0..N-1, 0)` in both, a cell's atlas x is its variant index — so it needs no display and no new API.

**Deliberately not done here, and why.** The 4-neighbour loops (`TerrainGeometry.Neighbours` iterator ×4, the unrolled `side == 0 ? 1 : side == 1 ? -1 : 0` form ×7) use *two different orders* — `−x,+x,−y,+y` in the iterator, `+x,−x,+y,−y` in the loops. Every BFS and `PriorityQueue` growth in the stages is order-sensitive, so unifying them changes which cells are claimed first and therefore the generated map. That consolidation belongs with ENH-16 behind a recorded three-seed determinism baseline; landing it "as duplication" would have changed generation output under the name of tidying. `TerrainShorelineField` stays for the owner's decision as the plan says.

**Follow-up, 2026-09-08 (with ENH-16).** The iterator is gone: `TerrainGeometry.Neighbours4` fills a caller's four-int span in the iterator's order (-x, +x, -y, +y) and serves its four BFS sites plus `ClearOrphanWaterSamples`, which already walked that way - bit-identical, confirmed by the recorded field snapshot. The five unrolled loops in the other order (+x, -x, +y, -y) - `Landmass.Grow`, `Erosion.Diffuse`, `Coherence.AbsorbOnce`, `ScaleConstraint.NeighbourLand`, `ScaleConstraint.Regions` - and the iso renderer's copy then moved onto it in their own commit, and a scan pin keeps the unrolled form out of `ecs/terrain`. That one changes generated output, deliberately and measurably. Over the six baseline worlds (37,632 cells): 22 terrain cells (0.058 %), 12 feature cells and 1 resource cell moved - the tie-breaks in the neighbour votes - and no relief, water, continent or start position did; 3,562 elevation values (9.5 %) differ by at most one float ulp (1.79e-7), from the changed summation order in erosion diffusion. The determinism fixture was re-recorded on this output after the change was counted cell by cell against a dump of the previous build; the allocation bill is unchanged (64.3 MiB). Verified: `dotnet build` clean; the baseline probe green on the new fixture; the exact-output probes (`terrain_final_topology`, `terrain_exact_recipe`, `grid_terrain_topology`, `grid_terrain_feature`, `grid_terrain_lake_scatter`) and the coastal, world, lab, scale, job, identity and isometric probes green; the new scan pin trips when the unrolled form is typed back into a stage.

Noted, not caused here: `tests/runtime_smoke.ps1` is red at `GridPlacementSmoke.VerifyPlacementOccupancy` ("Fresh placement grid should allow an empty footprint"), which calls `CanPlace` on a bare `GridPlacementComponent`. The only uncommitted file under `ecs/grid/` is the scatter's `RandomAmount`, which placement never reaches, so the failure is in the committed placement code — the other session's area.

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
