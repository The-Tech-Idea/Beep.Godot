# DUP-08 — Footprint, Z-clamp and current-instance helpers have one owner each

**Type:** duplication fix · **Area:** `GridPlacementComponent`, `GridObjectComponent`, `GridExtractorComponent`, `GridResourceScatterComponent`, `GridToolActionComponent`, `GridObjectInspectorComponent` · **Status:** **IMPLEMENTED 2026-09-09** (footprint + ClampZ + ResolveCurrent) · **Effort:** XS (½ day) · **Risk:** low

## Outcome (footprint + ClampZ + ResolveCurrent)

The footprint double-loop has one owner now. `GridObjectComponent` answers for its own cells - `FootprintCells()` (public, delegating to the new enumerator) and `Covers(cell)` - and `GridFootprint.Cells(origin, size)` serves the caller that has a size but no object yet, the placement preview. The four copies are gone: the placement component enumerates through `GridFootprint.Cells(anchor, EffectiveFootprint)`, the extractor through `ResolveGridObject()?.FootprintCells() ?? GridFootprint.SingleUnder(GetParent(), _grid)`, and the inspector's inverse `CoversCell` is `gridObject.Covers(cell)`. Each caller keeps its own size rule - the object and extractor clamp to at least 1x1, the placement preview takes the effective footprint as given - because the enumerator takes the size as passed. `Covers` is what ENH-14's object-at-cell index will read.

`ClampZ` - two byte-identical copies on the placement and scatter components - moved to `GridProjectionComponent.ClampZ`, which already owns z-order (`SetZIndexFromY`).

Verified: `dotnet build` clean, zero warnings; six probes across placement, building, worker effects, scatter, live resource view and economy green - a behaviour-preserving move stays invisible to them; a scan pin keeps `FootprintCells` to `GridObjectComponent`/`GridFootprint`, `ClampZ` to `GridProjectionComponent`, and `CoversCell` nowhere, and the mutation (re-adding a `FootprintCells` copy) trips it.

**Outcome: ResolveCurrent (2026-09-09).** The two identical `ResolveCurrent<T>` copies are gone. Because the difference from `EntityComponent.Resolve` is real - `ResolveCurrent` re-resolves a non-empty path every call so a live re-point is picked up, while `Resolve` returns the still-valid cache and would keep the stale node - the consolidation is a NEW sibling, `EntityComponent.ResolveLive`, not a fold into `Resolve`. Both `GridPlacementComponent` (4 calls) and `GridToolActionComponent` (9 calls) call it. The two components are a `Node2D` and a `Node`, not `EntityComponent` subclasses, so `ResolveLive` is the static, owner-taking overload, mirroring `Resolve`; no protected instance overload was added because nothing consumes it (rule 6).

`ComponentLookupSmoke` gains the live-re-point guard the plan asked for: `ResolveLive` from path A then path B returns B, while `Resolve` from A then B keeps A - proving both that the re-point is followed and that `ResolveLive`, not `Resolve`, is the variant that does it. Mutation-proven: making `ResolveLive` delegate to the caching `Resolve` fails the probe ("ResolveLive did not follow the re-pointed path"). A scan pin forbids any `ResolveCurrent<` copy under `ecs/grid`. Build clean; the placement, build-approach and building probes stay green.

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
