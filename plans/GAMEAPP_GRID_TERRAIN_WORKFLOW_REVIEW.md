# GameApp, Grid, and Terrain: Workflow Review and Fix Plan

Date: 2026-09-05. Status: review complete; economy fixes and initial session coordination implemented; broader backlog open.

This began as a review of the current working tree, not a claim that all addon features were audited
or that the campaign plan is implemented. The implementation log below records subsequent fixes.
The original runtime reproductions are retained in `tests/workflow_review_probe.gd`.

### Implementation Log

First corrective stage, 2026-09-05:
- W01 fixed: grid work reads the authoritative day divisor at each clock advance.
- W02 fixed: remaining job work is serialized, clamped and resumed by workers. Worker removal
  releases its claim; inactive workers no longer advance through WorkTick callbacks.
- W03 fixed: deferred-input production debits before output, waits when inputs are missing,
  saves its input-commitment fact, and refunds paid inputs at most once when requested.
- W04 fixed: construction detaches its pending callback before consuming materials; storage
  commits the complete debit before notifications. Reentrant/repeated site registration is rejected.
- Shared wallet/storage costs aggregate duplicate resource IDs, reject overflowing totals and
  insufficient combined quantities, and publish after committing all debits.
- Tests extended for real worker resumption/removal, inactive work, duplicate costs, multiple
  material types, production restoration/cancellation, and reentrant production completion.
- Verification: build passed (0 warnings/errors); workflow_review_probe, rts_lifecycle_probe and
  existing runtime_smoke passed. Clock-axis probe passed its assertions, with missing-localization
  fixture warnings. No visual/export test or game-copy synchronization is claimed.

Session coordination stage, 2026-09-05:
- MainMenu New Game now resets run score, clock, checkpoint, save slot and pending restore through
  StartNewSession; lifetime progression remains intact. Missing entry scenes are checked first.
- GameApp-owned clocks start disabled. Menu/scene suspension and loading stop automatic time and
  EndTurn. Explicit clock Advance remains available to tools; this is not a universal command gate.
- Save and autosave require an active, ready session outside pending/in-flight restoration.
- MainGame, LevelLoader and save-participating TerrainWorld register loading work before deferring
  startup. BeginSession waits for registered loads, then restores, then releases startup callbacks.
- Matching saved terrain skips the fresh generation pass. Initial worker spawning and production
  wait for readiness; restored production does not perform a fresh AutoStart.
- Session generations invalidate queued restoration/readiness when starting another run or leaving
  the scene. Unregistered late load completions are ignored. Direct Load also stops the app clock.
- Verification: dotnet build passed with 0 warnings/errors; session_lifecycle_probe,
  workflow_review_probe, rts_lifecycle_probe, game_clock_axes_probe and runtime_smoke passed.
  Session tests cover blocked saves/time, stale restoration and completion, disabled saves,
  failed loading, and nested level/terrain restoration with exactly one world build.
  Clock/session fixtures still report a missing localization CSV warning.

Save discovery stage, 2026-09-05:
- SaveableHelper now respects its supplied subtree instead of returning every grouped node in
  the SceneTree. It excludes nodes beneath queued-for-deletion ancestors.
- GameStateManager scopes discovery and restoration to CurrentScene plus GameApp. SaveRootPath
  selects an explicit gameplay subtree; an invalid explicit path fails instead of widening scope.
  Headless hosts with no CurrentScene retain root discovery and can select an explicit scope.
- save_scope_probe verifies sibling exclusion on save and restore, pending deletion, retained
  GameApp state, and explicit scope selection. All six save/session/economy/clock/smoke probes pass;
  build has zero warnings/errors. Existing fixture localization warnings remain.
- F05 is not closed: snapshots still reuse the state container, so stale records from earlier
  snapshots require fresh snapshot construction. Stable IDs, duplicate key validation and entity
  reconstruction remain separate prerequisites. This change does not classify previews inside
  the chosen subtree; those must opt out of save participation or live outside the save scope.

