# TerrainCoherenceStage

Generation stage in the terrain pipeline, run by `TerrainFieldBuilder` after the rainfall biome table has classified every land sample.

`TerrainCoherenceStage` turns a per-sample biome classification (which necessarily produces lone, isolated tiles wherever rainfall crosses a threshold) into coherent regions a tilemap can render sensibly. It does this in two passes: `Smooth`, a Moore-neighbourhood majority filter that reassigns an isolated rainfall-biome sample to whatever its tile-distant neighbours mostly are; and `AbsorbSmallRegions`, a flood-fill pass that finds whole connected biome regions below a minimum-size fraction of the landmass and reassigns every tile in them to whichever eligible biome borders the region most (or a computed fallback if nothing eligible borders it). Only rainfall-derived biomes (desert/dry_grass/grass/swamp/jungle) are smoothed; a wider "absorbable" set that also includes snow and tundra can have its *regions* dissolved, because those two are placed by altitude cooling and can appear as a couple of tiles of arctic ground on an otherwise temperate small island.

## Public API

- `internal static void Apply(TerrainGenerationBuffer world, TerrainGenerationSettings settings)` — the only public entry point; runs `Smooth` then `AbsorbSmallRegions` in that order.

Everything else (`Smooth`, `AbsorbSmallRegions`, `AbsorbOnce`, and the `Rainfall`/`Absorbable`/`PeakMaterials`/`AbsorbTargets` static sets) is `private` to the class, which is itself `internal static`.

## Dependencies

- Reads and writes `TerrainGenerationBuffer.Terrain` (from `TerrainGenerationBuffer.cs`) — both passes reassign per-sample terrain-kind strings in place.
- Reads `TerrainGenerationBuffer.Land` and `.Relief`: land gates the passes; relief restricts peak-material targets for snow/tundra regions. Rainfall ground is excluded from those targets regardless of elevation.
- Reads `TerrainGenerationBuffer.Width`, `Height`, `Index`, `SamplesPerCell` (from `TerrainGenerationBuffer.cs`) — `SamplesPerCell` is used as the neighbour-sampling "reach" in `Smooth` so the majority filter operates at tile granularity, not sub-tile sample granularity.
- Reads `TerrainGenerationSettings.MinBiomeRegionFraction`, `BiomeCoherencePasses`, `BiomeCoherenceKeep` (from `TerrainGenerationSettings.cs`) to gate/tune both passes.
- Called by `TerrainFieldBuilder.Build` (outside this batch), after the biome/rainfall table has been applied and (per that file's ordering) elevation classification.

## Notes

- Rainfall-derived regions cannot be absorbed into rock/gravel/snow, even when raised. This prevents a neighboring rocky summit from consuming green ground. Non-rainfall snow/tundra regions can merge into peak material only when at least half their samples are raised.
- The `AbsorbSmallRegions` fallback-when-nothing-borders logic is also explained via an observed failure: a snow patch ringed only by sand had no eligible border and stayed, "so widening the beach put arctic ground on a temperate island." The `fallback` (most-common rainfall biome on the map) exists specifically to prevent an undersized region surviving for want of a neighbour.
- `AbsorbSmallRegions` runs up to 8 passes (`for (int pass = 0; pass < 8; pass++)`) and stops early once a pass changes nothing; 8 is a hardcoded cap with no `[Export]` or settings field backing it.
- `Smooth`'s per-cell neighbour sampling explicitly steps by `SamplesPerCell` tiles rather than by one sample — the doc comment explains a one-sample neighbourhood would smooth within-tile noise but leave the tile-sized speckle (the only visible artifact) untouched. Code matches: `nx = x + (dx * reach)`.
