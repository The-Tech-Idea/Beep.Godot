# ENH-12 — Job queue: indexed claim, counted states, one pin refresh per mutation

**Type:** enhancement (scaling the settlement loop) · **Area:** `GridJobQueueComponent` (+`.Dispatch`, `.Reservations`, `.SharedReservations`, `.ChunkPins`), `GridWorkerComponent`, `GridWorkerDispatchComponent`, `ui/GridJobBoardComponent`, `ui/GridWorkerStatusPanelComponent` · **Status:** proposed 2026-09-08 · **Effort:** M (2 days) · **Risk:** medium (dispatch fairness/ordering must be preserved — record first)

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