Fresh snapshot stage, 2026-09-05 (supersedes stale-container limitation above):
- Each capture creates a new GameStateData. Component feature/world records are rebuilt from
  current participants, so deleted components and previous-level keys do not accumulate.
- SetGameData/GetGameData now own CustomData, serialized as custom_data independently of
  component GameData. Explicit choices and save metadata survive rebuilding; component records
  must be authored each capture. No old mixed-bag migration has been added.
- Capture publishes only after every participant succeeds and the session remains valid.
  Nested capture is refused; invalid scope/exception returns false without writing the save.
- Tests prove deleted-record removal, nested custom-data JSON round-trip, and unchanged file
  contents on rejected capture. Build passes with zero warnings/errors; all six probes pass.
  Expected negative-test scope warning and existing localization fixture warnings remain.
- Still pending: duplicate-key detection, stable entity manifests/recreation, complete effective
  terrain recipes, and disk-write rollback of in-memory save metadata. This is not full F05 closure.

These are targeted corrections, not completion of F01-F17. Remaining lifecycle work includes
transactional rollback on scene failure, unified Retry/Continue routes, asynchronous loader
cancellation/owner removal, and a gameplay-command gate beyond clock-driven simulation. Custom
GameApp scenes must request BeginSession; standalone scenes without GameApp remain supported.
Load owners must complete their registrations; retry a failed session through a new session.
Scoped saves, entity reconstruction, persistent construction associations, remaining production
time across multiple cycles, and campaign/demo work remain pending. Worker startup is delayed,
not an entity reconstruction implementation. No visual/export/game-copy synchronization claim.

This expands `C:/Users/f_ald/.claude/plans/quiet-weaving-bonbon.md` beyond campaign routing.
Use this document for cross-system defects and acceptance gates; retain that plan's campaign
catalog, required-objective, scene-local recipe, and mission deliverables. Do not restart the
obsolete PainterlyTerrain rewrite. Preserve the current terrain generation algorithms.

## 1. Ownership Today

| Owner | Actual responsibility | Integration boundary |
|---|---|---|
| GameInfo | Authored genre, scene paths, tuning | BeepGenreScene mutates the active resource and reapplies clock tuning |
| GameApp | Owns Clock, Settings, Locale, Saves; session and progression | Does not currently coordinate map readiness or transactional transitions |
| Boot / MainMenu / SceneNav | Entry, menu actions, screen changes | New game, continue, retry, and return do not share a session operation |
| MainGame | Persistent world/entity/HUD roots; filename-based level instantiation | Duplicates LevelLoader; always instantiates an avatar |
| LevelLoader | Instantiates one scene from an inline list | Its level state is separate from GameApp's level state |
| GameFlow | Score/lives and terminal signals | Calls BeginSession before deferred content load; results screens separately advance progression |
| TerrainWorld | Applies axes; generates or regenerates a field; rebuilds selected renderers | WorldBuilt means generation/drawing happened, not that entities, saves, and navigation are ready |
| TerrainGenerator / FieldBuilder | Cached deterministic field and staged land/water/climate/resource/start generation | GenerateTerrain copies initial terrain kinds into live grid cells |
| GridCellData / GridWorldState | Mutable cell state; snapshot of roads, occupancy, objects, selection, jobs | Object restoration finds existing paths; it does not recreate missing placed scenes |
| GridNavigation / PathFollower | Cell-space A*; movement in projected coordinates | Bounds and projection are separately authored; existing paths do not track map edits |
| WorkClock / workers / production | Converts game beats to work turns; executes jobs and recipes | Fallback frame ticking exists; startup, reconfiguration, and deactivation are not uniform |
| BuildSite / storage / transport | Material gating, job-to-building mapping, physical cargo | Several transient relationships are absent from saves |
| Objective tracker / binder | Cumulative progress and event connections | No required-objective aggregate; production binding is initially scanned |

