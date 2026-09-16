# FEAT-13 — Symmetric and competitive layouts

**Type:** feature (genre precedent: StarCraft II and Warcraft III melee maps use rotational symmetry for two players and mirror symmetry for four — competitive fairness *is* terrain symmetry; 0 A.D.'s circle, line, river and arc player layouts) · **Area:** `TerrainGenerationSettings.cs`, `TerrainNoiseSet.cs`, `TerrainLandmassStage.cs`, `TerrainErosionStage.cs`, `TerrainRiverStage.cs`, `TerrainCoherenceStage.cs`, `TerrainStartPositionStage.cs`, `TerrainWorldComponent.cs` (axis and recipe), `tests/TerrainGenerationBaselineSmoke.cs` · **Status:** proposed 2026-09-15 · **Effort:** L (5–8 days) · **Risk:** high (every stage that scans in raster order can break exact symmetry)

## Gap

There is no symmetry anywhere: noise is sampled per position, erosion, rivers (steepest descent over the D8 network, `TerrainRiverStage.cs:60-66`) and biome coherence (majority votes) run in scan order, and starts are taken greedily by score (`TerrainStartPositionStage.cs:64-73`, `Take` at `:93-137`). Competitive RTS fairness is terrain symmetry, not start symmetry; the colony and 4X genres the addon ships get their fairness from FEAT-09's kit — which is why this is a separate, last item and not an option on FEAT-09. Origin symmetry alone would be cosmetic.

## Design

1. `TerrainSymmetry { None, MirrorX, MirrorY, Rotate180, MirrorXY }` on the settings record, the generator export, the world axis and the recipe (`None` default; recipe key `symmetry`). `TerrainNoiseSet` samples at the folded coordinate, so landmass, water, elevation and climate are symmetric by construction.
2. Stages with scan-order tie-breaks process each cell together with its image (erosion diffusion is symmetric when the field is; the river source ranking and the coherence vote gain an image-aware tie-break). Honest fallback: a `symmetry_error` diagnostic (the fraction of cells whose image differs) is **reported, not hidden**.
3. `TerrainStartPositionStage`: under symmetry, candidates are taken in image pairs (choose one, add its image; separation checked on both). FEAT-09 areas then grow symmetrically because their inputs are.

## Guards (fail first)

- Baseline: every existing case is unchanged with `None`; a new `Rotate180` Small case is recorded once.
- Probe: `terrain[c] == terrain[image(c)]` for at least 99.5 % of cells and `symmetry_error` reported; starts are exact image pairs. **Mutation:** sample one noise channel unfolded → symmetry collapses and the probe fails.

## Dependencies / collisions

FEAT-09 (image pairs must satisfy the footprint predicate). FIX-02 (the dead noise channels were removed — fewer to fold). The generation stage files belong to the terrain session.

## Out of scope

Main/natural/third expansion layouts (a later kit `Scope = Expansion` reusing FEAT-14's placement), hand-authored ladder maps (FEAT-12 markers), asymmetric-but-balanced maps (a game's map script).
