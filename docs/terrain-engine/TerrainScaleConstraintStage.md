# TerrainScaleConstraintStage

Generation stage in the terrain pipeline, run in two parts by `TerrainFieldBuilder`: `ApplyTerrain` right after tile reduction and continent tagging (before resources/features are placed), `ApplyFeatures` last, after `TerrainFeatureStage.Apply` — both gated on `settings.UseScaleRules` and both operating on the reduced gameplay-tile grid.

`TerrainScaleConstraintStage` enforces that every feature on the map (lakes, raised relief, rivers, vegetation clumps) is big enough in absolute tile count to actually read as that feature, and that no single lake swallows the landmass it sits on. It groups matching tiles into four-connected regions (`Regions`, a BFS flood fill over `TerrainWorld`'s reduced grid), then drains/levels/clears any region below the relevant `TerrainScaleRules` minimum, replacing it with the dominant surrounding land terrain rather than leaving a hole. It also grounds any leftover "peak" terrain (rock/snow/gravel) sitting on flat relief, enforcing that invariant in one place regardless of which upstream stage produced the mismatch. `ApplyTerrain` handles the constraints on the land itself (which must settle before anything is placed on it, since a drained lake becomes ground that can grow things); `ApplyFeatures` handles what stands on the land, which can only be judged once features exist.

## Public API

- `public static void ApplyTerrain(TerrainWorld world, TerrainGenerationSettings settings)` — no-ops if `!settings.UseScaleRules`; otherwise runs, in order: `DrainOversizedLakes`, `DrainSmallLakes`, `LevelSmallRelief`, `ClearShortRivers`, `GroundPeakMaterial`.
- `public static void ApplyFeatures(TerrainWorld world, TerrainGenerationSettings settings)` — no-ops if `!settings.UseScaleRules`; otherwise runs `ThinLoneFeatures`.

Everything else in the file (`GroundPeakMaterial`, `DrainOversizedLakes`, `SetTile`, `InLandmass`, `NotLakeBedKinds`, `DominantLand`, `DrainSmallLakes`, `LevelSmallRelief`, `ClearShortRivers`, `ThinLoneFeatures`, `PeakKinds`, `NeighbourLand`, `Regions`) is private to the `internal static class TerrainScaleConstraintStage`.

## Dependencies

- Reads and writes `TerrainWorld.CellWater`, `TerrainWorld.CellTerrain`, `TerrainWorld.CellRelief`, `TerrainWorld.CellsWide`/`CellsHigh`, `TerrainWorld.CellIndex`, `TerrainWorld.Feature` (reduced-grid arrays); writes `TerrainWorld.Water`, `TerrainWorld.Terrain`, `TerrainWorld.Land` at sample resolution too, via `SetTile`, which mirrors every reduced-tile change down into the `SamplesPerCell × SamplesPerCell` block of samples it covers (all `TerrainWorld.cs`).
- Reads `TerrainGenerationSettings.UseScaleRules` (`TerrainGenerationSettings.cs`).
- Reads `TerrainScaleRules.MinLakeTiles`, `.MinReliefTiles`, `.MinRiverTiles`, `.MinFeatureTiles`, `.MaxLakeShareOfLandmass` (`TerrainScaleRules.cs`) as the minimum/maximum sizes it enforces.
- Reads `TerrainTileSets.IsLandKind(string)` (`TerrainTileSets.cs`) to decide whether a terrain kind counts as land when picking replacement/dominant terrain.
- Reads the `WaterBody` enum (`.None`, `.Lake`, `.River`) and `TerrainRelief` enum (`.Flat`).
- Consumed by: `TerrainFieldBuilder.Build` (calls `ApplyTerrain` then, later, `ApplyFeatures`).

## Notes

- `DrainSmallLakes`, `LevelSmallRelief`, `ClearShortRivers` and `ThinLoneFeatures` all follow the identical shape — flood-fill regions via `Regions`, drop any region under a `TerrainScaleRules` minimum, replace/clear it — but each is its own small method rather than one parametrized helper; this is a recognized repeated pattern within this single file, not a bug, and the class's own doc comment names it explicitly as "the rule... applied to features."
- `GroundPeakMaterial`'s doc comment explains it is intentionally the *one* place peak-on-flat mismatches are fixed, specifically because earlier attempts to prevent the mismatch in each upstream stage kept recurring via new routes (coherence stage, then erosion) — documented rationale, not dead defensive code.
- `SetTile`'s doc comment explains why it must write both the reduced `Cell*` arrays and the underlying `Water`/`Terrain`/`Land` sample arrays together: the painted-ground view reads samples while the tile/isometric views read cells, and skipping the sample write previously left a drained lake visible in one view and gone in another. Every mutation in this file that changes terrain goes through `SetTile`, so that invariant is upheld consistently within this file.
- `NotLakeBedKinds` (`sand`, `gravel`, `rock`, `snow`) and `PeakKinds` (`rock`, `snow`, `gravel`) overlap (`rock`, `snow`, `gravel` in both) but serve different exclusion purposes — one keeps a drained lake bed from becoming sand/rock/snow (shore or peak material), the other specifically keeps a levelled-relief tile from being re-assigned peak terrain. Not a duplicate; two call sites intentionally excluding overlapping-but-not-identical sets for different reasons.
- `LevelSmallRelief` only substitutes replacement terrain for `"rock"`, `"snow"`, `"gravel"` tiles specifically (hardcoded string match); a differently-named peak terrain kind added to a biome table without also being added to this check, `PeakKinds`, and `NotLakeBedKinds` would not be caught by any of the three.
- No dead code, stubs, or TODOs found in this file.
