# FEAT-15 — Player lands first: the players shape the map, not the reverse

**Type:** feature (genre precedent: Age of Empires II `create_player_lands` + `base_size`, Age of Mythology per-player areas, 0 A.D. `placePlayerBases` + `avoidClasses(clPlayer, …)`, Age of Empires IV `startBufferTerrain`, Factorio's start-relative noise, Return To The Roots' cleared headquarters) · **Area:** `ecs/terrain/TerrainFieldBuilder.cs` (stage order), a new `TerrainPlayerLandStage`, and the core weight read by `TerrainWaterStage`, `TerrainElevationStage.Classify`, `TerrainRiverStage`, `TerrainBiomeStage`, `TerrainCoherenceStage`, `TerrainFeatureStage`, `TerrainResourceStage`; `TerrainStartPositionStage` and `TerrainStartAreaStage` (FEAT-09/14) · **Status:** proposed 2026-09-16; the owner decided 2026-09-16: replace (not a mode), `TerrainStartKit.BaseTerrain` defaulting to grass, and FIX-15 lands first · **Effort:** L (4–6 days) · **Risk:** medium-high — it changes every map built with start areas, and the stages it touches decide each other's percentiles

## Gap

The owner, on the Oilfield Days capture (2026-09-16): *"still distribution of terrain is too random. … depending how many players, allocate area for each player, distributed, then there should be a minimum area allocated for the player base, his buildings. Then we create other things."*

The engine does the opposite order. `TerrainFieldBuilder.BuildPrepared` (`TerrainFieldBuilder.cs:77-126`) makes the whole world first — landmass, lakes, elevation, erosion, relief, climate, rivers, biomes, features — and only at the end asks `TerrainStartPositionStage` to find starts in it: it scores the finished tiles by food, production and water within 3 cells and takes the best, spaced (`TerrainStartPositionStage.cs:26-93`). FEAT-09's start areas then reserve the ground around each start (`TerrainStartAreaStage.Apply`) and **never change a tile** (rule 2 of [the 2026-09-15 research](../../docs/terrain-engine/RTS_COLONY_MAP_RESEARCH.md#8-rules-adopted-each-names-the-precedent-and-the-plan-that-carries-it): "the existing stage stays the only scorer"). So a base inherits whatever the noise put there. The game's own start log shows the area check passing: `[start] camp at (32, 62) (9, 6), 174 cells, 25 exits — usable` (Oilfield Days `tmp/start.log`). Yet the capture shows the Field Office's 9×6 pad where a sand patch, a lake and the grass meet. FEAT-09 validates that there is room. It says nothing about what the ground is, and it cannot change it.

A map can also end up with no start at all, and the engine does not say so. Oilfield Days' own terrain probe rebuilds its 24 × 24 test basin with seed 54321 and climate profile 4 (Cold, Normal rainfall). No cell there holds the camp's 9 × 6 headquarters on dry, level, startable ground, so no start is placed. `TerrainStartPositionStage` returns at `candidates.Count == 0` (`TerrainStartPositionStage.cs:66-67`), before its shortfall warning (`:86-92`), and nothing is logged. The only report is the game's: `OilfieldPresentation.PlantTile` logs "the terrain generated no start area" and founds the camp on its old fixed site, (12, 19), where the game's probe had pinned it (verified 2026-09-17). A base that is decided before the ground closes this case by construction.

That is the Civilization family: terrain first, starts fitted afterwards, fairness patched with scoring and normalisation. The owner asks for the Age of Empires family, and so do the games this engine's users make: bases, fleets and buildings on a map sized for N players.

## Research (2026-09-16)

