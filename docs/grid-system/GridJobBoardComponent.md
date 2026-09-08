# GridJobBoardComponent

Compact HUD panel (a `Control`, `[Tool][GlobalClass]`) for `GridJobQueueComponent`: shows queued/claimed/done counts plus a short, sorted list of individual jobs, and lets a game let players cancel a job from the board without writing custom queue UI.

Like its siblings it binds to an authored `Summary`/`Rows` surface (optionally also a `Title`) or generates a `PanelContainer` with those three controls when `GenerateControlsWhenPathsEmpty` is set. Its row list is maintained *in place*: `RefreshBoard()` diffs the current visible-job set against a `Dictionary<string, Label>` keyed by job id, reusing existing `Label`s (updating text/color and `MoveChild`-ing them into sorted order) and only creating or `QueueFree`-ing rows when the job set actually changes — the file's own comment explains this was deliberately chosen over recreating every label per refresh, since `QueueChanged` fires on every claim and completion.

## Public API

- `[Signal] JobCancelRequestedEventHandler(string jobId)` — fired unconditionally at the start of `CancelJob`, before the queue is asked to cancel anything.
- `[Export] NodePath JobQueuePath/TitleLabelPath/SummaryLabelPath/RowsContainerPath` — component/control wiring.
- `[Export] bool BuildInEditor = true`, `bool GenerateControlsWhenPathsEmpty = false`.
- `[Export] bool HideWhenEmpty = false` — hide the whole panel when queued+claimed+completed counts are all zero.
- `[Export] bool ShowCompletedJobs = false` — include `Completed`-state jobs in the visible list.
- `[Export(Range 1..20)] int MaxVisibleJobs = 6`.
- `[Export] string TitleText = "Jobs"`.
- `[Export] Vector2 PanelMinimumSize = new(220, 128)`.
- `public override void _Ready()` — resolves references, defers `RebuildBoard()` per `BuildInEditor`, subscribes to `QueueChanged` at runtime.
- `public override void _ExitTree()` — unsubscribes `QueueChanged`.
- `public override string[] _GetConfigurationWarnings()` — warns on missing `JobQueuePath` or (without generation enabled) no `Summary`/`Rows` surface.
- `public void RebuildBoard()` — binds or generates the panel, then calls `RefreshBoard()`.
- `public void RefreshBoard()` — rewrites the summary line and diffs the job-row list in place (see above); shows `"Job queue missing"` and clears rows if no queue is resolved.
- `public string SummaryText()` — `"Queued N | Active N | Done N"`, or the all-zero form if no queue.
- `public string TextForJob(string jobId)` — **calls `RefreshBoard()` as a side effect**, then looks up the resulting row label's text.
- `public string TextForJob(Godot.Collections.Dictionary job)` — pure formatting overload: `"{kind} ({x},{y}) {state}[ by {worker}] [{id}]"`.
- `public int VisibleJobRowCount()` — `_jobRows.GetChildCount()`.
- `public bool CancelJob(string jobId, string reason = "cancelled_from_job_board")` — emits `JobCancelRequested`, then calls `GridJobQueueComponent.CancelJob(jobId, reason)`, refreshes, and returns whether the queue confirmed cancellation.
- `public bool UsesSceneControls()` — true if any of the three label/rows paths are set or found by convention.

## Dependencies

- Reads `GridJobQueueComponent.QueuedCount/.ClaimedCount/.CompletedCount`, `.GetJobs()` (a `Godot.Collections.Dictionary` per job with `id`/`kind`/`state`/`cell`/`claimed_by`/`priority` fields), `.CancelJob(...)`, and its `QueueChanged` signal (outside this batch).
- Uses `GridVariantReader.Int`/`.Vector2I` (outside this batch) to read job dictionary fields with fallbacks.
- Uses `EntityComponent.FindComponent<GridJobQueueComponent>(...)` as the scene-fallback lookup, same as every other file in this batch.
- Uses `KitChrome.SetConstantOverrideIfChanged`/`SetColorOverrideIfChanged` (outside this batch) for generated-control styling.
- Not established from this batch alone whether anything calls into `GridJobBoardComponent` — none of the other 12 UI files reference it, though `GridMinimapComponent` in this same batch independently reads the same `GridJobQueueComponent.GetJobs()` dictionary (for drawing job dots), so the two panels read overlapping state through separate resolution paths rather than sharing a snapshot.

## Notes

- `TextForJob(string jobId)` re-runs the entire board refresh (including the row diff and every label's text/color update) just to answer a single-job text query — an expensive way to read one row's current text, and it mutates panel state (row list, `MoveChild` order) as a side effect of what looks like a pure getter.
- The row-diffing structure (`seen` `HashSet<string>`, per-row create-or-reuse, `MoveChild(row, shown)`, stale-row removal loop) is duplicated near-verbatim in `GridObjectivePanelComponent`, `GridProductionPanelComponent`, and `GridWorkerStatusPanelComponent` in this same batch — four independent implementations of the same "diff a list into row Labels" logic rather than one shared helper.
- `StateRank`/`ColorForState` compare against `GridJobQueueComponent.GridJobState` enum member names via `nameof(...)` and `StringComparison.OrdinalIgnoreCase` string matching against the job dictionary's `"state"` field, rather than reading a typed enum value — a string-typed bridge between the marshalled dictionary and the strongly-typed enum on the other side.
