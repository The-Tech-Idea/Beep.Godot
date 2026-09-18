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

**Progress:** **all 17 items are implemented**, each with a mutation-proven guard. The two high-severity items (**DUP-14**, **FIX-07**); the load/restore family (**FIX-05**, **FIX-06**, **FIX-09**, **FIX-11**, **FIX-13**); the standalone terrain fixes (**FIX-01**, **FIX-02**, **FIX-03**, **FIX-04**); the XS grid fixes (**FIX-08**, **FIX-10**, **FIX-12**); **ENH-17**, **DUP-15** and **DUP-16**. Nothing in this set remains.

A second pass grounded in the **current** code (after the 2026-09-08 items that have landed — DUP-13's terrain-kind catalog fold, ENH-08/09/12, DUP-12, …), adding the **resource system** (generation-side placement plus the wallet/storage/production/hauler economy) as a first-class subject. Run as a multi-agent review — nine reviewer lanes across terrain/grid/resource surfaced 23 candidates; an adversarial verify pass confirmed 17 as real-and-novel (six refuted as misread or already covered). Each item has verified file:line evidence and mutation-tested guards, and excludes the 2026-09-08 findings unless a landed fix had regressed.

Bug-heavy, so it adds a **Fixes (FIX-NN)** category. Two themes: **load/restore correctness** (FIX-05/06/07/09/13, DUP-14 — state lost or hidden across save/load) and **resource-id normalisation drift** (DUP-14, extending DUP-05). Highest severity: DUP-14 (wallet debits lost under an un-normalised key → infinite resources + save corruption) and FIX-07 (in-progress world jobs parked Idle on load).

### Duplication (3)

| Id | Plan | Headline | Effort | Status |
|---|---|---|---|---|
| DUP-14 | [Resource-id normalization drift: one canonical key across wallet, storage and cost totals (fixes the wallet phantom-key debit loss)](DUP-14-resource-id-normalisation.md) | Route TryTotals, the wallet, storage and the hauler/extractor cargo ports through one GridIds.Normalize key so spaced/dashed resource ids stop losing wallet debits and desyncing stores | M | **Implemented 2026-09-11** |
| DUP-15 | [Live-water sub-cell reconstruction is implemented twice, once per streaming mode](DUP-15-live-water-reconstruction.md) | Extract the twice-copied half-cell water reconstruction into one ReconstructWater helper taking wet/patch delegates, called by both the snapshot sampler and the live streaming query | S | Implemented 2026-09-11 |
| DUP-16 | [Transport and extraction managers duplicate the pruning duck-typed Node registry](DUP-16-duck-node-registry.md) | Extract a shared DuckTypedNodeRegistry base so transport and extraction managers stop hand-rolling the same list/register/unregister/count/prune, keeping only the per-manager contract check as an override hook | M | Implemented 2026-09-11 |

### Enhancement (1)

| Id | Plan | Headline | Effort | Status |
|---|---|---|---|---|
| ENH-17 | [Coherence smoothing re-derives every sample rainfall index by linear string scan each pass](ENH-17-coherence-rainfall-index.md) | Replace the per-sample O(kinds) rainfall-index string scan with a once-built ordinal Dictionary<string,byte> so coherence passes stop re-scanning up to 1.25M samples ×6 | S | Implemented 2026-09-11 |

### Fixes (13)

| Id | Plan | Headline | Effort | Status |
|---|---|---|---|---|
| FIX-01 | [Hillslope diffusion coefficient exceeds its documented stability limit at high ErosionStrength](FIX-01-erosion-diffusion-stability.md) | Clamp the hillslope diffusion coefficient (0.35×dial) to ≤1 so ErosionStrength above ~2.86 stops amplifying checkerboard speckle before relief classification | XS | Implemented 2026-09-11 |
| FIX-02 | [Shape/ShapeWarpX/ShapeWarpY/Detail noise channels are built every generation but read by nothing](FIX-02-dead-noise-channels.md) | Correct the false "Shape decides where land is" doc and, on Fahad's call, drop the four never-read noise channels built every generation run | XS | Implemented 2026-09-11 — option (A), removed |
| FIX-03 | [TerrainTextures.Load throws NRE on a failed external-file load instead of returning null](FIX-03-terraintextures-load-nre.md) | Add an `image is null` guard to the external-file branch so a failed absolute-path load warns and returns null instead of throwing | XS | Implemented 2026-09-11 |
| FIX-04 | [Mountain prefab sprites set Owner before AddChild, so editor-authored parts vanish on reload](FIX-04-mountain-prefab-owner-order.md) | Route mountain-prefab sprite parenting through TerrainAuthoring.Adopt (AddChild then adopt) so editor-saved art survives reload | XS | Implemented 2026-09-11 |
| FIX-05 | [GetOrCreate bumps the global TerrainRevision on lazy cell creation, forcing a full-map terrain rebuild on gameplay first-touch](FIX-05-getorcreate-spurious-terrain-revision.md) | Drop the TerrainRevision++ from GetOrCreate so a gameplay first-touch of virgin ground stops forcing a full painted+isometric map rebuild | XS | Implemented 2026-09-11 |
| FIX-06 | [Archive auto-load aborts on any CellsChanged, including a Residency-only move of an unrelated chunk (violates the ENH-01 contract)](FIX-06-archive-load-abort-residency.md) | Make the archive bulk load-abort listener read the CellsChanged kind+chunks payload so a Residency-only or unrelated-chunk edit no longer kills an in-flight demand load | S | Implemented 2026-09-11 |
| FIX-07 | [RequeueClaimedJobsOnLoad default silently drops in-progress world-execution/dispatch work on load](FIX-07-requeue-drops-world-execution.md) | Make world-execution and dispatch restore re-claim a requeued job under the saved worker id (mirroring the actor path) so an in-progress world-owned job survives save/load instead of parking the worker Idle | M | **Implemented 2026-09-11** |
| FIX-08 | [CompleteJob on a Queued job orphans its id in the queued spatial index](FIX-08-completejob-queued-index-leak.md) | Add the IndexRemoveQueued guard to CompleteJob so a public force-complete of a Queued job stops orphaning its id/chunk in _queuedIndex | XS | Implemented 2026-09-11 |
| FIX-09 | [GridObjectiveTrackerComponent.RestoreState mutates state silently, leaving signal-driven HUDs stale after load](FIX-09-objective-restore-signals.md) | Re-emit ObjectiveProgressChanged/Activated/Completed per objective at the end of RestoreState so signal-driven HUDs repaint on load | S | Implemented 2026-09-11 |
| FIX-10 | [Objective panel Goals-N summary reports the capped visible-row count, not the true active-goal count](FIX-10-objective-panel-goal-count.md) | Uncap the panel's goal-total count by dropping VisibleObjectives()'s MaxVisibleObjectives break; rows stay capped by UpdateRows so the summary reports true totals | S | Implemented 2026-09-11 |
| FIX-11 | [GridCalendarHudComponent wires calendar signals only in _Ready and never reconnects on re-resolve](FIX-11-calendar-hud-signal-reconnect.md) | Move calendar signal wiring into ResolveReferences so the HUD reconnects DayAdvanced/SeasonChanged/YearChanged when the calendar node is swapped instead of going stale after a world reload | S | Implemented 2026-09-11 |
| FIX-12 | [GridPorts.Transfer silently discards the un-accepted remainder for an unload-only giver (breaks its never-lost contract)](FIX-12-ports-transfer-remainder.md) | Cap GridPorts.Transfer to the receiver's free space and report any unrecoverable remainder so an unload-only giver never silently loses cargo | XS | Implemented 2026-09-11 |
| FIX-13 | [A rejected actor-travel restore loads nothing and reports nothing](FIX-13-actor-travel-restore-report.md) | Have Load read RestoreState's result and report a rejected restore instead of silently dropping saved travellers | XS | Implemented 2026-09-11 |

Suggested order: **nothing left** — all 17 items in this set are DONE, each with a mutation-proven guard: the two high-severity data-integrity items (**DUP-14**, **FIX-07**), the load/restore family (**FIX-05/06/09/11/13**), the standalone terrain fixes (**FIX-01/02/03/04**), the XS grid fixes (**FIX-08/10/12**), **ENH-17**, **DUP-15** and **DUP-16**. **FIX-02** was resolved as option (A) on the owner's call: the four never-read noise channels were removed. Same standing rules and collision notes as the 2026-09-08 set apply; nothing here touches the streaming/`TerrainWorldComponent` files the concurrent session owns.

## Terrain rendering review and map gameplay features (2026-09-15)

A third pass with two subjects. **Rendering:** every terrain view — Painted, Tiles, Isometric
(block) and IsometricAutotile — and its companion renderers (features, relief props, resource
icons, map overlay, collision, streaming, publication), read as source on 2026-09-15 and compared
capability by capability; the parity matrix, the second owners found (D1–D11) and the
generation→render contract are in [`docs/terrain-engine/VIEW_PARITY_REVIEW.md`](../../docs/terrain-engine/VIEW_PARITY_REVIEW.md).
**Map gameplay:** what RTS/colony games put on a map that this engine cannot yet say — the owner's
example is a distinct area reserved for each player — researched against the games' own code
where it is public (Ensemble's random-map patent, 0 A.D.'s `player.js`, Civ V's
`AssignStartingPlots`, Widelands, OpenRA's map format) and documented in
[`docs/terrain-engine/RTS_COLONY_MAP_RESEARCH.md`](../../docs/terrain-engine/RTS_COLONY_MAP_RESEARCH.md).
Every item below has its own document with file:line evidence, design, mutation-tested guards,
effort and collision notes; the same standing rules as the earlier sets apply. Two items change a
shipped look (VIEW-04's shared water look, VIEW-12's hillshade owner) and land only after the owner
has seen a before/after capture — VIEW-04's was approved on 2026-09-16, the owner having run the
lab and seen all four views draw the shared sea. The block view is a shipped view and keeps its
investment items. FIX-14 (below) changes a shipped look too and skipped that gate, having been
reported from the lab and fixed the same day; the owner did not accept the result.

Headline evidence: IsometricAutotile drew terrain and nothing else — no sea, features, props,
resources or start markers (`TerrainWorldComponent.Drawing.cs:36`; the companions are drawn since
VIEW-01 and the sea since VIEW-04, elevation remains VIEW-09); three per-view sea bindings
with defaults the code itself admits diverge (`TerrainTileRendererComponent.cs:126-134`; one
`TerrainWaterLook` since VIEW-04); two prop
stamp rules, so one map shows different trees per projection; eight owners of cell size and a
collision component that never merges shapes under any view (`TerrainCollisionComponent.cs:242`);
terrain shaders that dropped a tint or fade set on their renderer node, and two unshaded ones
the scene's `CanvasModulate` never reached (corrected on implementation — the review had the sea
staying daylight; it was the tile view's ground, see VIEW-14); river flow computed and thrown away (`TerrainRiverStage.cs:60-66`); start positions that
never reach the cells, are never saved and are read as `starts[0]` everywhere
(`TerrainGeneratorComponent.cs:264-286`, `TerrainWorldComponent.Drawing.cs:286`); no owner, zone
or player anywhere in the grid.

### Rendering (14)

| Id | Plan | Headline | Effort | Status |
|---|---|---|---|---|
| VIEW-01 | [IsometricAutotile draws its companions](VIEW-01-isoautotile-companions.md) | replace the `flat` gate with the grid binding; features, relief, resources and overlay under IA | S | Implemented 2026-09-15 |
| VIEW-02 | [One owner of cell geometry](VIEW-02-cell-geometry-owner.md) | `GridProjectionComponent.EffectiveTileSize` answers from the bound surface; collision merges again | S | Implemented 2026-09-15 |
| VIEW-03 | [One prop stamp core](VIEW-03-one-prop-stamper.md) | `TerrainPropStamper`: one roll, one clump rule, one anchor; two thin views | M | |
| VIEW-04 | [One sea binding and one water look](VIEW-04-one-sea-binding.md) | `TerrainWaterLook` + `TerrainSeaSurface`; IsometricAutotile gets a sea | M | Implemented 2026-09-16 |
| VIEW-05 | [Water depth has one owner](VIEW-05-water-depth-owner.md) | the coast field's distance replaces the block view's private BFS | S | Implemented 2026-09-16 |
| VIEW-06 | [River flow is generated data](VIEW-06-river-flow-field.md) | direction + width on the field and the cells; `flow_map` in the shared water material | M–L | |
| VIEW-07 | [Beach owner and cell-centre contract](VIEW-07-beach-owner-contract.md) | the stage owns width/inland kind; the painter's band agrees at cell centres | S | **Partly implemented 2026-09-16** (the one-owner half-sample correction landed as `TerrainEuclideanDistance.ToTiles`, pinned both directions, generation baseline unchanged; the cell-centre shader contract was built, rejected on sight by the owner and reverted — the band can still disagree with the cell kind at a centre, and that half is open with no guard) |
| VIEW-08 | [One terrain-connect painter](VIEW-08-one-terrain-connect-painter.md) | `TerrainLibraryPainter.Connect`; windowed edits for the autotile bindings path | M | |
| VIEW-09 | [Generator elevation reaches the pack views](VIEW-09-pack-elevation-from-generator.md) | `ProfileFor(relief)`; one elevation fact; IA binds as an elevated surface | M | |
| VIEW-10 | [Stepped publication for tile and block views](VIEW-10-stepped-publication.md) | the publication contract on `TerrainRendererComponent` (closes DUP-01) | M | |
| VIEW-11 | [Block view: windowed live edits](VIEW-11-iso-windowed-edits.md) | diff changed cells + halos instead of a whole rebuild | M | |
| VIEW-12 | [Hillshade has one owner](VIEW-12-hillshade-owner.md) | the painter reads `terrain_shade`; the tile view is lit too — look change, owner approves | S | |
| VIEW-13 | [One resource drawer; parity probe](VIEW-13-resource-drawer-and-parity-probe.md) | overlay = starts + survey; lab wires every companion; `terrain_view_parity_probe.gd` | S | Implemented 2026-09-15 |
| VIEW-14 | [Shaders honour the item and canvas modulate](VIEW-14-shader-canvas-modulate.md) | every terrain shader multiplies by the `COLOR` it receives and none is unshaded; a node tint reaches the painted ground and sea, day/night reaches the tile ground and natural terrain | XS–S | Implemented 2026-09-15 |

### Features (6)

| Id | Plan | Genre precedent | Effort | Status |
|---|---|---|---|---|
| FEAT-09 | [Player start areas](FEAT-09-player-start-areas.md) | AoE player lands + kit, 0 A.D. bases, Civ normalisation, Factorio starting area — a reserved, validated, kitted zone per start; closes E01 | L | Implemented 2026-09-15 |
| FEAT-10 | [Faction catalog and start assignment](FEAT-10-faction-catalog-and-start-assignment.md) | OpenRA `Players` + `Spawn`; the catalog FEAT-02/03 defer, introduced once | M | Implemented 2026-09-16 |
| FEAT-11 | [Zones and district range](FEAT-11-zones-and-district-range.md) | RimWorld zones/home area, Timberborn path-distance districts | M | |
| FEAT-12 | [Spawn markers and playable cordon](FEAT-12-spawn-markers-and-cordon.md) | OpenRA `Bounds` + cordon, `mpspawn`; BGB-13's `Spawns` node | S–M | Implemented 2026-09-16 |
| FEAT-13 | [Symmetric and competitive layouts](FEAT-13-symmetric-layouts.md) | StarCraft II / Warcraft III mirror and rotational maps | L | |
| FEAT-14 | [Start-distance field, richness scaling, neutral sites](FEAT-14-start-distance-and-neutral-sites.md) | Factorio richness by distance; AoE neutral objects between areas | S–M | Implemented 2026-09-16 |

### Raised from the lab (1)

Not part of the 2026-09-15 read. The owner reported it while running the lab and it is filed here
because it lands in the same files.

| Id | Plan | Headline | Effort | Status |
|---|---|---|---|---|
| FIX-14 | [A lake is bounded: a shore on any ground, and a waterline that is a line](FIX-14-lake-shore-and-edges.md) | drop the `TerrainRelief.Flat` gate from all three lake-shore owners; take both water shaders' waterline softness from `open_sea` so a lake ends in a line; `GroundTextureTiles` 6 in the shipped look | S | **Landed 2026-09-16, not accepted** — the owner reports the lakes still look wrong |

### Raised from the Oilfield Days capture (2)

The owner's 2026-09-16 capture of the game sample (`Beep.OilandGas.Sim`, Oilfield Days) showed beach
sand across the land, ground detail the size of vehicles, and a base pad among sand and a lake. The
props and ground-scale half was fixed that day (the owner's cartoon tree, bush and rock sheets; the
Cartoon ground repeat from 4.8 to 1.6 cells; bushes drawn at last). These two carry the rest, in the
owner's chosen order: FIX-15, then FEAT-15 (players always shape the map when start areas are on;
base ground is a start-kit setting defaulting to grass).

| Id | Plan | Headline | Effort | Status |
|---|---|---|---|---|
| FIX-15 | [A temperate world is not a desert](FIX-15-temperate-world-is-not-a-desert.md) | desert grows with map size at a fixed climate (0 % at 32×32, 44–60 % at 144×144, temperate/normal) because coastal moisture reaches a fixed 6.5 cells; the Rainfall axis never reaches moisture; measure the reach in kilometres from the map's span and let Rainfall offset moisture. Landed with the woodland stand scale (forests had come out in bands once the map turned green), woodland cover and the dry belt; Rainfall reaching moisture was built and reversed (owner, 2026-09-17: Rainfall is water and growth, as in Civilization) | S–M | **implemented 2026-09-17, not yet accepted**: 0 % desert at every temperate size, hot lab maps 29–36 % desert through a dry belt at 15–35°; woodland 14/18/22 % by rainfall (Civ VI's caps) in 16-cell forests; guarded (`terrain_climate_share_probe`, twelve mutations; `vegetation.gd` spread check; `biomes.gd` passes again) and baseline re-recorded; lakes at wet rainfall found short, predating FIX-15, open |
| FEAT-15 | [Player lands first](FEAT-15-player-lands-first.md) | AoE2/0 A.D. order: origins after the landmass, a guaranteed flat base core, and every ground-shaping stage keeps out of cores — replaces terrain-first starts whenever start areas are on | L | proposed 2026-09-16, owner's decisions taken, after FIX-15 |

