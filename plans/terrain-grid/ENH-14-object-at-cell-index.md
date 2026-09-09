# ENH-14 — One object-at-cell index; HUD status wires late-resolved sources

**Type:** enhancement (lookup cost + a small correctness fix) · **Area:** `GridPlacementComponent`, `GridObjectComponent`, `ui/GridObjectInspectorComponent`, `GridToolActionComponent`, `GridSelectionJobCommandComponent`, `ui/GridInteractionStatusComponent` · **Status:** **PARTIALLY IMPLEMENTED 2026-09-09** (status late-source fix done; the object-at-cell index investigated and declined as specified - see below) · **Effort:** S (1 day) · **Risk:** low

## Outcome (finding 2: status wires late-resolved sources, 2026-09-09)

`GridInteractionStatusComponent` connected its four sources once, behind a single `_connected` flag set in `_Ready`; a source that resolved later (an empty or not-yet-present path filled once the collaborator appears) never got its signals connected, so the readout went dead for it. Replaced the flag with the per-source connect-on-new-instance pattern the mode bar uses: `SyncSignals`, called from every `ResolveReferences`, tracks the connected instance per source and, when a source resolves to a new instance, disconnects the old and connects the new (idempotent, so it never double-subscribes; guarded against spurious `-=` by only disconnecting a still-valid tracked instance). `_ExitTree`'s `DisconnectSignals` now works off the same trackers.

`GridPlacementSmoke.VerifyGridInteractionStatusLateSource` (new) adds a placement collaborator AFTER the panel's `_Ready`, then emits `PlacementRejected` and asserts the readout shows it - proving the late source got wired. Mutation-proven: disabling the placement re-sync makes the readout stay dead and the check fails with "did not wire a placement source resolved after _Ready". Build clean; the full HUD smoke green (verified past the two pre-existing placement reds, reverted).

**Finding 1 (the object-at-cell index) investigated 2026-09-09 and declined as specified.** Read against the code, the plan's model does not hold:

- **Resource nodes are not `GridObjectComponent`s.** `GridResourceNodeComponent : Node2D` joins its OWN group `grid_resource_nodes` (`ResourceNodeGroup`), not `grid_objects`, and never goes through `GridPlacementComponent` occupancy. So the plan's "resource nodes are GridObjectComponents ... register through a second, non-blocking map [on GridPlacementComponent]" cannot work - a placement occupant map keyed by `GridObjectComponent` has nothing to do with resource nodes.
- **The consumer attributions are wrong.** `FindResourceNodeAt` lives in `GridJobEffectComponent`, not `GridToolActionComponent`, and scans `grid_resource_nodes`. `GridSelectionJobCommandComponent` does no object-at-cell lookup at all. The only per-cell object lookup is the inspector's `FindSelectedObject` over `grid_objects`; `GridWorldStateComponent`'s group scan wants EVERY object (for save), which an index does not speed up.
- **The inspector has no placement, by design.** `FindSelectedObject`/`CandidateObjects` discover objects through the `grid_objects` group (with a root-subtree fallback), never through a `GridPlacementComponent`; its smoke test wires no placement at all, and `ReserveFootprintOnReady` defaults to false so objects are not in any occupancy structure. Replacing that scan with `placement.ObjectAt` would give the inspector a placement dependency it does not have and require a per-world object index that every object joins on enter/move - not the placement-occupant map the plan sketches.

So the two real per-cell scans (inspector over `grid_objects`; job-effect over `grid_resource_nodes`) are two indexes over two node types in two groups, each tied to that type's own group and lifecycle - not one occupant map on `GridPlacementComponent`. A genuine optimisation would add a per-group cell->node spatial index that each node type maintains on enter/move/exit, independent of placement occupancy, and point each scan at its own index; that is an architectural decision about where those indexes live (a shared `GridCellIndex<T>`? per-group registries?) and is larger than the "S / low risk" estimate. The narrow occupant-map form - `GridPlacementComponent` tracking the occupying `GridObjectComponent` per cell, hooked into `SetOccupied` - is buildable, but has NO consumer today (the inspector needs all objects, not just occupying ones, and has no placement), so per rule 6 it would be an API nothing calls. Left for the owner to decide the index architecture. DUP-08's `Covers` (landed) is still the right primitive whenever it is built.

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
