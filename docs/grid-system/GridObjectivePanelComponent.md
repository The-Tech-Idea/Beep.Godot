# GridObjectivePanelComponent

Compact HUD panel (a `Control`, `[Tool][GlobalClass]`) for `GridObjectiveTrackerComponent`: lists active settlement/tutorial goals and their progress, read-only (there is no cancel/command signal here, unlike `GridJobBoardComponent`/`GridProductionPanelComponent`/`GridWorkerStatusPanelComponent` in this same batch).

Like its row-list siblings it binds an authored `Summary`/`Rows` surface or generates one, and maintains its rows *in place* via the same diff pattern: a `Dictionary<string, Label>` keyed by objective id, reused labels repositioned with `MoveChild` into sorted order, and stale rows freed only when the visible objective set changes. It is driven both by the tracker's `ObjectiveActivated`/`ObjectiveProgressChanged`/`ObjectiveCompleted` signals and, as a safety net, by a slow `AutoRefresh` polling interval (`RefreshIntervalSeconds`, default 0.5s) — the file's own comment on the interval export says the timer only exists as a backstop since objective changes normally arrive via signal.

## Public API

- `[Export] NodePath ObjectiveTrackerPath/TitleLabelPath/SummaryLabelPath/RowsContainerPath` — component/control wiring.
- `[Export] bool BuildInEditor = true`, `bool GenerateControlsWhenPathsEmpty = false`.
- `[Export] bool AutoRefresh = true`, `[Export(Range 0.05..5)] float RefreshIntervalSeconds = 0.5f` — runtime polling backstop (see above).
- `[Export] bool HideCompleted = false` — exclude completed objectives from the visible list.
- `[Export(Range 1..24)] int MaxVisibleObjectives = 6`.
- `[Export] string TitleText = "Objectives"`.
- `[Export] Vector2 PanelMinimumSize = new(236, 128)`.
- `public override void _Ready()` — resolves references, connects tracker signals, defers `RebuildPanel()` per `BuildInEditor`, sets `_Process` per `AutoRefresh`/editor-hint.
- `public override void _ExitTree()` — disconnects tracker signals.
- `public override void _Process(double delta)` — accumulates `delta` and calls `RefreshPanel()` once `RefreshIntervalSeconds` has elapsed, while `AutoRefresh` or in-editor.
- `public override string[] _GetConfigurationWarnings()` — warns on missing `ObjectiveTrackerPath` or (without generation enabled) no `Summary`/`Rows` surface.
- `public void RebuildPanel()` — binds or generates the panel, then calls `RefreshPanel()`.
- `public void RefreshPanel()` — rewrites the summary (`"Goals N | Done N"`) and diffs the objective-row list in place.
- `public string SummaryText()` — **calls `RefreshPanel()` as a side effect**, then returns the summary label's text.
- `public string TextForObjective(string objectiveId)` — **calls `RefreshPanel()` as a side effect**, then looks up the row label's text via `GridObjectiveDefinition.Normalize(objectiveId)`.
- `public string TextForObjective(GridObjectiveDefinition objective)` — pure formatting overload: `"{DisplayName}: {progress}/{target} {Done|Active}"`, or `"{DisplayName}: unavailable"` with no tracker.
- `public int VisibleObjectiveRowCount()` — `_rowLabels.Count`.

## Dependencies

- Reads `GridObjectiveTrackerComponent.Objectives`, `.IsActive(id)`, `.IsComplete(id)`, `.GetProgress(id)`, `.GetTarget(id)`, and its `ObjectiveActivated`/`ObjectiveProgressChanged`/`ObjectiveCompleted` signals (outside this batch), iterated via `GridObjectiveDefinition.Enumerate(...)`/`.Normalize(...)`/`.NormalizedId()`/`.HiddenUntilActive`/`.DisplayName`/`.Description` (outside this batch).
- Uses `EntityComponent.FindComponent<GridObjectiveTrackerComponent>(...)` as the scene-fallback lookup, same pattern as every other file in this batch.
- Uses `KitChrome.SetConstantOverrideIfChanged`/`SetColorOverrideIfChanged` (outside this batch) for generated-control styling.
- Not established from this batch alone whether anything calls into `GridObjectivePanelComponent` — none of the other 12 UI files reference it.

## Notes

- `VisibleObjectives()` applies three filter rules in sequence: skip an objective with `HiddenUntilActive` while it isn't active; skip any objective that is neither active nor complete; skip a complete objective when `HideCompleted` is set — an objective that was never activated and isn't `HiddenUntilActive` is therefore excluded too (the "neither active nor complete" rule), which is easy to misread as only the `HiddenUntilActive` rule mattering.
- `SummaryText()` and `TextForObjective(string)` both force a full `RefreshPanel()` (row diff included) purely to answer what look like read-only queries — the same side-effecting-getter shape as `GridJobBoardComponent.TextForJob(string)` and `GridWorkerStatusPanelComponent.TextForWorker(string)` in this batch.
- The row-diffing structure here (seen-set, reuse-or-create, `MoveChild`, stale removal) is duplicated near-verbatim in `GridJobBoardComponent`, `GridProductionPanelComponent`, and `GridWorkerStatusPanelComponent` in this same batch rather than factored into one shared helper.