FIX-14 changes generated maps (a lake is banked on any ground), so
`tests/fixtures/terrain_generation_baseline.json` was re-recorded;
`terrain_generation_baseline_probe`, `terrain_beach_footprint_probe` and `terrain_lake_bank_probe`
pass. It also changes a shipped look and did not go through the before/after approval gate VIEW-04
and VIEW-12 carry — it was reported and fixed the same day. A rendered capture of the lab's large
lake does show the intended result, so what is fixed is the case that was measured and something
else is still wrong in front of the owner. Deliberately left open, and listed in the item:
sub-cell ponds (water stored as a patch inside land cells, which nothing banks), blur at map-fit
zoom (mipmapping, not a texture dial), and whether the crisper lake edge is wanted in each art
style. The item also carries a method note for whoever picks it up — two lab captures prove
nothing unless the map is redrawn rather than regenerated and `wave_speed` is set to zero first.

### Raised by FIX-15 (1)

Found while measuring FIX-15's climate, in a stage FIX-15 does not touch.

| Id | Plan | Headline | Effort | Status |
|---|---|---|---|---|
| FIX-16 | [A wet world has more lakes](FIX-16-a-wet-world-has-more-lakes.md) | a wet map delivers 0–2% lake coverage against a normal map's 5%, because `LakeCoverage` is one flood budget that grows ONE lake and `DrainOversizedLakes` then deletes whole lakes once a landmass passes 30% water — rivers, which also rise with Rainfall, are what tip it over | S–M | proposed 2026-09-18 |

