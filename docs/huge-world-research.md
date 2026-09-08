# Huge World Implementation Research

Research checkpoint: 2026-09-07. This is an implementation direction, not a
completion claim or a performance qualification. No runtime changes accompany it.
It supplements `actors-and-world-streaming.md`.

## Findings and Constraints

Godot recommends avoiding repeated path queries and simultaneous agent replans.
Keep the addon's scheduled grid searches; do not replace grid movement with a
navmesh merely to follow a tutorial. Measure queue latency as well as search cost.
[Godot navigation performance](https://docs.godotengine.org/en/stable/tutorials/navigation/navigation_optimizing_performance.html)

Active scene-tree mutation is not thread-safe. Generate isolated CPU data on
workers and publish native scene/texture changes through a bounded main-thread
queue. Shared AStar helper instances cannot be queried concurrently.
[Godot thread-safe APIs](https://docs.godotengine.org/en/stable/tutorials/performance/thread_safe_apis.html)

MultiMesh batches have all-or-none visibility, not per-instance culling. Spatially
partitioned batches can help repeated visuals, but cannot fix actor simulation or
archive serialization. The tutorial itself warns that it is not yet updated for
4.7; validate any chosen API against the installed engine before adopting it.
[Godot MultiMesh guidance](https://docs.godotengine.org/en/stable/tutorials/performance/using_multimesh.html)

Factorio describes event-driven activation and chunk registration counters that
avoid repeatedly scanning overlapping areas. Apply that principle to idle work
and spatial membership; its published gains are not predictions for this addon.
[Factorio optimization report](https://www.factorio.com/blog/post/fff-421)

Parallel chunk generation can become nondeterministic through completion order
and callbacks. Test generation order and worker-count independence explicitly.
[Factorio determinism failure analysis](https://www.factorio.com/blog/post/fff-415)

Official Godot demos provide version-matched native examples, not proof of this
addon's performance or cross-genre completeness. Use the matching stable branch,
not master, when borrowing API patterns.
[Godot demo projects](https://github.com/godotengine/godot-demo-projects)

## 1. Huge Map and 1,000 Actors

Observed addon boundaries: ActorComponent publishes position each physics tick;
GridCellArchiveComponent captures native dictionary snapshots for saving and
again during verified eviction. GeneratedTerrainField retains whole-map gameplay
arrays and packed fine samples. These are separate costs from drawing.

Implementation order:

1. Establish repeatable native measurements: identical seed, renderer, camera,
   1024x1024 map, 1,000 actors and 200 simultaneous routes. Separate idle, moving,
   combat, archive-churn and camera-travel workloads. Include animated actors.
2. Attribute time and allocations to physics, path search, archive capture,
   publication and rendering. Separate traced runs from untraced timing runs.
3. Address archive snapshot allocation/copying first if the profile repeats.
   Introduce budgeted immutable snapshots and encoding. Preserve revision checks,
   metadata isolation, file-content validation and atomic saved-file publication.
   Do not simply delete the second comparison.
4. Update actor spatial/pin membership on meaningful changes, while retaining
   teleport, reparent, map rebinding and moving-grid correctness. Idle actors
   should not perform unnecessary native lookups every physics tick.
5. Preserve fair path scheduling; bound request latency and prevent starvation.
   Evaluate a chunk-level route graph only after measured long-route bottlenecks.
6. Optimize visual batching only when render profiling warrants it. Preserve
   native authored scenes for interactive/full-detail actors.

Acceptance: sustained native frame-time percentiles, physics cost, allocation
rate, process memory and route completion/latency, over multiple seeds and runs.
Set a documented hardware-specific frame budget before qualification. A passing
identity-count fixture or a screenshot is not the performance gate.

## 2. Offscreen Economy and Combat

Proposed architecture, inferred from the research and existing addon contracts:

- Extend stable actor records and the existing work clock, jobs, reservations and
  registry. Do not introduce a second world or a second inventory authority.
- Separate presentation residency from simulation activity. Camera visibility
  must not determine whether production, travel, attacks or death occur.
- Schedule idle economic work by next meaningful event: arrival, supplies,
  completion or interruption. Preserve material conservation and exclusive claims.
- Use a fixed simulation clock and deterministic event ordering. Persist due
  events, orders, health, inventory and simulation time independently of nodes.
- For combat, initially retain detailed simulation around active encounters even
  offscreen. Only add reduced combat for explicitly supported rules with parity
  tests; elapsed-time damage estimates are not equivalent to projectile combat.
- Promote/demote at a defined tick boundary with exactly one authoritative actor
  representation. Restore presentation without replaying completed effects.
- Bound catch-up work; report simulation lag rather than silently skipping events.

Acceptance: matching seeded outcomes with camera present/absent, unload/reload and
save/restore mid-job or mid-fight; no duplicated actors, loot, deliveries, claims
or damage. Include player ownership of many units with no possessed avatar.

## 3. Fully Streamed Terrain Data

Rendered chunk culling and gameplay-cell archives are already useful, but neither
makes GeneratedTerrainField out-of-core. Implement finite-world field streaming
before claiming endless-world support.

1. Keep TerrainWorldComponent as coordinator. Introduce a chunk-addressable field
   store behind terrain query contracts, with explicit pending/ready/failed states.
   Unloaded data must never silently become water or walkable land.
2. Preserve a compact macro plan for continents, water networks, land percentage
   and starts. Generate fine details by world coordinates, seed and recipe version.
   Independent local noise alone cannot enforce global topology constraints.
3. Generate overlapping sample halos sized for contour/filter dependencies; trim
   only after processing. Test identical borders and grass/beach/water alignment
   regardless of request order. Preserve the accepted smooth coastline rendering.
4. Budget CPU fields, native images/textures, navigation, collision, pending jobs
   and IO payloads separately. Evict only unpinned clean or durably saved chunks.
5. Demand-load from camera, actor routes, work sites and structures through a fair
   scheduler. Bound prefetch and cancellation; reject stale generation results.
6. Save edits independently of regenerable base data. Publish a checkpoint manifest
   only after its referenced revisions are durable; recover interrupted writes.

Acceptance: long camera traversal with bounded resident memory, seam checks,
different generation orders/worker counts, reload equivalence, corrupt/missing
archives, cancellation and routes crossing unloaded chunks. Include all painters
and flat/elevated isometric navigation and collision, not just surface textures.

## 4. Genre Demos and Validation

Extend existing authored labs using the UI kit and addon components. No separate
game-specific engine. Inventory existing scenes before declaring a demo missing.

| Scenario | Required integration checks |
| --- | --- |
| RTS | No avatar required; multi-unit ownership, selection, group orders, combat and reinforcements |
| Colony | Autonomous claiming, hauling, construction and offscreen production |
| RPG/party | Possession, companions, interaction, follow and save/restore |
| Platformer | Direct motor, jumping, abilities, collision and respawn |
| Shooter | Aim, firing, faction filtering, projectiles and offscreen encounters |
| Streaming lab | Camera travel, resident/pending counts, memory, pins, loading failures and regeneration |

Each scenario needs automated behavioral tests plus a native interactive run.
Verify UI layout, controls, animation, ownership transfer and save/load. Re-run
the complete existing suite after shared changes; separately report native visual
coverage and performance qualification. Do not treat headless passes as either.

## Patch Sequence

1. Reproducible profiling and archive/actor hot-path correction.
2. Authoritative offscreen economic simulation and encounter-safe combat policy.
3. Chunk-addressable fields, bounded publication and persistence checkpoints.
4. Genre integration gaps and combined long-running qualification.

Add focused regression tests with each step. Retain current terrain appearance
and existing gameplay behavior while changing residency and execution scheduling.

## Snapshot Validation Implementation

The first archive change validates arbitrary metadata while constructing each
cell snapshot, instead of reading every fixed primitive field back through native
Variant dictionaries. Metadata retains its original depth of two beneath the
cell array. Temporary metadata, cell dictionary and array wrappers are disposed
after their contents have been retained by the snapshot or encoded.

The typed cell-array encoding is unchanged. Eviction still compares current
snapshot bytes with the saved file; revision checks and IO admission are unchanged.
The archive probe checks exact byte parity against GetChunkCells, rejects nested
objects, callables, signals, RIDs and object keys, and checks depth 64 acceptance
versus depth 65 rejection without overwriting the previous valid file.

This does not yet eliminate main-thread snapshot encoding or its temporary native
allocations. Budgeted capture/publication and authoritative offscreen simulation
remain required work.

Verification: build passed with zero warnings/errors; all 85 headless checks
passed, including the expanded archive probe. An isolated native scale run after
the suite measured 245.631 ms median / 314.033 ms p95 frame time, 488 physics steps
over 61 rendered frames, and 6 of 200 routes completed in 15 seconds. Generation
readiness was 52,318 ms and actor spawning 4,714 ms. The probe exited 1 because
route completion failed. This does not demonstrate a combined performance gain;
the earlier concurrent run is excluded from timing comparison. Repeat profiling
of actor ticking and physics catch-up remains necessary before qualification.

## Shared Actor Pin Bindings

ActorRegistryComponent now resolves its cell-data and projection services once
per scene-structure change rather than once per actor per physics tick. Runtime
tree entry subscribes to SceneTree.TreeChanged; exit disconnects and releases
pins. Explicit refresh and configured-path changes invalidate the cache too.
Missing paths are cached only until the next structural change.

The cache does not retain projected coordinates or transforms. Teleports and
moving/isometric grids still project each current position, and pin ownership is
still checked against the live cell service. Same-frame renaming, same-path
replacement, freeing, detach/reattach and missing-path recovery are covered by
the expanded terrain_chunk_pins_probe. Build and all 85 headless checks passed.

## Native Diagnostic Comparisons

The scale probe accepts `-- --scale-mode=full`, `terrain-hidden`, `actors-paused`
or `archive-paused`. Every mode generates the same 1024x1024 fixture, spawns 1,000
actors and submits 200 orders before changing the diagnostic condition. Runs are
sequential, native OpenGL, with a fixed camera and a 15-second observation window.
Reports and captures are in `tests/output/terrain_actor_scale` with mode suffixes.

| Mode | Frame median ms | Frame p95 ms | Physics monitor median ms | Draw calls median |
| --- | ---: | ---: | ---: | ---: |
| Full | 261.517 | 353.065 | 21.369 | 216 |
| Terrain hidden | 220.592 | 278.697 | 20.027 | 215 |
| Actors/navigation paused | 6.922 | 123.290 | 0.255 | 216 |
| Archive eviction paused | 142.065 | 155.304 | 19.440 | 216 |

The runtime has two native TileMapLayers. TerrainPaintedRendererComponent uses
the shader-bearing SplatSurface layer plus LogicalGrid. It is incorrect to infer
that the painter does not use TileMapLayer just because the authored scene has a
renderer Node2D rather than an explicit tile-layer node.

Interpretation: actor/navigation processing is a major source of sustained cost;
archive work contributes substantial cost/spikes. Terrain visibility alone does
not remove the slowdown. Do not interpret these differences as additive CPU/GPU
timings or as repeated-run statistical results. The hidden-terrain mode also
affects visibility-gated rebuilding and streaming, not only GPU drawing. The
paused-actor mode retains visuals, physical bodies and pins but disables actor
and navigation processing; it is not a production simulation strategy. Archive
eviction changes resident data over time, so end-of-run states differ by design.

All diagnostic fixtures retained 1,000 actors and 200 accepted orders. Diagnostic
exit code zero means the fixture ran, not that gameplay or performance passed.
The full run still failed route completion (5/200 arrivals); all reports retain
performance_qualified=false. Physics monitors can lag and are not phase profiles.
Generation took roughly 54-57 seconds in these runs and is excluded from the
frame-time window; these comparisons do not diagnose startup publication stalls.

Next: profile actor-only processing with archive eviction disabled, then replace
unnecessary per-node ticking with the planned authoritative simulation scheduling.
Budget archive capture and publication separately. Preserve TileMapLayer-based
rendering and the accepted appearance; measure stationary and moving-camera
terrain-only workloads before attempting renderer-specific changes.

## Actor Callback Scheduling

An eight-second dotnet-sampled-thread-time trace of the archive-paused workload
identified Engine.IsEditorHint native calls inside actor/follower physics callbacks
as a prominent sampled managed path. The trace includes waiting worker threads;
its inclusive percentages are not CPU utilization or additive phase timings.

ActorComponent now checks editor mode during readiness, where runtime registry
binding is established, instead of querying it every physics tick. Editor actors
disable physics processing and never acquire the runtime registry. Runtime exit
continues to clear that registry.

GridPathFollowerComponent tracks runtime readiness and enables physics processing
only while a route is pending or moving. Idle, cancelled and arrived followers
stop receiving automatic physics callbacks. Active/inactive actor checks and
movement/collision rules remain in AdvancePath.

For an external simulation clock, set AutoAdvancePath=false and call AdvancePath
explicitly. This persists across route changes, unlike manually toggling Godot's
processing flag, which automatic route activation now owns. The manual movement
fixtures use this option; the collision/route-retirement assertion remains intact.
The follower-request probe verifies idle, pending, cancellation, arrival, external
clock opt-out, opt-in and reparented pending-route callback states.

This is callback scheduling, not reduced offscreen economic/combat simulation.
It does not change TileMapLayer rendering or lower the simulation tick frequency.

Verification: build passed with zero warnings/errors and all 85 headless checks
passed after migrating explicitly clocked movement fixtures to AutoAdvancePath.
The unchanged route-retirement collision assertion passes without double ticks.

Sequential untraced native runs after this change:

| Mode | Frame median ms | Frame p95 ms | Physics monitor median ms | Arrivals / 200 |
| --- | ---: | ---: | ---: | ---: |
| Full | 188.929 | 267.195 | 14.623 | 6 |
| Archive eviction paused | 49.924 | 65.961 | 14.168 | 3 |

The preceding untraced diagnostic medians were 261.517 ms full and 142.065 ms
archive-paused. These are individual observations, not repeated-run qualification.
The full probe still exits 1. The paused-archive run reports diagnostic success
only: 197 path requests remain pending and performance_qualified remains false.
Archive stalls and path-request latency remain priorities alongside further actor
scheduling work. Neither these runs nor the headless suite prove animated-actor
scale, reduced offscreen simulation, or fully streamed terrain-field completion.

## Reachability and Eviction Starvation

The original native fixture used IsBlocked to discover a supposedly connected
spawn region. An audit found 63 discovery edges that CanTraverse rejected under
the configured height/ramp rules. This did not prove every selected goal was
unreachable, but invalidated the fixture's connectivity guarantee. The audit is
retained as `tests/output/terrain_actor_scale/report_unvalidated-spawns.json`.

The revised discovery uses CanTraverse and marks a cell visited only after a
valid incoming edge. Invalid approaches do not prevent another legal approach.
Reports carry fixture_revision=traversable-connected-spawns. Do not directly
compare route counts or timings between these different spawn/goal sets.

Before the engine fix, the revised archive-paused fixture produced 67 successful
paths, 133 pending requests and no arrivals in 15 seconds. With eviction enabled,
it produced no completed paths and retained all 200 requests. Neither run waited
for terrain loading. The full fixture's median frame time was 183.005 ms.

Root cause found in the scheduler: every verified chunk eviction incremented the
global navigation revision and restarted every active demand search, including
searches whose observed terrain remained pinned. Continuous unrelated evictions
could prevent long searches from ever completing.

GridCellDataComponent now tracks a separate PinnedNavigationRevision. All prior
navigation mutations advance both revisions except verified eviction, which
advances only the global one. Demand searches use the pinned revision because
their TerrainDemand observes and pins cells before reading them; eviction cannot
remove those chunks. Non-demand searches retain global invalidation. Changing
LoadMissingTerrain is also part of the captured request configuration.

Explicit availability changes, archive restoration, terrain/height/blocked edits,
source and rule changes retain invalidation. An unobserved evicted chunk is still
unknown when subsequently encountered and enters the ordinary demand-load path.
This does not turn missing terrain into traversable terrain or disable pin checks.

The new terrain_search_eviction_probe exercises a 501-cell route through 40
unrelated evictions, actual-edit invalidation and non-demand invalidation. The
full runner now includes 86 checks.

Verification: build passed with zero warnings/errors; all 86 headless checks
passed. In the revised full native fixture, 18 searches completed while 42 chunks
were evicted (previously zero completed searches in the same fixture). There
were 182 pending requests and no arrivals at 15 seconds. Frame median/p95 were
164.713/221.303 ms; readiness was 37,503 ms and spawning 1,476 ms. The run still
exited 1 and remains unqualified. This establishes progress through eviction,
not acceptable performance or completion of the huge-world roadmap.

## Staged Archive Capture

RequestSaveChunk now copies request-time records, including detached mutable
metadata, before constructing native dictionaries incrementally. The default
batch is 32 cells, clamped to 1..256, with a 2 ms elapsed-time check between cells.
Workers still receive only detached bytes. Cancellation, rebinding, late encoding
failure and destruction release admission without replacing existing saved data.

This is not a strict frame budget: initial record copies, individual metadata
encoding, final binary/base64/JSON serialization, synchronous saves and verified
eviction comparisons can still stall. Shared admission limits payload envelopes,
not total transient memory. Fully streamed terrain fields remain unfinished.

Verification: build passed with zero warnings/errors; all 87 headless checks
passed, including terrain_capture_budget_probe. One sequential native full run
of the same traversable-connected-spawns fixture reported:

| Measurement | Previous | Staged capture |
| --- | ---: | ---: |
| Frame median ms | 164.713 | 72.921 |
| Frame p95 ms | 221.303 | 127.858 |
| Completed path searches | 18 | 49 |
| Evicted chunks | 42 | 5 |
| Arrivals / 200 | 0 | 0 |

The lower eviction throughput matters: this spreads work across frames, not an
equivalent-work throughput speedup. The latest run retained 1,019 chunks and
1,043,456 resident cells, with 151 pending paths and eight active searches.
Readiness took 38,073 ms and spawning 1,571 ms. It still exited 1, with both
functional_complete and performance_qualified false. Terrain painting remained
unchanged on two native TileMapLayers. These are single observations, not
repeated-run or animated-actor qualification.

## Navigation Budget Diagnostic

The native scale probe now records sampled scheduler milliseconds and heap
expansions alongside the configured budgets. A subsequent unchanged-engine full
run recorded median/p95 navigation time of 2.2972/2.7694 ms, median 256 expansions,
and 52,221 sampled expansions across 229 frames in 15,009 ms. The configured
limits were 2 ms and 1,024 expansions per frame. Time checks occur between search
batches, so the elapsed target is not a hard deadline. Samples describe the most
recent scheduler pass, not a new cumulative engine counter.

This supports time-budget saturation rather than expansion-limit saturation.
At low render-frame frequency the queue also receives fewer slices per second.
There were 59 successful searches, 141 pending, eight active and no terrain-waiting
requests. No actor arrived. Median/p95 frame times were 56.644/106.589 ms, with six
evictions. Only instrumentation changed, so differences from the preceding run
are run variation, not evidence of a new optimization.

Do not increase the scheduler budget as a substitute for fixing frame cost.
Next profiling should separate per-search traversal cost from moving followers'
repeated live-edge validation and reference/rule preparation. In particular,
RefreshCellSegment calls CanTraverse on each active movement update; CanTraverse
resolves sources and builds normalized blocking rules. Any reuse must preserve
live terrain, height/ramp, occupancy, authored-rule and source-change detection.

## Economic Dispatch Budget (2026-09-07)

Godot's [optimization guidance](https://docs.godotengine.org/en/stable/tutorials/performance/general_optimization.html)
recommends timing suspected work and distinguishes sustained cost from stalls.
Its [Time documentation](https://docs.godotengine.org/en/stable/classes/class_time.html)
specifies monotonic tick timers for elapsed durations rather than system time.

The work-clock queue previously capped only callback count. It now also has a
cooperative 2 ms dispatch budget using Time.GetTicksUsec, checked between entries.
Callbacks remain atomic; an expensive callback can overshoot. The measured
duration and callback count are exposed, and zero disables time admission for
count-only deterministic batch execution. This does not budget ordinary WorkTick
subscribers or make the entire frame bounded. The deadline probe deliberately
exceeds the budget inside one callback and checks that remaining work is queued
with elapsed turns intact. The production probe also drains 1,000 node-free
records with timing enabled and checks exact inputs and outputs.

This addresses economic callback admission, not the measured navigation/render
bottleneck above. No improved native scale result is claimed for this change.

## Current Native Baseline (2026-09-07)

A fresh full-mode run after economic scheduling changes, with no navigation or
renderer optimization applied, recorded 59.977 ms median / 125.432 ms p95 frame
time. It rendered 218 frames in 15.017 seconds (about 14.5 FPS overall), with
61,030 ms world readiness and 1,570 ms actor spawning. There were 59 successful
path searches, 141 pending requests, seven active searches and zero arrivals.
The fixture still used 1,000 actors, 200 orders and two native TileMapLayers.
The native gate remains failed; these results show no measured improvement over
the earlier 56.644/106.589 ms run. They are individual runs, not proof of a
regression caused by economic scheduling. The runtime MCP bridge was enabled
and attempted reconnects in this run, unlike the earlier disabled-bridge setup;
future controlled comparisons must disable it consistently.

The 15-second arrival count is an observation, not an isolated navigation metric:
route length, movement speed and time spent waiting for a path all contribute.
No path-length-adjusted completion qualification has been established here.
Authoritative output: tests/output/terrain_actor_scale/report.json and run_full.log.
