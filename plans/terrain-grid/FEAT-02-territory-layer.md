# FEAT-02 — Territory / ownership as a cell layer

**Type:** feature (genre standard: Civ borders, Settlers land claims, Anno island ownership, OpenTTD company ownership of tiles) · **Area:** `GridCellDataComponent` (typed record field), `GridPlacementComponent`, `GridSelectionJobCommandComponent`, `GridObjectiveTrackerComponent`, `TerrainPaintedRendererComponent`/`TerrainMapOverlayComponent` (border draw), `ui/GridMinimapComponent`, `StrategyEmpireComponent` · **Status:** proposed 2026-09-08 · **Effort:** M–L (4–6 days) · **Risk:** medium (touches the cell record; save format grows one field)

## Gap

Nothing in `ecs/grid/` or `ecs/terrain/` knows who owns a cell. The Python scan finds `territory` once, in a `StrategyEmpireComponent` doc comment ("base per-turn yield … the empire's territory"), and no `Owner`/`Faction` on `CellRecord`, `GridObjectComponent` (it has `OwnerId` only for units via `ActorComponent`), placement rules or objectives. So:

- Placement cannot refuse a build on another faction's land (Civ/Settlers rule #1).
- Objectives cannot be "own 40 cells" / "claim the oil basin".
- The strategy genre's yield comment cannot be implemented as written.
- Workers/haulers cannot be restricted to their side of a border.

## Design

1. **Data:** `CellRecord` gains `byte Owner` (0 = unowned; faction ids are small — `GridFactionCatalog` resource maps id → name/colour; ≥ 250 factions is not this grid's game). Stored in the archive envelope like other typed fields; `GridCellDataComponent.SetOwner(cell, owner)` / `GetOwner(cell)` / `FillOwner(Rect2I | radius)`; change kind `Gameplay | Territory` (add `Territory = 16` to `TerrainChangeKind`, ENH-01) so renderers that draw borders react and terrain renderers ignore it.
2. **Claiming rules (component, not doctrine):** `GridTerritoryComponent` — the *default* claim model: a placed `GridObjectComponent` with `ClaimRadius > 0` (a town hall, a border post) claims cells within radius on completion and releases on demolition; contested cells go to the nearer/older claimant (deterministic tie-break). Games replace or subclass it (the extractor's "DEFAULT, not doctrine" pattern). `GridPlacementComponent` gains `RequireOwnedLand` (default off — sandbox behaviour unchanged) and `PlacementOwner`.
3. **Consumers arriving with the feature (rule 6):** placement rule; `GridObjectiveDefinition` kind `own_cells` (count) and `own_cell` (specific cells) with the tracker fed by `TerritoryChanged`; `GridSelectionJobCommandComponent` refuses jobs on foreign land when `RespectTerritory`; `StrategyEmpireComponent` yield becomes `GoldPerTurn × owned cells / base` behind an export so existing tuning is unchanged by default.
4. **Presentation:** a border pass — `TerrainMapOverlayComponent` draws faction-coloured cell-edge lines where `Owner` changes between neighbours (the same edge-walk the dual-grid transition uses); minimap tints owned cells 20 % toward the faction colour (ENH-13's chunk bake). Painted shader border tint is optional later.
5. **Persistence:** owner rides in the cell record (archive + save automatically); `GridWorldStateComponent` needs nothing new.

## Guards (fail first)

- Smoke: place a hall with `ClaimRadius = 3` → 37 cells owned by faction 1; demolish → 0. Second faction's hall 4 cells away → the shared ring splits deterministically (record the split).
- Smoke: `RequireOwnedLand = true`, faction 2 tries to build on faction 1's cell → `PlacementRejected("foreign_territory")`. **Mutation:** skip the owner check → placement succeeds.
- Save round-trip: owner survives evict/reload and `Save/Load`.
- Objective: `own_cells ≥ 30` completes after the hall claim; `RestoreState` stays silent (tracker rule).

## Dependencies / collisions

ENH-01 (change kinds), ENH-13 (minimap bake). `CellRecord` is owned by the other session's streaming work — coordinate the record change. Campaign session: objective kinds (`RequiredForVictory`) are theirs — add the new kinds after their objective work lands.

## Out of scope

Diplomacy, AI factions, combat — actors/strategy layers. Fog of war (FEAT-03) is a separate layer with different rules.
