# FEAT-09 — Player start areas: a reserved, validated, kitted zone per start

**Type:** feature (genre precedent: Age of Empires player lands and per-player object kit — Ensemble patent US7589742B2 and AoE II `create_player_lands`; 0 A.D. `rmgen-common/player.js` base kit; Civilization V start normalisation; Factorio starting-area guarantees; closes workflow review E01) · **Area:** `ecs/terrain/TerrainStartAreaStage.cs` (new), `ecs/terrain/TerrainStartKit.cs` (new Resource), `TerrainGenerationSettings.cs`, `TerrainGenerationBuffer.cs`, `GeneratedTerrainField.cs`, `TerrainFieldBuilder.cs`, `TerrainStartPositionStage.cs` (eligibility only), `TerrainGeneratorComponent.cs` (export, handoff, accessors), `TerrainWorldComponent.cs` (+ `.Drawing.cs`; recipe, status), `TerrainWorldCameraComponent.cs`, `TerrainDataLayersComponent.cs`, `TerrainTileSets.cs`, `TerrainMapOverlayComponent.cs`, `ecs/grid/GridCellDataComponent.cs` (+ `.Publication.cs`), `ecs/grid/GridStartAreaComponent.cs` (new), `GridPlacementComponent.cs`, `GridWorkerSpawnerComponent.cs`, `tests/TerrainGenerationBaselineSmoke.cs` · **Status:** Implemented 2026-09-15 · **Effort:** L (5–7 days) · **Risk:** medium–high (touches the settings record, the handoff tuple, the cell record and the baseline fixture)

## As landed (2026-09-15)

Built as designed, with these differences. Each one comes from something found while reading the code:

- **No `HqAnchorOf`, no `StartAreaOrigin(index)`.** The stage grows each area from
  `GridFootprint.Cells(origin, HqFootprint)` and checks that footprint for level ground, so the origin
  *is* the headquarters anchor. A second accessor would have been a copy of it. The data layers'
  `StartCells()[k]` is start k's origin, and `GridStartAreaComponent.OriginOf(k)` reads it (returning
  `NoCell` for an index that is not a start).
- **The Bonus test for relaxation level 3 reads the captured `TerrainResourceRules`.**
  `TerrainResourceRules.Entry` gained `Category`, plus a `Find(id)` lookup. `TerrainResourceStage.CategoryOf`
  searches every live catalog, but the stage runs on a worker against a detached copy of the map's own
  catalog, so `CategoryOf` could answer for an id that catalog does not hold. `CategoryOf` stays unused;
  its in-place note says why.
- **`GridFootprint.Cells` became an allocation-free struct enumerable.** It keeps its `IEnumerable`
  signature, which `GridObjectComponent.FootprintCells` relies on. Walking a 3×3 footprint for every
  eligible cell through the old iterator allocated 1.5 MB in the start stage.
- **The handoff carries `int StartArea`**, like the tuple's other integers. The generation buffer
  stores it as a byte.
- **`StartCells()` ordering in the materialised mode** sorts by each tile's `start_index`. Godot's
  `GetUsedCells` happens to return insertion order, which `Rebuild` already makes the start order, so
  the probe re-inserts the start tiles in reverse to prove the sort matters. `clear()` only marks cells
  for erasure, so the probe calls `update_internals()` first; without it the re-insert reuses the old
  slots and the order never changes.
- **Spawner.** `SpawnAtStartArea` gives each new worker the nearest walkable area cell (from
  `SpawnCellsFor`) that no live spawned unit stands on and that passes the spawner's own spawn rules.
  This is `actor_lab.gd`'s rule, moved into the engine. `AutoSpawnOnReady` spawns its initial workers
  the same way.
- **Placement** returns `not_ready`, never `outside_start_area`, when the start-area component or its
  cell store is not wired. An unwired reservation has no answer, and "outside" would be a guess.
- **Camera.** `StartAreaPath` set to something that is not a `GridStartAreaComponent` warns and does
  not frame. It does not silently fall back to start 0. `StartPositionViewAt` warns when asked for an
  index past the last start, then shows the middle of the map, which is the existing no-start answer.
- **Overlay rings** take their start's colour only when `ShowStartAreas` is on. Every shipped scene
  keeps its cream rings, and so do the captures the owner approved.
