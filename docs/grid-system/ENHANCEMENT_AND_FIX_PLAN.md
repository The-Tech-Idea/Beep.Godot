# Grid System - Enhancement and Fix Plan

This plan covers all 70 files under `addons/beep_game_builder_cs/ecs/grid/`,
each documented individually elsewhere in this directory
(`docs/grid-system/<FileName>.md`). It is the result of a documentation pass
over every one of those 70 files followed by a five-dimension review
(duplication, ownership/settings, consistency, completeness against genre
standard, performance), with every finding independently re-verified against
the actual source - grep results, exact line numbers, and traced call chains
- before being included below.

## Status

The review pass itself changed no source; every one of the 19 confirmed
findings survived an adversarial verify pass (independent re-confirmation
against current source, not just the original review's own claim), so they are
recorded below as confirmed defects, not as tentative or "worth a look"
observations.

**All 19 are now closed.** Each finding carries a `Status:` paragraph saying
what was actually done, including where an option other than the obvious one
was chosen and why. The one finding that needed no change -
`GridInteractionModeComponent` forcing its siblings' `UseMouseInput` - says so
explicitly rather than being silently dropped.

Six of the nineteen are genuine open decisions rather than a single
obviously-correct fix, and are written with both real options named rather
than picked: `GridBuilderUnitComponent`'s duplication of
`GridWorkerComponent`'s dispatch machinery, `GridRoadComponent`/
`GridWorldStateComponent`'s double save ownership, `GridDispatchBoardComponent`'s
missing `ResolveReferences`, `GridCropDefinition`'s `YieldItemId` fallback,
`GridExtractionManagerComponent`'s unfulfilled "HUDs, objectives and game
logic" promise, and `GridJobEffectComponent`'s missing Repair effect - the
same way the terrain engine plan
(`docs/terrain-engine/ENHANCEMENT_AND_FIX_PLAN.md`) left `ReliefAt` and
`NodeScene`/`.Icon` as open decisions rather than picking for the reader.

## Confirmed findings

### GridBuilderUnitComponent duplicates GridWorkerComponent's push-dispatch state machine, and drops the safety check the original carries

**Files:** `GridBuilderUnitComponent.cs`, `IBuilder.cs`,
`GridBuildingManagerComponent.cs`, `GridWorkerComponent.cs`,
`GridBuildSiteComponent.cs`, `ui/GridWorkerStatusPanelComponent.cs`,
`GridWorkerSpawnerComponent.cs`

`IBuilder.cs`'s own doc comment argues a separate builder abstraction is
needed because a build must be "dispatched by registration, not claimed from
a queue," the way `GridExtractorComponent` pushes hauls straight to
`ITransporter.RequestHaul` with no job ever entering
`GridJobQueueComponent`. That premise doesn't hold for building:
`GridBuildSiteComponent.StartBuildJob` always adds the job to the queue
first - `string jobId = _jobs!.AddJob(cell, kind, build.EffectiveBuildSeconds);
... _buildingManager?.RequestBuild(cell, build.BuildId, jobId,
build.EffectiveBuildSeconds);` - so the job exists in `GridJobQueueComponent`
regardless of whether `IBuilder`/`GridBuildingManagerComponent` exist at all.

`GridWorkerComponent` already has exactly the push-dispatch entry point the
doc comment claims is missing: `AssignJob(string jobId)` claims (or accepts
an already-self-claimed) job and calls `BeginClaimedJob`, starting the same
MovingToJob/Working state machine - `if (_queue.GetJobState(jobId) ==
GridJobQueueComponent.GridJobState.Queued) { if (!_queue.ClaimJob(jobId,
WorkerId)) return false; } ... return BeginClaimedJob(jobId);`. It has zero
production callers anywhere in the addon (only a test calls it), so it was
never wired up as the push path a building manager could have called on
registered idle workers. Instead, `GridBuilderUnitComponent` (235 lines)
independently re-implements the claim/travel/work/complete loop, and drops a
guard the original carries: `GridWorkerComponent.StartWorkOrFail`
re-validates `_queue.HasJob(CurrentJobId)` and `GetJobState(...) == Claimed
&& GetJobClaimedBy(...) == WorkerId` the moment travel finishes,
specifically because - per its own comment - "the job may have been
cancelled or reassigned while this worker walked to it... working through it
anyway wasted the whole duration." `GridBuilderUnitComponent.StartWorking()`
has no equivalent check; it is only `State = BuilderState.Working;`. So if a
build site is cancelled (`GridBuildSiteComponent.CancelBuildSite`/
`OnJobCancelled` removes the job, and `GridJobBoardComponent.CancelJob` is a
real, wired production path that can cancel a claimed job) while a
`GridBuilderUnitComponent` is mid-travel, it still burns the full
remaining work (`WorkRemainingTurns` today) before `FinishBuild` discovers
`_jobs.CompleteJob(...)` returns false and emits `BuildFailed` - the exact
wasted-duration bug `GridWorkerComponent`'s check exists to prevent. Builders
are also invisible to existing worker tooling: `GridWorkerStatusPanelComponent.
CollectWorkers` only recurses on `is GridWorkerComponent`, and
`GridWorkerSpawnerComponent` only ever spawns `GridWorkerComponent`.

**Fix - two options, not picked:**

- **(a) Delete the parallel system.** Remove
  `IBuilder`/`GridBuilderUnitComponent`/`GridBuildingManagerComponent`, and
  have `GridBuildSiteComponent`'s "push the moment materials are ready" need
  met by calling `AssignJob(jobId)` on registered `GridWorkerComponent`
  instances - add a lightweight registry, or a job-kind filter so idle
  workers auto-claim "build"-kind jobs.
- **(b) Keep IBuilder for genuinely different agent semantics** (cranes with
  no path follower, off-map labour pools), but at minimum port
  `GridWorkerComponent.StartWorkOrFail`'s job-still-claimed revalidation into
  `GridBuilderUnitComponent.StartWorking` so the duplicate doesn't regress a
  fix the original already has.

**Status: Fixed - option (a), deletion.** `IBuilder.cs`, `GridBuilderUnitComponent.cs`,
and `GridBuildingManagerComponent.cs` are removed outright, along with
`GridBuildSiteComponent.BuildingManagerPath` and its `_buildingManager?.RequestBuild(...)`
push call. `StartBuildJob` now only ever does one thing - `_jobs.AddJob(cell, kind,
build.EffectiveBuildSeconds)` - and a build job is claimed and completed exactly
like a gather/clear/till job: any `GridWorkerComponent` with "build" (or the
definition's own `JobKind`) allowed polls and claims it through the ordinary
`ClaimNextJob`/ledger path. No registry was added - `AssignJob` was considered
but the plain poll already covers the "push the moment materials are ready"
case closely enough (`ClaimIntervalSeconds` default 0.25s, tunable down) without
a second dispatch mechanism to keep in sync with the job queue. Construction is
now visible to every existing worker tool (`GridWorkerStatusPanelComponent`,
`GridWorkerSpawnerComponent`) for free, since it is just another job kind, not
a parallel agent type. `tests/grid_terrain_building_probe.gd` was rewritten to
prove the new flow with a real `GridWorkerComponent` - wired with a real
`GridProjectionComponent`/`GridNavigationComponent`/`GridPathFollowerComponent` -
claiming and completing three build jobs in sequence (materials-gated, ungated,
and the real-hauler-delivery case), with zero `WorkerFailedJob` signals along
the way.

### Road data has two independent, both-default-on save/load owners

**Files:** `GridRoadComponent.cs`, `GridWorldStateComponent.cs`

`GridRoadComponent` implements `ISaveable` itself and joins
`SaveableHelper.Group` under its own key: `[Export] public bool
ParticipatesInSave { get; set; } = true;` / `[Export] public string SaveKey
{ get; set; } = "grid_roads.state";`, with `_Ready` doing `if
(!Engine.IsEditorHint() && ParticipatesInSave)
AddToGroup(SaveableHelper.Group);` and `Save`/`Load` writing/reading
`state.GameData[SaveKey]` (roads-only). `GridWorldStateComponent` separately
captures/restores the *same* `GridRoadComponent` instance's roads into its
own aggregate key: `[Export] public bool CaptureRoads { get; set; } =
true;`, and in `CaptureState`/`RestoreState`: `if (CaptureRoads && _roads !=
null) state["roads"] = _roads.GetRoads();` / `_roads.LoadRoads(ReadArray(state,
"roads"));`, under `SaveKey = "grid_world.state"`.

This breaks the pattern every other subsystem `GridWorldStateComponent`
aggregates follows: `GridCellDataComponent`, `GridPlacementComponent`,
`GridNavigationComponent`, `GridSelectionComponent`, and
`GridJobQueueComponent` implement no `ISaveable` of their own -
`GridWorldStateComponent` is their sole owner. `GridRoadComponent` is the
only one of the six that also persists itself independently. Both
`LoadRoads` calls default `clearExisting: true`, so with both components
present at their shipped defaults - the configuration the addon's own
reference scene (`templates/scenes/grid_world_2d_iso.tscn`) actually ships,
where the "Roads" node leaves `ParticipatesInSave`/`SaveKey` untouched and
the "State" node's `RoadPath` points straight at it with `CaptureRoads` also
untouched - a save writes road data to two keys in the same file, and a load
calls `LoadRoads` on the same instance twice, once from each owner, in an
order `SaveableHelper`'s group iteration does not contractually guarantee,
each clearing and reloading over the other's result.

**Fix - two options, not picked:**

- Strip `GridRoadComponent`'s own `ISaveable`/`ParticipatesInSave`/
  `SaveKey`/group-join, matching `GridCellDataComponent`/
  `GridPlacementComponent`/`GridNavigationComponent`/`GridSelectionComponent`/
  `GridJobQueueComponent`, which rely solely on `GridWorldStateComponent`.
- Or remove roads from `GridWorldStateComponent`'s `CaptureRoads`/`RoadPath`
  aggregation and let `GridRoadComponent` keep persisting itself
  independently.

Not both.

**Status: Fixed - option 1.** `GridRoadComponent`'s own `ISaveable`,
`ParticipatesInSave`/`SaveKey` exports, and `SaveableHelper.Group` join/leave
are removed, matching its five siblings. `CaptureState`/`RestoreState` are
kept as plain convenience methods (`GridWorldStateComponent` still calls them
directly, and `tests/GridPlacementSmoke.cs` still exercises them), with an
updated doc comment explaining they are no longer part of a save pipeline on
their own. No `.tscn` explicitly set `ParticipatesInSave`/`SaveKey` on a road
node, so no scene edit was needed. Verified via `tests/runtime_smoke.ps1`
("roads" section) after the change.

### GridJobEffectComponent's Clear/Till/Water bypass the terrain-workability gating GridToolActionComponent enforces for the same actions

**Files:** `GridJobEffectComponent.cs`, `GridToolActionComponent.cs`

`GridToolActionComponent.ApplyClear` gates on `CanWorkTerrain(cell)` before
calling `_cells.ClearLand(cell)`; `ApplyHoe` gates on the `Blocked` flag plus
`CanWorkTerrain`; `ApplyWater` gates on the `Tilled` flag.
`GridJobEffectComponent`'s equivalents skip all of it: `ApplyClear` calls
`_cells!.ClearLand(cell)` directly (no `CanWorkTerrain` call), `ApplyTill`
calls `_cells!.Till(cell)` with zero gating, and `ApplyWater` calls
`_cells!.Water(cell)` with zero gating - not even the `Tilled` check
`GridToolActionComponent.ApplyWater` performs. Only `ApplyHarvest` has a
delegate-or-fallback split (`UseToolActionForHarvest`) routing through
`GridToolActionComponent.ApplyToCell(cell, ToolAction.Harvest)` when
configured; there is no `UseToolActionForClear`/`Till`/`Water` equivalent.
Because `GridCellDataComponent.ClearLand`/`Till`/`Water` are themselves
unconditional flag mutations with no terrain checks of their own, and
`GridJobQueueComponent.AddJob` is also ungated, `GridJobEffectComponent` is
the last real gating opportunity in the job-completion path for these three
actions - and it doesn't take it. Completing a `clear_land`/`till`/`water`
job can therefore mutate a cell the click-driven tool path would have
rejected outright as unworkable terrain.

**Fix:** Route `ApplyClear`/`ApplyTill`/`ApplyWater` through
`GridToolActionComponent.ApplyToCell(cell, ToolAction.Clear/Hoe/Water)` the
same way `ApplyHarvest` already can, so a job-completed effect is gated
identically to the click-driven tool path, with one terrain-workability rule
instead of two.

**Status: Fixed.** `UseToolActionForClear`/`UseToolActionForTill`/
`UseToolActionForWater` exports added (default true), mirroring
`UseToolActionForHarvest` exactly; `ApplyClear`/`ApplyTill`/`ApplyWater` route
through `GridToolActionComponent.ApplyToCell` when wired, falling back to the
old direct `GridCellDataComponent` mutation, ungated, only when no
`GridToolActionComponent` is present. Verified via `tests/runtime_smoke.ps1`
("job-effects" section).

### GridToolActionComponent and GridSelectionJobCommandComponent independently reimplement "is this cell queueable," and only one checks the CellData Blocked flag

**Files:** `GridToolActionComponent.cs`, `GridSelectionJobCommandComponent.cs`

`GridToolActionComponent.ApplyQueueJob` gates through
`WorkJobBlockReason(cell)`, which only checks nav bounds/blocked (gated by
`RejectNavigationBlockedCellsForJobs`) and `CanWorkTerrain` (terrain-kind
allow/block lists) - it never checks
`GridCellDataComponent.CellFlags.Blocked`.
`GridSelectionJobCommandComponent.QueueBlockReason` explicitly does: `if
(TreatCellDataBlockedAsUnqueueable && _cellData.HasFlag(cell,
GridCellDataComponent.CellFlags.Blocked)) return "blocked_cell";`, before the
same terrain-kind checks. Both components separately duplicate the same
NodePath-resolution boilerplate for
`GridSelectionComponent`/`GridJobQueueComponent`/`GridCellDataComponent`/
`GridNavigationComponent`, and both end their path with the same `(cell,
kind, workTurns, priority)` call into `AddJob`. A project wiring both a
farming toolbar (`GridToolActionComponent`'s QueueJob action) and a
settler-style selection command (`GridSelectionJobCommandComponent`) gets two
independently-tuned answers to "is this cell queueable," with no comment
anywhere documenting the asymmetry as intentional.

**Fix:** Have `GridToolActionComponent.ApplyQueueJob` delegate to a shared
helper - or to `GridSelectionJobCommandComponent.QueueCells`/
`CanQueueJobAt` when one is wired - instead of re-deriving its own
`WorkJobBlockReason`, so the queueability rule, including the CellData
Blocked check, has one implementation.

**Status: Fixed - the shared helper, not the delegate-when-wired option.**
Delegating to `GridSelectionJobCommandComponent` only when one happens to be in
the scene would make correct gating conditional on a second component being
present - the weaker rule would still be reachable, which is the drift the
finding is about. Instead a new `GridCellRules` (a readonly struct built from
whichever component is asking, holding its collaborators and its own export
values) owns the single bounds/nav-blocked/cell-blocked/terrain decision, and
both paths ask it unconditionally. It returns a `GridJobBlock` reason rather
than a string, because the two components report the same condition in
different words (`unworkable_terrain` vs `unqueueable_terrain`) and that
vocabulary is each one's public signal API - one rule, two vocabularies. The
tool path gained `RejectCellDataBlockedCellsForJobs`, defaulting false to match
its sibling's `TreatCellDataBlockedAsUnqueueable`, so the two are configured
alike out of the box; the selection path gained `DataLayersPath`, so a settler
command over a terrain-engine map is judged by that map exactly as the tool
path already was. `GridPlacementComponent`'s own placement rule is genuinely
different (a definition's `AllowedTerrainKinds` overrides the scene's list
entirely) and stays its own, but its duplicate `TerrainKindAt` now calls
`GridCellRules.TerrainKindAt`, so terrain is read one way everywhere.
Guarded by `GridPlacementSmoke.VerifyQueueabilityIsOneRule`, which asserts both
paths agree on a Blocked cell, on blocked terrain, on an open cell, and when
the flag is turned off - confirmed to fail (with "The tool and selection
queueing paths disagreed about a CellData-Blocked cell") against the old
asymmetry before being kept.

### Every HUD panel in ui/ hand-rolls the same bind-or-generate bootstrap, SetEditedOwner, SafeName, and row-diff loop

**Files:** `ui/GridResourceBarComponent.cs`, `ui/GridJobBoardComponent.cs`,
`ui/GridWorkerStatusPanelComponent.cs`, `ui/GridBuildToolbarComponent.cs`

`GridResourceBarComponent`, `GridJobBoardComponent`, and
`GridWorkerStatusPanelComponent` each independently define a byte-for-byte
identical `SetEditedOwner(Node node)` (`if (!Engine.IsEditorHint()) return;
node.Owner = GetTree()?.EditedSceneRoot;`) and a near-identical
`SafeName(string value)` that replaces `Path.GetInvalidFileNameChars()` and
spaces with `_`; `GridBuildToolbarComponent` duplicates the same pair too.
All four also carry a near-identical `FindXLabel`/`FindXContainer`
three-tier lookup (NodePath property, then `FindChild(name, recursive:
true, owned: false)`, then `GetParent()?.FindChild(...)`), and the "bind
scene-authored controls, else optionally generate a default layout"
bootstrap (`RebuildBar`/`RebuildBoard`/`RebuildPanel`/`RebuildToolbar`) is
present in the same shape across all four. `GridJobBoardComponent.RefreshBoard`
and `GridWorkerStatusPanelComponent.RefreshPanel` additionally share a
near-identical seen-set row-diff loop (`HashSet<string> seen`, reuse-or-create
a Label per id, `MoveChild(row, shown)`, then remove everything not in
`seen`); `GridResourceBarComponent`'s row-refresh loop uses a similar
seen-set idea without the `MoveChild` step. This is purely structural
boilerplate, unrelated to each panel's own domain (resources vs. jobs vs.
workers vs. builds).

**Fix:** Factor the bind-or-generate bootstrap, `SetEditedOwner`,
`SafeName`, and the seen-set row-diff loop into one shared base `Control` or
static helper class under `ui/`, and have the panel components call it
instead of each carrying its own copy.

**Status: Fixed - a two-level base class, and it covers six panels, not the
four this finding named.** `GridPanelComponent : Control` (abstract, the same
`[Tool] [GlobalClass]` shape `EntityComponent` already uses for an abstract
base with exports) owns `BuildInEditor`, `GenerateControlsWhenPathsEmpty`,
`SetEditedOwner`, `SafeName`, and a `FindControl<T>(path, params names)` that
searches by tier - exported path, then every candidate name as a child, then
every candidate name under the parent - which is the order the resource bar
had always used for its authored row and then its generated one.
`GridListPanelComponent : GridPanelComponent` adds the Title/Summary/Rows
paths, the generated PanelContainer/Content/Title/Summary/Rows layout, and
`UpdateRows(IEnumerable<GridPanelRow>, maxVisible)` - the seen-set row diff,
`MoveChild` included. A subclass supplies only `GeneratedRootName`,
`RowNamePrefix`, and what a row says.

Writing it revealed the finding undercounted: `GridProductionPanelComponent`
and `GridObjectivePanelComponent` carry the identical copy too (the contract
scan pinned `FindTitleLabel`/`FindSummaryLabel`/`FindRowsContainer` in all
four list panels). Converting four and leaving two would have left two
patterns, so all six moved: job board, worker status, production, objectives
on `GridListPanelComponent`; resource bar and build toolbar on
`GridPanelComponent`. The base's `SafeName` is the union of the copies it
replaced - it strips the separators and the colon explicitly, because
`Path.GetInvalidFileNameChars()` omits the backslash on Unix and a production
row's key is a whole node path.

`tests/addon_contract_scan.ps1` needed updating with the change, and that is
worth stating plainly: it pinned those declarations *per panel file*, so as
written it required the duplication. The pins moved to the new base files
(where the fact now lives) and each panel gained a stricter pin that it
actually derives from the base - `class GridJobBoardComponent :
GridListPanelComponent` and so on - which the old text-per-file checks could
not express. Verified by the contract scan plus `tests/runtime_smoke.ps1`'s
job-board, worker-status, production, objective, resource-bar and
build-toolbar sections, and by the full `tests/run_addon_checks.ps1` gate.

### GridPlacementComponent.BeginPlacement(GridBuildDefinition) permanently overwrites two inspector-owned defaults with no restore path

**Files:** `GridPlacementComponent.cs`, `GridBuildDefinition.cs`

`MarkPlacedCellsOccupied` and `SetZIndexFromY` are `[Export]`ed,
inspector-owned policy defaults (both `true` by default).
`BeginPlacement(GridBuildDefinition)` writes directly into them:
`MarkPlacedCellsOccupied = definition.OccupiesCells;` and `SetZIndexFromY =
definition.SetZIndexFromY;`. Contrast the sibling fields
`_activeBlocksNavigation`/`_activeAllowedTerrain`, which the *same method*
writes into private shadow fields instead, leaving the component's own
exported properties untouched - and which `BeginPlacement(string id)`'s `if
(!fromDefinition)` block correctly resets (`_activeBlocksNavigation = true;
_activeAllowedTerrain = new(...)`) for a subsequent non-definition
placement. `MarkPlacedCellsOccupied` and `SetZIndexFromY` are absent from
that reset block, and are likewise never reset by `CancelPlacement()` or
`FinishPlacement()`. Both values are read to make real decisions downstream:
`if (SetZIndexFromY) placed.ZIndex = ...`, `if (MarkPlacedCellsOccupied)
SetFootprintOccupied(CurrentCell, true);`, and
`gridObject.ReservePlacementFootprint = MarkPlacedCellsOccupied;`. So
selecting one build from a catalog whose `GridBuildDefinition` sets
`OccupiesCells = false` (or `SetZIndexFromY = false`) permanently overwrites
the component's own configured default, with no code path anywhere in the
file that ever restores it - a later, non-definition placement silently
inherits the last selected build's policy.

**Fix:** Mirror the existing `_activeBlocksNavigation`/`_activeAllowedTerrain`
pattern - introduce `_activeMarkPlacedCellsOccupied`/`_activeSetZIndexFromY`
shadow fields, or add `MarkPlacedCellsOccupied`/`SetZIndexFromY` to the `if
(!fromDefinition)` reset block in `BeginPlacement(string id)`, restoring
them from the component's own exported defaults - so a non-definition
placement after a build-catalog placement isn't silently governed by the
last selected build's policy.

**Status: Fixed - both halves, not either/or.** `_activeMarkPlacedCellsOccupied`
and `_activeSetZIndexFromY` shadow the two exports exactly as
`_activeBlocksNavigation` shadows its own; `BeginPlacement(GridBuildDefinition)`
writes the shadows, the four read sites (placed z, footprint occupancy, preview
z, `GridObjectComponent.ReservePlacementFootprint`) read the shadows, and the
`if (!fromDefinition)` block restores both from the component's own exports -
the shadow pattern alone would have left the fields holding the last
definition's policy forever, since nothing else resets them. The exports keep
their `[Export]`s and their scene overrides untouched (`grid_world_2d_iso.tscn`
sets `MarkPlacedCellsOccupied = true`, which the contract scan pins).
Guarded by `GridPlacementSmoke.VerifyPlacementDefinitionPolicyIsPerBuild`:
place a definition with `OccupiesCells`/`SetZIndexFromY` false, assert the
exports are untouched, then place a non-definition build and assert it occupies
its cell and sorts by y again. Confirmed to fail ("A non-definition placement
inherited the last selected build's occupancy/z-sorting policy...") with the
reset removed, before being kept.

### GridResourceScatterComponent's MarkGeneratedCellsOccupied is silently overwritten one frame later by GridResourceNodeComponent's own catalog application

**Files:** `GridResourceScatterComponent.cs`, `GridResourceNodeComponent.cs`

`GridResourceScatterComponent.CreateResourceNode` carries a comment
explaining that writing `Amount`/`AmountPerGather`/`GatherJobKind`/
`GatherSeconds` onto the newly-created node unconditionally used to be
"values accepted, stored, and silently overwritten one frame later" - and
now writes all four only when the catalog doesn't define the resource: `if
(Catalog?.Find(resourceId) is null) { resource.Amount = ...;
resource.AmountPerGather = ...; resource.GatherJobKind = ...;
resource.GatherSeconds = ...; }`. Immediately after that guarded block, the
same method sets a fifth property unconditionally, outside the guard:
`resource.MarkCellOccupiedOnReady = MarkGeneratedCellsOccupied;`.
`GridResourceNodeComponent`'s own `_Ready()` calls `ApplyCatalogDefinition()`
before the node's own occupancy logic runs, and that method overwrites the
identical five properties whenever `Catalog.Find(...)` succeeds - including
`MarkCellOccupiedOnReady = definition.OccupiesCell;`. So for any
`resourceId` the shared catalog defines, `GridResourceScatterComponent`'s
`MarkGeneratedCellsOccupied` export has no effect on the node's actual
occupancy behaviour - the exact accepted-then-silently-overwritten bug the
adjacent comment says was already fixed for the other four properties, left
unapplied to this fifth one.

**Fix:** Move `resource.MarkCellOccupiedOnReady = MarkGeneratedCellsOccupied;`
inside the `if (Catalog?.Find(resourceId) is null)` guard alongside
`Amount`/`AmountPerGather`/`GatherJobKind`/`GatherSeconds`, consistent with
the fix already applied to the other four catalog-owned properties.

**Status: Fixed.** The line moved inside the guard exactly as scoped.
Verified via `tests/runtime_smoke.ps1` ("resource-scatter" section).

### GridInteractionModeComponent silently forces three sibling components' UseMouseInput to false

**Files:** `GridInteractionModeComponent.cs`, `GridSelectionComponent.cs`,
`GridToolActionComponent.cs`, `GridPlacementComponent.cs`

`ApplyChildInputOwnership()` - called from `_Ready()` and every `SetMode()`
call, whenever `ManageChildMouseInput` is true (the default) -
unconditionally sets `_selection.UseMouseInput = false; _tools.UseMouseInput
= false; _placement.UseMouseInput = false;`, discarding whatever each
sibling's own `[Export]` was configured to in the inspector
(`GridSelectionComponent.UseMouseInput` defaults `true`,
`GridToolActionComponent.UseMouseInput` defaults `false`,
`GridPlacementComponent.UseMouseInput` defaults `true` - each independently
authorable). This is documented, toggleable behaviour
(`ManageChildMouseInput = false` opts out), so it reads as intentional
design rather than a defect - it is called out here for visibility, not as
something to fix by default. It also never restores `true` when
`ManageChildMouseInput` is later toggled off, and gives an author no signal
when it overrides a child whose `UseMouseInput` was explicitly set `true` in
the scene.

**Fix:** No change required if this is the intended behaviour. If it should
be surfaced more clearly, consider logging/warning once when
`ManageChildMouseInput` overrides a child whose `UseMouseInput` was
explicitly set `true` in the scene, so an author who intentionally enabled a
child's own input doesn't lose it silently.

### GridExtractionManagerComponent.Register skips the duck-type shape validation its two sibling managers both perform

**Files:** `GridExtractionManagerComponent.cs`,
`GridTransportManagerComponent.cs`, `GridBuildingManagerComponent.cs`

All three managers share the identical "EXPANDABLE BY REGISTRATION, not by
type" doc paragraph and were introduced as one family -
`GridBuildingManagerComponent`'s own doc literally says "the same
registration shape as GridTransportManagerComponent, deliberately - one
family, one philosophy, across extraction, transport and building."
`GridTransportManagerComponent.Register` and
`GridBuildingManagerComponent.Register` both validate the duck-typed
contract before accepting a registrant, with a matching warning: `if
(!transporter.HasMethod("RequestHaul") || !transporter.HasMethod("CanAccept")
|| !transporter.HasMethod("Load") || !transporter.HasMethod("Unload")) {
GD.PushWarning($"[{Name}] {transporter.Name} does not answer the
transporter contract..."); return; }`, and the equivalent two-method check
for `IBuilder`. `GridExtractionManagerComponent.Register` has no such check
at all: `if (extractor == null || !GodotObject.IsInstanceValid(extractor)
|| _extractors.Contains(extractor)) return; _extractors.Add(extractor);
EmitSignal(...);`. A malformed GDScript extractor lacking
`IsExtracting`/`ActiveResourceId` joins the registry silently and only
fails later, at read time, in `IsActivelyExtracting`/
`EstimatedRatePerSecond`, instead of being rejected up front with a named
reason.

**Fix:** Add the same `HasMethod`-based shape check (e.g.
`Get("IsExtracting")`/`Get("ActiveResourceId")` presence, or a documented
minimal contract) with a matching `GD.PushWarning` to
`GridExtractionManagerComponent.Register`, so a misconfigured/incomplete
extractor is rejected and reported instead of silently added to the
registry.

**Status: Fixed - by property presence, and the refusal is now in the
signature.** It cannot be the siblings' `HasMethod` check: the extractor
contract is answered by PROPERTY (`IsExtracting`, `ActiveResourceId`), so
`Register` asks `Get(...)` and rejects a `Variant.Type.Nil` - the same "does it
answer the shape" question one level down - with the same named
`GD.PushWarning` the transport manager emits. `Register` also changed from
`void` to `bool`, and `GridExtractorComponent.TryRegisterWithManager` now
records what the manager actually did (`_registered = _extractionManager
.Register(this)`) instead of assuming success: with validation added but the
old unconditional `_registered = true`, a refused extractor would have believed
it was registered and never retried - a rejection turned back into a silent
one, which is exactly what this finding is about. Registering the same node
twice is still a no-op and still reports success.

### GridExtractorComponent has no RegisterOnReady export, unlike its logistics/construction siblings

**Files:** `GridExtractorComponent.cs`, `GridHaulerComponent.cs`,
`GridBuilderUnitComponent.cs`

`GridExtractorComponent._Ready` does `if (!Engine.IsEditorHint())
TryRegisterWithManager();`, and `TryRegisterWithManager()` unconditionally
registers with `_extractionManager` whenever it resolves - there is no
gating export among its 14 `[Export]` members. `GridHaulerComponent` and
`GridBuilderUnitComponent` both carry `[Export] public bool RegisterOnReady
{ get; set; } = true;`, gating their own manager-registration call sites
with it specifically so an orchestrator can control registration
timing/order - the same reasoning behind this addon's `RefreshOnReady`/
`AutoConnect` pattern used throughout. `GridExtractorComponent` is the one
logistics/construction component missing that lever.

**Fix:** Add `[Export] public bool RegisterOnReady { get; set; } = true;`
to `GridExtractorComponent` and gate `TryRegisterWithManager`'s call site
the same way `GridHaulerComponent` and `GridBuilderUnitComponent` do.

**Status: Fixed.** The export is added and both call sites are gated exactly as
`GridHaulerComponent` gates its own - `_Ready` (`if (RegisterOnReady)
TryRegisterWithManager();`) and the per-tick retry (`if (!_registered &&
RegisterOnReady)`), so turning it off keeps the extractor out of the registry
rather than merely delaying it by one frame. `GridBuilderUnitComponent` is gone
(finding #1), so `GridHaulerComponent` is now the only sibling this matches -
the shape is unchanged.

Guarding these two findings turned up a gap worth naming: the only runtime
coverage that touches `GridExtractorComponent`/`GridExtractionManagerComponent`
was `tests/grid_terrain_subsurface_probe.gd`, which had **no `.ps1` runner and
was not invoked by `tests/run_addon_checks.ps1`** - written, passing, and never
run. It is now wired in (`tests/grid_terrain_subsurface_probe.ps1`, registered
in the gate beside the other grid probes) and extended with two checks: that a
node answering neither `IsExtracting` nor `ActiveResourceId` is refused rather
than silently registered, and that `RegisterOnReady = false` keeps a rig out of
the registry through `_Ready` AND the per-tick retry. The second was confirmed
to fail ("RegisterOnReady false keeps the extractor out of the registry...")
with the tick gate removed, before being kept.

### GridToolActionComponent.IsBlockedTerrainKind re-embeds a hardcoded duplicate of GridTerrainRules' default list, and inverts empty-list semantics

**Files:** `GridToolActionComponent.cs`, `GridTerrainRules.cs`

`GridTerrainRules.cs`'s own doc comment records that "seven components used
to carry their own copy of the blocked-kinds default list... and the copies
had already drifted," and were consolidated into one place.
`GridToolActionComponent.IsBlockedTerrainKind` reintroduces exactly that
pattern: `if (BlockedTerrainKinds.Count == 0) return normalizedTerrainKind
is "water" or "sea" or "ocean" or "deep_water" or "shallow_water" or
"lava"; return GridTerrainRules.MatchesAny(normalizedTerrainKind,
BlockedTerrainKinds);` - the six-entry fallback is a hand-typed duplicate of
`GridTerrainRules.DefaultBlockedTerrainKinds()`. Worse, it inverts the
semantics every other `BlockedTerrainKinds` consumer relies on:
`GridTerrainRules.MatchesAny` returns `false` for an empty list, so
`GridPlacementComponent`, `GridRoadComponent`, `GridWorkerSpawnerComponent`,
`GridResourceScatterComponent`, and `GridSelectionJobCommandComponent` all
treat an emptied `BlockedTerrainKinds` inspector list as "nothing blocked"
(and `GridNavigationComponent` guards with `BlockedTerrainKinds.Count > 0`
before even building its blocked set). `GridToolActionComponent` is the only
one of these consumers where clearing the list in the inspector does *not*
mean "nothing blocked" - it silently falls back to blocking
water/sea/ocean/deep_water/shallow_water/lava anyway.

**Fix:** Delete the hardcoded fallback branch and call
`GridTerrainRules.MatchesAny(normalizedTerrainKind, BlockedTerrainKinds)`
unconditionally, matching every other consumer's empty-means-unblocked
semantics. A project that wants the default list back gets it via
`BlockedTerrainKinds`' own export default, the same as every sibling
component.

**Status: Fixed - the method is gone entirely, not just its fallback branch.**
With the hardcoded list deleted, `IsBlockedTerrainKind` was a one-line wrapper
over `GridTerrainRules.MatchesAny` with a single caller, so the terrain
decision moved into `GridCellRules.CanWorkTerrain` (finding #1's shared rule)
and the method was removed rather than kept as a name with no consumer. An
emptied `BlockedTerrainKinds` now means "nothing blocked" here as it does for
every sibling. No scene or script anywhere sets that list empty, so no shipped
configuration changes behaviour; the default export list is unchanged and
`GridPlacementSmoke`'s water-rejection sections still exercise it.
`tests/addon_contract_scan.ps1` had pinned the method NAME in this file, and
that pin moved with the fact - the tool file is now required to mention
`GridCellRules`, and a new block requires `GridCellRules.cs` to carry every
term of the rule and forbids it re-embedding a hardcoded `"water" or ...`
list.

### GridDispatchBoardComponent resolves its NodePath collaborators once in _Ready(), with no ResolveReferences() and no staleness re-check

**Files:** `GridDispatchBoardComponent.cs`

Every other reviewed `Node`-derived grid component defines a private
`ResolveReferences()` that re-validates a cached node with
`GodotObject.IsInstanceValid` and is called from every public entry point -
confirmed for `GridBuildSiteComponent`, `GridExtractorComponent`,
`GridHaulerComponent`, `GridWorkerSpawnerComponent`, and the UI panels.
`GridDispatchBoardComponent._Ready()` instead resolves
`_spawner`/`_wallet`/`_status`/`_workMarker` exactly once, inline: `_spawner
= SpawnerPath.IsEmpty ? null :
GetNodeOrNull<GridWorkerSpawnerComponent>(SpawnerPath); ...` - there is no
`ResolveReferences()` method anywhere in the file, and none of
`Request`/`Dispatch`/`SetStatus` (or any other entry point) re-resolves or
`IsInstanceValid`-checks these fields before use. A NodePath assigned after
`_Ready`, or a collaborator node swapped or freed at runtime, is never
picked back up.

**Fix - two options, not picked:**

- Factor the four lookups into a conventional `ResolveReferences()` (with
  `IsInstanceValid` re-checks) called from `Request`/`OnUnitSpawned`/etc.,
  for consistency with the rest of the directory.
- Or, if this file is intentionally kept as a minimal, one-shot demo
  controller distinct from the reusable component family, say so explicitly
  in the class doc comment, so a developer moving from any other file in
  this directory isn't left assuming the same lazy-refresh contract applies
  here.

**Status: Fixed - option 1.** `ResolveReferences()` added, factoring the
four lookups (`_spawner`/`_wallet`/`_status`/`_workMarker`) with
`IsInstanceValid` re-checks, matching the rest of the directory. `_Ready`
and `Request` (the one public entry point besides Godot lifecycle callbacks)
both call it. The spawner's signal connect/disconnect moved into
`ConnectSpawner`/`DisconnectSpawner`, called from `ResolveReferences`
whenever `_spawner` is re-resolved, so a spawner swapped or freed at runtime
is picked back up instead of leaving the board silently deaf to
`UnitSpawned`/`SpawnRejected`.

### GridCropDefinition.TryRead's YieldItemId fallback doesn't match the field's own default, and the null-check-only ?? never catches it

**Files:** `GridCropDefinition.cs`, `GridCropCatalogComponent.cs`,
`GridToolActionComponent.cs`

`GridCropDefinition` exports `[Export] public string YieldItemId { get;
set; } = "turnip";`, but `TryRead`'s dictionary path and duck-typed-Resource
path both read it as `ReadString(data, "YieldItemId", "yield_item_id",
"")` - fallback `""`, not `"turnip"`. `GridCropCatalogComponent.YieldItem`
then does `return crop?.YieldItemId ?? cropId;` - a null check on `crop`
itself, not on the string. When a crop is authored via a plain dictionary or
a duck-typed resource with no `yield_item_id` key, `crop` is non-null but
`crop.YieldItemId == ""`, so `"" ?? cropId` evaluates to `""`, not `cropId`
and not `"turnip"`. `GridToolActionComponent.ApplyHarvest` then calls
`_resourceWallet.AddAmount(yieldId, yieldCount)` with that empty id. The
codebase already treats this exact case correctly one method away:
`SeedItem(cropId)` explicitly guards it - `return crop == null ||
string.IsNullOrWhiteSpace(crop.SeedItemId) ? "" : crop.SeedItemId.Trim();` -
proving the empty-vs-null distinction is already understood elsewhere in the
same file, just not applied to `YieldItem`.

**Fix - two options, not picked:**

- Change `YieldItem()` to treat an empty `YieldItemId` as "unset,"
  mirroring `SeedItem`'s `IsNullOrWhiteSpace` pattern:
  `string.IsNullOrEmpty(crop.YieldItemId) ? cropId : crop.YieldItemId`.
- Or change `TryRead`'s fallback for `YieldItemId` to default sensibly for a
  dictionary/duck-typed crop with no explicit yield id - e.g. the crop's
  own id - matching how a typed `GridCropDefinition` Resource already
  defaults to `"turnip"`.

**Status: Fixed - option 1.** `YieldItem()` now reads `crop == null ||
string.IsNullOrWhiteSpace(crop.YieldItemId) ? cropId : crop.YieldItemId.Trim()`,
mirroring `SeedItem` exactly. Verified via `tests/runtime_smoke.ps1`
("crops" section) and a clean `dotnet build`.

### The extraction/transport/building managers promise HUD and objective consumers that don't exist anywhere in this addon

**Files:** `GridExtractionManagerComponent.cs`,
`GridTransportManagerComponent.cs`, `GridBuildingManagerComponent.cs`,
`GridObjectiveEventBinderComponent.cs`

`GridExtractionManagerComponent`'s class doc reads: "every derrick, mine and
custom rig announces itself here, and HUDs, objectives and game logic ask
ONE node instead of crawling the tree." `GridTransportManagerComponent` and
`GridBuildingManagerComponent` are the same shape (Register/Unregister plus
query methods). None of the 13 files in `ui/` reference any of the three
managers - jobs get `GridJobBoardComponent`, workers get
`GridWorkerStatusPanelComponent`, production gets
`GridProductionPanelComponent`, but extraction, transport and building get
no HUD panel at all. `GridObjectiveEventBinderComponent.ConnectSystems` only
wires `_jobs.JobCompleted`, `_buildSites.BuildSiteCompleted`,
`resourceNode.Gathered`, and `production.ProductionCompleted` - never
`ExtractorRegistered`/`Unregistered`, `HaulAssigned`/`Unassigned`, or
`BuildAssigned`/`Unassigned`. The practical consequence: extraction via the
shipped `GridExtractorComponent` (a placed building working a deposit) never
advances any objective - only `GridResourceNodeComponent.Gathered`
(hand-gathered props) does. Two parallel resource-acquisition paths exist,
and the objective system understands only one of them.

**Fix - two options, not picked:**

- Add the promised consumer(s): an extraction/transport/building status HUD
  panel alongside the existing worker/production ones, and wire
  `GridObjectiveEventBinderComponent` to `ExtractorRegistered`/an
  extraction-cycle signal plus the transport/building managers' assignment
  signals.
- Or remove the "HUDs, objectives and game logic ask ONE node" claim from
  the three doc comments, so the API stops promising a consumer that never
  exists in this addon.

**Status: Fixed - a hybrid of both, plus a scope change from finding #1.**
`GridBuildingManagerComponent` (the "building" leg of this finding) is
deleted outright - see finding #1 above - so build objectives were never
this gap: `GridObjectiveEventBinderComponent` already wires
`BuildSiteCompleted` directly. For extraction,
`GridObjectiveEventBinderComponent` now tracks each registered
`GridExtractorComponent`'s `ExtractionCycle` (via the extraction manager's
own `ExtractorRegistered`/`Unregistered`) and feeds it into the SAME
resource-id objective `TrackGatheredResources` already uses, so "collect N
wood" advances identically whichever path acquired it - closing the actual
defect the finding named. Transport is deliberately left unwired: an
extractor delivering `DeliverVia = TransportManager` already fires one
`ExtractionCycle` for those units, and the hauler's later `HaulDelivered` is
the SAME units arriving somewhere, not new units acquired - wiring both
would double-count. `GridExtractionManagerComponent`'s doc comment corrected
to describe what actually consumes it (the binder, not a HUD) and to name
that no dedicated HUD panel exists for extraction or transport.
`GridTransportManagerComponent`'s doc comment never made the "HUDs,
objectives" claim in the first place - only `GridExtractionManagerComponent`
did - so it needed no correction. No new HUD panel was built.

### "Repair" is a documented settler-style job command with no implementing effect, and no building-health data model to act on

**Files:** `GridJobEffectComponent.cs`, `GridSelectionJobCommandComponent.cs`,
`GridJobQueueComponent.cs`, `GridObjectComponent.cs`,
`GridToolActionComponent.cs`, `GridCalendarComponent.cs`

Two class doc comments already use the vocabulary:
`GridSelectionJobCommandComponent`'s doc lists "clear land, prepare pad,
harvest, repair, build, deliver, or inspect," and `GridJobQueueComponent`'s
doc independently repeats "clear land, build road, harvest tile, repair
object, deliver resource, or inspect a cell." `GridJobEffectComponent.
ApplyJobEffect` switches only on gather/collect/forage/chop/mine/fish,
clear_land/clear, till/hoe/prepare_soil, water, and harvest - every other
kind, including "repair," falls to `_ => Reject(jobId, kind, cell,
"unknown_job_kind")`. There is nothing for a repair effect to act on:
`GridObjectComponent` exposes only a `Complete` bool (under-construction vs.
finished, set once by `GridBuildSiteComponent.CompleteBuildSite`), no
Health/Condition/Durability property exists anywhere in the grid domain, and
`GridToolActionComponent.ToolAction` has no Repair case (Clear, Hoe, Water,
Plant, Harvest, QueueJob, Road, RemoveRoad only). No decay/upkeep tick
exists either: `GridCalendarComponent.DayAdvanced` has exactly one
subscriber addon-wide (`GridCalendarHudComponent`, which only repaints a
label).

**Fix - two options, not picked:**

- Implement the missing piece end to end: a Condition/Durability field on
  `GridObjectComponent`, a decay tick (e.g. driven off
  `GridCalendarComponent.DayAdvanced`), and a "repair" case in
  `GridJobEffectComponent` that restores it.
- Or remove "repair"/"repair object" from the two doc comments' lists of
  supported commands, so they stop advertising a capability nothing
  implements.

**Status: Fixed - option 2.** "Repair"/"repair object" removed from both
`GridSelectionJobCommandComponent`'s and `GridJobQueueComponent`'s class doc
comments. No Condition/Durability/decay system was built - that remains a
real, larger feature a project can add later (it would need its own design
pass: what decays, how fast, whether it is per-building or per-resource-type),
not something to bolt on as a side effect of a doc-comment audit.

### In-flight economic state - extractor buffers, hauler cargo, in-progress recipes - isn't part of the save system, unlike comparable sibling state

**Files:** `GridExtractorComponent.cs`, `GridHaulerComponent.cs`,
`GridProductionComponent.cs`, `ISaveable.cs`, `GridResourceWalletComponent.cs`

`ISaveable.cs` defines an explicit opt-in pattern (implement `ISaveable`,
join the `"saveables"` group via a `ParticipatesInSave` export), which
`GridStorageComponent`, `GridSubsurfaceStoreComponent`,
`GridProspectingComponent`, `GridCalendarComponent`,
`GridObjectiveTrackerComponent`, `GridRoadComponent`, and
`GridResourceWalletComponent` all follow. Three components with comparable
in-flight state do not: `GridExtractorComponent`'s Buffer-delivery
`_bufferAmount`/`_bufferId` (its own `ILoadPort`/`IUnloadPort`
implementation), `GridHaulerComponent`'s `_cargoId`/`_cargoAmount`, and
`GridProductionComponent`'s `CurrentRecipeId`/`RemainingTurns` (then named
`RemainingSeconds`) - none implement `ISaveable`, and neither `GameplayComponent` nor `EntityComponent`
(their base classes) add any generic persistence. This is concretely
damaging for production: `GridProductionComponent.StartProduction` spends
`recipe.Inputs` from the wallet immediately when `ConsumeInputsOnStart` is
true (the default) - that debit *is* saved, because the wallet is
`ISaveable` - but `CurrentRecipeId`/`RemainingTurns` are not, so a save
taken mid-cycle and reloaded leaves the machine sitting Idle: the inputs are
already gone from the wallet, and the output is never produced. This
directly contradicts the doc comments' own repeated invariant ("yield is
never lost," "cargo is never duplicated and never lost") - true only while
the game keeps running, not across a save/load.

**Fix:** Give `GridExtractorComponent`, `GridHaulerComponent`, and
`GridProductionComponent` the same `ISaveable` treatment as their siblings
(persist buffer contents, cargo hold, and recipe-in-progress respectively).
At minimum, as a stopgap short of full persistence, refund `recipe.Inputs`
for any `GridProductionComponent` found `Producing` at save time, so the
loss doesn't happen silently before the fuller fix lands.

**Status: Fixed - full persistence, not the stopgap.** All three now
implement `ISaveable` (explicit-interface on `GridExtractorComponent`/
`GridHaulerComponent`, since both already have a duck-typed `Load(resourceId,
amount)` that would otherwise collide with `ISaveable.Load(state)` under
Godot's name-based `Call`), each with its own `ParticipatesInSave`/`SaveKey`
and `SaveableHelper.Group` join/leave, matching the sibling pattern exactly.
`GridExtractorComponent` persists only `_bufferId`/`_bufferAmount` - binding
state re-derives itself via `TryBind()` on the next tick against the
also-persisted `GridSubsurfaceStoreComponent`. `GridHaulerComponent` persists
`_cargoId`/`_cargoAmount` and immediately retries `TryDeliverCargo()` on
restore, so cargo mid-haul either resumes the normal depot-full backpressure
retry or, if no depot is wired, delivers straight to the wallet instead of
sitting lost. `GridProductionComponent` persists `State`/`CurrentRecipeId`/
`RemainingTurns` (key `remaining_turns`) and restores them directly (bypassing `StartProduction`,
so the already-saved wallet debit is never spent a second time) when the
saved state was `Producing`/`Paused`. Verified via `tests/runtime_smoke.ps1`
("production" section) and a clean `dotnet build`.

### GridWorkerStatusPanelComponent.RefreshPanel re-marshals the entire job queue into fresh Dictionaries, 2-3 times per idle worker, every tick

**Files:** `ui/GridWorkerStatusPanelComponent.cs`, `GridJobQueueComponent.cs`

`RefreshPanel()` runs on a fixed, ungated timer - every
`RefreshIntervalSeconds` (default 0.25s, 4Hz), driven by
`SetProcess(AutoRefresh)` with no dirty-flag or change-signal gating at all.
For every idle worker, its summary loop calls `EffectiveState(worker)`,
which calls `FindClaimedJobId(worker.WorkerId)` when the worker is idle; the
row-building loop then calls `TextForWorker(worker)`, which calls
`EffectiveState(worker)` *again* and, separately, calls `FindClaimedJobId` a
third time whenever `worker.CurrentJobId` is empty - which it always is for
an idle worker, since every state transition to Idle in `GridWorkerComponent`
also clears `CurrentJobId`. `FindClaimedJobId` is `foreach
(Godot.Collections.Dictionary job in _jobs.GetJobs()) { ... }` - a full
linear scan - and `GridJobQueueComponent.GetJobs()` allocates a brand-new
`Godot.Collections.Array` plus one new 7-key `Dictionary`
(`GridJob.ToDictionary()`) per job on every single call, discarded after one
scan. So each idle worker triggers up to three O(totalJobs) marshal-and-scan
passes per refresh tick - O(idleWorkers x totalJobs) allocations, four times
a second, forever, regardless of whether the queue changed.
`GridJobBoardComponent`, by contrast, only rebuilds in response to
`_queue.QueueChanged` - genuinely signal-gated, with no polling at all.

**Fix:** Build the claimed-job-by-worker lookup once per `RefreshPanel`
call - call `_jobs.GetJobs()` a single time, build a local
`Dictionary<string,string> claimedJobByWorkerId`, and have
`EffectiveState`/`TextForWorker` consult it instead of each independently
re-querying the whole queue. Better still, maintain an incremental reverse
index (workerId -> jobId) on `GridJobQueueComponent` itself, updated on
`ClaimJob`/`ClaimNextJob`/`ReleaseJob`/`CompleteJob`/`CancelJob`, so no O(n)
scan is ever needed to answer "what job is this worker claiming."

**Status: Fixed - the "better still" reverse-index option, not the local-map
minimum.** `GridJobQueueComponent` gained `FindClaimedJobId(workerId)` (a
direct scan over the live `GridJob` objects, no `Dictionary` marshalling) and
`GetClaimedJobIdsByWorker()` (one pass building the whole map). `RefreshPanel`
now builds that map once per tick and threads it through `EffectiveState`/
`TextForWorker`; the two on-demand single-worker call sites
(`CancelWorkerJob`, the public single-worker `TextForWorker` overload) go
through `FindClaimedJobId` instead. The dead `DictString` helper (its only
caller was the old scan) was removed. Verified via `tests/GridPlacementSmoke.cs`'s
worker-status-panel section (idle/active row rendering) through
`tests/runtime_smoke.ps1`, and a clean `dotnet build`.

### GridWorkerStatusPanelComponent.Workers() walks and sorts the entire units subtree unconditionally, every refresh tick

**Files:** `ui/GridWorkerStatusPanelComponent.cs`, `GridWorkerSpawnerComponent.cs`

`Workers()` - called from `RefreshPanel()` every timer tick (default 4Hz,
per the finding above) - recurses `CollectWorkers` over every descendant of
`_unitsRoot` regardless of type, then sorts the result with a string
comparison, unconditionally: no dirty flag, no cache, no early-out. This
runs whether or not the worker roster actually changed since the last tick;
workers only enter or exit rarely, via `GridWorkerSpawnerComponent`.
Contrast the two signal/dirty-flag-driven siblings in the same folder:
`GridJobBoardComponent` only rebuilds on `_queue.QueueChanged`, and
`GridMinimapComponent` gates each of its cached lists behind its own dirty
flag, set only by the relevant change signal.

**Fix:** Cache the worker list and only rebuild it when the roster actually
changes - subscribe to `GridWorkerSpawnerComponent.UnitSpawned` (already
emitted in the codebase) to append new workers, and prune on
`GodotObject.IsInstanceValid` failure the same way
`GridBuildingManagerComponent`/`GridTransportManagerComponent` already
prune their own registries - rather than re-walking the whole tree every
refresh tick.

**Status: Fixed.** `Workers()` now returns a cached `List<GridWorkerComponent>`,
rebuilt in full only on first use (or after `InvalidateWorkerCache()`, a new
public escape hatch); every other call just prunes freed entries
(`IsInstanceValid`) and returns the cache. A new optional `SpawnerPath`
export (auto-found scene-wide when empty) subscribes to
`GridWorkerSpawnerComponent.UnitSpawned` and appends the spawned unit's
`GridWorkerComponent` to the cache, re-sorting once, instead of re-walking
`UnitsRootPath`. `GridBuildingManagerComponent` is gone (finding #1), so the
"prune their own registries" comparison now reads on
`GridTransportManagerComponent`/`GridExtractionManagerComponent` alone - the
shape is unchanged. Documented limit: a worker added some OTHER way (a
second spawner, or one hand-placed in the scene after the panel's first
refresh) is missed by the incremental path; `InvalidateWorkerCache()` covers
that case explicitly rather than pretending the cache is always complete.

### GridProductionPanelComponent.Machines() does the same full tree walk, plus a GetPath().ToString() allocation per comparison

**Files:** `ui/GridProductionPanelComponent.cs`

`Machines()` performs the same unconditional full recursive tree walk as
`GridWorkerStatusPanelComponent.Workers()` (above), called every
`RefreshPanel()` tick (default `RefreshIntervalSeconds` 0.25s, 4Hz)
regardless of whether any production building was added, removed, or
changed state. Its sort comparator additionally calls `MachineKey(machine)
=> machine.GetPath().ToString()` on every comparison, uncached - each call
walks the node up to the scene root and allocates a fresh string - and
`RefreshPanel`'s own `seen.Add(key)` loop calls it again per machine on top
of that.

**Fix:** Same remedy as `GridWorkerStatusPanelComponent.Workers()`: cache
the machine list and only rebuild it on an actual add/remove event (or
invalidate via a dirty flag toggled from a lightweight registration call
each `GridProductionComponent` could make in `_Ready`/`_ExitTree`,
mirroring `GridExtractionManagerComponent`'s Register/Unregister pattern),
and precompute each machine's sort key once instead of calling
`GetPath().ToString()` repeatedly inside the comparator.

**Status: Fixed - reusing GridPlacementComponent.PlacementPlaced rather than
a new registration manager.** `Machines()` is now a cached
`List<GridProductionComponent>` with the same rebuild-once/prune-cheaply
shape as `Workers()` above, plus `InvalidateMachineCache()`. Rather than
inventing a new registration manager for production buildings (no such
manager exists in this codebase, and one purely to feed a cache felt like
more surface than the finding), a new optional `PlacementPath` export
subscribes to the ALREADY-emitted `GridPlacementComponent.PlacementPlaced`
signal and appends a newly-placed node's `GridProductionComponent` (if it
has one) to the cache. Each machine's key (`GetPath().ToString()`) is
computed once and cached in a `Dictionary<GridProductionComponent,string>`,
populated on rebuild and on each placement-triggered append, purged on
prune; `RefreshPanel`'s row loop and `FindMachine` read through the cache
instead of recomputing. Same documented limit as the worker panel: a machine
added with no placement signal at all is missed incrementally;
`InvalidateMachineCache()` is the escape hatch.

## File index

### Core primitives and port interfaces

| File | Purpose | Doc |
|---|---|---|
| IWorker.cs | C# interface for something that claims and works one job at a time from `GridJobQueueComponent` - a settler, a crane, a robot, a drone; `GridWorkerComponent` is the shipped implementation. | [IWorker.md](IWorker.md) |
| IExtractor.cs | C# interface for something that produces material out of the world - a mine, well, or farm plot - composing `ILoadPort`/`IUnloadPort` so its output buffer connects directly to the logistics chain. | [IExtractor.md](IExtractor.md) |
| ILoadPort.cs | C# interface for the RECEIVING port - anything material can be pushed into - one of the two atomic connectors the logistics layer is built from. | [ILoadPort.md](ILoadPort.md) |
| IStorage.cs | C# interface for a stationary hold - a tank, silo, or warehouse - composing `ILoadPort`/`IUnloadPort` and adding nothing of its own. | [IStorage.md](IStorage.md) |
| ITransporter.cs | C# interface for both ports plus mobility - a truck, boat, train, or drone; `GridHaulerComponent` is the shipped implementation. | [ITransporter.md](ITransporter.md) |
| IUnloadPort.cs | C# interface for the GIVING port - anything material can be drawn out of - the second atomic connector the logistics layer is built from. | [IUnloadPort.md](IUnloadPort.md) |
| GridPorts.cs | Static helper holding the one safe port hand-off implementation (unload from giver, load into receiver, remainder back), read by duck-typed name so GDScript nodes participate too. | [GridPorts.md](GridPorts.md) |
| GridWorkerPorts.cs | The IWorker equivalent of GridPorts - duck-typed IsWorking/CurrentJobId lookup for a GDScript worker that cannot implement the C# interface. | [GridWorkerPorts.md](GridWorkerPorts.md) |
| GridVariantReader.cs | Static helper coercing loosely-typed Godot `Variant` values into concrete C# types, defensively, regardless of source shape. | [GridVariantReader.md](GridVariantReader.md) |
| GridDefinitionReader.cs | Static helper reading definition data (build/crop/recipe/objective/resource-amount) whether it arrives as a typed Resource, duck-typed Resource, or plain Dictionary, PascalCase or snake_case. | [GridDefinitionReader.md](GridDefinitionReader.md) |
| GridResourceAmount.cs | `[Tool][GlobalClass]` Resource pairing a resource id with a quantity - the smallest data primitive in the grid economy. | [GridResourceAmount.md](GridResourceAmount.md) |

### Logistics: extraction, transport, storage

| File | Purpose | Doc |
|---|---|---|
| GridExtractionManagerComponent.cs | Registry of everything currently extracting, so a HUD/objective/game-logic query asks one node instead of crawling the tree for extractors. | [GridExtractionManagerComponent.md](GridExtractionManagerComponent.md) |
| GridExtractorComponent.cs | Default extractor - a placed building that works the deposit under it and draws it down cycle by cycle into the wallet or logistics chain. | [GridExtractorComponent.md](GridExtractorComponent.md) |
| GridHaulerComponent.cs | Default transporter - a vehicle that accepts hauls, drives to pickup then depot, and pays the load into the wallet or a wired storage's load port. | [GridHaulerComponent.md](GridHaulerComponent.md) |
| GridTransportChainComponent.cs | A standing transport chain between ports - the general mechanism a pipeline, conveyor, or bucket brigade is one dress for. | [GridTransportChainComponent.md](GridTransportChainComponent.md) |
| GridTransportManagerComponent.cs | Dispatcher between "something needs moving" and "something that moves things" - offers a requested haul to registered transporters, fastest first. | [GridTransportManagerComponent.md](GridTransportManagerComponent.md) |
| GridStorageComponent.cs | Stationary cargo hold implementing `IStorage`/`ISaveable` - holds material and does nothing else. | [GridStorageComponent.md](GridStorageComponent.md) |
| GridSubsurfaceStoreComponent.cs | The one owner of "how much is left underground" per cell - the mutable drawdown half, separate from the immutable generated map. | [GridSubsurfaceStoreComponent.md](GridSubsurfaceStoreComponent.md) |
| GridProspectingComponent.cs | Optional discovery layer - the underground stratum starts hidden and survey work reveals it, cell by cell. | [GridProspectingComponent.md](GridProspectingComponent.md) |
| GridTerrainRules.cs | The one place grid components agree on what a terrain-kind string means for building and working - default blocked-kinds list, normalizer, membership checks. | [GridTerrainRules.md](GridTerrainRules.md) |
| GridCellRules.cs | The one implementation of "can this cell be worked, and can it take a job" - bounds, navigation, the cell's Blocked flag and terrain kind - built by a component from its own exports and asked at the point of decision. | [GridCellRules.md](GridCellRules.md) |
| GridJobBlock.cs | The reason `GridCellRules` gives for refusing a cell, so the two queueing paths can share one rule while each keeps its own signal vocabulary. | [GridJobBlock.md](GridJobBlock.md) |
| IGridSite.cs | C# interface for anything commissioned onto the grid that occupies cells: area, materials, turns — three facts asked one way. | [IGridSite.md](IGridSite.md) |
| GridSitePorts.cs | Duck-typed reader for the site contract, so a GDScript definition or an authored Dictionary answers the same three questions by name. | [GridSitePorts.md](GridSitePorts.md) |
| GridClockPorts.cs | Duck-typed lookup for the game clock, so the grid is driven by it without depending on it — no `GameApp`, no autoload path. | [GridClockPorts.md](GridClockPorts.md) |
| GridWorkClockComponent.cs | The one node that knows what a unit of work is: converts the game clock's beats into turns (a turn is a day), emits `WorkTick`, drives the calendar, self-ticks when no clock exists. | [GridWorkClockComponent.md](GridWorkClockComponent.md) |
| GridWorkClockBinding.cs | Composed helper that binds a timed component to the work clock and reports whether it found one — one bind, not five copies. | [GridWorkClockBinding.md](GridWorkClockBinding.md) |

### Construction

| File | Purpose | Doc |
|---|---|---|
| GridBuildCatalogComponent.cs | Menu data source holding the list of placeable builds - the single front door a build menu talks to. | [GridBuildCatalogComponent.md](GridBuildCatalogComponent.md) |
| GridBuildDefinition.cs | `[Tool][GlobalClass]` Resource describing one placeable build target - identity, footprint, cost, and construction rules. | [GridBuildDefinition.md](GridBuildDefinition.md) |
| GridBuildSiteComponent.cs | State machine turning a just-placed build into a finished building - blueprint visuals, materials wait, construction job, refund-on-cancel. | [GridBuildSiteComponent.md](GridBuildSiteComponent.md) |
| GridBuildProgressBarComponent.cs | Instantiates an authored `Range` scene (default: a `KitMeter`) as an overlay over each structure under construction and sets its value from `GridJobQueueComponent.GetJobProgress01`; draws nothing. | [GridBuildProgressBarComponent.md](GridBuildProgressBarComponent.md) |
| GridWorkerBuildActivityEffectComponent.cs | Worker-side construction effect - triggers a sibling `ParticleComponent`/`SpriteEffectComponent` while an `IWorker` is actively working a matching job kind. | [GridWorkerBuildActivityEffectComponent.md](GridWorkerBuildActivityEffectComponent.md) |
| GridBuildStageVisualComponent.cs | Instantiates a build's `ConstructionStages` scenes, tells each the site through `IConstructionVisual`, and shows one by job progress; draws nothing. | [GridBuildStageVisualComponent.md](GridBuildStageVisualComponent.md) |
| IConstructionVisual.cs | Contract a construction-stage scene (or the build's own Scene) answers: `SiteFootprint`, `CellSize`, `BuildProgress` - the line between the grid layer orchestrating and the scene rendering. | [IConstructionVisual.md](IConstructionVisual.md) |
| GridConstructionVisualPorts.cs | Duck-typing fallback for `IConstructionVisual` so a pure GDScript scene participates by name. | [GridConstructionVisualPorts.md](GridConstructionVisualPorts.md) |

### Cells, placement, grid core

| File | Purpose | Doc |
|---|---|---|
| GridCellDataComponent.cs | Pure data model for per-cell land state - terrain kind, workflow flags, crop id/age/maturity, metadata - with no TileMap dependency. | [GridCellDataComponent.md](GridCellDataComponent.md) |
| GridCellOverlayComponent.cs | Lightweight debug/placeholder renderer drawing colored fills/outlines over cell state before a project has authored TileMap art. | [GridCellOverlayComponent.md](GridCellOverlayComponent.md) |
| GridPlacementComponent.cs | Drives the full grid-placement interaction - preview-follows-mouse, click-to-place, footprint occupancy, cost charging, terrain validity. | [GridPlacementComponent.md](GridPlacementComponent.md) |
| GridProjectionComponent.cs | Shared grid-math foundation for top-down/isometric worlds - WorldToCell, CellToWorld, SnapWorld - that almost every other file reads through. | [GridProjectionComponent.md](GridProjectionComponent.md) |
| GridTileMapLayerBridgeComponent.cs | Synchronizes `GridCellDataComponent`/`GridRoadComponent` state into a real `TileMapLayer`, the authored-art counterpart to the debug overlay. | [GridTileMapLayerBridgeComponent.md](GridTileMapLayerBridgeComponent.md) |
| GridWorldStateComponent.cs | `ISaveable` `Node` capturing/restoring the entire reusable grid toolkit's runtime state as one versioned Dictionary snapshot. | [GridWorldStateComponent.md](GridWorldStateComponent.md) |
| GridObjectComponent.cs | Common identity/inspection/footprint-reservation component for anything placed on the grid - building, prop, machine, resource node, unit. | [GridObjectComponent.md](GridObjectComponent.md) |

### Navigation and movement

| File | Purpose | Doc |
|---|---|---|
| GridNavigationComponent.cs | Runs A* pathfinding over a 2D grid, cell-based and TileMap-independent, with blocking/cost from placement, cell data, and roads. | [GridNavigationComponent.md](GridNavigationComponent.md) |
| GridPathFollowerComponent.cs | Moves its parent body along a path - fetched from `GridNavigationComponent` or handed directly - the ready-made movement loop for workers/trucks/units. | [GridPathFollowerComponent.md](GridPathFollowerComponent.md) |
| GridCameraControllerComponent.cs | Pan/zoom camera control for authored top-down/isometric maps, driving the camera directly without depending on TileMap. | [GridCameraControllerComponent.md](GridCameraControllerComponent.md) |
| GridRoadComponent.cs | Stores, draws, and saves player-built roads/paths per cell - the movement-cost source `GridNavigationComponent` reads back. | [GridRoadComponent.md](GridRoadComponent.md) |

### Jobs, dispatch, workers

| File | Purpose | Doc |
|---|---|---|
| GridJobQueueComponent.cs | Central job registry for the builder/RTS/farming/tactics work loop - owns the full Queued -> Claimed -> Completed/Cancelled lifecycle. | [GridJobQueueComponent.md](GridJobQueueComponent.md) |
| GridJobEffectComponent.cs | Translator between "a job finished" and "the world changed" - turns a completed job's kind into a concrete cell/crop mutation. | [GridJobEffectComponent.md](GridJobEffectComponent.md) |
| GridDispatchBoardComponent.cs | Single-vehicle "settlers" dispatch loop - a button requests a task, a shared vehicle tweens out and back - independent of the job queue/worker system. | [GridDispatchBoardComponent.md](GridDispatchBoardComponent.md) |
| GridDispatchTaskDefinition.cs | Pure-data Resource describing one dispatchable task for `GridDispatchBoardComponent` - trigger, destination, visibility effects, wallet cost/gain. | [GridDispatchTaskDefinition.md](GridDispatchTaskDefinition.md) |
| GridWorkerComponent.cs | Per-unit agent half of the job system - claims a job, drives to its cell, works it out, completes or fails it. | [GridWorkerComponent.md](GridWorkerComponent.md) |
| GridWorkerSpawnerComponent.cs | Factory for worker/truck/NPC units - validates the spawn cell, instantiates the unit, wires its path-follower and worker components. | [GridWorkerSpawnerComponent.md](GridWorkerSpawnerComponent.md) |
| GridSelectionJobCommandComponent.cs | Turns a selection/cell array/rectangle into jobs on `GridJobQueueComponent` - the input-to-job bridge for settler-style commands. | [GridSelectionJobCommandComponent.md](GridSelectionJobCommandComponent.md) |
| GridToolActionComponent.cs | Unified Stardew-style tool dispatcher - one `ToolAction` enum applied to a cell, selection, or hover, each action with its own eligibility rules. | [GridToolActionComponent.md](GridToolActionComponent.md) |

### Economy, production, crops

| File | Purpose | Doc |
|---|---|---|
| GridResourceNodeComponent.cs | A single harvestable resource deposit on the grid - tree, rock, bush, crate - gathered through the job queue into the wallet. | [GridResourceNodeComponent.md](GridResourceNodeComponent.md) |
| GridResourceScatterComponent.cs | Seeded generator populating resource-node props across a rectangular bounds so a map gets deposits without hand-placing every one. | [GridResourceScatterComponent.md](GridResourceScatterComponent.md) |
| GridResourceWalletComponent.cs | Small string-keyed integer resource store - the shared sink/source every other economy component writes through. | [GridResourceWalletComponent.md](GridResourceWalletComponent.md) |
| GridProductionComponent.cs | Attach-to-building timed production cycle - consumes a recipe's inputs from the wallet, waits out the duration, adds outputs back. | [GridProductionComponent.md](GridProductionComponent.md) |
| GridProductionRecipe.cs | Data-driven production recipe - id, display name, duration, input costs, output yields - read by `GridProductionComponent.Recipes`. | [GridProductionRecipe.md](GridProductionRecipe.md) |
| GridCropCatalogComponent.cs | Lookup table of `GridCropDefinition` entries, queried by crop id for maturity, seasons, regrowth, seed cost, and yield. | [GridCropCatalogComponent.md](GridCropCatalogComponent.md) |
| GridCropDefinition.cs | One crop's farming rules - maturity time, valid seasons, regrowth, seed cost, harvest yield - authored in the Inspector. | [GridCropDefinition.md](GridCropDefinition.md) |

### Interaction and selection

| File | Purpose | Doc |
|---|---|---|
| GridInteractionCursorComponent.cs | Presentation-only node drawing the single outlined cell the current interaction cares about - never writes to any other component. | [GridInteractionCursorComponent.md](GridInteractionCursorComponent.md) |
| GridInteractionModeComponent.cs | Orchestrator owning "what does a click mean right now" and routing it to exactly one of selection, tool, or placement. | [GridInteractionModeComponent.md](GridInteractionModeComponent.md) |
| GridSelectionComponent.cs | Standalone hover/click/rectangle-selection primitive for a grid, fully self-driving with its own input handling and drawing. | [GridSelectionComponent.md](GridSelectionComponent.md) |

### Objectives and calendar

| File | Purpose | Doc |
|---|---|---|
| GridObjectiveDefinition.cs | Authored Resource describing one settlement/grid objective's static goal - id, display name, description, target count. | [GridObjectiveDefinition.md](GridObjectiveDefinition.md) |
| GridObjectiveEventBinderComponent.cs | Wires grid gameplay signals (jobs, builds, gathers, production) into `GridObjectiveTrackerComponent` progress calls, so a project needs no bespoke glue. | [GridObjectiveEventBinderComponent.md](GridObjectiveEventBinderComponent.md) |
| GridObjectiveTrackerComponent.cs | `ISaveable` owner of runtime objective state - activation, progress, completion - for the entries authored on `GridObjectiveDefinition`. | [GridObjectiveTrackerComponent.md](GridObjectiveTrackerComponent.md) |
| GridCalendarComponent.cs | `ISaveable` in-game calendar (day/season/year) that advances crops by day and can run from real seconds or manual end-day. | [GridCalendarComponent.md](GridCalendarComponent.md) |

### UI panels

| File | Purpose | Doc |
|---|---|---|
| ui/GridPanelComponent.cs | Abstract base for every grid HUD panel - the editor-owner stamp, the node-name sanitiser, and the three-tier authored-control lookup, once. | [GridPanelComponent.md](GridPanelComponent.md) |
| ui/GridListPanelComponent.cs | Abstract base for the four panels that render a keyed, sorted row list - the bind-or-generate bootstrap, the generated layout, and the in-place row diff. | [GridListPanelComponent.md](GridListPanelComponent.md) |
| ui/GridPanelRow.cs | One row a list panel renders: reuse key, text, state colour, tooltip. | [GridPanelRow.md](GridPanelRow.md) |
| ui/GridBuildToolbarComponent.cs | Build palette HUD grouping a catalog's registered builds into category tabs, one button per build calling `BeginPlacement`. | [GridBuildToolbarComponent.md](GridBuildToolbarComponent.md) |
| ui/GridCalendarHudComponent.cs | Compact HUD showing the current in-game date, an optional day-progress bar, and an optional "next day" button. | [GridCalendarHudComponent.md](GridCalendarHudComponent.md) |
| ui/GridInteractionModeBarComponent.cs | HUD button bar for switching `GridInteractionModeComponent` between Select/Inspect/Tool/Build/Disabled. | [GridInteractionModeBarComponent.md](GridInteractionModeBarComponent.md) |
| ui/GridInteractionStatusComponent.cs | Single-line status readout combining mode, hovered/placement cell, current tool/build id, and last feedback into one label. | [GridInteractionStatusComponent.md](GridInteractionStatusComponent.md) |
| ui/GridJobBoardComponent.cs | HUD panel showing queued/claimed/done job counts plus a sorted job list, with per-job cancel. | [GridJobBoardComponent.md](GridJobBoardComponent.md) |
| ui/GridMinimapComponent.cs | Overview map custom-drawing terrain, roads, jobs, selection, live units, and the camera viewport rectangle, all in one `_Draw` call. | [GridMinimapComponent.md](GridMinimapComponent.md) |
| ui/GridObjectInspectorComponent.cs | HUD inspector bridging the current `GridSelectionComponent` selection to a title/details label pair for the selected object. | [GridObjectInspectorComponent.md](GridObjectInspectorComponent.md) |
| ui/GridObjectivePanelComponent.cs | Read-only HUD panel listing active objectives and their progress. | [GridObjectivePanelComponent.md](GridObjectivePanelComponent.md) |
| ui/GridProductionPanelComponent.cs | HUD panel scanning a production-root subtree for every machine and exposing start/pause/resume/cancel commands. | [GridProductionPanelComponent.md](GridProductionPanelComponent.md) |
| ui/GridResourceBarComponent.cs | HUD strip displaying every non-zero wallet entry, refreshing on the wallet's `ResourcesChanged` signal. | [GridResourceBarComponent.md](GridResourceBarComponent.md) |
| ui/GridToolPaletteComponent.cs | HUD palette with one toggle button per land-tool action, kept in sync with the component's current action. | [GridToolPaletteComponent.md](GridToolPaletteComponent.md) |
| ui/GridWorkerSpawnerPanelComponent.cs | Simple HUD panel for a spawner base - title, count/max readout, spawn button. | [GridWorkerSpawnerPanelComponent.md](GridWorkerSpawnerPanelComponent.md) |
| ui/GridWorkerStatusPanelComponent.cs | HUD panel scanning a units-root subtree for workers and showing idle/moving/working state plus job and remaining time. | [GridWorkerStatusPanelComponent.md](GridWorkerStatusPanelComponent.md) |

## What this review does not cover

The terrain engine (`addons/beep_game_builder_cs/ecs/terrain/`) is a
separate system with its own review, already recorded in
`docs/terrain-engine/ENHANCEMENT_AND_FIX_PLAN.md`, and was out of scope
here even though a few files in this batch (`GridResourceScatterComponent`,
`GridCameraControllerComponent`, `GridTerrainRules`) are read by or
referenced from that engine's own docs. Anything that would require a live
Godot editor render to judge - whether a HUD panel's generated layout
actually looks right, whether a sprite/icon renders correctly - was not
judged; findings here are limited to what could be verified by reading
source, tracing call chains, and grep. The GDScript-side half of this
system's duck-typed contracts (a GDScript node implementing `IExtractor`'s
shape, for instance) was not separately audited beyond what the C# side's
own `HasMethod`/shape checks already imply - a full audit of every
GDScript participant across the repo was out of scope. Performance findings
are based on tracing the call graph and counting allocations/passes per
tick, not on profiler measurements against a real playtime frame budget;
the O(n) multipliers cited are structural, not wall-clock, though they are
large enough (4Hz x every idle worker x every queued job, repeated per
production machine) that the structural case does not depend on a specific
frame budget to be worth fixing.

Raw finding counts before dedup/verify, per dimension: duplication 5 raw / 5
confirmed; ownership 3 raw / 3 confirmed; consistency 5 raw / 5 confirmed;
completeness 4 raw / 3 confirmed (one completeness finding did not survive
the adversarial verify pass, and is not included above); performance 3 raw
/ 3 confirmed. 20 raw, 19 confirmed overall - the 19 "Confirmed findings"
above.
