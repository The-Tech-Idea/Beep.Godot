# TerrainFeatureStage

Generation stage in the terrain pipeline, run by `TerrainFieldBuilder` after the biome table and tile reduction, on the reduced gameplay-tile grid rather than the fine sample grid.

`TerrainFeatureStage` decides which terrain tiles carry a vegetation/water "feature" layered on top of their base terrain kind — woods, dense forest, jungle, marsh, oasis — the way Civilization treats a tile as "grassland with woods on it" rather than a separate forest terrain. For woods-eligible ground (grass/dry_grass/tundra, not water, not mountains, not too cold) it builds a noise field biased by moisture, ranks that field into a percentile threshold computed **locally per 8x8-tile block** (padded and blended across block edges so no grid seams appear), and assigns `None`/`Woods`/`Forest` by comparing each cell's stand value against its block's threshold and "dense" cutoff. Jungle, swamp and desert tiles get their matching feature (Jungle/Marsh/rare Oasis) unconditionally rather than through the ranked field.

## Public API

- `internal static void Apply(TerrainWorld world, TerrainNoiseSet noise, TerrainGenerationSettings settings)` — the only public member. Populates `world.Feature[cell]` for every cell in the reduced tile grid. No-ops entirely if `settings.FeatureDensity <= 0`.
- `public const string None = ""`, `public const string Woods = "woods"`, `public const string Forest = "forest"`, `public const string Jungle = "jungle"`, `public const string Marsh = "marsh"`, `public const string Oasis = "oasis"` — the feature-name string constants other files (notably the renderers) switch on to decide what to draw.

Everything else (`Blend`, `AverageWetness`, `Choose`, `StandBias`, `Hash01`, and the tuning constants `BlockTiles`, `MinBlockCells`, `StandSpread`) is private; the class is `internal static`.

## Dependencies

- Reads/writes `TerrainWorld.CellWater`, `TerrainWorld.CellRelief`, `TerrainWorld.CellTerrain`, `TerrainWorld.Moisture`, `TerrainWorld.Temperature`, `TerrainWorld.CellsWide/CellsHigh`, `TerrainWorld.CellIndex`, `TerrainWorld.CellCentreIndex`; writes `TerrainWorld.Feature` (from `TerrainWorld.cs`).
- Reads `noise.Vegetation` (a `TerrainNoiseSet` field, from `TerrainNoiseSet.cs`) for the per-cell vegetation fbm sample.
- Reads `settings.FeatureDensity` and `settings.Seed` (from `TerrainGenerationSettings.cs`).
- Calls `TerrainGeometry.Percentile` (from `TerrainGeometry.cs`) both globally and per-block to turn the vegetation field into a coverage-meaning threshold.
- Calls `TerrainTileSets.IsLandKind` (from `TerrainTileSets.cs`) in `AverageWetness` to decide which tiles count toward the map's average wetness.
- `TerrainFeatureRendererComponent` (and the isometric feature renderer, and `TerrainDataLayersComponent`) read `world.Feature` back out (via `GeneratedTerrainField`/`TerrainGeneratorComponent.FeatureAt`) and switch on the `Woods`/`Forest`/`Jungle`/`Marsh`/`Oasis` constants defined here.
- Called by `TerrainFieldBuilder.Build`, after `TerrainResourceStage.Apply` and before `TerrainScaleConstraintStage.ApplyFeatures` — the latter can subsequently strip features that don't reach a minimum contiguous tile count.

## Notes

- Extensive comments in the file document three specific, measured regressions that were fixed and are guarded against by the current code: (1) ranking the *bare* noise instead of noise-plus-bias caused whole dry islands (five islands, 49–171 tiles) to grow nothing; (2) a single global (rather than per-block) threshold left a quarter of one map's land bare because the field's "top slice" landed in one region; (3) blending a block's threshold toward its neighbours (rather than taking the nearest block's value outright) pulled low-lying thresholds up and a genuinely wet quadrant grew nothing. These read as real regression notes, not speculation — the code as written (nearest-block, not blended; ranked-with-bias field; per-block percentile) matches what the comments say was needed.
- A comment explicitly notes a *removed* setting: "Dryness is not re-tested here — and the generator export that once carried it is gone, because nothing read it" — i.e., a dryness gate/export was deleted after being found to duplicate what the biome classification already decided. This is a resolved instance of the exact duplication pattern the project's standing rules warn about (two places deciding the same fact), already fixed rather than a live issue.
- `Hash01(int seed, int x, int y)` here is byte-for-byte the same bit-mixing function as `TerrainFeatureRendererComponent.Hash01(int x, int y, int seed)` (only the parameter order differs) — duplicated rather than factored into a shared utility.
- `AverageWetness`'s doc comment is followed immediately by a second, more detailed doc comment on the same method (two `<summary>` blocks stacked back to back, lines 252–273) — the first one is superseded by the second and is effectively stale/redundant text, not an accurate description on its own of what the method measures over.
