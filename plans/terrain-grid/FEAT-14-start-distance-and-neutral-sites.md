# FEAT-14 — Distance-from-start field, richness scaling and neutral sites

**Type:** feature (genre precedent: Factorio's resource richness and enemy bases scale with distance from spawn; Age of Empires places neutral objects between player areas at a minimum distance from every area; RimWorld raids arrive from the map edges) · **Area:** `ecs/terrain/TerrainStartAreaStage.cs` (FEAT-09), `TerrainStartKit.cs` (`Scope`), `TerrainGenerationBuffer.cs`, `GeneratedTerrainField.cs`, `TerrainDataLayersComponent.cs`, `TerrainGeneratorComponent.cs` · **Status:** Implemented 2026-09-16 · **Effort:** S–M (2–3 days) · **Risk:** low

## As landed (2026-09-16)

Built as designed, with these differences:

- **The distance is measured whether or not areas are reserved.** Design 1 computes
  `CellStartDistance` in the start-area stage, and that stage returned at radius 0. Distance from the
  starts is a fact about the starts, not about their areas; Factorio measures it from spawn. `Apply`
  now returns early only when there are no starts. The measurement and the scaling run whenever
  `StartDistanceScaling` is above 0, before the radius check. A map with both dials at 0 still
  allocates nothing.
- **The scaling runs before the kit.** It multiplies only what `TerrainSubsurfaceStage` laid, so the
  deposits the kit stamps, per start or neutral, keep the richness of 0.5 the kit gives them.
- **Scaled richness is clamped to [0.05, 1].** Design 2 multiplies without a bound: at `s` = 2 a
  deposit on a start would fall to 0 and one at the farthest cell would double. The result is clamped
  to `[TerrainSubsurfaceStage.MinimumRichness, 1]`.
  - 0.05 is `MinimumRichness`, the thinnest deposit the subsurface stage lays. The start-area stage
    reads that one constant, not a copy, so a cell never says it holds a resource with nothing to take.
  - 1 is the top of the range richness is banded against (`TerrainUndergroundIdentity.RichnessBand`
    clamps to 0–1 first), so the far country saturates rather than leaving the range.
- **The neutral band excludes each area's gap, not only the areas.** Design 3 describes land "beyond
  every area plus gap", but its guard only asks that a placement lie outside every area. As landed, a
  band tile is in no area, no area holds a cell within the kit's `AreaGap` (Chebyshev) of it, and it is
  claimable ground: dry, not mountainous, not blocked by default, the same ground an area may reserve.
  The probe checks the gap as well as the areas. Only the placement's own cell is held to the band. A
  neutral underground deposit's radius-2 disc is stamped on empty cells that belong to no area.
  - The first build stamped those cells whatever their ground. Rims ran under terrain the catalog says
    never holds the resource: 32 cells on the probe's crowded map.
  - Every stamped deposit, per start or neutral, now takes only cells whose terrain and relief the
    resource `Supports`, the rule the subsurface stage lays every deposit by.
  - The fix changed the underground layers of the two `*_start_distance` baseline cases and nothing
    else, so the fixture was re-recorded after a compare run showing exactly those four layers.
- **An undefined `Scope` fails capture.** A value that is neither PerPlayer nor Neutral, which a script
  or a hand-edited resource can store, would be skipped by both passes. `TerrainStartKitRules.Capture`
  now throws, naming the entry and the value.
- **Neutral entries are read only with start areas on.** They are placed at the end of the area pass,
  after every start's kit, so a contested site takes what the players' own areas left. Like the rest
  of the kit they need `StartAreaRadius` above 0: at radius 0 a Neutral entry places nothing and
  `NeutralSites` stays `None`. With fewer than two starts, each Neutral entry reports
  `neutral_needs_two_starts:<id>`, because one start has no "between".
- **The neutral report is its own record.** Design 3 says "FEAT-09's relaxation and report
  (`neutral_placements`)". The placements and problems land on `TerrainNeutralSitesReport`
  (`GeneratedTerrainField.NeutralSites`, `TerrainGeneratorComponent.GetNeutralSiteReport()`), and
  `neutral_placements` is the diagnostic count. A neutral shortfall never makes a start unusable.
  Per-start and neutral placement share one `Place` core and one relaxation order, and both reports
  build their GDScript placements through `TerrainStartAreaPlacement.ToArray`.
- **`StartDistanceAt` answers -1, never 0, when nothing was measured**, because 0 means the tile is a
  start. The data layers read the published field in both storage modes and never materialise the
  distance as tiles.
- **The recipe carries the dial through `TerrainWorldComponent`.** Its `StartDistanceScaling` export
  is pushed by `ConfigureGenerator`, whose doc comment names it, and saved as `start_distance_scaling`.
  `RecipeVersion` is 5, and a recipe without the key restores 0. `RecipeError` refuses a non-finite
  value or one outside 0–2, so generation is cancelled without touching live cells.
- **`Scope` is on `TerrainStartKitEntry`**, as design 3 says, not on `TerrainStartKit.cs` as the area
  line above has it. `TerrainStartKitRules.Entry` carries it to the worker.
- **A wrong comment was removed.** `TerrainGeneratorComponent.CurrentSettings` said a NaN scaling would
  make the settings cache miss on every query. Record equality compares a float field with
  `float.Equals`, which treats NaN as equal to NaN, so it would not. The value is now clamped to 0–2
  like its neighbours.

**Guards:**

- **Baseline.** `TerrainGenerationBaselineSmoke` hashes two more layers, `start_distance` and
  `neutral_sites`. `SnapshotWithStartDistance` (FEAT-09's radius-8 kit plus one Neutral horses and one
  Neutral iron entry, at scaling 1) is recorded as the two `*_start_distance` cases. The probe was
  first run against the pre-change fixture. The only failures were the two new layers and the two
  missing cases; every earlier layer of all eight existing cases was unchanged. The fixture was then
  re-recorded and holds ten cases of twenty layers.
- **`tests/terrain_start_distance_probe.gd`** (registered in `run_terrain_integration.ps1`).
  - *Far country*: a 112×80 Continents world with two starts and abundant Oil And Gas, read at scaling
    0 and at 1. Scaling 0 measures nothing: -1 on every cell and no neutral report. Scaling 1 gives
    every start 0 and every cell the brute-force rounded Euclidean distance. Every deposit keeps its
    kind and depth and carries exactly the clamped factor, and the surface resources do not move. The
    near/far mean-richness ratio falls against the same map unscaled.
  - *Crowded starts*: a 64×48 map with six starts and radius-10 areas. Every neutral placement is in
    the band, outside every area and its gap, dry, not mountainous, not lava and on the map. Counts
    stay within `Count × starts`, any shortfall and the unknown id are reported, and the diagnostics
    agree. A one-start map reports `neutral_needs_two_starts`.
  - *Kit invariants*, through `tests/TerrainStartKitSmoke.cs`:
    - an entry with scope 7 fails capture by name;
    - with a per-player and a Neutral iron entry that both stamp deposits, no underground cell lies
      under ground its resource does not support.
  - **Mutations**, each failing the probe:
    - the curve inverted;
    - the band filter dropped;
    - the area and gap filter dropped;
    - the distance floored instead of rounded;
    - scope validation removed;
    - the rim's `Supports` test removed.
  - Each section returns true from its last line. When the smoke was first missing from
    `Beep.Godot.csproj`, a runtime error aborted that section silently and the probe still printed OK.
- **The plan's richness guard could not fail, and was replaced.** "Mean underground richness within 10
  cells of an origin is lower than the mean beyond 30" still passed with the curve inverted (0.169
  near against 0.186 far), because this map's deposits are already richer far from its starts. The
  probe compares the map scaled against the same map unscaled instead: the ratio of the near mean to
  the far mean must fall. That check fails under inversion (0.906 scaled against 0.555 unscaled).
