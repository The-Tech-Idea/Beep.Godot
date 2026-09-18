# FIX-15 — A temperate world is not a desert: moisture follows the map's real size, and a hot one keeps its deserts

**Type:** fix (generation calibration) · **Area:** `ecs/terrain/TerrainClimateStage.cs` (`Maritime`, `SubtropicalAridity`), `TerrainGeneratorComponent.ApplyMapSetup` (the Rainfall axis), `TerrainMapSetup`, and since 2026-09-17 the woodland stand scale and cover (`TerrainFeatureStage`, `TerrainNoiseSet`) · **Status:** implemented 2026-09-17 with the owner's decisions on stands, cover, Rainfall and the dry belt; guarded and baseline re-recorded; awaiting the owner's acceptance and commit (see "As landed") · **Effort:** S–M · **Risk:** medium. It changes generated maps, so the generation baseline is re-recorded, and it is a look change that goes through the owner's before/after gate.

## Evidence

The owner's Oilfield Days capture (2026-09-16) shows "beach sand inside the terrain, inside grass". That sand is **desert**, and there is a lot of it. Oilfield Days builds its map from these settings:

- a 24 km basin: 144×144 cells, Continents, land 0.6;
- `ClimateLatitudeSpan` = km / 10000;
- temperate and normal rainfall, climate maps and scale rules on.

The same settings, built headless:

| Map | Climate | Desert | Dry grass | Grass | Beach sand |
|---|---|---|---|---|---|
| 32×32 | temperate / normal | 0 % | 72 % | 0 % | 28 % |
| 64×64 | temperate / normal | 20–21 % | 59–61 % | 5–6 % | 14–15 % |
| 96×96 | temperate / normal | 40–41 % | 43–46 % | 4–5 % | 9–10 % |
| 144×144 | temperate / normal | 44–52 % | 35–46 % | 3–6 % | 6–7 % |
| 144×144 | temperate / **wet** | 44 % | 43 % | 6 % | 7 % |
| 144×144 | hot / arid | 61 % | 28 % | 5 % | 6 % |
| 144×144 | cold / normal | 0 % | 0 % | 63 % | 7 % |

- The temperate rows use seeds 31415 and 2027, and five more seeds at 144×144 gave 44–60 % desert.
- The wet, arid and cold rows are seed 31415 only.
- The temperate latitude centre is 0.52 on every row.

Three facts come out of the table:

1. **Desert grows with map size at a fixed climate.** `TerrainClimateStage.Maritime` (`TerrainClimateStage.cs:64-72`) fades coastal moisture to zero at a fixed `SamplesPerCell × 6.5` from the coast, i.e. 6.5 cells.
   - Moisture is `fractal × 0.62 + maritime × 0.38 − rain shadow − subtropical aridity`, then scaled by `lerp(0.55, 1, temperature)` (`:50-57`).
   - Past 6.5 cells a tile keeps only the noise share. At latitude 0.52 the arid belt removes about 0.07 and the temperature factor is about 0.86, which pushes most of it under the desert cutoff 0.20 (`TerrainBiomeStage.cs:38`).
   - The bigger the map, the more of its land is "interior", whatever the land represents. Oilfield Days' 144 cells are 24 km. No 24 km basin has a continental interior; the world's interior deserts lie hundreds to thousands of kilometres from the sea.
2. **The Rainfall axis never reaches moisture.** `ApplyMapSetup` turns it into `LakeCoverage`, `RiverDensity` and `FeatureDensity` only (`TerrainGeneratorComponent.cs:573-577`). A "wet" temperate world is still 44 % desert. The world offers Rainfall as a climate control and nothing in the climate reads it: accepted, and ignored where its name says it acts.
3. **The cold map is the green one.** Cold / normal gives 63 % grass, and temperate gives 3–6 %. The temperate centre sits near enough to the subtropical dry belt (centre 0.34, width 0.17, `:81-87`) that grass hardly appears.

## Design

