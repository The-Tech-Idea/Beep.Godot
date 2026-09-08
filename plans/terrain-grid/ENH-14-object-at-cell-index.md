# ENH-14 — One object-at-cell index; HUD status wires late-resolved sources

**Type:** enhancement (lookup cost + a small correctness fix) · **Area:** `GridPlacementComponent`, `GridObjectComponent`, `ui/GridObjectInspectorComponent`, `GridToolActionComponent`, `GridSelectionJobCommandComponent`, `ui/GridInteractionStatusComponent` · **Status:** proposed 2026-09-08 · **Effort:** S (1 day) · **Risk:** low

## Finding

1. **Object lookup by cell is a group scan.** `GridObjectInspectorComponent.FindSelectedObject` (`:864-884`) walks `GetNodesInGroup(GridObjectComponent.ComponentGroupName)`, filters by `IsNodeWithin(root)` (a parent walk per object), then tests every object's footprint against every selected cell — O(objects × cells) on each `SelectionChanged`. `GridToolActionComponent.FindResourceNodeAt` scans the resource-node group per completed gather job (tracker item E24). Both answer "what stands on this cell?" — a question `GridPlacementComponent` already tracks for occupancy (`IsOccupied(cell)`, footprint reserve/release) but only as a boolean.
2. **`GridInteractionStatusComponent.ConnectSignals`** (`:204-233`) connects once (`_connected = true`) in `_Ready`. `ResolveReferences` may fill a reference later (empty path → scene-wide search finds the component after a level loads); its signals are never connected, so the status line stops updating for that source. The other panels (`GridInteractionModeBarComponent`, `GridObjectivePanelComponent`) re-connect inside `ResolveReferences` when the instance changes — the pattern exists.

## Design

- `GridPlacementComponent` (owner of occupancy) keeps `Dictionary<Vector2I, GridObjectComponent> _occupant` beside its occupied set, filled by `ReserveFootprint`/cleared by `Release` (the object component already stamps both), and exposes `GridObjectComponent? ObjectAt(Vector2I cell)` and `IEnumerable<GridObjectComponent> ObjectsIn(Rect2I cells)`. Resource nodes are `GridObjectComponent`s with `OccupiesCell` when authored so; nodes that do not occupy register through a second, non-blocking map (`Register(node, cells)`) so `FindResourceNodeAt` is also O(1).
- Inspector, tool action and selection command call `ObjectAt`; the group scans go. `IsNodeWithin(root)` scoping is replaced by the placement instance itself (one placement per world → one index per world).
- `GridInteractionStatusComponent.ResolveReferences` adopts the connect-on-new-instance pattern (per source: disconnect old, connect new), same as the mode bar. Folds into DUP-12's base if that lands first (`BindSource<T>(ref field, path, connect, disconnect)` helper on `GridPanelComponent`).

## Guards (fail first)

- Probe: 2 000 placed objects, select one cell → `FindSelectedObject` performs no group enumeration (pin: `GetNodesInGroup(` not referenced from the inspector) and returns the right object; time < 0.05 ms. **Mutation:** restore the scan → pin fails.
- Probe: gather job completes on a node cell → node found via `ObjectAt`, depleted correctly (existing smoke).
- Probe: status panel with empty `PlacementPath`, placement component added **after** the panel's `_Ready` → a placement rejection updates the status text. **Mutation:** restore the once-only connect → text unchanged (this proves the bug).

## Dependencies / collisions

DUP-08 (`Covers`/footprint owner). `ecs/grid/` collision — placement is a core file; coordinate.

## Out of scope

Multi-object stacking per cell (footprint release is boolean today — a known limitation in the tracker; the index stores one occupant per cell to match).
