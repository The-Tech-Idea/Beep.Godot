# ENH-08 — `GridProjectionComponent` hot paths: no per-call allocation, one mouse→cell per frame

**Type:** enhancement (per-frame cost) · **Area:** `GridProjectionComponent`, `GridSelectionComponent`, `GridPlacementComponent`, `GridInteractionCursorComponent`, `GridToolActionComponent`, `GridCellOverlayComponent`, `GridRoadComponent`, `TerrainCollisionComponent`, `TerrainMapOverlayComponent`, `Terrain*RendererComponent` · **Status:** **PARTIALLY IMPLEMENTED 2026-09-09** (parts 1-3: span CellCorners + caller migration + surface cache; hover-owner part 4 pending) · **Effort:** S (1 day) · **Risk:** low

## Outcome (parts 1-3, 2026-09-09)

- **Part 1 - the span overload.** `GridProjectionComponent.CellCorners(Vector2I, Span<Vector2>)` fills the caller's buffer and returns the count; the array overload stays as the GDScript/convenience path and is now implemented over it, so the geometry lives once. The two private corner helpers became span-fillers and the editor grid draw reuses one member `Vector2[4]`. The elevated branch stays allocation-free too: `TerrainIsometricRendererComponent.SurfaceCorners` gained matching `Span` overloads. `CellRect` from the design was **not** added - no caller wanted a single-cell AABB (every consumer draws a four-corner polygon), and rule 6 forbids an orphan. Guard: `tests/grid_projection_probe.gd` + `GridProjectionSmoke` assert span/array agreement (top-down + isometric), a sub-four buffer returns 0, and 10000 span fills allocate 0 bytes, with the array overload's own allocation measured as a positive control. Mutation-proven.
- **Part 2 - the callers.** The nine external per-cell callers fill spans: the four grid drawers (overlay, cursor, road, selection) take corners into a stackalloc span before building the `Vector2[]` the Godot draw APIs still need; the three read-only terrain renderers (feature, relief, resource) drop to a span outright; terrain collision keeps the one array `ConvexPolygonShape2D.Points` needs but its three neighbour lookups reuse one hoisted span; the isometric renderer's own surface-extent and feature-stamp callers move to the span `SurfaceCorners`. `TerrainMapOverlayComponent.CellOutline` stays on the array overload by design (it returns a `Vector2[]`).
- **Part 3 - surface cache.** `NativeLayer` and `ElevatedTerrain` were re-resolved with `GetNodeOrNull` on every call (per cell on a native/elevated `CellCorners`); they now cache the resolved node, re-resolving on a path change (the setters clear it) or when the cached node is freed (an `IsInstanceValid` check). `HasSnapTargetPath` stopped allocating a string per frame (`ToString()` -> `NodePath.IsEmpty`). Guard: the probe resolves a `TileMapLayer` projection, frees the layer, and asserts `CellToWorld` re-resolves to non-finite rather than touching the freed node; mutation-proven (dropping the validity check throws).

Verified after each part: `dotnet build` clean (0 warnings); `grid_projection`, `showcase_interaction`, `renderer_reporting`, `grid_terrain_feature`, `grid_terrain_topology`, `grid_terrain_building` green.

### Still pending: part 4 - the hover owner

The one-conversion-per-frame consolidation (finding 3) is the remaining piece.

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