Keep these ownership boundaries. Do not centralize every gameplay operation in GameApp or create
a second grid/terrain store. GameApp should coordinate lifecycle, not implement pathfinding,
terrain rendering, recipe arithmetic, or UI layout.

## 2. Workflow Matrix

| Workflow | Current execution | Required end state / affected findings |
|---|---|---|
| Boot -> menu -> fresh game | GameApp builds clock/saves; menu changes scene; GameFlow begins session; MainGame defers content | Explicit new-session reset and loading barrier; no menu autosave or simulation; F01-F04 |
| Direct F6 authored scene | Components ready independently; optional clocks self-tick; TerrainWorld defers generation | Works without shell/autoload; explicit authored-vs-generated startup policy; F04, F07, F12 |
| Generate / reroll in lab | TerrainWorld configures generator, fills cells, draws, signals synchronously | Cancellable generation operation, actual stage timings, no accidental gameplay-state retention; F09-F12 |
| Continue / load while paused | Save parsed; GameApp restored early; scene changes; BeginSession queues all saveables | Restore once after entity creation, no signals causing gameplay mid-restore, preserve prior scene on failure; F03-F06 |
| New game after an old run | MainMenu changes scene without resetting existing manager state | Fresh session, clock, checkpoint, map state; preserve only declared profile progression; F01 |
| Place -> deliver -> build | Placement emits; BuildSite marks incomplete; storage gates job; worker completes it | Validate before debit, consume materials once, reserve reachable approach, enable production only on completion; F07-F08, F14 |
| Extract -> haul -> store -> produce | Transport manager offers jobs; storage signals update sites; production uses wallet | Declared wallet economy versus physical logistics; capacity/backpressure and conservation; F08, E03 |
| Clear/build while units move | Navigation searches current cells; follower retains world-space waypoints | Revalidate affected path; arrival is not cancellation; deterministic unreachable-job retry; F13-F14 |
| Pause / speed / end turn / retune genre | Tree pause; clock beats; work clock cached conversion; travel stays frame-driven | Explicit simulation vs cosmetic pacing; consistent conversion after retune; F02, F15 |
| Save in construction / hauling / production | Keyed component saves + GridWorldState object paths | Stable entities, in-flight commitments, remaining work, construction relationships round-trip; F05-F08 |
| Victory -> results / next / menu | GameFlow terminal gate; results Continue increments level | Completion committed once before results; catalog-bounded next or campaign finish; F16 |
| Map swap / multiple previews / projection switch | Global scene searches; deferred old level free; renderer-specific geometry | Scoped references and snapshots; detach outgoing level; one coordinate contract; F04, F10, F17 |

## 3. Confirmed Runtime Reproductions

Run from the repository root:

```powershell
$env:GODOT_MCP_BRIDGE_AUTO_CONNECT_RUNTIME='false'
dotnet build Beep.Godot.csproj --no-restore
godot --headless --path . --script res://tests/workflow_review_probe.gd
```

The review probe originally exited **1** with the four observations below. It now exits **0**;
the original assertions remain and additional regression cases cover the corrective stage.

| Probe | Expected | Observed Before Fix |
|---|---|---|
| W01 | Bind at 10 beats/day, configure 100, advance 100: one work turn | Ten work turns |
| W02 | Save/load a ten-turn job with three remaining: 70% complete | 0% complete |
| W03 | ConsumeInputsOnStart=false, zero wood, one plank recipe: no free output | One plank produced without inputs |
| W04 | Site requires five wood, storage contains fifteen: consume five, create once | Consumed fifteen; three BuildSiteCreated events |

## 4. Findings and Corrections

Source paths below are relative to `addons/beep_game_builder_cs/`. Line anchors refer to this
reviewed working tree. S = confirmed source-level behavior; R = runtime reproduced above.

