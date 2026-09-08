# DUP-10 — Arrival is a path-follower fact, not a worker/hauler inference

**Type:** duplication fix · **Area:** `GridWorkerComponent`, `GridHaulerComponent` (+`.ChunkPins`), `GridPathFollowerComponent`, `GridActorTravelComponent` · **Status:** proposed 2026-09-08 · **Effort:** S (½–1 day) · **Risk:** low

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
