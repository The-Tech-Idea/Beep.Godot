# DUP-09 — One chunk-pin helper; chunk arithmetic in one place

**Type:** duplication fix · **Area:** `GridCellDataComponent.Pins`, `*.ChunkPins.cs` partials (7), `GridCameraDemandComponent`, `TerrainMotionGateComponent`, `TerrainVisualSnapshot`, `GridCellArchiveComponent.Loading`, `GridNavigationComponent.TerrainDemand` · **Status:** **implemented 2026-09-09** (arithmetic centralised + helper; 4 of 7 owners ported — see Outcome) · **Effort:** M (1–2 days) · **Risk:** medium (streaming correctness)

## Outcome (implemented 2026-09-09)

Build clean (0 warnings). The full chunk/streaming/demand/eviction/route/worker/follower probe set is green and byte-for-byte unchanged from the pre-refactor baseline, including the DUP-10-sensitive `terrain_worker_arrival_probe` and `terrain_follower_requests_probe`, plus `actor_residency/spatial/travel/integration/visual_culling`. One probe, `terrain_search_eviction_probe`, is red BEFORE and AFTER this change (pre-existing "Unpinned search lost eviction invalidation"); it is not touched by the pin mechanism and is out of scope here.

**What shipped.**

- **One chunk rule.** `ChunkedCellStore.ChunkAxis(long) => (int)(coordinate >> 5)` is now the single place the shift lives; `ChunkFor` is `new(ChunkAxis(cell.X), ChunkAxis(cell.Y))`. `GridCellDataComponent.ChunkOf(Vector2I)` and `ChunkAxis(long)` are the public faces. All 52 inline `>> 5` sites across 12 files are gone; the scan pins that the literal appears only in `ChunkedCellStore.cs`. Mutation-proven: reintroducing one inline shift fires the pin naming the file.
- **`GridChunkPins`** (new, `ecs/grid/`): a per-owner accumulator — `Bind` / `Want` / `WantCell` / `WantCells` / `WantRect` / `Commit` / `Release` — that reuses one wanted-set across commits (no per-refresh `HashSet` allocation) and hands the set to the store. Ported: `GridObjectComponent`, `GridJobQueueComponent`, `GridHaulerComponent`, `GridActorTravelComponent` — the rebuild-from-scratch, owner-is-self owners.

**Two deliberate departures from the proposal, both load-bearing.**

1. **The diff was never duplicated; only the arithmetic and the resolve/allocate were.** The plan described a "compute set, diff against last, pin/unpin the delta" pattern written seven times. In the code the diff and the refcount and the `TreeExiting` release already live in ONE place — `GridCellDataComponent.ReplaceChunkPins` — and every owner already just built a set and handed it over. So `GridChunkPins.Commit()` delegates to `ReplaceChunkPins` rather than re-implementing the diff; the helper removes the duplicated *resolve-store / rebind-on-change / allocate-a-HashSet / shift-each-cell* block, which is the duplication that was actually there. The unpin path is still guarded: skipping it trips `terrain_chunk_pins_probe` and `terrain_camera_demand_probe` (verified).

2. **Three owners keep their own pin policy; they borrow only the arithmetic.** `GridPathFollowerComponent` maintains a route set incrementally with an expiry priority queue and retires passed chunks; `ActorRegistryComponent` pins a radius box per actor with one pin owner *per actor* and a per-actor change cache; `GridCameraDemandComponent` rebuilds a viewport rectangle every `_Process` frame behind a `_lastChunkBounds` cache that must stay to avoid rebuilding a large set each frame. None of these is a rebuild-from-scratch/owner-is-self shape, so forcing them onto `Want`/`Commit` would either drop their optimisation or change behaviour. They route their shifts through `ChunkOf`/`ChunkAxis` and keep their logic. `GridChunkPins` has four real consumers, well past the two-consumer bar.

**Not done, by design.**

- **`RefreshChunkPins` kept its name.** The proposal's guard wanted the name to disappear, on the theory that the name carried the duplicated diff. It did not — the diff is in `ReplaceChunkPins`. `RefreshChunkPins` is each owner's public "recompute my demand now" entry point with ~30 call sites; renaming all of them is name churn with no invariant behind it, and churning fragile streaming code is exactly what regressed DUP-10. The real invariant — one chunk rule — is pinned and mutation-proven instead. No `RefreshChunkPins`-name pin was added.
- **The `EmitQueueChanged` double-refresh** (a redundant second diff per job-queue mutation) is left in place; it is a harmless no-op through `ReplaceChunkPins`'s `SetEquals` early-out, and removing it belongs to ENH-12, not to an arithmetic dedup.
- **The lightweight `GridPinToken` owner** (second `GridChunkPins` constructor in the proposal) is ENH-03's work and has no consumer yet, so it was not added.

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