### F01 [P1, S] Fresh-game ownership is incomplete

Evidence: `ecs/scenes/MainMenu.cs:60` only navigates. `ecs/GameStateManagerComponent.cs:261`
creates fresh state only when `_currentState == null`. `ecs/GameApp.cs:328` does not reset its clock
or checkpoint, and the menu does not call it. A second run in the same process can inherit state.

Fix: one explicit StartNewSession operation, distinct from Continue and Retry. Reset run state,
clock, checkpoint and pending restore; keep profile progression only by explicit policy. Do not
infer fresh versus restored from whether a dictionary happens to exist.
Gate: run A -> menu -> new B has zero run time/score, no A checkpoint/jobs/cargo or pending load.

### F02 [P1, S/R-W01] Clock and work conversion drift

Evidence: `ecs/time/GameClock.cs:118` can reconfigure BeatsPerDay; `_Process` advances regardless
of IsGameRunning. `ecs/grid/GridWorkClockComponent.cs:144` caches the divisor once and
`OnClockAdvanced` uses it. BeepGenreScene explicitly supports retuning at runtime.

Fix: session coordinator enables simulation only when ready; publish configuration changes or
read the authoritative divisor at the beat boundary. Reset the clock for new runs, restore for
loads. Make any mid-run axis change explicit; do not silently reinterpret outstanding durations.
Gate: W01, menu dwell leaves simulation time unchanged, pause/restore emits no production tick.

### F03 [P1, S] Autosaves can replace gameplay saves from non-game scenes

Evidence: `ecs/GameStateManagerComponent.cs`, `_Process` checks IsActive/AutosaveEnabled only;
SaveAutosave creates state on demand. Save/SaveAutosave do not enforce the disabled-save setting
or readiness. Metadata uses CurrentScene, which can be the menu/shell instead of the active map.

Fix: manual and automatic writes share a readiness/permission gate; save profile separately from
the active run; successful writes alone announce success. Do not advance autosave while loading.
Gate: menu idle beyond interval, loading, failed map load, disabled saves: no gameplay file changes.

### F04 [P1, S] Startup and scene replacement have no completion barrier

Evidence: GameFlow._Ready calls BeginSession; MainGame._Ready separately defers StartGame.
`ecs/MainGameComponent.cs:149` and `ecs/LevelLoaderComponent.cs:78` QueueFree without detach.
Scene-wide lookup can still discover the outgoing world while the incoming one is added.

Fix: single loader with operation identity: resolve -> instantiate disabled -> generate/restore ->
rebind/validate -> activate. Detach old level before binding new systems; keep failure recovery
explicit. WorldBuilt is an intermediate event, not gameplay-ready. Reject stale deferred work.
Gate: observe ordering with a signal trace, both sibling orders, nested level load, double-click,
missing content, failure after generation, immediate switch during delayed victory.

### F05 [P1, S] Saves lack scoped identity and dynamic entity reconstruction

Evidence: `ecs/GameStateManagerComponent.cs:292` reuses GameData without removing outgoing-map
keys. Default hauler/production/storage keys collide. `ecs/grid/GridWorldStateComponent.cs:201`
restores only existing GridObject paths, skipping missing nodes. Saved occupancy may outlive the
missing building and leave invisible blockers.

Fix: stable scene-authored/spawned entity IDs; active-level snapshot separate from session/profile;
entity manifest with scene identity and state. Validate duplicate IDs/keys before writing; recreate
entities before resolving job/storage references. Remove deleted entities deterministically.
Gate: two identical factories, two haulers, new building, demolished building, A -> B -> save,
catalog reorder, transformed/reparented instances, reload into a new process.

### F06 [P1, R-W02] Job work progress is not serialized or resumed

Evidence: `ecs/grid/GridJobQueueComponent.cs:302` and GridJob.ToDictionary omit RemainingTurns;
GridWorker.StartWorkOrFail starts from GetJobWorkTurns, not remaining work. Requeue avoids orphan
claims but restarts completed effort. Worker._ExitTree does not release a claim while the queue survives.

