# TerrainFeatureStage

Generation stage in the terrain pipeline, run by `TerrainFieldBuilder` after the biome table and tile reduction, on the reduced gameplay-tile grid rather than the fine sample grid.

`TerrainFeatureStage` decides which terrain tiles carry a vegetation/water "feature" layered on top of their base terrain kind — woods, dense forest, jungle, marsh, oasis — the way Civilization treats a tile as "grassland with woods on it" rather than a separate forest terrain. For woods-eligible ground (grass, dry grass or tundra that is not water, not mountains and not too cold) it:
1. builds a noise field (`TerrainNoiseSet.Vegetation`) biased by moisture;
2. decides what share of that ground carries woodland (`WoodsCapableShare`, below);
3. ranks the field into a percentile threshold computed **locally per 24x24-tile block**, each cell taking its nearest block's threshold unblended; a block with fewer than `MinBlockCells` eligible cells takes the map-wide threshold;
4. assigns `None`/`Woods`/`Forest` by comparing each cell's stand value against its block's threshold and "dense" cutoff.

Jungle, swamp and desert tiles get their matching feature (Jungle/Marsh/rare Oasis) unconditionally rather than through the ranked field.

## How much woodland

`WoodsCapableShare` decides the budget as a share of **all land**, then divides it over the woods-capable cells (at most 95% of them):

share of land = 0.18 × `FeatureDensity`

**0.18** (`ReferenceWoodlandShare`) is what shipped strategy maps grow. Civilization VI caps forest at a share of land plots: `FeatureGenerator.lua`'s `iForestPercent` of 18, moved by −4 for arid rainfall and +4 for wet. Age of Empires II's standard land maps give forest 12–18% of the map (Arabia 13, Mongolia 12, Highland 18). `ApplyMapSetup` carries the rainfall part as `FeatureDensity` (`TerrainMapSetup.VegetationScaleFor`: 14/18, 1, 22/18), so a map grows 14, 18 or 22% of its land as woodland.

The curve this replaced was a slope of 2.6 on the map's average wetness. On the green temperate basin it grew 39–45% woodland. While FIX-15 briefly let Rainfall shift moisture as well, it grew 0% when arid and 83% when wet.

Measured, temperate (basin seeds 12345 and 2027; lab 64x64 seed 2027 and 96x60 seed 31415):

| Map | Arid | Normal | Wet |
|---|---|---|---|
| Oilfield Days' basin, 144x144 | 13.3–13.6% | 17.3–17.5% | 21.2–21.4% |
| Scale-rules lab maps | 13.4–13.6% | 17.2–17.6% | 20.5–20.7% |

The measured shares come out slightly under the caps.

## Stands and the block grid

Nearest-block thresholds leave no visible seam only while the field varies within a block. The field's wavelength is set in tiles by `StandWavelengthTiles` (16), and `BlockTiles` (24) holds one and a half of them.

Before FIX-15 the field was scaled to the landmasses, about a hundred tiles a wavelength on a 144-tile map, with 8-tile blocks. Each block's percentile took the high side of a flat slope, and 24–31% of woodland edges lay on a block boundary, where chance puts one in eight: straight bands of forest. Measured with 24-tile blocks on the basin at normal rainfall, seeds 12345 and 2027:

| Wavelength | Edges on a block boundary (chance 4%) | Stands | Median stand | Woodland in stands of 25+ |
|---|---|---|---|---|
| ~100 tiles (landmass-scaled) | 16–25% | 39 | 36–38 tiles | 91–93% |
| 40 tiles | 13–20% | 30–40 | 32–33 tiles | 90–92% |
| **16 tiles** | **3–7%** | **56–60** | **22–23 tiles** | **79–81%** |
| 6 tiles | 2–4% | 110 | 12–13 tiles | 37–47% |

Sixteen groups woodland into forests with open ground between them, the way Age of Empires II's maps do, rather than scattering copses. Coverage is a percentile and barely moves with the wavelength. `terrain_climate_share_probe` guards the edge share, the coverage and the stand sizes; `tests/examples/vegetation.gd` guards that every landmass and every quadrant of a map gets its share.

