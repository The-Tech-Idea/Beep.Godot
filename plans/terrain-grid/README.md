# Terrain + Grid review — plan set (2026-09-08)

A complete, file-by-file read of the terrain engine and the grid system on 2026-09-08:
`addons/beep_game_builder_cs/ecs/terrain/` (85 files), `ecs/grid/` (112 files) and `ecs/grid/ui/`
(16 files), read as source — not from the existing plan documents. Every finding below was verified
against the code; file:line references are to the tree as of that date (uncommitted huge-world
streaming work from the same session included).

Three questions were asked of every file: **is this a second implementation of something that
exists** (duplication), **what would make it faster, safer or more correct** (enhancement), and
**what does a game of this genre need that the engine cannot yet say** (feature). One plan document
per answer. Each document carries: the finding with evidence, why it matters, the design, steps,
guards that must fail before the fix (mutation-tested), effort, dependencies, and what is out of
scope.

**Progress.** Items are marked in the tables below as they land; each one's own document gains an
"Outcome" section recording what actually differed from the plan and why. Implementation began
2026-09-08 with the terrain-only, no-collision group.

Standing rules these plans follow: one owner per fact; no stubs; every addition arrives with its
consumer; nothing is deleted on "nothing reads it" grounds without the owner's call (DUP-11, the
calendar helpers in DUP-06 and `TerrainShorelineField` in DUP-04 are flagged as the owner's
decisions); per-genre classes are not merged; guards must be able to fail.

## Duplication (13)

