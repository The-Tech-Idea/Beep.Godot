# GridSelectionJobCommandComponent

`GridSelectionJobCommandComponent` turns a set of cells — either the current `GridSelectionComponent` selection, an explicit cell array, or a rectangle — into jobs on `GridJobQueueComponent`. It is the input-to-job bridge for settler-style commands: the class doc comment names the pairing directly — "Pair with GridSelectionComponent and GridJobQueueComponent for settler-style commands such as clear land, prepare pad, harvest, repair, build, deliver, or inspect."

It exists so that queuing work from a selection isn't just "loop and call AddJob" repeated per project: it applies the same navigation-bounds and terrain-eligibility filtering the rest of the grid system uses (`GridTerrainRules`), silently drops individually-invalid cells rather than aborting the whole batch, and — notably — distinguishes *why* zero jobs were queued. `QueueFailed` fires with `"no_cells"` when nothing was even offered versus `"no_valid_cells"` when cells were offered but every one was filtered out, so a caller or UI can tell "you selected nothing" from "everything you selected was blocked."

## Public API
- `int QueueSelectedCells(string kind = "", float workSeconds = -1f, int? priority = null)` — queues the resolved `GridSelectionComponent`'s current selection; clears the selection afterward if `ClearSelectionAfterQueue` and at least one job queued.
- `int QueueCells(Godot.Collections.Array cells, string kind = "", float workSeconds = -1f, int? priority = null)` — the core implementation: reads each cell via `GridVariantReader.TryReadCell`, filters via `QueueBlockReason`, calls `GridJobQueueComponent.AddJob` for each survivor, and emits `JobsQueued`/`QueueFailed` accordingly.
- `int QueueCells(Array<Vector2I> cells, ...)` — typed-array overload that boxes into a loose `Array` and delegates to the above.
- `int QueueRectangle(Vector2I a, Vector2I b, ...)` — convenience wrapper over `GridSelectionComponent.CellsInRect(a, b)`.
- `bool CanQueueJobAt(Vector2I cell, string kind = "")` — dry-run predicate.
- `float EffectiveWorkSeconds` — sanitized `WorkSeconds`.
- Signals: `JobsQueued(kind, count)`, `QueueFailed(reason)`.
- Exports: `SelectionPath`, `JobQueuePath`, `CellDataPath`, `NavigationPath`, `JobKind` (default `"clear_land"`), `WorkSeconds`, `Priority`, `ClearSelectionAfterQueue` (default on), `UseKeyboardShortcut` (default off) + `QueueShortcutKey` (default `Enter`), `UseNavigationBounds` (default on), `RejectNavigationBlockedCells` (default **off**), `TreatCellDataBlockedAsUnqueueable` (default **off**), `TreatBlockedTerrainKindsAsUnqueueable` (default on), `BlockedTerrainKinds` (defaults to `GridTerrainRules.DefaultBlockedTerrainKinds()`), `AllowedTerrainKinds`.

## Dependencies

- Calls `GridJobQueueComponent.AddJob` (this batch) for every eligible cell.
- Resolves `GridSelectionComponent` (outside this batch) for `GetSelectedCells`/`ClearSelection`, and its static `CellsInRect`.
- Resolves `GridCellDataComponent`/`GridNavigationComponent` (outside this batch) and uses `GridTerrainRules`/`GridVariantReader` (outside this batch) — `GridVariantReader.TryReadCell` here is the same helper family `GridJobQueueComponent.LoadJobs` uses (`TryDictionary`/`Int`/`Float`/`Vector2I`), so the two files share one cell/variant parsing utility rather than each rolling their own.

## Notes

- `TreatCellDataBlockedAsUnqueueable` defaults **off** here, while the structurally equivalent `TreatCellDataBlockedAsUnspawnable` on `GridWorkerSpawnerComponent` (this batch) defaults **on** — two components applying "the same" cell-data-blocked-flag rule to eligibility default oppositely, with nothing in either file explaining the asymmetry. Might be intentional (a blocked cell is still worth queuing clear-land work on; it isn't worth spawning a unit onto), but it isn't stated anywhere.
- `RejectNavigationBlockedCells` also defaults off — by default this component queues jobs onto cells a pathfinder can't reach a worker to, and it's `GridWorkerComponent`'s job/`GridPathFollowerComponent.MoveToCell` returning false that would fail such a job later with `"no_path"`, not this component catching it up front.
- The two `QueueCells` overloads are a simple call-through (typed array → loose array → shared implementation), not duplicated filtering logic.
