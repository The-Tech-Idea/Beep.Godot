# GridInteractionModeComponent

Orchestrator node: the single place that owns "what does a click on the grid mean right now" and routes it to exactly one of `GridSelectionComponent`, `GridToolActionComponent`, or `GridPlacementComponent` depending on the current `InteractionMode` (Select, Tool, Build, Inspect, Disabled). Its class doc comment states the reason directly — without it, selection, tools, and placement would all try to consume the same click.

It is a plain `Node` (no drawing of its own), and its whole job is arbitration: capture mouse input once, decide the active mode, and call the one child component that mode owns. To make that arbitration actually exclusive rather than merely additive, `ApplyChildInputOwnership` sets `UseMouseInput = false` on the selection, tool-action, and placement children it finds (gated by `ManageChildMouseInput`, default true) — each of those components is independently capable of handling its own mouse input when used standalone, so this component has to actively switch that off to become the sole input path. Because switching off `GridSelectionComponent.UseMouseInput` also disables its own `_Process`-driven hover tracking, `GridInteractionModeComponent._Process` explicitly re-drives `_selection.UpdateHoverFromWorld(...)` itself in Select/Tool/Inspect modes so hover keeps working under new ownership. It emits three signals (`ModeChanged`, `InteractionApplied`, `InteractionRejected`) so UI and gameplay code can react without polling, and every rejection path names a `reason` string rather than just returning false.

## Public API

- `enum InteractionMode { Select, Tool, Build, Inspect, Disabled }`
- `[Signal] ModeChangedEventHandler(int mode)` — emitted from `SetMode` whenever the mode actually changes.
- `[Signal] InteractionAppliedEventHandler(string mode, int x, int y)` — emitted after any successful interaction (select, tool apply, build confirm, drag start/finish, secondary-click cancel/clear).
- `[Signal] InteractionRejectedEventHandler(string mode, int x, int y, string reason)` — emitted from every `Reject(...)` call, always with a specific reason string (`"invalid_cell"`, `"missing_selection"`, `"missing_tool_action"`, `"tool_rejected"`, `"missing_placement"`, `"not_placing"`, `"placement_rejected"`, `"missing_drag"`, `"disabled"`).
- `[Export] NodePath GridPath`, `SelectionPath`, `ToolActionPath`, `PlacementPath` — paths to the four components it coordinates; all fall back to a scene-tree search when empty.
- `[Export] InteractionMode CurrentMode` — the active mode; set through `SetMode`/the mode-shortcut methods to trigger side effects, not by assigning the property directly.
- `[Export] bool UseMouseInput` — master switch for this component's own `_Process`/`_UnhandledInput`.
- `[Export] bool ManageChildMouseInput` — when true, `ApplyChildInputOwnership` forces `UseMouseInput = false` on the resolved selection/tool/placement children so only this component reads mouse input.
- `[Export] bool AdditiveSelectionWithShift` — Shift or Ctrl held during a primary click passes `additive: true` through to selection.
- `[Export] bool ClearSelectionWhenLeavingSelect` — if true, `SetMode` clears the current selection when leaving `Select` for a different mode.
- `override void _Ready()` — resolves references, applies child input ownership, and enables processing/input only outside the editor (`Engine.IsEditorHint()`).
- `override void _Process(double delta)` — while `UseMouseInput` and not in the editor: drives hover updates for Select/Tool/Inspect, and moves the placement preview to the mouse cell while `Build` mode is actively placing.
- `override void _UnhandledInput(InputEvent @event)` — left-click → `HandlePrimaryCell`; right-click → `HandleSecondaryCell`; Escape → `CancelCurrentInteraction`; all gated by `UseMouseInput` and skipped entirely in `Disabled` mode.
- `override string[] _GetConfigurationWarnings()` — warns when `GridPath` is unset.
- `void SetMode(InteractionMode mode)` — no-op if unchanged; optionally clears selection when leaving Select, updates `CurrentMode`, reapplies child input ownership, emits `ModeChanged`.
- `void SelectMode()`, `ToolMode()`, `BuildMode()`, `InspectMode()`, `DisableInteractions()` — `SetMode` shortcuts.
- `bool HandlePrimaryCell(Vector2I cell, bool additive = false)` — dispatches a left-click cell by mode: Select/Inspect → select the cell (Inspect never additive), Tool → apply the current tool action, Build → confirm the pending placement; rejects `"invalid_cell"` for the sentinel cell and `"disabled"` for any other mode.
- `bool HandleSecondaryCell(Vector2I cell)` — right-click: cancels an in-progress build placement, or clears the current selection in Select/Inspect mode; returns false if neither applies.
- `bool CancelCurrentInteraction()` — cancels an in-progress build placement, else cancels an in-progress selection drag; returns false if neither is active.
- `bool BeginDragAtCell(Vector2I cell, bool additive = false)` — starts a rectangle drag on the selection component and emits `InteractionApplied`; rejects `"missing_selection"` if unresolved.
- `bool FinishDragAtCell(Vector2I cell)` — finishes an active drag; rejects `"missing_drag"` if no drag is in progress or selection is unresolved.
- `bool ApplyToolAtCell(Vector2I cell)` — calls `_tools.ApplyToCell(cell, _tools.CurrentAction)`; emits `InteractionApplied` on success, else rejects `"tool_rejected"` (or `"missing_tool_action"` if unresolved).
- `bool ConfirmBuildAtCell(Vector2I cell)` — moves the placement preview to `cell` then calls `_placement.ConfirmPlacement()`; rejects `"missing_placement"`, `"not_placing"`, or `"placement_rejected"` as appropriate.

