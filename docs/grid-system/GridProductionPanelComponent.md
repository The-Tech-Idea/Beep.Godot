# GridProductionPanelComponent

Compact HUD panel (a `Control`, `[Tool][GlobalClass]`) for `GridProductionComponent` buildings: it recursively scans a `ProductionRootPath` subtree for every machine, shows each one's state/recipe/progress, and exposes start/pause/resume/cancel commands so a game gets working production UI without custom glue.

Unlike its row-list siblings that also subscribe to per-item change signals (`GridJobBoardComponent`, `GridObjectivePanelComponent`, `GridWorkerSpawnerPanelComponent`), this panel has no `_ExitTree` override and never subscribes to anything on the individual `GridProductionComponent` machines — it is purely poll-driven, refreshing on a `RefreshIntervalSeconds` accumulator (default 0.25s) in `_Process`. Rows are still maintained in place with the same reuse/`MoveChild`/stale-removal diff pattern used throughout this batch, keyed here by each machine's own `GetPath()` string.

## Public API

- `[Signal] ProductionCommandRequestedEventHandler(string machinePath, string command, string recipeId)` — fired just before the corresponding machine method runs; `StartMachine` emits unconditionally, while `PauseMachine`/`ResumeMachine`/`CancelMachine` each check a state guard first and never emit on a rejected call (see Notes).
- `[Export] NodePath ProductionRootPath/TitleLabelPath/SummaryLabelPath/RowsContainerPath` — component/control wiring; `ProductionRootPath` is the subtree walked for machines (no scene-wide fallback).
- `[Export] bool BuildInEditor = true`, `bool GenerateControlsWhenPathsEmpty = false`.
- `[Export] bool AutoRefresh = true`, `[Export(Range 0.05..5)] float RefreshIntervalSeconds = 0.25f`.
- `[Export(Range 1..24)] int MaxVisibleMachines = 6`.
- `[Export] string TitleText = "Production"`.
- `[Export] Vector2 PanelMinimumSize = new(246, 142)`.
- `public override void _Ready()` — resolves references, defers `RebuildPanel()` per `BuildInEditor`, sets `_Process` per `AutoRefresh`/editor-hint.
- `public override void _Process(double delta)` — accumulates `delta` and calls `RefreshPanel()` once `RefreshIntervalSeconds` has elapsed.
- `public override string[] _GetConfigurationWarnings()` — warns on missing `ProductionRootPath` or (without generation enabled) no `Summary`/`Rows` surface.
- `public void RebuildPanel()` — binds or generates the panel, then calls `RefreshPanel()`.
- `public void RefreshPanel()` — rewrites the summary (`"Machines N | Active N"`) and diffs the machine-row list in place.
- `public string SummaryText()` — calls `RefreshPanel()` as a side effect, then returns the summary label's text.
- `public string TextForMachine(string machinePath)` — calls `RefreshPanel()` as a side effect, then looks up the row label by path.
- `public string TextForMachine(GridProductionComponent machine)` — pure formatting overload: `"{name}: {state}[ {recipe}[ {progress}%]]"`.
- `public int VisibleMachineRowCount()` — `_rowLabels.Count`.
- `public bool StartMachine(string machinePath, string recipeId = "")` — resolves the machine, emits `ProductionCommandRequested`, calls `StartProduction(recipeId ?: machine.ActiveRecipeId)`.
- `public bool PauseMachine(string machinePath)` — guarded on `State == Producing`; emits then calls `PauseProduction()`.
- `public bool ResumeMachine(string machinePath)` — guarded on `State == Paused`; emits then calls `ResumeProduction()`.
- `public bool CancelMachine(string machinePath, bool refundInputs = false)` — guarded on `State != Idle`; emits then calls `CancelProduction(refundInputs)`.

## Dependencies

- Reads `GridProductionComponent.State`, `.Progress01`, `.ActiveRecipeId`, `.CurrentRecipeId`, `.FindRecipe(id)`, `.StartProduction/.PauseProduction/.ResumeProduction/.CancelProduction`, and its `ProductionState` enum (outside this batch), plus `GridProductionRecipe.DisplayName/.RecipeId` (outside this batch).
- Recursively walks `Node.GetChildren()` under `ProductionRootPath` to collect `GridProductionComponent` instances — no `EntityComponent.FindComponent` scene-fallback here, unlike most other files in this batch (`ProductionRootPath` must be set for the panel to find anything).
- Uses `KitChrome.SetConstantOverrideIfChanged`/`SetColorOverrideIfChanged` (outside this batch) for generated-control styling.
- Not established from this batch alone whether anything calls into `GridProductionPanelComponent` — none of the other 12 UI files reference it.

## Notes

- `PauseMachine`/`ResumeMachine`/`CancelMachine` check their state guard *before* emitting `ProductionCommandRequested`, so a rejected command never reaches a listener — but `StartMachine` has no guard at all and always emits, then returns whatever `StartProduction` reports. The four sibling commands are therefore inconsistent with each other in exactly when the signal fires relative to success/failure.
- `FindMachine` matches a `machinePath` argument against three different keys in order — the machine's own `GetPath()` string, its parent node's `Name`, or the machine node's own `Name` — a flexible but easy-to-collide id scheme if two machines share a parent/self name.
- No `_ExitTree` override exists because this panel never subscribes to any machine signal — a structural difference from its otherwise-similar row-list siblings (`GridJobBoardComponent`, `GridObjectivePanelComponent`) that do subscribe and correspondingly do unsubscribe.
- The row-diffing structure here is duplicated near-verbatim in `GridJobBoardComponent`, `GridObjectivePanelComponent`, and `GridWorkerStatusPanelComponent` in this same batch rather than factored into one shared helper.