## Public API

- `internal static void Apply(TerrainGenerationBuffer world, TerrainNoiseSet noise, TerrainGenerationSettings settings)` — the only entry point. Populates `world.Feature[cell]` for every cell in the reduced tile grid. No-ops entirely if `settings.FeatureDensity <= 0`.
- `public const string None = ""`, `public const string Woods = "woods"`, `public const string Forest = "forest"`, `public const string Jungle = "jungle"`, `public const string Marsh = "marsh"`, `public const string Oasis = "oasis"` — the feature-name string constants other files (notably the renderers) switch on to decide what to draw.
- `internal const int BlockTiles = 24` — side of a ranking block, in tiles. Internal so `TerrainClimateShareSmoke` can measure how many woodland edges fall on a block boundary.
- `internal const float StandWavelengthTiles = 16` — wavelength, in tiles, of the vegetation field at a `FeatureFrequencyMultiplier` of one; read by `TerrainNoiseSet`.

Everything else (`Blend`, `WoodsCapableShare`, `Choose`, `StandBias`, and the constants `MinBlockCells`, `StandSpread`, `ReferenceWoodlandShare`) is private; the class is `internal static`.

## Dependencies

- Reads `TerrainGenerationBuffer.CellWater`, `CellRelief`, `CellTerrain`, `MoistureAtCell`, `TemperatureAtCell`, `CellsWide`/`CellsHigh`, `CellIndex` and the `FloatScratchA` buffer; writes `TerrainGenerationBuffer.Feature` (from `TerrainGenerationBuffer.cs`).
- Reads `noise.Vegetation` (a `TerrainNoiseSet` field, from `TerrainNoiseSet.cs`) for the per-cell vegetation fbm sample.
- Reads `settings.FeatureDensity` and `settings.Seed` (from `TerrainGenerationSettings.cs`).
- Calls `TerrainKindCatalog.Standard.FeatureEligibility` to decide which kinds are woods-capable and which carry jungle, marsh or an oasis.
- Calls `TerrainGeometry.SortedSelection` and `TerrainGeometry.RankedValue` (from `TerrainGeometry.cs`), map-wide and per block, to turn the vegetation field into coverage-meaning thresholds, and `TerrainGeometry.Hash01` for the per-cell jitter and the oasis roll.
- Calls `TerrainTileSets.IsLandKind` (from `TerrainTileSets.cs`) in `WoodsCapableShare` to decide which tiles count as land.
- `TerrainFeatureRendererComponent` (and the isometric feature renderer, and `TerrainDataLayersComponent`) read `world.Feature` back out (via `GeneratedTerrainField`/`TerrainGeneratorComponent.FeatureAt`) and switch on the `Woods`/`Forest`/`Jungle`/`Marsh`/`Oasis` constants defined here.
- Called by `TerrainFieldBuilder.Build`, after `TerrainResourceStage.Apply` and before `TerrainScaleConstraintStage.ApplyFeatures` — the latter can subsequently strip features that don't reach a minimum contiguous tile count.

## Notes

- Extensive comments in the file document measured regressions that were fixed and are guarded against by the current code: (1) ranking the *bare* noise instead of noise-plus-bias caused whole dry islands (five islands, 49–171 tiles) to grow nothing; (2) a single global (rather than per-block) threshold left a quarter of one map's land bare because the field's "top slice" landed in one region; (3) blending a block's threshold toward its neighbours (rather than taking the nearest block's value outright) pulled low-lying thresholds up and a genuinely wet quadrant grew nothing; (4) deciding the budget over the eligible cells rather than over all land coupled coverage to eligibility, and widening eligibility left fewer woods (seven bare islands became eleven).
- A comment explicitly notes a *removed* setting: "Dryness is not re-tested here — and the generator export that once carried it is gone, because nothing read it" — i.e., a dryness gate/export was deleted after being found to duplicate what the biome classification already decided. This is a resolved instance of the exact duplication pattern the project's standing rules warn about (two places deciding the same fact), already fixed rather than a live issue.
