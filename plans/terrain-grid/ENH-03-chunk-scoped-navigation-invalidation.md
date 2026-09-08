# ENH-03 — Navigation invalidates by chunk; a path request is not a Node

**Type:** enhancement (huge-world performance) · **Area:** `GridNavigationComponent` (+`.Requests`, `.Search`, `.TerrainDemand`), `GridCellDataComponent.Pins` · **Status:** proposed 2026-09-08 · **Effort:** M (2–3 days) · **Risk:** medium (pathing correctness)

## Finding

1. **Global restart on any change.** `GridNavigationComponent.Requests.cs:33-36` `RequestConfiguration()` folds `NavigationRevision` and `PinnedNavigationRevision` into the per-request `Configuration` record; any change to the global counter restarts **every** in-flight time-sliced search from scratch (`"navigation_changed"`). Today the counter is bumped by every chunk publish and eviction (ENH-01), so on a streamed map with a moving camera no long route ever finishes: it restarts each time any chunk anywhere changes residency.
2. **A Node per request as pin owner.** `GridNavigationComponent.TerrainDemand.cs:34-35`: `_lease = new Node { Name = "PathTerrainDemand" }; owner.AddChild(_lease);` — one scene-tree node allocated, added and later freed per path request, solely because chunk pins are keyed by `Node` instance id and released on `TreeExiting`. A hundred workers re-pathing after a road change create a hundred nodes in one frame.
3. **Per-frame `Costs` dictionary.** `BuildSearch(includeCosts)` (`.cs:194+`) rebuilds the terrain-kind → cost `Dictionary` from the exported arrays on every search step batch; the arrays only change in the Inspector.
4. **`MaxVisitedCells = 10000`** caps a search at ~100×100 cells of frontier; any route longer than that on a huge map fails with `too_far` (FEAT-01 addresses the algorithm; this plan addresses invalidation).

## Design

- **Chunk-scoped invalidation.** Each `PathSearch` records the set of chunks its frontier has *touched* (it already pins them via `TerrainDemand`; the set exists). `GridCellDataComponent` exposes per-chunk navigation revisions (`Revisions` partial has per-chunk terrain revisions; add the navigation counterpart or reuse with the `Navigation` kind from ENH-01). On `CellsChanged(kind, chunks)`, a search restarts only if `kind` has `Navigation` **and** `chunks ∩ touched ≠ ∅`; `Residency` changes never restart it (the pinned chunks cannot be evicted while the search holds them).
- **Pin tokens.** `GridCellDataComponent.Pins` accepts a `GridPinToken` (a struct id from a counter, or the `GridChunkPins` instance from DUP-09) as owner; release is explicit (`Dispose`) with the owning *component* node's `TreeExiting` as the safety net for all its tokens. `TerrainDemand` becomes a `GridChunkPins` field on the request record — no Node.
- **Costs cached** on the component, rebuilt when the exported arrays change (`_GetPropertyList`/setter) — the `Configuration` record already carries a hash of the arrays for restart detection; reuse it as the cache key.

## Guards (fail first)

- Probe: start a long search (A far from B, 200×200 map), then evict/reload a chunk **outside** the search's touched set → assert the search does not restart (`Search.Restarts == 0`, expose the counter). **Mutation:** keep the global revision check → restarts.
- Probe: change a cell **inside** the touched set to blocked → assert exactly one restart and the final path avoids it (the guard must still catch real changes).
- Probe: 100 simultaneous `RequestPath` calls add zero child nodes to the navigation component. Mutation: restore the lease Node → `GetChildCount()` assertion fails.
- Determinism: paths for the fixture seeds identical before/after (record first).

## Dependencies / collisions

Depends on ENH-01 (kinds) and DUP-09 (pin helper/tokens). **Collision** with the other session's `ecs/grid` streaming work — `GridCellDataComponent.Pins` is theirs to coordinate.

## Out of scope

Hierarchical pathfinding / flow fields (FEAT-01), road cost rules, the A* itself.
