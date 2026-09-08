# GridBuildProgressBarComponent

A floating progress bar over each structure currently under construction. Attach beside `GridBuildSiteComponent`; listens to its already-emitted `BuildSiteCreated`/`BuildSiteCompleted`/`BuildSiteCancelled` signals to track which placed nodes are active — no new plumbing needed on the site — and reads `GridJobQueueComponent.GetJobProgress01` for the value, never the worker, so it stays correct for ANY `IWorker` implementation that reports progress, not just `GridWorkerComponent`.

**This component draws nothing and sizes nothing.** The bar is an authored scene — `ProgressBarScene`, any scene whose root is a Godot `Range` (a plain `ProgressBar`, the kit's `KitMeter`, a `TextureProgressBar`); the shipped default is `templates/scenes/grid_build_progress_bar.tscn`, a `KitMeter` — and all this component does is instantiate it, place it over the site, and set its `Value` within the scene's own `MinValue`..`MaxValue`. How it looks is the scene's job. Refreshes on a throttled timer (`RefreshIntervalSeconds`, default 0.25s) rather than every frame.

The bar is an **overlay**: parented under this component, not under the placed node, and positioned in world space from the placed node's `GlobalPosition` + `BarOffset`. Parenting it under the structure (the way `HealthBarComponent` parents onto its entity) put it inside the subtree the construction-stage visuals treat as "the building's art", so the reveal shader clipped the bar to nothing. An overlay must never live where an effect on the building can reach it; it also does not inherit the structure's scale or rotation, which a readout should not. Its `MouseFilter` is forced to `Ignore` so it never takes the click meant for the world under it.

## Public API
- Exports: `BuildSitePath`, `JobQueuePath` (`NodePath`), `AutoConnect`, `ProgressBarScene` (`PackedScene?`, root must be a `Range`; unset uses the shipped default), `BarOffset` (`Vector2`, where the bar's centre sits relative to the placed node's origin), `StalledModulate` (`Color`, the bar's modulate while its job is queued with no worker holding it — an unstaffed site must read differently from a slow one: Frostpunk's 0/10 ring, ONI's "no worker"; white while a worker holds the job), `RefreshIntervalSeconds`.
- `bool IsStalled(string jobId)` — whether a job's bar currently shows the stalled look.
- `void ConnectSystems()` / `void DisconnectSystems()` — wire/unwire the build-site signals.
- `int ActiveBarCount { get; }` — number of bars currently tracked.
- `float ProgressForJob(string jobId)` — the progress (0..1, normalised through the bar's own min/max) a specific bar is currently showing, or -1 if untracked.
- `Range? BarForJob(string jobId)` — the bar node for a job, or null.

## Dependencies
`GridBuildSiteComponent` (signals) and `GridJobQueueComponent` (`GetJobProgress01`), both resolved via `NodePath` or scene-wide `FindComponent` fallback, matching every other grid component's `ResolveReferences()` convention. `ResourceLoader` for the shipped default scene.

## Notes
- A `ProgressBarScene` whose root is not a `Range` is reported with a warning naming the build and skipped — not a crash mid-signal.
- One of the independent, optional components in the construction-in-progress effect family (alongside `GridWorkerBuildActivityEffectComponent` and `GridBuildStageVisualComponent`) — a project opts into any subset per scene.
- Guarded by `tests/grid_worker_build_effects_probe.gd`: the bar's parent is the component, nothing under the placed node is a `Range`, a plain 0..100 `ProgressBar` scene reads 65 at 65% progress. Falsified by parenting under the placed node again — exactly those checks fail.
