# FIX-08 — CompleteJob queued-index leak: force-completing a queued job orphans its id in the spatial index

**Type:** fix · **Area:** `GridJobQueueComponent.CompleteJob` (`GridJobQueueComponent.cs`), `GridJobQueueComponent.Reservations.cs`, one test hook + `GridJobQueueSmoke` · **Status:** **PROPOSED 2026-09-11** · **Effort:** XS (½ day) · **Risk:** low

## Finding

`GridJobQueueComponent` maintains `_queuedIndex` — a per-priority `Dictionary<Vector2I, List<string>>` mapping chunk → job ids — that `ClaimNextJobExcluding` walks outward from the worker to find the nearest claimable job (ENH-12). Every state exit from `Queued` is responsible for removing the job's id from that index. One exit does not.

1. **`CompleteJob` never removes a still-`Queued` job from `_queuedIndex`.** `CompleteJob` (`GridJobQueueComponent.cs:338-356`) calls `ReleaseReservation(job)` (`:347`), flips `job.State = Completed`, and — with the default `RemoveCompletedJobs = true` (`:46`) — deletes the job from `_jobs` at `:352-353`. It never calls `IndexRemoveQueued`. `ReleaseReservation` (`Reservations.cs:54-56`) returns early for any job whose state is not `Claimed`, so for a `Queued` job it does nothing at all — no index cleanup happens there either. The id therefore stays in its `_queuedIndex` chunk bucket while the `GridJob` it names is gone from `_jobs`.

2. **The sibling state-exit gets it right, which is what makes this the odd one out.** `CancelJob` (`:104-119`) explicitly guards `if (job.State == GridJobState.Queued) IndexRemoveQueued(job);` at `:110` before flipping to `Cancelled`. `ReserveClaim` (`Reservations.cs:43-52`) removes the job from the queued index at `:45` as it becomes `Claimed`. So a job leaves `_queuedIndex` on cancel and on claim — but not on a direct `Queued → Completed`.

3. **The "pruned on next touch" comment is false for this case and will misdirect the next maintainer.** `ScanChunk` (`:257-259`) skips a stale id with the comment `// a stale id (pruned on its next index touch)` — but a job removed from `_jobs` receives no further index touch: `IndexRemoveQueued`/`IndexAddQueued` are only ever called with a live `GridJob`, and the bucket is only rebuilt by `RebuildQueuedIndex` (`:300-306`), which runs on restore (`Reservations.cs:89`). So the orphaned id and, once it is the last id in that chunk, the chunk bucket itself persist for the lifetime of the component. Each subsequent `SearchTierNearest`/`ScanChunk` outward walk visits that dead chunk (it counts toward `chunksTotal` at `:214`), doing a failed `_jobs.TryGetValue` per visit forever.

**Why it is low, and why it is still real.** No in-tree path reaches `CompleteJob` on a `Queued` job today: worker completion flows through `AdvanceClaimedWorkCore` (`:384-395`), which rejects unless `job.State == GridJobState.Claimed` (`:389`) and only then calls `CompleteJob` at `:394` — by which point `ReserveClaim` has already removed the index entry. The leak is reachable only by the **public** `CompleteJob(id)` API on a job the caller never claimed (an integrator force-completing a queued job, e.g. a scripted objective grant). `GridJobQueueComponent` is a shipped `[GlobalClass]` reusable component, so its public contract must hold regardless of the in-tree caller set: the index is a private invariant of the component, and a public method that corrupts it is a defect even when nothing internal trips it. The symptom is a slow leak, not wrong dispatch — `QueuedCount` (`:476`) is computed from job states, not the index, so it stays correct; `IsJobClaimable` (`:154-163`) rejects the vanished id, so no phantom job is ever claimed. What degrades is the outward-ring search: dead chunks accumulate and are re-visited on every claim.

## Design

One owner, one exit rule: **every `Queued`-state exit removes the job from `_queuedIndex`.** `CancelJob` and `ReserveClaim` already do; `CompleteJob` is brought in line.

