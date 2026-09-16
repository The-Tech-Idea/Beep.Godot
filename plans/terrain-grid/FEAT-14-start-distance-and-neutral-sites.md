# FEAT-14 — Distance-from-start field, richness scaling and neutral sites

**Type:** feature (genre precedent: Factorio's resource richness and enemy bases scale with distance from spawn; Age of Empires places neutral objects between player areas at a minimum distance from every area; RimWorld raids arrive from the map edges) · **Area:** `ecs/terrain/TerrainStartAreaStage.cs` (FEAT-09), `TerrainStartKit.cs` (`Scope`), `TerrainGenerationBuffer.cs`, `GeneratedTerrainField.cs`, `TerrainDataLayersComponent.cs`, `TerrainGeneratorComponent.cs` · **Status:** proposed 2026-09-15 · **Effort:** S–M (2–3 days) · **Risk:** low

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