- **`TerrainOverlayEdges`** reports each area's border sides. `SharedSide` then turns a
  (cell, neighbour) pair into the side their two polygons share, so the same code draws borders on a
  square or a diamond grid. The headquarters outline is the same walk over the footprint rectangle.
- **`ConfigureGenerator`** now also sets `StartAreaRadius`, and its doc comment says so. The contract
  pin's documented list gained the name. That pin was already failing on `UseCustomClimateSpan`,
  which is unchanged here.

**Guards:**
- **Baseline.** All 96 existing hashes are unchanged (compared by `compare_fixture.py`). The two
  `*_start_areas` cases are recorded. **Mutation:** applying the headquarters footprint with the
  radius at 0 changed old cases' start layers.
- **`tests/terrain_start_area_probe.gd`** (registered). Small map, Continents, seed 31415, radius 10,
  critical wheat ×2 on a one-cell band, `MinAreaCells` 80: 5 of 6 starts usable, 8 relaxed placements.
  Since this session it also checks `TerrainDataLayersComponent` in both modes (origin offset (5,3)):
  `StartAreaAt` agrees with the generator on every cell, and `StartCells()[k]` is start k.
  **Mutations:**
  - Skipping the area gap failed the adjacency check.
  - Skipping relaxation failed the wheat check.
  - Skipping the footprint predicate failed the level-footprint check.
  - Reading the continent layer for `StartAreaAt` failed on 624 cells.
  - Ignoring `start_index` in the materialised `StartCells` failed the order check.
- **Handoff and save** (`TerrainWaterSurfaceSmoke`). **Mutation:** dropping `terrain_start_area`
  from `GeneratedKeys` lost the value on reload.
- **`tests/terrain_start_area_play_probe.gd`** (registered). Covers `OriginOf`, `SpawnCellsFor` order
  and blocked-cell skipping, the placement refusal and its `not_ready` cases, successive spawner
  cells, and the overlay's segment count. The count must equal every area border side plus every
  footprint perimeter (488 on the fixture), and 0 with `ShowStartAreas` off. **Mutations**, one at a
  time or together, each failed its own check:
  - Skipping the placement check: the other start's origin was allowed.
  - Ignoring standing units: three workers spawned on one cell.
  - Dropping the headquarters outlines: 416 segments instead of 488.
  - Dropping the blocked filter: a blocked cell came back as a spawn cell.
  - Not asking `HasCells`: an unwired store read as `outside_start_area`.
- **`tests/terrain_world_recipe_probe.gd`** checks the radius reaching the generator (261 reserved
  cells), `start_area_radius` at recipe version 4, the status suffix (`areas 1 of 5 usable`), nothing
  reserved at radius 0, and restore bringing back the same 261 cells. **Mutations:** not reading
  `start_area_radius` on restore, and dropping the status suffix, each failed its check.
- **Contract pins.**
  - The data-layers pin requires `StartAreaAt(`, `DescribeStartArea` and `_publishedStartOrder`.
  - A forbid pin fails if `StartCells` returns the start set again. **Mutation:** restoring the set
    return failed the scan (17 vs the 16 pre-existing).
- **Still green:** `terrain_view_grid`, `terrain_world_live_source`, `terrain_placement_live`,
  `terrain_view_parity`, `terrain_survey_overlay`, `terrain_build_approach`.
- **Camera by index** (`terrain_view_grid_probe`): a `StartAreaPath` whose active start is the last
  start frames that start, not start 0. The probe's fixture generator produced no starts in Mode 0,
  so this case switches to the default mode with four starts. **Mutation:** framing 0 regardless
  failed the check.
- **Broken and repaired by this item:**
  - `terrain_data_origin_probe` and `terrain_data_storage_probe` expected eight materialised layers.
    Both now expect nine, compare `StartAreaAt` on fixtures that actually have start areas, and the
    storage probe compares start order across modes.
  - `terrain_exact_recipe_probe` expected recipe version 3. It now expects 4 and
    `start_area_radius` 0. Nothing else reads the recipe version.
  - The contract pin that limits which grid components may reference the terrain data layers now
    lists `GridStartAreaComponent`, with the reason in the pin: the start order is recipe data the
    live cells never save. The no-terrain-kind-from-layers rule still applies to it.
