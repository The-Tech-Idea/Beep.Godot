# GridResourceNodeComponent

Gameplay component: a single harvestable resource deposit on the grid — a tree, rock, berry bush, scrap pile, crate, or oilfield supply cache. Workers gather it through the grid job queue, and every gather adds the yield to `GridResourceWalletComponent`.

The file's own design comment draws a deliberate line between two owners of one resource: `Catalog` (a `ResourceCatalog` of `ResourceDefinition` assets, defined in the terrain-engine batch) owns what a resource *is* — the same answer everywhere it appears — while this component owns *this* deposit: which cell it sits on and how much is left in it. `ApplyCatalogDefinition()` runs on `_Ready()` and seeds `Amount`, `AmountPerGather`, `GatherSeconds`, `GatherJobKind`, and `MarkCellOccupiedOnReady` from the catalog whenever `Catalog.Find(ResourceId)` resolves — seeded rather than clamped, because from that point on `Amount` is this deposit's own remaining stock, not a value the catalog keeps re-asserting. Leaving `Catalog` unset falls back to the component's own exports, which is the correct behavior for a game that places a few nodes by hand and has no resource system.

## Public API
- `const string ResourceNodeGroup = "grid_resource_nodes"` — group every node adds itself to in `_Ready()`, so other systems can enumerate all deposits without a NodePath to each one.
- Signals: `GatherQueued(jobId, x, y)`, `Gathered(resourceId, amount, remainingAmount)`, `GatherRejected(reason)`, `Depleted()`.
- `[Export] NodePath GridPath/PlacementPath/ResourceWalletPath/JobQueuePath` — resolved lazily to `GridProjectionComponent`, `GridPlacementComponent`, `GridResourceWalletComponent`, `GridJobQueueComponent`.
- `[Export] bool UseExplicitCell` / `Vector2I Cell` — when true, `Cell` is authoritative instead of deriving the cell from `GlobalPosition` via the grid.
- `[Export] string ResourceId` — the resource id this deposit is, normalized (lowercased, spaces to underscores) before use.
- `[Export] ResourceCatalog? Catalog` — see above; when set and it defines `ResourceId`, overrides the exports below on `_Ready()`.
- `[Export] int Amount / AmountPerGather`, `string GatherJobKind`, `float GatherSeconds`, `int GatherPriority` — fallback gather economics used only when the catalog doesn't define this id.
- `[Export] bool HideWhenDepleted/DisableProcessWhenDepleted/QueueFreeWhenDepleted` — what happens to the node itself once exhausted.
- `[Export] bool MarkCellOccupiedOnReady` / `ReleaseOccupiedCellWhenDepleted` — whether this deposit blocks its cell in `GridPlacementComponent`, and whether depleting frees it.
- `bool IsDepleted { get; }` — `_depleted || Amount <= 0`.
- `int RemainingAmount { get; }` — `Amount` clamped to non-negative.
- `string ActiveGatherJobId { get; }` — the job id currently outstanding for this deposit, if any.
- `Vector2I CurrentCell()` — `Cell` when `UseExplicitCell`, otherwise the grid's cell for `GlobalPosition`; returns `(int.MinValue, int.MinValue)` if no grid is resolved.
- `string QueueGatherJob()` — enqueues a gather job on the job queue at this deposit's cell and returns the job id; returns the existing `ActiveGatherJobId` unchanged if one is already outstanding; emits `GatherRejected` and returns `""` if there's no job queue, the deposit is depleted, or the cell is invalid.
- `bool Gather()` — shorthand for `GatherForJob("")`.
- `bool GatherAllForJob(string jobId)` — repeatedly gathers (bounded to 10000 iterations) until depleted or a gather fails; returns whether at least one gather succeeded.
- `bool GatherForJob(string jobId)` — removes `min(max(1, AmountPerGather), Amount)` units, adds them to the wallet, clears the active job if `jobId` matches, emits `Gathered`, and calls `Deplete()` once `Amount` hits zero.
- `Godot.Collections.Dictionary CaptureState()` — cell, resource id, amount, amount-per-gather, depleted flag.
- `void RestoreState(Godot.Collections.Dictionary state)` — restores those fields; forces `UseExplicitCell = true` when a `"cell"` key is present; calls `Deplete()` if the restored state says depleted or `Amount <= 0`, otherwise resets `_depleted`, visibility and process mode and re-reserves the cell if configured to.

## Dependencies
- Resolves `GridProjectionComponent`, `GridPlacementComponent`, `GridResourceWalletComponent`, and `GridJobQueueComponent` via NodePath or, failing that, `EntityComponent.FindComponent` against the current scene.
- Reads `ResourceCatalog.Find(id)` (terrain-engine batch) for its `ResourceDefinition`.
- Calls `GridJobQueueComponent.AddJob/HasJob/GetJobState` and reacts to its `GridJobState.Completed`/`Cancelled` values to clear a stale `ActiveGatherJobId`.
- Calls `GridPlacementComponent.SetOccupied` to reserve/release its cell.
- **Consumed within this batch by `GridResourceScatterComponent`**, which instantiates or locates this component on each generated prop, sets `UseExplicitCell`, `Cell`, `ResourceId`, `Catalog`, `GatherPriority`, `MarkCellOccupiedOnReady`, rewires its NodePaths to relative paths, and subscribes a closure to its `Depleted` signal to release the cell it reserved.

## Notes
- `CaptureState()`/`RestoreState()` follow the same shape as `ISaveable`, but this class does not implement `ISaveable` and never joins `SaveableHelper.Group` — unlike `GridResourceWalletComponent` in this same batch, which does implement `ISaveable` with a matching `Save(GameStateData)`/`Load(GameStateData)` pair. Whatever externally persists resource-node state (presumably `GridResourceScatterComponent` or a level-save system, neither of which is confirmed to call these methods from this batch alone) must call `RestoreState` itself; nothing here registers this component with the save system automatically.
- `ApplyCatalogDefinition()` runs unconditionally on every `_Ready()`, before any external caller has a chance to call `RestoreState()`. If a scene re-enters the tree (e.g. after being freed and re-instantiated) without an explicit `RestoreState` call following it, the catalog's full `Amount` is reapplied even if the deposit had previously been partially or fully gathered — this is consistent with the documented catalog-owns-the-definition design, but it means the save/restore call order (`_Ready()` then `RestoreState()`) is load-bearing.
- `GatherAllForJob`'s early-exit check (`if (Amount >= before) break;`) is defensive: `GatherForJob` always removes at least 1 unit when it succeeds (since `IsDepleted` is already false), so `Amount` can never fail to shrink on a successful gather — the guard against a non-decreasing loop looks unreachable given the current `GatherForJob` implementation.
