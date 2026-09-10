# ENH-12 — Job queue: indexed claim, counted states, one pin refresh per mutation

**Type:** enhancement (scaling the settlement loop) · **Area:** `GridJobQueueComponent` (+`.Dispatch`, `.Reservations`, `.SharedReservations`, `.ChunkPins`), `GridWorkerComponent`, `GridWorkerDispatchComponent`, `ui/GridJobBoardComponent`, `ui/GridWorkerStatusPanelComponent` · **Status:** **IMPLEMENTED 2026-09-10** (one-notification, cached counts, typed HUD enum, fairness baseline, spatial claim index) · **Effort:** M (2 days) · **Risk:** medium (dispatch fairness/ordering must be preserved — record first)

## Outcome (complete — 2026-09-10)

**Spatial claim index.** `ClaimNextJobExcluding` scanned every job per worker per tick. Queued jobs are now indexed by priority tier (highest first) → `ApproachCell` chunk (`GridCellDataComponent.ChunkOf`) → ids, and a claim visits chunks outward from the worker within the top tier, stopping once the nearest found is closer than any farther ring could be. Fairness is preserved *by construction*, not reimplemented: the spatial and linear paths share one `IsJobClaimable` filter and one `BetterClaim` comparison, so the index only changes the visiting ORDER, never the winner. The ring termination is conservative (never under-visits), the chunk span for its distance bound is derived from `ChunkOf` (not a restated shift), and a fallback to the linear scan when the index yields nothing while queued jobs exist makes a silent undispatch impossible. Maintained at every queued↔non-queued transition (add/claim/release/cancel/approach-move) and rebuilt on load/reservation-rebuild. Guard: `grid_job_queue_probe` runs 300 random multi-chunk layouts asserting the spatial claim equals a brute-force reference, plus release/cancel/approach-move/load maintenance cases; mutation-proven (a too-early ring termination reports a farther job). This was the item whose design the plan had wrong (see the correction note that was here) — it is built against the pinned fairness baseline, not the heap sketch.

## Earlier outcome (one-notification, counts, fairness baseline, typed HUD view — 2026-09-09)

**O(1)-amortized counts.** `QueuedCount`/`ClaimedCount`/`CompletedCount` each scanned every job per read, and `EmitQueueChanged` read all three (three O(jobs) scans per mutation, plus three per job-board summary refresh, more from the minimap). They are now cached and recomputed in a single pass, lazily, the first read after a mutation marks the cache dirty; the rest of that frame hits the cache. Chosen over the plan's incremental per-state buckets because it is far lower risk: over-invalidation only forces a recompute (never a wrong count), so only a *missed* invalidation could go stale — and there are just two chokepoints (`EmitQueueChanged`, which all seven mutators reach, and `RebuildReservations`, whose conflict requeue changes state outside a mutator). The private `Count(state)` scan is gone. Guard: `grid_job_queue_probe` checks the count values after AddJob→claim→complete; mutation-proven (dropping the dirty flag leaves AddJob reporting 0/0/0).

**Typed HUD enumeration.** `GridJobBoardComponent` marshalled `GetJobs()` (a Godot `Array<Dictionary>`, one Dictionary per job) per `QueueChanged`, reading every field back through `GridVariantReader`. The queue now exposes an internal typed `EnumerateJobs()` (a `JobSnapshot` record struct), and the board filters/sorts/renders from it — no per-job Dictionary marshal on a mutation. `GetJobs` stays for GDScript/saves; the public `TextForJob(Dictionary)` stays and shares one `FormatJobText` with the typed row text. Guard: a scan pin requires the board to use `EnumerateJobs` and forbids `GetJobs`, mutation-proven by block extraction; `VerifyGridJobBoard` confirms the rows/ordering are unchanged.

**One-notification path.** Every mutation ran the O(jobs) `RefreshChunkPins` pass twice — once in the mutator and again in `EmitQueueChanged`. `EmitQueueChanged` no longer refreshes; the three mutators that were relying on it now refresh explicitly, before they emit: `ClaimJob` (its `ReserveClaim` flips state Queued→Claimed and reserves a work cell), `ClearJobs` (nothing left to want a cell), and `LoadJobs` (the loaded jobs want theirs). `AddJob`/`CancelJob`/`ReleaseJob`/`CompleteJob` already refreshed. Order is preserved (each mutator refreshes before it emits `QueueChanged`), and pin state is unchanged — only the redundant second pass per edit is gone.