Fix: queue owns total and remaining work; serialize both. Restore stable worker association or
requeue while retaining remaining work. Worker destruction releases its claim. Authorize progress
updates against the assigned executor instead of accepting unrelated writers.
Gate: W02; reload at 70%, destroy a working worker, reassign, then require exactly three more turns.

### F07 [P1, S] Construction relationships and activation are incomplete

Evidence: BuildSite's `_sitesByJobId` and `_pendingByPlaced` are memory-only; the component is not
ISaveable. RegisterPlacedBuild changes visuals/Complete before checking material storage. A missing
storage leaves an incomplete site with no runnable job. Production._Ready AutoStart ignores Complete.

Fix: persist site phase, entity ID, material commitment, job ID and approach; restore bindings after
entities. Validate dependencies before confirming/debiting placement. Gate production/extraction
on construction completion rather than merely visibility. Define material refund policy explicitly.
Gate: save/reload awaiting materials, halfway built, completed, cancelled; invalid site rolls back
cost and footprint; no output before completion; cancel consumes/refunds each resource at most once.

### F08 [P1, R-W03/W04] Economy mutations are not transactional

Evidence: `ecs/grid/GridBuildSiteComponent.cs:203` unloads while StorageChanged remains connected
and pending state exists. Recursive callbacks consume excess materials and emit repeated creation.
`ecs/grid/GridProductionComponent.cs:139` conditionally debits on start, but CompleteProduction
never debits when ConsumeInputsOnStart=false.

Fix: validate and aggregate material requirements, claim/remove pending transition before any
observable mutation, consume exactly once, then publish. Deferred-input recipes reserve or debit
atomically at completion and wait/reject when insufficient. Persist whether inputs were committed.
Gate: W03/W04 plus multiple input types, duplicate material entries, exact/excess/partial inventory,
signal callbacks that inspect/request the same operation, and reload before completion.

### F09 [P1, S] Saved terrain recipe is insufficient to reproduce arbitrary configured worlds

Evidence: `ecs/terrain/TerrainWorldComponent.cs:334` reads nine axes but does not validate its stored
version. Generator.CurrentSettings also depends on Frequency, Octaves, erosion, beaches, topology,
origin, custom resource catalog and other settings not persisted by TerrainWorld. Those can change
while the saved axes stay identical. Saved-size warning cannot detect same-size content changes.

Fix: persist the complete effective generation input or a versioned immutable recipe with all
remaining settings and catalog revision. Validate version/enums/bounds before mutating the world.
No legacy migration is required in development: reject incompatible saves explicitly. Test field
fingerprints, not only dimensions. Keep generator tuning outside the recipe only if proven visual-only.
Gate: nondefault noise/catalog/origin, reload after scene defaults change, same dimensions with
different content, unsupported version, missing recipe, and a non-saving preview during a load.

### F10 [P2, S] Projection and origin contracts diverge

Evidence: `ecs/terrain/TerrainWorldComponent.Drawing.cs:175` / `:203` handle Isometric specially
but fall back to painted TileSize or 64 for Tiles and IsometricAutotile. Draw knows the selected
flat size but does not propagate grid/navigation bounds or every renderer origin.

Fix: selected projection provides CellToView/ViewToCell/extent; propagate origin, bounds and cell
size once to grid, placement, navigation, selection and camera. Include isometric autotile geometry;
test non-square tiles and transformed parent nodes. Do not duplicate projection math in HUD scripts.
Gate: select/build/path/camera frame the same cell in all four projections at nonzero origin.

### F11 [P2, S] Generated visual terrain and live edits are disconnected

Evidence: TerrainTileRenderer.CreateLayer feeds TerrainGeneratorPath, not live cells. Its
ConfiguredLayers uses generated kinds. TerrainWorld.RestoreWorld redraws the original generated
field while preserving edited cells. The renderer has no live cell-change subscription.