- **Fixed while verifying, not caused by this item:** `tests/runtime_smoke.ps1` stopped at
  `GridPlacementSmoke` check 4, and every later check was skipped. Two checks called `CanPlace` on
  a placement with no grid, which has answered `not_ready` since placement stopped falling back on
  missing configuration.
  - `VerifyPlacementOccupancy` now asserts the unwired refusal, then tests occupancy on a wired grid.
  - `VerifyPlacementUsesCellDataTerrain` had been passing its water and blocked-flag rejections only
    through `not_ready`. It now has a grid.
  - **Mutation:** disabling the `blocked_ground` rule failed it. The whole runtime smoke passes.
- **Headless integration run** (`run_terrain_integration.ps1 -SkipRendering`): every probe passes
  except the four recorded ones (`lake_bank` finalizer, `ground_cover`, `prop_sizing :40`,
  `lab_grid :14`), each failing exactly as recorded.
- **Contract scan:** only the 16 pre-existing failures.

## Gap

`TerrainStartPositionStage` chooses **tiles**, not areas. Eligibility is a single cell — not water, not `Mountains`, `Startable` (`TerrainStartPositionStage.cs:89-91`); the score is a radius-3 disc of kinds, relief and water (`:142-200`); separation is a distance, `max(4, min(w,h)/max(2,n)*1.6)` (`:67`); one start per continent is taken first (`:72-73`). Nothing checks a building footprint, an exit or reachability, and nothing guarantees a resource near the start: `TerrainResourceStage` scatters at a uniform 0.085 share of eligible land (`TerrainResourceStage.cs:39,77`) with only a same-resource spacing of 4 (`:49`).

The output is a bare `List<Vector2I>` (`TerrainGenerationBuffer.cs:256`) that never reaches the cells. The generation→cells handoff tuple (`TerrainGeneratorComponent.cs:264-286`) carries terrain, feature, relief, shade, elevation, water source, water patch, inland terrain, beach width, lake patch and lake width — not starts, so `CellRecord` (`GridCellDataComponent.cs:601-619`) has no start fact and `GridWorldStateComponent` cannot save one; only the seed is saved. Every engine consumer reads `starts[0]`: `TerrainWorldComponent.Drawing.cs:286` (`StartPositionView`), the camera through `StartPositionGlobal()` (`TerrainWorldCameraComponent.cs:141-150`), and the shipped `templates/scenes/actors/actor_lab.gd:59-77` (`home = starts[0]`, then its own reachable-cell search). The overlay draws one cream ring per start with no identity (`TerrainMapOverlayComponent.cs:236-245, 354-365`). The published `StartCells()` is not index-ordered: `_publishedStarts` is a `HashSet` (`TerrainDataLayersComponent.cs:53`) and the materialised path returns `GetUsedCells()` (`:391-395`). Workflow review E01 (`plans/GAMEAPP_GRID_TERRAIN_WORKFLOW_REVIEW.md:359-362`) — "playable starts need a headquarters footprint, clear exits, and reachable essential resources … Report an unusable seed, never silently reroll it" — is open.

What the genre does: the Ensemble patent divides the map into **one contiguous area per player** (a fraction of the map, e.g. 60 of 240 tiles for four players) with a minimum distance between areas and exclusion of impassable/water cells, then runs a nested loop *per player area × per object type* placing objects with min/max distance from the player origin, avoid-type distances, a **critical** flag whose constraints are **relaxed in a predefined order** when placement fails, and a spiral closest-point fallback; equity is the **same object set at matched distances for every player**, scaled by player count. AoE II scripts grow all `create_player_lands` from their origins **simultaneously** until they meet (`land_percent`, `base_size`, `other_zone_avoidance_distance`, `border_fuzziness`). 0 A.D. gives every base the same kit at authored distances — base radius `scaleByMapSize(15, 25)`, trees at 11–13 tiles, stone/metal mines at 12, berries at 12, starting animals at 9, resources avoiding the 5-tile civic-centre disc — and requires a quarter of the map diameter between bases. Civilization V divides landmasses into regions of equal fertility and **normalises** weak starts. Factorio's starting area guarantees at least one patch each of iron, copper, coal and stone, always a lake, never cliffs, and no enemies.

## Design

