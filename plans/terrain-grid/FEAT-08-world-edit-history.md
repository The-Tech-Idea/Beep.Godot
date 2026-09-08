# FEAT-08 — Edit history for world tools (undo/redo)

**Type:** feature (genre standard in builders with terraforming and in every map editor; Cities: Skylines II, Timberborn, RimWorld's plan/undo mods, Godot's own editor) · **Area:** `GridCellDataComponent`, `GridToolActionComponent`, `GridRoadComponent`, `GridPlacementComponent`, `GridTerrainEditComponent` (FEAT-05), `GridWorldStateComponent`, `TerrainLabComponent` (editor lab) · **Status:** proposed 2026-09-08 · **Effort:** M (3–4 days) · **Risk:** low–medium (must respect the same one-owner rule the save system uses)

## Gap

Every edit path writes straight into `GridCellDataComponent` — `Till`, `Water`, `SetTerrainKind`, `SetFlags`, roads, placement occupancy — and emits change signals. There is no record of the *previous* value anywhere, so:

- A misplaced road or a wrong terraform (FEAT-05) can only be reverted by hand.
- The terrain lab (`TerrainLabComponent`, the authoring surface) has no undo, though it edits cells and regenerates.
- Design-time authoring (`TerrainAuthoring.Adopt`, `GenerateInEditor`) produces saved scenes with no way back but scene reload.

The addon's saves are whole-world snapshots (`SAVE_SNAPSHOTS.md`), not suited to per-edit undo.

## Design

1. **`GridEditHistoryComponent`** (`ecs/grid/`): a bounded stack (`MaxEntries`, default 64) of `GridEdit` records — each a list of `(cell, CellRecord before, CellRecord after)` plus the object placements/removals in the same transaction (`GridPlacementComponent` node scene path + cell + definition id). Entries are built by a **transaction scope**: `using (history.Begin("road"))` around a tool/placement operation; every `GridCellDataComponent` mutation inside the scope reports `(cell, before, after)` to the open transaction through one hook on the cell store (`EditObserver`), so no mutator needs to know about history. `Undo()`/`Redo()` replay records through the normal mutators (so signals, revisions, chunk pins and renderers behave exactly as for a real edit — no second write path).
2. **Streaming safety:** a transaction whose cells are in a non-resident chunk pins those chunks for the life of the history entry (DUP-09 `GridChunkPins`); entries older than the pin budget are dropped from the tail (history is a convenience, not a save).
3. **Consumers (rule 6):** `GridToolActionComponent`, `GridRoadComponent`, `GridPlacementComponent` and FEAT-05's terraform API open transactions; the interaction status shows "Undo: road" ; `TerrainLabComponent` binds Ctrl+Z/Ctrl+Y (`ui_undo`/`ui_redo` actions, matching the UI-kit input contract).
4. **Not persisted:** history is session-local; `GridWorldStateComponent` ignores it. Objects: undoing a placement frees the instance; redo re-instantiates from the definition — build progress on an undone site is not restored (documented; undo is for placement mistakes, not a time machine).

## Guards (fail first)

- Smoke: road tool over 5 cells → `Undo()` → cells' `IsRoad` false, `TerrainRevision`/`NavigationRevision` bumped (renderers repaint), `CellsChanged` emitted once with 5 cells; `Redo()` restores. **Mutation:** write `before` records directly into the store instead of via mutators → no `CellsChanged` → assertion fails.
- Smoke: placement + undo frees the node and releases occupancy; redo re-places (`ObjectAt` returns a new instance).
- Probe: history entry on an evicted chunk keeps it pinned; dropping the entry unpins.
- Probe: `MaxEntries = 3`, four transactions → oldest gone, `CanUndo` count 3.

## Dependencies / collisions

DUP-09 (pins), ENH-01 (the observer hook lands beside the change classifier), FEAT-05 (its edits must be undoable). `ecs/grid/` collision — coordinate.

## Out of scope

Editor-plugin integration with Godot's `EditorUndoRedoManager` (a follow-on that can wrap the same transactions), multiplayer.
