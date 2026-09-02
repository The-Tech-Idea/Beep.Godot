# TerrainResourceStage

Generation stage in the terrain pipeline, run by `TerrainFieldBuilder` after `TerrainScaleConstraintStage.ApplyTerrain` and before `TerrainFeatureStage.Apply` — i.e. after the land itself (including lake draining) has settled, but before woods/features are placed on it.

`TerrainResourceStage` scatters resource ids (e.g. `wheat`, `iron`, `crude_oil`, `helium3`) onto the reduced gameplay-tile grid, one per eligible cell, weighted by which resources the cell's terrain kind (and relief, for relief-gated resources like ore) actually supports. Placement uses a deterministic hash of the seed and cell coordinates so the same seed always produces the same layout, applies a lower density on water than on land so the sea doesn't end up holding most of the map's resources, and enforces a minimum spacing between two placements of the same resource id so one resource doesn't cluster into a single spot. It also exposes a lookup, `CategoryOf`, used by other components to classify an already-placed resource id without needing to know which catalogue produced it.

## Public API

- `internal static void Apply(TerrainWorld world, TerrainGenerationSettings settings)` — the stage's only mutating entry point. No-ops immediately if `settings.ResourceDensity <= 0`. Otherwise, for every gameplay cell, rolls a hash-based chance (land density 0.085, water density scaled by ×0.22, both further scaled by `settings.ResourceDensity`), and on a hit, weight-picks a supported resource id for that cell's terrain/relief via a second hash roll, then places it in `world.Resource[cell]` if it clears the same-resource spacing check (4 tiles).
- `internal static ResourceCategory CategoryOf(string id)` — looks the id up across every shipped catalogue (not just the one the current settings selected) via `ResourceCatalogs.FindAnywhere`, returning `ResourceCategory.Bonus` if the id isn't found in any of them. Exists because a saved/loaded map can carry resource ids from a catalogue the generator is no longer configured for.

Also defined in this file: the public `ResourceSet` enum (`Historical`, `OilAndGas`, `SpaceExploration`) naming which shipped resource catalogue a map draws from.

Everything else (`Density`, `WaterDensityScale`, `SameResourceSpacing`, `CatalogueFor`, `Choose`, `Supports`, `FarEnough`, `Hash01`) is private to the `internal static class TerrainResourceStage`.

## Dependencies

- Reads `TerrainWorld.CellTerrain`, `TerrainWorld.CellWater`, `TerrainWorld.CellRelief`, `TerrainWorld.CellsWide`/`CellsHigh`; writes `TerrainWorld.Resource[cell]` (all `TerrainWorld.cs`).
- Reads `TerrainGenerationSettings.ResourceDensity`, `.Seed`, `.ResourceCatalog`, `.ResourceSet` (`TerrainGenerationSettings.cs`).
- Reads `ResourceCatalog.Resources` and `ResourceCatalogs.For(ResourceSet)` / `ResourceCatalogs.FindAnywhere(id)` (`ResourceCatalog.cs`, `ResourceCatalogs.cs`) to get the weighted resource list and, for `CategoryOf`, to search every catalogue.
- Reads `ResourceDefinition` fields (`Id`, `Weight`, `RequiresRelief`, `RequiredRelief`, `TerrainKinds`, `Category`) (`ResourceDefinition.cs`).
- Reads the `WaterBody` and `TerrainRelief` enums (used to test land/water and relief-gated resources).
- Consumed by: `TerrainFieldBuilder.Build` (calls `Apply`), `TerrainResourceRendererComponent`/`TerrainDataLayersComponent`/`TerrainMapOverlayComponent` and others (read `world.Resource` indirectly via `TerrainGeneratorComponent.ResourceAt`), and `TerrainMapOverlayComponent` (calls `CategoryOf` to colour the overlay).

## Notes

- `CatalogueFor` prefers `settings.ResourceCatalog` (an author-supplied catalogue) over the shipped `ResourceCatalogs.For(settings.ResourceSet)` table — the comment states this is "the same one" object the game side reads, but this file only ever reads it; it does not itself verify that game code reads the identical catalogue instance.
- `Choose`'s weighted-roll pattern (accumulate total weight, roll `Hash01(...) * total`, walk again subtracting weights until `roll <= 0`) is the same two-pass weighted-selection idiom as elsewhere in the terrain stages that do seeded weighted picks; not a copy-paste duplicate here, just the same technique reused, consistent with the file's own comment that this is "the standard" approach.
- `FarEnough` scans the entire `placed` list (every resource placed so far, any id) for every candidate cell and filters by id inside the loop, rather than indexing placements by id — O(total placements) per candidate, not O(same-id placements). Fine at the current density (~8.5% of land) and the map sizes this pipeline targets, but not a spatial index.
- No dead code, stubs, or TODOs found in this file.