## Dependencies

Resolves four siblings (NodePath, else `EntityComponent.FindComponent<T>` scene search):

- `GridProjectionComponent` — only for `WorldToCell` (via the private `MouseCell()`), to convert the mouse position to a cell.
- `GridSelectionComponent` — calls `UpdateHoverFromWorld`, `SelectCell`, `ClearSelection`, `BeginDrag`, `FinishDrag`, `CancelDrag`, reads `IsDragging`, and writes `UseMouseInput = false` (established from this batch: this is the component that actually mutates `GridSelectionComponent`'s state — the cursor component only reads it).
- `GridToolActionComponent` — calls `ApplyToCell(cell, CurrentAction)`, reads `CurrentAction`, writes `UseMouseInput = false` (not one of this batch's three files, so its contract is only known from how it's called here).
- `GridPlacementComponent` — reads `State`/`PlacementState.Placing`, calls `MovePreviewToCell`, `ConfirmPlacement`, `CancelPlacement`, writes `UseMouseInput = false` (same caveat as above).

`GridInteractionCursorComponent` (this batch) reads this component's `CurrentMode` but never calls into it — established one-way relationship from this batch.

## Notes

- **Rectangle-drag selection looks unreachable through this component's own input handling.** `_UnhandledInput` only matches `InputEventMouseButton { Pressed: true }` (button-down) and never a button-up event, so `HandlePrimaryCell` in `Select` mode always calls the private single-cell `SelectCell(cell, additive)` — it never calls `BeginDragAtCell`/`FinishDragAtCell`. Those two public methods exist and fully implement a drag flow (with their own `InteractionApplied` emission), but nothing inside this file's own input pipeline calls them; they appear to be meant as an explicit entry point for external code (a custom input script or a UI drag gesture) that wants drag selection routed through this orchestrator instead of calling `GridSelectionComponent` directly. Combined with `ManageChildMouseInput` disabling `GridSelectionComponent`'s own standalone drag handling (see that file's notes), the net effect is that `GridSelectionComponent.Mode = Rectangle` (its default) produces no drag selection at all unless something outside this batch calls `BeginDragAtCell`/`FinishDragAtCell` directly.
- `ApplyChildInputOwnership` calls `ResolveReferences()` itself and is called from both `_Ready()` and every `SetMode()` call, so newly-resolved children always get their `UseMouseInput` turned off even if they weren't resolved yet at `_Ready` time — but it never turns it back on if `ManageChildMouseInput` is toggled off later at runtime; it only ever sets it to `false`, never restores `true`.
- Uses direct, statically-typed component references (`GridSelectionComponent?`, `GridToolActionComponent?`, `GridPlacementComponent?`) rather than reflection-based duck typing — this batch's UI/interaction layer is first-party C# talking to first-party C#, unlike the logistics port interfaces elsewhere in the grid system.
