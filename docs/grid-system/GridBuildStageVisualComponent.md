# GridBuildStageVisualComponent

Drives a structure's construction-site visuals from the build site and the job queue. Attach beside `GridBuildSiteComponent`; for every build with `GridBuildDefinition.ConstructionStages` it instantiates those scenes and tells them the facts of the site through `IConstructionVisual` — footprint and cell size, which listed stage each one is, the site's **state** (`pending` materials, `queued` with no worker, `working`, `complete`), the materials on site, progress, and each hit of work (`WorkPulse`). The scenes render all of that; **this component draws nothing.**

A site exists from the moment the build is placed, not from the moment its job starts: the delivery beat every simulated-site game has (Settlers' stacks, Banished's piles, Timberborn's deliveries — see `CONSTRUCTION_VISUALS_RESEARCH.md`) happens while the build is still waiting for `RequiredMaterials`, so the visual is created on `BuildSiteAwaitingMaterials` as well as on `BuildSiteCreated`, and keyed by the placed node.

Stage scenes live under a **site root that is a child of this component**, positioned from the placed node — not under the placed node. `GridBuildSiteComponent` hides (staged builds) or tints (unstaged) the placed node while it builds; stages under it would inherit that, and a translucent site is the "fading in" look this family exists to replace. The site root's z is the footprint's **front edge** in the same absolute domain units use (their own y), so a worker on the approach cell in front of the site draws over the rising wall and one behind draws behind.

On completion the site component shows the finished art at once (a hard swap) and this component keeps the stage scenes for `TeardownSeconds` with `SiteState = "complete"`, so they can take their scaffold down over it, then frees them.

## Public API
- Exports: `BuildSitePath`, `BuildCatalogPath`, `JobQueuePath`, `GridPath` (`NodePath`; the `GridProjectionComponent` whose `EffectiveTileSize` is the `CellSize` told to every stage), `AutoConnect`, `RefreshIntervalSeconds` (0.25), `TeardownSeconds` (1.0), `WorkPulseIntervalSeconds` (0.6 — the cadence of `WorkPulse` while progress is actually advancing).
- `void ConnectSystems()` / `void DisconnectSystems()`.
- `int VisibleStageIndex(string jobId)` — the stage index currently visible for a job, or -1 if untracked (including after teardown).
- `Node2D? StageForJob(string jobId, int index)`, `string SiteStateForJob(string jobId)`.
- `Array<Node2D> StagesForPlaced(Node2D placed)`, `Node2D? SiteRootForPlaced(Node2D placed)` — also for a site still pending materials, which has no job id yet.

## Dependencies
`GridBuildSiteComponent` (all four site signals), `GridBuildCatalogComponent.FindBuild`, `GridJobQueueComponent` (`GetJobState`, `GetJobProgress01`), `GridProjectionComponent` (`EffectiveTileSize`), `GridStorageComponent` on the placed node (what is delivered while pending), `GridResourceAmount.Enumerate` over `RequiredMaterials` — never `Costs`, which is wallet currency.

## Notes
- State is derived from the queue each refresh: no job → `pending`; job `Queued` → `queued`; `Claimed` → `working`; the completion signal → `complete`. `WorkPulse` fires only while `working` and progress advanced since the last refresh — a stalled site is silent.
- Once the job starts the stock reads as fully delivered (the site component built it in at `StartBuildJob`); a scene shows it being used up by `BuildProgress`.
- A `ConstructionStages` list of ONE scene is told `StageIndex 0 / StageCount 1` and stays visible throughout — the shipped `construction_stage.gd` draws the whole build from `BuildProgress` that way. Several scenes are shown by progress band, as static per-stage art would be.
- No `GridProjectionComponent` found is reported once with a warning naming `GridPath`; stages are then told a zero `CellSize`.
- Guarded by `tests/grid_worker_build_effects_probe.gd` over a site's whole life with a pure-GDScript stage scene: hidden finished art, pending before the job, materials from `RequiredMaterials` and the storage, site root under the component with the front-edge z, the approach cell, `queued`/`working`/`complete`, pulses only while progress advances, hard swap then teardown.