These are shipped scripts and source, not engines. The full report with parameters and every URL is in [RTS_COLONY_MAP_RESEARCH.md §9](../../docs/terrain-engine/RTS_COLONY_MAP_RESEARCH.md#9-player-lands-first-2026-09-16).

- **Players first, terrain second.**
  - AoE2's script sections run in a fixed order: `PLAYER_SETUP` → `LAND_GENERATION` (`create_player_lands`: origins on a ring with variance, all lands grow at once, `base_size` radius 3–13 guaranteed) → `ELEVATION` → `CLIFF` → `TERRAIN` → `CONNECTION` → `OBJECTS`.
  - Terrain patches only replace their `base_terrain`. Forests use `set_avoid_player_start_areas`. Hills keep ~9 tiles from player origins and cliffs keep 22 from land origins (both measured by the RMS guide's author, not stated officially).
  - 0 A.D. places players (`playerPlacementCircle(0.35)`), stamps each base, and then everything avoids the player class: hills, mountains and forests by 20, dirt/grass patches by 12, decoratives by 10.
  - AoE4 writes `startBufferTerrain` over whatever was there within `startBufferRadius`.
- **Terrain first, starts after:** Civ V/VI, OpenRA, Widelands and Return To The Roots. They compensate with reservations, normalisation, or by throwing when no room is left.
- **In between:** Factorio makes every field a function of the distance to the start. Cliffs are suppressed near it, elevation is raised so a start always has land, and a start lake is always made.

## Design

One principle: **a base core is decided before the ground is, and every stage that shapes ground leaves it alone.** The core is a disc of `StartAreaRadius` cells around each player origin (FEAT-09's radius, one owner), plus a fade ring. Terrain decisions blend back to normal across that ring, so no circle is stamped onto the map.

1. **Place player origins right after the landmass** (new `TerrainPlayerLandStage.PlaceOrigins`, between `Landmass` and `Water`).
   - `StartPositionCount` origins go on a ring around the landmass centroid, radius 0.35–0.40 of the half-size (AoE2 `circle_radius` ~40 %, 0 A.D. 0.35), evenly spaced with seeded angular and radial variance.
   - Each origin snaps, by spiral search, to the nearest cell whose whole core is inside the land footprint and at least a core radius from the map edge.
   - Island and archipelago maps take one origin per landmass, largest first, before doubling up. That is the rule the current stage keeps (`TerrainStartPositionStage.cs:76-79`).
   - Separation is at least 2 × (radius + `AreaGap`). It relaxes ×0.95 per retry round (0 A.D.), with a bounded number of rounds. A shortfall is reported, as today (`:86-92`), never silently filled.
2. **Make the core land, and measure distance once.**
   - Samples inside a core are set to land in `Land`/`Footprint` (0 A.D.'s continent map guarantees a land blob around each player). The ocean is never inside a base.
   - The start-distance transform FEAT-14 runs at the end runs here, at sample resolution, on the shared `TerrainEuclideanDistance` transform. It gives a core weight `w = smoothstep(R + fade, R, d)`, and `CellStartDistance` is derived from the same measurement, so there is still one owner.
3. **Every ground-shaping stage reads `w`:**
   - **Water.** Lake basins are suppressed where `w > 0`, so no lake is in or at the edge of a base.
   - **Elevation / relief.** Before `Classify` cuts hills and mountains by percentile, elevation is blended toward the core's mean by `w`, so a core is Flat and its ring rises gently (Factorio's cliff suppression; AoE2's hills keep ~9 tiles off). The percentiles are measured over non-core samples, so the rest of the map keeps its hills.
   - **Rivers.** The descent treats core samples as walls, and no source starts inside a ring.
   - **Biomes and coherence.** Inside the core the kind is the kit's base terrain (AoE2 `terrain_type`, AoE4 `startBufferTerrain`). Across the ring the climate kind returns, and desert and sand are held back a further margin (0 A.D. patches avoid players by 12). Coherence neither absorbs nor spreads into a core.
   - **Features.** Nothing grows in a core: no woods, forest, jungle, marsh or oasis (Return To The Roots: no trees within 5 of the headquarters; AoE2 forests avoid start areas).
   - **Surface resources.** The uniform scatter skips cores. The kit places its own there, as FEAT-09 does.
4. **The origins are the starts.** `TerrainStartPositionStage` no longer searches the finished map when start areas are on. It validates each origin's footprint (level, dry, startable) and reports. Scoring stays the rule for maps without start areas, which are built exactly as today, except that a map with no eligible cell reports its shortfall like a partial one instead of returning before the warning. This adds a warning only and changes no data.
5. **FEAT-09 and FEAT-14 run unchanged on top.** Area growth, exits, kit and relaxation, neutral sites and richness by distance all still apply. A core is clean by construction, so a start is unusable only when the map genuinely has no room, and that is reported.
6. **Base terrain is kit data.** `TerrainStartKit.BaseTerrain` is a terrain-kind id, default `grass`, validated against `TerrainKindCatalog.Standard`: it must be Land class and Startable. Anything else is a configuration error, named at capture time, never silently replaced. An arid scenario authors `dry_grass` or `dirt`, as AoE2's desert maps use DIRT for player lands.

## Guards (fail first)

- **Baseline.** Every case without start areas stays byte-identical. The `*_start_areas` and `*_start_distance` cases are re-recorded deliberately, and the change is written up in this item's outcome with before/after renders.
- **`terrain_player_lands_probe.gd`**, over three seeds × {Continents, Island, Archipelago} × {2, 4, 6} players:
  - the requested origins are placed, or the shortfall is reported;
  - origins are pairwise separated by at least the minimum;
  - every cell within `R` of an origin is dry, Flat, the base terrain, carries no feature and no scattered resource, and is not a lake or river;
  - no lake lies within `R + fade`;
  - every start area is usable on the Standard map size;
  - the same seed gives the same origins;
  - Oilfield Days' cold case (24 × 24, seed 54321, Cold, a 9 × 6 headquarters) gets its start;
  - on a map without start areas where no cell is eligible, the shortfall is reported (the silent `candidates.Count == 0` return fails this today).
  - **Mutations:**
    - lake suppression off → a lake in a core;
    - flattening off → hills in a core;
    - biome override off → another kind in a core;
    - feature exclusion off → woods in a core;
    - river walls off → a river through a core;
    - origins placed after terrain again → the relief/biome checks fail.
- **Look.** Before/after renders of Oilfield Days' 24 km basin with the Field Office on its pad go to the owner. As with VIEW-04 and VIEW-12, this lands only once the owner has seen them.

## Owner's decisions

1. **Replace, or add a mode?** Recommended: **replace**. With `StartAreaRadius > 0` the players always shape the map; without start areas nothing changes. A separate mode keeps the "too random" path selectable, and a second way to make the same map is the duplication the standing rules forbid.
2. **Base terrain:** kit `BaseTerrain`, default `grass` (recommended), or derived from each start's climate.
3. **Placement pattern:** ring with variance, per-landmass on island maps (recommended; AoE2/0 A.D. default). Team grouping waits for FEAT-10's teams to reach generation, which happens before assignment today.

## Dependencies / collisions

- FEAT-09 (areas, kit), FEAT-14 (distance field moves earlier; one measurement), FEAT-10 (teams, later), FEAT-13 (symmetry folds origins too), FIX-14 / VIEW-07 (shore owners the water change must not bypass).
- `TerrainFieldBuilder` stage order is pinned by the progress contract (`"Complete", 20` — 20 stages); the new stage is counted.

## Out of scope

- Sizing the map from the player count: the game decides map size; Oilfield Days sizes by kilometres.
- Civ-style normalisation beyond the kit.
- Symmetric layouts (FEAT-13).
- Territory ownership at runtime (FEAT-02).
- Separate desert and beach art: a rendering item. The painted views draw desert and beach sand from one texture with starfish (`terrain_splat.gdshader` slots 2 and 3 share `tex_sand`), and needs art.
