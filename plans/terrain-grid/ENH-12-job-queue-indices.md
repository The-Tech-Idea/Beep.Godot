# ENH-12 — Job queue: indexed claim, counted states, one pin refresh per mutation

**Type:** enhancement (scaling the settlement loop) · **Area:** `GridJobQueueComponent` (+`.Dispatch`, `.Reservations`, `.SharedReservations`, `.ChunkPins`), `GridWorkerComponent`, `GridWorkerDispatchComponent`, `ui/GridJobBoardComponent`, `ui/GridWorkerStatusPanelComponent` · **Status:** **PARTIALLY IMPLEMENTED 2026-09-09** (one-notification path done; buckets/claim-index/typed-enum pending) · **Effort:** M (2 days) · **Risk:** medium (dispatch fairness/ordering must be preserved — record first)

## Outcome (one-notification, fairness baseline, typed HUD view — 2026-09-09)

**Typed HUD enumeration.** `GridJobBoardComponent` marshalled `GetJobs()` (a Godot `Array<Dictionary>`, one Dictionary per job) per `QueueChanged`, reading every field back through `GridVariantReader`. The queue now exposes an internal typed `EnumerateJobs()` (a `JobSnapshot` record struct), and the board filters/sorts/renders from it — no per-job Dictionary marshal on a mutation. `GetJobs` stays for GDScript/saves; the public `TextForJob(Dictionary)` stays and shares one `FormatJobText` with the typed row text. Guard: a scan pin requires the board to use `EnumerateJobs` and forbids `GetJobs`, mutation-proven by block extraction; `VerifyGridJobBoard` confirms the rows/ordering are unchanged.

**One-notification path.** Every mutation ran the O(jobs) `RefreshChunkPins` pass twice — once in the mutator and again in `EmitQueueChanged`. `EmitQueueChanged` no longer refreshes; the three mutators that were relying on it now refresh explicitly, before they emit: `ClaimJob` (its `ReserveClaim` flips state Queued→Claimed and reserves a work cell), `ClearJobs` (nothing left to want a cell), and `LoadJobs` (the loaded jobs want theirs). `AddJob`/`CancelJob`/`ReleaseJob`/`CompleteJob` already refreshed. Order is preserved (each mutator refreshes before it emits `QueueChanged`), and pin state is unchanged — only the redundant second pass per edit is gone.

Guard: `grid_job_queue_probe` asserts `AddJob`, `ClaimJob` and `CompleteJob` each refresh the pins exactly once (a new internal `ChunkPinRefreshCount`) and emit `QueueChanged` once, and that claim + complete still behave. Mutation-proven: restoring the refresh in `EmitQueueChanged` makes `AddJob` refresh twice. The full job/worker loop (`jobs`, `job-effects`, `worker-spawner`) stays green in the headless smoke.

### Still pending (the larger, fairness-critical half)

- **Bucketed O(1) counts.** `Count(state)` is still three linear scans in `EmitQueueChanged`; needs a `Dictionary<GridJobState, HashSet<string>>` maintained by a centralised `SetState` (state writes are spread across mutators today). Medium risk — a missed transition site silently drifts the buckets.
- **Claim index — and a correction to this plan's design.** `ClaimNextJobExcluding` is still a linear scan per claim (N idle workers × M jobs per tick). **The design above is wrong about fairness:** the live rule (verified in source, `GridJobQueueComponent.cs:124-128`) is highest priority, then **nearest to the claiming worker** (Manhattan distance to its `ApproachCell`), then lowest id — it is NOT "priority then oldest (seq)". A per-kind `PriorityQueue` keyed by `(-priority, seq)`, as sketched, would return the oldest job regardless of distance and silently regress "workers take the nearest job" (the Settlers/AoE behaviour the rule exists for). The **baseline is now recorded and mutation-proven** in `grid_job_queue_probe` (two jobs, one far+older, one near+newer → the near one is claimed; an age/id rule instead of the distance tie-break makes the probe fail, returning the far/older job). So the fairness-preserving scaling fix is the **spatial index** (the design's "second step", now the load-bearing one): queued jobs bucketed by chunk, searched outward from the worker for the nearest high-priority job. That is the larger, careful change, and it must keep the `grid_job_queue_probe` fairness baseline green.

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
