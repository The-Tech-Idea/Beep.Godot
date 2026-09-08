# GridJobBlock

Internal enum naming why a cell cannot take a job, as decided by [GridCellRules](GridCellRules.md).

A reason, not a message. The two components that ask — the click/toolbar path and the settler-style selection path — report the same condition in different words, and those words are each component's public signal API: `GridToolActionComponent.ToolRejected` has always said `unworkable_terrain`, `GridSelectionJobCommandComponent.QueueFailed` has always said `unqueueable_terrain`. Returning a reason rather than a string is what let the rule be shared without either component changing what it tells its listeners.

## Public API
- `OutOfBounds` — outside the navigation grid's bounds.
- `Blocked` — blocked, by navigation or by the cell's own `Blocked` flag. One value for both, because both callers already reported both as `blocked_cell`.
- `UnworkableTerrain` — the terrain kind here is not one that can be worked.

## Dependencies
None; a bare enum. Produced by `GridCellRules.QueueBlock`, consumed by `GridToolActionComponent.WorkJobBlockReason` and `GridSelectionJobCommandComponent.QueueBlockReason`, each of which maps it to its own strings.

## Notes
- `internal` — the mapping to public signal strings happens at the call sites inside the addon, so no consumer outside it ever sees this type.
- A null `GridJobBlock?` is the "no reason, the cell is fine" answer; there is deliberately no `None` member to be confused with it.
