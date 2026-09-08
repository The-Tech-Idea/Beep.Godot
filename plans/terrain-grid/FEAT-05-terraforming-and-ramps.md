# FEAT-05 — Terraforming API; a writer for `terrain_ramp_direction`

**Type:** feature + accepted-then-ignored fix · **Area:** `GridCellDataComponent` (terrain metadata keys), `GridNavigationComponent.Search.CanCross` (ramp rule), `TerrainGeneratorComponent` handoff, `TerrainIsometricRendererComponent` (block heights), `TerrainPaintedRendererComponent` (shade/elevation), `GridToolActionComponent`, `GridBuildDefinition` · **Status:** proposed 2026-09-08 · **Effort:** M (3–4 days) · **Risk:** medium (elevation is read by four renderers and navigation)

## Gap

1. **A reader with no writer.** `GridNavigationComponent.Search.CanCross` (`GridNavigationComponent.cs:155-168`) implements ramps: a height step of exactly 1 is crossable only if the lower cell's metadata `terrain_ramp_direction` (a `Vector2I`) points uphill. `GridCellDataComponent.SetMetadata` (`:291-292`) bumps `NavigationRevision` when that key changes. A repository-wide scan finds **no writer**: no generation stage, no handoff, no tool, no component ever sets `terrain_ramp_direction`. `AllowRamps` is therefore an export that enforces nothing — every 1-step slope is impassable when `RespectHeight` is on, whatever the flag says. (The `MOUNTAIN_ACCESS_RAMPS.md` doc describes ramps for the *prefab* mountain assets, which paint visuals only and write no cells — the tracker records that gap too.)
2. **No terraforming.** `terrain_elevation`, `terrain_relief`, `terrain_shore_inland` are written once by the generator handoff (`GeneratedCells`) and read by navigation (`HeightAt`), the isometric block renderer (levels), the painted renderer (shade) and `TerrainDataLayersComponent`. No API raises, lowers, flattens or cuts terrain at runtime — the citybuilder/strategy genre's "level ground before building", "dig a canal", "cut a pass" all need it.

## Design

1. **`GridTerrainEditComponent`** (`ecs/grid/`; grid orchestrates, renderers respond) with a small, complete API — each returns `bool`/count and emits `TerrainEdited(kind, cells)`:
   - `SetElevation(cell, int level)`, `Raise(cells, +1)`, `Lower(cells, -1)`, `Flatten(Rect2I, toLevel)` — writes `terrain_elevation`/`terrain_relief` through `GridCellDataComponent` (change kind `Terrain | Navigation`), clamps to `TerrainLayers.Count` levels, recomputes `terrain_shade` for the 3×3 neighbourhood with `TerrainShadingStage.AtCell` (the static already exists for exactly this: "local cell gradient over a snapshot").
   - `SetRamp(cell, Vector2I uphill)` / `ClearRamp(cell)` — **the writer** for `terrain_ramp_direction`; validates that the uphill neighbour is exactly one level higher.
   - `Cut(cells)` — mountain → hills → flat with rock kind rules from the kind registry (DUP-13) or today's `PeakKinds` table.
   - Cost hooks: `TerraformCost(kind, cells)` consulted by the wallet (`GridResourceWalletComponent.TrySpend`) when `ChargeWallet` is on.
2. **Generator writes ramps.** `TerrainTileReductionStage`/handoff marks natural ramps: where a hill cell borders a flat cell and the sample-level slope is gentle (elevation gradient below a threshold), write `terrain_ramp_direction` on the lower cell — so generated hills are climbable at their gentle sides, as in Anno/Settlers maps. Deterministic; part of the field so the determinism probes cover it.
3. **Tools and builds:** `GridToolActionComponent.ToolAction.Raise/Lower/Flatten/Ramp` (turn-costed jobs through the queue like other tools — `JobWorkTurns`); `GridBuildDefinition.FlattenFootprint` (default false) — a build that levels its footprint on placement (city-builder standard).
4. **Renderers:** already subscribe to cell changes; with ENH-01 they rebuild the edited chunks' blocks/shade/collision. `TerrainDataLayersComponent` reports the **generated** relief (per the tracker's ownership decision) — live relief is the cell's; the doc page states which is which.

## Guards (fail first)

- Probe: generated Small map with hills; `AllowRamps = true` → at least one hill/flat pair is crossable (today: zero — this is the guard that proves the reader had no writer). **Mutation:** skip the generator's ramp writer → zero crossable pairs.
- Probe: `Raise(cell)` → `HeightAt` +1, iso block level +1 for that cell, painted shade texel changed within 3×3 only; `Flatten` → level uniform; `TerrainRevision` bumped once per call.
- Probe: `SetRamp` on a 2-step drop rejected (`ramp_step_too_steep`).
- Smoke: `FlattenFootprint` build on a slope → footprint cells level, job cost charged.
- Determinism: three-seed field snapshot unchanged except the new `terrain_ramp_direction` key (record new baseline once).

## Dependencies / collisions

ENH-01 (kinds), DUP-13 optional (kind rules on cut). `ecs/grid/` and `TerrainGeneratorComponent` handoff — coordinate with both sessions.

## Out of scope

Water-level changes (canals that flood) — needs the coast field to be live-editable beyond kind flips; listed as follow-on. Prefab mountain ramps (`MOUNTAIN_ACCESS_RAMPS.md`) stay with the mountain asset workstream.
