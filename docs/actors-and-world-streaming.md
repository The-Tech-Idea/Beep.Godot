# Actors and World Streaming

## Delivery Scope

The deliverable is reusable building blocks in the addon, not playable genre
demos. Genre support means composable ownership, control, movement, jobs,
combat and world-data APIs. An RTS or colony player can own many actors without
having a player-character scene. Automated probes validate these contracts;
new demo scenes are not an implementation requirement. Performance optimization
and benchmarking are deferred until functional integration is complete.

### Dormant Actor Travel

`GridActorTravelComponent` uses the existing navigation requests, grid projection,
actor registry and work clock. Configure their node paths and call
`BeginTravel(actorId, goalCell, worldUnitsPerTurn)` for a dormant actor.
Movement changes the authoritative position and spatial index without loading
an actor scene. `TravelFinished` reports arrival or failure. Navigation validates
each step; a blocked route stops instead of crossing newly blocked terrain.
Only one travel service can own an actor's movement. Loaded route chunks are
pinned when navigation uses streamed terrain data.

Capture the registry and travel state together, restoring the registry first.
Travel restore replans from the saved position; pending path time does not count
as movement. Waking/deleting an actor cancels its dormant route. Removing the
travel service cancels its routes; capture state before removing it if needed.
Automatic worker dispatch and travel-to-job chaining are provided by the
component below. Hauling and scene-independent
combat remain unfinished; this is not yet the full simulation roadmap.

### Dormant Worker Dispatch

`GridWorkerDispatchComponent` composes the registry, grid, job queue,
`GridActorTravelComponent`, `GridJobExecutionComponent` and world clock through
explicit node paths. These services must reference the same registry and grid.
Call `RegisterWorker(actorId, worldUnitsPerTurn, workSpeed)` to opt in an owned
actor; no player-character scene is required. Registering a loaded actor does
not take control of its commands. Dispatch begins only while that actor is
dormant. `AllowedJobKinds` filters the queue's existing priority/distance
selection; it does not introduce another job queue or reservation system.

Each positive clock tick reconciles assignments and claims available work for
idle registered actors. `DispatchJobs()` also permits explicit dispatch.
The actor travels to the reserved approach cell before starting timed work.
Completed workers can claim subsequent jobs. Failed travel releases its claim;
waking while travelling cancels the route and releases the job. World-owned
work already started can continue after wake through the existing worker binding.
Unregistering, deleting the actor or removing the dispatcher releases its
assignments, cancelling travel or preserving partial work before releasing a job.
`AssignmentFinished` reports completion or failure; `GetWorkerJob` and the
worker/assignment counts expose current state without per-actor scene nodes.

`CaptureState`/`RestoreState` and `ISaveable` persist registrations, movement/work
speeds, current assignment phase, allowed job kinds and retry delays. Pause clock
advancement during a checkpoint restore. Restore the registry, queue with
`RequeueClaimedJobsOnLoad=false`, travel, execution, then dispatcher. The
dispatcher reconnects to those authoritative service records instead of copying
position or remaining work. Invalid or conflicting state is rejected before
replacing metadata. A live assignment cannot be silently dropped by restoring
an idle or empty dispatcher snapshot; restore its dependencies first or explicitly
unregister it. Save keys must be unique when a world contains multiple dispatchers.
Removing a dispatcher still clears registrations and releases assignments; a
fresh dispatcher can reconnect after the saved dependencies have been restored.

Failed jobs receive a per-worker `FailedJobRetryTurns` delay (default five world
turns). The queue excludes these jobs while retaining its existing kind,
priority and distance rules for other work. Retry expiry uses absolute world-clock
time, so a failure during a large tick does not immediately consume its new
delay. Checkpoints store remaining delay, allowing restoration against the
restored clock. This is a retry policy, not a permanent unreachable-job decision.

Verified with `tests/worker_dispatch_probe.gd`: consecutive unloaded jobs,
kind filtering, competing dispatchers, no scene instantiation, route rejection,
wake cancellation, unregister during travel/work, permanent deletion, fresh
travel restore, working restore, invalid/conflicting restore, failed-job
starvation prevention and retry-delay expiry/persistence.

## Implementation Status

The player/NPC and huge-world plan is in progress. The actor foundation and
opt-in ambient scene residency are implemented. This is not completion of the
full cross-genre and endless-world roadmap.

The shared GridWorkClockComponent now provides ScheduleWork(owner, turns, method)
and CancelScheduledWork(id). The named owner method receives request ID and actual elapsed turns
once its deadline is due. A priority queue avoids calling future tasks on each
tick; equal deadlines use submission order. ScheduledWorkPerTick (default 256)
bounds heap removals per tick, including cancelled entries; delayed work remains
queued and receives its full elapsed duration when dispatched. Owners leaving
the tree cancel their tasks through an owner index, not a full task scan.
Reentrant advancement cannot recursively dispatch tasks added by a callback.
Clock exit clears tasks; reattachment reconnects clock lifecycle handlers.

ScheduledWorkBudgetMilliseconds (default 2 ms) also limits elapsed dispatch time.
The monotonic timer is checked between heap entries, admitting at least one entry
per dispatch to make progress. A callback already executing is never interrupted:
its transaction may exceed the budget. LastWorkDispatchMilliseconds and
LastWorkDispatchCount expose the measured time and actual callback count from the
last dispatch. Cancelled/invalid entries consume the count/time budgets but do
not increment the callback count. This budget covers scheduled callbacks only,
not ordinary WorkTick listeners, queue submission/cancellation or the whole frame.

Set ScheduledWorkBudgetMilliseconds to 0 for count-only deterministic batch or
lockstep execution; a wall-time admission budget is hardware-dependent. Deferred
callbacks keep their original start time and receive all elapsed turns. This is
not a guarantee of deterministic multiplayer simulation or a hard frame-time cap.

Verification: the deadline probe checks overshoot visibility and deferred elapsed
time using a deliberately slow callback. The production probe completes 1,000
node-free, non-looping records with the 2 ms budget enabled and exact input/output
totals (15 dispatches on the recorded run; the count depends on hardware/load).
Build and all 92 headless checks pass after this scheduler change.

This is a scheduling foundation, not completed offscreen simulation. Production
now uses this queue when bound to a work clock; other WorkTick subscribers are
unchanged and do not automatically use it.
Runtime callback registrations are not saved: authoritative economic/actor task
records must rebuild their deadlines on restore. Detached actor records and other
economic consumers still need integration.

Production deadline integration is verified separately by
production_scheduling_probe: 1,000 factories keep frame processing disabled,
wait for their deadlines and finish due work within the shared dispatch-count
limit. RemainingTurns/Progress01 read elapsed clock time without per-tick
production callbacks. Pause and detach retain partial progress; restore and
producer/clock reattachment rebuild deadlines. Large advances use the bounded
production catch-up path; cancellation removes the deadline. With no authored
or discovered work clock, the existing standalone delta fallback remains.
Clock-driven callbacks run after WorkTick listeners and before standalone
calendar advancement. Each dispatched factory can complete multiple overdue
cycles before the shared cooperative time budget is checked again. This is not
a strict frame-time budget or moving-actor performance qualification.

GridProductionProcess now owns cycle state, input commitments, outputs and
catch-up independently of a scene node. GridProductionComponent is the authored
scene adapter; GridProductionSimulationComponent owns keyed production records
using the same process, recipes, shared wallet and work clock. The simulation
service creates no per-factory nodes and does not process each frame. It exposes
StartProduction(id, recipe), PauseProduction(id), ResumeProduction(id),
RemoveProduction(id, refundInputs), GetProduction(id), CaptureState and
RestoreState. The service must remain in the world, with a valid work clock and
wallet; removing it suspends its records until reattachment. Visual scenes are
not required. One service uses one wallet, so author distinct services for
economies that must not share resources.

Snapshots retain per-record loop/payment settings, committed inputs, remaining
turns and overdue work. Restore validates records before replacing the current
set, then rebuilds deadlines. Save and restore the wallet alongside production
state; production restoration does not itself restore resource balances. Record
IDs are caller supplied or supplied by a bound actor. Ownership transfer, worker
jobs, hauling and combat are not implemented by this service.
Executing dictionary recipes now reads values without manufacturing temporary
Godot Resources. The initial 1,000-record test exposed a shutdown failure from
those temporary resources; the corrected run exits cleanly with no per-factory
nodes. Build and all 91 headless checks passed after the shared runtime refactor.

### Binding Economic Scenes

The authored `templates/scenes/actors/economy_lab.tscn` demo exercises three
player-owned producers without a possessed avatar. It uses the existing UI kit
and native scene nodes. Select a mill to pause/resume it, unload/wake its scene,
or permanently remove it. Shared stock continues to change while a scene is
unloaded. Capture/restore stores the wallet, registry and production records
together in memory; this demo is not a disk-save implementation. Advance adds
ten world turns. The preview uses placeholder building icons, not final art.

`tests/economy_lab_probe.gd` exercises the authored buttons, shared stock, residency,
pause/resume, deletion and snapshot restoration. Native OpenGL verification passes;
1280x800 and 900x680 captures were inspected under `tests/output/economy_lab`.
The headless and native runner lists now include this scene (93 headless checks
listed; the preceding full-suite checkpoint remains 92 until rerun).

Set GridProductionComponent.SimulationPath to the world's
GridProductionSimulationComponent. ProductionId selects the persistent record;
when empty it uses the containing actor's stable ActorId. The service owns the
recipe catalog and wallet. The existing production component reads current
record state and forwards start, pause, resume, cancel, completion and explicit
advance commands, so its existing UI panel remains usable. The view never
schedules a second timer or starts a local fallback if the service is missing.
Treat this binding as authored scene configuration; changing its path or ID on
an already running view does not rebind it until reattachment.

Bound views do not independently participate in saves. Actor snapshots therefore
cannot restore an old production timer over the current world record on wake.
Save the world simulation and wallet together. With the existing opt-in residency
policy and other actor guards satisfied, a bound production actor can retire its
scene while production continues. Active or paused scene-owned production blocks
retirement. Unbinding/freeing a view does not delete the economic entity.
ActorRegistryComponent.RemoveActor is permanent gameplay destruction: its separate
ActorDestroyed signal removes linked production records and their deadlines,
without refunding spent inputs. Snapshot reconciliation and scene retirement do
not emit this signal. Non-actor production records still use RemoveProduction.
Ownership changes must still coordinate the correct wallet/service. This is not
automatic hauling, moving-worker or combat simulation; health reaching zero alone
does not call RemoveActor.

Actor links and the service-relative registry path are saved with production, so
a fresh service can reconnect while the actor is dormant. One service binds to
one registry. If destruction occurs during a production transaction, removal is
completed as that transaction unwinds, and no further loop cycle is started.
This does not establish a transactional checkpoint across all world services.

The production_residency_probe frees a player-owned actor's scene, advances world
production, wakes a fresh scene and verifies current state, single completion
signals, commands, ownership without a possessed avatar, and local-production
residency protection. It also checks snapshot reconciliation, cold restoration of
dormant actor links, idempotent permanent deletion, and destruction inside a
completion callback without extra output or input consumption.
The full 92-check headless suite passes after this binding and destruction change.
The run log is tests/output/actor_checks_latest.log. This is an integration
checkpoint, not a native rendering or sustained performance qualification.

Latest focused change: GridProductionComponent preserves elapsed work across
looping recipe completions, a prerequisite for reduced-frequency economic updates.
AdvanceWork performs at most 256 transactional completions per call; excess turns
remain in PendingWorkTurns and CaptureState/RestoreState. Subsequent positive
work updates drain that backlog. Paused calls do not accrue time, cancellation
clears backlog, and input starvation/non-looping completion discard surplus time
rather than crediting future production. Reentrant work advancement is rejected.
This does not yet detach working actors or simulate combat without scenes.

Last full-suite verification (2026-09-07): the preceding build succeeded with zero
warnings/errors; all 92 headless actor/terrain/streaming checks passed, including
actor-bound production residency, permanent destruction, node-free production,
production elapsed-time/deadline scheduling, and staged archive
capture isolation/cancellation and protected-search
progress during unrelated chunk eviction, follower callback scheduling, shared work claims,
material reservations, automatic construction access recovery, packed numeric
terrain samples, shared direct-component and motion lookup, and grouped
work-clock discovery, direct navigation edge parity and actor body binding. The earlier
12 native OpenGL probes passed (not rerun in this headless checkpoint): actor,
escort, party and platformer labs; actor overview and visual culling;
square/isometric surface overview; authored-isometric renderer/world publication;
isometric feature streaming; and the streaming lab. The party 768x800 and streaming
worker captures were inspected. `run_actor_checks.ps1 -Capture` now includes this
complete native set. Expected missing-authoring warnings in negative fixtures do
not establish that every addon scene is warning-free.

The functional scale fixture completed all 200 moving routes with 1,000 registered
identities (218 ms spawning, 268 ms capture, 1,298 ms scheduled travel on this run).
It is not 1,000 fully simulated animated actors or a sustained frame-time test.
Full-world native-upload stalls, total RAM, elevated-isometric residency and the
combined 1024x1024/1,000-actor performance gate remain open.

After that full-suite checkpoint, numeric terrain sample chunking passed its new
probe plus generation-job, water-surface and painted-publication regressions.
Painted publication also passed with native OpenGL. The runner now contains 83
headless checks at that stage; the latest 84-check run includes these changes.

The subsequent lazy gameplay-output allocation change passed scratch/default
initialization, generation-job, water-surface and painted-publication probes.
The 1024x1024 CPU-only benchmark completed in 30,049 ms with a 530 MiB process peak,
versus the preceding 28,415 ms / 743 MiB run. These are individual measurements,
not a sustained rendering/actor performance qualification.

| Area | Implemented | Remaining |
| --- | --- | --- |
| Player identity | Scene-local player, faction, optional possession, owned roster, selection, saved control groups; direct-control party cycling, opt-in companion handoff and possession-aware RPG HUD | Authored examples for every genre; party tactical policies |
| Actor identity | Stable IDs, authored definitions and scenes, registry spawning, ownership transfer, dormant records independent of scene instances | Reduced simulation for unloaded economic/combat actors |
| Commands | Move, stop, attack, work, interact, follow; ownership/capability validation; queues; custom handler port; resumable A*, scheduled followers, reachable group destinations, convoy lab and saved queued group moves | Formation matching/path reservations; navigation latency qualification |
| Existing components | Actor intent in movement, top-down, platformer, shooter, flight, jump, wall-jump, dash, glide, hover and slide; final ability velocity arbitration; order arbitration with AI/path following; faction filtering in attacks/projectiles | Full single-motor refactor; targeting systems and genre templates |
| Worker state | Stable worker IDs; owned job queues; exclusive worker/work-cell claims shared across queues on the same live map; completion/failure routing; route, work and hauling state captured | Multi-cell work-site/material/transport reservations; cross-chunk jobs with unloaded actor representations; scheduling policy by genre |
| Saves | Per-actor component state, position, velocity, owner, current/queued orders; movement ability runtime state including pending slide collision and wall launch; player selection/groups; registry restore after world services | Transactional validation of arbitrary malformed saves; a complete persistent simulation store |
| Presentation | Definition-based static/animated sprite limits, feet anchor, selection ring; native animation adapter with saved semantic state and sprite/player phase; authored platformer actor example and opt-in native visual culling | Other animated genre demos; tree blend/custom-speed persistence; full-detail actor performance qualification |
| Finite rendering | Camera-based chunks for uniform shader surfaces, flat feature/relief props and isometric features; square and diamond-isometric shader overviews; budgeted authored-isometric preparation integrated with world readiness; prop detail suppression and owned-actor overview markers | Streaming terrain fields, authored/elevated isometric surfaces, collision and navigation data; aggregate prop overview and dense actor LOD |
| Recipe metadata | Direct published-field queries by default; optional native TileData authoring view; streaming underground fingerprint | Chunk-addressable field storage and bounded metadata publication |
| Chunk persistence | Typed per-chunk archives; asynchronous reads and staged writes; shared operation/payload admission; cancellation, saved-content checks, actor/job/structure/camera/haul-endpoint pins, scheduled-search frontier loading and retiring route pins, automatic demand loads, resident chunk/cell limits and verified idle-chunk eviction | Fair global priority scheduling, total-memory budgets, hierarchical route demand, intermediate-storage demands, budgeted encoding/publication and checkpoint manifest |
| Huge generation | Existing TerrainWorldComponent remains the only world coordinator; bounded resource-placement lookup; streaming typed cell handoff; isolated worker backend; world/lab background generation, progress, cancellation and stale-result rejection; staged painted/autotile preparation and collision readiness | Remaining synchronous native uploads and scene publication; memory-bounded 1024x1024 terrain generation; macro topology plus detailed chunks |
| Endless generation | Not implemented | Coordinate-seeded expansion, persistence, bounded caches and seam validation |

## Scene Composition

### Scene-Independent Job Execution

`GridJobQueueComponent.AdvanceClaimedWork(jobId, workerId, workCell, elapsedTurns,
workSpeed)` is the shared work-execution entry point. It validates the reservation
scope, persistent worker ownership, current claim and reserved standing cell, then
advances the queue's authoritative remaining turns. Results are Rejected,
Progressed or Completed. Completion runs the existing JobCompleted signal and
reservation-release path; repeated advancement of a completed job is rejected.
Invalid time/speed inputs do not mutate progress.

`GridWorkerComponent` now calls this API instead of maintaining a separate work
calculation and then completing the job independently. A world-owned executor can
call the same API without instantiating a worker scene. Ownership checks use
ActorRegistryComponent.GetActorOwner, which includes dormant identities. Queue
snapshots retain remaining work; set RequeueClaimedJobsOnLoad to false when
restoring an executor and its claims together.

The executor must establish arrival at the reserved cell and validate worker
activity before advancing work; this API does not teleport workers or simulate
travel. Automatic offscreen movement and hauling are still remaining integration
work. No camera-based suspension was added to GridWorker.

`GridJobExecutionComponent` supplies the world-owned work-at-site scheduler.
Author JobQueuePath, ActorRegistryPath, GridPath and WorkClockPath, then call
BeginWork(workerId, jobId, workSpeed) on a claimed job after arrival. The component
requires the persistent actor position to match the reserved work cell. It uses
the shared clock and queue-owned remaining work, without a per-worker scene or
timer node. Stationary dormant identities can execute work. Live actors must be
active and alive. An active work order for this same job is permitted; unrelated
orders are not. A scene-owned GridWorker job must explicitly bind to this executor.

Set `GridWorkerComponent.ExecutionPath` to the world executor to enable automatic
handoff on arrival. The worker retains normal pathfinding and travel, then starts
world execution at the reserved cell. The queue and grid must match on both
components. There is no scene timer fallback if a configured executor is missing
or rejects the handoff. With an empty path, scene-owned work is unchanged.

Bound workers implement the existing residency guard: their work phase may retire
when world execution owns the job, but their travel phase may not. All other actor
residency restrictions still apply, including the active-order restriction and
the opt-in AmbientDormancy policy. Scene exit does not release delegated claims.
After wake, the worker reads current world progress; a job completed while absent
does not restart from the actor snapshot. Completion signals flow back to a loaded
worker and finish its matching player work order. Ordinary cancellation stops the
world execution and releases the claim.

Actor snapshot restoration preserves current delegated work during its initial
scene-command reset. Executor restoration then reconciles bound worker state via
ExecutionStateRestored, including when restoring a different job over a live one.
Capture the actor registry, queue and executor together and restore in that order.
This is not a general transactional world checkpoint.

The queue holds an executor lock for each running job. Another world executor or
ordinary AdvanceClaimedWork caller cannot advance that job simultaneously.
Releasing/completing its reservation removes the lock. The executor rechecks
position, ownership and claim state at its scheduled updates. Permanent actor
deletion stops execution and releases its claim immediately. StopWork(workerId,
releaseClaim) removes the execution; normal cancellation retains accrued partial
work before releasing the claim. Successful completion uses the queue's existing
events, so existing job effects remain the completion path.

CaptureState retains job IDs, standing cells, work speeds and pending elapsed
turns, not a second copy of job progress. Save registry, queue and executor state
together; restore the registry and queue before the executor, with
RequeueClaimedJobsOnLoad=false. Restore preflights claims, worker eligibility and
executor conflicts before replacing its records. Detaching the world executor
suspends its work and retains pending time for reattachment; it is distinct from
unloading an actor scene, which does not stop the executor. Author unique SaveKey
values when using multiple executors in one save scope.

The job_execution_service_probe verifies exclusive execution, continued work
after actor scene retirement, cold restoration, pending time across executor
detach/reattach, permanent deletion, arrival validation and leaving-site rejection.
It and five queue/worker/workflow regressions pass after this addition. Build
succeeds with zero warnings/errors. No new demo or optimization was added.

The worker_execution_binding_probe additionally verifies normal travel before
handoff, no double advancement, claimed work surviving scene retirement, wake at
current progress, completion while absent, restoring a different running job, and
player-issued work-order completion. It and eight focused actor/queue/worker/
production regressions pass. Offscreen travel and automatic claiming by unloaded
workers are now composed by GridWorkerDispatchComponent as described above;
hauling remains to be integrated.