Fix: distinguish immutable base field from authoritative live terrain edits. Render final visible
terrain from base plus edits; update touched dual-grid neighbors and coastline data where needed.
Keep data layers explicitly named as generated climate/resource data if they are intentionally immutable.
Gate: edit grass to mud/water, save/load, switch projection: visible kind and path/build rules agree.

### F12 [P2, S] Generation ownership and progress reporting are ambiguous

Evidence: generator GenerateOnReady, world BuildOnReady, and renderer RefreshOnReady can all defer
their own work. TerrainWorld performs generation and drawing synchronously; its success signal
does not include failure. FieldBuilder's sample cap still bottoms out at two samples per cell and
does not enforce a maximum input map area.

Fix: one trigger per composed world. Explicit authored, generated-new, restored, and preview modes.
Stage timings separate field work from tile/GPU commit; cooperative cancellation and request IDs.
Bound map allocation before multiplication. Snapshot data before background CPU work; do not run
Node/TileMap/resource mutations indiscriminately in Task.Run. Preserve authored edits on F6.
Gate: one generation per start, cancel/regenerate, invalid giant bounds, no stale commit, progress
indicator renders before work and responds during large-map generation. Record p50/p95 timings.

### F13 [P2, S] Navigation's heuristic floor can overestimate cheap paths

Evidence: `ecs/grid/GridNavigationComponent.cs:170` clamps the combined road/terrain minimum to
0.05. StepCost multiplies individually permitted 0.05 costs, allowing a 0.0025 step. The heuristic
can therefore exceed the true cost; this implementation closes nodes without reopening them.

Fix: use a proven admissible lower bound or zero heuristic; compare weighted routes against a
Dijkstra oracle. Evaluate AStarGrid2D only after checking dynamic costs and blocking requirements;
do not replace working domain logic purely to use a different class.
Gate: randomized terrain/road multipliers including both minima, diagonal policies and blockers;
path cost equals the oracle. This review proves the invalid bound, not a measured failing route.

### F14 [P2, S] Worker approach, cancellation and path invalidation are underspecified

Evidence: BuildSite.ApproachCellFor chooses only one front cell without reachability checks;
GridWorker releases no-path jobs to the same priority/nearest queue; PathFollower does not recheck
blocked cells after path creation. Worker detects movement stopping, not DestinationReached,
before starting work. Cancelling movement can therefore be interpreted as arrival.

Fix: select a reachable footprint perimeter cell; distinguish arrival/cancel/failure signals;
repath on relevant grid revisions; back off unreachable jobs until topology changes. Preserve
work on reassignment. Clamp sorting depth or use YSort instead of unbounded world-Y ZIndex.
Gate: build at border, obstruct front only, block route mid-travel, cancel movement, remove worker,
and an unreachable high-priority job beside an available reachable job.

### F15 [P2, S] Deactivation and timing differ between execution paths

Evidence: GridWorker._Process checks IsActive, but WorkTick -> AdvanceWork does not. Production
AdvanceWork finishes only one cycle and discards excess turns, so one large step and many smaller
steps yield different outputs. PathFollower movement is fixed wall-time rather than clock-scaled.

Fix: central execution eligibility check used by signals and manual stepping. Decide whether travel
is cosmetic between turns or simulation work; expose that deliberately. Carry excess production
time through bounded cycles where allowed, and define backpressure/retry instead of silently stalling.
Gate: inactive worker receives WorkTick; speed/pause tests; split-step equivalence with sufficient
inputs; deterministic turn ordering. Do not advertise lockstep multiplayer without separate tests.

### F16 [P2, S] Completion depends on a results button

Evidence: `ecs/scenes/LevelSummary.cs`, OnContinue calls CompleteLevel and increments without a
catalog end check. Menu does not commit completion. Other result screens have their own logic.
GameApp.RecordGameEnd has no idempotence guard; no caller was found in the inspected scene/UI paths.

