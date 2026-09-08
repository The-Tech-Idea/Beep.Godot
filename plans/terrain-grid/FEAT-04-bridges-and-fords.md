# FEAT-04 — Bridges and fords: a road that crosses water

**Type:** feature (genre standard: OpenTTD bridges, Settlers/Anno bridges over rivers, Civ river crossings) · **Area:** `GridRoadComponent`, `GridNavigationComponent` costs, `GridCellRules.TerrainKindAt`, `GridPlacementComponent`/`GridBuildDefinition` (bridge as a build), `TerrainTileRendererComponent`/painted road shader (`terrain_roads.gdshader`), `TerrainCollisionComponent` · **Status:** proposed 2026-09-08 · **Effort:** M (3–4 days) · **Risk:** low–medium

## Gap

The generator produces rivers (`TerrainRiverStage`, drainage network, width 1–3 samples → `shallow_water` river tiles) and lakes on purpose, and the tracker's Phase 1.11 made `shallow_water` unbuildable by default for roads, builds and spawns — correctly. Navigation treats shallow water as wadeable at 2.5× and deep water as blocked. The result: a river splits every settlement in two. There is no way to build a road across it — `GridRoadComponent` refuses water cells, and nothing overrides terrain passability for a structure.

Every game in the genre has the answer: a **bridge** (a build whose footprint may stand on water and whose cells become road-passable) and a **ford** (a marked shallow crossing with road cost, no structure).

## Design

1. **Passability override on the cell.** `CellRecord` already carries roads (`GridRoadComponent` marks `IsRoad`); add a `Crossing` flag (`CellFlags.Crossing`) meaning "traversable regardless of terrain kind". `GridCellRules.TerrainKindAt` is unchanged (the kind stays water — the painter still draws water under the bridge); `GridNavigationComponent.Search.CostFor`/`IsBlocked` check `Crossing` first: crossing → road cost. Change kind `Navigation`.
2. **Bridge as a build.** `GridBuildDefinition` gains `bool IsCrossing` and `AllowedTerrainKinds` already exists (an offshore platform authorises `shallow_water` for itself — the same mechanism); a bridge definition lists `shallow_water` (+`deep_water` for long bridges if the game wants), `IsCrossing = true`, `OccupiesCells = false` (units walk on it), `BlocksNavigation = false`. On completion `GridBuildSiteComponent` sets `Crossing` on the footprint and lays road (`GridRoadComponent.SetRoad(cells)`), on demolition clears both. Straight-line footprints (1×N or N×1) with both ends on land are validated by placement (`bridge_needs_banks`).
3. **Ford as a tool.** `GridToolActionComponent.ToolAction.Ford` marks a single `shallow_water` cell `Crossing` at a higher cost (1.5× road) with no structure — cheap, early-game; blocked on `deep_water` and river tiles wider than 1 (read `GridTerrainWaterPatch`/kind of neighbours).
4. **Rendering:** road shader/tile layer draws the road stroke over water where `Crossing` is set (the road renderer already reads `IsRoad`; it needs to stop skipping water cells when `Crossing`); a bridge build's own scene draws the deck. `TerrainCollisionComponent` omits crossing cells from the water collision body.
5. **Boats:** `Crossing` does not block water movement (boats path on water kinds by job kind "fish") — a bridge cell is passable to both until a game says otherwise (`BridgeBlocksBoats` export, default false).

## Guards (fail first)

- Smoke: river splits a 48×48 map; worker cannot reach the far bank (`too_far`/blocked). Place a 1×5 bridge across → path found, crosses on bridge cells; demolish → blocked again. **Mutation:** skip the `Crossing` check in navigation → still blocked after the bridge.
- Smoke: bridge with one end on water → `PlacementRejected("bridge_needs_banks")`.
- Probe: ford on a deep-water cell rejected; on a 1-wide river accepted; cost 1.5× road.
- Render probe (headless texture read): road stroke present on the bridge cell's texel.
- Save round-trip: `Crossing` flag persists (flags already do).

## Dependencies / collisions

DUP-08 (footprint owner), ENH-14 (object index for demolition), ENH-01 (change kinds). `ecs/grid/` collision on placement/roads/navigation — coordinate. Independent of FEAT-01 (works with today's A\*).

## Out of scope

Bridge art, multi-level bridges over roads, ship locks, tunnels (a `Crossing` through a mountain is the same flag — the plan lists it as a follow-on, not built here).
