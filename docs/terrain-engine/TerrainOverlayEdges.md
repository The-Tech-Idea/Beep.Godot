# TerrainOverlayEdges

Support utility: a stateless `internal static class` that finds the borders of labelled regions on a cell grid, for overlays (FEAT-09). Not a stage or a component.

A border is every side where a cell's id differs from its +x or +y neighbour's, reported once per non-zero side. A region drawn from these edges is outlined in its own colour without the walk knowing the projection: the caller turns each (cell, neighbour) pair into the side their two cell polygons share. `TerrainMapOverlayComponent` uses it for start-area borders and, over a footprint rectangle, for headquarters outlines. Its doc comment names a territory border as the same walk over another id.

## Public API

- `internal readonly record struct Edge(Vector2I Cell, Vector2I Neighbour, int Id)` — one side of region `Id`, between its `Cell` and the outside `Neighbour`.
- `static List<Edge> Collect(Rect2I bounds, Func<Vector2I, int> idAt)` — walks from one row and one column before `bounds` to its end, comparing each cell with its right and down neighbours. Cells outside `bounds` read as 0, so a region touching the edge of the bounds is closed there. Where the two ids differ, each non-zero side is added as its own edge, so a border between two regions yields one edge for each.
- `static bool SharedSide(Vector2[] cell, Vector2[] neighbour, out Vector2 from, out Vector2 to)` — the side two neighbouring cell polygons share, found as two matching corners within a tolerance of a hundredth of the cell's first side (compared squared). False, with both points zero, when they share fewer than two corners (a projection whose neighbours touch at a point only) or `cell` has fewer than two corners.

## Dependencies

- No dependency on other terrain files; uses Godot's `Rect2I`, `Vector2I` and `Vector2`.
- Called by `TerrainMapOverlayComponent.BakeStartAreas` and its `AddSegment` helper, which pass `GeneratedTerrainField.StartAreaAtCell` (offset by the overlay's `BoundsOrigin`) as `idAt` and the overlay's own `CellOutline` polygons to `SharedSide`.

## Notes

- `Collect` allocates one list per call. The overlay calls it once for the map and once per start report during `Rebuild`, never from `_Draw`.
- `tests/terrain_start_area_play_probe.gd` requires the overlay's segment count to equal every area border side plus every headquarters footprint perimeter, which fails if either walk drops or duplicates a side.
