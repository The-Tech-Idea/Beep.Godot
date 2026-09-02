# TerrainCoherenceStage

Generation stage in the terrain pipeline, run by `TerrainFieldBuilder` after the rainfall biome table has classified every land sample.

`TerrainCoherenceStage` turns a per-sample biome classification (which necessarily produces lone, isolated tiles wherever rainfall crosses a threshold) into coherent regions a tilemap can render sensibly. It does this in two passes: `Smooth`, a Moore-neighbourhood majority filter that reassigns an isolated rainfall-biome sample to whatever its tile-distant neighbours mostly are; and `AbsorbSmallRegions`, a flood-fill pass that finds whole connected biome regions below a minimum-size fraction of the landmass and reassigns every tile in them to whichever eligible biome borders the region most (or a computed fallback if nothing eligible borders it). Only rainfall-derived biomes (desert/dry_grass/grass/swamp/jungle) are smoothed; a wider "absorbable" set that also includes snow and tundra can have its *regions* dissolved, because those two are placed by altitude cooling and can appear as a couple of tiles of arctic ground on an otherwise temperate small island.

## Public API

- `internal static void Apply(TerrainWorld world, TerrainGenerationSettings settings)` — the only public entry point; runs `Smooth` then `AbsorbSmallRegions` in that order.

Everything else (`Smooth`, `AbsorbSmallRegions`, `AbsorbOnce`, and the `Rainfall`/`Absorbable`/`PeakMaterials`/`AbsorbTargets` static sets) is `private` to the class, which is itself `internal static`.

## Dependencies

- Reads and writes `TerrainWorld.Terrain` (from `TerrainWorld.cs`) — both passes reassign per-sample terrain-kind strings in place.
- Reads `TerrainWorld.Land`, `TerrainWorld.Relief` (from `TerrainWorld.cs`) — `Land` gates every operation to land samples only; `Relief` (`TerrainRelief.Flat` vs. raised) decides whether a dissolving region is allowed to become a peak material (rock/gravel/snow) or not.
- Reads `TerrainWorld.Width`, `Height`, `Index`, `SamplesPerCell` (from `TerrainWorld.cs`) — `SamplesPerCell` is used as the neighbour-sampling "reach" in `Smooth` so the majority filter operates at tile granularity, not sub-tile sample granularity.
- Reads `TerrainGenerationSettings.MinBiomeRegionFraction`, `BiomeCoherencePasses`, `BiomeCoherenceKeep` (from `TerrainGenerationSettings.cs`) to gate/tune both passes.
- Called by `TerrainFieldBuilder.Build` (outside this batch), after the biome/rainfall table has been applied and (per that file's ordering) elevation classification.

## Notes

- `AbsorbOnce`'s peak-material guard has a detailed comment describing an observed bug it fixes: without restricting rock/gravel/snow as absorption targets to regions that are actually raised, a flat region beside a rocky summit was being absorbed into rock, and the comment cites a measured "three islands came out 66 to 70% rock while only 9–11% of them was raised at all." This reads as a genuine post-mortem note, not stale documentation — the code (`raised * 2 < region.Count` removing peak materials from `borders`) matches the claim.
- The `AbsorbSmallRegions` fallback-when-nothing-borders logic is also explained via an observed failure: a snow patch ringed only by sand had no eligible border and stayed, "so widening the beach put arctic ground on a temperate island." The `fallback` (most-common rainfall biome on the map) exists specifically to prevent an undersized region surviving for want of a neighbour.
- `AbsorbSmallRegions` runs up to 8 passes (`for (int pass = 0; pass < 8; pass++)`) and stops early once a pass changes nothing; 8 is a hardcoded cap with no `[Export]` or settings field backing it.
- `Smooth`'s per-cell neighbour sampling explicitly steps by `SamplesPerCell` tiles rather than by one sample — the doc comment explains a one-sample neighbourhood would smooth within-tile noise but leave the tile-sized speckle (the only visible artifact) untouched. Code matches: `nx = x + (dx * reach)`.