- **Recipe.** `terrain_world_recipe_probe` and `terrain_exact_recipe_probe` both expect recipe
  version 5. The world-recipe probe saves `start_distance_scaling` and restores it with the same
  distances. The exact-recipe probe saves 0 for a world that scales nothing, and checks that 2.5,
  -0.5 and NaN are refused without touching live cells.
- **Data layers.** `terrain_data_storage_probe` compares `StartDistanceAt` across the two storage
  modes, and `terrain_data_origin_probe` across a shifted origin.
- **Contract pins.** `StartDistanceScaling` joins `ConfigureGenerator`'s documented list, and
  `StartDistanceAt(` joins the data-layer accessors. Two pins are new, and both were mutation-proven:
  - `StartDistanceAt` must read the published field in both storage modes.
  - The stage must measure through `TerrainEuclideanDistance.Squared(`, clamp to
    `[MinimumRichness, 1]`, skip cells without a deposit, and keep Neutral and PerPlayer entries apart.

  The scan still reports only the 16 pre-existing failures.

## Gap

Resource density is uniform (`TerrainResourceStage.cs:77`), underground richness is pure field noise (`TerrainSubsurfaceStage.cs:58-62`), and nothing knows how far a cell is from a start, so neither the map nor a game can make the far country richer and more dangerous, and nothing places the contested sites between players that make AoE maps play. Games read generated facts from the data layers (`GridResourceScatterComponent.DataLayersPath`, `:36`) — there is no distance fact to read.

## Design

1. `float StartDistanceScaling` (0..2, 0 = off) on the settings record, the generator export and the recipe. When > 0 and starts exist, `CellStartDistance` (ushort, Euclidean to the nearest origin, water included) is computed in the start-area stage and published as `StartDistanceAt(cell)` on the field, the generator and the data layers — a generated fact, read from the layers like the scatter reads resources. It is not written to cells (dense and regenerable).
2. Richness: `CellUndergroundRichness *= lerp(1 − 0.5s, 1 + 0.5s, d / dMax)` — a multiplicative pass over deposits that already exist; no second placer.
3. Neutral sites: `TerrainStartKitEntry.Scope { PerPlayer, Neutral }` (added here, so it has a reader the moment it exists). Neutral candidates are land cells beyond every area plus gap whose two nearest origins differ in distance by at most 2 cells (the Voronoi band between areas); `Count × starts` placements spread by hash with FEAT-09's relaxation and report (`neutral_placements`).

## Guards (fail first)

- Baseline unchanged at 0; a new case with scaling 1 recorded once.
- Mean underground richness within 10 cells of an origin is lower than the mean beyond 30. **Mutation:** invert the curve → fails. `StartDistanceAt(origin) == 0`.
- Every neutral placement satisfies the band and lies outside every area. **Mutation:** drop the band filter → a placement inside an area appears.

## Dependencies / collisions

FEAT-09 (origins, the kit, the report). FEAT-06 (+2 bytes per cell in the field, gated).

## Out of scope

Enemy and raid spawning and creep camps (game-owned; they read `StartDistanceAt`), thinning surface density near starts (would be a second placer), per-chunk distance for infinite worlds.