1. **Measure the coastal reach in kilometres, not cells.** The map already states its real extent through `ClimateLatitudeSpan`, and `TerrainGenerationBuffer.Latitude` reads it in two branches:
   - below 1, the height covers `span` latitude units, and one unit is equator to pole, about 10,000 km;
   - at 1 or above, the height runs pole to equator to pole, about 20,000 km.

   Oilfield Days sets `span = km / 10000`, so its 144-cell, 24 km basin is about 0.17 km a cell. `TerrainScaleRules` sets `span = height / 240`, which is about 42 km a cell. The fixed 6.5-cell reach is therefore about 1.1 km on the game's basin and about 270 km on a lab map: the same constant describes two different climates. Kilometres per sample come from the same two branches, so latitude and moisture read one fact.

   The decay follows measurement, not a guess. On non-forested land, annual precipitation falls exponentially with distance from the ocean, with a global mean e-folding length of about 600 km ([Makarieva, Gorshkov & Li 2009](https://www.sciencedirect.com/science/article/abs/pii/S1476945X08000834), *Ecological Complexity* 6:302–307). The paper explains the different behaviour of forested regions with a separate hypothesis, the "biotic pump". This fix does not rely on it: only the non-forest measurement is used, because the generator places forests after climate. So `Maritime` becomes `exp(-distance_km / 600)` and the linear 6.5-cell ramp goes.
   - A whole-world map keeps a continental interior.
   - A region map of tens of kilometres is maritime throughout, which is what a basin, an island or a province is.
   - One fact, the map's geographic extent, decides both latitude range and moisture reach. There is no second scale dial.
2. **Rainfall reaches moisture.** The axis adds a moisture offset (arid −, wet +) in `TerrainMapSetup`, which already owns what each axis means, next to its lake, river and feature scales. It passes through the settings to the climate stage.
3. **The aridity belt keeps its latitude.** It is only reached where the map's latitude actually lies in it, which the span already decides. The temperate centre is checked against the Whittaker bands: after 1 and 2, a temperate / normal map must be majority grass and dry grass at every size.

## Guards (fail first)

- **`terrain_climate_share_probe.gd`**, over sizes {32, 64, 96, 144} × two seeds × the game's span rule:
  - temperate / normal: desert ≤ 10 % of land at every size, grass + dry grass ≥ 70 %, and the desert share does not rise with size;
  - hot / arid: desert ≥ 40 %;
  - wet: less desert than normal at the same seed and size;
  - a whole-world map (span 1) still has an interior drier than its coast.
  - **Mutations:** fixed 6.5-cell reach restored → the size check fails; Rainfall offset removed → the wet check fails.
- **Baseline.** Re-recorded deliberately. The changed layers (terrain, features, resources, starts) are listed in this item's outcome with before/after renders of Oilfield Days' basin and the lab.

## Dependencies / collisions

- **FEAT-15 (player lands first):** land this first or together. A core's base terrain is chosen against the climate this fixes.
- **VIEW-07 / FIX-14 (shore owners):** untouched.
- **Separate desert and beach art:** a rendering item that needs art. Slots 2 (desert) and 3 (sand) both sample `tex_sand`, and in the art styles both use `art_beach` as their base (`terrain_splat.gdshader:171-172,190`). Even a correct desert draws as beach until they differ.

## Out of scope

- A full wind-transport moisture model.
- Seasons (FEAT-07).
- Per-biome art.

## As landed (2026-09-17)

### Code

- **Coastal reach.** `TerrainClimateStage.Maritime` is `exp(-distance_km / 600)`. `distance_km` comes from `TerrainGenerationBuffer.KilometresPerSample(span)`, which uses `Latitude`'s own two cases: below span one, the height covers that fraction of 10,000 km; at span one, it runs pole to pole over 20,000 km.
- **Rainfall is water and growth, not ground (owner decision, 2026-09-17).** Design step 2 was built first and then reversed. A flat moisture offset (arid −0.35, wet +0.20), tuned on the maritime basin, made every inland cell of both scale-rules lab maps desert when arid, because those maps are mostly dry grass to begin with. Civilization scopes the axes differently. Its map scripter, Sirian, says "Rainfall affects feature types: forest, jungle, marsh, oasis, etc." and "Temperature affects terrain types"; the owner's standing reference for world axes is the same. So:
  - Rainfall scales lakes and rivers (`WaterScaleFor`, unchanged) and vegetation (`TerrainMapSetup.VegetationScaleFor` → `FeatureDensity`: 14/18, 1, 22/18).
  - The offset, its export and its settings field are gone, and `ApplyMapSetup` derives eleven settings again.
  - Evidence item 2 ("the Rainfall axis never reaches moisture") no longer holds as a defect. The desert it pointed at came from the coastal reach, and wet no longer adds jungle ground.
- **The dry belt (owner decision, 2026-09-17).** The kilometre reach broke the genre's other rule. `tests/examples/biomes.gd` failed: a hot Continents map had no desert, and hot lab maps were greener than temperate ones. The belt's peak of 0.20 had been chosen while every interior past 6.5 cells was bone dry. It now sits where subtropical deserts are, 15–35° latitude (*ACC Physical Geology*, 2nd ed., §23.2): centre 0.28 (25°), width 0.13, peak 0.35. It had been centred on 31° and still at half strength at 43°.
- **Woodland stands (owner decision, 2026-09-17: same change).** The desert had hidden an older defect.
  - The vegetation noise was scaled to the landmasses at 2.2 × the continental frequency × `FeatureFrequencyMultiplier` 0.18, with a floor of 0.01. On the 144-cell basin that is about a hundred cells a wavelength.
  - Against `TerrainFeatureStage`'s 8-cell percentile blocks, which each take the nearest block's threshold, every block took the high side of a flat slope. Once the map turned green, forests came out in straight bands.
  - Now `StandWavelengthTiles` sets the field in tiles, and `FeatureFrequencyMultiplier` defaults to 1, relative to that standard stand. No scene set the old value.
  - A block has to hold more than one wavelength, so the stand and the block are chosen together. A first cut was 6-cell stands on 8-cell blocks; shown small stands against large forests, the owner chose larger forests, Age of Empires style: **16-cell stands on 24-cell blocks**.
- **Woodland cover (owner decision, 2026-09-17, after research).** With the map green, woods and forest covered 39–45% of the basin's land under the old curve, a slope of 2.6 on the map's average wetness. While Rainfall still shifted moisture, it grew 0% arid and 83% wet.
  - What shipped games grow: Civilization VI caps forest at a share of land plots, `FeatureGenerator.lua`'s `iForestPercent` of 18 moved by −4 arid and +4 wet. Age of Empires II's standard land maps give forest 12–18% of the map, in clumps of about 60–225 tiles. Civilization V takes the top 31% of a forest noise field plus a 10% clump field.
  - `TerrainFeatureStage.WoodsCapableShare` now decides the share of **all land**, `0.18 × FeatureDensity`, and divides it over the woods-capable cells. Civilization VI's caps, 14, 18 and 22%, come through `FeatureDensity` from Rainfall.
- **Guards the change weakened.** On `tests/examples/vegetation.gd`'s 48x48 map a 24-cell block is a quadrant, so its "no bare quadrant" check could no longer fail: ranking the whole map at once still left every quadrant some woods. It gains a spread check: the thinnest quadrant must carry at least half the rate of the richest. The contract scan's `ApplyMapSetup` pin, which fired when the offset was added, is back to eleven names.
- **Fixtures the change moved.** Two probes check that their own map still exercises what they test, and the greener map failed both preconditions.
  - `terrain_start_area_probe` needs usable and unusable starts, and every area of 84 cells or more now held its wheat. Its minimum area goes from 80 to 120 cells: the areas are 189, 154, 138, 97 and 84, so three are usable and two too small.
  - `terrain_start_distance_probe` compares mean richness near and far from the starts and needs ten deposits each way. Only 7 now lay within 10 cells. Near becomes 15 cells, which holds 29.

### Measured (the game's recipe, seeds 31415 and 2027 unless noted; climate shares of inland cells)

| Case | Before | After |
|---|---|---|
| Temperate/normal desert, 32 / 64 / 96 / 144 cells, share of land | 0 / 20–21 / 40–41 / 44–52% | 0% at every size |
| Temperate/normal grass + dry grass, share of land | 72–73% at 32 cells (all of it dry grass), 41–49% at 144 | all of the inland cells, at every size |
| Hot/normal desert on the lab maps (64x64 seed 2027, 96x60 seed 31415) | with the kilometre reach and the old belt: 0% | 29% and 36% |
| `biomes.gd`'s hot 64x40 Continents map, desert tiles | 328 before FIX-15; 0 with the kilometre reach and the old belt | 231 |
| Basin (seed 12345), desert + dry grass, temperate against hot | 12% against 10% with the old belt | 2% against 85% |
| Temperate/arid desert on the lab maps | 100% and 98% with the Rainfall offset | 0%: 53–56% dry grass, 44–47% grass |
| Whole world (span 1), dry share of interior against coast | 88–96% against 41–51% | 88–96% against 47–54% |
| Woodland edges on a ranking-block boundary, basin at normal rainfall (seeds 12345 and 2027) | 24–31% on 8-cell blocks (chance 12.5%) | 3–7% on 24-cell blocks (chance 4%) |
| Woodland, share of land, basin arid / normal / wet (seeds 12345 and 2027) | the old curve on the green map: 39–45% at normal | 13.3–13.6% / 17.3–17.5% / 21.2–21.4% |
| Woodland, share of land, lab maps arid / normal / wet | — | 13.4–13.6% / 17.2–17.6% / 20.5–20.7% |
| Stands on the basin at normal rainfall: count, median size, woodland in stands of 25+ cells | — | 56–60, 22–23 cells, 79–81% |

### Guards

`tests/terrain_climate_share_probe.gd` over `tests/TerrainClimateShareSmoke.cs` checks seven things:
- size does not make desert;
- temperature reaches the ground, through the dry belt;
- rainfall reaches water, not the ground;
- a whole world keeps an interior;
- woods follow their own field;
- woodland is what shipped strategy maps grow;
- woodland comes in forests.

It is registered in `run_terrain_integration.ps1`. Each check failed first:

| Mutation | What failed |
|---|---|
| Fixed 6.5-cell reach restored | The size checks: 31% and 16% desert at 144 cells, "desert rose with map size" |
| The pre-FIX-15 dry belt (31°, width 0.17, peak 0.20) | All five temperature checks: hot lab maps 0% desert, the hot basin 10% dry against 12% temperate |
| Rainfall shifting moisture again (−0.35 when arid) | The ground check on all three maps: arid 100%, 98% and 33% desert |
| Rainfall not scaling water | All six water checks: lakes and rivers did not move |
| A 60,000 km coastal reach | The interior check for seed 31415: 18% dry against 11% on the coast |
| The vegetation field scaled to the landmasses again | The edge checks: 16–25% of woodland edges on block boundaries, against at most 12.5% (three times chance) |
| A 40-cell stand wavelength | The edge checks, 13–20%; it still made 30–40 stands, which is why the stand count is not a check |
| A 6-cell stand wavelength | The stand checks: median 12–13 cells against at least 18, 37–47% in stands of 25+ against at least 70% |
| Rainfall not scaling vegetation | The arid and wet bands and the gap: 17.2–17.5% at every rainfall |
| A reference share of 0.08 | Every lower band (normal 7.3–7.4%), and the stand checks, since stands shrink with cover |
| A reference share of 0.26 | Every upper band (normal 25.2–25.3%, wet 30.7–31.0%) |
| `vegetation.gd`: the whole map ranked at once | Two bare islands (101 and 74 woods-capable tiles), and the new spread check (11% against 38%) |

`tests/examples/biomes.gd`, which caught the dry-belt regression, passes again.

### Baseline

Re-recorded after a structural old-versus-new comparison of every case's layers, in three steps as the design settled.
- **Against the fixture from before FIX-15, changed in every case:** terrain (cell, sample and inland), features, resources, underground resource and richness, and starts and shores. Underground depth changed in six of the ten cases. Start areas, reports, distance and neutral sites changed where a case has them.
- **Unchanged in every case:** continent, elevation, relief, sample water, water source, liquid resources and shade.
- **The steps:**
  1. The reach with 6-cell stands.
  2. The 16-cell stands and cover: features only, in all ten cases.
  3. The dry belt with Rainfall taken off moisture: terrain, resources and features in nine cases, features alone in the tenth.

  None of the steps moved the land, the water or the relief.

### Renders

In `tmp/` (Painted, cartoon art):
- `fix15_oilfield_basin_before.png` against `fix15_oilfield_basin_final.png`: the game recipe, seed 12345.
- `fix15_lab_large_before.png` against `fix15_lab_large_final.png`: a Large lab map on the scale rules, seed 31415.
- `_final_arid.png`, `_final_hot.png` and `_final_wet.png`: the same maps at arid, hot and wet.
- The steps between, same maps: `_after.png` (the banded forests of the old stand scale on the green map), `_after_stand6.png` (6-cell stands at the old cover), `_after_cover18_small_stands.png` (6-cell stands at the new cover), `_belt_arid.png` (what the Rainfall moisture offset did to an arid lab map before it was taken out).

### Still open

- **Lakes at wet rainfall** — now [FIX-16](FIX-16-a-wet-world-has-more-lakes.md). `ApplyMapSetup` asks for 9.5% lake coverage when wet and the stage delivers less than at normal's 5%: 0.99% on the basin, 2.11% on the standard lab map, 0% on the large one. The request is one flood budget, so it grows one lake instead of more, and `DrainOversizedLakes` then deletes whole lakes once a landmass passes 30% water; the rivers Rainfall also raises are what tip it over. It predates FIX-15. The probe checks that arid has fewer lakes than normal and deliberately does not claim wet has more.
- **The rain shadow** still reaches four cells upwind, a distance in cells, and reads normalized elevation (see `TerrainClimateStage.md`).
- **The scale rules' span** (`height / 240`, "a whole planet pole to pole at 240 tiles") and `Latitude`'s reading of a span below one (a fraction of equator to pole) differ by a factor of two. FIX-15 measures distance with `Latitude`'s reading, the one the climate bands use.
