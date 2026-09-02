# ResourceCatalogs

World-data model / support utility: the addon's three built-in, statically-constructed `ResourceCatalog` instances (Historical, OilAndGas, Space), plus the lookup used to resolve an arbitrary resource id against whichever of them was actually used.

`ResourceCatalogs` is a static factory and registry for the resource sets the addon ships out of the box. Each catalog is built once (lazily, on first access) from a hardcoded tuple array of `(id, displayName, category, terrainKinds, weight)` entries via the private `Build` helper, which constructs a `ResourceDefinition` per tuple and adds it to a new `ResourceCatalog`. The class doc comment notes these were previously three private arrays living inside the generation stage itself, invisible to the game side; moving them here (as the shared `ResourceCatalog`/`ResourceDefinition` type) is what lets a wallet, HUD or resource node ask what an id like `"crude_oil"` actually is. A game wanting a different economy is expected to author its own `ResourceCatalog` and assign it on the generator rather than edit this file.

## Public API

- `static ResourceCatalog For(ResourceSet set)` — returns `OilAndGas`, `Space`, or `Historical` (the default/fallback arm) for the given `ResourceSet` enum value.
- `static ResourceCatalog Historical { get; }` — lazily-built catalog of 19 resources (Bonus: wheat/cattle/banana/deer/fish/whale/stone; Luxury: gems/spices/wine/furs/incense/ivory/silver; Strategic: horses/iron/coal/oil/aluminium/uranium) — a historical-4X-style resource table keyed to terrain kinds like `"grass"`, `"jungle"`, `"desert"`.
- `static ResourceCatalog OilAndGas { get; }` — lazily-built catalog of 12 hydrocarbon-themed resources (crude_oil, offshore_oil, natural_gas, offshore_gas, shale, oil_sands, condensate, helium, sulphur, salt_dome, brine, coalbed_methane), keyed to terrain kinds chosen to mimic real hydrocarbon geology per the doc comment (onshore sands/evaporite basins, offshore shelf, cold-bog heavy oil).
- `static ResourceCatalog Space { get; }` — lazily-built catalog of 12 off-world resources (water_ice, ammonia_ice, methane_ice, helium3, regolith, silicates, iron_ore, titanium, rare_earths, platinum, thorium, deuterium), keyed to terrain kinds standing in for cold-trap/exposed-rock geology.
- `static ResourceDefinition? FindAnywhere(string id)` — searches `Historical`, then `OilAndGas`, then `Space` (via each catalog's own `Find`) and returns the first match, or null.

## Dependencies

- Constructs instances of `ResourceCatalog` and `ResourceDefinition` (`ResourceCatalog.cs`, `ResourceDefinition.cs`), writing `Id`, `DisplayName`, `Category`, `Weight`, and `TerrainKinds` on each definition built by `Build`.
- Consumed by `TerrainResourceStage.cs` (`CatalogueFor` calls `ResourceCatalogs.For(settings.ResourceSet)` as the fallback when `settings.ResourceCatalog` is unset) and by `TerrainGenerationSettings.cs` (declares the `ResourceSet` enum this file switches on). `FindAnywhere` is used by the *other*, static `TerrainResourceStage.CategoryOf(string id)` helper (outside this batch), which `TerrainMapOverlayComponent.cs` calls for overlay colouring.

## Notes

- `FindAnywhere` only ever searches the three *built-in* catalogs. A game that assigns a fully custom `ResourceCatalog` to its generator (the addon's own sanctioned customization path, per `ResourceCatalog.cs`'s doc comment) will have resource ids that `FindAnywhere` cannot find — and `TerrainResourceStage.CategoryOf`, the method actually wired to `TerrainMapOverlayComponent`'s overlay colouring, resolves categories exclusively through `FindAnywhere`. So a custom catalog's resources silently render as `ResourceCategory.Bonus` on the map overlay regardless of what category they were actually assigned — see the matching note in `ResourceCatalog.md` about the unused instance-level `CategoryOf` that *would* resolve them correctly. This is the accepted-but-unread path: the "assign your own catalog" feature is real and read by generation, but not by this one downstream consumer.
- The three catalogs are built lazily via `??=` with no thread-safety guard; harmless under Godot's single-threaded scripting model but worth noting if this is ever called from a background generation thread.
- Data-only file: no gameplay logic, no dead code, no TODOs. The per-catalog doc comments (geology rationale for OilAndGas/Space) are design notes that match the terrain-kind arrays beneath them.