Guard: `grid_job_queue_probe` asserts `AddJob`, `ClaimJob` and `CompleteJob` each refresh the pins exactly once (a new internal `ChunkPinRefreshCount`) and emit `QueueChanged` once, and that claim + complete still behave. Mutation-proven: restoring the refresh in `EmitQueueChanged` makes `AddJob` refresh twice. The full job/worker loop (`jobs`, `job-effects`, `worker-spawner`) stays green in the headless smoke.

### The design's fairness correction (now resolved)

The design below sketched the claim index as a per-kind `PriorityQueue` keyed by `(-priority, seq)` — i.e. highest priority then **oldest**. That was wrong about the live rule (`GridJobQueueComponent.cs`), which is highest priority then **nearest to the claiming worker** (Manhattan to `ApproachCell`) then lowest id. A seq-keyed heap would have silently regressed "workers take the nearest job" (the Settlers/AoE behaviour). The fairness baseline was recorded and mutation-proven first, and the implemented claim index is the chunk-bucketed **nearest**-job search built against that baseline — see the Outcome above. ENH-12 is complete.

## Finding

| Site | Cost today |
|---|---|
| `GridJobQueueComponent.ClaimNextJobExcluding` `:114-133` | Linear scan of all jobs per claim attempt, filtering by state, kind allow-list, exclusions, then picking by priority/age. Every idle worker attempts a claim every dispatch tick, so N idle workers × M jobs per tick. |
| `EmitQueueChanged` `:382-389` | Calls `Count(state)` three times (three more linear scans) to fill the signal's `(queued, claimed, completed)`, and then `RefreshChunkPins()` — which every mutator that reaches `EmitQueueChanged` has already called (DUP-09). |
| `GetJobs()` (Dictionary marshal of every job) | Consumed by `GridJobBoardComponent.VisibleJobs` on every `QueueChanged` (sorts the marshalled list) and, before the recent fix, by the worker panel per worker; `GetClaimedJobIdsByWorker` / `FindClaimedJobId` now exist for the latter, showing the shape of the fix. |
| `GridToolActionComponent.FindResourceNodeAt` | Scans the whole `GridResourceNodeComponent` group for a cell per completed gather job (ENH-14 owns the fix). |

Settlers/OpenTTD-scale job boards keep jobs bucketed by state and kind with a priority heap per bucket; a claim is a heap pop with an exclusion check.

## Design

- **Buckets:** `Dictionary<GridJobState, HashSet<string>>` maintained by the one state-transition method (`SetState(job, next)` — today state writes are spread across mutators; centralise them). `Count(state)` becomes `O(1)`.
- **Claim index:** per job kind, a `PriorityQueue<string, (int -priority, long seq)>` of *queued* job ids; `ClaimNextJobExcluding(worker, kinds, excluded)` pops per allowed kind, skipping excluded/stale entries lazily (re-validate against the bucket; stale ids are dropped). Fairness rule unchanged: highest priority, then oldest (`seq`).
- **Spatial index (optional, second step):** `Dictionary<Vector2I chunk, List<string>>` for "nearest job to worker" dispatch — the hauler/dispatch code already prefers near jobs by scanning; index it if profiling shows it after the buckets.
- **One notification path:** mutators call `Touch(job)` which marks dirty; `EmitQueueChanged` runs once per frame (deferred) with the bucket counts and *does not* refresh pins — pins are refreshed by the mutators through `GridChunkPins.Commit()` (DUP-09).
- **Typed enumeration for HUDs:** `IReadOnlyCollection<GridJob> Jobs` (the typed record) beside `GetJobs()` for GDScript; the job board sorts a typed list without marshalling.

## Guards (fail first)

- Dispatch-order probe: 50 jobs of mixed priority/age, 5 workers → claim order identical to today's (record the sequence first). **Mutation:** pop without the `seq` tie-break → order differs.
- Timing probe: 10 000 queued jobs, 200 idle workers, one dispatch tick < 2 ms. Mutation: restore the linear scan → fails.
- Probe: one `AddJob` produces exactly one `RefreshChunkPins`/`Commit` and one `QueueChanged`. Mutation: restore the refresh in `EmitQueueChanged` → count = 2.
- Existing smoke: claimed-job requeue on load, shared reservations, `job_no_longer_claimed`.

## Dependencies / collisions

DUP-09 (pins). **`ecs/grid/` collision** — the job queue is a likely file for the other session; coordinate.

## Out of scope

Job semantics, reservations model, work-turn accounting.