**Three facts, three owners.** (a) `terrain_start_area` (byte, 0 = none, k+1 = start k) is a *generated, static* per-cell fact: it rides the handoff into `CellRecord.Generated` like `terrain_relief`, so it saves with the cells and regenerates from the recipe. (b) `Owner` (FEAT-02) is the *live* fact; the only bridge is FEAT-02's `GridTerritoryComponent.ClaimStartAreasOnStart`, which seeds `Owner` from the reservation once. (c) `GridFactionCatalog` (FEAT-10) says who a faction is; `GridStartAreaComponent` says which start a faction was *assigned*. Nothing says "whose land is this" twice: placement asks `outside_start_area` (reservation) or `foreign_territory` (ownership); a game enables one or both.

**The existing stage stays the only scorer and selector.** The sort and separation (`:64-73`), the shortfall warning (`:80-86`), `Take` (`:93-137`) and `Score` (`:142-200`) are untouched. Its one change is a stricter eligibility predicate when the feature is on. The new stage never re-scores.

1. **Settings (deterministic input).** `TerrainGenerationSettings` gains `int StartAreaRadius` (0 = off, clamped 0..32) and `TerrainStartKit? StartKit` (a reference, like `ResourceCatalog`, with the same caveat that mutating the resource does not invalidate the cached field). `TerrainGeneratorComponent` exports both beside `StartPositionCount` (`:91`): `StartAreaRadius = 0`, `StartKit = null`. `ApplyMapSetup` (`:489-525`) does **not** touch them, so shipped demos are unchanged. `TerrainWorldComponent` gains a `StartAreaRadius` export pushed in `ConfigureGenerator` and a recipe key `start_area_radius` (default 0 on read); `RecipeVersion` 3 → 4 (`TerrainWorldComponent.cs:194`). The kit resource is not in the recipe — the same limitation as `Resources` today — and is listed for BGB-02's catalog-inputs slot.
2. **`TerrainStartKit : Resource`** owns the per-scenario start requirements E01 asks for: `HqFootprint` (Vector2I, default 3×3), `ExitCount` (default 2), `AreaGap` (cells between areas, default 1), `MinAreaCells` (0 = 60 % of the radius disc), `Entries : Array<TerrainStartKitEntry>` with `ResourceId`, `Count` (1..8), `MinDistance` (default 2), `MaxDistance` (default = radius), `Critical`. A null kit means the defaults with no entries. Every field is read by the stage; none is decorative.
3. **Eligibility (the one change to the existing stage).** `Eligible(world, settings, index)`: when `StartAreaRadius > 0`, every cell of `GridFootprint.Cells(origin, HqFootprint)` must be in bounds, land, not `Mountains`, `Startable`, and share the anchor's relief — the same rule `GridPlacementComponent.WhyNot` enforces as `not_level` (`GridPlacementComponent.cs:366-367`), walking `GridFootprint.Cells` exactly as `:358` does. With the feature off the predicate is exactly today's.
4. **`TerrainStartAreaStage.Apply(world, settings, rules)`** runs as `Run("Start areas", …)` immediately after the start positions (`TerrainFieldBuilder.cs:123`) and returns at radius 0 without allocating. Per-cell output `world.CellStartArea` (`byte[]` through `CellValues`, `TerrainGenerationBuffer.cs:124-131`; null on the field when absent, so `StartAreaAtCell` answers 0 at no cost). Every step is integer, scan-order deterministic, with hashes through `TerrainGeometry.Hash01(x, y, seed + <new constant>)`:
   - *Growth:* one queue seeded with all origins in start order (`IntScratchA`, as `TerrainContinentStage.cs:17-52` does); a cell is claimable when it is land, relief ≠ `Mountains`, not `BlockedByDefault` (the catalog, DUP-13), within `r²` of its origin, and no 8-neighbour belongs to another area within `AreaGap`. Equal-distance ties go to the earlier queue entry — "first influence owns", deterministic. Every area cell is reachable from its origin over land by construction.
   - *Exits:* claimable land cells 4-adjacent to the footprint ring; fewer than `ExitCount` → problem `no_exit`. *Size:* fewer than `MinAreaCells` → `area_too_small`.
   - *Kit (the AoE nested loop, per start × per entry):* candidates are area cells with distance in `[MinDistance, MaxDistance]`, outside the footprint, holding no resource, where `TerrainResourceRules.Entry.Supports(terrain, relief)` holds for the entry's resource (the same `rules` object `TerrainResourceStage.Apply` receives, `TerrainResourceStage.cs:57-62`, and applies at `:117-126`); ordered by `Hash01`, taking `Count` with spacing 4 between the same id. Surface entries write `world.Resource`; Underground entries are satisfied if the area already holds the id, else stamp a radius-2 disc (`CellUndergroundResource`, richness 0.5, depth from the definition); Liquid entries are a kit validation error (`kit_entry_unplaceable`) because areas contain no water. **Relaxation order** when short: (1) band → whole area; (2) drop spacing; (3) overwrite an existing Bonus-category non-kit resource (`TerrainResourceStage.CategoryOf`, `:188-189`). `Supports`, land and relief are never relaxed. Still short: `Critical` → `missing_critical:<id>`, else `missing:<id>`. Each placement records its relaxation level.
   - *Report:* `StartAreaReport { Index, Origin, Footprint, CellCount, Exits, Placements[(id, cell, relaxation)], Problems[], Usable }` per start on the buffer and field (`StartAreas`). Unusable starts stay in `StartPositions` — dropping them would hide the fact. Diagnostics (`TerrainGenerationSettings.cs:137-180`) gain `start_area_count`, `start_area_usable_count`, `start_area_min_cells`, `start_area_max_cells`; one `GD.PushWarning` lists the problems, mirroring the shortfall warning at `TerrainStartPositionStage.cs:80-86`.
