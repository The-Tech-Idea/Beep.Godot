# DUP-09 — One chunk-pin helper; chunk arithmetic in one place

**Type:** duplication fix · **Area:** `GridCellDataComponent.Pins`, `*.ChunkPins.cs` partials (7), `GridCameraDemandComponent`, `TerrainMotionGateComponent`, `TerrainVisualSnapshot`, `GridCellArchiveComponent.Loading`, `GridNavigationComponent.TerrainDemand` · **Status:** proposed 2026-09-08 · **Effort:** M (1–2 days) · **Risk:** medium (streaming correctness)

## Finding

`ChunkedCellStore<T>.ChunkFor(cell) => cell >> 5` is the chunk rule. It is re-derived inline **52 times in 12 files** (`>> 5` scan), and the pin-refresh pattern — compute the chunk set an owner needs, diff against last frame's set, `PinChunk`/`UnpinChunk` on the delta — is written seven times:

| Partial | `>> 5` sites | `RefreshChunkPins` sites |
|---|---|---|
| `GridPathFollowerComponent.ChunkPins.cs` | 8 | — (own `_pinned` set) |
| `GridCameraDemandComponent.cs` | 8 | — |
| `GridActorTravelComponent.cs` | 6 | — |
| `GridJobQueueComponent.ChunkPins.cs` (+`.cs`, `.Reservations.cs`) | 6 | 3 + 6 + 1 |
| `GridHaulerComponent.ChunkPins.cs` (+`.cs`) | 4 | 3 + 9 |
| `GridObjectComponent.ChunkPins.cs` (+`.cs`) | 4 | 2 + 5 |
| `actors/ActorRegistryComponent.ChunkPins.cs` (+`.cs`) | 2 | 4 + 1 |
| `GridCellDataComponent.Eviction.cs`, `TerrainVisualSnapshot`, `GridCellArchiveComponent.Loading`, `GridNavigationComponent.TerrainDemand`, `ChunkedCellStore` | 4, 4, 2, 2, 2 | — |

Two consequences already visible:

- `GridJobQueueComponent.EmitQueueChanged` calls `RefreshChunkPins()` although every mutator that reaches it already called it — a double diff per mutation (see ENH-12).
- `GridNavigationComponent.TerrainDemand.cs:34-35` creates a `Node { Name = "PathTerrainDemand" }` **per path request** purely to have a pin owner with a `TreeExiting` hook (see ENH-03) — because pins are keyed by `Node` instance id, there is no lighter token.

## Design

`GridChunkPins` (sealed class, `ecs/grid/`), one instance per pin owner:

```csharp
public sealed class GridChunkPins : IDisposable
{
    public GridChunkPins(GridCellDataComponent cells, Node owner);     // Node owner: TreeExiting releases
    public GridChunkPins(GridCellDataComponent cells, GridPinToken token); // ENH-03: lightweight owner
    public void Want(Vector2I chunk);                 // accumulate this frame's wanted set
    public void WantCells(IEnumerable<Vector2I> cells);
    public void WantRect(Rect2I cells);
    public void Commit();                             // diff against last, pin/unpin the delta, swap sets (no allocation)
    public void Release();                            // unpin everything
    public IReadOnlyCollection<Vector2I> Pinned { get; }
}
```

`GridCellDataComponent` exposes `public static Vector2I ChunkOf(Vector2I cell)` and `ChunkRect(Rect2I cells)`; the `>> 5` literal appears once, in `ChunkedCellStore`. The seven partials shrink to "which cells do I need" (`WantCells(path.Cells)`, `WantRect(footprint)`, `WantRect(camera view + margin)`) plus one `Commit()`.

## Steps

1. Add `GridChunkPins` and `ChunkOf`; port `GridObjectComponent.ChunkPins` (smallest).
2. Port hauler, job queue (remove the `EmitQueueChanged` refresh), path follower, actor travel, camera demand, actor registry, motion gate.
3. Replace the 52 inline shifts.

## Guards

- Pin: `>> 5` appears only in `ChunkedCellStore.cs`. Mutation: restore one inline shift → fails.
- Pin: `RefreshChunkPins(` declared nowhere (the method name goes with the pattern).
- Streaming probes (`actors/streaming_lab`, capture-budget probe, eviction probes): pinned-chunk sets before/after for a moving hauler, a queued job at a far cell, and a camera pan are identical to the current implementation (record them first, then refactor).
- Mutation for the diff logic: make `Commit()` skip unpin → the eviction probe's "unpinned chunk is evictable after the hauler leaves" assertion fails.

## Dependencies / collisions

**Direct collision** with the other session's `ecs/grid` streaming work — this must be coordinated, not landed blind. Prerequisite for ENH-03 (pin tokens) and simplifies ENH-12.

## Out of scope

Pin *policy* (who pins what, preload margins) — unchanged; only the mechanism moves.
