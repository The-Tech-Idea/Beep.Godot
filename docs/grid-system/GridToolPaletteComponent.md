# GridToolPaletteComponent

HUD palette (a `Control`, `[Tool][GlobalClass]`) for `GridToolActionComponent`: creates one toggle button per farming/settler land-tool action (`Clear`/`Hoe`/`Water`/`Plant`/`Harvest`/`QueueJob`/`Road`/`RemoveRoad`) and keeps the pressed button in sync with the component's `CurrentAction`.

It uses the same three-source binding shape as `GridInteractionModeBarComponent` in this batch — an explicit parallel `BoundActionNames`/`BoundButtonPaths` array binding (validated for matching length, parsed with a `TryParseAction` that falls back to a normalized, punctuation-stripped comparison against the enum names), scene-authored buttons found by the `Tool_{action}` naming convention, or a generated `HBoxContainer` row (when `GenerateControlsWhenPathsEmpty` is set) covering whichever actions their `Show*` export enables. The two files are structurally the same template applied to two different enums (`ToolAction` here, `InteractionMode` there).

## Public API

- `[Signal] ToolSelectedEventHandler(string action)` — fired by `SelectTool` on success.
- `[Signal] ToolApplyRequestedEventHandler(string action, int appliedCount)` — fired by `ApplySelectedTool` with however many cells the tool actually applied to.
- `[Export] NodePath ToolActionPath/InteractionModePath` — component wiring.
- `[Export] string[] BoundActionNames` / `[Export] NodePath[] BoundButtonPaths` — explicit parallel binding; must be the same length.
- `[Export] bool BuildInEditor = true`, `bool GenerateControlsWhenPathsEmpty = false`.
- `[Export] bool AutoSwitchInteractionMode = true` — a successful `SelectTool` also calls `GridInteractionModeComponent.ToolMode()`.
- `[Export] bool IncludeApplyButton = false` — add a non-enum "Apply" button (generated-mode only; see Notes).
- `[Export] bool ShowClear/ShowHoe/ShowWater/ShowPlant/ShowHarvest/ShowQueueJob/ShowRoad/ShowRemoveRoad = true` — which actions get a button in the default (non-`BoundActionNames`) case.
- `[Export] Vector2 ButtonMinimumSize = new(86, 34)`.
- `public override void _Ready()` — resolves references, defers `RebuildPalette()` per `BuildInEditor`.
- `public override void _ExitTree()` — disconnects all button handlers.
- `public override string[] _GetConfigurationWarnings()` — warns on missing `ToolActionPath`, mismatched bound array lengths, or (without generation enabled) no authored buttons and no bound names.
- `public void RebuildPalette()` — binds existing buttons if any resolve, otherwise generates a row (plus an Apply button if `IncludeApplyButton`) if allowed.
- `public bool SelectTool(GridToolActionComponent.ToolAction action)` — sets `_tools.CurrentAction`, optionally switches interaction mode, refreshes toggle state, emits `ToolSelected`.
- `public int ApplySelectedTool()` — calls `GridToolActionComponent.ApplyCurrent()` and emits `ToolApplyRequested` with the result; returns 0 without emitting anything if no tool component is resolved.
- `public string SelectedActionName()` — `_tools?.CurrentAction.ToString() ?? ""`.
- `public int VisibleToolButtonCount()` — count of currently tracked tool buttons.
- `public bool UsesSceneButtons()` — true if `BoundActionNames`/`BoundButtonPaths` are set or any conventionally-named tool button exists.
- `public void RefreshSelection()` — syncs every tracked button's pressed state to `_tools.CurrentAction`.

## Dependencies

- Reads/writes `GridToolActionComponent.CurrentAction`, `.ApplyCurrent()`, its `ToolAction` enum (outside this batch).
- Calls `GridInteractionModeComponent.ToolMode()` (outside this batch) when `AutoSwitchInteractionMode` is set.
- Uses `EntityComponent.FindComponent<T>(...)` as the scene-fallback lookup for both components, same pattern as every other file in this batch.
- Uses `KitChrome.SetConstantOverrideIfChanged` (outside this batch) for the generated row's spacing.
- Not established from this batch alone whether anything calls into `GridToolPaletteComponent` — none of the other 12 UI files reference it, though `GridInteractionModeBarComponent` and `GridBuildToolbarComponent` in this same batch each independently call methods on the same `GridInteractionModeComponent` this file drives via `AutoSwitchInteractionMode`.

## Notes

- `IncludeApplyButton` only has any effect when `RebuildPalette()` falls through to the generated-controls branch — `BindExistingButtons()` (the scene-authored / bound-array path) never looks for or wires an "Apply" button at all, so a scene using authored tool buttons gets no working Apply button from this component regardless of `IncludeApplyButton`'s value. An export that is accepted but silently ignored under one of the two supported binding modes.
- Architecturally this file and `GridInteractionModeBarComponent` are the same template (parallel-array binding + convention-name lookup + tolerant `TryParse` + `Dictionary<enum, Button>` + `ToggleMode` + `SetPressedNoSignal` sync) instantiated for two different enums — the clearest duplicate-looking pair in this batch.
- `ApplySelectedTool()` emits `ToolApplyRequested` with whatever `ApplyCurrent()` returns, including 0 when nothing was applied — but its *return value* alone can't distinguish "applied to zero cells" (component resolved, signal emitted) from "no tool component resolved" (no signal, also returns 0); only the signal side tells them apart.
