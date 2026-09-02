# ResourceCatalog

World-data model: an authored `Resource` container holding the set of `ResourceDefinition`s a given world/game uses, shared between the terrain generator (deciding what to place) and the economy/UI (knowing what a placed resource means).

`ResourceCatalog` is a small `[Tool][GlobalClass]` list wrapper — a `CatalogName` and an array of `ResourceDefinition`s — plus lookup helpers keyed on the definition's `Id`. Assigning a different catalog to the generator (via `TerrainGenerationSettings.ResourceCatalog`, outside this batch) is the sanctioned way for a game to swap in its own resource set instead of the addon's built-in Historical/OilAndGas/Space catalogs (`ResourceCatalogs.cs`), so that the generator and every gameplay consumer stay looking at the same definitions.

## Public API

- `[Export] string CatalogName { get; set; } = "Resources"` — display/identifying name for the catalog asset.
- `[Export] Godot.Collections.Array<ResourceDefinition> Resources { get; set; } = new()` — the definitions this catalog holds.
- `ResourceDefinition? Find(string id)` — linear scan for the definition with matching `Id`; returns null for an empty/unmatched id.
- `bool Contains(string id)` — `Find(id) is not null`.
- `ResourceCategory CategoryOf(string id)` — `Find(id)?.Category ?? ResourceCategory.Bonus`; deliberately falls back to `Bonus` rather than throwing so a saved map referencing an id the current catalog doesn't define doesn't fail to load.
- `string DisplayNameOf(string id)` — the definition's `DisplayName`, or the raw `id` itself if the definition is missing or has no display name set.
- `Godot.Collections.Array<ResourceDefinition> ForTerrain(string terrainKind)` — every definition in the catalog whose `TerrainKinds` array contains the given terrain kind string; returns an empty array for an empty `terrainKind`.

## Dependencies

- Reads `ResourceDefinition.Id`, `.Category`, `.DisplayName`, `.TerrainKinds` (`ResourceDefinition.cs`).
- Consumed by `TerrainResourceStage.cs` (via `settings.ResourceCatalog ?? ResourceCatalogs.For(settings.ResourceSet)`, reading `Weight`/`RequiresRelief`/`RequiredRelief` off the definitions it returns — not through this class's own helper methods) and by `ResourceCatalogs.cs` (`Build` populates instances of this type).
- Also consumed outside this batch by `GridResourceScatterComponent.cs` (calls `Catalog.Contains(id)`).

## Notes

- `ForTerrain` and `DisplayNameOf` have no callers anywhere in the repo (checked via full-repo grep) — accepted-but-unread public API. The class doc comment frames `ForTerrain` specifically as one of the three sanctioned ways to use the catalog ("the generator asks it which resources belong on a terrain kind"), but the actual generator (`TerrainResourceStage.cs`) does its own inline filtering/weighting over `catalogue.Resources` instead of calling `ForTerrain` — the method the doc comment describes as load-bearing is not the one the generator actually uses.
- `CategoryOf(string id)` (this instance method, resolving against whichever catalog is actually assigned) has no callers either. The only caller found for "map an id to a category" is `TerrainResourceStage.CategoryOf(string id)` (a *different*, static method on a different class) used by `TerrainMapOverlayComponent.cs` for overlay colouring — and that static method resolves via `ResourceCatalogs.FindAnywhere(id)`, i.e. it searches only the three built-in catalogs (Historical/OilAndGas/Space), not whatever custom `ResourceCatalog` a game may have assigned to its generator. A game using a fully custom catalog with ids outside the three built-in sets would see its resources fall back to `ResourceCategory.Bonus` on the overlay even though this class's own (unused) `CategoryOf` would resolve them correctly against the assigned catalog. This is the duplicated-logic case rule 3 flags: two near-identical "id → category, default Bonus" implementations, and the one actually wired up is the one that ignores per-game customization.