5. **Publication.** `GeneratedTerrainField.StartAreaAtCell`, `StartAreas`; generator `StartAreaAt(cell)` and `GetStartAreaReports()` (an `Array<Dictionary>`, GDScript-callable, no overloads per the note at `TerrainGeneratorComponent.cs:288-290`). The handoff tuple (`TerrainGeneratorComponent.cs:264-286`; the `GeneratedCell` alias at `GridCellDataComponent.Publication.cs:4` and `CreateGeneratedRecord` at `:10-20`) and `LoadGeneratedCells` (`GridCellDataComponent.cs:521`) gain `byte StartArea`; `GeneratedMetadata` (`:588-589`) gains `StartArea`; key `terrain_start_area` is present only when > 0 (no save growth outside areas), read back by `GeneratedKeys` (`:682-683`) and `AdoptMetadata` (`:777`); reader `GridCellDataComponent.GetStartArea(cell)`. Data layers: `StartAreaAt(cell)` (field path through `HasPublishedCell`; materialised path through a ninth `StartAreaData` layer with `Cell.StartArea` int custom data and `DescribeStartArea`, one tile per index like the continent layer at `TerrainDataLayersComponent.cs:187-188`) and `StartAreaOrigin(index)`. **Fix inside this item:** `_publishedStarts` becomes an ordered list plus set, and the `StartData` tile gains a `start_index` custom data, so `StartCells()[k]` is start k in both modes. The accessor list the contract scan pins (`tests/addon_contract_scan.ps1:2038`) gains `StartAreaAt(`.
6. **`GridStartAreaComponent`** (`ecs/grid/`): exports `CellDataPath`, `DataLayersPath`, `NavigationPath`, `LocalStartIndex` (0). API: `ActiveStartIndex` (= `LocalStartIndex` here; FEAT-10 redirects it through the assignment), `StartAreaAt(cell)` from cells, `OriginOf(i)` / `HqAnchorOf(i)` from the data layers, `IsInArea(cell, i)`, `SpawnCellsFor(i, count)` = nearest walkable area cells (`IsInBounds && !IsBlocked`) ordered by (distance², y, x) — `actor_lab.gd:65-71` moved into the engine. Consumers arriving with it: `GridPlacementComponent.RestrictBuildToStartArea` (default false) + `StartAreaPath`, with `WhyNot` (`:347-377`) returning `outside_start_area` when any footprint cell's `terrain_start_area ≠ ActiveStartIndex + 1`, checked after `off_grid` and before `not_level` (FEAT-02's `foreign_territory` slots right after it); `GridWorkerSpawnerComponent.SpawnAtStartArea` (default false) + `StartAreaPath`, so `SpawnWorker()` and `AutoSpawnOnReady` (`GridWorkerSpawnerComponent.cs:68-75`) take cells from `SpawnCellsFor` instead of `SpawnCell + (i, 0)`; `TerrainWorldComponent.StartPositionViewAt(int)` / `StartPositionGlobalAt(int)` with the no-argument forms delegating to index 0; `TerrainWorldCameraComponent.StartAreaPath` (optional) so `FrameStartPosition` frames `ActiveStartIndex`, else 0 — today's behaviour.
7. **Views.** Overlay `ShowStartAreas` (default false): an edge walk where `StartAreaAtCell` differs between +x/+y neighbours, factored as `TerrainOverlayEdges.Collect(bounds, idAt)` so FEAT-02's border pass reuses it; colour per index from a fixed 24-entry palette (FEAT-10 lets the catalog override); the HQ footprint outline from the report; the existing ring (`:354-365`) takes the index colour. Drawn inside the ENH-09 camera window at `TerrainLayers.ZForMarkers()`. `StatusLine` (`TerrainWorldComponent.cs:521-538`) appends `areas U of N usable` when `start_area_count > 0`.