Verification: job_execution_probe covers an owned identity after its scene is
freed, rejected foreign/wrong-cell work, saved progress, completion once, released
reservations and rejection after actor deletion. Worker-arrival, job-reservation,
cross-queue reservation, construction-effect and workflow regressions also pass.
The current build succeeds with zero warnings/errors; this focused change has
not yet had a full-suite rerun.

### Storage Material Claims

`GridStorageComponent` now exposes `TryReserveMaterials(owner, amounts)`,
`ReleaseMaterials(owner)`, `TryConsumeReserved(owner)`, `Reserved(resourceId)` and
`Available(resourceId)`. A claim keeps physical stock in its existing storage;
ordinary `Unload`, `CanProvide` and `TryConsume` operate on unreserved quantities.
`Stored`, `CurrentLoad` and saves still report the physical quantities, not a
second inventory. This prevents ordinary transport/production ports from spending
materials promised to another job.

Each owner has one complete material claim per storage. Resource amounts use the
existing parser and case-insensitive aggregation. Replacement is all-or-nothing,
including overflow/malformed-input rejection; failed replacement preserves the
old claim. Empty requirements release it. Reserved consumption removes the claim
and all required stock before any storage-change signal, preventing reentrant
double consumption. Owner exit, storage exit and storage restore release claims.
Owners must be live nodes in the same scene tree. Claims are transient: job/save
orchestration must reacquire them after restoring physical stock.

Construction sites now use this API for fully delivered supplies while waiting for
a valid work approach. A blocked site reserves but does not consume its materials;
ordinary unloading cannot steal them. Once access is available, the site consumes
its claim before creating the build job. Pending cancellation and coordinator exit
release claims without consuming supplies. Missing job queues cannot debit the stock,
and zero-quantity requirements do not stall waiting for a nonexistent claim.
Pending sites automatically retry access through a bounded round-robin queue
(8 checks per process frame by default). Storage deliveries still check immediately;
the explicit pending-build refresh entry point performs an immediate full refresh.
There is no second construction state machine.

This is not automatic remote material allocation for every construction job.
Remote construction/production orchestration, persistent job
claim reconciliation, destination-capacity booking and transport allocation remain
implementation work. The existing hauler already carries accepted stock; this
change does not add a second source-stock promise to that protocol.

Verification: build succeeded with zero warnings/errors. The new storage reservation
probe passed aggregation, competing claims, ordinary-port exclusion, replacement,
callback safety, teardown and restore checks. Resource-catalog ports, construction
effects, hauling and scoped-save probes passed. The runner now registers 81 headless
checks at that stage; the latest 82-check full-suite run includes these additions.

The extended `terrain_build_approach_probe.gd` passed blocked-site claim protection,
access recovery, zero requirements, cancellation and coordinator teardown. Build
completed with zero warnings/errors. The probe is now included in the runner,
bringing its registered headless count to 82. All 82 passed in the subsequent
full-suite run, including the bounded automatic-retry extension.
Storage claims, cross-queue claims, construction effects and the end-to-end terrain
building probe also passed, including real hauler delivery through storage signals
to a queued job completed by a GridWorkerComponent.

### Cross-Queue Work Claims

Queues with the same resolved `ChunkCellDataPath` now share worker and standing-cell
reservation indexes. The scope is the actual live `GridCellDataComponent` instance,
not a path string, player ID or static global world. Queues on different maps remain
independent. Empty paths retain local-only reservations; an explicitly missing map
rejects claims instead of silently becoming local. Bind this existing property on
every queue participating in the same colony or RTS map.

`CanClaimJob`, `ClaimJob`, `ClaimNextJob` and `TryReserveWorkCell` respect foreign
claims. Automatic selection skips occupied high-priority jobs. A rejected fallback
retains the old reservation; a successful transfer updates shared indexes before
callbacks. Completion, release, cancellation, clearing and teardown remove only the
owning queue/job pair, even when queues contain identical job IDs. Rebinding and
reattachment rebuild claims without overwriting existing reservations. On restore,
already-held shared claims win; conflicting saved claims return to Queued. This is
safe conflict handling, not a globally fair or order-independent restore scheduler.

The shared indexes are weakly scoped by the map and released on queue exit; they
are runtime indexes, not another world or a new save format. Existing job records
and chunk-pin behavior remain authoritative. This reserves one standing cell, not
an entire construction footprint, materials, transport capacity or a route.

Verification: clean build; cross-queue and existing local reservation probes passed,
including foreign worker/cell conflicts, fallback transfer, isolated maps, restore,
same-ID ownership, detach/reattach, map rebinding, missing-map rejection and nested
claim/release callbacks. Worker arrival, hauling, chunk pins and construction effects
also passed, as did the native OpenGL actor lab. The new probe raises the registered headless suite to 80; the prior
79-check full-suite result predates this reservation change.

### Budgeted Collision Updates

`TerrainCollisionComponent` now drains queued geometry changes through a deduplicated
FIFO at `ChunksPerFrame` (default 4, clamped 1..64). `RequestRebuild` schedules a
complete replacement, including retirement of chunks outside changed bounds.
`PendingChunkCount` and `IsUpdating` expose progress. Pending chunks fail
`IsChunkReady`, preserving the direct-motion gate until replacement and the next
physics frame. Detach clears pending work; reattachment refreshes existing shapes.

`Rebuild` remains synchronous and cancels queued work. Synchronous world initialization
still uses it. Background `BeginNewWorld` now queues collision and delays completion
until all chunks are ready after native physics synchronization. Budgeting the entire
rendering publication pipeline is still open. The limit is chunks per callback, not milliseconds or total shapes;
native/elevated chunks remain a potential expensive case. This does not introduce
camera-based physics removal or suspend offscreen economy/combat simulation.

`terrain_collision_budget_probe.gd` verifies budget limits, edit coalescing,
pending readiness, old-bounds retirement, immediate cancellation, missing-source
cleanup and automatic scheduling after reattachment.
Pre-tree rebuild requests also start on ready. Verification: the 74-check headless
suite passed; after the final pre-tree lifecycle fix, the build completed with zero
warnings/errors and the native OpenGL budget and direct-motion probes passed.

### Party Control and HUD

`PlayerContextComponent.CyclePossession(direction, group)` switches direct control
among owned actors. Use group `-1` for the owned roster or `0..9` for an existing
control group populated with `SelectActor` and `StoreGroup`. Groups remain the
same saved stable-ID sets used by RTS selection; there is no second party roster,
duplicate actor instance or separate world. Cycling leaves current selection
unchanged. IDs are traversed in ordinal order, wrapping forward or backward;
dead, inactive, missing and transferred actors are skipped. A one-member group
does not repossess itself or cancel its own orders.

For keyboard/controller switching, author `NextActorAction`,
`PreviousActorAction` and `PossessionGroup` on the player, and register those
actions in the game's InputMap. The default action names are empty, so no genre's
bindings change automatically. Switching is available only in Direct mode and
does not require an existing avatar. Orders and Colony modes never acquire an
avatar through this API. An input event consumed by a focused control is not used
for party switching. Querying `GetGroupActors` does not wake actors; cycling wakes
only the candidate it attempts to possess, using the existing residency service.

Assign `RpgHudComponent.PlayerPath`, relative to the HUD host, to follow the
possessed actor's direct-child `RpgPartyComponent`. The HUD disconnects the old
stats signals, binds the new actor, and clears readouts when possession is lost
or that actor has no RPG stats. It never falls back to another actor's stats when
an explicit player binding is present. Save restore and HUD reattachment rebind
to current possession. The Follow command below supplies movement for companions.

Opt into `FollowPartyOnPossession` to redirect idle companions when possession
changes in Direct mode. `PossessionGroup` selects the existing control group
(`-1` means owned roster); `PartyFollowDistance` is measured in world units.
Orders and Colony modes do not acquire an avatar or automatic party following.
Policy-created orders are tracked by actor identity and command revision, so a
manual Stop or replacement command survives subsequent handoffs. Active jobs,
hauling and existing movement/orders are not replaced. `RegroupParty()` explicitly
releases manual holds, but still does not take over busy actors.

Disabling the policy, detaching the player or leaving Direct mode cancels only
policy-owned following. Save/restore retains the policy, group, distance and
manual holds. Reattachment resumes eligible companions. Public command callbacks
may replace orders without having those replacements claimed by the policy.
Settled followers retain movement ownership rather than resuming ambient roaming.
This policy uses grid navigation; platformer following and autonomous party
tactics remain unimplemented. Group dispatch is synchronous, not yet budgeted
for thousand-member parties.

Verified on 2026-09-07: clean C# build and all 63 headless checks passed with
automatic handoff, manual holds, JSON restore, ownership changes, reentrant
command replacement, reattachment and physical companion movement. After the
final settled-follow movement arbitration change, the build and party-handoff
probe passed again. Its ambient arbitration fixture uses a Node2D body and
therefore emits the expected animal-body/season warnings; it tests ownership,
not native animal locomotion. The subsequent 64-check run for the party lab also
passed with that final arbitration change included.

### Follow and Escort Commands

Submit `ActorAction.Follow` through the existing registry with an owned
`TargetActorId` and `FollowDistance` in world units (default 64, accepted 1..65,536).
Recipients require Move capability and the existing grid path follower. No avatar
is required: RTS/colony owners can issue exactly the same command as a party game.
Self, foreign, dead/inactive/missing targets and circular current/queued follow
chains are rejected before replacing the recipient's orders. Custom command
handlers retain their own command validation/execution contract.

The actor remains ordered while waiting near its leader. It resumes when the
leader moves beyond the range plus a hysteresis margin, preventing small target
movements from causing constant restarts. Replanning occurs at most once per
0.5 seconds per follower. Pending A* requests, including archived terrain demand,
are allowed to finish instead of being cancelled every frame. Blocked approaches
try up to sixteen angular directions over successive retries using the existing
navigation rules; followers never teleport or move directly through water.
An unreachable follower keeps its command and retries, so removing an obstacle
can restore progress. This is not a guaranteed formation-slot solver.

Followers reserve non-overlapping resting destinations through the existing actor
registry. Bounds use the actor definition's world-space footprint plus four units
of padding, and exclude the leader's footprint. Already settled actors keep their
position until the leader leaves the hysteresis band or enters their footprint.
Companions starting at the same point must find separate destinations before
settling; candidate positions are projected onto the follower's existing grid and
known blocked cells are rejected. Missing archived cells still go through the
scheduled navigation demand path. Cell quantization may place the final resting
point within the distance plus hysteresis rather than strictly inside the distance.

`ActorRegistryComponent.FollowRestReservationCount` reports these temporary claims.
Cancellation, order completion, detach and removal release the actor's claim.
Save restore rebuilds claims from restored orders instead of serializing runtime
reservations. A full ring or unreachable slot keeps the Follow order pending; it
does not teleport or declare success. These claims prevent resting overlap between
followers, not crossing routes, arbitrary idle actors, or every collider shape.
Navigation still uses its existing cell clearance rules. Reservation acquisition
uses an internal world-space index with 256-unit buckets, independent of the actor
position index's configurable cell size. A normal query checks intersecting buckets
and oversized entries, deduplicating claims spanning multiple buckets. Each claim
occupies at most 64 buckets; larger or out-of-index-range bounds use a single
overflow entry and exact rectangle tests. Oversized queries scan existing claims
instead of allocating a huge query grid. Nonfinite bounds are rejected.

Release and relocation remove empty buckets. Invalid local claims are retired
before deciding whether a destination is free. `FollowRestBucketCount` and
`LastFollowRestCandidateCount` expose index residency and candidate work for tests.
This reduces interaction between distant parties; a dense crowd in one area, many
oversized claims and fair slot assignment still need performance/design work.

Verified on 2026-09-07: clean C# build and all 66 headless checks passed after
indexing. The added index probe creates 1,000 actors in 500 separated groups;
their initial acquisitions examine zero unrelated reservation candidates, with
roughly 331-353 ms for the complete fixture setup on this machine. It additionally
checks negative coordinates, local stale-claim removal, reactivation, relocation,
oversized-footprint overlap detection and complete bucket cleanup on cancellation.
This is an index-work/functional test, not a dense-crowd or rendered FPS benchmark.

Verified on 2026-09-07: clean C# build and all 65 headless checks passed after
resting reservations were added. The new spacing probe starts six followers at
one point, includes unequal footprints, checks settling and leader-motion jitter,
and exercises save/restore, cancellation and body removal. The native party lab
also passes a footprint-overlap assertion after regrouping; its refreshed desktop
and narrow captures were visually inspected. Crossing-path avoidance and
large-group reservation performance have not been qualified by these checks.

Removing the leader, making it inactive/dead, or changing its owner ends the
command with `follow_target_unavailable` and cancels its route. Stop/replacement
commands use normal order cancellation. Follow is persistent rather than an
arrival task, so appended commands wait until it ends or is replaced. Actor
snapshots retain the target and distance; restore resumes against current leader
position. `FollowTargetActorId` reports the active or next follow target.

The authored `actor_lab.tscn` and inherited streaming lab provide an Escort button.
Select at least two units, press Escort, and then move the selected leader. The
lab orders selected IDs deterministically into a convoy; each follower tracks the
preceding unit, rather than converging on one shared destination. Selection then
contains only the leader. Select all and Stop to cancel the convoy. The UI uses
the existing kit button and shared wrapping toolbar layout.

Verified on 2026-09-07: clean C# build and the 62-check headless suite passed.
After the final cycle rejection and convoy assignment changes, the follow probe
and native escort-lab probe passed again. Captures at 1280x800 and 768x800 were
inspected for convoy separation and toolbar/status layout. Autonomous party
tactics, collision-aware formation slots and following
in platformers without grid navigation are not implemented by this command.

### Shared RPG Health

`RpgPartyComponent` requires sibling `HealthComponent` and `LevelingComponent`
nodes. It owns RPG growth tuning, mana and quests, but no independent HP or XP.
Its Health, MaxHealth,
HealthFraction and IsDead readouts use the combat health source, and health
changes/death are forwarded to existing RPG HUD signals. `Damage(GameDamage)`
uses the normal typed damage pipeline, including resistance, armor, invulnerability
and source attribution. There is no untyped integer-damage compatibility overload.
Healing and revival delegate to HealthComponent as well.

RPG BaseMaxHealth/HealthPerLevel configure capacity on first binding and level-up.
First binding preserves the authored health fraction; level-up fills a living
character without emitting a false revival. `HealthComponent.SetMaximumHealth`
changes capacity without generating damage, healing or revival events. Capacity
changes and healing never resurrect dead characters. Reattaching the same stats
and health nodes preserves HP, capacity and mana and reconnects subscriptions once.

HealthComponent alone restores current HP and saved capacity, including runtime
modifications. RPG loading does not overwrite either, so actor JSON restores work
in either authored component order. The obsolete `rpg.health` save entry is no
longer written or read. Registered actors already capture both ISaveable components
in separate scopes; actorless scenes must include HealthComponent in their save
walk explicitly. No health node is silently created when a scene is misconfigured.

`tests/actor_rpg_health_probe.gd` runs real C# combat packets through both entry
points, checking mitigation, fractional HP, progression, death/possession loss,
revival, modified-capacity saves, reversed load order and reattachment.

### Shared RPG Progression

`LevelingComponent` is the level/XP/stat-point source for RPG characters as well
as other progression-based actors. Configure its BaseXp, XpGrowthMultiplier,
MaxLevel and StatPointsPerLevel. RpgPartyComponent's separate BaseXpToLevel,
XpCurve, level state and XP state have been removed. Its AwardXp(float) API
delegates to the same source used by HealthComponent's fatal-hit XpReward path.
Fractional rewards are retained; dead characters do not gain XP through either
entry point. RPG health/mana growth and LeveledUp notifications observe the
source's LevelUp signal.

LevelingComponent is an ISaveable participant with versioned
`progression.leveling` data for level, fractional XP, stat points and active state.
Actor saves include it automatically; actorless scene save walks can opt in with
ParticipatesInSave. Loads validate the complete progression record before changing
live state and notify XP readouts without emitting LevelUp/MaxLevelReached or
granting stat points again. ActorComponent restores progression before dependent
RPG pools, independently of authored child order. For actorless custom save walks,
restore LevelingComponent before RpgPartyComponent. Old rpg.level/rpg.xp entries
are neither written nor read.

`tests/actor_rpg_progression_probe.gd` verifies real kill rewards, single credit
per death, fractional XP, shared RPG progression, modified actor state restoration,
stat-point persistence, no reward replay, malformed-record rejection and clean
subscriptions after reattachment.

`tests/actor_party_probe.gd` checks deterministic cycling, skipped actors,
ownership revocation, saved group/possession restoration, selection independence,
input routing, HUD signal isolation and reattachment, and selective dormant wake.

### Animated Platformer Example

Run `addons/beep_game_builder_cs/templates/scenes/actors/actor_platformer_lab.tscn`
directly. A/D or arrow keys move; Space jumps. The sample uses the existing
default-input generator without changing project settings. It is a movement and
presentation example on a finite test floor, not a complete platformer level.

`actor_platformer.tscn` is an authored CharacterBody2D with separate Actor,
PlatformerController, Animation and Presentation components. Its instanced
`actor_visuals.tscn` subtree contains the native AnimatedSprite2D and visibility
enabler. Kenney Adventurer poses supply idle, two-frame walking, jump, fall and
hurt clips; their CC0 license is included beside the textures. Visual bounds are
48x64 world pixels; the independently authored collision is 24x48. It does not
replace the RTS worker or assume that all players require an avatar.

The lab assigns an explicit registry path, stable actor ID and owner ID, then
PlayerContext possesses that actor. When reusing the actor scene, supply those
three values in the containing scene. The animation adapter stays outside the
cullable visuals, so gameplay state continues to update offscreen. The camera
follows the body; the native test deliberately moves it away to verify culling.

`tests/actor_platformer_scene_probe.gd` verifies ownership/possession, actual
player input routing, grounded idle, walking, directional jumping and landing.
With a native renderer it additionally checks that culled actors keep moving,
visuals resume when the camera returns, and captures
`tests/output/actors/platformer.png`. It is included in the actor check runner's
headless and optional native-render passes.

Actor, player-context and actor-presentation components reconnect on scene-tree
reattachment. Actor IDs, ownership and order queues are not reinitialized; initial
player possession is not replayed. Detaching an actor still unregisters it and
clears player selection/control-group references, so callers should select it
again after reparenting. Authored relative service paths must resolve from the
new parent; use explicit absolute paths for dynamically moved containers.
`tests/actor_reattachment_probe.gd` verifies repeated reparenting during a move,
single completion, player reattachment, presentation subscriptions, death cleanup,
delayed actor reattachment and permanent removal. This is not cross-world order
migration: moving between different registries requires a separate transfer policy.

```text
Game
  TerrainWorldComponent + existing grid / cells / navigation
  ActorRegistryComponent
  PlayerContextComponent (Orders or Colony; no avatar required)
  GridJobQueueComponent (owner + registry)
  Actors
    CharacterBody2D
      Sprite2D + CollisionShape2D
      ActorComponent
      HealthComponent
      GridPathFollowerComponent
      GridWorkerComponent
      ActorPresentationComponent
  Camera2D / GridCameraControllerComponent
  ActorOrdersControllerComponent
  Authored HUD scene using the existing UI kit
```

`PlayerContextComponent` represents the command issuer, not a character. An RTS
player can own hundreds of actors without possessing one. A direct-control game
sets `ControlMode = Direct` and calls `Possess(actorId)`. Neutral NPCs can have an
empty owner. Two different factions are not automatically enemies: use
`ActorRegistryComponent.SetHostile(factionA, factionB, true)`.

`ActorDefinition` references a PackedScene, capabilities, a logical footprint,
maximum visual dimensions and a normalized feet anchor. Author collision shapes
in that scene. `ActorPresentationComponent` fits the sprite uniformly within the
visual dimensions; it does not stretch art or silently resize collision shapes.
All distances are world-space pixels, independent of camera zoom. Animated sprites
use one scale fitted to the largest frame dimensions across their SpriteFrames;
frame changes adjust the feet anchor without changing scale or collision geometry.

### Native Animation

Add `ActorAnimationComponent` beside the actor's gameplay components. Assign an
`ActorAnimationProfile` and an explicit `AnimationTargetPath` to one native
`AnimatedSprite2D`, `AnimationPlayer`, or `AnimationTree` with a root state machine.
The profile names clips/states; it does not generate another animation graph.
For a tree, author and enable the native tree and its transitions. Native nodes own
playback, looping and blending. Avoid a second controller driving the same target.

The adapter observes actual displacement, not movement input or a pending path.
It retains facing while idle and supports horizontal flip, four-way and eight-way
suffixes. Missing directional clips fall back to their base name, then idle;
missing or empty sprite animations are never sent to native playback. Enable
`UseGroundedStates` only for side-view actors. `FaceAim` supports aim-facing actors.

