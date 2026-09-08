# GridInteractionStatusComponent

Compact single-line HUD status readout (a `Control`, `[Tool][GlobalClass]`) showing the active `GridInteractionModeComponent` mode, the hovered/placement cell, the selected tool or build id, and the most recent interaction feedback — a debugging/UX aid that turns four otherwise-invisible subsystems' state into one label.

It aggregates signals from up to four different sources — `GridInteractionModeComponent` (`ModeChanged`, `InteractionApplied`, `InteractionRejected`), `GridSelectionComponent` (`HoverCellChanged`), `GridToolActionComponent` (`ToolApplied`, `ToolRejected`), and `GridPlacementComponent` (`PlacementStarted`/`Moved`/`Placed`/`Cancelled`/`Rejected`) — into one connect/disconnect pair guarded by a single `_connected` flag, and rebuilds `_lastFeedback` from whichever signal fired most recently. Unlike every other panel in this batch, `AutoRefresh` here does *not* drive a runtime polling loop: the doc comment on `_Process` states that at runtime every value this readout shows already arrives through a signal, so `SetProcess` is gated to `Engine.IsEditorHint() && AutoRefresh` — the editor-preview case only. (Contrast with `GridProductionPanelComponent`/`GridObjectivePanelComponent`/`GridWorkerStatusPanelComponent` in this same batch, where `AutoRefresh` *does* drive a runtime interval-based re-poll — the same export name means a different thing in different files across this batch.)

## Public API

- `[Export] NodePath InteractionModePath/SelectionPath/ToolActionPath/PlacementPath/StatusLabelPath` — component/control wiring; the first is required, the rest are optional per-signal sources.
- `[Export] bool BuildInEditor = true`, `bool GenerateControlsWhenPathsEmpty = false`.
- `[Export] bool AutoRefresh = true` — editor-preview-only process gate (see above).
- `[Export] bool ShowHoverCell = true`, `bool ShowFeedback = true`.
- `[Export] Vector2 PanelMinimumSize = new(380, 34)`.
- `public override void _Ready()` — resolves references, connects signals, defers `RebuildStatus()` per `BuildInEditor`, sets `_Process` per the editor-only rule above.
- `public override void _ExitTree()` — disconnects all four signal sources.
- `public override void _Process(double delta)` — calls `RefreshStatus()` only while `Engine.IsEditorHint()` is true.
- `public override string[] _GetConfigurationWarnings()` — warns if `InteractionModePath` is unset or no status label can be found/generated.
- `public void RebuildStatus()` — binds an existing `Status` label or generates a `PanelContainer`/`Label` pair, then refreshes.
- `public void RefreshStatus()` — sets the label text to `StatusText()`.
- `public string StatusText()` — composes `"{mode} | {detail} | {cell} | {feedback}"`, omitting any segment that is empty.
- `public string LastFeedback { get; set; }` — settable property; the setter both stores the value and immediately calls `RefreshStatus()`, so external code can push feedback text into the readout directly.
- `public bool UsesSceneControls()` — true if `StatusLabelPath` is set or a `Status` label can be found by convention.

## Dependencies

- Reads `GridInteractionModeComponent.CurrentMode` and its `ModeChanged`/`InteractionApplied`/`InteractionRejected` signals (outside this batch).
- Reads `GridSelectionComponent.HoverCell` and its `HoverCellChanged` signal (outside this batch).
- Reads `GridToolActionComponent.CurrentAction` and its `ToolApplied`/`ToolRejected` signals (outside this batch).
- Reads `GridPlacementComponent.State`, `.PlacementId`, `.CurrentCell`, `.CurrentCellValid` and its five placement signals (outside this batch).
- Uses `EntityComponent.FindComponent<T>(...)` as the scene-fallback lookup for all four sources, same as every other file in this batch.
- Not established from this batch alone whether anything calls into `GridInteractionStatusComponent` — none of the other 12 UI files reference it.

## Notes

- `AutoRefresh`'s meaning diverges from its same-named export in `GridObjectivePanelComponent`/`GridProductionPanelComponent`/`GridWorkerStatusPanelComponent` (which use it to gate a runtime polling accumulator): here it only ever gates the editor-hint `_Process` call, so setting `AutoRefresh = false` at runtime has no observable effect at all (the component is already 100%-signal-driven at runtime regardless of the flag's value).
- `DetailText()` shows the current tool action only in `Tool` mode and the placement id only in `Build` mode — in `Select`/`Inspect`/`Disabled` mode the detail segment is always empty, even if a tool or placement happens to still be set from a previous mode.
- `CellText()` prefers the placement's `CurrentCell` over the selection's `HoverCell` whenever `_placement.State == Placing`, so during an active placement the hover cell (if the player's cursor is elsewhere, e.g. over UI) is not shown at all.