## Guards (fail first)

- **Baseline.** With `StartAreaRadius = 0` every existing `tests/TerrainGenerationBaselineSmoke.cs` case (`Settings`, `:21-35`) is byte-identical — the proof the feature is opt-in. Two new cases (seed 31415, Small and Huge, shape 0, radius 8, default kit + `{wheat×2 @2–6 critical, stone×1 @3–8}`) add layers `start_area` and `start_reports`, recorded once; the fixture diff shows every old hash unchanged. **Mutation:** re-weight `Score` by area size → the old cases' start layer changes.
- **Stage probe** `tests/terrain_start_area_probe.gd` (Small, Continents, 31415): every area cell is land and non-mountain, 4-connected to its origin, no two areas 8-adjacent, footprint cells share relief, each usable start has ≥ 2 exits, kit counts met, `start_area_usable_count` equals reported usability. **Mutation:** skip the gap test → adjacency fails; skip relaxation → `missing_critical:wheat` where the probe expects a placement; skip the footprint predicate → a start straddling hills/flat appears.
- **Handoff and save.** `cells.GetStartArea(origin) == k+1`; a cell outside every area has no `terrain_start_area` key in `ToDictionary`; `GridWorldStateComponent` Save/Load and evict/reload keep it. **Mutation:** drop the key from `GeneratedKeys` → lost on reload.
- **Placement.** `RestrictBuildToStartArea = true` → `WhyNot` is `outside_start_area` outside, `""` inside; off → unchanged. **Mutation:** skip the check → `""` outside.
- **Data layers.** `StartAreaAt` agrees with the generator for every cell in both modes; `StartCells()[k] == GetStartPositions()[k]` in both modes (the ordering fix is what makes this deterministic).
- **Existing probes** `terrain_view_grid_probe.gd` and `terrain_world_live_source_probe.gd` stay green (index 0 default).

## Dependencies / collisions

FEAT-05 also grows the handoff tuple and `GeneratedMetadata` (ramps) — make the tuple change once. DUP-13: claimability reads `Startable` / `BlockedByDefault` from the catalog; no new kind literal (the no-literals pin). ENH-09 (overlay culling); ENH-13 (the minimap tint is FEAT-10's). BGB-02 (recipe completeness: the new key and the kit reference), BGB-04 (the Beep profile carries the cell key). FEAT-06: +1 byte per cell, gated. E01 closes here. `TerrainWorldComponent` and `GridCellDataComponent` are the streaming and grid sessions' files — coordinate the recipe/status and handoff edits.

## Out of scope

Engine-owned here: the stage and its determinism, the per-cell reservation and its save path, the kit mechanism (relaxation, critical, report), the single start source for consumers, the placement refusal, the spawn-cell search, camera framing by index, overlay and status. Game-owned: kit *content* per scenario, which build is the HQ and placing it (the game calls placement at `HqAnchorOf`), starting units and stock, AdvCiv-style score-and-swap balancing, Civ-style normalisation beyond the kit, enemies and hazards (the engine has none; areas exclude water, mountains and blocked kinds by construction). `actor_lab.gd` may switch to `SpawnCellsFor` later; the shipped scene is unchanged now.
