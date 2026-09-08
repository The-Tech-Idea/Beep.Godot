# TerrainScaleConstraintStage

Generation stage in the terrain pipeline, run in two parts by `TerrainFieldBuilder`: `ApplyTerrain` after tile reduction, before continent tagging and resources/features; `ApplyFeatures` after feature placement, before start positions. Both are gated on `settings.UseScaleRules`. Size decisions use the reduced gameplay grid; terrain cleanup also reconciles the fine sample field.

`TerrainScaleConstraintStage` enforces that every feature on the map (lakes, raised relief, rivers, vegetation clumps) is big enough in absolute tile count to actually read as that feature, and that no single lake swallows the landmass it sits on. It groups matching tiles into four-connected regions (`Regions`, a BFS flood fill over `TerrainGenerationBuffer`'s reduced grid), then drains/levels/clears any region below the relevant `TerrainScaleRules` minimum, replacing it with the dominant surrounding land terrain rather than leaving a hole. It also grounds any leftover "peak" terrain (rock/snow/gravel) sitting on flat relief, enforcing that invariant in one place regardless of which upstream stage produced the mismatch. `ApplyTerrain` handles the constraints on the land itself (which must settle before anything is placed on it, since a drained lake becomes ground that can grow things); `ApplyFeatures` handles what stands on the land, which can only be judged once features exist.

## Public API

Themed presets use ground materials independently of relief: volcanic basalt,
snowfields and desert stone can legitimately be flat. `ApplyTerrain` asks the
biome classifier whether the preset is themed. For those presets, it still
levels undersized relief but preserves both cell and fine-sample materials,
and does not run the flat-peak material replacement. This fixes the old cleanup
turning volcanic rock into lava or invented grass. Climate-driven peak cleanup
retains its existing behavior; cold climate ground/peak provenance still needs
a separate review. Lake/river draining remains a separate material policy.

- `public static void ApplyTerrain(TerrainGenerationBuffer world, TerrainGenerationSettings settings)` - no-ops if `!settings.UseScaleRules`; otherwise runs, in order: `DrainOversizedLakes`, `DrainSmallLakes`, `LevelSmallRelief`, `ClearShortRivers`, `ClearOrphanWaterSamples`, `GroundPeakMaterial`.
- `public static void ApplyFeatures(TerrainGenerationBuffer world, TerrainGenerationSettings settings)` — no-ops if `!settings.UseScaleRules`; otherwise runs `ThinLoneFeatures`.

Everything else in the file (`GroundPeakMaterial`, `DrainOversizedLakes`, `SetTile`, `ReplacePeakMaterial`, `InLandmass`, `NotLakeBedKinds`, `DominantLand`, `DrainSmallLakes`, `LevelSmallRelief`, `ClearShortRivers`, `ClearOrphanWaterSamples`, `ThinLoneFeatures`, `PeakKinds`, `NeighbourLand`, `Regions`) is private to the `internal static class TerrainScaleConstraintStage`.

## Dependencies

- Reads and writes `TerrainGenerationBuffer.CellWater`, `TerrainGenerationBuffer.CellTerrain`, `TerrainGenerationBuffer.CellRelief`, `TerrainGenerationBuffer.CellsWide`/`CellsHigh`, `TerrainGenerationBuffer.CellIndex`, `TerrainGenerationBuffer.Feature` (reduced-grid arrays); writes `TerrainGenerationBuffer.Water`, `TerrainGenerationBuffer.Terrain`, `TerrainGenerationBuffer.Land` at sample resolution too, via `SetTile`, which mirrors every reduced-tile change down into the `SamplesPerCell × SamplesPerCell` block of samples it covers (all `TerrainGenerationBuffer.cs`).
- Reads `TerrainGenerationSettings.UseScaleRules` (`TerrainGenerationSettings.cs`).
- Reads `TerrainGenerationSettings.Preset` through `TerrainBiomeStage.ThemedKind`.
- Reads `TerrainScaleRules.MinLakeTiles`, `.MinReliefTiles`, `.MinRiverTiles`, `.MinFeatureTiles`, `.MaxLakeShareOfLandmass` (`TerrainScaleRules.cs`) as the minimum/maximum sizes it enforces.
- Reads `TerrainTileSets.IsLandKind(string)` (`TerrainTileSets.cs`) to decide whether a terrain kind counts as land when picking replacement/dominant terrain.
- Reads the `WaterBody` enum (`.None`, `.Lake`, `.River`) and `TerrainRelief` enum (`.Flat`).
- Consumed by: `TerrainFieldBuilder.Build` (calls `ApplyTerrain` then, later, `ApplyFeatures`).

## Notes

- Water fringes can cross into cells whose majority remains dry. Removing only
  the main water cells formerly left those samples as isolated painted flecks.
  `ClearOrphanWaterSamples` scans four-connected fine lake/river components after
  cell cleanup. A component survives when a sample lies in a remaining cell of
  the same water-body type. Otherwise its samples adopt their receiving cells'
  water, terrain, relief, elevation and shade. Ocean samples are not candidates;
  orphan river samples inside ocean cells become ocean, not dry holes.
- This reconciliation is linear in the sample count. It does not alter cell
  topology or the frozen landmass footprint, flatten retained coastlines, or run
  with scale rules disabled. It affects newly generated fields; restored live
  grid snapshots remain authoritative and are not silently regenerated.
- `DrainSmallLakes`, `LevelSmallRelief`, `ClearShortRivers` and `ThinLoneFeatures` all follow the identical shape — flood-fill regions via `Regions`, drop any region under a `TerrainScaleRules` minimum, replace/clear it — but each is its own small method rather than one parametrized helper; this is a recognized repeated pattern within this single file, not a bug, and the class's own doc comment names it explicitly as "the rule... applied to features."
- `GroundPeakMaterial`'s doc comment explains it is intentionally the *one* place peak-on-flat mismatches are fixed, specifically because earlier attempts to prevent the mismatch in each upstream stage kept recurring via new routes (coherence stage, then erosion) — documented rationale, not dead defensive code.
- `SetTile` writes reduced water/material and underlying `Water`/`Terrain`/`Land` samples together. Lake and short-river draining use it, preventing water left in the painter after the grid becomes land.
- Relief cleanup uses `ReplacePeakMaterial`, not whole-tile reclamation. It replaces only peak material in dry samples, retains non-peak biome detail and water samples, and clears sample relief when removing a small relief region. Previously this left old rock/snow/gravel in fine queries or, on the flat-peak path, filled sub-cell shoreline water with land.
- Flat is a gameplay relief tier, not zero altitude. Relief cleanup preserves continuous elevation and its shade at both resolutions. Water-fringe reclamation copies the receiving cell's values for changed samples only. The isometric level uses relief; natural elevation variation can still shade a ground-tier tile.
- `NotLakeBedKinds` (`sand`, `gravel`, `rock`, `snow`) and `PeakKinds` (`rock`, `snow`, `gravel`) overlap (`rock`, `snow`, `gravel` in both) but serve different exclusion purposes — one keeps a drained lake bed from becoming sand/rock/snow (shore or peak material), the other specifically keeps a levelled-relief tile from being re-assigned peak terrain. Not a duplicate; two call sites intentionally excluding overlapping-but-not-identical sets for different reasons.
- `LevelSmallRelief` and `ReplacePeakMaterial` share `PeakKinds` (rock, snow, gravel). A new peak material must be added to that set; lake-bed exclusions remain a separate policy.
- `terrain_final_topology_probe.gd` includes 18 mixed-sample relief fixtures (three materials, three relief tiers, scale rules on/off), checks unchanged water/detail/elevation/shade, compares cell and fine material queries, and preserves a six-cell mountain range at the minimum size.
- The probe also reproduces lake/river fringes in dry cells, verifies retained
  eight-cell lakes and six-cell rivers, checks scale-rule opt-out, preserves
  ocean/mouth water and footprint bits, and independently flood-fills actual
  generated fine water over four seeds to detect bodies with no live-water cell.
- Twenty-one themed fixtures cover seven presets and three relief tiers,
  preserving materials, fine detail, water, height and shade while still
  flattening undersized relief. A real volcanic seed is checked by the lava
  material probe with scale rules enabled.
- No dead code, stubs, or TODOs found in this file.
