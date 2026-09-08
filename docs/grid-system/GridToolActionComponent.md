# GridToolActionComponent

`GridToolActionComponent` is the unified Stardew-style tool dispatcher for click/toolbar-driven games: one `ToolAction` enum (`Clear`, `Hoe`, `Water`, `Plant`, `Harvest`, `QueueJob`, `Road`, `RemoveRoad`) applied to a target cell, the whole current selection, or the hovered cell, with each action carrying its own eligibility rules and — for `Plant`/`Harvest` — integration with a crop catalog, calendar, and resource wallet. The class doc comment frames it directly: "Stardew-style grid tool actions for click/toolbar driven games. It applies hoe, water, plant, harvest, clear, and job-queue actions to a target cell or to the current GridSelectionComponent selection."

The component resolves nine collaborators and shares tool behavior between player input and completed jobs. Planting charges seed cost before mutation and refunds it if planting fails. Terrain eligibility reads `GridCellDataComponent` through `GridCellRules`: the live cells, not a separate generated terrain array, own the player's edited map.

## Public API
- `bool CanClearCell(Vector2I cell)` checks clear eligibility without modifying terrain, collecting resources or emitting tool signals. Job completion uses this before gathering a clear target's deposit. This is terrain-kind eligibility, not a general navigation or occupancy test.
- `enum ToolAction { Clear, Hoe, Water, Plant, Harvest, QueueJob, Road, RemoveRoad }`.
- `int ApplyCurrent()` / `int Apply(ToolAction action)` — applies to the current selection if `ApplyToSelectionWhenPresent` and non-empty, else the selection's hover cell, else fails with `"no_target_cell"`.
- `int ApplyToCells(Godot.Collections.Array cells, ToolAction action)` / typed-array overload — applies to each cell, returns the success count.
- `bool ApplyToCell(Vector2I cell, ToolAction action)` — the per-cell dispatcher; emits `ToolApplied`/`ToolRejected` and requires `_cells` (`GridCellDataComponent`) to be resolved for anything but an invalid cell.
- Signals: `ToolApplied(action, x, y)`, `ToolRejected(action, x, y, reason)`.
- Exports (selected): `GridPath`, `CellDataPath`, `SelectionPath`, `JobQueuePath`, `RoadPath`, `NavigationPath`, `CropCatalogPath`, `CalendarPath`, `ResourceWalletPath`, `CurrentAction`, `RoadKind`/`RoadCostMultiplier`, `CropId`/`CropDaysToMature`, `JobKind`/`JobWorkTurns`/`JobPriority`, `ApplyToSelectionWhenPresent` (default on), `UseMouseInput` (default off), `AddHarvestYieldToWallet` (default on), `ConsumeSeedsFromWallet` (default on), `UseNavigationBounds` (default on), `RejectNavigationBlockedCellsForJobs` (default off), `TreatBlockedTerrainKindsAsUnworkable` (default on), `BlockedTerrainKinds` (defaults to `GridTerrainRules.DefaultBlockedTerrainKinds()`), `AllowedTerrainKinds`.

## Dependencies

- Calls `GridJobQueueComponent.AddJob` (this batch) for the `QueueJob` action.
- Called into **by** `GridJobEffectComponent` (this batch), which routes completed `"harvest"` jobs through `ApplyToCell(cell, ToolAction.Harvest)` when `UseToolActionForHarvest` is on — so this file's harvest logic (wallet payout, crop-catalog yield lookup) is shared between a direct player click and a completed job effect, not reimplemented twice.
- Explicit collaborator paths are resolved on each operation, including the queue. Replacing a node or changing its path cannot retain the previously resolved node. Empty paths use `EntityComponent.Resolve` discovery.

## Notes

- Terrain kind is judged from `GridCellDataComponent` alone, through `GridCellRules`. The component used to carry a `DataLayersPath` to the terrain engine's data layers as a preferred kind source; that was removed when cells became the one owner of live kind (see `GridCellRules.md`) — the layers publish the generated world, not the map the player has edited.
- Terrain allow/block rules are delegated to `GridCellRules`; this component does not carry a second terrain-kind classifier.
- `ApplyHarvest` fails closed rather than silently discarding yield: if `AddHarvestYieldToWallet` is on and `ResourceWalletPath` is set but hasn't resolved to an actual node, it rejects with `"missing_resource_wallet"` instead of harvesting the crop and dropping the reward.
- `ApplyPlant`'s season check (`_cropCatalog.CanPlant(cropId, _calendar.Season)`) only runs if **both** the crop catalog and calendar are resolved — a scene missing either wallet-adjacent system silently skips season gating rather than blocking planting.
