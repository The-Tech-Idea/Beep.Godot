# GridWorkerStatusPanelComponent

Compact HUD panel (a `Control`, `[Tool][GlobalClass]`) for worker/truck status: recursively scans a `UnitsRootPath` subtree for `GridWorkerComponent` instances and shows, per worker, whether it's idle, moving, or working, plus the job it's on and (while working) remaining time.

Like `GridProductionPanelComponent` it is purely poll-driven — no `_ExitTree` override, no per-worker signal subscriptions, only a `RefreshIntervalSeconds` accumulator (default 0.25s) in `_Process` — and it maintains its rows in place with the same reuse/`MoveChild`/stale-removal diff pattern used throughout this batch, keyed by `worker.WorkerId`. Its notable piece of logic is `EffectiveState()`: it does not fully trust a worker's own `State` field, instead treating an `Idle`-reporting worker as `Working` whenever the job queue shows a job `Claimed` by that worker's id — reconciling two sources of truth (the worker's own state machine and the job queue's claim record) that could otherwise disagree.

## Public API

- `[Signal] WorkerCancelRequestedEventHandler(string workerId)` — fired only when `CancelWorkerJob` actually finds a job to cancel for that worker (unlike most other command signals in this batch, this one does *not* fire unconditionally).
- `[Export] NodePath UnitsRootPath/JobQueuePath/TitleLabelPath/SummaryLabelPath/RowsContainerPath` — component/control wiring; `UnitsRootPath` is the subtree walked for workers (no scene-wide fallback).
- `[Export] bool BuildInEditor = true`, `bool GenerateControlsWhenPathsEmpty = false`.
- `[Export] bool AutoRefresh = true`, `[Export(Range 0.05..5)] float RefreshIntervalSeconds = 0.25f`.
- `[Export(Range 1..24)] int MaxVisibleWorkers = 8`.
- `[Export] string TitleText = "Workers"`.
- `[Export] Vector2 PanelMinimumSize = new(220, 126)`.
- `public override void _Ready()` — resolves references, defers `RebuildPanel()` per `BuildInEditor`, sets `_Process` per `AutoRefresh`/editor-hint.
- `public override void _Process(double delta)` — accumulates `delta` and calls `RefreshPanel()` once `RefreshIntervalSeconds` has elapsed.
- `public override string[] _GetConfigurationWarnings()` — warns on missing `UnitsRootPath` or (without generation enabled) no `Summary`/`Rows` surface.
- `public void RebuildPanel()` — binds or generates the panel, then calls `RefreshPanel()`.
- `public void RefreshPanel()` — rewrites the summary (`"Total N | Idle N | Active N"`, using `EffectiveState()` to classify idle vs. active) and diffs the worker-row list in place.
- `public string SummaryText()` — calls `RefreshPanel()` as a side effect, then returns the summary label's text.
- `public string TextForWorker(string workerId)` — calls `RefreshPanel()` as a side effect, then looks up the row label by worker id.
- `public string TextForWorker(GridWorkerComponent worker)` — pure formatting overload: `"{id}: {state}"`, or `"{id}: {state} {job}[ ({x},{y})][ {remaining}s]"` when a job (own or reconciled-claimed) is found.
- `public int VisibleWorkerRowCount()` — `_rowLabels.Count`.
- `public bool CancelWorkerJob(string workerId, string reason = "cancelled_from_worker_panel")` — finds the worker, resolves its job (own `CurrentJobId` or a reconciled claim lookup), emits `WorkerCancelRequested`, then either calls `worker.CancelCurrentJob(reason)` or, for the reconciled-claim case, calls `_jobs?.ReleaseJob(jobId, worker.WorkerId)` directly on the queue instead — bypassing the worker component. Returns false if no worker/job is found.

## Dependencies

- Reads `GridWorkerComponent.WorkerId/.State/.CurrentJobId/.WorkRemainingTurns/.CancelCurrentJob(...)`, and its `WorkerState` enum (outside this batch). The remaining-work readout is shown with a `t` suffix — turns, the grid's one unit — not the `s` it used to claim.
- Reads `GridJobQueueComponent.GetJobs()` (same dictionary shape consumed by `GridJobBoardComponent`/`GridMinimapComponent` in this batch), `.GetJobKind(id)`, `.GetJobCell(id)`, `.ReleaseJob(id, workerId)` (outside this batch).
- Uses `GridVariantReader`-style local `DictString(...)` helper to read job dictionary fields with a fallback.
- Uses `EntityComponent.FindComponent<GridJobQueueComponent>(...)` as the scene-fallback lookup for the job queue only (`UnitsRootPath` has no such fallback), same pattern as most other files in this batch.
- Uses `KitChrome.SetConstantOverrideIfChanged`/`SetColorOverrideIfChanged` (outside this batch) for generated-control styling.
- Not established from this batch alone whether anything calls into `GridWorkerStatusPanelComponent` — none of the other 12 UI files reference it, though `GridJobBoardComponent` and `GridMinimapComponent` in this same batch independently read the same `GridJobQueueComponent.GetJobs()` dictionary this panel reconciles against.

## Notes

- `EffectiveState()` overrides a worker's own reported `Idle` state to `Working` whenever the job queue independently shows a `Claimed` job for that worker — meaning this panel can disagree with what `worker.State` itself says, and `CancelWorkerJob()` has a matching second cancellation path (`_jobs?.ReleaseJob(...)`) specifically for the case this reconciliation detects: a worker that thinks it's idle but the queue still has a job claimed under its id.
- `WorkerCancelRequestedEventHandler` fires only on a job actually found — unlike most other "requested" signals in this batch (`GridCalendarHudComponent`, `GridProductionPanelComponent.StartMachine`, `GridWorkerSpawnerPanelComponent`), which emit before checking whether the underlying action will succeed.
- The row-diffing structure here is duplicated near-verbatim in `GridJobBoardComponent`, `GridObjectivePanelComponent`, and `GridProductionPanelComponent` in this same batch rather than factored into one shared helper.
- Like `GridProductionPanelComponent`, this file has no `_ExitTree` override since it subscribes to nothing — purely poll-driven via the refresh accumulator.