Existing attack/shooter and health signals drive action poses. Death takes priority
over hurt and attacks; movement abilities, flight and real worker activity supply
their corresponding states. Worker discovery uses `IWorker`, with `WorkerPath`
support for the existing GDScript worker port. A pending path is not work.
`RequestAction(Attack/Hurt/Interact, seconds)` supports custom action handlers.
The exported hold durations control pose selection, not combat or work timing.
Repeating an actual action restarts its clip; steady state does not restart playback
each tick, including a completed death clip.

For an AnimationPlayer/tree with horizontal flipping, assign `FlipSpritePath` to
the visual sprite. No body scale, physics mode or simulation tick is changed.
Disable the adapter to hand presentation to another controller, and call
`RefreshBindings()` after replacing target/source nodes at runtime. Reattachment
automatically disconnects old gameplay signals and resolves the new body.

Starter profiles are `templates/scenes/actors/animation_topdown.tres` and
`templates/scenes/actors/animation_platformer.tres` under the addon. They require
the user's authored art/clips; they are not complete animated game demos.
`tests/actor_animation_probe.gd` checks all three native playback targets, directional
selection, no-displacement idle, action priority/restarts, real/custom workers,
frame sizing, reattachment and simulation isolation. Actor-scoped JSON saves include
facing, remaining action holds, pending action restart and current state/clip.
Animated sprites retain frame/progress and paused state; AnimationPlayer retains
its playback position and paused state without evaluating method tracks on load.
Loading resets the displacement sample so teleporting back to a save is not movement.
AnimationTree resumes the selected root state, but in-progress cross-fades and nested
graph parameters are not serialized. Missing authored clips fall back on the next
normal update. Custom playback speed/direction and externally driven cutscene state
are not captured by this adapter.
See Godot's [AnimatedSprite2D](https://docs.godotengine.org/en/stable/classes/class_animatedsprite2d.html)
and [state-machine playback](https://docs.godotengine.org/en/stable/classes/class_animationnodestatemachineplayback.html) contracts.

The body owns its movement/collision components. Actor commands reuse those
components; the registry does not create a second world or a second pathfinder.

### Native Visual Culling

Instance `templates/scenes/actors/actor_visuals.tscn` under an actor body. Its
`Visuals` root contains native AnimatedSprite2D, AnimationPlayer and
VisibleOnScreenEnabler2D nodes; assign your art and animation resources in an
inherited scene. This is a reusable visual template, not a complete animated
character demo. Point `ActorPresentationComponent.SpritePath` and
`ActorAnimationComponent.AnimationTargetPath` at the chosen node under `Visuals`.

The enabler targets only `Visuals`. Keep ActorComponent, movement, health, workers,
haulers, collisions and the animation state adapter as siblings outside that root.
Offscreen animation processing stops while gameplay and action timers continue.
Animation tracks under this root must be visual-only: gameplay effects belong to
gameplay components, never animation method keys that would pause with culling.
Children should inherit process mode; an Always-processing child opts out of this
policy. Adjust the enabler's authored rectangle to enclose the largest animation
frame, attachment and particle extent. Do not hide the enabler itself: its native
visibility detection depends on rendering. It resumes using inherited process mode
and therefore respects SceneTree pause. This is process culling, not actor unloading.

`tests/actor_visual_culling_probe.gd` runs native visibility, animation-phase,
simulation-isolation, pause and reattachment checks with a rendering backend.
The headless dummy renderer checks only authored structure and reports that limit.
The template is opt-in and does not retrofit existing static worker art or claim
the 200-full-detail actor performance gate.
See [Godot VisibleOnScreenEnabler2D](https://docs.godotengine.org/en/stable/classes/class_visibleonscreenenabler2d.html).

`FlockingComponent` participates in the same actor motor ownership as direct
controllers. A pending/travelling path takes precedence; otherwise the first active
supported motor in authored child order owns movement. Flocking yields without
changing velocity and resumes from current body momentum after another controller
or path yields. Its final integration uses shared dash/knockback handling, so a
knockback component does not independently integrate the same body. Reattachment
clears the old body binding. `tests/actor_flocking_probe.gd` verifies these contracts.
Registered flocks default to `UseActorSpatialIndex`: only nearby live actors in
the same registry and named Godot group contribute. Set it false for mixed groups
containing non-actor bodies; actorless flocks also use the group scan. Radius
filtering remains exact after the spatial broad phase. Registry queries choose
direct local bucket lookups or occupied-bucket scans for overview rectangles,
whichever visits fewer buckets, and retain stable ID ordering. Read-only
`LastQueryBucketVisits` and `LastQueryCandidateCount` expose query work for the lab
and tests. `tests/actor_spatial_probe.gd` compares queries against an exhaustive
1,000-actor reference, including negative bounds, owners, movement and bucket resize.
Shared native movement and direct path movement publish bucket changes immediately,
before path-arrival callbacks, including projection changes and destination snaps.
Custom movers/teleports should call `ActorComponent.SynchronizePosition()` after
changing the body transform and before issuing queries or notifying other actors.
The actor tick still refreshes externally changed transforms as a fallback.
This reduces broad-phase work; dense local swarms still require performance
qualification. Standalone sports/projectile movers remain a separate audit.
Only one participating mover drives an actor body at a time. Do not stack two
alternative direct-control motors and expect both to execute.

### Dash and Knockback

`PlayerContextComponent` forwards unhandled `dash` input only to its possessed actor.
Actor-bound dash components consume `SetDashIntent(held)`/`ConsumeDash`, never global
keyboard state. Game logic can call `DashComponent.TryDash(direction)` explicitly;
it enforces cooldown, stamina, stun, activity and current-order constraints.
Order replacement cancels a running dash. Bodies without actor identity retain
the authored `DashAction` input path.

Participating controllers use `CharacterMotion` immediately before native
`MoveAndSlide`: active knockback overrides dash, which overrides ordinary velocity.
This is a final velocity policy, not an additional movement integration. Knockback
does not add its impulse repeatedly to last frame's velocity. Bodies without an
active mover integrate knockback directly. Physical grid followers apply knockback
without advancing or snapping route waypoints; direct-position followers pause.
Horizontal platformer dashes retain gravity; top-down dashes discard perpendicular
drift. Dash protection is read directly by health and never removes a separate
`invincible` status effect. Reattaching dash/knockback resets their body bindings.

`tests/actor_motion_abilities_probe.gd` checks global-input isolation, displacement,
priority, standalone and pending-follower knockback, reattachment and protection.
This does not yet unify all movement ownership. Persistence coverage is listed below.

### Flight

Actor-bound `FlyComponent` uses `MoveIntent`, including analog magnitude, rather
than global movement actions. The actor's dash intent requests boost when no active
dedicated `DashComponent` owns that input. Standalone flight retains `BoostAction`;
when it shares an action with a dedicated dash, dash takes precedence. Explicit
`TryBoost()` checks activity, stun, duration and current movement orders. A held
button does not retrigger boost automatically. Flight yields to another actor mover
and clears its boost when ownership changes. Reattachment resets cached body,
sprite and boost state. `tests/actor_flight_probe.gd` covers input isolation, analog
movement, boost/dash priority, follower ownership and reattachment.

### Wall Jump

Wall detection and jump intent run before `JumpComponent` and the platformer motor,
independently of authored sibling order. Wall-jump consumes actor jump intent only
when wall sliding (including the stick-time window). The normal jump handlers defer
on that physics tick; without wall contact they retain the input. Standalone bodies
retain the ordinary `jump` action. Final velocity arbitration preserves the horizontal
kick for `WallJumpLockTime`, while gravity resumes after the launch tick. Wall-slide
speed is clamped after gravity. Knockback and dash take priority over wall movement.
Orders, stun, deactivation and reattachment reset wall motion; active wall motion
prevents ambient dormancy. The last contact direction remains valid during stick time.

Generated detection rays are retired on exit and recreated safely on same-frame
reattachment. Existing authored rays retain their settings and are never deleted by
the component. `tests/actor_wall_jump_probe.gd` verifies native ray contact, input
isolation, jump priority, kick/gravity behavior, stick-time direction and ray ownership.

### Glide and Hover

`PlayerContextComponent` routes each possessed actor's authored `GlideAction` and
`HoverAction`, including custom action names, through unhandled input. Actor-bound
abilities read held actor intent, not global keyboard state. Game logic can use
`SetAbilityHeld(action, held)`; possession loss and intent clearing release it.
Bodies without an actor retain their authored input actions.

Glide evaluates falling state and horizontal air control before the motor. Hover
retains its airborne time budget and cooldown. `CharacterMotion` applies both
velocity limits after ordinary gravity/acceleration, before wall movement, dash
and knockback. The abilities do not integrate the body a second time. Movement
orders, stun, dash and knockback interrupt them; active abilities prevent ambient
dormancy. Reattachment clears body bindings and transient ability state.

`tests/actor_air_abilities_probe.gd` verifies custom player input routing, global
input isolation, final descent/air-control limits, hover exhaustion, release,
order cancellation, dormancy and reattachment. Hover budget/cooldown and glide
activity/horizontal momentum are persisted; hardware input is not replayed.

### Ground Slide

`SlideComponent` uses actor-held `SlideAction`, including custom actions routed by
the player context. A rising press starts one slide; holding the action does not
repeat it. `TrySlide()` allows explicit NPC/game-logic activation and requires
ground contact, horizontal momentum and no competing order, dash, stun or knockback.
The motor applies slide momentum after ordinary acceleration, with native movement
integrated once. Slide ends on timeout, stopped momentum or lost ground contact.

Collision shrinking currently supports an authored `RectangleShape2D`. It uses a
temporary duplicate, never mutates shared resources, and offsets the shape to keep
the feet anchored. After cancellation or expiry, a native standing-shape intersection
query checks clearance using the body's collision mask and exceptions. Under a low
ceiling the actor keeps its reduced collider, without slide propulsion, until it can
stand safely. That pending state prevents ambient unloading. Reattachment restores
the previous body's authored shape/transform and resolves the new body's collider.

`tests/actor_slide_probe.gd` exercises native floor/ceiling collisions, shared shapes,
feet anchoring, input isolation, momentum, one-shot activation, cancellation,
standing clearance, dormancy, deactivation and reattachment. Clearance uses Godot's
[shape query API](https://docs.godotengine.org/en/stable/classes/class_physicsshapequeryparameters2d.html).

### Movement Ability Saves

Dash, hover, jump, flight boost, knockback, wall jump, glide, slide and the
platformer's built-in jump implement the existing `ISaveable`
contract. Actor snapshots keep their state separately by authored component path;
no second registry or save coordinator is introduced. The records are versioned
JSON dictionaries, not serialized native objects. Load restores state without
replaying activation signals, spending stamina again or replaying held input.

Dash resumes remaining duration, direction and cooldown, including protection.
Hover preserves consumed airborne time and cooldown. Jump preserves its remaining
jumps, coyote/buffer windows and variable-height cut progress. Flight restores its
remaining boost and banking state. Knockback restores its remaining time and already
decayed impulse. Wall jump restores contact direction, stick time, horizontal lock
and a launch awaiting integration, but never reapplies an already-integrated kick.
Glide restores its activity and horizontal momentum. Slide stores its remaining
momentum and collider reduction ratio, including a completed slide waiting for
standing clearance. Restore first releases its previous temporary collider, then
derives a new duplicate from the authored shape, so repeated loads cannot compound
the reduction. The platformer preserves its built-in coyote and buffered-jump state.
Timers are bounded by current authored limits. Each participant
validates all fields before mutation; this is not whole-registry transactional load.

Actor snapshots also preserve native velocity and the most recently resolved floor
contact. Until the first participating motor integrates the restored body, movement
components use that saved contact rather than stale native pre-load flags. The first
integration returns contact ownership to native physics. This prevents loading an
airborne actor over a previously grounded one from refilling jumps or hover budget.

`tests/actor_ability_save_probe.gd` covers JSON round trips, mid-ability and cooldown
restoration, no activation replay, stale floor flags, fresh-instance flight restore,
and malformed ability data rejection without participant mutation.
`tests/actor_contact_ability_save_probe.gd` additionally checks repeated slide loads,
fresh low-ceiling restoration, clearance recovery, built-in coyote/buffered jumps,
wall launch saves before/after native integration and glide momentum without input
replay. These tests do not establish transactional restoration of the whole world
or the remaining cross-genre presentation/performance requirements.

## Developer API

```csharp
// Registry and player are authored scene nodes. The definition contains a scene.
ActorComponent? worker = registry.SpawnActor("worker", player.PlayerId, spawnPosition);
player.SelectActor(worker!.ActorId);
player.IssueOrder(ActorAction.Move, destinationCell);
player.IssueOrder(ActorAction.Work, destinationCell, jobId: jobId);
registry.TransferOwnership(worker.ActorId, otherPlayer.PlayerId);
```

Use `ActorOrdersControllerComponent` for pointer selection and move commands.
Left click selects, drag box-selects, Shift adds/queues, right click moves or
attacks an explicitly hostile actor, and Ctrl+number stores a control group.
Orders use unhandled input so GUI interactions take precedence. Group movement
uses scheduled navigation preflights to assign distinct reachable nearby cells.
`MoveSelection` returns the number accepted for planning, not dispatched commands;
`IsFormationPending` and `FormationFinished(dispatched, rejected)` expose the result.
Candidates are tested in deterministic rings, with one outstanding search per
controller and at most 64 candidate checks per update. Unreachable candidates are
skipped; stopped, reassigned, removed, or otherwise reordered actors are rejected.
Replacement cancels pending searches and queued group plans. Appended groups retain
their selected actors and enter a FIFO planning queue, capped by
`MaximumQueuedFormations` (default 16 waiting groups). `QueuedFormationCount`
excludes the active group. A full queue returns zero without changing earlier plans.
Appended movement waits for each actor's earlier commands to finish before querying
its actual start position. The controller's own dispatch advances later plans;
external stop/replacement, ownership changes and actor restores invalidate them.
Navigation changes or a changed actor start reject that actor's stale preflight.
Followers still validate when executing. This does not reserve routes, prevent unit collisions,
or optimize formation matching. `tests/actor_formation_probe.gd` covers barrier
fallbacks, distinct targets, cancellation, replacement, stale terrain, queue limits,
selection changes, stop across queued groups, and multi-destination arrival.
The registry includes group plans in player snapshots, keyed by the controller's
path relative to its player. Restore actors first, then players and their plans;
the existing registry restore performs this ordering. Saves retain remaining actor
IDs, eligibility, ordered candidates, assigned destinations and completion counts.
Search IDs, partial search state and runtime command revisions are not serialized.
Loading restarts pending checks and does not reissue already dispatched members.
Cancelled members remain ineligible even through repeated JSON saves. Missing actors
are rejected when their turn is processed. A directly supplied malformed plan is
validated before live plans are replaced; arbitrary whole-registry restore is not
transactional. `tests/actor_formation_save_probe.gd` checks these save boundaries.
Pending and queued plans keep their eligible actors resident even after deselection;
cancelled plans release that restriction, without pausing offscreen economic actors.

Custom execution is optional: assign `ActorComponent.CommandHandlerPath` to an
`IActorCommandHandler` implementation (or a node exposing its four methods).
The handler owns validation, start, completion and cancellation for that actor.
Custom handler state must implement `ISaveable` if it needs to resume mid-order.

Use `GridCameraControllerComponent.FocusWorld` and `SetZoomLevel` to position an
authored camera. Assigning Camera2D.Position directly does not update the
controller's target.

## Budgeted Navigation

`GridNavigationComponent.RequestCellPath(start, goal)` queues a cooperative
main-thread search and returns its positive request ID, or zero when the service
is unavailable or its queue is full. Connect `PathRequestCompleted(id, path,
reason)` before submitting. An empty reason means success. Cancellation through
`CancelPathRequest(id)` is silent; clearing requests or removing the service also
discards work without completion. Request IDs are runtime-only, not save identities.

The default limits are 1,024 heap removals per frame, eight active search working
sets, and 1,024 outstanding requests. Active searches rotate in 64-removal slices;
stale heap candidates count against the budget too. A best-effort 2 ms deadline
is checked between slices. Zero disables that deadline, not the work-count limit.
Setup, reconstruction, native calls and signal listeners can exceed the deadline;
this is not a hard real-time guarantee. Diagnostics expose pending/active counts,
heap removals and elapsed milliseconds for the last processing update.

The synchronous `FindCellPath` API now consumes the same resumable search engine.
Traversal costs, bounds, corner rules and ramps are shared, not reimplemented.
Active searches fail with `navigation_changed` if traversal inputs, roads, occupancy,
explicit blocks, source bindings or navigation rules change. Cell traversal revisions
track normalized terrain kinds, the blocked flag, relief and ramp direction.
Farming, crop growth and decorative metadata still publish their normal cell signals
but do not restart searches. Bulk cell replacement conservatively invalidates searches.
Queued searches use the state when they begin. The caller decides whether to retry.
Completion listeners may cancel, clear or enqueue work without reentering processing.

`tests/terrain_navigation_requests_probe.gd` compares 200 queued routes against
synchronous A*, enforces per-update work/active limits, and checks mutations,
cancellation, capacity, reentrant listeners and normal frame-driven completion.
`tests/terrain_navigation_invalidation_probe.gd` checks traversal mutations, harmless
cell updates, bulk restores, and continuous farming during a pending search.
`GridPathFollowerComponent.MoveToCell` and `MoveToWorld` now schedule requests.
A true return means accepted, not that a route has already been found. `IsMoving`
stays true throughout search and travel; `IsPathPending` distinguishes searching
from walking. `PathStarted` fires when a route is installed, and `MoveFailed` plus
`LastMoveFailure` reports failure. No motion or arrival is reported while pending.
Explicit `SetCellPath`/`SetWorldPath` still install supplied routes directly.

Replacement, cancellation and actor removal cancel queued work. Reparenting a
pending actor resubmits from its new binding; snapshots persist destination intent,
not runtime request IDs. Loading a pending save creates a fresh request before
worker/hauler state restores. Terrain changes or movement of the starting actor
retry at most three times before reporting failure. A cleared/removed navigation
service is detected by the follower, which releases movement ownership.
Workers try their job-cell fallback after an asynchronous approach failure and
start work only after verified arrival. Haulers retain their existing arrival and
cargo safeguards while waiting. `tests/terrain_follower_requests_probe.gd` covers
these transitions and pending JSON saves; the actor suite covers hauling restores.

The lightweight 1,000-identity/200-moving-actor test completed all short scheduled
routes in approximately 2.0-2.3 seconds locally, including travel. This does not
qualify full-detail FPS or worst-case navigation latency. The broad
`headless_runtime_smoke.gd` currently fails earlier on a standalone placement
fixture without a grid; the affected follower fixtures run separately through
`GridPlacementSmoke.RunFollowerChecks`.

## Saving and Jobs

The actor registry joins the existing save system. Actor-owned ISaveable nodes
are excluded from global singleton slots and captured into separate component
records keyed by their path relative to the actor body. Nested actor bodies are
separate save boundaries. This prevents one unit's health/inventory overwriting
another unit's state.

Restore terrain, jobs and other world services before restoring actors. The
existing GameStateManager restores actor registries last. Authored definition
IDs and component paths must remain stable within a save format. No legacy
actor format or migration path is introduced.

Use actor IDs as worker IDs. The worker spawner configures registry, owner, grid,
navigation and queue references before parenting the instance. Owned queues
reject claims from another player's actors. Worker success comes from its
completion signal, not merely from becoming idle. Cancelling a failed route
releases the work claim and reports failure.

For actor-backed worker factories, assign `GridWorkerSpawnerComponent.UnitDefinition`
and register that definition in the actor registry's catalog. Its scene takes
precedence over the anonymous `UnitScene` option. Dead actors cannot claim jobs.
Cancelling an active haul holds its cargo instead of remotely delivering it;
`ResumeHaulToDepot()` explicitly resumes delivery. Actor saves restore the route
before worker/hauler state, regardless of the authored child-node order.

`GetJobs()` is a runtime dictionary API containing Vector2I coordinates. A custom
JSON exporter must encode `cell`, `approach_cell` and `reserved_cell` as `{x, y}` dictionaries,
as shown in the actor lab. Do not stringify vectors and expect to reload them.

### Work Reservations

`GridJobQueueComponent` owns both claim indexes: one claimed job per worker ID
and one claimed job per standing cell, within that queue. Worker identity is
ordinal and must be nonblank. `CanClaimJob` is a read-only preflight;
`ClaimJob` publishes both reservations before emitting `JobClaimed`.
`ClaimNextJob` skips reserved approaches and ranks eligible jobs by priority,
then distance to the approach, then ordinal job ID. Distance uses widened
arithmetic for large coordinates.

`GetWorkCellReservation` returns the occupying job ID. `GetReservedWorkCell`
returns the current standing cell for a claim. `TryReserveWorkCell` atomically
transfers that cell only for its claimant and only when free; a rejected transfer
leaves the old reservation intact. The worker uses this before either immediate
or asynchronous path fallback. The reserved fallback chunk is pinned along with
the target and approach. An active job's authored approach cannot be changed;
release the job before changing its approach.

Release, completion, cancellation and queue clearing release reservations before
their callbacks. Worker removal releases its claim through the existing worker
lifecycle. Working/travelling agents stop if their claim or reserved destination
changes, and an old worker cannot report progress for a replacement claimant.
Camera culling does not release working claims or stop their simulation.

The job record persists `reserved_cell`; indexes are rebuilt, not serialized.
The default restore requeues claims so restored workers reclaim explicitly.
When `RequeueClaimedJobsOnLoad` is false, conflicting/blank saved claims are
requeued in stable job-ID order, preserving the first valid claim. Worker restore
must acquire its saved standing cell before resuming work.

This is work-position exclusivity, not a movement collision system, a route-time
reservation, or a material-stock reservation. Multiple queues do not share the
index. A shared workforce should use one queue with job-kind filters; global
cross-queue spatial arbitration and multi-worker work slots remain separate gates.
`terrain_job_reservations_probe.gd` covers exclusivity, fallback transfer, chunk
pins, reentrant callbacks, restore conflicts and coordinate arithmetic.
`terrain_worker_arrival_probe.gd` also checks actual worker reassignment and
rejection of an occupied fallback destination.

Verified on 2026-09-07: zero-warning/error C# build; all 55 actor-suite checks
passed, including the expanded worker and reservation probes. The separate
`terrain_build_approach_probe.gd` and `terrain_job_preflight_probe.gd` also passed.

## Hauling Demand

Assign `GridHaulerComponent.ChunkCellDataPath` to the same cell service used by
the hauler's navigation. A loaded hold pins its depot chunk even when delivery
is suspended or storage is full. The pickup chunk is additionally pinned while
approaching pickup. Pins are published before `HaulAccepted`, released when no
longer needed, and cached between state changes. They do not depend on the
camera. Rebinding releases the old service's pins; removal releases demand and
reattachment rebuilds it from the cargo and dispatch state.

With `GridCellArchiveComponent.AutoLoadPinnedChunks` enabled, archived endpoints
load through the existing bounded archive reader. An accepted haul waits with
`IsWaitingForTerrain == true` and no active movement until its current endpoint
is available, then requests the ordinary scheduled path. No terrain is invented
and no cargo is credited during the wait. Without an archive loader, or with a
missing archive, demand remains waiting and can be cancelled; inspect the
archive's error/completion signal for the loading failure.

Cancellation releases pickup demand but retains held cargo and depot demand.
Changing `DepotCell` while loaded suspends the old haul; call
`ResumeHaulToDepot` to request the new destination. Unloading the last cargo
releases endpoint pins. Configured unavailable depot terrain also prevents a
delivery retry from paying into the destination prematurely.

Snapshots include pickup coordinates and the terrain-wait flag. Actor restore
recreates the waiting dispatch without requiring a previously calculated route.
Standalone restore keeps the existing suspended-cargo policy until explicitly
resumed. `terrain_haul_demand_probe.gd` verifies real archive eviction/reload,
exactly-once delivery, cancellation, service rebinding, removal/reattachment,
depot changes and actor JSON restore during the terrain wait.

These are endpoint pins, not a generated travel corridor. Enable navigation's
`LoadMissingTerrain` for intermediate chunks encountered by scheduled searches.
Cargo-transfer networks and custom transporters still need explicit demand
policies. An empty `ChunkCellDataPath` supplies no endpoint demand;
standalone non-streamed scenes continue to use their already-resident terrain.

Verified on 2026-09-07: C# build completed with zero warnings/errors and all 56
actor-suite checks passed, including the disk-backed haul-demand probe and actor
restore while waiting for terrain. This does not qualify intermediate-route
streaming or a total-memory budget by itself.

## Navigation Terrain Demand

Enable `GridNavigationComponent.LoadMissingTerrain` for scheduled requests over
an archived finite world. This uses the navigation component's existing
`CellDataPath`; it introduces neither another world nor another generator.
The streaming lab enables it. Pair it with the existing archive's
`AutoLoadPinnedChunks` policy.

A scheduled search pins chunks as it examines terrain. Unavailable start/goal
or frontier chunks become demand, not passable defaults. If no route is found
while that demand is missing, the request parks outside the active search slots.
Once data arrives, the search restarts against current rules under the same
request ID; ordinary follower retry limits are not consumed by each chunk load.
A valid route through already-loaded data can finish without waiting for unrelated
frontier demand. Terrain edits also restart a streamed search against current rules.
Non-streamed requests retain their existing invalidation results.

`TerrainWaitingRequestCount` exposes parked searches. Up to 64 parked requests
are checked per processing call, sharing the normal scheduling loop.
`MaximumTerrainChunksPerRequest` defaults to 256 (1..4096); exceeding it reports
`terrain_demand_limit`. `TerrainRequestTimeoutSeconds` defaults to 30 (0.1..120)
from first search activation and bounds loading/restarts; expiry reports
`terrain_request_timeout`. Neither is reported as a proven `no_path`.
Cancellation, clearing or removing navigation releases that request's pins.
Each request has an independent pin owner, so cancelling one cannot unpin another.
The cell service retains owned snapshots of demand sets, allowing incremental
callers to extend their sets without corrupting shared pin counts.

After success, a streamed `GridPathFollowerComponent` pins its installed cell
route, including diagonal side cells used for traversal validation. Chunks are
released after their last required segment, retaining chunks needed by a later
return leg and the side cells while crossing a diagonal. `PinnedRouteChunkCount`
reports the remaining demand. Arrival, cancellation and removal release it.
Reattachment and saved moving-route restore
request a fresh route, allowing missing cells to load again. World-point-only
paths do not have cell-route demand. Synchronous `FindCellPath` does not perform
I/O or wait and still treats unavailable cells as non-traversable.

Searches retain explored chunks until the search ends; installed routes use a
last-use priority queue to retire travelled sections without rescanning the route
every frame. Limits are per request, not a global memory cap. Hierarchical
navigation, shared search caches and full-world memory qualification remain open.
`terrain_route_demand_probe.gd` checks three physically evicted intermediate
chunks, an archived water barrier, independent request cancellation, timeout,
chunk limits, actor removal, route pinning and reattachment.

Verified on 2026-09-07: zero-warning/error C# build and a passing 57-check actor
suite. After the final completion-handoff adjustment, the route-demand,
navigation-request (200 routes/lifecycle), follower-request and chunk-pin probes
were rerun successfully. Search pins now remain held throughout result callbacks
until the follower can install its own route pins.

`terrain_route_retirement_probe.gd` covers return legs, independent pin owners,
unloading passed chunks, diagonal side-cell lifetime, replacement, cancellation
and native `CharacterBody2D` movement. Native movement caps navigation velocity
at the next waypoint using the engine integration timestep, preventing fast
units from oscillating around a waypoint. It retains `MoveAndSlide` collision
handling and shared ability velocity arbitration. See Godot's
[movement timestep documentation](https://docs.godotengine.org/en/stable/tutorials/physics/physics_introduction.html).

Verified on 2026-09-07 after route retirement and the native arrival fix: C# build
with zero warnings/errors and all 58 headless actor/streaming checks passed.
The native-body regression also blocks against a physics obstacle and resumes
after its removal. This is functional verification, not full-detail FPS or a
whole-world byte-budget qualification.

## Surface Streaming

`TerrainShaderSurface.Fill` attaches `TerrainSurfaceStreamingComponent` at runtime
when a uniform shader surface exceeds 65,536 cells. Smaller maps and editor-authored
surfaces retain their existing behavior. The streamer keeps the shader's existing
coordinate origin; it does not replace the material or modify shore/biome contours.

The streamer places no tiles. The surface shaders read only `VERTEX`, so it draws one
quad per resident chunk into a single `Polygon2D` child of the layer (`SurfaceGeometry`,
positioned at the first cell centre - the origin a one-quadrant TileMapLayer hands its
shader) and one whole-map quad at overview zoom. Residency is exact on every update: the
set of chunks the viewport spans is recomputed each frame and the mesh is rebuilt only
when that set, or the overview decision, changes. There is no per-frame mutation budget
and nothing pending.

- Default chunk width: 32 cells (minimum 8).
- Preload margin: one chunk around the camera viewport.
- Camera rotation and partial map-edge chunks are handled. Chunk outlines come from the
  corner cells' native `MapToLocal` centres pushed out by half a tile, so adjacent chunks
  share edges exactly and their union is the overview outline.
- Square TileSets and diamond-down, horizontal-offset isometric TileSets - the two
  layouts `TerrainTileSets.Create` produces - are supported. Any other layout fails
  `Configure` with an error naming it rather than drawing a wrong outline.
- Switching away from a renderer, or hiding it, retires its surface geometry.
- Offscreen jobs, actors, collision and simulation are not disabled.
- The overview replaces the chunk quads when a cell occupies at most `OverviewCellPixels`
  screen pixels (default 4), with the same material and coordinate origin. Zooming back
  above 1.5 times the threshold returns to chunk quads the same frame, so there is no
  interval with neither; the 1.5x hysteresis stops a camera resting near the threshold
  from flipping. `EnableOverview` disables this policy.
- Measured on a 1024x1024 square surface in a 1280x720 viewport, panning one chunk per
  frame: the previous per-cell tile design left 4,096 of 16,384 wanted cells resident and
  needed eight updates to fill a camera jump; the quad design keeps all 16,384 resident
  throughout and covers a jump in one update, at 0.2-0.4 ms for an update that changes
  residency and under 20 us for one that does not. The process's first update costs
  about 45 ms of runtime warm-up (233 us for the same update after a reconfigure).
- The native OpenGL probes render the chunk quads and the overview quad from the same
  camera at zoom 1 and require zero differing pixels; a one-pixel inset or outset of the
  chunk outline trips them. Captures are under `tests/output/surface_overview` and
  `tests/output/iso_surface`. Headless runs cover million-cell residency, zoom
  transitions, camera jumps, hidden layers and finite edge chunks.
- Detailed props, actor presentations and shader-field textures are not simplified by
  this overview. Full huge-world memory/FPS qualification remains open.
- Terrain field textures and generated data remain whole-map allocations.

This is rendering residency, not a claim that a million-cell generated world is
already production-ready. Keep terrain generation limits unchanged until the
data pipeline has been partitioned and measured. Shader surfaces retain one
rendering quadrant for coordinate correctness; the streamed geometry is one canvas
item beside it, not a new GPU batching architecture.

Navigation now caches scene-discovered optional sources, including absent ones.
Relevant node additions/removals and scene changes invalidate discovery. Explicit
NodePaths still resolve per query, so changing or replacing an authored source
does not leave the old node attached.

## Generation Scaling Work

Live `GridCellDataComponent` storage and staged publication now partition records
into sparse 32x32-cell chunks. Lookup uses floor-divided chunk coordinates, including
negative cells, and each inner dictionary contains at most 1,024 entries. There is
still one authoritative live store, not a second world. `StoredChunkCount` and
`GetStoredChunks()` expose allocated chunk coordinates; `GetChunkCells(chunk)`
returns a detached snapshot of only that chunk for incremental save consumers.
Existing full-map APIs remain available; enumeration order is not a contract.
Chunk-local snapshots can be restored with `LoadCells(snapshot, false)`.
The existing snapshot parser reserves int.MinValue coordinates as invalid, although
raw store lookup supports them. No simulation records are evicted by camera culling.
Automatic unloading and memory residency limits remain future work.

### Cell Chunk Archives

`GridCellArchiveComponent` saves and restores one 32x32 logical cell chunk per file.
Assign CellDataPath and an explicit world-specific ArchiveDirectory, for example
`user://worlds/my_world/cells`. No shared default directory is assumed. SaveChunk
and LoadChunk take chunk coordinates, not individual cell coordinates; negative
coordinates use the same floor-divided partition as the live cell store.

Archive files use the existing typed cell snapshot codec, preserving flags, crops,
water patches and portable metadata. Saves validate/encode the complete chunk,
write and flush a same-directory temporary file, then replace the destination.
Loads enforce a byte limit, validate the snapshot and expected coordinate in a
detached store, and replace only that chunk after validation. Neighboring chunks
remain intact. An empty saved chunk removes any later records in that same chunk.
Failed saves preserve the previous file; failed loads leave the live map unchanged.
Both APIs return false with LastError for handled file/format failures. The default
file limit is 8 MiB, configurable between 1 KiB and 64 MiB.

The underlying CaptureSingleChunkState/RestoreSingleChunkState APIs are also
available on GridCellDataComponent. A successful restore advances terrain and
navigation revisions and emits one CellsChanged event, including for empty chunks.
The full-world CaptureChunkState/RestoreChunkState format is unchanged.

SaveChunk and LoadChunk are synchronous, explicit snapshot I/O, not automatic residency
management or a crash-consistent multi-chunk world checkpoint. Call them on the
main thread. EvictSavedChunk is a separate opt-in operation described below.
The readiness API protects navigation and placement; automatic unloading still
requires complete demand coverage and simulation residency.
Generation fields, actors, occupancy and jobs
are not stored in these cell-only files. Budgeted snapshot encoding/publication and a world-level manifest
remain required before using this as the huge-world streaming backend.

`tests/terrain_chunk_archive_probe.gd` writes actual files and checks negative
coordinates, typed metadata, neighbor isolation, single publication, rejected
truncated/wrong-coordinate/oversized data, preservation of previous files, empty
chunk restoration, temporary-file cleanup and the full snapshot codec.

### Nonblocking Chunk Reads

GridCellArchiveComponent.RequestLoadChunk(chunk) starts one bounded read on a
worker and returns a positive request ID, or zero when rejected. Observe
ChunkLoadFinished(requestId, success, error), IsLoading and PendingLoadId. The
worker reads the file into a pooled buffer, parses the envelope from its UTF-8
bytes with System.Text.Json's Utf8JsonReader (the payload's Base64 decodes
straight to bytes; the generic path made a 200 KB string of the document and
another of the Base64 first), decodes the cell array and builds the records. Only
the path, byte limit, coordinate, default terrain kind and cancellation token enter
it, and it returns records that reference nothing live. Publication - replacing the
chunk and emitting CellsChanged - stays on the main thread, as do the completion
checks: a newer local edit, a day advance, a target change or cancellation still
discard the result. Decoding 1,024 records used to be the main thread's per-reload
cost; measured on a 32x32 chunk whose every cell carries nested metadata, the
request takes about 1 ms and the completion pumps a few more, and the capture
probe holds those bounds (a mutation that decoded on the main thread measured
90 ms in the request). On a 320x256 generated world the asynchronous reload's
main-thread share is 0.2 ms median and under 8 ms worst, publication lands within
about nine frames, and the renderer's re-sample of the chunk costs 4 ms; the
synchronous LoadChunk, which decodes on the main thread by design, costs 64 ms
per chunk. The node processes completed reads automatically while inside the
running tree.

One request may be outstanding per archive node, shared between reads and writes.
IsBusy reports that shared slot. Additional requests and synchronous
SaveChunk/LoadChunk calls return archive_busy until it drains. CancelLoad cancels
publication immediately but retains the busy slot until the worker completes.
Detaching the node cancels its request and suppresses that request's completion
signal, including after reattachment. Completion handlers can request another load.

A pending read is rejected if its chunk is edited, the world advances a day,
bulk cell data changes, or the target cell service/directory/default terrain
changes. An unrelated single-cell edit in a neighboring chunk does not invalidate
it. The captured content revision also rejects changes away and back to the same
default terrain. Failures leave live cells and readiness unchanged. This protects the request
window only; it does not determine whether a file was already stale before the
request. Synchronous LoadChunk remains an explicit unconditional restore.

Archive nodes retain their individual slots and now also use the shared admission
budget below. Encoding and decoding run on the worker; publication is one chunk
replacement on the main thread.
Full demand coverage, total-memory budgets and a world checkpoint manifest remain
required.

### Shared Archive Admission

All archive instances in the process share operation and detached-payload limits.
Admission happens before snapshot capture or worker creation; there is no queue
of already-allocated byte snapshots. This is an internal service, not another
world node or generator. Configure custom project settings before the first
archive operation (settings are read once per process):

```ini
[beep]
streaming/archive_max_operations=2
streaming/archive_max_inflight_bytes=33554432
```

Defaults are two operations and 32 MiB. The operation setting clamps to 1..64;
the byte setting clamps to 1 KiB..1 GiB. A read or write conservatively reserves
its configured `MaximumChunkBytes` envelope. Eviction verification reserves one
envelope for the saved file it reads back; it compares that file's hash with the
hash of the bytes this archive wrote, not a fresh encode. This policy also
covers synchronous `SaveChunk`, `LoadChunk` and `EvictSavedChunk` calls.

An occupied shared limit returns `archive_io_busy` (zero request ID or false),
before doing the operation. An envelope larger than the configured total returns
`archive_io_request_too_large`; lower the per-chunk limit or increase the shared
setting before startup. Explicit callers retry rejected operations themselves.
Automatic demand and residency policies use their existing delayed retry paths.
Accepted saves retain their original request-time snapshot semantics.

`SharedIoOperationLimit`, `SharedIoByteLimit`, `SharedIoActiveOperations` and
`SharedIoReservedBytes` expose the same process-wide counters from every archive.
Reservations remain held through worker completion and main-thread publication
or discard. Cancellation alone does not release them early. Detached/freed nodes
drain their worker results and clean up temporary files without requiring another
frame callback, then release their reservation. Normal completion releases before
the completion signal so callbacks can request new work.

The multi-archive probe covers operation and byte saturation, synchronous bypass
prevention, impossible requests, capture failures, cancellation, permanent removal,
reattachment and automatic retry. This is admission control, not a fair global
priority queue or a total RAM limit. Transient encoding/decoding allocations,
oversized inputs rejected after encoding, persistent cell/generator data and
native rendering resources are not bounded by this detached-payload reservation.
Encoding runs on the worker (see Nonblocking Chunk Writes); global request
fairness and complete memory qualification remain open.

Verified on 2026-09-07: zero-warning/error C# build and a passing 60-check
headless actor/streaming suite. The shared-admission probe was then strengthened
to isolate operation saturation from byte saturation and cover permanent reader
removal and eviction admission; its final version passed separately.

### Loading Pinned Demand

GridCellArchiveComponent.AutoLoadPinnedChunks is opt-in and defaults to false.
When enabled, the existing archive scans its bound cell service's pins at most
every 100 ms while idle. It starts one asynchronous read for an unavailable,
pinned chunk, sharing the same slot as explicit reads and writes. Available
chunks and unrequested chunks are untouched. Actor, job and structure demand
all use the existing cell-service pins; no player avatar or second world is needed.

Eligible coordinates rotate in stable coordinate order. Failed requests receive
a per-chunk DemandRetrySeconds delay (default 2, clamped 0.25..60); a missing file
does not prevent later requests. Expired demand is pruned from retry tracking,
and a different cell service/archive directory resets that tracking. A missing
archive is reported through the normal completion signal, not silently replaced
with generated/default terrain.

Automatic results are discarded if demand disappears, the chunk becomes ready
elsewhere, the target changes, or automatic mode is disabled before publication.
Explicit RequestLoadChunk/LoadChunk retain their deliberate restore semantics.
Detachment cancels work; reattachment resumes scanning. ProcessChunkDemand supports
explicit pumping, separate from ProcessPendingLoad and ProcessPendingSave.
Decoding runs on the worker; publication is one chunk replacement. Multiple archive nodes
share the admission limits above. The optional residency policy below selects
saves and evictions; total byte-level memory limits remain unfinished.

The demand probe checks opt-in behavior, requested-only restoration, delayed
missing-file retries, lost-demand cancellation, manual restore independence,
read revision validation, disabling and reattachment with real archive files.

### Automatic Residency Limits

GridCellArchiveComponent.AutoEnforceChunkBudget defaults to false. When enabled,
MaximumResidentChunks (default 256, clamped 1..65,536) is a soft limit on stored
cell chunks, not on process RAM. `MaximumResidentCells` adds an independent exact
stored-record limit; zero (the default) disables this additional limit. Either
limit can trigger retirement. `ResidentChunksOverBudget` and
`ResidentCellsOverBudget` report excess retained chunks and records respectively.
`GridCellDataComponent.GetStoredChunkCellCount` is an allocation-free count lookup,
including for sparse and negative-coordinate chunks. Camera/actor/job/structure pins and crop/water simulation always win over
the target, so pressure never forces required records out of memory.

While nothing is over budget the archive re-checks every 100 ms. While over
budget it runs the retirement pipeline continuously: as soon as it is idle it
scans and requests one save; in the frame that write publishes it verifies and
releases the chunk; the next frame it requests the next one. It waits 100 ms only
after a scan finds nothing eligible or a step fails. Each scan rotates through
stored coordinates and tests at most 128 candidates. Under cell pressure, it
chooses the densest eligible chunk in that bounded scan to release more records
per write; a pending candidate remains stable until its save can be checked.
Candidate enumeration
and sorting still visit stored chunk keys; this is not a millisecond budget.
Pinned-demand reads are checked before budget work and share the same I/O slot.
Unsaved/currently edited data must save successfully before eviction. The current
revision, the disk file's hash and eligibility are rechecked before removal. A new
edit after save completion delays that candidate so it cannot monopolize writes.
Write/verification failures receive a two-second retry delay while other eligible
chunks can proceed. A changed/corrupt external archive is not overwritten merely
to force an eviction; its live records remain resident.

CanEvictChunk exposes cell-service eligibility only, not permission to discard
unsaved data. EvictSavedChunk remains the verification gate. Disabling the budget
cancels its pending save without cancelling independently requested saves, and
does not unload anything further. ProcessChunkBudget supports explicit pumping.
The policy does not pause simulation, remove archive files, or introduce a new
world coordinator. Each archive requires an exclusive world-specific directory.

The budget probe checks opt-in behavior, save-before-release, edits during save
completion, protected pressure, ongoing crop growth, recovery after demand ends,
coexistence with automatic demand loading, failure isolation, disabling while
a write is pending, and that a published budget save is verified and released in
the same frame rather than at the next 100 ms tick. The cell-budget probe additionally checks sparse-versus-dense
selection, exact eviction/reload counts, protected excess records, changing limits
during a save, edit preservation and cleared-world accounting. The streaming lab
authors limits of 16 chunks and 8,192 cells and displays both pressure counters.
Chunk sizes and payloads vary: accounting for bytes, generator
fields, native resources and multiple archive instances remains future work.

Verified on 2026-09-07: zero-warning/error C# build, all 59 headless
actor/streaming checks, and the native streaming-lab probe passed. Desktop
(1280x800) and narrow (768x800) captures were inspected: both residency counters
fit below the map without overlapping the toolbar or worker status. These are
stored-record limits, not a claim that generator or rendering memory is bounded.

### Camera Data Demand

GridCameraDemandComponent binds GridPath and CellDataPath to existing components.
Author BoundsCells explicitly; an empty rectangle requests nothing. It uses the
grid's actual viewport/canvas transform, so the active Camera2D, SubViewport,
rotation, nonuniform zoom and transformed map parents are respected. No separate
camera path or assumed player character is required. Top-down and isometric
projection use the existing WorldToCell conversion.

Visible cell bounds include a one-cell picking margin and PaddingChunks (default
1, clamped 0..4), clipped to the finite map rectangle. Demand is a conservative
rectangle in chunk space, not exact polygon culling. Elevated terrain currently
pins the entire authored bounds because screen-corner picking cannot prove its
visible footprint. Invalid/singular transforms and demands exceeding 65,536 chunks
retain previous valid demand and return false. Fully off-map views release camera
demand. Stationary chunk bounds reuse the current pin set without allocating it
again; reattaching the cell service recreates missing owner pins.

Enabled=false, rebind and tree exit release only this component's pins. Other
actor/job/building pins remain independent. Reattachment resumes demand updates.
RefreshDemand supports explicit updates after changing scene properties.
With AutoLoadPinnedChunks enabled on the archive, camera pins restore unavailable
chunks automatically. Without an archive they still protect resident data from
explicit eviction. The actor lab authors this component for its 48x32 map.

This component does not sleep actors, change gameplay ticks, or evict records.
Without an overview binding, a full-map zoom-out requests the full bounded cell
region. Bind OverviewSurfacePath to the existing TerrainSurfaceStreamingComponent
for this same world to avoid that demand while its overview is displayed. The
binding must represent the same map/projection placement; it is not inferred from
unrelated renderers. CanSupplyOverview requires requested overview mode, a visible
overview in the grid's viewport, zero-origin bounds and an exact map-size match.
Hidden, missing and mismatched surfaces leave normal camera demand in effect.

IsUsingOverview reports the active handoff. Only the camera's pins are released;
actor/job/building demand continues to load and protect simulation data. The
resident-chunk budget may then retire idle detailed records. On zoom-in, camera
demand resumes as soon as detailed rendering is requested, even while the old
overview stays visible to cover pending geometry. No second world is generated.
This uses the existing uniform shader surface overview; other renderer families
still need equivalent coarse representations and explicit integration. Generator
fields remain resident and byte-level memory limits are still unfinished.

The camera demand probe uses a real Camera2D/SubViewport, transformed
top-down/isometric grids, negative bounds, extreme zoom, independent pins,
rebinding/lifecycle checks and an actual automatic disk reload. The surface
streaming probe additionally tests overview demand release, hidden/mismatched
bindings, simulation pin isolation, automatic save/eviction and zoom-in disk
reload. Its native rendering run checks nonblank detail/overview pixel agreement.

`tests/terrain_chunk_loading_probe.gd` verifies real asynchronous reads, busy
rejection, cancellation, local versus neighboring edits, day/bulk invalidation,
rebinding, reattachment, missing files and reentrant completion requests.

### Nonblocking Chunk Writes

RequestSaveChunk(chunk) copies request-time records from one available chunk on
the main thread - that copy is the main thread's whole share. A worker then
encodes the copies into the same snapshot document the synchronous path writes,
hashes the bytes, and writes and flushes a uniquely named same-directory
temporary file; ProcessPendingSave publishes it. The copies are private to the
capture (metadata is a deep duplicate; nothing references the live store), which
is the single-owner use of Variant containers Godot's thread-safety rules allow.
The encode used to advance on the main thread at 32 cells a frame; on a 1024x1024
world that was about 75 ms of main-thread work per chunk and, with a second full
encode at eviction and a 100 ms wait between steps, 38 chunks retired in 15 s.
The same harness now retires 300-350 chunks in 15 s with a worst frame near
50 ms. Three allocation costs were found on the way and removed: every string or
container handed to a snapshot dictionary is a Variant owning native memory
through a finalizable Disposer unless disposed (about fifteen per record, 11% of
the earlier sampled trace), so the record encoder disposes each one; the document
used to pass through a Base64 string, a Json.Stringify string and a UTF-8 array -
about 800 KB of large objects per chunk whose gen2 collections over a 200 MB live
world were 100-200 ms main-thread stalls - so the fixed JSON envelope is written
straight into a pooled buffer; and the one large array that remains per chunk,
the VarToBytes payload of 100-300 KB, no longer reaches the large-object heap
because `Beep.Godot.csproj` raises the runtime's `System.GC.LOHThreshold` to 1 MB
(measured first with the environment knob: worst frame 214 ms to 69 ms). What
remains is the packet format itself: one Godot Dictionary per cell is about
700 KB of managed garbage per chunk, collected in gen0. A packet without per-cell
Variants is the next lever and is a format decision.
It returns a positive request ID, or zero for a rejected request (including
unavailable chunks and invalid setup). Encoding and payload-size failures arrive
through completion. ChunkSaveFinished(requestId, success, error), IsSaving and
PendingSaveId expose completion. Load and save IDs share one increasing sequence
per archive node. All public archive APIs must be called on the main thread.

The worker holds only the detached bytes, destination path and cancellation token.
It never replaces the destination. ProcessPendingSave, called by normal Godot
processing, validates the bound cell service/directory/default terrain and approves
the final file replacement on the main thread. CancelSave prevents that approval
while the request is pending, including when the worker has already finished
writing. The slot stays busy until drained. Failure or cancellation discards the
temporary file and preserves the previous destination. Save completion callbacks
may start another read or write after the old slot is released.

Tree exit cancels the pending save and suppresses its completion signal. Temporary
file cleanup continues without Godot calls even if the node is permanently freed.
Reattachment cannot start another operation until cancelled work has drained.
Cleanup is best-effort if the filesystem itself refuses deletion; it only ever
targets that operation's unique temporary path, never scans/deletes other files.

Saves preserve the point-in-time snapshot taken by RequestSaveChunk. Later live
edits are neither lost nor included in that file. The saved-content check below
reports whether live data still matches that snapshot; success alone does not
make a chunk eligible for eviction. A streaming coordinator must respect resident
demands and implement an atomic retirement policy. The initial record copies, the
final rename and publication remain on the main thread; encoding, hashing, decoding
and the file I/O are the worker's. Synchronous SaveChunk still captures whole
chunks; verified eviction reads the file back and hashes it. It does not provide
crash-consistent multi-chunk saves or world checkpoint recovery.

`tests/terrain_chunk_saving_probe.gd` checks actual files, shared-slot rejection,
deferred replacement, request-time typed snapshots, later edits, cancellation,
target changes, reattachment/permanent destruction, unavailable and oversized
snapshots, worker and final-replacement failures, negative chunk coordinates,
normal frame completion and reentrant save-to-load requests.

`tests/terrain_capture_budget_probe.gd` checks multi-frame capture, request-time
record/metadata isolation, cancellation, target rebinding, late payload rejection,
preservation of an existing archive, and shared admission cleanup on destruction.

### Saved Content Revisions

GridCellDataComponent.GetChunkRevision(chunk) returns a process-local content
token independent of terrain rendering and pathfinding revisions. Single-cell
changes, terrain fills, additive loads and single-chunk restores invalidate only
the affected chunks. Full cell replacement, committed generation and changes to
DefaultTerrainKind invalidate every chunk, including implicit empty/default
chunks. Tokens are not reused after world replacement, serialized into saves,
or comparable between different cell service instances. Setters may conservatively
advance a token even when the caller writes the same value.

Farming mutations are included. AdvanceDay invalidates growing crops and cells
whose Watered flag expires, but leaves dry, uncropped chunks unchanged. Water
expiry on an uncropped cell now emits CellChanged as well. Content tracking is
updated before crop-maturity callbacks can observe the change. Readiness alone
does not change content tokens because SetChunkAvailable retains the records.

Metadata dictionaries and arrays are copied deeply at the public SetMetadata
and GetMetadata boundary. To edit a returned container, call SetMetadata with
the updated value; mutating the returned container is not a live edit. This
prevents caller-owned nested containers from bypassing content/change tracking.
Live Godot objects remain unsupported by the portable archive codec.

GridCellArchiveComponent.IsChunkSaveCurrent(chunk) compares its last successful
save's captured token, cell service identity and destination path with live data.
It returns false for unavailable chunks or edits made during an async write, even
if that point-in-time write completed successfully. An unrelated chunk edit does
not dirty the saved chunk. Failed/cancelled saves do not claim newer data is saved.
The same check is updated by synchronous SaveChunk. Loads conservatively change
the content token and do not declare themselves current saves.

This query performs no file I/O and trusts this archive node's own completed
writes. It does not detect external file edits/deletion, another archive node's
writes or filesystem corruption. Use one archive owner per world and call
ForgetSavedChunks after external storage changes. Checks are not persisted across
application restarts, are not a checksum, and do not authorize eviction on their
own. Snapshot/cell retirement, full demand coverage and bounded residency remain work.

`tests/terrain_chunk_revisions_probe.gd` covers editing/farming APIs, neighbor
isolation, water expiry, nested metadata isolation, readiness, bulk/single restores,
default changes, rebinding and saved-token invalidation. The async save probe
checks edits during writing versus unchanged saves; the world-generation probe
checks that cancelled/staged work leaves tokens unchanged until publication.

### Chunk Readiness

GridCellDataComponent exposes SetChunkAvailable(chunk, available),
IsChunkAvailable(chunk), IsCellAvailable(cell) and UnavailableChunkCount. All
chunks start available for ordinary sparse/default-terrain maps. A streaming
coordinator must explicitly mark a chunk unavailable before loading or retiring
it. This API changes readiness only: it does not free records or stop resident
simulation. Crops continue advancing, and edits to retained records remain live.
Do not restore an old archive over concurrent edits without a pin/revision policy.
SetChunkAvailable now returns false when asked to make a pinned chunk unavailable;
otherwise it returns true, including when readiness already has the requested value.

Readiness changes advance navigation/terrain revisions once and invalidate pending
routes. GridNavigationComponent rejects unavailable start/goal cells, adjacent
steps, diagonal side cells and traversal costs even when blocked-start/goal or
blocked-flag overrides are enabled. GridPlacementComponent rejects unavailable
footprint cells independently of terrain allow-lists. Custom consumers must check
IsCellAvailable; existing getters still expose retained/default data and are not
an availability test. Direct physics controllers and native navigation meshes are
not constrained by this API yet.

A successful single-chunk publication marks exactly that chunk available. Full
cell loads, generated-world publication, clear and full snapshot restore reset
readiness for the replaced world. Failed publication does not clear readiness.
CaptureChunkState refuses a full snapshot while any chunk is unavailable, and
single-chunk capture refuses that chunk, preventing missing data from overwriting
an archive. Readiness is transient and is not serialized as complete terrain.

`tests/terrain_chunk_availability_probe.gd` checks override-resistant path/placement
rejection, pending-route invalidation, diagonal boundaries, negative coordinates,
idempotent notifications, preserved crop simulation, publication transitions and
rejection of incomplete full saves. Automatic eviction and memory limits remain
unimplemented; these gates are prerequisites, not a completed streaming system.

### Actor, Job and Structure Chunk Pins

GridCellDataComponent.SetChunkPins(owner, chunks) replaces one live scene node's
complete demand set. Duplicates are coalesced; overlapping owners retain separate
counts. ReleaseChunkPins(owner) or the owner's tree exit releases that owner's
set only. IsChunkPinned, GetChunkPinCount, HasChunkPins and PinnedChunkCount expose
the current demands; GetPinnedChunks returns a detached list for a future loader.
The cell service disconnects owners and drops transient pins on its own tree exit.
Pins are not saved data and do not alter content, render or navigation revisions.

A pin prevents SetChunkAvailable(chunk, false), but never marks missing data
available. Pinning an already-unavailable chunk records demand for the loader;
it does not generate or read it. Readiness true and validated chunk publication
remain allowed. Full-world replacement is an explicit separate operation and is
not blocked by pins. These APIs are main-thread only.

On ActorRegistryComponent, assign ChunkCellDataPath and ChunkGridPath explicitly.
Each resident actor pins its projected current chunk plus ActorChunkRadius chunks
in each direction (default 1, clamped 0..4). The default is a 3x3 neighborhood, not
a sprite-sized calculation; author the radius for the game's movement and actor
size. Registering, waking, restoring and synchronizing position refresh demand;
unregistering, ambient sleep and scene exit release it. Shared movers already
synchronize position; custom moves/teleports must call Actor.SynchronizePosition
before callbacks that can act on residency. Radius/binding changes refresh live
actors. Ownership, selection, visual culling and actor inactivity do not remove
scene residency demands. Cached demand sets avoid reallocating them while an
actor stays in the same chunk/radius. Invalid projected coordinates retain the
previous set rather than create a bogus sentinel chunk.

On GridJobQueueComponent, assign ChunkCellDataPath. Queued and claimed jobs pin
their target and approach chunks; completed/cancelled jobs do not, even when
retained for history. Adding jobs updates pins before JobAdded callbacks. Queue
clear/load, approach edits, rebind and reattachment rebuild demands from the
existing job records, not a second job store. Both components expose
RefreshChunkPins for custom lifecycle work, including reattaching the cell service
without reattaching its users. The worker lab authors both bindings.

GridObjectComponent.ChunkCellDataPath binds a placed object's entire logical
Cell/Footprint rectangle to terrain residency, independent of BlocksNavigation,
occupancy flags, visibility and construction completion. Placement propagates its
existing CellDataPath. For directly registered construction sites, author
GridBuildSiteComponent.ChunkCellDataPath; registration supplies a GridObject when
absent, before material-waiting or job-created callbacks. The placed object owns
the pins after construction finishes, not the build coordinator or job record.
Cell/footprint edits, Configure, RestoreState, rebinding and reattachment refresh
demand; exit releases it even when ReleaseReservedFootprintOnExit is false.
RefreshChunkPins also supports explicit cell-service reattachment. Demand is
computed per chunk, not per footprint cell. Out-of-range cell endpoints or more
than 65,536 chunks per object report an error and retain previous pins; they are
invalid authoring, not supported world-scale structures.

This is not complete residency scheduling. Actor pins do not reserve entire
future routes, and structure pins do not cover remote depots, cargo routes or
all custom simulation dependencies.
Unregistered physics objects need explicit owner pins. Dormant ambient records
do not retain scene pins. Automatic loading, demand prioritization, actual cell
eviction scheduling and reduced economic/combat simulation remain unfinished.

### Verified Idle-Chunk Eviction

GridCellArchiveComponent.EvictSavedChunk(chunk) physically removes that chunk's
cell records only when this archive has a current save (the chunk's revision is
still the one that save captured), no read/write is pending, the disk file still
hashes to the bytes that save wrote, no live owner pins it, and it contains
neither crops nor watered cells. Calls during the cell day-simulation loop are
rejected. LastError explains refusal; rejection leaves records intact. The
verification reads the file back and hashes it; it does not re-encode the chunk,
which cost a second full main-thread encode per eviction. It relies on the cell
service marking its chunk revision on every mutation, which is what makes
revision equality mean content equality. This is synchronous bounded
verification, not a per-frame scheduler. No disk file is deleted. Applications
must retain exclusive ownership of their world archive; external writes after
verification cannot be prevented by the component.

EvictedChunkCount/IsChunkEvicted expose the detached data state. Eviction advances
render/navigation revisions, not content revisions, and emits one CellsChanged
after removal/readiness changes. SetChunkAvailable(true) cannot expose an evicted
chunk: a validated synchronous/asynchronous load or explicit whole-world replacement
must supply its data. Cell and bulk-paint edits into evicted chunks throw before
creating partial records; additive snapshot loads prevalidate their affected cells.
Default terrain cannot change while archives are detached except through whole-world
replacement. Clear, full restore and generation clear transient eviction markers.
Read-only cell getters still return defaults for absent records: custom consumers
must check IsCellAvailable and pin their simulation dependencies.

Crops and water expiration remain fully resident; custom economic/combat records
must use owner pins. This does not yet impose a memory budget, evict generator
fields, or implement reduced offscreen simulation. The new eviction probe verifies
physical record removal, dirty/pinned/crop/water refusal, altered-file protection,
atomic edit rejection, negative coordinates and synchronous/asynchronous reload.

`tests/terrain_chunk_pins_probe.gd` verifies shared owners, deduplication, detached
enumeration, release/replacement, unavailable demand, readiness rejection, job
callbacks/approaches/history/load, actor movement, padding, sleep/wake, negative
coordinates, transformed isometric projection, cross-chunk structure footprints,
placement bindings, nonblocking construction, resizing, restore, rebinding and
lifecycle cleanup. The construction-effects probe also checks terrain residency
before materials arrive, after job completion and after demolition.

`GridWorldStateComponent` now writes snapshot version 2 with a `cell_chunks`
payload instead of the old `cell_data` array. There is no version-1 compatibility
path. `CaptureChunkState()` serializes each allocated 32x32 chunk independently
using Godot Variant bytes encoded as base64. Outer JSON saves therefore preserve
cell coordinates, vectors, colors and nested metadata without string coercion.
Live objects, callables, signals and RIDs are rejected rather than serialized.

`RestoreChunkState()` validates every chunk into a detached store before replacing
live cells and emitting one revision/change notification. Invalid encoding,
duplicate chunks/cells, non-integer coordinates and misplaced records leave the
live cell store unchanged. This atomicity covers cell data, not the entire world
snapshot's roads, objects and jobs. Capture and restore are still synchronous and
retain the complete encoded payload; this is not bounded-memory disk streaming.
The water-surface smoke probe covers JSON round trips and rejection paths, while
the world-ownership probe checks independent snapshots from separate world nodes.

### Actual Huge-Field Benchmark

Run `tests/terrain_huge_generation_probe.gd` explicitly with the Godot console
executable in headless mode. It uses the real 1024x1024 generator with seed 31415,
65% island footprint, climate maps, scale rules, lakes, rivers, resources and six
starts. The existing sampling fallback produces 2048x2048 fine samples (two per
cell), exceeding the nominal 1.25-million sample ceiling; that ceiling is not a
hard memory limit. The benchmark does not publish gameplay nodes or render a map.

On the development machine on 2026-09-07, the initial run took 54.0 seconds and
peaked at 778 MiB process working set. Feature ranking cleared/scanned a whole-map
mask for every 8x8 block. Replacing that mask with a reusable 64-value buffer and
sorting once per block reduced the observed feature phase from about 17.4 seconds
to 0.4 seconds. The rerun took 37.5 seconds with a 793 MiB process peak: a CPU
improvement, not measured memory improvement. Stage times are sampled every 100ms,
so short stages may be combined or omitted; timings are not benchmark guarantees.

Both runs produced 608,963 dry cells, 439,613 water cells, 153 feature cells,
28,007 surface-resource cells and six distinct dry starts. Tests verify finite
terrain/elevation/shading and exclude woods/jungle from water. Sparse feature
coverage and visual/topological suitability still need review; these counts do not
prove a good gameplay map. The report is written to
`tests/output/terrain_async/huge_generation.json`. Erosion remains about 16 seconds.
Bounded-memory generation and final publication remain unimplemented.

A subsequent erosion optimization precomputes the fixed drainage factor once,
retains height-ordered incision, and visits independent diffusion outputs in row
order for memory locality. The reference test compares height bits across three
fixtures and strengths 0, 0.25, 1 and 4. The same huge-field benchmark then measured
28.3 seconds total, 6.7 seconds for erosion and 776 MiB peak working set, with the
same dry/water counts. These single-run results establish progress, not a sustained
FPS or memory-budget guarantee. The worker still constructs whole-map arrays.

Generation scratch arrays now allocate on first stage use instead of all at buffer
construction. The unused sample-level continent array was removed; gameplay
continent IDs remain unchanged. Coast-distance storage is released after river
generation, and elevation/temperature/moisture/relief scratch is released before
finished-field packing. Access after release fails explicitly rather than silently
allocating blank replacement data. Shading remains output data, initializes to 1,
and is not discarded with scratch. This reduces buffer-rooted memory lifetimes;
actual collection is controlled by the CLR, and no forced collection is used.
The scratch lifetime test checks lazy allocation, release accounting, rejected stale
access and preservation of output arrays. Large-map peak RSS still needs measurement
after the remaining whole-map stages are partitioned.

Generated gameplay cells keep fixed terrain metadata in typed managed records,
without a native Godot Dictionary per tile. A native dictionary is allocated only
when metadata is explicitly edited; those values override the generated defaults.
`GetMetadata`, shore queries and snapshots expose the same values and key presence.
Snapshot dictionaries are materialized separately and deeply isolate custom data.
The compact-cell test checks zero live native dictionaries after generation and
snapshot reads, per-cell allocation on edits, save/load parity and shore clearing.
Loading arbitrary saved metadata still uses dictionaries. This removes a native
allocation source during publication, not all per-cell objects or whole-map stores;
it has not yet been qualified as a bounded-memory million-cell live world.

Published fine terrain labels now use immutable 32x32-sample `TerrainSampleKinds`
chunks instead of retaining one string reference per sample. Uniform chunks store
one label; mixed chunks store a local string palette and 16-bit indices. Lookup is
lossless, including partial map edges; sample density and all coastline/biome
decisions remain unchanged. This is resident field compression, not streamed
generation: the temporary generator label array still exists during packing, and
water/shade arrays remain whole-map allocations. Payload diagnostics exclude CLR
array/object headers and the shared string objects; they are not peak-memory metrics.
`TerrainWaterSurfaceSmoke` verifies exact labels on a million-sample fixture,
source-array isolation, partial chunks and palettes exceeding 256 labels, alongside
the existing fine-water placement tests.

The flat `TerrainFeatureRendererComponent` now uses camera-based residency above
65,536 cells. It evaluates at most 256 cells per update by default in 32-cell
chunks, uses local live-water queries, and reuses the full renderer's scatter and
prop-size rules. It retires distant/hidden stamps and invalidates neighboring
chunks after a live cell edit. This does not suspend gameplay. Stamp sorting and
asset loading are not millisecond-budgeted, and an overview showing the whole
map can still request the whole prop population. The flat rock renderer now uses
the same `TerrainPropResidency` scheduler with its own art and sizing rules.
`TerrainIsometricFeatureRendererComponent` also uses this scheduler above 65,536
cells. It derives visible cell bounds from the terrain's native TileMapLayer
transforms at every elevation, preserving raised props under rotated/scaled views.
Resident stamps are sorted and distributed back to their existing elevation nodes.
Defaults are 32-cell chunks, one preload chunk and 256 evaluated cells per frame.
Surface rebuilds invalidate prop residency, including after live cell edits;
isometric terrain geometry itself is still whole-map and rebuilt synchronously.

Large-map flat features, relief and isometric features expose
`MinimumDetailCellPixels` (default 1.5, zero disables the cutoff). When a projected
cell falls below that screen size, the shared residency helper retires visual
stamps and stops processing new prop cells. Detail resumes above 1.5 times the
cutoff to prevent rapid zoom switching. Flat grid transforms and isometric surface
transforms determine screen size; camera rotation does not bypass the cutoff.
Read `IsFeatureDetailSuppressed` or `IsReliefDetailSuppressed` for diagnostics.
This only removes distant decoration: gameplay terrain, resource records, actor
simulation, collisions and jobs remain untouched. Returning to detail rebuilds
the same deterministic stamps under the existing per-frame budget. Small maps
using the non-streamed rendering path are unaffected. This is detail suppression,
not a strategic marker layer or an aggregate forest/rock overview representation.
See `tests/terrain_feature_streaming_probe.gd`,
`tests/terrain_relief_streaming_probe.gd` and
`tests/terrain_iso_feature_streaming_probe.gd` for the verified scope. The isometric
probe uses a 260x260 surface with negative origin, partial edge chunks, camera
jumps, rotation/scale, full-renderer anchor parity and hide/show/source removal.

`TerrainDataLayersComponent` now reads its published field directly by default.
It does not allocate eight hidden metadata tile layers or duplicate the field's
arrays. `MaterializeTileLayers = true` explicitly requests the native TileData
authoring view; rebuild after changing that setting. Both modes expose the same
absolute-coordinate queries and banded underground values. The underground
identity hash streams its existing binary representation instead of retaining
and copying a map-sized byte buffer. This removes duplicated runtime metadata,
but the source generated field is still a whole-map allocation.

`TerrainResourceStage` no longer searches every prior deposit for each placement.
Its row-major pass keeps four rows of resource IDs and checks only neighbors
strictly within the existing four-cell exclusion distance. Auxiliary spacing
storage is proportional to map width, not deposit count. Eligible weighted
resource choices are cached per terrain/relief/stratum for one build only, so
editing an authored catalog takes effect on the next build. Catalog order,
floating-point subtraction, seed hashes and land/water rules are unchanged.

`TerrainGeneratorComponent` now enumerates generated cell records directly into
`GridCellDataComponent`. It no longer retains an additional full-map tuple list.
The cell store still sends one change notification for the batch and reports the
loaded count. This is a synchronous handoff, not asynchronous generation or a
transactional chunk store.

`tests/terrain_resource_scale_probe.gd` compares placement against the original
exhaustive algorithm across three catalogs, three seeds and three map shapes,
including narrow maps. It also checks catalog edits and a 1024x1024 mixed-terrain
fixture for spacing and land/water eligibility. A local Debug run placed 84,743
deposits in 114 ms with 51,144 bytes of managed allocation inside the resource
stage. These measurements exclude allocation of the fixture's terrain arrays,
other generation stages, live cells and rendering; they do not qualify the full
million-cell world or represent a portable performance guarantee.

### Collision Build Readiness

Chunk readiness now depends on successful creation of every required polygon,
not just completion of the cell loop. `TerrainCollisionComponent` rejects missing
geometry, singular transforms, nonfinite coordinates and collinear polygons.
If a chunk fails partway through, its newly created shapes are removed and no
ready-frame token is published. `FailedChunkCount` exposes failed chunks; fixing
the source geometry or transform and rebuilding clears the failure on success.
This prevents the motion gate from treating incomplete native collision as ready.

`terrain_collision_readiness_probe.gd` verifies missing native geometry, collapsed
native-layer corners, an initially singular collision parent, valid mirrored
transforms, a partial elevated surface, shape cleanup and recovery after corrected
bounds. The singular-parent fixture configures the invalid transform before
native bodies exist; changing an existing physics body to a singular transform
can itself trigger a Godot diagnostic, independently of collision generation.

Verified on 2026-09-07: C# build passed with zero warnings/errors; all 73 headless
actor/streaming checks passed. Native OpenGL readiness and motion-gate probes
also passed without the singular-live-body fixture diagnostic. This verifies
failure handling and recovery, not completion of frame-budgeted publication.

### Direct Motion Terrain Readiness

An authored `TerrainMotionGateComponent` under a floating `CharacterBody2D`
connects to the existing grid, cell store and terrain collision component. It
does not own terrain, pathfinding or a second movement controller. The shared
`CharacterMotion` integration point checks it after dash/knockback arbitration
and before `MoveAndSlide`. When terrain is unavailable or collision is stale,
the body stays in place with zero velocity; continued input retries normally.

The gate uses the body's enabled native shape-owner bounds, including their
transforms, rather than visual sprite dimensions. A conservative movement-length
expansion covers changed slide directions. Demanded chunks are bounded by
`MaximumDemandChunks` (default 16), pinned through the existing cell service,
and loaded by `GridCellArchiveComponent.AutoLoadPinnedChunks`. Stopping releases
forward demand when the next stationary motion is prepared. Disabled/detached
gates release pins; a controller that stops preparing motion expires its lease
after two physics ticks. Existing actor/job pins remain independent.

`TerrainCollisionComponent.IsChunkReady` requires current cell content and
availability plus a physics-frame boundary after shape publication. This avoids
treating an archive read as immediate physics readiness. Source mismatches,
unsupported projection/body combinations, invalid motion and excessive demand
fail closed with `WaitReason`; there is no terrain fallback or unbounded request.

The companion scene now authors this gate. Supported ground projections are
manual top-down/isometric and native square/isometric TileMap layouts, with
padding for staggered rows. Elevated projections and grounded/platformer bodies
need separate height-aware residency queries; moving-platform behavior and
external teleports are not qualified by this floating-body integration. Custom
scripts that bypass `CharacterMotion` must call `PrepareMotion` before integration
each physics tick. This is readiness gating, not a new collision solver.

`terrain_motion_gate_probe.gd` exercises controller movement, a saved/evicted
chunk and automatic archive reload, native collision against restored water,
large shape bounds, dash, knockback, live terrain edits and pin lifecycle.
The party-lab probe tests the actual authored native-grid integration.
The geometry queries follow Godot's [Shape2D bounds API](https://docs.godotengine.org/en/stable/classes/class_shape2d.html)
and [CharacterBody2D movement contract](https://docs.godotengine.org/en/stable/classes/class_characterbody2d.html).

Verified on 2026-09-07: clean C# build, all 72 headless checks passed, and native
OpenGL motion-gate/party-lab probes passed. Party captures at 1280x800 and 768x800
were inspected. The gate test includes stopped-controller lease expiry and a
watchdog that exits nonzero if an assertion prevents normal completion.

### Chunked Native Collision

`TerrainCollisionComponent` groups native shape owners by the shared 32x32
gameplay chunk coordinates. Flat manual top-down and isometric grids merge
adjacent cells of the same collision class into rectangles (parallelograms after
isometric projection). Merging never crosses a chunk boundary, fills a dry hole,
or combines water with steep terrain. Native TileMap layouts and elevated
surfaces retain their exact per-cell polygons because they need not be affine.

Cell edits rebuild their affected chunks. Bulk notifications compare chunk content
tokens and availability before rebuilding; geometry/source changes and explicit
`Rebuild` still replace all collision. Unavailable chunks release their shapes
and rebuild when available again. Existing actor/job pins continue to protect
their terrain from eviction; collision does not suspend simulation or impose
camera-only culling. `ChunksRebuiltLastUpdate` exposes the incremental work count.

The million-cell uniform-water fixture produced 1,024 native shapes in 193 ms,
rather than 1,048,576 per-cell shapes. This is a uniform-map result: fragmented
terrain can still need many shapes, and initial scanning/publication remains
synchronous. Camera/actor-demand physics residency, readiness gating for direct
movement into unloaded regions and per-frame collision publication remain open.

`terrain_collision_chunks_probe.gd` checks this scale fixture with native physics
queries, transformed grids, holes, masks, single-chunk edits, unavailable/reloaded
chunks and manual isometric coverage. `terrain_collision_probe.gd` retains the
native TileMap and elevated-surface movement checks. Both are in the actor suite.

Verified on 2026-09-07: clean C# build and all 71 headless checks passed. The
chunk-collision probe also passed with native OpenGL rendering (188 ms for the
uniform ocean). The native party-lab probe passed movement, terrain collision,
party controls and restore checks; its 1280x800 and 768x800 captures were inspected.
The mixed-terrain test checks every cell in both manual projections, including
negative coordinates and partial chunks. No sustained full-world FPS claim is
implied by these functional and uniform-map checks.

### Prepared Underground Identity

`GeneratedTerrainField` now prepares its underground content digest as part of
field construction, on the generation worker for asynchronous builds. Hashing
uses a bounded 16 KiB write buffer and checks cancellation each row. Normal
`TerrainDataLayersComponent.Rebuild` combines that digest with absolute origin
and dimensions instead of scanning all underground cells on the main thread.
The retained digest is 32 bytes per field; repeated full-map metadata publication
does not allocate another map-sized buffer. Cropped or otherwise mismatched
metadata bounds still require a content scan, and explicit native TileData
materialization still visits every cell.

The identity format is now SHA-256 over a versioned bounds header and content
digest. Content includes resource ID, the same four-step richness band used by
metadata queries, and depth. No legacy identity migration is provided: old
development depletion snapshots with the previous identity do not match newly
generated worlds. Current-format snapshots retain depletion across unchanged
metadata rebuilds and restore.

`terrain_identity_probe.gd` compares against an independent binary-stream oracle,
checks content and bounds changes, same-band richness stability, cropped views,
cancellation, and bounded repeated-lookup allocation. The data-storage probe
also checks depletion across rebuild and save/restore. This removes one linear
publication pass, not the remaining synchronous renderer/collision rebuilding.

Measured on 2026-09-07 against a 1024x1024 field: 1,000 full-map identity
lookups allocated 208,040 bytes total and completed below the timer's one-ms
resolution. This measures identity lookup only, not total publication latency.
The C# build passed with zero warnings/errors; all 69 headless actor/streaming
checks passed, including generation, metadata depletion restore and both labs.
Native visual captures were not rerun for this metadata-only change.

### Climate Scratch Lifetime

After biome coherence, `TerrainGenerationBuffer.CompactClimate` retains only
the exact gameplay-cell centre temperature and moisture samples consumed by
vegetation. `TerrainFeatureStage` reads these cell samples; the builder releases
them immediately after that stage. Fine climate reads after compaction fail
explicitly rather than allocating replacement arrays. Cancellation before commit
leaves the fine source intact. Elevation and relief remain available for their
later shoreline and terrain-constraint consumers.

For 1024x1024 cells at two samples per cell, the retained climate payload falls
from 32 MiB to 8 MiB after biome classification, then to zero after features.
Compaction temporarily holds both representations while copying. Released
references do not imply immediate garbage collection or an equal process-memory
reduction. Erosion also reuses its diffusion buffer for median calculation,
removing a separate four-byte-per-land-sample allocation without changing heights.

`terrain_scratch_lifetime_probe.gd` checks exact climate and nonempty vegetation
parity at 1, 2, 4 and 8 samples per cell, release and cancellation behavior, and
erosion height parity against the previous separate-median-buffer algorithm.
The erosion oracle shares the unchanged drainage and diffusion kernels.

The 2026-09-07 million-cell CPU generation probe completed in 28,689 ms with
608,963 dry cells, 439,613 water cells, 153 feature cells, 28,007 surface-resource
cells and six starts, matching the previous run's counts. Peak process working
memory was 765,571,072 bytes (about 730 MiB); the full-world memory target is
still open. This probe excludes scene publication, rendering, navigation and FPS.

Verification: C# build passed with zero warnings/errors and all 68 headless
actor/streaming checks passed. Native render captures were not rerun for this
output-preserving scratch-storage change.

### Start Selection Storage

`TerrainStartPositionStage` now stores each candidate's coordinate, score and
continent in one compact record. An eligibility-count pass allocates the candidate
list once, instead of growing a coordinate list and two dictionaries. Radius-three
scoring, exclusion rules, score ordering, continental distribution and minimum
separation remain unchanged. The regression oracle compares exact ordered starts,
including score ties, across several map sizes and requested start counts.

The 1024x1024 mixed-terrain fixture measured 70,749,296 bytes / 2,216 ms for the
original dictionary-based stage versus 5,594,152 bytes / 1,513 ms for compact
candidates, with identical starts. These are stage allocations and local Debug
timings, not total-world memory or portable timing guarantees.

Cancellation is checked per row while counting/scoring, before and after sorting,
and every 256 candidates during selection. The native sort itself is not
interruptible. `terrain_huge_generation_probe.gd -- --cancel-starts` cancels after
observing this stage in a real 1024x1024 build; the checked run cancelled in 103 ms
with no published field. The complete CPU build also passed: 29,543 ms, six valid
starts and a process peak of 764,006,400 bytes. Its report is
`tests/output/terrain_async/huge_generation.json`. This does not qualify scene
publication, renderer memory, gameplay simulation or FPS, and the whole-map field
is still allocated.

Verified on 2026-09-07: clean C# build, all 67 headless actor/streaming checks,
the complete huge CPU-generation probe, and its start-stage cancellation variant
passed. The regular suite includes `terrain_start_scale_probe.gd`.

### Background Build Backend

`TerrainGenerationJob` is an internal, isolated CPU build, not a second world
coordinator. Its constructor captures `TerrainResourceRules` on the main thread
and removes the authored ResourceCatalog reference from the settings passed to
the worker. Resource IDs, eligibility, weights and subsurface properties are
copied into detached read-only input. Both resource stages use that same input;
no worker enumerates the editor's resource arrays. Each build owns and disposes
its FastNoiseLite instances.

The worker calls the existing field builder and returns an unpublished field.
Progress contains the current stage and completed-stage count out of 20. It is
stage-count progress, not a prediction of elapsed time. Cancellation is checked
between stages and before returning the field. Erosion, shared drainage and river
carving also check between batches of at most 4,096 loop iterations and around
sorting. A sort already in progress and other uninstrumented stages can still
delay cancellation. Cancel, observe Completion, and only then dispose the job.
No worker updates TileMapLayers, live gameplay cells, nodes or textures.

The opt-in huge benchmark accepts `-- --cancel-erosion`. It cancels after observing
the Erosion stage on a 1024x1024 run and requires a cancelled task with no published
field within two seconds. The development-machine run completed cancellation in
103 ms (100 ms observation interval). This is not a universal latency guarantee or
coverage of every cancellation point inside every stage.

`tests/terrain_generation_job_probe.gd` checks exact cell and sub-cell equality
against synchronous generation, separate-job cancellation, monotonic progress,
and isolation after the source catalog is edited and disposed. Its local run
processed 75 main-thread frames during worker generation. Whole-map allocations
remain unchanged.

### World and Lab Integration

All asynchronous generation paths now retire the completed job and staged cell
records before emitting completion signals, including worlds without a painted
renderer or collision consumer. Successful requests emit `WorldBuilt` followed by
`GenerationFinished`; cancellation and failure emit only `GenerationFinished`.
A listener may start another request from either event. No old-job cleanup runs
after the callbacks, and duplicate completion for a retired job is ignored.
If a `WorldBuilt` listener starts a replacement, the following `GenerationFinished`
still describes the previous request; `IsGenerating` then describes the new one.

The expanded `terrain_world_generation_probe.gd` verifies two builds chained from
`WorldBuilt`, restart from cancellation, exactly-once completion counts and the
replacement seed. These checks passed along with collision publication, world
ownership, native painted publication and native terrain-lab generation. The C#
build completed with zero warnings/errors. The native streaming-lab probe also
passed, and its worker-view capture was inspected. This is targeted lifecycle verification,
not a new full-suite or huge-world performance qualification.

Background painted-world publication now includes a `Preparing painted terrain`
phase after committing gameplay cells and before drawing. The existing
`PublicationCellsPerFrame` budget (1..4096) also bounds live visual-sample reads
per callback. Samples use the same terrain, elevation, shore and water-patch
accessors as synchronous rebuilding; a completed snapshot is installed at once.
Pending work neither uploads partial textures nor overwrites the previous surface.

The renderer suppresses its deferred cell-change rebuild while this copy is owned
by the world. Source identity, replacement/terrain revision, default terrain,
bounds and availability are checked. A changed source fails publication; removing
the world releases pending samples. Explicit renderer Rebuild interrupts the
preparation rather than allowing two publishers to compete. This phase is after
the live-cell commit, so failure does not roll back gameplay cells.

`terrain_painted_publication_probe.gd` compares all five uploaded maps byte-for-byte
against synchronous generation and exercises the cell budget, old texture retention,
mid-copy mutation, recovery and world removal. This budgets sample reads, not array
allocation or total memory: the sample array still spans the finite world. Coast
distance computation and contour smoothing now run as detached worker work after
this copy. Image reconstruction and texture upload remain synchronous; no full
frame-time guarantee is claimed.
Verification: build completed with zero warnings/errors; all 76 headless checks
passed. Native OpenGL snapshot-parity and terrain-lab generation checks passed,
and the completed lab capture was inspected.

### Detached Painted Coast Computation

`TerrainPaintedCoastJob` consumes the complete, unpublished visual snapshot and
returns managed raw and smoothed coast/lake pixel buffers. It reads no scene nodes and creates no
Godot image or texture. `TerrainCoastField` now separates pixel computation from
upload, sharing the same algorithm with synchronous callers. Ocean connectivity,
distance transforms and pixel encoding accept cancellation; scene removal retires
the job and observes its completion without a callback into freed nodes.

The world reports `Computing painted coast` while waiting. The renderer validates
source revisions, bounds and coast settings before accepting the result, then uploads
on the main thread. A prepared-result key prevents reuse for a different source or
configuration. The previous visible surface stays unchanged until publication.
Generated-coast distance semantics and the lake-bank opt-in are unchanged.

This moves CPU distance computation off the render callback, not all renderer work:
ID/shade packing, cubic image reconstruction, image/texture upload and other renderers
remain synchronous. Worker buffers still cover the finite map and there is not yet
an aggregate memory/admission budget for simultaneous worlds. This is not complete
huge-world performance qualification.
Verification: build completed with zero warnings/errors; all 76 regression checks
passed. Generated-coast encoding and live-shoreline probes passed. Native OpenGL
filter reconstruction, worker publication/parity and terrain-lab generation probes
passed; the completed lab capture was inspected. No sustained frame-time improvement
or total-memory limit has yet been measured for the complete million-cell scene.

The worker now also applies the existing five-tap, separable contour filter to
eligible float distance buffers. The kernel, geometry flags, coarse-mask behavior
and allocation thresholds are unchanged. Cancellation is checked on each row;
raw buffers are never modified. Ineligible buffers reuse their raw allocation.
The renderer consumes prepared contours only for the exact source texture, cell
bounds and pixel dimensions, then performs the existing cubic resizing and GPU
upload on the main thread. This avoids both repeating the convolution and
changing the shoreline through double smoothing. Prepared buffers are consumed
once; a different source or bounds falls back to normal reconstruction.

`TerrainCoastFilterSmoke` checks prepared/synchronous byte parity, raw-buffer
isolation, stale source/bounds rejection, coarse masks and cancellation. The native
OpenGL filter probe passed with the same reconstruction error as before this move.
This shifts CPU work, not a measured total frame-time or memory improvement.
Verification on 2026-09-07: build succeeded with zero warnings/errors; all 76
headless regression checks passed. Native filter, painted-publication and terrain-lab
generation probes passed, and `tests/output/terrain_async/complete.png` was inspected.

`BeginNewWorld()` stages replacement gameplay-cell records over multiple frames
when the generator's `ClearExistingCells` is true. `PublicationCellsPerFrame`
defaults to 512 (clamped to 1..4096). During this phase, progress reports
`Preparing gameplay cells`; the current field, cell store and save recipe remain
live. Cancellation discards the staging store. Completion swaps the prepared
dictionary and emits one cell revision before rebuilding renderers.
This is a cell-count budget, not a millisecond guarantee. Staging temporarily keeps
both old and new cell stores in memory. Renderer and navigation publication
remain synchronous; additive generation (`ClearExistingCells=false`) retains its
existing synchronous merge behavior. Synchronous `NewWorld()` is unchanged.

After the live-cell commit, background builds schedule collision through
`RequestRebuild`. `IsGenerating` remains true while `Preparing terrain collision`
is displayed. `WorldBuilt` and successful `GenerationFinished` are delayed until
the configured collision consumer drains its chunk budget and native physics has
consumed the shapes. Missing/rebound sources, a removed consumer, unavailable
chunks or rejected geometry report failure rather than a usable world. There is
no rollback of already committed terrain after a publication failure; correct the
consumer and start a new build. `BuiltSize` and the captured recipe describe the
committed field even while collision is still preparing, not readiness for play.

`CanCancelGeneration` is true only before the live-cell commit. After commit,
`CancelGeneration` leaves publication running; authored terrain/streaming lab
Cancel controls disable at this boundary. Pre-commit cancellation still preserves
the old field and cells. Scenes should keep gameplay behind the loading state until
successful completion. This is not camera culling or suspension of offscreen simulation.

`TerrainWorldComponent.BeginNewWorld()` starts a runtime background build and
returns whether the request was accepted. It rejects overlapping requests.
`IsGenerating`, `GenerationProgress(stage, fraction)` and
`GenerationFinished(success, message)` expose its lifecycle. Call
`CancelGeneration()` to cancel, including the frame immediately before
publication. The existing `WorldBuilt` signal still means the field and scene
have been published, with the background path additionally waiting for collision
readiness. `NewWorld()` and save restoration remain synchronous entry
points; `BuildOnReady` has not been silently converted to asynchronous behavior.

The request restores the live generator's configuration before yielding, so
queries continue reading the previous field during generation. Cancelled or
stale requests do not write live cells. Changes to world recipe, generator
configuration, generator identity or the world's explicit cell-source path
discard an obsolete result. Saves capture the last built recipe, not pending
UI choices. Exiting the scene cancels the worker and schedules node-independent
cleanup after it finishes; the callback cannot publish into a freed scene.

Publication runs on the main thread after one visible "Publishing world" frame.
Renderer and navigation work can still stall that frame on a large map. The cell
staging and collision chunk budgets do not make this an atomic rollback mechanism
or a fully frame-budgeted renderer. Those remain required before qualifying huge worlds.

`terrain_world_collision_publication_probe.gd` exercises multi-frame collision,
physics-ready completion, late-bound consumer paths and scene processing order,
the post-commit cancellation boundary, consumer removal, invalid geometry and recovery.
Verification: all 75 headless checks passed. After the final generator-binding
validation, the build had zero warnings/errors; native OpenGL publication, party
lab and streaming lab checks passed. Party and narrow streaming captures were
inspected. This is functional validation, not a sustained huge-world FPS benchmark.

The terrain lab uses this background entry point. Its authored scene contains a
progress bar and UI-kit Cancel button; conflicting recipe controls are disabled
while generating and restored on completion or cancellation. Camera pan/zoom
remains available. Stage fractions reserve the last 10% for publication and are
not time estimates.

`tests/terrain_world_generation_probe.gd` checks preservation of live fields,
cell revisions and saved recipes, cancellation/restart, stale world/generator
settings, pre-publication cancellation and scene exit. The authored lab is
exercised by `tests/terrain_lab_generation_probe.gd`, which also writes loading
and completed viewport captures under `tests/output/terrain_async` when run with
a rendering backend.

## Actor Residency

`ActorDefinition.SimulationPolicy` defaults to `Continuous`. This is the policy
for RTS troops, colony workers, economic actors and anything whose simulation
must continue offscreen. Moving the camera never suspends these actors.

`AmbientDormancy` is an explicit authoring choice for idle ambient actors whose
changing state is covered by their ISaveable components. The registry can capture
the actor, remove its body from the SceneTree, free the scene instance and keep
its identity as a data record. Waking instantiates its registered definition and
restores component state, position, rotation and scale. This is not object
pooling: sleeping scene instances really are released.

Suspension is refused for selected/possessed actors, active or queued orders,
active paths/jobs/hauls, held cargo, non-idle AI, injury, status effects, physical
motion, incoming actor-command targets and nested actor bodies. A custom node
can implement `IActorResidencyGuard.CanSuspendActor()` or expose that method
through GDScript to veto suspension while its own activity must run. Ambient
dormancy freezes its eligible idle state; it does not advance time or calculate
offline economic outcomes.

Add `ActorResidencyComponent` with explicit registry and Camera2D paths for
camera-driven residency. It uses the current camera, including rotated views,
wakes actors before retiring distant ones, and defaults to at most four scene
transitions per 0.25-second poll. Separate wake and sleep margins prevent rapid
unload/reload around a viewport edge. This is a transition-count budget, not a
guarantee that an arbitrarily expensive actor scene takes a bounded number of
milliseconds to instantiate.

- `ActorCount` counts all identities; `ResidentActorCount` and `DormantActorCount`
  distinguish live scenes from stored records.
- `FindActor(id)` returns only a live component. `WakeActor(id)` returns or wakes
  it; selection, possession, control-group recall and addressed orders wake it.
- `GetActorRecord(id)` returns a deep copy. Inspection cannot mutate authoritative
  dormant state. `GetActorPosition` and `GetActorOwner` work without instantiation.
- `GetOwnedActors()` includes dormant actors. `QueryActors(..., includeDormant: true)`
  includes their stored positions; the default query still returns only residents.
- `TransferOwnership` also works on dormant identities. `RemoveActor` destroys
  either representation and removes its spatial/ownership entries.
- Registry snapshots include each actor's residency. Restoring a dormant record
  does not eagerly instantiate it. Definition IDs must exist in the registry's
  authored catalog so waking remains possible.

`tests/actor_residency_probe.gd` verifies actual instance destruction, stable IDs,
ownership/spatial queries, deep-copy isolation, JSON restore without eager scene
creation, selection/camera waking, continuous-policy protection and removal.

## Runnable Example

### Actor Overview

`ActorOverviewComponent` is a world-aligned `Node2D` marker overlay, not another
world or HUD minimap. Bind `PlayerPath` to the scene's `PlayerContextComponent`.
It queries that player's registry for owned live/dormant actors inside the camera
viewport, filters dead/inactive live actors, and never instantiates dormant scenes.
Enemy and neutral actors are intentionally excluded; this is not a fog visibility
system. A player avatar is not required.

Markers activate below `ShowBelowPixels` (default 12) for `ReferenceWorldUnits`
(default 64), with 1.5x exit hysteresis. Marker radii remain screen-pixel sized
under camera rotation and transformed parents. Selected actors use `SelectedColor`.
`RefreshInterval` defaults to 0.1 seconds; camera transform changes refresh sooner.
`RefreshOverview()` supports explicit refresh, and `GetMarkerActors()` exposes a
copy of the current IDs. `PickActor(worldPosition)` applies a screen-space hit
radius. Set the existing orders controller's `OverviewPath` to use this picking
for owned markers. Its drag threshold is also screen-space rather than world units.

The actor lab contains this authored overlay. It does not hide actor sprites,
pause animations, reduce simulation detail, or cap visible actors. Dense overview
clustering and full-detail actor LOD/performance remain future gates. The overview
probe checks ownership, dormant-state isolation, picking, zoom transitions and a
native pixel check; its capture is `tests/output/actor_overview/markers.png`.

Scene: `addons/beep_game_builder_cs/templates/scenes/actors/actor_lab.tscn`.
It generates a map through the existing TerrainWorldComponent, creates six
owned workers without an avatar, and exposes recruit/select/work/stop/save/restore
actions using authored UI-kit controls. The snapshot is in-memory demonstration
state, not a durable save slot. The preparation job demonstrates the existing
worker execution loop; it does not itself construct a building or edit terrain.
Truck navigation explicitly blocks shallow water as well as deep water.

```powershell
& 'H:\dev\Godot\Godot_v4.7-stable_mono_win64_console.exe' --path . addons/beep_game_builder_cs/templates/scenes/actors/actor_lab.tscn
```

Run checks from the repository root:

```powershell
./tests/run_actor_checks.ps1 -Capture
```

The scale probe checks 1,000 identities and 200 simultaneous routes. This is a
functional stress test with lightweight nodes, not the approved 1,000-actor / 200
fully detailed production benchmark. The surface probe checks a logical 1024x1024
uniform surface, not a fully generated 1024x1024 world.

## Next Implementation Gates

### Runnable Party Lab

Scene: `addons/beep_game_builder_cs/templates/scenes/actors/party_lab.tscn`.
It inherits the generated-world actor lab, substitutes an authored animated
companion scene, and uses the existing player, registry, control group and Follow
commands. The default six-member party possesses one actor; the other five follow.
The underlying actor lab and streaming lab retain avatar-free RTS control.

Move with WASD or arrow keys. Q/E and the Previous/Next buttons switch possession.
Hold companions submits manual Stop orders to everyone except the current leader;
Regroup explicitly releases those holds. Save/Restore uses the existing in-memory
lab snapshot, including party policy state; it is not a durable save slot.
Fit map, wheel zoom and middle-button camera dragging remain available. Movement
refocuses the camera on the controlled character. Keyboard camera panning is
disabled so it cannot compete with character movement.

Controls are authored UI-kit nodes in the scene, with a wrapping toolbar. Native
`CharacterBody2D`, `TopDownController`, `GridPathFollowerComponent`, shared actor
animation/presentation and `TerrainCollisionComponent` supply movement and visuals.
Water and steep collision masks apply to direct movement as well as navigation.
The existing adventurer artwork has two walking frames and horizontal facing;
this is not a new four-direction character art pack. Footprint-sized resting
reservations separate settled followers. Crowd avoidance while moving,
collision-aware routes and party tactics remain separate implementation gates.

The scene installs only missing example input actions and removes those it owns
on exit. Existing project bindings are preserved. The party probe loads this
actual scene, exercises input and controls, tests native blocked-terrain collision,
checks snapshot restore and renders 1280x800 and 768x800 captures under
`tests/output/actors/party_1280.png` and `party_768.png`.

Verified on 2026-09-07: all 64 headless actor/streaming checks passed; the party
scene probe also passed with native OpenGL rendering. Both window-size captures
were inspected. No claim of collision-aware formation spacing or full-detail
large-population performance is implied by these six-character checks.

```powershell
& 'H:\dev\Godot\Godot_v4.7-stable_mono_win64_console.exe' --path . addons/beep_game_builder_cs/templates/scenes/actors/party_lab.tscn
```

### Runnable Streaming Lab

`addons/beep_game_builder_cs/templates/scenes/actors/streaming_lab.tscn` inherits
the authored actor lab and uses its existing UI kit, worker definition, orders,
jobs and player ownership. There is no player avatar. It generates a finite
300x256 world asynchronously with progress and cancellation, then enables the
existing archive with a soft target of 16 resident 32x32 gameplay chunks.
Camera demand loads archived regions. Fit map uses the uniform-surface overview;
Workers returns to the starting workers. Auto archive pauses eviction without
disabling demand loading. Actor and job pins protect active simulation.

The lab creates a unique `user://streaming_lab/<session>` directory on each run.
These are session chunk files, not a resumable world checkpoint. Snapshot and
Restore affect workers/jobs in memory only. Generated terrain fields, visual
samples and textures remain resident: the counter is not a total RAM budget.
The initial count can exceed the target while sequential writes finish, or while
camera/gameplay pins protect more chunks than the target allows.

```powershell
& 'H:\dev\Godot\Godot_v4.7-stable_mono_win64_console.exe' --path . addons/beep_game_builder_cs/templates/scenes/actors/streaming_lab.tscn
```

The painted renderer retains the last observed terrain kind, elevation, shore
width and fine water/lake patches for archived chunks. Resident content revisions
refresh these samples; residency-only changes reuse existing map textures and
streamed geometry. This is a render cache, not another gameplay world. Replacing
or rebinding the cell source resets it. A new source with unavailable data stays
hidden until its initial complete visual snapshot is possible; loading a world
directly from partial archives needs a persisted overview in a later phase.

Reloading an archived chunk decodes fresh record and water-patch objects.
`GridTerrainWaterPatch` compares by value (resolution and bits), so identical
content is not a change: the retained samples match, the snapshot revision holds,
and the map textures are reused. A reloaded record also folds the generated
terrain facts back out of the snapshot's metadata dictionary into its typed
fields, so it is as light and as quick to sample as a generated one - no native
Dictionary per cell, no string-keyed Variant lookups when the renderer reads it;
only genuinely custom metadata keeps a dictionary. Before that, patches compared by reference and
every reload of a shoreline chunk rebuilt the whole map's id, shade and coast
textures - the same full pass a genuine edit pays - each time an actor walked
into archived coast.

Measured at 320x256, a streamed size: a Rebuild after eviction costs 0.2-0.4 ms
and after reload 8-20 ms (the re-sample of one chunk), and neither rebuilds a
texture. A genuine edit used to rebuild the whole map - 742 ms for an inland cell
and 820 ms for a shoreline cell at that size, most of it distance transforms over
samples nothing had moved - and now costs 6-9 ms and 29-49 ms respectively:

- The id, shade and lake-width maps keep their pixels between rebuilds and
  rewrite only the chunks the visual snapshot reports changed (plus a one-cell
  shade halo), as new textures of the same size.
- A whole-cell edit drops a cell's fine shoreline (a partial patch is generated
  geometry a paint replaces) but keeps a UNIFORM patch, which draws no boundary.
  Every generated cell carries one, so a land-to-land edit leaves the coast and
  lake inputs untouched and both caches simply hit.
- `TerrainCoastField.LiveCache` keeps each field's inputs and samples. A change
  is answered by diffing the inputs and recomputing the window that can see it:
  distances saturate at the coast range, so the update window is the changed
  cells grown by the range, computed over a window one range wider still; the
  ocean-connectivity mask is the one global dependency, recomputed in full, and a
  change to it outside the window falls back to a whole rebuild. The result is
  exact - the water-surface smoke test compares a restored field byte for byte.
- `RenderCache` retains the smoothed field, its padded copy and the 2x cubic
  reconstruction, re-smooths only the dirty window plus the kernel's reach and
  re-upscales only the 64-sample blocks that see it, from the raw field the live
  cache hands it rather than a GPU readback. It never modifies a source image (in
  headless runs a texture's `GetImage` is its own stored image). The remaining
  ~50 ms of a shoreline edit is mostly the upload of the reconstructed field.

The isometric and tile renderers share these caches; they pass the field's
content revision to `RenderCache.Resolve`, since an updated field arrives as a
new texture whose instance alone no longer says whether the reconstruction is
current.

`terrain_painted_archive_probe.gd` verifies texture stability across eviction,
resident edits, reload (texture identity, not only texture data) and world
replacement. `streaming_lab_probe.gd` checks
actual generation, owned workers, eviction, overview demand handoff and camera
reload, with native captures in `tests/output/streaming_lab`. Both are included in
the actor checks; the streaming lab is also included in `-Capture`.

Verified on 2026-09-07: C# build completed with zero warnings/errors; all 54
headless actor checks passed. The native streaming-lab probe passed at 1280x800
and 768x800, and the native surface probe passed its overview/detail pixel checks.
These are functional checks, not the pending sustained huge-world FPS/RAM gate.

### Remaining Gates

Authored isometric terrain now exposes `RequestRebuild`, `CancelRebuild`,
`IsRebuilding`, `CellsPerFrame` and `CellsProcessedLastFrame`. Requested rebuilds
sample source cells, paint assigned tiles or native terrain connections, and
validate coverage over multiple callbacks. A hidden staging TileMapLayer keeps
partial results offscreen. Publication copies native tile data into the existing
`IsoTerrain` layer so grid references, authored transforms and paths remain valid.
Explicit `Rebuild` stays synchronous and shares this implementation. Deferred live
edits use the budgeted request when a request is already active or bounds exceed
65,536 cells. Background world generation now calls the budgeted request and
reports `Preparing isometric terrain` after committing live cells. The world stays
busy until the request publishes valid coverage, then binds the gameplay grid and
waits for configured collision readiness. Its final draw reuses the prepared layer
without rebuilding it again. Explicit synchronous world operations remain synchronous.

World publication validates renderer/source paths, projection and bounds; cancelled,
removed or incomplete renderers fail rather than emitting `WorldBuilt`. A publication
revision distinguishes an old valid surface from a completed replacement. World exit
retires pending renderer work. Starting an explicit renderer request also retires any
older deferred rebuild, preventing cancellation from accidentally launching it.

`terrain_world_iso_publication_probe.gd` verifies multi-frame preparation, stable
publication revision after showing the view, native collision readiness, renderer
cancellation, recovery and removal. Headless and native integration runs passed;
the removal case was added and verified headlessly. Existing renderer publication,
world generation, painted publication and collision publication checks also passed.
Build completed with zero warnings/errors. The runner now contains 79 headless
checks; the latest full-suite verification above includes this integration.

Changes to live terrain, source identity, bounds, bindings, TileSet resources or
generator configuration restart a pending request. Cancellation and scene exit
dispose the iterator and hidden layer. Failed asynchronous validation retains the
old visible terrain and exposes diagnostics. Synchronous invalid authoring still
clears stale tiles, as verified by the existing live-cell authoring checks.

The budget counts source reads, requested paint cells and validation reads. Native
connection calls contain at most 64 requested cells and may adjust neighbors; a
callback can exceed the configured count by at most 63. Atlas discovery, field
generation for uncached previews, native packing/final tile-data transfer, and
native resource disposal are not frame-budgeted. Both old and staged layers plus
the full grouping data coexist during preparation: this is not bounded-residency
terrain streaming or a total-memory improvement.

The new `terrain_iso_publication_probe.gd` verifies both tile modes, retained old
data during preparation, stable layer identity, finite coverage, cancellation,
live edits, bounds changes and scene teardown. Headless and native OpenGL runs
passed; build, existing live-cell authoring, and view/grid tests passed. The probe
is registered for normal and native runs (78 headless checks now registered);
the latest 79-check verification above includes this change.

The shared `TerrainSurfaceStreamingComponent` supports overview geometry for uniform
shader surfaces using diamond-down, horizontal-offset isometric TileSets as well as
square TileSets. Its detail geometry now uses the same outline math: chunk diamonds
derived from native `MapToLocal` cell centres, preserving the first-cell shader origin,
whose union is the overview diamond. Other layouts fail `Configure` with an error naming
them rather than receiving a wrong outline; `TerrainTileSets.Create` never produces one.
Existing camera-data demand uses the same `CanSupplyOverview` contract; actor and
simulation pins are not changed. This does not stream the separate elevated block
renderer, authored autotile ground, or full-world material textures.

`terrain_iso_surface_overview_probe.gd` verifies a 1024x1024 logical isometric surface
with no detailed geometry at overview zoom, bounded chunk geometry the same frame the
camera zooms in, every chunk vertex inside the map diamond, and - natively, at zoom 1 on
a 20x12 map cut into six chunk diamonds, four of them partial - zero pixels differing
between the chunk diamonds and the one overview diamond under a translucent coordinate
shader; a one-pixel inset or outset of the chunk outline trips that comparison. Captures
are under `tests/output/iso_surface`. The probe is registered in both normal and capture
runs.

1. Complete per-genre actor adapters and demos; eliminate competing motor ownership
   across auxiliary abilities; qualify scheduled navigation latency and add job
   cross-queue and path reservations.
2. Extend the implemented dormant-record store with genre-specific reduced
   simulation for economic/combat actors. RTS and colony simulation currently
   stays fully live offscreen; ambient dormancy is implemented separately.
3. Partition existing terrain fields into chunks with neighbor halos and stable
   macro topology. Keep all generation under TerrainWorldComponent. Validate
   coastline/biome continuity and save edits before enabling large bounds.
4. Stream props, collisions and navigation with the same chunk coordinates;
   add overview rendering, memory limits, progress and cancellation.
5. Qualify the full-detail actor and huge-map budgets on measured hardware.
   Only then enable endless coordinate-seeded generation and bounded persistence.

## Automatic Construction Access Retry

`GridBuildSiteComponent` now retries pending material sites in a round-robin
linked queue. `PendingChecksPerFrame` defaults to 8 (clamped to 1..64), and
`PendingChecksLastFrame` exposes the automatic check count. Terrain reopening,
obstacle removal and navigation path changes no longer require a caller to invoke
`RefreshPendingBuilds`. Storage delivery still performs an immediate check.
Cancelled, started, removed and freed sites leave the retry queue; coordinator
exit clears it and releases outstanding material reservations.

This bounds the number of automatic site checks, not milliseconds or footprint
edge probes. Pending sites are polled even without navigation changes; a full
retry round takes approximately ceil(pending sites / budget) process frames.
The explicit `RefreshPendingBuilds` API remains an immediate, unbudgeted refresh.
This does not add remote material allocation or reduced offscreen simulation.

Verification: build passed with zero warnings/errors. The expanded
`terrain_build_approach_probe.gd` passed automatic reopening, one-check-per-frame
fairness across separate sites, freed-site cleanup and coordinator teardown.
Storage material reservations, worker build effects and terrain building delivery
probes also passed. The subsequent `run_actor_checks.ps1` run rebuilt successfully
with zero warnings/errors and passed all 82 headless checks, including this step.

## Numeric Terrain Sample Storage

`GeneratedTerrainField` now stores fine water classification and shading through
`TerrainSampleValues<T>`, alongside the existing chunked terrain-label storage.
Each immutable 32x32-sample chunk stores one value when bitwise uniform, otherwise
an exact dense local array. Partial edge chunks preserve row indexing; source
arrays are copied, not retained. Cancellation is checked before allocation and
between chunks. Float comparisons preserve signed zero and NaN payloads.

The uniform 1024x1024 water/shade fixture retained 5,120 bytes of sample payload
instead of 5,242,880 bytes (5 MiB), excluding chunk tables/object overhead. Its
combined setup/compression measurement was 85 ms. This is a uniform-data fixture,
not a measured reduction for a full generated world. Fully varying shading still
requires dense values plus chunk overhead. During publication the source buffer
and the current compact copy coexist; generation peak memory is not bounded by
this change. Completed source arrays are now retired between packing steps.
Chunk dimensions here count fine samples, not gameplay cells or archive chunks.

The new probe verifies bitwise parity, partial edges, invalid indices/dimensions,
source isolation, uniform compression and cancellation. Build passed with zero
warnings/errors; generation worker equality, four-seed water sampling and five-map
painted-publication parity passed, including native OpenGL publication. All chunks
remain resident. Disk-backed field residency, halo queries and macro/detail
generation remain necessary for the huge-map roadmap.

## Generation Buffer Retirement

After diagnostics, `ReleaseGenerationScratch` now also retires the fine land and
footprint masks. Field construction consumes terrain, water and shade through
individual packing methods, releasing each dense source only after its compact
copy succeeds. Retired getters throw instead of silently reallocating. Terrain
label packing now checks cancellation between chunks, matching numeric packing.
Cancellation preserves the source of the currently failing packing operation;
previously completed packing steps remain consumed. A cancelled generation run
is discarded, not resumed from its partially consumed buffer.

The extended sample probe verifies source preservation on cancellation, retirement
of all five arrays and continued readability of packed data. Build, water surface,
generation-job, identity and painted-publication probes passed. A 1024x1024 CPU-only
run passed in 28,415 ms with 608,963 dry cells, 439,613 water cells and six starts;
process peak was 743 MiB. This does not demonstrate reduced whole-process peak
memory versus the earlier 730 MiB run. Dropping references does not force immediate
GC. The latest machine-readable measurement is
`tests/output/terrain_async/huge_generation.json`; it excludes scene publication,
rendering, navigation and full-detail actor performance.

## Lazy Gameplay Outputs

`TerrainGenerationBuffer` allocates gameplay terrain, water, relief, shade,
continent, feature and resource arrays on first access instead of constructing
all outputs before the landmass stage. First-use initialization preserves the
previous defaults, including grass terrain, unit shade, empty resource IDs and
the uninitialized inland labels. Subsequent reads return the same mutable array.
This buffer remains worker-owned, not a concurrently mutable service.

At 1024x1024 on the 64-bit runtime, 67 MiB of output-array payload is deferred
until the consuming stages. All outputs still exist in the completed field;
this reduces early overlap, not final field size or streaming residency.
`CellPayloadBytes` reports allocated output payload excluding object headers.

The expanded scratch-lifetime probe verifies allocation timing, every default,
array identity, retained edits and payload accounting. Build completed with zero
warnings/errors; generation-worker equality, four-seed water sampling and painted
publication checks passed. The full-size run retained 608,963 dry cells, 439,613
water cells and six distinct valid starts; measured process peak was 530 MiB and
generation took 30,049 ms. Earlier results (743 MiB / 28,415 ms) are historical,
not a controlled multi-run timing comparison. The current report remains
`tests/output/terrain_async/huge_generation.json`.

## Combined Scale Baseline (Failing)

`tests/terrain_actor_scale_probe.gd` is an opt-in native benchmark, deliberately
not part of the short regression runner. It loads the authored streaming lab at
1024x1024, finds connected dry spawn cells, creates 1,000 native worker instances,
submits 200 routes and measures 15 seconds of wall-frame intervals with automatic
archiving enabled. The workers use existing static art, not the required animated
full-detail population. Route completion is checked separately from submission;
incomplete routes cause a nonzero exit. No FPS qualification is claimed.

Run with the Godot console executable using `--path . --rendering-method
gl_compatibility --script tests/terrain_actor_scale_probe.gd`. Latest native result:
37,190 ms scene readiness, 38,243 ms bulk spawning, 777.436 ms median frame,
840.610 ms p95, 20 measured frames and 0/200 arrivals. Only 10 chunks retired;
1,014 remained resident with 16 pinned chunks. The test correctly exited 1.
`tests/output/terrain_actor_scale/report.json`, `detail.png` and `run.log` provide
the report, capture and console evidence. This fixture uses a unique transient
archive session; it does not implement a persistent world checkpoint.

Native errors repeatedly identify `GridPathFollowerComponent` setting absolute
world Y as ZIndex (around 11,800), outside Godot's -4096..4096 range. Correct shared
depth ordering, worker update costs, scheduled navigation latency and archive
throughput need investigation before rerunning the performance gate. The image
was inspected and is populated, but is not approved art/layout coverage. The
fixture's stale bulk-spawn label and archive checkbox were corrected afterward;
that presentation-only correction has not yet been recaptured.

Rerun on 2026-09-08 after the surface-streaming, archive and painted-renderer
changes described above (same fixture, same machine, native OpenGL).
`actors-paused`, which isolates terrain and the archive: 6.9 ms median frame,
22.4 ms p95, 93 ms max, 127 chunks retired in 15 s (previously 6.9 / 123.3 /
162.5 ms and 67 retired). `archive-paused`, which isolates the actor layer:
92.3 ms median, 144 ms p95, 5.7 physics steps per render frame (earlier runs of
this same mode ranged 57-150 ms). `full`: 130-166 ms median across two runs,
7.4-8 physics steps per frame, 43-58 chunks retired, 0/200 arrivals.

Rerun again the same day after the chunk-scoped repaint, the worker-side reload
decode and the large-object threshold: `actors-paused` 3.4 ms median, 12.6 ms
p95, 37.9 ms max, 181 chunks retired in 15 s (3,452 frames); `archive-paused`
66.5 / 89.8 / 104 ms with 4.1 physics steps per frame; `full` 110.9 / 140 /
146 ms with 6.7 physics steps per frame, 67 chunks retired and 0/200 arrivals.
Terrain and the archive alone are now cheap. The 44 ms the full scene adds over
`archive-paused` is no longer the reload itself (its main-thread share is 0.2 ms
median): the process phase grows 9 ms, the physics phase 19 ms because the longer
frame runs more catch-up steps of the same 1,000-body cost, and the rest is what
the actor and navigation layers do in response to chunk publication - every
eviction and every reload bumps the navigation revisions, and 144-169 path
requests stay pending in every mode, so no route completes with or without the
archive. Those are the actor layer's items; the archive and renderer items above
are closed.

## Native Actor Depth Ordering

The actor worker scene now sets `SetZIndexFromY = false`: the authored actor lab
already has a native Y-sorted Actors container. Worker movement must not also
write absolute world Y into ZIndex, which exceeded Godot's range on huge maps.
This fixes the authored actor/streaming labs without clamping all distant units
to one artificial depth. It does not redesign depth ordering across separate
terrain-prop and actor containers or change other generic worker templates.

The combined benchmark now checks that each worker uses its Y-sorted container.
The native rerun produced no Z-index errors; actor and escort lab probes passed.
Current failing scale result: 36,874 ms ready, 38,204 ms spawn, 764.408 ms median
frame, 830.075 ms p95, 20 frames and zero of 200 routes completed. Thus error
logging was not the principal slowdown. The current screenshot includes the
correct bulk-spawn label and archive checkbox. Shared-service and per-worker
profiling remains necessary; do not interpret this fix as performance completion.

## Shared Direct-Component Lookup

A managed stack sample from the slow combined workload landed in
`ActorComponent.IsDead -> EntityComponent.GetSiblingComponent -> Godot.Array`
enumeration. `GetSiblingComponent` and `ActorComponent.ForBody` now share a
weakly body-scoped direct-child snapshot. Native `ChildOrderChanged` invalidates
it for additions, removals and ordering changes. Queries still select the first
matching child, exclude the requesting sibling where applicable and validate
native object lifetime. This is not a replacement actor registry or a recursive
scene service cache.

`component_lookup_probe.gd` verifies positive and negative results, self exclusion,
reordering, removal/free, off-tree edits, reattachment and reparenting. Ten thousand
stable reads allocated zero managed bytes. The probe is included in the runner,
which now registers 84 headless checks. The complete run passed all 84 with a
zero-warning/error build; native rendering probes were not all repeated.

The native combined benchmark improved to 307.475 ms median / 350.329 ms p95,
23,477 ms bulk spawning and 3/200 arrivals in 15 seconds (50 frames). There were
25 evicted chunks and 999 resident chunks. Its nonzero exit remains correct:
the combined performance gate is still failing. Earlier comparison was
764.408 ms median and 38,204 ms spawning. Individual runs are not controlled
statistical performance guarantees, and more profiling is required.

## Work Clock Discovery Cost

Five of six managed stack samples during bulk worker creation were inside
recursive scene discovery for a missing `GridWorkClockComponent`. Workers now
share clock discovery with `GridWorkClockBinding`, using a native Godot group
registered in `_EnterTree` and removed in `_ExitTree`. Empty paths search only
clocks within the current scene; explicit paths remain authoritative, including
explicit references outside that scene. This removes the growing full-scene
scan without inventing another service registry or changing work-turn units.

The component lookup probe covers scene isolation, missing explicit paths,
explicit external references, removal and reattachment. Game-clock axis checks
also passed. The initial new fixture incorrectly nested CurrentScene beneath
the test node; it was corrected to attach the fixture directly to SceneTree.Root.
After that correction, the zero-warning/error build and all 84 headless checks
passed again. The 12-test native rendering set was not rerun in this checkpoint.

Native combined benchmark after this change: 36,843 ms ready, 3,892 ms spawning,
310.841 ms median frame, 351.093 ms p95, and 3/200 arrivals in 15 seconds.
Bulk spawning previously took 23,477 ms. Per-frame performance did not improve;
the benchmark correctly exits nonzero and remains unqualified. These are
single-run measurements, not statistical guarantees. Further work must profile
the simulation and navigation paths separately from scene creation.

## Motion Component Enumeration

A fresh six-sample managed stack capture of the native combined workload found
movement checks, actor position updates and navigation rule construction. The
`CharacterMotion.HasKnockback` sample still used a native child-array allocation.
`CharacterMotion` now reads the same invalidated direct-child snapshot used by
component lookup, through an internal read-only span. Both knockback detection
and final velocity arbitration read current ability state on every call; this
does not cache which ability is active, collapse duplicate components to the
first inactive match, or skip offscreen simulation.

The lookup regression verifies a later active knockback component, live
enable/disable changes and removal. Ten thousand motion queries allocate zero
managed bytes after warmup. Existing child mutation and reparent tests cover
snapshot invalidation. Build passed with zero warnings/errors.
All 84 headless regression checks subsequently passed, including movement ability
arbitration, reattachment, navigation, worker jobs and save scope. The separate
12-probe native rendering set was not repeated.

The native rerun measured 282.338 ms median / 348.511 ms p95, 4,307 ms spawning,
54 rendered frames and 5/200 arrivals in 15 seconds. It still exits nonzero.
The preceding unsampled run was 310.841 ms median; this modest single-run change
is not performance qualification. Actor position/pin updates, navigation rule
construction and native integration remain profiling candidates.

## Actor Position Publication Cost

`ActorRegistryComponent.UpdatePosition` now reads the body's global position
once and passes that same value into chunk pinning and spatial indexing.
`RefreshChunkPins` also supplies one position value per actor. Values are not
cached across calls: custom teleports, projection changes and service changes
still take effect immediately. Missing/nonfinite positions still release pins
without inserting an invalid spatial coordinate. Pin replacement emits no
callbacks that could move the body between these two consumers.

`GridPathFollowerComponent.AdvancePath` now returns for inactive/idle followers
before resolving actor movement authority. Active and pending paths still use
the existing ownership, death and knockback arbitration. This avoids unnecessary
actor queries for idle units; it does not stop their economy or physics updates.

The build passed with zero warnings/errors. Seven targeted probes passed:
actor spatial indexing, chunk pins, actor reattachment, actor residency, motion
abilities, follower requests and worker arrival. The full 84-test suite and
12-probe native set were not repeated for this small checkpoint.

The native combined workload measured 265.808 ms median / 304.751 ms p95,
3,404 ms spawning, 58 frames and 5/200 arrivals in 15 seconds. It still fails
the route-completion gate and remains unqualified. The preceding run measured
282.338 ms median / 348.511 ms p95; these are individual runs, not controlled
statistical guarantees. Main-thread actor updates and navigation still need
substantial improvement.

## Direct Navigation Edge Checks

`CanTraverse`, `CanTraversePath` and `IsBlocked` now build legality-only rules,
without normalizing cost tables or computing heuristic cost floors. A* searches
and `TraversalCost` retain full cost setup. No rules are cached across queries,
so authored collection edits, terrain changes and source replacements remain
visible immediately.

One-edge validation no longer enumerates all neighboring moves to locate its
destination. It calls the same `CanEnterNeighbor` used by A* enumeration, with
unchanged goal permissions, diagonal policies, cliff checks and ramp checks.
The public edge query rejects zero-length/non-adjacent moves using wide integer
differences, including coordinates that would overflow a 32-bit subtraction.

`terrain_navigation_edges_probe.gd` independently checks all 49,152 combinations
of a 3x3 blocked mask, diagonal policy, endpoint permissions and adjacent target.
It also samples direct-route parity with A* and rejects non-adjacent endpoints.
This passed, and the runner now registers 85 headless checks. The build passed
with zero warnings/errors.
The full run subsequently passed all 85, including height/ramp traversal,
scheduled-request invalidation, source rebinding and route-demand tests. The
separate 12-probe native rendering set was not repeated in this checkpoint.

The native combined run measured 258.272 ms median / 324.253 ms p95, 3,533 ms
spawning, 57 frames and 5/200 arrivals. It remains a failing benchmark. Compared
with 265.808 ms median / 304.751 ms p95 previously, these individual runs do not
demonstrate a clear overall performance gain; the tail frame time increased.

## Actor Body Binding

`ActorComponent.Body` now retains its direct Node2D parent instead of calling
the native parent lookup on every read. Native parenting/unparenting
notifications refresh or clear it, including changes outside SceneTree. An
initial lazy lookup also handles a script attached to an already-parented node.
The binding is not cleared merely because a body exits SceneTree: the parent
relationship still exists during detach and exit callbacks.

The component lookup fixture covers orphan state, off-tree attachment,
reparenting, removal, non-Node2D parents and recovery to a valid body. It passed
alongside live actor reattachment, spatial publication and chunk-pin tests.
Build passed with zero warnings/errors.

Native combined result: 238.196 ms median / 272.380 ms p95, 2,310 ms spawning,
64 frames and 7/200 arrivals. The prior run was 258.272 ms median / 324.253 ms
p95. These are single runs, and the benchmark still correctly fails. This is
not full-detail performance qualification or completion of the streaming plan.
All 85 headless checks subsequently passed. The separate 12-probe native set
was not repeated; the combined workload above was run with native OpenGL.

## Physics Catch-Up Diagnosis

The combined native probe now records actual physics-step counts, configured
tick rate, wall measurement duration, pending navigation requests, moving and
waiting followers, and sampled engine frame/physics monitors. Engine monitor
samples may lag and are not an additive CPU phase breakdown.

Before the idle-worker change, a 15,156 ms measurement rendered 63 frames and
ran 504 physics steps: exactly eight per rendered frame, at a configured 60 Hz.
The sampled physics-step median was 18.821 ms, above the 16.667 ms tick budget.
Only 5 routes had arrived; 192 followers were pending and 3 moving. This shows
physics catch-up and navigation queue backlog, not merely expensive terrain
rendering. Lowering simulation frequency or raising the catch-up cap is not a
solution to the workload target.

Idle workers with automatic claiming disabled now return before actor lookup
and collaborator resolution. Work-clock notifications also return immediately
unless the worker is active and working. Explicit assignment and automatic
claiming still perform their normal resolution; active work retains claim
validation, arrival checks and progress reporting.

Build passed with zero warnings/errors. Five targeted probes passed: worker
arrival, job reservations, cross-queue reservations, worker construction effects
and the actor lab. The full 85-check suite was not repeated for this checkpoint.

Afterward the native run measured 232.716 ms median / 288.085 ms p95, with
6 arrivals, 191 pending followers and 3 moving. It still ran 504 physics steps
over 63 rendered frames; the sampled physics-step median was 19.144 ms. Thus
the idle-worker shortcut does not resolve the physics budget or route backlog.
The combined gate remains failing. Next profiling must target physics actor
publication and scheduled-search throughput rather than extrapolate these
small, noisy frame-time differences into completion.

## Longer Runtime Trace

A ten-second .NET sampled-thread-time trace is available at
`tests/output/terrain_actor_scale/cpu.nettrace`. It was collected during the
1,000-worker measurement, not generation. `dotnet-trace` 9.0.661903 was installed
locally. Its supported Windows profile is `dotnet-sampled-thread-time`; the first
attempt using `cpu-sampling` was rejected before collection, then retried with
the supported profile.

The inclusive report identifies substantial archive capture/eviction work,
native Variant finalization, and repeated `Engine.IsEditorHint()` calls, in
addition to actor publication. These are sampled thread-time percentages across
all threads, including waiting thread-pool threads, not CPU utilization or an
additive main-thread breakdown. Archive `CaptureBytes` appears at 7.48%,
`Engine.IsEditorHint` at 6.39%, Variant finalization at 11.41%, and actor position
publication at 2.64% of this aggregate sample. The traced benchmark's frame
times must not be treated as an uninstrumented comparison.

This changes the next action: inspect redundant archive snapshot creation and
per-tick editor-context queries before continuing small movement optimizations.
The full plan remains unfinished, including reduced economic/combat simulation,
streamed terrain fields, remaining genre examples and combined performance.

## Design References

- [Godot TileMapLayer](https://docs.godotengine.org/en/stable/classes/class_tilemaplayer.html)
- [Godot thread-safe APIs](https://docs.godotengine.org/en/stable/tutorials/performance/thread_safe_apis.html)
- [Godot navigation performance](https://docs.godotengine.org/en/stable/tutorials/navigation/navigation_optimizing_performance.html)
- [Godot parenting notifications](https://docs.godotengine.org/en/stable/tutorials/best_practices/godot_notifications.html)
- [Godot performance monitors](https://docs.godotengine.org/en/4.5/classes/class_performance.html)
- [Godot MultiMesh limits](https://docs.godotengine.org/en/stable/tutorials/performance/using_multimesh.html)
- [Factorio worker dispatch](https://www.factorio.com/blog/post/fff-374)
- [Factorio map-generation determinism](https://www.factorio.com/blog/post/fff-200)
- [Widelands economy implementation](https://github.com/widelands/widelands/blob/master/src/economy/economy.cc)
