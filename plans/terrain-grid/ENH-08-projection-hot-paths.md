# ENH-08 — `GridProjectionComponent` hot paths: no per-call allocation, one mouse→cell per frame

**Type:** enhancement (per-frame cost) · **Area:** `GridProjectionComponent`, `GridSelectionComponent`, `GridPlacementComponent`, `GridInteractionCursorComponent`, `GridToolActionComponent`, `GridCellOverlayComponent`, `GridRoadComponent`, `TerrainCollisionComponent`, `TerrainMapOverlayComponent`, `Terrain*RendererComponent` · **Status:** proposed 2026-09-08 · **Effort:** S (1 day) · **Risk:** low

## Finding

1. **`CellCorners` allocates.** `GridProjectionComponent.CellCorners(cell)` returns `new Vector2[4]` per call (9 internal sites; 10 external callers: overlay, cursor, road, selection, `TerrainCollisionComponent` ×4, feature/relief/resource renderers, map overlay). The overlay calls it per stored cell per redraw; the collision component per cell per chunk build.
2. **`ResolveSurface` per call.** `WorldToCell`/`CellToWorld` re-resolve the snap target (`HasSnapTargetPath`, 2 sites) and re-read the TileMapLayer's `TileSet` / transform on every call rather than caching per frame or on change.
3. **Three to four mouse→cell conversions per frame.** `GridSelectionComponent`, `GridPlacementComponent`, `GridInteractionCursorComponent` and (in tool mode) `GridToolActionComponent` each call `GetGlobalMousePosition()` → `WorldToCell` in their own `_Process`/`_UnhandledInput`, then each compares against its own `_lastHover`. `GridInteractionModeComponent` is the router that decides which of them is active, yet each computes the hover independently. The same divergence produced the "one owner of hover" issue in the UI-kit review.

## Design

- `CellCorners(Vector2I cell, Span<Vector2> corners)` (fills, no allocation) beside a `Rect2 CellRect(cell)` for the square case; keep the array-returning overload only for GDScript callers (marked as the convenience path).
- Cache the resolved surface (`TileSet`, tile size, shape, layer transform) in a `SurfaceState` struct refreshed when `SnapTargetPath` changes or the target emits `Changed`; `_Process` does not re-resolve.
- **Hover owner:** `GridInteractionModeComponent` (already the input router) computes `HoverCell` once per frame from the projection and publishes `HoverCellChanged(x, y)`; selection, placement, cursor and tools read `Interaction.HoverCell` and drop their own conversions and `_lastHover` fields. `GridSelectionComponent.HoverCellChanged` (consumed by `GridInteractionStatusComponent`) forwards from the interaction component so the HUD contract is unchanged.

## Guards (fail first)

- Allocation probe: 10 000 `CellCorners(span)` calls allocate 0 bytes (`GC.GetAllocatedBytesForCurrentThread`). Mutation: call the array overload → allocates.
- Probe: with selection + placement + cursor + tools active, count `WorldToCell` calls per frame via an internal counter → exactly 1. Mutation: restore one component's own conversion → 2.
- Existing interaction smoke (`HoverCell`, placement validity, tool apply) stays green.

## Dependencies / collisions

`ecs/grid/` — interaction components; coordinate. Independent of streaming.

## Out of scope

Isometric math correctness (already probed), camera controller.
