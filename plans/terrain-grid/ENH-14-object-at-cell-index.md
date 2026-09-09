# ENH-14 — One object-at-cell index; HUD status wires late-resolved sources

**Type:** enhancement (lookup cost + a small correctness fix) · **Area:** `GridPlacementComponent`, `GridObjectComponent`, `ui/GridObjectInspectorComponent`, `GridToolActionComponent`, `GridSelectionJobCommandComponent`, `ui/GridInteractionStatusComponent` · **Status:** **PARTIALLY IMPLEMENTED 2026-09-09** (status late-source fix done; the object-at-cell index deferred) · **Effort:** S (1 day) · **Risk:** low

## Outcome (finding 2: status wires late-resolved sources, 2026-09-09)

`GridInteractionStatusComponent` connected its four sources once, behind a single `_connected` flag set in `_Ready`; a source that resolved later (an empty or not-yet-present path filled once the collaborator appears) never got its signals connected, so the readout went dead for it. Replaced the flag with the per-source connect-on-new-instance pattern the mode bar uses: `SyncSignals`, called from every `ResolveReferences`, tracks the connected instance per source and, when a source resolves to a new instance, disconnects the old and connects the new (idempotent, so it never double-subscribes; guarded against spurious `-=` by only disconnecting a still-valid tracked instance). `_ExitTree`'s `DisconnectSignals` now works off the same trackers.

`GridPlacementSmoke.VerifyGridInteractionStatusLateSource` (new) adds a placement collaborator AFTER the panel's `_Ready`, then emits `PlacementRejected` and asserts the readout shows it - proving the late source got wired. Mutation-proven: disabling the placement re-sync makes the readout stay dead and the check fails with "did not wire a placement source resolved after _Ready". Build clean; the full HUD smoke green (verified past the two pre-existing placement reds, reverted).

**Deferred: the object-at-cell index (finding 1).** `GridPlacementComponent` gaining a `Dictionary<Vector2I, GridObjectComponent>` occupant map with `ObjectAt`/`ObjectsIn`, and rewiring the inspector, tool-action and selection-command group scans onto it, is a change to a CORE file plus a second non-blocking map for resource nodes that do not occupy - more surface than the self-contained status fix, and better done attended. It is a real O(objects x cells) -> O(1) win and stays on the list; it builds on DUP-08's `Covers`, already landed.

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