Fix: lifecycle coordinator commits terminal outcome once, including progression/statistics, before
results. Results only present it and request Next/Menu. Bound next by stable catalog identity;
support no-results route and final campaign completion. Preserve AutoNavigateOnEnd=false.
Gate: win -> menu retains completion; double Continue; loss cannot advance; final level ends cleanly.

### F17 [P2, S] Scene-wide resolution defeats isolated worlds

Evidence: EntityComponent.Resolve returns any still-valid cached object before considering a
changed path; empty paths search CurrentScene. Spawner adds a unit before configuring paths,
allowing its _Ready to cache another world's collaborators. GridClockPorts searches root children
and grandchildren, which can include a shallow preview despite its comment saying otherwise.

Fix: resolve within explicit world scope; configure before activation; invalidate on path/scope
change. Diagnose ambiguity instead of picking the first match. Define required versus optional
dependencies; missing a wallet must not accidentally enable free construction in economic mode.
Gate: two worlds + preview, different wallets/queues, path retarget, old world detached but valid,
and spawned scene whose default references would otherwise find the first world's systems.

## 5. RTS / Settlers Enhancements (Not Existing Bug Claims)

- E01: playable starts need a headquarters footprint, clear exits, and reachable essential resources.
  TerrainStartPositionStage scores nearby terrain and separation; it does not validate the actual
  building footprint/navigation graph or every resource catalog's needs. Report an unusable seed,
  never silently reroll it. Add per-scenario start requirements.
- E02: scenario capabilities, not a genre string, choose avatar/camera, turn or real-time driver,
  physical logistics, combat, and construction requirements. Keep scene-authored defaults readable.
- E03: separate abstract wallet mode from physical-storage mode. Physical transport should queue
  demand, reserve cargo/destination capacity, retain unassigned output, and expose blocked reasons.
  Fastest-first transport dispatch is an extensible policy, not a complete logistics simulation.
- E04: one read-only world diagnostic view: lifecycle stage, seed/effective recipe, entity count,
  queued/blocked work, in-transit stock, generation/commit timings and failed bindings.
- E05: design-time Kit controls bound to real state. Build menu, job list, resource bar, objectives,
  loading/cancel UI and pause overlay should not recreate their controls from runtime code.
- E06: save thumbnails and human-facing summaries are optional polish after round-trip integrity;
  do not prioritize appearance over missing buildings, material loss, or stuck workers.

