# GridBuildSiteComponent

State machine: a `[Tool][GlobalClass]` `Node` that turns a just-placed build into a finished building — blueprint visuals, an optional materials-delivery wait, a construction job on `GridJobQueueComponent`, and cleanup (refund + despawn) if that job is cancelled.

The class doc comment frames this as giving builder games a "blueprint → job → finished building" loop with no per-project glue code. A build with no `RequiredMaterials` goes straight from placement to a queued/dispatched job, unchanged from the simplest possible flow. A build that declares `RequiredMaterials` instead inserts a delivery step first: no job is created — so nothing can be claimed or completed — until a `GridStorageComponent` found on the placed node reports every required resource in stock, filled the same way an `ITransporter` fills any other cargo hold. `RemovePlacedOnJobCancelled` defaults on because the pre-existing behavior of simply forgetting a cancelled site left a paid-for, tinted, footprint-blocking building standing forever with no job that could ever finish it; `RefundOnJobCancelled` only refunds when a `grid_build_cost_charged` metadata flag on the placed node says the wallet was actually charged for it — a flag this component reads but does not itself set, so refund correctness depends on `GridPlacementComponent` having stamped it honestly at placement time. There is no separate builder-agent dispatch any more: `StartBuildJob` only ever calls `_jobs.AddJob(...)`, and any `GridWorkerComponent`/`IWorker` with the build's `JobKind` allowed claims and completes it through the ordinary pull-based job queue, exactly like a gather/clear/till job — `IBuilder`/`GridBuilderUnitComponent`/`GridBuildingManagerComponent` and this component's own `BuildingManagerPath` export were removed together (see `ENHANCEMENT_AND_FIX_PLAN.md` finding #1).

## Public API
- Registration validates required material storage before changing the node's visibility, tint or
  construction metadata. A missing storage component rejects without marking the object unfinished.
- Completion rechecks the recorded work face against current navigation. A blocked face or missing
  explicit navigation rejects `build_approach_blocked` and invokes the existing site cancellation
  policy (removal/refund exports still apply). The job ledger may already record completion; consumers
  should use `BuildSiteCompleted` rather than queue completion to announce a finished building.
  This does not dynamically relocate an already-working crew or refund consumed physical materials.
- `NavigationPath` binds the live navigation map for build access. Registration prefers the front
  midpoint, then tries cardinal edge neighbours, requiring in-bounds and unblocked cells. A missing
  explicit navigation target or sealed perimeter rejects `no_build_approach` before construction
  metadata/visuals change. If no navigation is configured or discovered, the geometric front cell
  remains available for applications without grid navigation.
- `RefreshPendingBuilds()` retries sites awaiting materials/access after application terrain edits.
  Delivered materials are not consumed while all work faces are blocked. A storage change also
  triggers this validation. Existing queued jobs retain their selected approach; worker pathfinding
  still owns reachability from the worker, not this local perimeter check.
- `[Signal] BuildSiteCreatedEventHandler(string buildId, string jobId, Node2D placed, int x, int y)`
- `[Signal] BuildSiteAwaitingMaterialsEventHandler(string buildId, Node2D placed, int x, int y)`
- `[Signal] BuildSiteCompletedEventHandler(string buildId, string jobId, Node2D placed, int x, int y)`
- `[Signal] BuildSiteCancelledEventHandler(string buildId, string jobId, int x, int y)`
- `[Signal] BuildSiteRejectedEventHandler(string buildId, int x, int y, string reason)`
- `[Export] NodePath PlacementPath/BuildCatalogPath/JobQueuePath/ResourceWalletPath` — required collaborators.
- `[Export] bool AutoConnect = true` — whether `_Ready()` wires `ConnectSystems()` itself outside the editor.
- `[Export] bool RemovePlacedOnJobCancelled = true` — despawn the placed node when its job is cancelled.
- `[Export] bool RefundOnJobCancelled = true` — refund `Costs` on cancel, gated by the `grid_build_cost_charged` meta flag.
- `[Export] bool HidePlacedUntilBuilt = false` — hide (vs. tint via `UnderConstructionModulate`) the placed node while unbuilt. **Always in effect for a build with `ConstructionStages`**: those scenes are its under-construction look (drawn beside the placed node by `GridBuildStageVisualComponent`), and every real game hard-swaps to the finished art at the end — a finished building showing through its own site at 72% alpha is the "fading in" look, not a site. The export decides only for builds with no stages.
- `ApproachCellFor(anchorCell, footprint)` returns the preferred geometric front midpoint. Job creation stores the validated perimeter selection, which may differ when that front cell is unusable.
- `[Export] Color UnderConstructionModulate`, `CompletedModulate` — visual state colors.
- `public override void _Ready()` / `_ExitTree()` / `string[] _GetConfigurationWarnings()`.
- `public void ConnectSystems()` / `public void DisconnectSystems()` — subscribe/unsubscribe `GridPlacementComponent.PlacementPlaced` and `GridJobQueueComponent.JobCompleted`/`JobCancelled`.
- `public bool RegisterPlacedBuild(string buildId, Node2D placed, Vector2I cell)` — the main entry point (also reachable via `PlacementPlaced` when auto-connected); applies under-construction visuals/metadata, then routes to the materials wait or straight to a job. Returns `false` with no signal at all if the build's `EffectiveBuildSeconds <= 0` (see Notes).
- `public bool CancelPendingBuild(Node2D placed)` — cancels a site still waiting on material delivery (no job exists yet to cancel).
- `public bool CompleteBuildSite(string jobId)` — marks a site's job as finished (also reachable via `JobCompleted`).
- `public int ActiveBuildSiteCount { get; }` — in-flight-job sites plus materials-pending sites, after pruning freed nodes.
- `public int PendingMaterialsCount { get; }` — sites still waiting on `RequiredMaterials` delivery.
- `public bool CancelBuildSite(string jobId)` — tears down a site whose job was cancelled (also reachable via `JobCancelled`): refunds if charged, despawns if configured, emits `BuildSiteCancelled`.

## Dependencies
- Resolves and subscribes to `GridPlacementComponent` (`PlacementPlaced`) and `GridJobQueueComponent` (`JobCompleted`, `JobCancelled`; also calls `AddJob`); resolves `GridBuildCatalogComponent` (`FindBuild`) and `GridResourceWalletComponent` (`Refund`).
- Looks for a `GridStorageComponent` recursively under the placed node (to gate on `RequiredMaterials` and to `Unload` them once stocked) and a `GridObjectComponent` directly on it (to set `Complete`/`SetMetadataValue`).
- Reads `GridBuildDefinition` fields (`EffectiveBuildSeconds`, `RequiredMaterials`, `JobKind`, `Costs`) fetched from `GridBuildCatalogComponent.FindBuild`. `StartBuildJob` only ever calls `_jobs.AddJob(cell, kind, build.EffectiveBuildSeconds)` — queued like any other job kind, claimed and completed by whichever `GridWorkerComponent`/`IWorker` has that kind allowed.
- Consumed by the construction-in-progress effect family (`GridBuildProgressBarComponent`, `GridBuildStageVisualComponent`), which listen to `BuildSiteCreated`/`BuildSiteCompleted`/`BuildSiteCancelled` to track active sites with no changes needed on this component.
- `UnderConstructionModulate` tints the placed node while building — only for builds with no `ConstructionStages` (a staged build's placed node is hidden instead, and its stage scenes live outside the placed node, so the tint never reaches them). The tint is for builds whose only under-construction visual is their own `Scene`.

## Notes
- `RegisterPlacedBuild` returns `false` with **no signal emitted at all** when `build.EffectiveBuildSeconds <= 0` — a caller cannot tell "this build has zero construction time and was silently skipped" apart from any other unhandled `false` without separately inspecting the build definition; every other rejection path in this file at least emits `BuildSiteRejected` with a reason string.
- `CancelPendingBuild` and `CancelBuildSite` duplicate the same refund-then-despawn sequence (check `grid_build_cost_charged`, `Refund` via wallet, `QueueFree` if configured) almost line for line, once for the pending-materials state and once for the job-in-flight state — the two differ only in what bookkeeping they tear down first (a storage-changed subscription vs. a job-ledger entry), so the duplication tracks two genuinely different site states rather than being an accidental copy.
- Formerly had an optional `BuildingManagerPath` export enabling immediate push-dispatch of new jobs to a registered `IBuilder`; removed along with the whole `IBuilder`/`GridBuilderUnitComponent`/`GridBuildingManagerComponent` family (`ENHANCEMENT_AND_FIX_PLAN.md` finding #1) once it was established the job queue's ordinary claim path already covered construction with no separate dispatch mechanism needed.
