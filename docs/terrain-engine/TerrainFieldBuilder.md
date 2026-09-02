# TerrainFieldBuilder

Generation stage — specifically the top-level orchestrator/entry point of the whole terrain-generation pipeline, not a stage itself.

`TerrainFieldBuilder` is the single place that owns the generation pipeline's order: it allocates the sample-resolution `TerrainWorld`, then calls each generation stage (landmass, water, elevation, erosion, relief classification, climate, rivers, shading, biome, coherence, tile reduction, continents, scale constraints, resources, features, start positions) in the specific sequence the pipeline requires, and packages the result into a `GeneratedTerrainField` plus `TerrainGenerationDiagnostics`. It also handles `TerrainMode.Plain` as a short-circuit path that skips every stage and fills a uniform terrain. This is the file to read to understand *why* the stages run in the order they do — most of that reasoning lives in this file's comments rather than in the individual stages.

## Public API

- `internal static GeneratedTerrainField Build(TerrainGenerationSettings settings)` — the sole entry point. Computes an effective sub-cell sample resolution, builds a `TerrainWorld` at that resolution, and either runs `BuildPlain` (for `TerrainMode.Plain`) or the full ordered stage pipeline, returning the finished `GeneratedTerrainField`.

Everything else (`BuildPlain`, `Finish`, `EffectiveSamplesPerCell`, `PlainKind`, and `MaxFieldSamples`) is private; the class is `internal static`.

## Dependencies

This file is the pipeline's hub and therefore touches nearly every other file in the folder:

- Constructs `TerrainWorld` and reads/writes many of its fields directly in `BuildPlain`/`Finish` (`Terrain`, `CellTerrain`, `Water`, `CellWater`, `Land`, `Footprint`, `CellContinent`, `Elevation`... via other stages, `Count`, `StartPositions`, `Resource`, `Feature`, `SamplesPerCell`, `Width`, `Height`) — from `TerrainWorld.cs`.
- Reads `TerrainGenerationSettings` (`Mode`, `Size`, `TopologySamplesPerCell`, `Preset`, `TargetLandCoverage`, `RequestedLandmassCount`) — from `TerrainGenerationSettings.cs` — and constructs `TerrainGenerationDiagnostics` from it.
- Calls, in this order: `TerrainNoiseSet.Create`, `TerrainLandmassStage.Apply`, `TerrainWaterStage.Apply`, `TerrainElevationStage.Apply`, `TerrainErosionStage.Apply`, `TerrainElevationStage.Classify`, `TerrainClimateStage.Apply`, `TerrainRiverStage.Apply`, `TerrainShadingStage.Apply`, `TerrainBiomeStage.Apply`, `TerrainCoherenceStage.Apply`, `TerrainTileReductionStage.Apply`, `TerrainContinentStage.Apply`, `TerrainScaleConstraintStage.ApplyTerrain`, `TerrainResourceStage.Apply`, `TerrainFeatureStage.Apply`, `TerrainScaleConstraintStage.ApplyFeatures`, `TerrainStartPositionStage.Apply` — each from its own like-named file.
- Calls `TerrainGeometry.CountComponents` (from `TerrainGeometry.cs`) when building diagnostics.
- Calls `TerrainTileSets.IsWaterKind` (from `TerrainTileSets.cs`) in `BuildPlain` to decide whether the plain preset fills water or land arrays.
- Constructs and returns `GeneratedTerrainField` (from `GeneratedTerrainField.cs`), which is what every renderer and gameplay component ultimately reads.

## Notes

- The ordering comments are explicit and specific about *why* — e.g. "lakes move the coastline, so elevation must be measured after them", "climate needs elevation for lapse rate and rain shadow", "shading follows rivers so a carved river is not left lit like the hillside it replaced" — these read as accurate descriptions of the actual call order below them, not stale documentation.
- `BuildPlain` has an explicit comment/bugfix note: an earlier version filled only the per-sample `Terrain` array and left `CellTerrain` at its constructor default, so every `TerrainMode.Plain` map ignored `Preset` entirely at gameplay-tile resolution. The current code fills both `Terrain` and `CellTerrain` (and both `Water`/`CellWater` or `Land`/`Footprint`) — this is a fixed, documented regression, not a live bug.
- `EffectiveSamplesPerCell` silently reduces `TopologySamplesPerCell` (down to a floor of 2) when `Size.X * Size.Y * samples^2` would exceed `MaxFieldSamples` (1,250,000) — a large requested map gets a *coarser* sub-tile sample resolution than requested, with no diagnostic or warning surfaced to the caller about the downgrade; `TerrainGenerationDiagnostics` does record the actual `SamplesPerCell`/`Width`/`Height` used, so the information is available but not flagged as a deviation from what was asked.
- `Finish`'s land/ocean/lake/river counts iterate every sample in `world.Count`; note that `world.Water` values of `WaterBody.River` are counted only if `Land[index]` is false (rivers are represented as a water body, not as land, at this point in the pipeline) — consistent with the rest of the codebase's model where a river tile is water, not land-with-a-river-flag.
