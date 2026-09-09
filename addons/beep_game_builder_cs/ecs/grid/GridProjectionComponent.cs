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

        /// <summary>Optional native geometry source. Its TileSet layout and transform replace
        /// the manual Projection, TileSize and Origin for placement and picking.</summary>
        [Export] public NodePath TileMapLayerPath { get; set; } = new("");

        /// <summary>Elevated terrain geometry, taking precedence over the flat native layer.</summary>
        private NodePath _elevatedTerrainPath = new("");
        private TerrainIsometricRendererComponent? _connectedTerrain;
        [Export] public NodePath ElevatedTerrainPath
        {
            get => _elevatedTerrainPath;
            set
            {
                _elevatedTerrainPath = value;
                if (IsInsideTree()) BindElevatedTerrain();
            }
        }
        private TerrainIsometricRendererComponent? ElevatedTerrain => ElevatedTerrainPath.IsEmpty
            ? null : GetNodeOrNull<TerrainIsometricRendererComponent>(ElevatedTerrainPath);

        private TileMapLayer? NativeLayer => TileMapLayerPath.IsEmpty
            ? null : GetNodeOrNull<TileMapLayer>(TileMapLayerPath);

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

        public Vector2 EffectiveTileSize => new(
            Mathf.Max(1f, float.IsFinite(TileSize.X) ? Mathf.Abs(TileSize.X) : 64f),
            Mathf.Max(1f, float.IsFinite(TileSize.Y) ? Mathf.Abs(TileSize.Y) : 64f));

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

        /// <summary>Returns the grid cell under a global/world-space point.</summary>
        public Vector2I WorldToCell(Vector2 worldPosition)
        {
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

        /// <summary>Returns local-space corners for drawing or hit previews.</summary>
        public Vector2[] CellCorners(Vector2I cell)
        {
            if (!ElevatedTerrainPath.IsEmpty)
            {
                if (ElevatedTerrain is not { } terrain) return System.Array.Empty<Vector2>();
                Vector2[] corners = terrain.SurfaceCorners(cell);
                for (int i = 0; i < corners.Length; i++) corners[i] = ToLocal(terrain.ToGlobal(corners[i]));
                return corners;
            }
            if (!TileMapLayerPath.IsEmpty)
            {
                if (NativeLayer is not { TileSet: { } tiles } layer) return System.Array.Empty<Vector2>();
                Vector2 half = (Vector2)tiles.TileSize * 0.5f;
                Vector2[] offsets = tiles.TileShape switch
                {
                    TileSet.TileShapeEnum.Square => new[] { -half, new Vector2(half.X, -half.Y), half, new Vector2(-half.X, half.Y) },
                    TileSet.TileShapeEnum.Isometric => new[] { new Vector2(0, -half.Y), new Vector2(half.X, 0), new Vector2(0, half.Y), new Vector2(-half.X, 0) },
                    _ => System.Array.Empty<Vector2>()
                };
                Vector2 center = layer.MapToLocal(cell);
                for (int i = 0; i < offsets.Length; i++)
                    offsets[i] = ToLocal(layer.ToGlobal(center + offsets[i]));
                return offsets;
            }
            return Projection == GridProjection.Isometric
                ? IsometricCellCorners(CellToLocal(cell))
                : TopDownCellCorners(cell);
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
                        Vector2[] corners = CellCorners(new Vector2I(x, y));
                        if (corners.Length < 3) continue;
                        for (int i = 0; i < corners.Length; i++)
                            DrawLine(corners[i], corners[(i + 1) % corners.Length], GridColor);
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
            Vector2 tileSize = EffectiveTileSize;
            return Projection == GridProjection.Isometric
                ? origin + new Vector2((cell.X - cell.Y) * HalfWidth, (cell.X + cell.Y) * HalfHeight)
                : origin + new Vector2((cell.X + 0.5f) * tileSize.X, (cell.Y + 0.5f) * tileSize.Y);
        }

        private Vector2I LocalToCell(Vector2 localPosition)
        {
            if (!float.IsFinite(localPosition.X) || !float.IsFinite(localPosition.Y))
                return InvalidCell;

            Vector2 origin = EffectiveOrigin;
            Vector2 tileSize = EffectiveTileSize;
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

        private Vector2[] TopDownCellCorners(Vector2I cell)
        {
            Vector2 tileSize = EffectiveTileSize;
            Vector2 topLeft = EffectiveOrigin + new Vector2(cell.X * tileSize.X, cell.Y * tileSize.Y);
            return new[]
            {
                topLeft,
                topLeft + new Vector2(tileSize.X, 0f),
                topLeft + tileSize,
                topLeft + new Vector2(0f, tileSize.Y)
            };
        }

        private Vector2[] IsometricCellCorners(Vector2 center)
        {
            return new[]
            {
                center + new Vector2(0f, -HalfHeight),
                center + new Vector2(HalfWidth, 0f),
                center + new Vector2(0f, HalfHeight),
                center + new Vector2(-HalfWidth, 0f)
            };
        }

        private void DrawTopDownGrid(int radius)
        {
            Vector2 origin = EffectiveOrigin;
            Vector2 tileSize = EffectiveTileSize;
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

            if (_hoverCell.X != int.MinValue)
                DrawPolyline(CellCorners(_hoverCell), Colors.White with { A = 0.7f }, 2f, true);
        }

        private void DrawIsometricGrid(int radius)
        {
            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    var cell = new Vector2I(x, y);
                    var color = x == 0 || y == 0 ? AxisColor : GridColor;
                    DrawPolyline(CellCorners(cell), color, 1f, true);
                }
            }

            if (_hoverCell.X != int.MinValue)
                DrawPolyline(CellCorners(_hoverCell), Colors.White with { A = 0.7f }, 2f, true);
        }

        private bool HasSnapTargetPath() => !string.IsNullOrEmpty(SnapTargetPath?.ToString());

        private float HalfWidth => Mathf.Max(1f, EffectiveTileSize.X * 0.5f);
        private float HalfHeight => Mathf.Max(1f, EffectiveTileSize.Y * 0.5f);
    }
}