Research informs these proposals, not the defect evidence:
- [Widelands](https://github.com/widelands/widelands) identifies its Settlers II inspiration.
  Use a small playable settlement logistics loop as the demo target, not a corporate dashboard.
- [Factorio generation notes](https://www.factorio.com/blog/post/fff-258) motivate validating
  starting resources against water instead of judging starts from appearance alone.
- [Godot Node lifecycle](https://docs.godotengine.org/en/stable/classes/class_node.html) explains
  child-before-parent readiness; a deferred call alone is not a multi-stage loading protocol.
- [Godot thread safety](https://docs.godotengine.org/en/stable/tutorials/performance/thread_safe_apis.html)
  limits background work: keep active scene-tree changes on the main thread.

## 6. Implementation Order and Exit Gates

1. **Economy correctness (F06-F08).** Turn W02-W04 green. Atomic material commitment, deferred
   input debit, remaining job work. Add cancellation, duplicate-input, and signal-reentry cases.
2. **Session and time (F01-F04, F15).** One StartNew/Continue/Retry/End operation, readiness gate,
   clock conversion update (W01), save permission gate, no simulation during restore. Retain
   standalone scenes without forcing them to construct GameApp.
3. **Stable world persistence (F05-F09).** Entity identities/manifests, scoped snapshots,
   construction associations, effective recipe validation. Fresh-process save/load must conserve
   all stock and remaining work before adding campaign breadth.
4. **World integration (F10-F14, F17).** Coordinate/bounds contract, live-edit rendering,
   path correctness/invalidation and reachable starts. Test every projection with one seed and
   identical cell edits. Preserve current coast generation, textures and native Godot layers.
5. **Campaign routing (F16 + campaign plan).** One loader/catalog, required/optional/prerequisite
   goals, dynamic bindings, final-level handling. Commit outcome independent of results UI.
6. **Playable proofs (E01-E05).** Two strategy missions and two citybuilder missions from the
   campaign plan: headquarters -> worker -> gather -> delivery -> construction -> production ->
   objective -> next mission. At least one authored map and one procedural map. Include reload
   mid-job and no-results navigation fixture. UI nodes must be visible in the editor.
7. **Performance and export.** Measure cold/warm small/standard/large maps separately, field vs
   render commit, allocations and peak memory. Agree measured responsiveness budgets; do not invent
   achieved timings. Run exported build without development asset paths and verify save round-trip.

Every phase includes negative cases and a clear failure message. Green source-string checks alone
do not close a phase. No backward compatibility layer is required; update current scenes/callers
atomically and explicitly reject incompatible saves rather than guessing defaults.

## 7. Review Coverage and Limits

Read complete during this workflow review (under `addons/beep_game_builder_cs/ecs/`):
- GameApp.cs, GameStateManagerComponent.cs, MainGameComponent.cs, LevelLoaderComponent.cs,
  BeepGenreScene.cs, BootComponent.cs, EntityComponent.cs, NavigationComponent.cs.
- time/GameClock.cs; grid/GridClockPorts.cs, GridWorkClockBinding.cs, GridWorkClockComponent.cs.
- grid/GridWorldStateComponent.cs, GridNavigationComponent.cs, GridPathFollowerComponent.cs,
  GridWorkerComponent.cs, GridWorkerSpawnerComponent.cs, GridJobQueueComponent.cs.
- grid/GridBuildSiteComponent.cs, GridBuildCatalogComponent.cs, GridProductionComponent.cs,
  GridStorageComponent.cs, GridTransportManagerComponent.cs.
- terrain/TerrainWorldComponent.cs, TerrainWorldComponent.Drawing.cs, TerrainGeneratorComponent.cs,
  TerrainFieldBuilder.cs, TerrainStartPositionStage.cs, TerrainTileRendererComponent.cs.
- scenes/MainMenu.cs, scenes/LevelSummary.cs, ui/SceneNav.cs.

GameFlow, objective tracker/definition/binder and hauler were reviewed/tested in the preceding
implementation stage. Their fixed terminal/cargo/nonpositive-progress behavior is not presented
as still broken here. The session-stage log above supersedes the original readiness findings;
full gameplay-command suppression and stable entity reconstruction remain incomplete.

Targeted inspection only: build/recipe definition readers, data-layer integration, result-screen
call sites. This is not a complete audit of every placement rule, extraction implementation,
shader, UI control, combat/network system, generator stage, or game-specific Oilfield Days code.
Those are not prerequisites to this workflow review; implementation must inspect each touched
consumer before changing a shared contract.

Review evidence: the initial Godot headless probe reproduced W01-W04 with exit code 1 and no
script exceptions. Subsequent verification is recorded in the implementation log above; the
existing runtime smoke suite alone did not cover these cases. No visual/export/performance claim
is made here.

Review completion checklist:
- [x] Trace GameApp, grid, terrain and adjacent scene/economy workflows from source.
- [x] Separate ownership, verified defects, behavioral reproductions and enhancement proposals.
- [x] Record source anchors, concrete impact, correction and acceptance tests.
- [x] Define dependency order and playable cross-workflow exit gates.
- [x] State inspection boundaries and outstanding verification honestly.
- [ ] Implement the backlog and run every phase gate (separate work from this review).
