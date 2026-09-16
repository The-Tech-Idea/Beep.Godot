using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Shared grid math for top-down and isometric 2D worlds.
    ///
    /// Drop this Node2D at the map origin, set Projection/TileSize, then use
    /// WorldToCell, CellToWorld, and SnapWorld from placement, selection, AI job,
    /// or build-preview code. The optional debug drawing makes tile alignment
    /// visible in the editor without requiring a TileMap.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridProjectionComponent : Node2D
    {
        /// <summary>Clamps a z-index into Godot's canvas-item range. The projection owns z-order.</summary>
        public static int ClampZ(int zIndex)
            => zIndex < (int)RenderingServer.CanvasItemZMin ? (int)RenderingServer.CanvasItemZMin
                : zIndex > (int)RenderingServer.CanvasItemZMax ? (int)RenderingServer.CanvasItemZMax : zIndex;

        public enum GridProjection
        {
            TopDown,
            Isometric
        }

        [Signal] public delegate void HoverCellChangedEventHandler(int x, int y);
        [Signal] public delegate void GeometryChangedEventHandler();

        public void NotifyGeometryChanged()
        {
            BindElevatedTerrain();
            QueueRedraw();
            EmitSignal(SignalName.GeometryChanged);
        }

        private GridProjection _projection = GridProjection.TopDown;
        private Vector2 _tileSize = new(64, 64);
        private Vector2 _origin = Vector2.Zero;
        private bool _drawGrid = true;
        private int _drawRadius = 12;
        private Color _gridColor = new(1f, 1f, 1f, 0.18f);
        private Color _axisColor = new(1f, 0.72f, 0.24f, 0.45f);
        private bool _trackMouseCell = true;
        private bool _snapTarget;
        private NodePath _snapTargetPath = new("");
        private Vector2I _hoverCell = new(int.MinValue, int.MinValue);
        private static readonly Vector2I InvalidCell = new(int.MinValue, int.MinValue);
        // Reused by the editor grid draw so each cell outline allocates no corner array.
        private readonly Vector2[] _drawCorners = new Vector2[4];

        /// <summary>Optional native geometry source. Its TileSet layout and transform replace
        /// the manual Projection, TileSize and Origin for placement and picking.</summary>
        private NodePath _tileMapLayerPath = new("");
        [Export] public NodePath TileMapLayerPath
        {
            get => _tileMapLayerPath;
            set { _tileMapLayerPath = value ?? new NodePath(""); _cachedNativeLayer = null; }
        }

        /// <summary>Elevated terrain geometry, taking precedence over the flat native layer.</summary>
        private NodePath _elevatedTerrainPath = new("");
        private TerrainIsometricRendererComponent? _connectedTerrain;
        [Export] public NodePath ElevatedTerrainPath
        {
            get => _elevatedTerrainPath;
            set
            {
                _elevatedTerrainPath = value;
                _cachedElevatedTerrain = null;
                if (IsInsideTree()) BindElevatedTerrain();
            }
        }

        // The resolved surface nodes are cached so the per-cell CellCorners path and the
        // per-frame WorldToCell/CellToWorld do not re-run GetNodeOrNull on every call. The
        // cache re-resolves whenever the path changes (the setters clear it) or the cached
        // node is freed (the validity check below), which covers every way the target moves.
        private TerrainIsometricRendererComponent? _cachedElevatedTerrain;
        private TerrainIsometricRendererComponent? ElevatedTerrain
        {
            get
            {
                if (ElevatedTerrainPath.IsEmpty) return null;
                if (_cachedElevatedTerrain is null || !GodotObject.IsInstanceValid(_cachedElevatedTerrain))
                    _cachedElevatedTerrain = GetNodeOrNull<TerrainIsometricRendererComponent>(ElevatedTerrainPath);
                return _cachedElevatedTerrain;
            }
        }

        private TileMapLayer? _cachedNativeLayer;
        private TileMapLayer? NativeLayer
        {
            get
            {
                if (TileMapLayerPath.IsEmpty) return null;
                if (_cachedNativeLayer is null || !GodotObject.IsInstanceValid(_cachedNativeLayer))
                    _cachedNativeLayer = GetNodeOrNull<TileMapLayer>(TileMapLayerPath);
                return _cachedNativeLayer;
            }
        }

        [Export]
        public GridProjection Projection
        {
            get => _projection;
            set { _projection = value; QueueRedraw(); UpdateConfigurationWarnings(); }
        }

        [Export]
        public Vector2 TileSize
        {
            get => _tileSize;
            set { _tileSize = value; QueueRedraw(); UpdateConfigurationWarnings(); }
        }

        /// <summary>Local-space grid origin. Cell (0,0) is centered on this point.</summary>
        [Export]
        public Vector2 Origin
        {
            get => _origin;
            set { _origin = value; QueueRedraw(); }
        }

        [ExportGroup("Debug Drawing")]
        [Export]
        public bool DrawGrid
        {
            get => _drawGrid;
            set { _drawGrid = value; QueueRedraw(); }
        }

        [Export(PropertyHint.Range, "1,128,1")]
        public int DrawRadius
        {
            get => _drawRadius;
            set { _drawRadius = value; QueueRedraw(); }
        }

        [Export]
        public Color GridColor
        {
            get => _gridColor;
            set { _gridColor = value; QueueRedraw(); }
        }

        [Export]
        public Color AxisColor
        {
            get => _axisColor;
            set { _axisColor = value; QueueRedraw(); }
        }

        [ExportGroup("Runtime Helpers")]
        [Export] public bool TrackMouseCell
        {
            get => _trackMouseCell;
            set => _trackMouseCell = value;
        }

        /// <summary>When enabled, SnapTargetPath is moved to the nearest cell center each frame.</summary>
        [Export] public bool SnapTarget
        {
            get => _snapTarget;
            set => _snapTarget = value;
        }

        [Export] public NodePath SnapTargetPath
        {
            get => _snapTargetPath;
            set => _snapTargetPath = value ?? new NodePath("");
        }

        public override void _Ready()
        {
            BindElevatedTerrain();
            QueueRedraw();
            UpdateConfigurationWarnings();
        }

        private void BindElevatedTerrain()
        {
            var terrain = ElevatedTerrain;
            if (terrain == _connectedTerrain) return;
            DisconnectElevatedTerrain();
            _connectedTerrain = terrain;
            if (_connectedTerrain is not null) _connectedTerrain.SurfaceRebuilt += NotifyGeometryChanged;
        }

        private void DisconnectElevatedTerrain()
        {
            if (_connectedTerrain is not null && GodotObject.IsInstanceValid(_connectedTerrain))
                _connectedTerrain.SurfaceRebuilt -= NotifyGeometryChanged;
            _connectedTerrain = null;
        }

        public override void _ExitTree() => DisconnectElevatedTerrain();

        public override void _Process(double delta)
        {
            if (TrackMouseCell)
            {
                Vector2I cell = WorldToCell(GetGlobalMousePosition());
                if (cell != _hoverCell)
                {
                    _hoverCell = cell;
                    EmitSignal(SignalName.HoverCellChanged, cell.X, cell.Y);
                    if (Engine.IsEditorHint()) QueueRedraw();
                }
            }

            if (SnapTarget && HasSnapTargetPath() && GetNodeOrNull<Node2D>(SnapTargetPath) is { } target)
                target.GlobalPosition = SnapWorld(target.GlobalPosition);
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (!ElevatedTerrainPath.IsEmpty)
                return ElevatedTerrain is null
                    ? new[] { "ElevatedTerrainPath must resolve to a TerrainIsometricRendererComponent." }
                    : System.Array.Empty<string>();
            if (!TileMapLayerPath.IsEmpty)
            {
                if (NativeLayer?.TileSet is not { } tiles)
                    return new[] { "TileMapLayerPath must resolve to a TileMapLayer with a TileSet." };
                if (tiles.TileShape is not (TileSet.TileShapeEnum.Square or TileSet.TileShapeEnum.Isometric))
                    return new[] { "Native grid outlines currently support square and isometric tiles only." };
                return System.Array.Empty<string>();
            }
            if (TileSize.X <= 0f || TileSize.Y <= 0f || !float.IsFinite(TileSize.X) || !float.IsFinite(TileSize.Y))
                return new[] { "TileSize must be greater than zero on both axes." };

            if (Projection == GridProjection.Isometric && TileSize.X < 2f)
                return new[] { "Isometric TileSize.X should be at least 2 pixels so half-width math stays meaningful." };

            return System.Array.Empty<string>();
        }

        /// <summary>
        /// The size of one cell in this grid's local units, answered by the surface that owns
        /// cell geometry: a bound elevated surface's CellSize, else a bound native layer's
        /// TileSet.TileSize, each carried through that surface's transform relative to this grid
        /// so a scaled surface reports the size it draws. The TileSize export answers only for an
        /// unbound grid - it is the no-binding value, not a second opinion. A binding that does
        /// not resolve has no geometry and reports zero, the way CellToWorld reports NaN.
        ///
        /// This used to return the export whatever was bound, while CellToWorld on the same
        /// component answered from the layer, so a grid bound to a 96x48 layer placed props at
        /// 96x48 and measured their detail cutoff at the export's 64x64.
        /// </summary>
        public Vector2 EffectiveTileSize
        {
            get
            {
                if (!ElevatedTerrainPath.IsEmpty)
                    return ElevatedTerrain is { } terrain ? SurfaceCellSize(terrain, terrain.CellSize) : Vector2.Zero;
                if (!TileMapLayerPath.IsEmpty)
                    return NativeLayer is { TileSet: { } tiles } layer ? SurfaceCellSize(layer, tiles.TileSize) : Vector2.Zero;
                return ManualTileSize;
            }
        }

        /// <summary>The TileSize export, bounded: the geometry of an unbound grid only.</summary>
        private Vector2 ManualTileSize => new(
            Mathf.Max(1f, float.IsFinite(TileSize.X) ? Mathf.Abs(TileSize.X) : 64f),
            Mathf.Max(1f, float.IsFinite(TileSize.Y) ? Mathf.Abs(TileSize.Y) : 64f));

        private Vector2 SurfaceCellSize(Node2D surface, Vector2 size)
        {
            // Outside the tree there is no global transform to relate; the surface's own size is all there is.
            if (!IsInsideTree() || !surface.IsInsideTree()) return size.Abs();
            Transform2D relative = GlobalTransform.AffineInverse() * surface.GlobalTransform;
            return new Vector2((relative.X * size.X).Length(), (relative.Y * size.Y).Length());
        }

        /// <summary>
        /// Whether a TileSet's cells tile the plane as affine runs of one quadrilateral, so any
        /// rectangular block of cells has an exact four-vertex outline taken from its corner
        /// cells' corners: top-left of the first, then the matching corner of the last cell
        /// along each axis. True for square TileSets (their layout and offset axis only apply
        /// to half-offset shapes) and for diamond-down, horizontal-offset isometric ones, the
        /// two layouts TerrainTileSets.Create produces. Stacked, staired and diamond-right
        /// isometric layouts are not.
        ///
        /// ONE rule, read by both consumers that need it: terrain collision merges same-class
        /// cell runs into one shape on it, and surface streaming draws one quad per chunk on it.
        /// </summary>
        public static bool HasAffineCellRuns(TileSet tiles)
            => tiles.TileShape == TileSet.TileShapeEnum.Square
                || tiles is { TileShape: TileSet.TileShapeEnum.Isometric, TileLayout: TileSet.TileLayoutEnum.DiamondDown,
                    TileOffsetAxis: TileSet.TileOffsetAxisEnum.Horizontal };

        /// <summary>
        /// Whether this grid's cells form affine runs (see <see cref="HasAffineCellRuns"/>):
        /// an unbound grid does in either manual projection, a bound native layer does when its
        /// TileSet does, and an elevated surface never does - neighbouring cells stand at
        /// different heights, so a run of them has no single flat outline.
        /// </summary>
        public bool CellsFormAffineRuns
            => ElevatedTerrainPath.IsEmpty
                && (TileMapLayerPath.IsEmpty || NativeLayer?.TileSet is { } tiles && HasAffineCellRuns(tiles));

        public Vector2 EffectiveOrigin => new(
            float.IsFinite(Origin.X) ? Origin.X : 0f,
            float.IsFinite(Origin.Y) ? Origin.Y : 0f);

        /// <summary>Returns the global/world-space center of a grid cell.</summary>
        public Vector2 CellToWorld(Vector2I cell)
        {
            if (!ElevatedTerrainPath.IsEmpty)
                return ElevatedTerrain is { } terrain && terrain.ContainsSurfaceCell(cell)
                    && terrain.HasSurface
                    ? terrain.ToGlobal(terrain.SurfacePosition(cell)) : new Vector2(float.NaN, float.NaN);
            if (TileMapLayerPath.IsEmpty) return ToGlobal(CellToLocal(cell));
            return NativeLayer is { TileSet: not null } layer
                ? layer.ToGlobal(layer.MapToLocal(cell)) : new Vector2(float.NaN, float.NaN);
        }

        /// <summary>How many times WorldToCell has run. Test-only instrumentation so a probe can
        /// prove the interaction components share one mouse-&gt;cell conversion per frame (ENH-08).</summary>
        internal long WorldToCellCalls { get; private set; }

        /// <summary>Returns the grid cell under a global/world-space point.</summary>
        public Vector2I WorldToCell(Vector2 worldPosition)
        {
            WorldToCellCalls++;
            if (!float.IsFinite(worldPosition.X) || !float.IsFinite(worldPosition.Y)) return InvalidCell;
            if (!ElevatedTerrainPath.IsEmpty)
                return ElevatedTerrain is { } terrain ? terrain.SurfaceCellAt(terrain.ToLocal(worldPosition)) : InvalidCell;
            if (TileMapLayerPath.IsEmpty) return LocalToCell(ToLocal(worldPosition));
            return NativeLayer is { TileSet: not null } layer
                ? layer.LocalToMap(layer.ToLocal(worldPosition)) : InvalidCell;
        }

        /// <summary>Snaps a global/world-space point to the center of the nearest grid cell.</summary>
        public Vector2 SnapWorld(Vector2 worldPosition)
        {
            Vector2I cell = WorldToCell(worldPosition);
            if (cell == InvalidCell) return worldPosition;
            Vector2 snapped = CellToWorld(cell);
            return snapped.IsFinite() ? snapped : worldPosition;
        }

        /// <summary>Returns the current mouse cell using the active viewport mouse position.</summary>
        public Vector2I MouseCell() => WorldToCell(GetGlobalMousePosition());

        /// <summary>The inclusive cell rectangle currently visible in the viewport, expanded by
        /// <paramref name="margin"/> cells, so a per-cell drawer can cull to the camera. Returns
        /// false (draw everything) when there is no viewport or a corner does not resolve to a cell
        /// (an off-surface elevated corner), so culling never hides a cell it cannot place.</summary>
        public bool TryGetVisibleCellRect(out Rect2I rect, int margin = 1)
        {
            rect = default;
            if (GetViewport() is not { } viewport) return false;

            Transform2D screenToWorld = viewport.GetCanvasTransform().AffineInverse();
            Rect2 screen = viewport.GetVisibleRect();
            Vector2I c0 = WorldToCell(screenToWorld * screen.Position);
            Vector2I c1 = WorldToCell(screenToWorld * new Vector2(screen.End.X, screen.Position.Y));
            Vector2I c2 = WorldToCell(screenToWorld * screen.End);
            Vector2I c3 = WorldToCell(screenToWorld * new Vector2(screen.Position.X, screen.End.Y));
            if (c0 == InvalidCell || c1 == InvalidCell || c2 == InvalidCell || c3 == InvalidCell)
                return false;

            int minX = Mathf.Min(Mathf.Min(c0.X, c1.X), Mathf.Min(c2.X, c3.X)) - margin;
            int minY = Mathf.Min(Mathf.Min(c0.Y, c1.Y), Mathf.Min(c2.Y, c3.Y)) - margin;
            int maxX = Mathf.Max(Mathf.Max(c0.X, c1.X), Mathf.Max(c2.X, c3.X)) + margin;
            int maxY = Mathf.Max(Mathf.Max(c0.Y, c1.Y), Mathf.Max(c2.Y, c3.Y)) + margin;
            rect = new Rect2I(minX, minY, maxX - minX + 1, maxY - minY + 1);
            return true;
        }

        /// <summary>Returns local-space corners for drawing or hit previews. Convenience path over
        /// <see cref="CellCorners(Vector2I, System.Span{Vector2})"/> for GDScript and callers that hand a
        /// Vector2[] straight to a Godot draw/shape API; the span overload allocates nothing.</summary>
        public Vector2[] CellCorners(Vector2I cell)
        {
            System.Span<Vector2> corners = stackalloc Vector2[4];
            int n = CellCorners(cell, corners);
            if (n == 0) return System.Array.Empty<Vector2>();
            var result = new Vector2[n];
            for (int i = 0; i < n; i++) result[i] = corners[i];
            return result;
        }

        /// <summary>Fills up to four local-space cell corners into <paramref name="corners"/> (which must
        /// hold at least four) and returns the count written - 0 when the cell has no drawable face,
        /// otherwise 4. Allocation-free, so a hot per-cell caller avoids a per-call array.</summary>
        public int CellCorners(Vector2I cell, System.Span<Vector2> corners)
        {
            if (corners.Length < 4) return 0;

            if (!ElevatedTerrainPath.IsEmpty)
            {
                if (ElevatedTerrain is not { } terrain) return 0;
                int n = terrain.SurfaceCorners(cell, corners);
                for (int i = 0; i < n; i++) corners[i] = ToLocal(terrain.ToGlobal(corners[i]));
                return n;
            }
            if (!TileMapLayerPath.IsEmpty)
            {
                if (NativeLayer is not { TileSet: { } tiles } layer) return 0;
                Vector2 half = (Vector2)tiles.TileSize * 0.5f;
                switch (tiles.TileShape)
                {
                    case TileSet.TileShapeEnum.Square:
                        corners[0] = -half; corners[1] = new Vector2(half.X, -half.Y);
                        corners[2] = half; corners[3] = new Vector2(-half.X, half.Y);
                        break;
                    case TileSet.TileShapeEnum.Isometric:
                        corners[0] = new Vector2(0, -half.Y); corners[1] = new Vector2(half.X, 0);
                        corners[2] = new Vector2(0, half.Y); corners[3] = new Vector2(-half.X, 0);
                        break;
                    default:
                        return 0;
                }
                Vector2 center = layer.MapToLocal(cell);
                for (int i = 0; i < 4; i++) corners[i] = ToLocal(layer.ToGlobal(center + corners[i]));
                return 4;
            }
            if (Projection == GridProjection.Isometric)
                IsometricCellCorners(CellToLocal(cell), corners);
            else
                TopDownCellCorners(cell, corners);
            return 4;
        }

        public override void _Draw()
        {
            if (!DrawGrid) return;

            int radius = Mathf.Clamp(DrawRadius, 1, 128);
            if (!TileMapLayerPath.IsEmpty || !ElevatedTerrainPath.IsEmpty)
            {
                for (int x = -radius; x <= radius; x++)
                    for (int y = -radius; y <= radius; y++)
                    {
                        int n = CellCorners(new Vector2I(x, y), _drawCorners);
                        if (n < 3) continue;
                        for (int i = 0; i < n; i++)
                            DrawLine(_drawCorners[i], _drawCorners[(i + 1) % n], GridColor);
                    }
                return;
            }
            if (Projection == GridProjection.Isometric)
                DrawIsometricGrid(radius);
            else
                DrawTopDownGrid(radius);
        }

        private Vector2 CellToLocal(Vector2I cell)
        {
            Vector2 origin = EffectiveOrigin;
            Vector2 tileSize = ManualTileSize;
            return Projection == GridProjection.Isometric
                ? origin + new Vector2((cell.X - cell.Y) * HalfWidth, (cell.X + cell.Y) * HalfHeight)
                : origin + new Vector2((cell.X + 0.5f) * tileSize.X, (cell.Y + 0.5f) * tileSize.Y);
        }

        private Vector2I LocalToCell(Vector2 localPosition)
        {
            if (!float.IsFinite(localPosition.X) || !float.IsFinite(localPosition.Y))
                return InvalidCell;

            Vector2 origin = EffectiveOrigin;
            Vector2 tileSize = ManualTileSize;
            Vector2 p = localPosition - origin;
            if (Projection == GridProjection.TopDown)
            {
                return new Vector2I(
                    Mathf.FloorToInt(p.X / tileSize.X),
                    Mathf.FloorToInt(p.Y / tileSize.Y));
            }

            float x = (p.Y / HalfHeight + p.X / HalfWidth) * 0.5f;
            float y = (p.Y / HalfHeight - p.X / HalfWidth) * 0.5f;
            return PickNearestIsometricCell(p, new Vector2(x, y));
        }

        private Vector2I PickNearestIsometricCell(Vector2 localOffset, Vector2 gridPoint)
        {
            var nearest = new Vector2I(Mathf.RoundToInt(gridPoint.X), Mathf.RoundToInt(gridPoint.Y));
            float best = float.MaxValue;
            var bestCell = nearest;

            for (int x = nearest.X - 1; x <= nearest.X + 1; x++)
            {
                for (int y = nearest.Y - 1; y <= nearest.Y + 1; y++)
                {
                    var cell = new Vector2I(x, y);
                    Vector2 center = CellToLocal(cell) - Origin;
                    float score = Mathf.Abs((localOffset.X - center.X) / HalfWidth)
                                + Mathf.Abs((localOffset.Y - center.Y) / HalfHeight);
                    if (score < best)
                    {
                        best = score;
                        bestCell = cell;
                    }
                }
            }

            return bestCell;
        }

        private void TopDownCellCorners(Vector2I cell, System.Span<Vector2> corners)
        {
            Vector2 tileSize = ManualTileSize;
            Vector2 topLeft = EffectiveOrigin + new Vector2(cell.X * tileSize.X, cell.Y * tileSize.Y);
            corners[0] = topLeft;
            corners[1] = topLeft + new Vector2(tileSize.X, 0f);
            corners[2] = topLeft + tileSize;
            corners[3] = topLeft + new Vector2(0f, tileSize.Y);
        }

        private void IsometricCellCorners(Vector2 center, System.Span<Vector2> corners)
        {
            corners[0] = center + new Vector2(0f, -HalfHeight);
            corners[1] = center + new Vector2(HalfWidth, 0f);
            corners[2] = center + new Vector2(0f, HalfHeight);
            corners[3] = center + new Vector2(-HalfWidth, 0f);
        }

        private void DrawTopDownGrid(int radius)
        {
            Vector2 origin = EffectiveOrigin;
            Vector2 tileSize = ManualTileSize;
            for (int x = -radius; x <= radius; x++)
            {
                DrawLine(
                    origin + new Vector2(x * tileSize.X, -radius * tileSize.Y),
                    origin + new Vector2(x * tileSize.X, (radius + 1) * tileSize.Y),
                    x == 0 ? AxisColor : GridColor);
            }

            for (int y = -radius; y <= radius; y++)
            {
                DrawLine(
                    origin + new Vector2(-radius * tileSize.X, y * tileSize.Y),
                    origin + new Vector2((radius + 1) * tileSize.X, y * tileSize.Y),
                    y == 0 ? AxisColor : GridColor);
            }

            if (_hoverCell.X != int.MinValue && CellCorners(_hoverCell, _drawCorners) >= 3)
                DrawPolyline(_drawCorners, Colors.White with { A = 0.7f }, 2f, true);
        }

        private void DrawIsometricGrid(int radius)
        {
            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    var cell = new Vector2I(x, y);
                    var color = x == 0 || y == 0 ? AxisColor : GridColor;
                    if (CellCorners(cell, _drawCorners) >= 3)
                        DrawPolyline(_drawCorners, color, 1f, true);
                }
            }

            if (_hoverCell.X != int.MinValue && CellCorners(_hoverCell, _drawCorners) >= 3)
                DrawPolyline(_drawCorners, Colors.White with { A = 0.7f }, 2f, true);
        }

        // IsEmpty rather than ToString(), which allocated a string every frame SnapTarget ran.
        private bool HasSnapTargetPath() => !SnapTargetPath.IsEmpty;

        private float HalfWidth => Mathf.Max(1f, ManualTileSize.X * 0.5f);
        private float HalfHeight => Mathf.Max(1f, ManualTileSize.Y * 0.5f);
    }
}
