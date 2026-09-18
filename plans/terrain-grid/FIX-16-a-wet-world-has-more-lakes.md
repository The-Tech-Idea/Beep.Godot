# FIX-16 — A wet world has more lakes: one budget grows one lake, and a scale rule deletes it whole

**Type:** fix (generation) · **Area:** `ecs/terrain/TerrainWaterStage.cs` (`CarveLakeBasins`), `ecs/terrain/TerrainScaleConstraintStage.cs` (`DrainOversizedLakes`), `TerrainScaleRules` · **Status:** proposed 2026-09-18, found while measuring FIX-15 · **Effort:** S–M · **Risk:** medium. It changes generated maps, so the generation baseline moves, and it is a look change that goes through the owner's before/after gate.

## Evidence

The Rainfall axis raises lake coverage and river density together — `ApplyMapSetup`: `LakeCoverage = 0.05 × WaterScaleFor(rain)` and `RiverDensity = 1.0 × WaterScaleFor(rain)`, so 0.015/0.3 arid, 0.05/1.0 normal, 0.095/1.9 wet. A wet world should have more lakes than a normal one. It has fewer, or none.

Delivered lake coverage (the `lake_coverage` diagnostic, samples over the whole field — the same quantity the request is sized against), temperate Continents, land 0.6:

| Map | Arid (0.015 / 0.3) | Normal (0.05 / 1.0) | Wet (0.095 / 1.9) |
|---|---|---|---|
| Oilfield Days' basin, 144x144, seed 12345 | 1.50% | 5.00% | **0.99%** |
| Standard lab map, 64x64, seed 2027 | 1.50% | 5.01% | **2.11%** |
| Large lab map, 96x60, seed 31415 | 1.45% | 5.00% | **0.00%** |

The two dials separate cleanly. Holding rivers at normal and sweeping the lake request alone, the stage delivers exactly what it is asked for until it collapses to nothing:

| Lake request | 0.015 | 0.03 | 0.05 | 0.07 | 0.095 | 0.15 | 0.25 |
|---|---|---|---|---|---|---|---|
| Basin, delivered | 0.0150 | 0.0299 | 0.0500 | 0.0700 | 0.0946 | **0.0000** | **0.0000** |
| 64x64, delivered | 0.0150 | 0.0299 | 0.0501 | 0.0701 | 0.0949 | **0.0000** | **0.0000** |
| 96x60, delivered | 0.0145 | 0.0301 | 0.0500 | **0.0000** | **0.0000** | **0.0000** | 0.0143 |

And rivers decide the collapse at the wet request:

| Map | 0.05 lakes, rivers 1.0 | 0.05, rivers 1.9 | 0.095, rivers 1.0 | 0.095, rivers 1.4 | 0.095, rivers 1.9 |
|---|---|---|---|---|---|
| Basin | 0.0500 | 0.0500 | 0.0946 | **0.0099** | **0.0099** |
| 64x64 | 0.0501 | 0.0501 | 0.0949 | **0.0211** | **0.0211** |
| 96x60 | 0.0500 | 0.0501 | **0.0000** | **0.0000** | **0.0000** |

## Cause

Two rules meet, and neither is wrong on its own.

1. **The request is one flood budget, so it grows one lake rather than more lakes.** `TerrainWaterStage.CarveLakeBasins` turns `LakeCoverage` into `requested` samples, sorts candidate seeds by noise, and floods from the best seed with `budget = Mathf.Max(1, requested - carved)` — the whole remaining budget. The flood spreads over every 4-connected inland sample until the budget is met or the interior runs out. There is no per-lake cap. Raising the request makes the first lake bigger.
2. **The scale rule then deletes whole lakes.** `TerrainScaleConstraintStage.DrainOversizedLakes` compares each landmass's total lake cells against `TerrainScaleRules.MaxLakeShareOfLandmass` (0.30) of that landmass, and while it is over, turns entire lakes back into land, largest first. It never trims one. With a single big lake per landmass the outcome is binary.

Rivers are what tips it at the wet setting: river density 1.4 and above cuts enough land out of the landmass that the same lake crosses the allowance and is deleted whole. The 96x60 map crosses it at a lake request of 0.07 without any extra rivers, which is why it is empty at wet even at river density 1.0.

The 0.30 rule exists for a good reason, recorded in `TerrainScaleRules`: several archipelago islands came out as a ring of beach around open water. The defect is the pairing — an uncapped single lake against an all-or-nothing drain.

## Design

1. **A lake is a lake, not a sea: cap each one.** Give `TerrainScaleRules` a `MaxLakeTiles` beside its `MinLakeTiles` (8), and have `CarveLakeBasins` flood each seed with `min(requested - carved, MaxLakeTiles × SamplesPerCell²)`. The budget then spreads across seeds, so a wet map gets MORE lakes rather than one bigger one — which is what the axis promises and what the genre does: Civilization places many small lakes rather than one inland sea.
2. **Trim, do not delete.** `DrainOversizedLakes` should shrink a landmass's lakes to the allowance — dropping the smallest lakes, or eroding a lake's outer ring — so a map never loses all its inland water to one threshold. Deleting whole lakes largest-first is what turns 9.5% into 0%.
3. **Keep the reason the 0.30 rule was added.** A small island must still not be a ring of beach around open water; the guard for that stays.

Both numbers are chosen against measurement, not invented: a cap that still lets a normal map deliver 5% and a wet map deliver about 9.5% on every fixture above.

## Guards (fail first)

- A new section in `terrain_climate_share_probe.gd` (or its own probe): **lakes grow with rainfall.** On the basin and both lab maps, delivered lake coverage rises arid < normal < wet, and wet is at least 1.5× normal. The probe already checks arid < normal; the wet half is deliberately absent today because it fails.
- **No landmass is mostly lake:** every landmass's lake share stays at or under the scale rule, measured on the map.
- **Mutations:** the per-lake cap removed (the wet maps collapse to 0–2% again); the trim replaced by whole-lake deletion (the same); the 0.30 rule removed (an island comes back as a ring of beach).

## Dependencies / collisions

- `TerrainWaterStage` and `TerrainScaleConstraintStage` are generation stages; no other session owns them today.
- It moves the generation baseline, so it lands with a re-record and a structural old-versus-new comparison.
- FEAT-15 (player lands first) also touches what ground a start may claim; land it after FEAT-15 unless the owner moves it ahead.

## Out of scope

- River shape and density (`TerrainRiverStage`).
- Lake shores and banks (FIX-14).
- Whether the Rainfall axis should move lakes at all: it should, and does — this is about what the stage does with the number.
