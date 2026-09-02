# TerrainTileReductionStage

Generation stage: the last step of the fine-field pipeline, converting the sub-tile sample grid into one gameplay-tile-resolution dataset.

Everything upstream of this stage (elevation, water, relief, terrain-kind assignment) operates on a fine sample grid — several samples per gameplay tile — because that resolution is what produces good coastlines, mountain ranges and river courses. But a game moves units, paths and cities on whole tiles, not samples. `TerrainTileReductionStage` collapses each tile's block of samples down to a single terrain kind, relief band, water body, shade and elevation, so the renderer and the game state read from the same tile-grained truth. It uses two different reduction rules deliberately: terrain and relief take the sample-block *majority* (so a small feature that doesn't cover the tile centre isn't lost by point-sampling), while rivers count as a tile if *any* small fraction (10%) of samples are river, because a majority rule would delete a river that is narrower than a tile and break its connectivity into disconnected puddles.

## Public API

- `internal static void Apply(TerrainWorld world)` — the only entry point. For every gameplay cell, tallies its block of fine samples (land/ocean/lake/river counts, per-terrain-kind counts overall and per-relief-band, summed shade and elevation) and writes one reduced value per cell: `world.CellShade`, `world.CellElevation`, `world.CellWater`, `world.CellRelief`, `world.CellTerrain`. A cell becomes water (`Ocean`/`Lake`/`River`, by whichever water count is largest) when water samples outnumber land samples; otherwise it becomes a river tile if river samples meet the 10% threshold; otherwise it is land, with relief picked as the sample-block's largest relief-band count and terrain picked as the majority terrain kind *within that band* (falling back to the whole tile's majority if no sample fell in the winning band).
- `private const float RiverTileFraction = 0.10f` — the minimum fraction of a tile's samples that must be river for the tile to become a river tile (not exported, not a tunable — a hardcoded threshold justified in the class doc comment as needing to stay low so rivers stay connected).
- `private static string MostCommon(Dictionary<string,int> counts, string fallback)` — returns the key with the highest count, or `fallback` if the dictionary is empty or all counts are zero.
- `private static int LargestIndex(int[] values)` — returns the index of the largest value in a 3-element relief-band count array.

## Dependencies

- Reads from `TerrainWorld` (defined in `TerrainWorld.cs`): `CellsWide`, `CellsHigh`, `SamplesPerCell`, `InBounds`, `Index`, and the fine-resolution arrays `Shade`, `Land`, `Elevation`, `Relief`, `Terrain`, `Water`.
- Writes to `TerrainWorld`'s tile-resolution arrays: `CellShade`, `CellElevation`, `CellWater`, `CellRelief`, `CellTerrain`.
- Uses the `TerrainRelief` and `WaterBody` enums (defined alongside `TerrainWorld`/`GeneratedTerrainField`).
- Called by `TerrainFieldBuilder.cs` (`TerrainTileReductionStage.Apply(world)`), the pipeline orchestrator that runs all generation stages in order — this is not visible from the file itself but is the file's only caller in the codebase.
- Everything downstream that reads `CellTerrain`/`CellRelief`/`CellWater` (renderers, `TerrainDataLayersComponent`, `TerrainGeneratorComponent.TerrainKindAt`) depends on this stage having already run.

## Notes

- No accepted-but-unread settings: `RiverTileFraction` is a private constant, not an `[Export]`, so there is nothing here a designer can configure and have silently ignored.
- The two-majority disagreement the file's own comments call out (terrain majority vs. relief majority landing on different sample subsets, e.g. a snowfield tile on flat ground) is explicitly handled by picking relief first and then re-deriving terrain from only the samples inside the winning relief band, falling back to the whole-tile terrain majority when that band has zero land samples in it. This is a real fix, not just a comment — worth noting because the equivalent fallback-ordering care is easy to regress if this method is edited.
- `mostlyWater` uses strict `>` (`(ocean + lake + river) > land`), so an exact 50/50 split between water and land samples resolves to land, not water — an edge case with no explicit test or comment, though the effect is minor at typical `SamplesPerCell` values (odd number of total samples usually rules out exact ties).
- Class-level XML doc is accurate and matches the code precisely — no stale comments found.