- In `CompleteJob`, before flipping state, add `if (job.State == GridJobState.Queued) IndexRemoveQueued(job);` — the exact guard `CancelJob` uses at `:110`. Placed before `ReleaseReservation`/the state flip, it removes the index entry for the direct `Queued → Completed` case; the `Claimed → Completed` case is unaffected (the entry was already gone at claim time, and `IndexRemoveQueued` on an absent id is a no-op — it early-returns when the chunk bucket has no such id, `:286-298`).
- **Correct the misleading comment** at `:259`. The "pruned on next index touch" claim is only true for an id that is still in `_jobs` and will be re-indexed; reword it to say the skip tolerates a transiently stale id and that live state exits (cancel/claim/complete) own removal — so the surviving `_jobs.TryGetValue` guard reads as defence-in-depth, not as the prune mechanism.

This is a two-line behavioural change plus a comment; no rename, no new type, no signature change. No legacy shim — the fix restores the existing invariant rather than adding a compatibility path.

Not in this change: a periodic index compaction / sweep. It would be a second mechanism papering over exit sites that fail to clean up, which is the opposite of one-owner-per-exit. If a future audit finds another leaking exit, that exit is fixed, not swept.

## Guards (fail first)

The leak is a private-index invariant with no externally observable dispatch symptom (see Finding), so a behavioural-only assertion cannot catch it. Add a minimal test hook, following the existing `internal int ChunkPinRefreshCount` precedent (`GridJobQueueComponent.ChunkPins.cs:28`) — a real consumer (the smoke below) ships in the same change, satisfying rule 6:

- **Test hook (new):** `internal int QueuedIndexEntryCount` on `GridJobQueueComponent` — sum of `ids.Count` across every chunk bucket in every tier of `_queuedIndex`. Read-only, computed on access, no state.
- **Smoke assertion (new, in `GridJobQueueSmoke`):** add one queued job (`AddJob`); assert `QueuedIndexEntryCount == 1`. Call `CompleteJob(id)` with **no** worker id (the `Queued → Completed` public path). Assert `QueuedIndexEntryCount == 0` **and** `HasJob(id) == false` (confirming the job really left `_jobs`, so the 0 is index cleanup, not a live entry). Then run a claim from a distant worker cell and assert it does not fault on the (now absent) chunk.
- **Mutation that trips it:** revert the one-line `IndexRemoveQueued` guard in `CompleteJob`. With the job removed from `_jobs` but its id left in the bucket, `QueuedIndexEntryCount` reads `1` after `CompleteJob` → the assertion fails. Re-apply the fix → `0` → passes. (Confirm the hook itself can fail: temporarily assert `== 1` after the fix and watch it fail, proving the counter reflects the index and not a constant.)

Optionally pin in `tests/addon_contract_scan.ps1` that `CompleteJob`'s body contains an `IndexRemoveQueued` call, mirroring the scan-pin style used elsewhere; the smoke is the primary guard.

## Dependencies / collisions

- **ENH-12** (job-queue spatial claim index) — this fix defends the index that plan introduced; it touches only the exit-site cleanup, not the claim-search or ring-walk logic, so it composes cleanly.
- No collision with the concurrent `TerrainWorldComponent`/streaming/archive session — this is confined to `GridJobQueueComponent`'s reservation/index maintenance and its smoke.
- Independent of **ENH-01** (typed `CellsChanged`), **ENH-02** (edit-kind classification), **DUP-05/DUP-13** (resource/kind normalisation) — no shared surface.

## Out of scope

- Deduplicating the shared "remove from queued index" pattern across `CancelJob`, `ReserveClaim`, `CompleteJob`, and the re-cell move at `:437` into one helper. The three sites already call the single `IndexRemoveQueued` owner; wrapping the `if (Queued)` guard is a cosmetic refactor, not this fix.
- Any change to `RemoveCompletedJobs` semantics or to the `Claimed → Completed` execution path.
- A background index-compaction sweep (see Design — deliberately rejected).
- The `AdvanceClaimedWorkCore` completion flow and worker/dispatch restore (FIX-07 territory).
