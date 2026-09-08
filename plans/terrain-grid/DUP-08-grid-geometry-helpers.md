# DUP-08 — Footprint, Z-clamp and current-instance helpers have one owner each

**Type:** duplication fix · **Area:** `GridPlacementComponent`, `GridObjectComponent`, `GridExtractorComponent`, `GridResourceScatterComponent`, `GridToolActionComponent`, `GridObjectInspectorComponent` · **Status:** proposed 2026-09-08 · **Effort:** XS (½ day) · **Risk:** low

## Finding

| Helper | Copies | Notes |
|---|---|---|
| footprint enumeration (`for y … for x … yield cell + (x, y)`) | `GridPlacementComponent.FootprintCells:547-554`, `GridObjectComponent.FootprintCells:264-271`, `GridExtractorComponent.FootprintCells:600-614`, `GridObjectInspectorComponent.CoversCell:1077-1081` (the inverse test) | Extractor's version also has a "single cell under parent" fallback the others lack |
| `ClampZ(int)` | `GridPlacementComponent:568-573`, `GridResourceScatterComponent:430-435` | identical |
| `ResolveCurrent<T>(NodePath, ref T)` | `GridPlacementComponent:398-402`, `GridToolActionComponent:346-350` | identical; both duplicate what `EntityComponent.Resolve(this, path, ref field)` already does since the Phase-3 wiring rule |

`GridObjectComponent` is the natural owner of "the cells I stand on"; the placement component and the extractor ask *an object* for its footprint, and the inspector asks whether an object covers a cell.

## Design

- `GridObjectComponent` gains `public IEnumerable<Vector2I> Footprint Cells()` (already there) plus `public bool Covers(Vector2I cell)` and a static `GridFootprint.Cells(Vector2I origin, Vector2I size)` for callers that have a definition but no object yet (placement preview). All four sites call these.
- `GridExtractorComponent.FootprintCells` becomes `ResolveGridObject()?.FootprintCells() ?? GridFootprint.SingleUnder(parent, grid)`.
- `ClampZ` → `GridProjectionComponent.ClampZ` (the projection owns z-order conventions; `SetZIndexFromY` already lives there).
- Delete both `ResolveCurrent<T>`; call `EntityComponent.Resolve`.

## Guards

- Pin: `FootprintCells(` declared only in `GridObjectComponent.cs` and `GridFootprint.cs`; `ClampZ(` only in `GridProjectionComponent.cs`; `ResolveCurrent<` nowhere. Mutation: restore one → fails.
- Existing placement/occupancy smoke (`walkable-build-occupies`, footprint release) stays green.

## Dependencies / collisions

`ecs/grid/` collision — stage per file. Feeds ENH-14 (object-at-cell index uses `Covers`).

## Out of scope

Rotation of footprints (not a feature the grid has today).
