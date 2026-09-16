# VIEW-06 — River flow is generated data, drawn by every sea

**Type:** feature (the generator computes flow, throws it away, and every view animates rivers as still water) · **Area:** `TerrainRiverStage`, `TerrainGenerationBuffer`, `TerrainTileReductionStage`, `GeneratedTerrainField`, `TerrainGeneratorComponent` (handoff), `GridCellDataComponent` (tuple, metadata keys), `ITerrainSurfaceData`, new `ecs/terrain/TerrainFlowField.cs`, `TerrainWaterMaterial`, `shaders/water_common.gdshaderinc`, `TerrainIsometricRendererComponent` · **Status:** proposed 2026-09-15 · **Effort:** M–L (3–5 days) · **Risk:** medium (touches the generation handoff and the cell record's generated metadata; baseline re-recorded once)

## Gap

`TerrainRiverStage` computes a full drainage network — `TerrainFlow.Accumulate` fills each cell's downhill neighbour, the height order and the accumulation (`TerrainFlow.cs:25-31`) — into scratch buffers the later stages reuse (`TerrainRiverStage.cs:60-66`), carves channels whose radius follows accumulation (`:78-89`, radius `:87`), and keeps nothing but `WaterBody.River` per sample. `TerrainTileReductionStage` reduces river presence to a per-cell body (any ≥ 10 % of samples, `TerrainTileReductionStage.cs:23-26,34`). What a consumer can ask is `WaterSourceAtCell == "river"` (`GeneratedTerrainField.cs:103-109`); `ITerrainSurfaceData` exposes the same five facts (`ITerrainSurfaceData.cs:5-12`) and its live implementation reads `terrain_water_source` (`:27-32`). Direction, width and trunk-versus-tributary are gone before the field is published.

So no view can draw flow. The block view draws river cells as flat diamonds in the shared sea material with foam off (`TerrainIsometricRendererComponent.cs:439-450`, material duplicate `:1073-1077`); the painted and tile views draw them as water cells. The shared sea's texture drifts in one map-wide direction at `wave_speed` (`shaders/water_common.gdshaderinc:73-75`), so a river flows nowhere and upstream is indistinguishable from downstream. The authored river connector resources describe direction, inlet/outlet role and an animation contract for art (`docs/game-builder/TERRAIN_FLOW_CONNECTIONS.md:6-9`) but "do not change renderer defaults" (`:28-30`) — art without a fact to bind to. FEAT-04 (bridges and fords) needs river width and has nothing to read.

## Design

1. **Keep the flow before the scratch is reused.** `TerrainRiverStage` copies, per river sample, the D8 direction of `flowsTo` (sbyte 0..7, −1 none) and the accumulation into two buffer-owned arrays (`TerrainGenerationBuffer.FlowDirection`, `FlowAccumulation`), sized once per build in ENH-16's allocation pattern.
2. **Reduce to the cell.** `TerrainTileReductionStage`: per river cell, direction = the majority D8 among its river samples (tie → the sample with the largest accumulation); `RiverWidth` 1..3 = the carve radius that produced it (`:87`).
3. **Publish.** `GeneratedTerrainField.FlowDirectionAtCell(cell) → int` (−1 off-river) and `RiverWidthAtCell(cell) → int`; the handoff tuple (`TerrainGeneratorComponent.GeneratedCells`, `LoadGeneratedCells` at `GridCellDataComponent.cs:521`) and `GeneratedMetadata` (`:588-589`) gain `FlowDirection`/`RiverWidth`; keys `terrain_flow_direction` and `terrain_river_width` in `HasMetadata/GetMetadata` (`:632-655`) and `GeneratedKeys` (`:682-683`), present only on river cells; `ITerrainSurfaceData.FlowAtCell` with both implementations (`:15-44`). A river cell painted by hand has no flow (−1) — still water, never an invented direction.
4. **One texture, one writer.** `TerrainFlowField` (renderer-side) builds an RGBA8 texel per cell from `ITerrainSurfaceData`: RG = unit direction encoded 0..1, B = width / 3, A = source class (0 none, 1 river, 2 lake, 3 ocean, from `WaterSourceAtCell`). `TerrainSeaSurface` (VIEW-04) builds it and hands it to `TerrainWaterMaterial.Apply` as `flow_map`; `Settings` gains the texture, and being positional (`TerrainWaterMaterial.cs:43-51`) every view stops compiling until it passes one.
5. **The shader draws it.** `water_common.gdshaderinc`: on river texels the water UV scrolls along `flow_map.rg` at `wave_speed × width`, foam off; the open-sea/lake class is read from `flow_map.a`, so the block view's opaque river duplicate (`:1073-1077`) loses its `foam_strength 0` override and the painted view keeps `lake_map` for banks only.
6. **Docs:** `TERRAIN_FLOW_CONNECTIONS.md` cross-references the fact the connector profiles can now bind to; the generator page lists the two keys.

## Guards (fail first)

- `tests/terrain_generation_baseline_probe.gd` (ENH-16, 96 layer hashes): re-recorded once for the two new layers; then a new check that every river cell's direction points at an eight-neighbour that is water (river, lake or ocean) or off the map. **Mutation:** skip the reduction copy → every direction is −1 → fails on the first river cell.
- Handoff and save: `terrain_flow_direction`/`terrain_river_width` survive `LoadGeneratedCells`, evict/reload and `Save/Load`. **Mutation:** drop the keys from `GeneratedKeys` → missing after reload.
- Rendered: a fixture map with one north–south river drawn Tiles, Painted and Isometric — two captures 0.5 s apart show the river texel phase advancing along the flow direction in all three seas, while an open-sea texel keeps the map-wide drift. **Mutation:** drop `flow_map` from `Apply` → the river phase follows the sea's drift.
- Pin, both directions: `SetShaderParameter("flow_map"` only in `TerrainWaterMaterial.cs`; `FlowDirectionAtCell(` declared on `GeneratedTerrainField`.

## Dependencies / collisions

VIEW-04 (the surface builds the field) and VIEW-05 first; ENH-16 (landed: buffer allocation pattern and the baseline probe); FEAT-04 reads `RiverWidthAtCell`; the terrain art library's river connector profiles (`TerrainConnectorProfile.gd`) bind authored tiles to the direction later. Collisions: `GridCellDataComponent` is the grid session's file (the tuple change); FEAT-09 also extends the handoff tuple — whichever lands first adds its field, the other rebases.

## Out of scope

Waterfalls and junction art; validating authored connector routes (`TerrainFlowConnections.gd`); navigation cost from current; erosion; a flow field for lakes and sea currents.
