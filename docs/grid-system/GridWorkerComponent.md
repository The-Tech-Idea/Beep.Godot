# GridWorkerComponent

`GridWorkerComponent` is the per-unit agent half of the job system: it claims a job from `GridJobQueueComponent`, drives a `GridPathFollowerComponent` to the job's cell, burns the job's work — measured in **turns**, advanced by [GridWorkClockComponent](GridWorkClockComponent.md)'s `WorkTick` and scaled by `WorkSpeedMultiplier` — and completes or fails the job — emitting a signal at every state transition so UI/AI can react. Where `GridJobQueueComponent` is the shared registry and `GridWorkerSpawnerComponent` is the factory that stamps these out, this component is the state machine that actually executes one worker's lifecycle: Idle → MovingToJob → Working → Idle.

Unlike every other file in this batch, it extends `GameplayComponent` rather than plain `Node` (that base is outside this batch, so its `IsActive`/`base._Ready()` contract isn't established here beyond gating `_Process`). `AllowedJobKinds` exists for a documented reason: "a boat lists 'fish', a survey crew 'survey', and the truck lists the land kinds — otherwise a land worker keeps claiming water jobs it can never path to and churns claim/release forever." The re-check in `StartWorkOrFail` — refusing to begin working if the job is no longer `Claimed` by this worker — is a documented fix for a specific prior bug: "the job may have been cancelled or reassigned while this worker walked to it... working through it anyway wasted the whole duration and then failed at CompleteJob with a misleading reason."

Implements `IWorker` (`WorkerId`/`CurrentJobId` already existed and satisfy it as-is; `IsWorking` is a new one-line `State == WorkerState.Working`) — the formal, minimal contract a fully custom worker (crane, robot, drone, or a GDScript duck-typed one) can also answer, so the construction-in-progress effect family works for any of them, not just this component. `AdvanceWork` also calls `GridJobQueueComponent.ReportProgress(CurrentJobId, WorkRemainingTurns)` on every work tick, which is what makes job progress a fact the queue itself owns rather than something read off this specific class.

## Public API
- `enum WorkerState { Idle, MovingToJob, Working }`.
- `WorkerState State { get; }`, `string CurrentJobId { get; }`, `float WorkRemainingTurns { get; }` — read-only outside the class; the remainder is in turns, the grid's one unit.
- `bool IsWorking { get; }` — `IWorker`'s member; `State == WorkerState.Working`.
- `float EffectiveClaimInterval`, `float EffectiveWorkSpeed` — sanitized (positive, finite) versions of `ClaimIntervalSeconds`/`WorkSpeedMultiplier`.
- `void Tick(double delta)` — the real-time half of the state machine: arrival detection while `MovingToJob` and the claim poll while `Idle`. Both stay on frame delta on **both** time axes — a worker animates between turns, it does not teleport. When no work clock is present it also burns work from `delta`, so a template scene or headless probe still runs unchanged.
- `void AdvanceWork(float turns)` — burns turns off the claimed job while `Working`, reporting `ReportProgress` to the queue and completing at zero. Bound to `WorkTick` when a work clock exists. This split is why one authored `BuildTurns` means the same amount of world time in a turn-based game and an RTS.
- `bool ClaimNextJob()` — asks the resolved queue for the best job within `AllowedJobKinds` at the worker's current cell, then begins it; no-op (returns false) unless `State == Idle`.
- `bool AssignJob(string jobId)` — manual assignment: claims the job if it's still `Queued`; if it's already `Claimed`, succeeds only if claimed **by this same worker id** (idempotent re-entry), otherwise fails with `WorkerFailedJob(..., "claimed_by_another_worker")`.
- `void CancelCurrentJob(string reason = "worker_cancelled")` — releases the job back to the queue, cancels the path follower's move, returns to `Idle`, and emits `WorkerFailedJob`.
- Signals: `WorkerClaimedJob(workerId, jobId, kind, x, y)`, `WorkerStartedJob(workerId, jobId)`, `WorkerCompletedJob(workerId, jobId)`, `WorkerFailedJob(workerId, jobId, reason)`, `WorkerStateChanged(workerId, state)`.
- Exports: `JobQueuePath`, `GridPath`, `PathFollowerPath`, `WorkClockPath` (empty finds one scene-wide), `WorkerId` (auto-generated from parent name + instance id if left blank), `AutoClaimJobs`, `AllowedJobKinds`, `ClaimIntervalSeconds` (real seconds, deliberately — polling cadence is presentation, not gameplay duration), `WorkSpeedMultiplier`.

## Dependencies

- Claims/releases/completes jobs through `GridJobQueueComponent` (this batch): `ClaimNextJob`, `ClaimJob`, `GetJobState`, `GetJobClaimedBy`, `ReleaseJob`, `HasJob`, `GetJobCell`, `GetJobKind`, `GetJobWorkTurns`, `CompleteJob`.
- Resolves `GridProjectionComponent` (outside this batch) for `WorldToCell`, and `GridPathFollowerComponent` (outside this batch, expected as a sibling child of the same parent — `_body = GetParent()`, follower found non-recursively) for `MoveToCell`/`CancelMove`/`IsMoving`.
- Called into by `GridWorkerSpawnerComponent` (this batch), which is the file that actually sets this component's `JobQueuePath`, `GridPath`, `PathFollowerPath`, and `WorkerId` on every spawned unit — this component's own `ResolveReferences` reads back what the spawner wired.

## Notes

- `WorkerId` defaults to `$"{GetParent()?.Name ?? Name}_{GetInstanceId()}"` when left blank in `_Ready`, guaranteeing per-instance uniqueness without authoring input — but this is also exactly why `GridJobQueueComponent.RequeueClaimedJobsOnLoad` defaults on: the instance id half of this string never survives a reload.
- `SetState` is a no-op (no signal emitted) when the new state equals the current one, so `WorkerStateChanged` only ever fires on an actual transition.
- `Tick`'s `MovingToJob` branch detects arrival by edge-triggering on `_wasMoving && !_follower.IsMoving` rather than a follower-arrival signal — meaning arrival is only noticed on the frame after the follower stops, which is fine at frame-rate granularity but is a polling design, not an event-driven one.
- `BeginClaimedJob` first tries the approach cell, then the job cell. It records the accepted destination. Stopped movement alone is not arrival: `HasReachedDestination` must be true and the follower's destination must still match that accepted cell. Failed/cancelled travel releases the claim with `job_destination_not_reached` and cannot start the work timer. `WorkerClaimedJob` reports the job cell, not the approach cell.
