# FEAT-01 — Hierarchical pathfinding and flow fields for huge maps

**Type:** feature (genre standard: RTS/city-builder/4X on large maps) · **Area:** `GridNavigationComponent` (+`.Search`, `.Requests`), `GridPathFollowerComponent`, `GridActorTravelComponent`, `GridCellDataComponent` chunks · **Status:** proposed 2026-09-08 · **Effort:** L (1–2 weeks) · **Risk:** high (pathing is load-bearing for every worker loop; must ship behind the existing request API)

## Gap

`GridNavigationComponent` is a single-level, main-thread, time-sliced A* over cells with `MaxVisitedCells = 10000`. On a 1024² streamed world:

- A route longer than ~100 cells of open frontier exhausts the cap and fails (`too_far`); a hauler cannot cross a continent.
- Every worker re-plans from scratch; 200 workers converging on one depot run 200 independent searches over the same corridor.
- The search touches chunks by pinning them (`TerrainDemand`) — a long search pins a long ribbon of chunks, working against the archive.

The tracker records "Replacing grid A\* with Godot navigation — rejected; cell-exact paths, road costs, and per-cell blocking are the point". This plan keeps cell-exact A* for the last mile and adds the two structures every large-map genre game uses on top of it:

- **HPA\*** (hierarchical pathfinding A\*; Botea et al.; used in the form of "sectors and portals" by Factorio, Dwarf Fortress' zones, Age of Empires' tile-region graph): the map's 32-cell chunks are the sectors; a **portal** is a maximal run of passable cells along a shared chunk edge; an abstract graph links portals within a chunk by intra-chunk cost. A long route is an A\* over portals (a few hundred nodes on a 1024² map), then cell-exact A\* inside each chunk on the corridor.
- **Flow fields** (Supreme Commander 2, Planetary Annihilation; the standard for many units to one goal): per goal cell, an integration field over the reachable chunks and a direction per cell; N units share one field. Fits `GridHaulerComponent`/`GridWorkerComponent` converging on depots and build sites.

## Design

1. **Chunk portal graph** maintained incrementally by `GridCellDataComponent`'s `Navigation` changes (ENH-01): a `Navigation` change in chunk C rebuilds C's portals and its intra-chunk portal costs (a 32×32 A\* per portal pair — bounded); neighbours' portals on the shared edge are re-derived. Evicted chunks keep their portal data (portals are computed from cells that exist in the archive — cache them in the chunk's metadata on eviction so an archived chunk is still routable **without loading it**; this is what lets a route cross a continent of archived chunks).
2. **Two-level search:** `RequestPath(from, to)` first checks the existing single-level A\* for short routes (same chunk or adjacent — `MaxVisitedCells` kept), else runs abstract A\* on portals, then refines chunk by chunk *lazily* as the follower advances (`GridPathFollowerComponent` requests the next refined segment when it reaches a portal; the rest of the corridor stays abstract). Refinement of the current chunk needs that chunk resident — it is the follower's chunk, already pinned.
3. **Flow fields:** `RequestFlowField(goal, kinds)` builds an integration field over the goal's chunk and expands outward on demand to chunks that request it (a unit in chunk C asks for C's slice); shared by all units with that goal; invalidated per chunk by `Navigation` changes. `GridHaulerComponent` uses it for depot returns when ≥ 3 haulers share a depot (`GridTransportManagerComponent` knows the count).
4. **Off-thread abstract search:** the portal graph is immutable-per-revision (copy-on-write per chunk), so abstract A\* and field integration run on the worker pool with cancellation on revision change; cell-exact refinement stays main-thread time-sliced as today. Results arrive through the existing `PathReady` signal — **no API change** for workers.
5. **Terrain costs:** portal costs use the same `Costs` table (roads cheaper, shallow water 2.5×) so a road across a continent is preferred at the abstract level too.

## Guards (fail first)

- Probe: 1024×1024 generated world (streamed), worker at (10,10), job at (1000,1000), only 20 chunks resident → path found, worker arrives; today: `too_far`. **Mutation:** disable the portal level → `too_far`.
- Probe: portal graph incremental update — block a corridor cell in chunk C → only C's portals rebuild (count via hook); the abstract route avoids it.
- Probe: 100 haulers to one depot share one flow field (`ActiveFlowFields == 1`); total planning time < one worker's single-level A\*.
- Determinism: for the small-map fixtures, paths identical to today (short routes keep the single-level search).
- Equivalence: on a 128×80 map, hierarchical path length ≤ 1.1 × optimal A\* length for 100 random pairs (HPA\* suboptimality bound documented).

## Dependencies / collisions

Depends on ENH-01 (Navigation-kind change payload), ENH-03 (chunk-scoped invalidation, pin tokens), DUP-09. **Heavy `ecs/grid/` collision** — schedule after the other session's streaming work. Read `docs/huge-world-research.md` §navigation before implementing (the research file already surveys HPA\*/flow fields).

## Out of scope

Unit collision avoidance / local steering (actors domain), Godot `NavigationServer` (rejected), 3D.
