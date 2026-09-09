# DUP-10 — Arrival is a path-follower fact, not a worker/hauler inference

**Type:** duplication fix · **Area:** `GridWorkerComponent`, `GridHaulerComponent` (+`.ChunkPins`), `GridPathFollowerComponent`, `GridActorTravelComponent` · **Status:** **INVESTIGATED, NOT IMPLEMENTED 2026-09-09** (premise does not hold against the code; see the finding below) · **Effort:** S (½–1 day) · **Risk:** low

## Outcome (not implemented, and why)

Implemented, verified against the probe suite, and reverted. Two things the plan assumed turned out not to hold, and together they make the signal swap a regression rather than a fix.

**1. The signal already exists, and is fine.** `GridPathFollowerComponent` already emits `DestinationReached(int x, int y)` from `FinishMove` at the exact moment the last cell is reached, and `MoveFailed(int x, int y, string reason)` from `FailActiveRoute`. The plan's proposed `Arrived`/`PathFailed` are those two, already there. So the follower needs no change.

**2. The movers are stepped, and they react in their OWN step.** The worker polls arrival in `Tick` and the hauler in `AdvanceWork` (driven by the work clock), each a callback separate from the follower's `_PhysicsProcess`. Subscribing them to `DestinationReached`/`MoveFailed` makes them react SYNCHRONOUSLY inside the follower's step - and the reaction dispatches the next move (`StartWorkOrFail` retries the job-cell fallback; the hauler's `Arrived` begins the depot leg). That new move then advances within the SAME follower pass, so one `settle` cascades several legs where the stepped model advanced one. `terrain_follower_requests_probe` (a failed approach must schedule the job-cell fallback in the worker's next `Tick`) and `terrain_worker_arrival_probe` both fail on this timing shift; they pass the moment the change is reverted.

**3. The one-frame bug the plan cites is already mitigated.** Both movers seed their moving flag at DISPATCH, not from polling `IsMoving`: the worker sets `_wasMoving = true` when it starts a move, the hauler sets it from `MoveToCell(...) == true`. So a one-cell path that starts and finishes inside a frame is still caught next poll - `_wasMoving` is true (seeded) and `IsMoving` is false (finished), so the edge fires. The latent hole the plan describes (`_wasMoving never goes true`) is not reachable from the dispatch paths as they stand.

**What a real fix would take.** A signal-based consolidation has to preserve the stepped reaction - the mover must DEFER `Arrived`/`StartWorkOrFail` to its own next tick rather than run it inside the follower's frame - which is a larger change than "subscribe and delete `_wasMoving`", and it buys nothing over the seeded poll for a bug that is already handled. Left un-done deliberately: the duplication here is two copies of a five-line seeded edge, and resolving it the plan's way trades a real regression for tidiness. If the stepped-reaction constraint is written down, the deferred-signal version can be revisited.

## Finding

Both live movers detect "I have arrived" by polling the follower each frame and comparing to last frame:

- `GridWorkerComponent` — `_wasMoving` (6 sites): `if (_wasMoving && !_follower.IsMoving) OnArrived();`
- `GridHaulerComponent` (+`.ChunkPins.cs`) — `_wasMoving` (8 sites), same shape, plus the chunk-pin partial re-checking it to know when to unpin the route.

`GridPathFollowerComponent` already knows the exact moment it consumes the last waypoint; it emits nothing for it, so two consumers reconstruct the event from a boolean edge. The dormant path (`GridActorTravelComponent`) has its own `Arrived` handling because it does not use the follower at all — that one is a different mechanism and is not a duplicate.

Edge-detection has a real failure: a path of one cell that starts and finishes inside a single frame never shows `IsMoving == true` to the poller, so `_wasMoving` never goes true and the arrival is missed. Both copies share that hole.

## Design

`GridPathFollowerComponent` gains:

```csharp
[Signal] public delegate void ArrivedEventHandler(int x, int y);
[Signal] public delegate void PathFailedEventHandler(string reason);   // already partly there as PathRequest failure
```

emitted from the waypoint consumer at the moment the final cell is reached (and, for the one-frame case, in the same frame the path is accepted). Worker and hauler subscribe and delete `_wasMoving`; `GridHaulerComponent.ChunkPins` releases route pins from the `Arrived` handler.

## Guards

- Pin: `_wasMoving` declared in no file under `ecs/grid/` (the `TopDownController` copy in `ecs/` is a different domain and stays).
- Smoke: worker spawned on cell A, job on adjacent cell B; assert the job starts within one frame of the path being accepted. **Mutation:** revert to edge detection → the one-cell path case fails (this proves the latent bug).

## Dependencies / collisions

`ecs/grid/` collision — small, two files plus the follower. Independent of the rest.

## Out of scope

`GridActorTravelComponent` (dormant travel) — different mechanism by design.
