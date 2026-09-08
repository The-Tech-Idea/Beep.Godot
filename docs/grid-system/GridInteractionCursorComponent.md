# GridInteractionCursorComponent

Presentation-only node: draws a single outlined cell (with optional fill) at whatever cell the current interaction cares about right now — the hovered cell in Select/Tool/Inspect, or the in-progress preview cell while a build placement is active. It is a `Node2D` that reads three other grid components and never writes to any of them; removing it changes nothing about how the grid behaves, only what the player sees.

It exists as its own node, separate from `GridSelectionComponent`'s own hover/selection drawing, because the cursor's colour and even which cell it tracks depend on the *mode* the game is in (`GridInteractionModeComponent.CurrentMode`) and, for Build mode, on placement validity — concerns that `GridSelectionComponent` (a lower-level, mode-agnostic input primitive) doesn't know about. The class doc comment explains the geometry choice: it reads corners from `GridProjectionComponent.CellCorners`, so the exact same node draws correctly for both square top-down cells and isometric diamonds without needing to know which projection is active. A second, explicit design point lives in a code comment on `_Process`: an earlier version called `QueueRedraw()` unconditionally every frame, repainting a cursor that wasn't moving; the current version tracks the last-drawn cell, colour, and draw-or-not flag and only queues a redraw when one of those three actually changed.

## Public API

- `[Export] NodePath GridPath` — path to the `GridProjectionComponent` supplying cell corners; falls back to a scene-tree search when empty.
- `[Export] NodePath InteractionModePath` — path to a `GridInteractionModeComponent`; optional (cursor defaults to `SelectColor` behaviour when absent).
- `[Export] NodePath SelectionPath` — path to a `GridSelectionComponent`, source of the hovered cell outside Build mode.
- `[Export] NodePath PlacementPath` — path to a `GridPlacementComponent`, source of the preview cell during Build mode.
- `[Export] bool DrawCursor` — master on/off switch; `_Draw` and the dirty-check in `_Process` both short-circuit when false.
- `[Export] bool HideWhenDisabled` — when true (default), nothing is drawn while `CurrentMode == Disabled`.
- `[Export] Color SelectColor`, `ToolColor`, `BuildValidColor`, `BuildInvalidColor`, `InspectColor` — outline colour per interaction mode, each independently tunable in the editor.
- `[Export] Color FillColor` — translucent cell fill; skipped entirely when its alpha is 0.
- `[Export(Range 0.5–8)] float OutlineWidth` — polyline stroke width.
- `override void _Ready()` — resolves references, enables `_Process`, and refreshes configuration warnings.
- `override void _Process(double delta)` — recomputes cell/colour/draw-state each frame and calls `QueueRedraw()` only when one of them changed since the last frame.
- `override string[] _GetConfigurationWarnings()` — warns when `GridPath` is unset.
- `override void _Draw()` — draws the filled polygon (if `FillColor.A > 0`) and the outline polyline for the current cell, transforming the grid's cell corners through `ToLocal(_grid.ToGlobal(...))` so the cursor need not be a child of the grid node.
- `Vector2I CurrentCell()` — the cell to draw: the placement component's `CurrentCell` while it is `PlacementState.Placing`, otherwise the selection component's `HoverCell` (or an invalid sentinel if neither is resolved).
- `Color CurrentOutlineColor()` — maps `_interaction.CurrentMode` to the matching exported colour (`Build` further branches on `_placement.CurrentCellValid`); returns `SelectColor` when no mode component is resolved.
- `bool ShouldDrawForMode()` — false only when `HideWhenDisabled` is true and the mode is `Disabled`; true (draw) whenever no mode component is resolved.
- `private void ResolveReferences()` — resolves all four optional dependencies via `NodePath` first, then `EntityComponent.FindComponent<T>` scene search, guarded by `GodotObject.IsInstanceValid`.

## Dependencies

Reads three sibling components, all optional and independently resolved (NodePath, else scene-tree search via `EntityComponent.FindComponent<T>`):

- `GridProjectionComponent` — `CellCorners(cell)` for geometry; required for anything to actually draw (`_Draw` bails if unresolved).
- `GridInteractionModeComponent` — reads `CurrentMode` only (established from this batch: `GridInteractionModeComponent` never reads back from this cursor node — the relationship is one-way).
- `GridSelectionComponent` — reads `HoverCell` only (established from this batch: same one-way relationship; `GridSelectionComponent` has no awareness of this cursor).
- `GridPlacementComponent` — reads `State`, `CurrentCell`, `CurrentCellValid` (not established from this batch alone what a `GridPlacementComponent` is or does beyond these three members, since it isn't one of the three files read here).

Nothing in this batch calls into `GridInteractionCursorComponent` — it is a pure leaf/consumer.

## Notes

- The `Disabled`-mode outline colour (`new Color(1f, 1f, 1f, 0.16f)`) is hardcoded inline in `CurrentOutlineColor`, unlike the other five mode colours which are all `[Export]` fields tunable from the editor — a minor inconsistency, not a bug.
- `_Draw()` recomputes `CurrentCell()` and `CurrentOutlineColor()` from scratch rather than reusing the `_lastCell`/`_lastColour` fields already computed that frame in `_Process` — redundant but cheap, and `_Draw` only runs when `_Process` already decided a redraw was warranted.
- Entirely read-only towards the components it depends on: it never calls a mutating method on `GridSelectionComponent`, `GridInteractionModeComponent`, or `GridPlacementComponent`, so it can be added to or removed from a scene with zero effect on game logic.
