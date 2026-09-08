# GridCellRules

Internal readonly struct holding the one implementation of "can this cell be worked, and can it take a job." A component builds one from its own collaborators and its own exported policy, then asks it — the struct is a value, not a service, so there is nothing to wire, resolve, or keep in sync.

It exists because `GridToolActionComponent` (the click/toolbar path) and `GridSelectionJobCommandComponent` (the settler-style selection path) each derived this independently, and the copies had already diverged: only the selection path consulted `GridCellDataComponent.CellFlags.Blocked`, and only the tool path consulted the terrain engine's generated map. A project wiring both a farming toolbar and a selection command got two different answers for the same cell, with nothing anywhere documenting the asymmetry as intended. Same consolidation `GridTerrainRules` got for what a terrain-kind string *means*, one layer up: this struct is about a whole cell, terrain plus bounds plus flags.

**Cells are the map.** The live terrain kind of a cell has one owner, `GridCellDataComponent`: it is what the generator fills, what the player edits, and what the save carries. The terrain engine's `TerrainDataLayersComponent` is a projection of the *generated* world — the recipe's answer, not the live map — and this rule never consults it for kind. It used to, and the layers won: a rebuilt map overrode every restored or edited cell wherever it had a tile, so an edit survived a save only where the layers had nothing to say. OpenTTD and Widelands each keep one tile array that *is* the saved map; the cells are that array.

## Public API
- `GridNavigationComponent? Navigation`, `GridCellDataComponent? Cells` — the collaborators, either of which may be null; the rule degrades to what it can actually judge by.
- `bool UseNavigationBounds`, `bool RejectNavigationBlockedCells`, `bool RejectCellDataBlockedCells`, `bool TreatBlockedTerrainKindsAsBlocking` — the asking component's own policy switches, passed in rather than assumed.
- `Godot.Collections.Array<string> BlockedTerrainKinds`, `AllowedTerrainKinds` — the asking component's own exported lists.
- `static string TerrainKindAt(GridCellDataComponent?, Vector2I cell)` — the terrain kind in force at a cell, normalized, from the one owner; empty with no cell data wired. Static so `GridPlacementComponent` (whose placement rule is genuinely different and stays its own) and `GridNavigationComponent`'s per-search snapshot read terrain the one way rather than each carrying a copy of the rule.
- `string TerrainKindAt(Vector2I cell)` — the instance form, over this rule's own cells.
- `bool CanWorkTerrain(Vector2I cell)` — bounds and terrain kind only. With no cell data wired there is nothing to judge by and the answer is yes. An emptied `BlockedTerrainKinds` means nothing is blocked, matching every other consumer of that export.
- `GridJobBlock? QueueBlock(Vector2I cell)` — the full queueability rule; null when the cell can take a job.

## Dependencies
`GridTerrainRules` for normalization, allow-list and blocked-list membership; `GridNavigationComponent.IsInBounds`/`IsBlocked`; `GridCellDataComponent.HasFlag`/`GetTerrainKind`. Consumed by `GridToolActionComponent` (`CanWorkTerrain`, `WorkJobBlockReason`) and `GridSelectionJobCommandComponent` (`QueueBlockReason`); `GridPlacementComponent` and `GridNavigationComponent` use only the static `TerrainKindAt`.

## Notes
- `QueueBlock` returns a `GridJobBlock`, never a string, on purpose: the two callers report the same condition in their own signal vocabulary (`unworkable_terrain` vs `unqueueable_terrain`), and that wording is each component's public API. One rule, two vocabularies — the mapping stays at each call site.
- `QueueBlock` checks navigation bounds and then calls `CanWorkTerrain`, which checks them again. The repetition is deliberate: `CanWorkTerrain` is also called directly by the Clear/Hoe/Plant tool actions, which never go through `QueueBlock`, so it has to be correct standing alone.
- `RejectNavigationBlockedCells` is read only by `QueueBlock`. A navigation-blocked cell is still *workable* — that asymmetry is the pre-existing behaviour of both callers, preserved.
- Pinned by `tests/addon_contract_scan.ps1`, which requires every term of the rule to be present, forbids the file re-embedding a hardcoded `"water" or "sea" or ...` list, forbids any `DataLayers` reference here, and forbids every file under `ecs/grid/` other than the four resource/liquid/underground readers (`GridResourceScatterComponent`, `GridProspectingComponent`, `GridExtractorComponent`, `GridSubsurfaceStoreComponent`) from referencing `TerrainDataLayersComponent` at all.
