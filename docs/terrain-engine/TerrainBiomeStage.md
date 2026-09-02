# TerrainBiomeStage

Generation stage: an internal pipeline step that turns per-sample elevation, relief, temperature and moisture into the final terrain-kind string ("desert", "grass", "swamp", "shallow_water", ...) for every sample on the map.

`TerrainBiomeStage` runs after elevation, relief and climate have been computed and writes the last field the biome pipeline needs: `world.Terrain`. Land cells are classified through a small decision chain — an explicit themed preset first, then beach/lake-shore rim detection, then mountains/cold overrides, then (if climate biomes are enabled) a fixed Whittaker-style temperature x moisture lookup table for the remaining land; water cells are split into `shallow_water` and `deep_water` by whether they touch land (rivers and lakes are always shallow). The class comment documents that this table's fixed rainfall cutoffs are a deliberate choice over a percentile/quota approach, because a quota pass was tried and empirically collapsed entire biomes to zero on real map sizes.

## Public API

- `static void Apply(TerrainWorld world, TerrainGenerationSettings settings)` — the sole entry point. Computes ocean and lake distance fields via `TerrainGeometry.DistanceTo`, converts `settings.BeachWidth`/`settings.LakeShoreWidth` from tiles to samples using `world.SamplesPerCell`, then iterates every `(x, y)` in `world` and writes `world.Terrain[index]` by calling the internal `LandKind`/`WaterKind` classifiers.

(All other members — `MoistureBands`, `Bands`, `WaterKind`, `LandKind`, `EarlyKind`, `PlainGround`, `ThemedKind`, `TouchesLand` — are `private`/internal to the class and not part of its public surface.)

## Dependencies

- Reads `TerrainWorld.Water`, `.Land`, `.Elevation`, `.Relief`, `.Moisture`, `.Temperature`, `.Width`, `.Height`, `.Count`, `.SamplesPerCell`, `.Index(x,y)` and **writes** `TerrainWorld.Terrain[index]` (`TerrainWorld.cs`).
- Reads `TerrainGenerationSettings.BeachWidth`, `.LakeShoreWidth`, `.Preset`, `.UseClimateBiomeMaps` (`TerrainGenerationSettings.cs`).
- Calls `TerrainGeometry.DistanceTo(bool[], width, height)` and `TerrainGeometry.Neighbours(x, y, width, height)` (`TerrainGeometry.cs`) for the ocean/lake distance fields and the land-adjacency check in `TouchesLand`.
- Assumes `TerrainElevationStage`/relief-assigning stages and `TerrainClimateStage` have already populated `world.Elevation`, `world.Relief`, `world.Temperature` and `world.Moisture` before it runs — it only reads those fields, never computes them.
- Does not depend on `TerrainAuthoring`, `ResourceDefinition`, or `SeededTerrainPropScatterComponent`.

## Notes

- The `Bands` moisture cutoffs (0.20 / 0.38 / 0.78) are fixed constants, not settings-driven; the extensive comment on `Bands` documents that this is intentional (a quota/percentile approach was tried, measured, and reverted after it collapsed desert and dry-grass biomes to zero on real maps) — this is design history worth knowing, not a bug.
- `ResourceCategory`/relief-kind concerns from `ResourceDefinition.cs` are unrelated to this file; there is no shared logic to flag there.
- No silent-failure paths: `Apply` has no early-return branches that skip work, and every land/water cell is always assigned exactly one terrain-kind string.
