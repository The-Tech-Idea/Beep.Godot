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
| DUP-01 | [Terrain renderer lifecycle contract](DUP-01-terrain-renderer-lifecycle-contract.md) | `QueueRebuild` ×9, `DisconnectCells` ×7, `ResolveCells` ×4, `Draw()` hand-wires 9 renderers | M | **partial** (rebuild coalescer; resolution + Draw deferred) |
| DUP-02 | [Shared water material](DUP-02-shared-water-material.md) | 3 water builders, `SetTexture` ×2 (10 sites); tile view lacks foam/swell | S | **done** |
| DUP-03 | [Feature sheet loading](DUP-03-feature-sheet-loading.md) | flat vs iso loaders; iso ignores per-sheet columns/rows (bug) | S | **done** |
| DUP-04 | [Per-cell hash and generation helpers](DUP-04-per-cell-hash-and-generation-helpers.md) | 3 surviving hashes, `Negate` ×2, percentile ×2, "most common" ×5, 4-neighbour loop ×8, lab coast field | S | **done** |
| DUP-05 | [One id normaliser](DUP-05-one-id-normaliser.md) | 18 normaliser/sanitiser copies, ≥4 rules; wallet vs catalog disagree | S | **done** (id normaliser + GridIds.NodeName node-name sanitiser) |
| DUP-06 | [Dictionary reader wrappers](DUP-06-dictionary-reader-wrappers.md) | 13 `Dict*` wrappers; calendar dead numeric guards | XS | **done** (Dict* wrappers + GridMath guards + calendar dead helpers removed) |
| DUP-07 | [Prop residency façade](DUP-07-prop-residency-facade.md) | 3 `*.Streaming.cs` partials | S | |
| DUP-08 | [Grid geometry helpers](DUP-08-grid-geometry-helpers.md) | footprint ×4, `ClampZ` ×2, `ResolveCurrent<T>` ×2 | XS | **done** (footprint + ClampZ + ResolveCurrent via EntityComponent.ResolveLive) |
| DUP-09 | [Chunk-pin helper](DUP-09-chunk-pin-helper.md) | `>> 5` ×52 in 12 files, pin-refresh pattern ×7, a Node per path request | M | **done** (one chunk rule + `GridChunkPins`; 4 of 7 owners ported, see Outcome) |
| DUP-10 | [Arrival detection](DUP-10-arrival-detection.md) | `_wasMoving` edge detection ×2 with a one-frame hole | S | **investigated, not done** (premise does not hold; would regress stepped movers) |
| DUP-11 | [Dispatch board showcase](DUP-11-dispatch-board-showcase.md) | a second dispatch loop in seconds (owner's call: relocate) | S | |
| DUP-12 | [HUD panel base](DUP-12-hud-panel-base.md) | 6/16 panels bypass the base; two ~300-line twin button bars; 3 button-binding copies; 2 roster caches | M | **done** (panels+bindings+enum-parser+GridToggleBarComponent merge+roster-cache) |
| DUP-13 | [Terrain-kind registry](DUP-13-terrain-kind-registry.md) | what a kind means is spelled out in 11+ tables (the `lava` incident) | L | **classification fold done; close-out remains** (all 10 per-kind classification tables read the catalog: Startable, coherence flags, scale flags, Rainfall, Level, Class, BlockedByDefault, PropPalette, MaterialSlot, FeatureEligibility. Non-classification tables decided to stay with triage: IsoFrame, biome-preset stage, ResourceCatalogs terrain lists, TerrainMapArt/MaterialTiling. Left: the no-literals pin, and the game-assignable catalog through settings/recipe — coordinate with the terrain session) |

## Enhancements (16)

| Id | Plan | Headline | Effort | Status |
|---|---|---|---|---|
| ENH-01 | [Eviction-aware change notifications](ENH-01-eviction-aware-change-notifications.md) | `CellsChanged` carries kind + chunks; eviction stops bumping global revisions; 10 eviction-blind listeners | M–L | **done** (eviction storm removed; per-chunk minimisation follow-up) |
| ENH-02 | [Edit-kind classification](ENH-02-edit-kind-classification.md) | farming edits stop rebuilding terrain; no-op writes; `AdvanceDay` off the full scan | S–M | **done** (per-cell kind, no-op early-outs, crop-tick index) |
| ENH-03 | [Chunk-scoped navigation invalidation](ENH-03-chunk-scoped-navigation-invalidation.md) | searches restart only for touched chunks; pin tokens replace lease Nodes; cached costs | M | |
| ENH-04 | [Archive scheduler](ENH-04-archive-scheduler.md) | O(1) evictability, unthrottled demand loads, chunk-scoped reload abort, load ∥ save | M | |
| ENH-05 | [Painted memory and uploads](ENH-05-painted-memory-and-uploads.md) | chunked live snapshot (48 MB → <12 MB at 1M cells), windowed texture upload, no hot-path verification | M | |
| ENH-06 | [Prop residency update](ENH-06-prop-residency-update.md) | allocation-free merge, chunk invalidation, stamp retention, overview LOD | M | |
| ENH-07 | [Tile and isometric streaming](ENH-07-tile-and-isometric-streaming.md) | chunk-group TileMapLayer pool with patterns; overview hand-off | L | |
| ENH-08 | [Projection hot paths](ENH-08-projection-hot-paths.md) | span `CellCorners`, cached surface, one mouse→cell per frame | S | **done** (span CellCorners + caller migration + surface cache + one-conversion hover owner) |
| ENH-09 | [Overlay culling and bridge marshalling](ENH-09-overlay-culling-and-bridge-marshalling.md) | overlay draws the view; bridge stops marshalling the map | S | **done** (overlay culls to camera window; bridge paints from EnumerateFlags) |
| ENH-10 | [Streamed-world save](ENH-10-streamed-world-save.md) | `CaptureState` throws today on any streamed world; manifest + slot-isolated archive | M | |
| ENH-11 | [Autotile configuration per frame](ENH-11-autotile-configuration-per-frame.md) | `Json.Stringify` per frame while painting | XS | **done** |
| ENH-12 | [Job queue indices](ENH-12-job-queue-indices.md) | bucketed states, priority-heap claim, one notification, typed enumeration | M | **done** (one-notification + cached counts + typed HUD enum + spatial nearest-job claim index, fairness baseline preserved) |
| ENH-13 | [Minimap and scatter limits](ENH-13-minimap-and-scatter-limits.md) | silent 1024 caps removed; chunk bake; chunk-resident scatter | S–M | **partial** (minimap downsample done; resource scatter deferred — streaming collision) |
| ENH-14 | [Object-at-cell index](ENH-14-object-at-cell-index.md) | inspector/tool group scans → O(1); status panel late-resolve wiring bug | S | **partial** (status late-source fix done; index declined as specified - wrong premises) |
| ENH-15 | [Unit and contract drift](ENH-15-unit-and-contract-drift.md) | `TransportRate` doc says seconds; `GatherSeconds` read as turns; catalog index | XS–S | **done** (unit renames + Find index; ForTerrain index has no caller) |
| ENH-16 | [Generation stage allocations](ENH-16-generation-stage-allocations.md) | iterator BFS ×4, field clone per pass, per-region lists, shared distance fields | M | **done** |

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

## Terrain, grid and resource review (2026-09-11) — follow-up

**Progress:** the two high-severity items — **DUP-14** and **FIX-07** — are **implemented** (each with a mutation-proven guard); 15 remain, tracked in the tables below.

A second pass grounded in the **current** code (after the 2026-09-08 items that have landed — DUP-13's terrain-kind catalog fold, ENH-08/09/12, DUP-12, …), adding the **resource system** (generation-side placement plus the wallet/storage/production/hauler economy) as a first-class subject. Run as a multi-agent review — nine reviewer lanes across terrain/grid/resource surfaced 23 candidates; an adversarial verify pass confirmed 17 as real-and-novel (six refuted as misread or already covered). Each item has verified file:line evidence and mutation-tested guards, and excludes the 2026-09-08 findings unless a landed fix had regressed.

Bug-heavy, so it adds a **Fixes (FIX-NN)** category. Two themes: **load/restore correctness** (FIX-05/06/07/09/13, DUP-14 — state lost or hidden across save/load) and **resource-id normalisation drift** (DUP-14, extending DUP-05). Highest severity: DUP-14 (wallet debits lost under an un-normalised key → infinite resources + save corruption) and FIX-07 (in-progress world jobs parked Idle on load).

### Duplication (3)

| Id | Plan | Headline | Effort | Status |
|---|---|---|---|---|
| DUP-14 | [Resource-id normalization drift: one canonical key across wallet, storage and cost totals (fixes the wallet phantom-key debit loss)](DUP-14-resource-id-normalisation.md) | Route TryTotals, the wallet, storage and the hauler/extractor cargo ports through one GridIds.Normalize key so spaced/dashed resource ids stop losing wallet debits and desyncing stores | M | **Implemented 2026-09-11** |
| DUP-15 | [Live-water sub-cell reconstruction is implemented twice, once per streaming mode](DUP-15-live-water-reconstruction.md) | Extract the twice-copied half-cell water reconstruction into one ReconstructWater helper taking wet/patch delegates, called by both the snapshot sampler and the live streaming query | S | Proposed |
| DUP-16 | [Transport and extraction managers duplicate the pruning duck-typed Node registry](DUP-16-duck-node-registry.md) | Extract a shared DuckTypedNodeRegistry base so transport and extraction managers stop hand-rolling the same list/register/unregister/count/prune, keeping only the per-manager contract check as an override hook | M | Proposed |

### Enhancement (1)

| Id | Plan | Headline | Effort | Status |
|---|---|---|---|---|
| ENH-17 | [Coherence smoothing re-derives every sample rainfall index by linear string scan each pass](ENH-17-coherence-rainfall-index.md) | Replace the per-sample O(kinds) rainfall-index string scan with a once-built ordinal Dictionary<string,byte> so coherence passes stop re-scanning up to 1.25M samples ×6 | S | Proposed |

### Fixes (13)

| Id | Plan | Headline | Effort | Status |
|---|---|---|---|---|
| FIX-01 | [Hillslope diffusion coefficient exceeds its documented stability limit at high ErosionStrength](FIX-01-erosion-diffusion-stability.md) | Clamp the hillslope diffusion coefficient (0.35×dial) to ≤1 so ErosionStrength above ~2.86 stops amplifying checkerboard speckle before relief classification | XS | Proposed |
| FIX-02 | [Shape/ShapeWarpX/ShapeWarpY/Detail noise channels are built every generation but read by nothing](FIX-02-dead-noise-channels.md) | Correct the false "Shape decides where land is" doc and, on Fahad's call, drop the four never-read noise channels built every generation run | XS | Proposed |
| FIX-03 | [TerrainTextures.Load throws NRE on a failed external-file load instead of returning null](FIX-03-terraintextures-load-nre.md) | Add an `image is null` guard to the external-file branch so a failed absolute-path load warns and returns null instead of throwing | XS | Proposed |
| FIX-04 | [Mountain prefab sprites set Owner before AddChild, so editor-authored parts vanish on reload](FIX-04-mountain-prefab-owner-order.md) | Route mountain-prefab sprite parenting through TerrainAuthoring.Adopt (AddChild then adopt) so editor-saved art survives reload | XS | Proposed |
| FIX-05 | [GetOrCreate bumps the global TerrainRevision on lazy cell creation, forcing a full-map terrain rebuild on gameplay first-touch](FIX-05-getorcreate-spurious-terrain-revision.md) | Drop the TerrainRevision++ from GetOrCreate so a gameplay first-touch of virgin ground stops forcing a full painted+isometric map rebuild | XS | Proposed |
| FIX-06 | [Archive auto-load aborts on any CellsChanged, including a Residency-only move of an unrelated chunk (violates the ENH-01 contract)](FIX-06-archive-load-abort-residency.md) | Make the archive bulk load-abort listener read the CellsChanged kind+chunks payload so a Residency-only or unrelated-chunk edit no longer kills an in-flight demand load | S | Proposed |
| FIX-07 | [RequeueClaimedJobsOnLoad default silently drops in-progress world-execution/dispatch work on load](FIX-07-requeue-drops-world-execution.md) | Make world-execution and dispatch restore re-claim a requeued job under the saved worker id (mirroring the actor path) so an in-progress world-owned job survives save/load instead of parking the worker Idle | M | **Implemented 2026-09-11** |
| FIX-08 | [CompleteJob on a Queued job orphans its id in the queued spatial index](FIX-08-completejob-queued-index-leak.md) | Add the IndexRemoveQueued guard to CompleteJob so a public force-complete of a Queued job stops orphaning its id/chunk in _queuedIndex | XS | Proposed |
| FIX-09 | [GridObjectiveTrackerComponent.RestoreState mutates state silently, leaving signal-driven HUDs stale after load](FIX-09-objective-restore-signals.md) | Re-emit ObjectiveProgressChanged/Activated/Completed per objective at the end of RestoreState so signal-driven HUDs repaint on load | S | Proposed |
| FIX-10 | [Objective panel Goals-N summary reports the capped visible-row count, not the true active-goal count](FIX-10-objective-panel-goal-count.md) | Uncap the panel's goal-total count by dropping VisibleObjectives()'s MaxVisibleObjectives break; rows stay capped by UpdateRows so the summary reports true totals | S | Proposed |
| FIX-11 | [GridCalendarHudComponent wires calendar signals only in _Ready and never reconnects on re-resolve](FIX-11-calendar-hud-signal-reconnect.md) | Move calendar signal wiring into ResolveReferences so the HUD reconnects DayAdvanced/SeasonChanged/YearChanged when the calendar node is swapped instead of going stale after a world reload | S | Proposed |
| FIX-12 | [GridPorts.Transfer silently discards the un-accepted remainder for an unload-only giver (breaks its never-lost contract)](FIX-12-ports-transfer-remainder.md) | Cap GridPorts.Transfer to the receiver's free space and report any unrecoverable remainder so an unload-only giver never silently loses cargo | XS | Proposed |
| FIX-13 | [A rejected actor-travel restore loads nothing and reports nothing](FIX-13-actor-travel-restore-report.md) | Have Load read RestoreState's result and report a rejected restore instead of silently dropping saved travellers | XS | Proposed |

Suggested order: the two high-severity data-integrity items **DUP-14 and FIX-07 are DONE** (each with a mutation-proven guard). Next: the rest of the load/restore family (FIX-05/06/09/11/13), then the standalone terrain fixes (FIX-01/03/04) and the XS grid fixes (FIX-08/10/12), with FIX-02 (owner's-call removal), ENH-17 and the two refactors (DUP-15, DUP-16) as they fit. Same standing rules and collision notes as the 2026-09-08 set apply; nothing here touches the streaming/`TerrainWorldComponent` files the concurrent session owns.

## Tracker

The master tracker is `docs/ENGINE_ENHANCEMENT_PLAN.md` ("Terrain and grid review (2026-09-08)"). Per-subsystem history: `docs/terrain-engine/ENHANCEMENT_AND_FIX_PLAN.md` (closed), `docs/grid-system/ENHANCEMENT_AND_FIX_PLAN.md` (closed). When a plan lands, update its status here and in the tracker, and move its evidence into the component pages under `docs/terrain-engine/` and `docs/grid-system/`.
