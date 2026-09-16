# GridProjectionComponent

The shared grid-math foundation for top-down and isometric 2D worlds: `WorldToCell`, `CellToWorld`, `SnapWorld`, per-cell corner geometry, and an optional debug grid drawn directly with `_Draw` (no `TileMap` required). It is a `Node2D` dropped at the map origin; virtually every other grid file in this batch reads through it for the actual coordinate math rather than reimplementing it.

The `Origin` export is documented as local-space, with cell `(0,0)` centered on that point — a deliberate "cell centers, not cell corners, are the anchor" convention that both `CellToWorld` (used for placing/snapping objects) and `CellCorners` (used for drawing/hit-testing) are built around. Isometric cell lookup (`LocalToCell` in the isometric branch) does not use a closed-form inverse: the naive diamond-projection inverse gives a continuous, skewed grid coordinate, so `PickNearestIsometricCell` instead searches the 3×3 neighborhood of the rounded guess and picks whichever candidate cell's center is closest by a half-width/half-height-normalized (diamond) distance — a correctness-over-elegance choice for a projection where naive rounding picks the wrong neighbor near cell edges.

## Public API

- `public enum GridProjection { TopDown, Isometric }`.
- `[Signal] HoverCellChangedEventHandler(int x, int y)` — emitted from `_Process` whenever `TrackMouseCell` is on and the mouse crosses into a new cell.
- `[Export] public GridProjection Projection` / `public Vector2 TileSize` / `public Vector2 Origin` — core grid parameters; each setter triggers `QueueRedraw()` (and `TileSize`/`Projection` also refresh configuration warnings).
- `[ExportGroup("Debug Drawing")] [Export] public bool DrawGrid` / `public int DrawRadius` / `public Color GridColor` / `public Color AxisColor` — controls for the built-in `_Draw` grid visualization.
- `[ExportGroup("Runtime Helpers")] [Export] public bool TrackMouseCell` — enables the `HoverCellChanged` signal.
- `[Export] public bool SnapTarget` / `public NodePath SnapTargetPath` — when both set, the target node's `GlobalPosition` is snapped to the nearest cell center every `_Process` tick.
- `public Vector2 EffectiveTileSize` — the size of one cell in grid-local units, answered by the surface that owns cell geometry. For a bound elevated surface it is `CellSize`; for a bound native layer, `TileSet.TileSize`; each is carried through that surface's transform relative to the grid, so a scaled layer reports the size it draws. Only an unbound grid answers from the `TileSize` export (sanitized: finite, ≥ 1 px, absolute). A binding that does not resolve reports zero (no geometry), the way `CellToWorld` reports NaN. The grid's own unbound math uses the sanitized export privately. (VIEW-02, 2026-09-15: this used to return the export whatever was bound, so a prop's detail cutoff measured a 96×48 layer as 64×64.)
- `public Vector2 EffectiveOrigin` — the sanitized `Origin`.
- `public static bool HasAffineCellRuns(TileSet tiles)` / `public bool CellsFormAffineRuns` — THE rule for whether a block of cells has an exact four-vertex outline taken from its corner cells. It holds for square TileSets and for diamond-down, horizontal-offset isometric ones. The instance form holds for an unbound grid or a bound layer whose TileSet qualifies, and never for an elevated surface. Read by `TerrainCollisionComponent` (merge same-class runs) and `TerrainSurfaceStreamingComponent` (one quad per chunk).
- `public Vector2 CellToWorld(Vector2I cell)` — global-space center of a cell.
- `public Vector2I WorldToCell(Vector2 worldPosition)` — cell under a global-space point; returns a sentinel `(int.MinValue, int.MinValue)` cell for a non-finite input instead of throwing.
- `public Vector2 SnapWorld(Vector2 worldPosition)` — `CellToWorld(WorldToCell(worldPosition))`.
- `public Vector2I MouseCell()` — `WorldToCell` of the current viewport mouse position.
- `public Vector2[] CellCorners(Vector2I cell)` — four local-space corners, isometric-diamond or top-down-rectangle depending on `Projection`.
- `public override void _Ready()` / `_Process(double delta)` / `_GetConfigurationWarnings()` / `_Draw()`.

## Dependencies

- No `NodePath` dependency resolution except `SnapTargetPath`, resolved fresh via `GetNodeOrNull<Node2D>` every `_Process` tick (not cached in a private field the way sibling components cache their resolved dependencies).
- Called into by both `GridCellOverlayComponent` (`CellCorners`, `ToGlobal`/`ToLocal` composition for drawing) and `GridPlacementComponent` (`MouseCell()`, `CellToWorld(Vector2I)`), both confirmed by reading those files in this same batch — this component is the shared math dependency the rest of the grid subsystem is built on.

## Notes

- Unlike every other component in this batch, the `SnapTargetPath` lookup is not cached behind the `_field == null || !GodotObject.IsInstanceValid(_field)` pattern used everywhere else — it re-resolves via `GetNodeOrNull` on every `_Process` call, which is harmless but is a small inconsistency against the caching convention the rest of the grid subsystem follows.
- `_GetConfigurationWarnings` checks `TileSize` validity and a minimum isometric tile width, but does not warn when `SnapTarget` is enabled with an empty `SnapTargetPath` — `HasSnapTargetPath()` just silently short-circuits the per-frame snap in that case, with no signal to the editor that the toggle is doing nothing.