### Suggested order

VIEW-14 → VIEW-01 → VIEW-02 → VIEW-13 → **FEAT-09** → FEAT-12 → VIEW-04 → VIEW-05 → VIEW-07 →
FEAT-10 → **FEAT-14** → **FIX-15** → **FEAT-15** (the owner moved these two ahead on 2026-09-16) →
FIX-16 →
VIEW-03 (before DUP-07) → VIEW-08 (before ENH-07) → VIEW-10 (before ENH-07;
closes DUP-01) → VIEW-11 → FEAT-11 → VIEW-06 → VIEW-09 (with FEAT-05 and the library session) →
VIEW-12 → FEAT-13.

### Collision notes

`TerrainWorldComponent.Drawing.cs` / `.Generation.cs` and `GridCellDataComponent` belong to the
streaming session (VIEW-01/02/06/10, FEAT-09/11); `TerrainLibraryEditSession` and
`TerrainLibraryInspector` to the terrain-library session (VIEW-08/09); BGB-13's publisher owns the
emission that FEAT-12's helper serves; FEAT-05 and VIEW-06/09 and FEAT-09 all grow the
generation→cells handoff tuple — coordinate that change once. Deliberately not items: art for
IsometricAutotile and packs (the library), streaming/save/memory/residency (ENH-05/06/07/10,
DUP-07), roads (grid session), the `Projection` save (owner's call, recorded in the review), and
the open water-centre probe cell (21,3).

## Tracker

Owner clarification, 2026-09-11: terrain generation must deliver populated, editable Godot `TileMapLayer` maps that developers can save and instance. The [Game Builder lifecycle plan](../game-builder/IMPLEMENTATION_PLAN.md) adds BGB-13 for that required native output and BGB-04 for publication; see the [output contract](../../docs/game-builder/TILEMAP_OUTPUT.md). Native authored-map output comes before huge-world streaming. BGB-02/03/06/08 integrate FEAT-06/08 and ENH-07/10 without creating replacement generator, history or archive engines. Their implementation status is tracked in the master tracker; this note does not close existing items.

The master tracker is `docs/ENGINE_ENHANCEMENT_PLAN.md` ("Terrain and grid review (2026-09-08)"). Per-subsystem history: `docs/terrain-engine/ENHANCEMENT_AND_FIX_PLAN.md` (closed), `docs/grid-system/ENHANCEMENT_AND_FIX_PLAN.md` (closed). When a plan lands, update its status here and in the tracker, and move its evidence into the component pages under `docs/terrain-engine/` and `docs/grid-system/`.