| Id | Plan | Copies found | Effort | Status |
|---|---|---|---|---|
| DUP-01 | [Terrain renderer lifecycle contract](DUP-01-terrain-renderer-lifecycle-contract.md) | `QueueRebuild` ×9, `DisconnectCells` ×7, `ResolveCells` ×4, `Draw()` hand-wires 9 renderers | M | |
| DUP-02 | [Shared water material](DUP-02-shared-water-material.md) | 3 water builders, `SetTexture` ×2 (10 sites); tile view lacks foam/swell | S | **done** |
| DUP-03 | [Feature sheet loading](DUP-03-feature-sheet-loading.md) | flat vs iso loaders; iso ignores per-sheet columns/rows (bug) | S | **done** |
| DUP-04 | [Per-cell hash and generation helpers](DUP-04-per-cell-hash-and-generation-helpers.md) | 3 surviving hashes, `Negate` ×2, percentile ×2, "most common" ×5, 4-neighbour loop ×8, lab coast field | S | **done** (loops → ENH-16) |
| DUP-05 | [One id normaliser](DUP-05-one-id-normaliser.md) | 18 normaliser/sanitiser copies, ≥4 rules; wallet vs catalog disagree | S | |
| DUP-06 | [Dictionary reader wrappers](DUP-06-dictionary-reader-wrappers.md) | 13 `Dict*` wrappers; calendar dead numeric guards | XS | |
| DUP-07 | [Prop residency façade](DUP-07-prop-residency-facade.md) | 3 `*.Streaming.cs` partials | S | |
| DUP-08 | [Grid geometry helpers](DUP-08-grid-geometry-helpers.md) | footprint ×4, `ClampZ` ×2, `ResolveCurrent<T>` ×2 | XS | |
| DUP-09 | [Chunk-pin helper](DUP-09-chunk-pin-helper.md) | `>> 5` ×52 in 12 files, pin-refresh pattern ×7, a Node per path request | M | |
| DUP-10 | [Arrival detection](DUP-10-arrival-detection.md) | `_wasMoving` edge detection ×2 with a one-frame hole | S | |
| DUP-11 | [Dispatch board showcase](DUP-11-dispatch-board-showcase.md) | a second dispatch loop in seconds (owner's call: relocate) | S | |
| DUP-12 | [HUD panel base](DUP-12-hud-panel-base.md) | 6/16 panels bypass the base; two ~300-line twin button bars; 3 button-binding copies; 2 roster caches | M | |
| DUP-13 | [Terrain-kind registry](DUP-13-terrain-kind-registry.md) | what a kind means is spelled out in 11+ tables (the `lava` incident) | L | |

## Enhancements (16)

| Id | Plan | Headline | Effort | Status |
|---|---|---|---|---|
| ENH-01 | [Eviction-aware change notifications](ENH-01-eviction-aware-change-notifications.md) | `CellsChanged` carries kind + chunks; eviction stops bumping global revisions; 10 eviction-blind listeners | M–L | |
| ENH-02 | [Edit-kind classification](ENH-02-edit-kind-classification.md) | farming edits stop rebuilding terrain; no-op writes; `AdvanceDay` off the full scan | S–M | |
| ENH-03 | [Chunk-scoped navigation invalidation](ENH-03-chunk-scoped-navigation-invalidation.md) | searches restart only for touched chunks; pin tokens replace lease Nodes; cached costs | M | |
| ENH-04 | [Archive scheduler](ENH-04-archive-scheduler.md) | O(1) evictability, unthrottled demand loads, chunk-scoped reload abort, load ∥ save | M | |
| ENH-05 | [Painted memory and uploads](ENH-05-painted-memory-and-uploads.md) | chunked live snapshot (48 MB → <12 MB at 1M cells), windowed texture upload, no hot-path verification | M | |
| ENH-06 | [Prop residency update](ENH-06-prop-residency-update.md) | allocation-free merge, chunk invalidation, stamp retention, overview LOD | M | |
| ENH-07 | [Tile and isometric streaming](ENH-07-tile-and-isometric-streaming.md) | chunk-group TileMapLayer pool with patterns; overview hand-off | L | |
| ENH-08 | [Projection hot paths](ENH-08-projection-hot-paths.md) | span `CellCorners`, cached surface, one mouse→cell per frame | S | |
| ENH-09 | [Overlay culling and bridge marshalling](ENH-09-overlay-culling-and-bridge-marshalling.md) | overlay draws the view; bridge stops marshalling the map | S | |
| ENH-10 | [Streamed-world save](ENH-10-streamed-world-save.md) | `CaptureState` throws today on any streamed world; manifest + slot-isolated archive | M | |
| ENH-11 | [Autotile configuration per frame](ENH-11-autotile-configuration-per-frame.md) | `Json.Stringify` per frame while painting | XS | **done** |
| ENH-12 | [Job queue indices](ENH-12-job-queue-indices.md) | bucketed states, priority-heap claim, one notification, typed enumeration | M | |
| ENH-13 | [Minimap and scatter limits](ENH-13-minimap-and-scatter-limits.md) | silent 1024 caps removed; chunk bake; chunk-resident scatter | S–M | |
| ENH-14 | [Object-at-cell index](ENH-14-object-at-cell-index.md) | inspector/tool group scans → O(1); status panel late-resolve wiring bug | S | |
| ENH-15 | [Unit and contract drift](ENH-15-unit-and-contract-drift.md) | `TransportRate` doc says seconds; `GatherSeconds` read as turns; catalog index | XS–S | |
| ENH-16 | [Generation stage allocations](ENH-16-generation-stage-allocations.md) | iterator BFS ×4, field clone per pass, per-region lists, shared distance fields | M | **done** (allocation pass; cross-order loops follow) |

## Features (8)

| Id | Plan | Genre precedent | Effort |
|---|---|---|---|
| FEAT-01 | [Hierarchical pathfinding and flow fields](FEAT-01-hierarchical-pathfinding.md) | HPA\* sectors/portals (Factorio, AoE), flow fields (SupCom 2) — `MaxVisitedCells=10000` fails long routes | L |
| FEAT-02 | [Territory layer](FEAT-02-territory-layer.md) | Civ borders, Settlers claims — no owner on any cell today | M–L |
| FEAT-03 | [Fog of war and exploration](FEAT-03-fog-of-war-and-exploration.md) | Civ/AoE fog — only underground prospecting is hidden today | L |
| FEAT-04 | [Bridges and fords](FEAT-04-bridges-and-fords.md) | OpenTTD/Anno bridges — rivers split every settlement with no crossing | M |
| FEAT-05 | [Terraforming and ramps](FEAT-05-terraforming-and-ramps.md) | `terrain_ramp_direction` has a reader and **no writer**; no raise/lower/flatten | M |
| FEAT-06 | [Huge worlds through the recipe](FEAT-06-huge-worlds-through-the-recipe.md) | named sizes stop at 128×80; dense generation buffer; out-of-core field | L |
| FEAT-07 | [Seasonal terrain](FEAT-07-seasonal-terrain.md) | Anno/Banished winter — calendar has seasons the world cannot see | M |
| FEAT-08 | [World edit history](FEAT-08-world-edit-history.md) | undo/redo for tools, roads, placement, terraform | M |

## Suggested order

1. **Foundations, terrain-only, no collision:** DUP-02, DUP-03, DUP-04, ENH-11, ENH-16.
2. **The contract everything else lands on:** DUP-01 → ENH-01 → ENH-02 (coordinate `GridCellDataComponent` emitters with the streaming session).
3. **Grid plumbing (coordinate with the `ecs/grid` session):** DUP-05, DUP-06, DUP-08, DUP-09, DUP-10, DUP-12, ENH-08, ENH-14, ENH-15.
4. **Huge-world completion:** ENH-03, ENH-04, ENH-05, ENH-06, DUP-07, ENH-09, ENH-13, ENH-10, ENH-07, FEAT-06.
5. **Registry and features:** DUP-13, ENH-12, FEAT-05, FEAT-04, FEAT-02, FEAT-03, FEAT-08, FEAT-07, FEAT-01.

## Collision notes

Two other sessions were active while this review was written: one in `ecs/grid/` (streaming, archive, pins) and one in `TerrainWorldComponent`/`TerrainRecipe`/level loading. Every plan names its collision surface; nothing in this set should be landed in those files without coordinating first.

## Tracker

The master tracker is `docs/ENGINE_ENHANCEMENT_PLAN.md` ("Terrain and grid review (2026-09-08)"). Per-subsystem history: `docs/terrain-engine/ENHANCEMENT_AND_FIX_PLAN.md` (closed), `docs/grid-system/ENHANCEMENT_AND_FIX_PLAN.md` (closed). When a plan lands, update its status here and in the tracker, and move its evidence into the component pages under `docs/terrain-engine/` and `docs/grid-system/`.
