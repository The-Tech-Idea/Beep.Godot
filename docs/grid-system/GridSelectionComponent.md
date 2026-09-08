# GridSelectionComponent

Standalone hover/click/rectangle-selection primitive for a grid: tracks which cell the mouse is over, maintains a set of selected cells, supports both single-cell selection and drag-rectangle selection, and draws all three (hover outline, selected cells, in-progress drag rectangle) itself. The class doc comment lists its intended uses directly — map editors, tactics games, RTS unit selection, farming plots, builder tools, tile/cell inspectors — and it is written to work fully on its own, with its own `_Process`/`_UnhandledInput`, not only when driven by an orchestrator.

It exists as a self-contained `Node2D` (input handling + state + drawing in one place) rather than split across separate input/state/view components, which keeps single-cell games (an inspector, a simple picker) to one node with no orchestrator required, while still exposing enough surface — `UseMouseInput`, public `BeginDrag`/`FinishDrag`/`SelectCell`/`ClearSelection` — for `GridInteractionModeComponent` (documented separately in this batch) to take over the input side and drive it via direct calls when several interaction modes must share one grid.

## Public API

- `enum SelectionMode { Single, Rectangle }` — `Mode` export; only affects how a left-click-drag on this node's own `_UnhandledInput` is interpreted (`Rectangle` starts a drag, `Single` selects immediately).
- `[Signal] HoverCellChangedEventHandler(int x, int y)`, `CellSelectedEventHandler(int x, int y)`, `SelectionChangedEventHandler(int count)`, `DragSelectionStartedEventHandler(int x, int y)`, `DragSelectionFinishedEventHandler(int count)`.
- `[Export] NodePath GridPath` — the `GridProjectionComponent` supplying `WorldToCell`/`CellCorners`; falls back to scene search.
- `[Export] bool UseMouseInput` — master switch for this node's own `_Process` (hover tracking) and `_UnhandledInput` (click/drag); set to `false` by `GridInteractionModeComponent` when that component manages input instead.
- `[Export] SelectionMode Mode` — default `Rectangle`.
- `[Export] bool AdditiveWithShift` — Shift or Ctrl held during a click adds to rather than replaces the selection.
- `[Export] bool ClearOnSingleSelect` — in `Single` mode, whether a non-additive click clears prior selection first.
- `[Export] bool DrawSelection`, `DrawHover` — gate `_Draw`'s two concerns independently.
- `[Export] Color HoverColor`, `SelectedFillColor`, `SelectedOutlineColor`, `DragFillColor`, `DragOutlineColor` — all independently tunable.
- `Vector2I HoverCell { get; }`, `bool IsDragging { get; }`, `Vector2I DragStartCell { get; }`, `Vector2I DragEndCell { get; }` — read-only observable state.
- `override void _Ready()` — resolves the grid, enables processing/input only outside the editor, refreshes warnings, queues an initial redraw.
- `override void _Process(double delta)` — while `UseMouseInput`: updates hover from the live mouse position every frame, and updates the drag rectangle end cell while dragging.
- `override void _UnhandledInput(InputEvent @event)` — left mouse down starts a drag (`Rectangle` mode) or selects immediately (`Single` mode); mouse up while dragging finishes the drag; Escape clears selection and cancels any drag.
- `override string[] _GetConfigurationWarnings()` — warns when `GridPath` is unset.
- `void UpdateHoverFromWorld(Vector2 worldPosition)` — converts world position to a cell via the grid, updates `HoverCell`, emits `HoverCellChanged` and redraws only if the cell actually changed.
- `void SelectCell(Vector2I cell, bool additive = false)` — clears the set first unless `additive`, adds `cell`, emits `CellSelected` + `SelectionChanged`.
- `void ToggleCell(Vector2I cell)` — removes the cell if present, else adds it; emits `SelectionChanged`.
- `void BeginDrag(Vector2I startCell, bool additive = false)` — clears the set first unless `additive`, sets `IsDragging = true`, emits `DragSelectionStarted`.
- `void UpdateDrag(Vector2I endCell)` — no-op if not dragging; otherwise updates `DragEndCell` and redraws.
- `void FinishDrag(Vector2I endCell)` — adds every cell in the final rectangle to the selection set, clears `IsDragging`, emits `SelectionChanged` + `DragSelectionFinished`.
- `void CancelDrag()` — clears drag state without touching the selection set (no signal emitted).
- `void ClearSelection()` — no-op if already empty; otherwise clears and emits `SelectionChanged` with 0.
- `bool IsSelected(Vector2I cell)`.
- `Godot.Collections.Array<Vector2I> GetSelectedCells()` — a snapshot copy of the current selection set, as a Godot-native array for cross-language (GDScript) consumption.
- `Godot.Collections.Array<Vector2I> GetDragCells()` — the rectangle currently being dragged (empty if not dragging).
- `static Godot.Collections.Array<Vector2I> CellsInRect(Vector2I a, Vector2I b)` — public, Godot-array-returning wrapper around the private `CellsInRectangle` enumerator; usable without an instance.
- `override void _Draw()` — draws every selected cell, the live drag rectangle (if dragging), and the hover outline (if `DrawHover` and a valid hover cell), all via the shared private `DrawCell` helper.

## Dependencies

- `GridProjectionComponent` — resolved via `GridPath` else scene search; used for `WorldToCell` (private `WorldToCell` wrapper) and `CellCorners` (in `DrawCell`). This is the only external type this file depends on.
- Called into by `GridInteractionModeComponent` (this batch): `UpdateHoverFromWorld`, `SelectCell`, `ClearSelection`, `BeginDrag`, `FinishDrag`, `CancelDrag`, `IsDragging`, and the `UseMouseInput` setter are all invoked from that file.
- Read (never mutated) by `GridInteractionCursorComponent` (this batch): only `HoverCell` is read, to decide what cell to draw a cursor over outside Build mode.

## Notes

- **Its own drag-selection input path can be silently disabled by an orchestrator.** When `GridInteractionModeComponent.ManageChildMouseInput` is true (the default), that component sets `UseMouseInput = false` on this node, which short-circuits both `_Process` (so hover stops updating on its own — the orchestrator re-drives `UpdateHoverFromWorld` itself) and `_UnhandledInput` (so this node's own mouse-down/mouse-up drag handling never fires at all). Since `GridInteractionModeComponent`'s own click handler only ever calls the single-cell `SelectCell` and never `BeginDrag`/`FinishDrag` (see that file's notes), the net result under the common orchestrated setup is that this component's default `Mode = Rectangle` produces no actual drag-rectangle selection through mouse input — the machinery is intact and independently testable, just not wired to a live input path in that configuration.
- `CellsInRect` (public static, Godot array) and the private static `CellsInRectangle` (plain `IEnumerable<Vector2I>`) are not a real duplicate — the public one exists purely to give GDScript/cross-language callers a Godot-native return type while `_Draw`, `FinishDrag`, and `GetDragCells` use the lighter-weight enumerator internally.
- `CancelDrag()` intentionally emits no signal (unlike every other mutator here), so a listener relying only on `SelectionChanged`/`DragSelectionFinished` cannot distinguish "drag cancelled" from "nothing happened yet" without also watching `IsDragging`.
