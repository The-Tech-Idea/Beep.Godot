# FIX-15 — A temperate world is not a desert: moisture follows the map's real size, and Rainfall reaches it

**Type:** fix (generation calibration) · **Area:** `ecs/terrain/TerrainClimateStage.cs` (`Maritime`, the moisture sum), `TerrainGeneratorComponent.ApplyMapSetup` (the Rainfall axis), `TerrainMapSetup` · **Status:** proposed 2026-09-16, measured, not implemented · **Effort:** S–M · **Risk:** medium. It changes generated maps, so the generation baseline is re-recorded, and it is a look change that goes through the owner's before/after gate.

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
