using Godot;
using System.Collections.Generic;

namespace Beep.ECS;

/// <summary>
/// Camera-driven geometry residency for the existing uniform shader surface.
/// Does not generate terrain, change live cells, or suspend simulation.
///
/// The surface shaders read only VERTEX (see <see cref="TerrainShaderSurface"/>), so
/// what they draw on may be any geometry that covers the map from the right origin.
/// Below 65,536 cells that geometry is the layer's own tiles. Above it, this component
/// draws ONE QUAD PER RESIDENT CHUNK into a single Polygon2D child of the layer, and one
/// quad for the whole map once the camera is far enough out that a cell is a few pixels
/// wide. It places no tiles at all.
///
/// It used to place one tile per resident cell under a shared 2,048-cell-per-frame
/// budget, and that budget was the defect the camera exposed. Measured on a 1024x1024
/// surface: a one-chunk-per-frame pan left 4,096 of 16,384 wanted cells resident,
/// because retirement ran first and spent the budget erasing the trailing edge while the
/// leading edge stayed blank, and a camera jump took eight frames to fill. The mutations
/// themselves cost about 0.5 ms a frame and did not change with the rendering quadrant
/// size. Per-chunk quads make residency exact on every update: a jump is covered the
/// same frame, there is no budget, nothing pending, and no interval where the overview
/// has gone and the detail has not arrived.
/// </summary>
[GlobalClass]
public partial class TerrainSurfaceStreamingComponent : Node
{
    [Export(PropertyHint.Range, "8,128,1")] public int ChunkSize { get; set; } = 32;
    [Export(PropertyHint.Range, "0,4,1")] public int PreloadChunks { get; set; } = 1;
    [Export] public bool EnableOverview { get; set; } = true;
    [Export(PropertyHint.Range, "1,16,0.5")] public float OverviewCellPixels { get; set; } = 4;
    /// <summary>True while the whole map is drawn as one quad instead of per-chunk quads.</summary>
    public bool IsOverviewVisible { get; private set; }
    public bool CanSupplyOverview(Viewport viewport, Rect2I bounds)
        => IsOverviewVisible && GodotObject.IsInstanceValid(_geometry) && _geometry!.IsVisibleInTree()
            && GodotObject.IsInstanceValid(_layer) && _layer!.GetViewport() == viewport
            && bounds.Position == Vector2I.Zero && bounds.Size == _size;
    [Signal] public delegate void ResidencyChangedEventHandler(int residentCells, int residentChunks);
    /// <summary>Cells covered by resident chunk quads. Zero while the overview is shown or the layer is hidden.</summary>
    public int ResidentCellCount { get; private set; }
    public int ResidentChunkCount => _resident.Count;
    private const string GeometryName = "SurfaceGeometry";
    private TileMapLayer? _layer;
    private Vector2I _size;
    private int _chunkSize;
    private ulong _configuredTileSetId;
    private Polygon2D? _geometry;
    private readonly HashSet<Vector2I> _resident = new();
    private readonly HashSet<Vector2I> _wanted = new();

    internal void EnsureConfigured(Vector2I size)
    {
        var layer = GetParent() as TileMapLayer;
        if (layer == _layer && layer?.TileSet is { } tiles && _size == size
            && _chunkSize == Mathf.Clamp(ChunkSize, 8, 128) && _configuredTileSetId == tiles.GetInstanceId()) return;
        Configure(size);
    }

    public void Configure(Vector2I size)
    {
        _layer = GetParent() as TileMapLayer;
        _chunkSize = 0;
        _resident.Clear(); _wanted.Clear();
        ResidentCellCount = 0;
        IsOverviewVisible = false;
        if (GodotObject.IsInstanceValid(_geometry)) _geometry!.Free();
        _geometry = null;
        if (_layer?.TileSet is null || size.X <= 0 || size.Y <= 0 || size.X > 32767 || size.Y > 32767)
        {
            GD.PushError("Terrain surface streaming requires a TileMapLayer with a TileSet and finite positive bounds below 32768 cells.");
            SetProcess(false);
            return;
        }
        if (!GridProjectionComponent.HasAffineCellRuns(_layer.TileSet))
        {
            GD.PushError($"Terrain surface streaming draws chunk quads for square TileSets and diamond-down, horizontal-offset isometric TileSets; the TileSet on '{_layer.Name}' is {_layer.TileSet.TileShape}/{_layer.TileSet.TileLayout}/{_layer.TileSet.TileOffsetAxis}, whose chunks have no four-vertex outline.");
            SetProcess(false);
            return;
        }
        _size = size;
        _configuredTileSetId = _layer.TileSet.GetInstanceId();
        _chunkSize = Mathf.Clamp(ChunkSize, 8, 128);
        // A surface that grew past the streaming threshold still holds the tiles Fill placed.
        _layer.Clear();
        SetProcess(true);
    }

    public override void _Process(double delta) => UpdateResidency();

    /// <summary>
    /// Recomputes which chunks the camera can see and rebuilds the geometry when that set,
    /// or the overview decision, changed. Exact on every call: there is nothing to spread
    /// over frames because a chunk costs four vertices, not a thousand tile writes.
    /// </summary>
    public void UpdateResidency()
    {
        if (_layer is null || !GodotObject.IsInstanceValid(_layer) || _chunkSize == 0) return;
        _wanted.Clear();
        bool overview = false;
        if (_layer.IsVisibleInTree())
        {
            Transform2D transform = _layer.GetGlobalTransformWithCanvas();
            if (EnableOverview)
            {
                Vector2 tile = _layer.TileSet!.TileSize;
                float pixels = Mathf.Max((transform.X * tile.X).Length(), (transform.Y * tile.Y).Length());
                float threshold = float.IsFinite(OverviewCellPixels) ? Mathf.Clamp(OverviewCellPixels, 1, 16) : 4;
                // Once shown, the overview holds until a cell is 1.5x the threshold, so a camera
                // resting near the threshold does not flip between the two every frame.
                overview = pixels <= threshold * (IsOverviewVisible ? 1.5f : 1);
            }
            if (!overview) CollectWantedChunks(transform.AffineInverse());
        }
        bool changed = overview != IsOverviewVisible || !_wanted.SetEquals(_resident);
        IsOverviewVisible = overview;
        if (!changed)
        {
            // The material is the layer's; a scene may swap it at runtime.
            if (_geometry is not null && _geometry.Material != _layer.Material) _geometry.Material = _layer.Material;
            return;
        }
        _resident.Clear();
        int cells = 0;
        foreach (Vector2I chunk in _wanted)
        {
            _resident.Add(chunk);
            Vector2I extent = ChunkExtent(chunk);
            cells += extent.X * extent.Y;
        }
        ResidentCellCount = cells;
        RebuildGeometry();
        EmitSignal(SignalName.ResidencyChanged, ResidentCellCount, _resident.Count);
    }

    /// <summary>
    /// The chunks whose cells the viewport's four corners span, plus the preload margin,
    /// clamped to the map. The corners' bounding box in cell space is conservative under
    /// rotation and for isometric layouts; the excess is a few quads, not a few thousand tiles.
    /// </summary>
    private void CollectWantedChunks(Transform2D inverse)
    {
        Rect2 viewport = _layer!.GetViewportRect();
        Vector2I min = new(int.MaxValue, int.MaxValue), max = new(int.MinValue, int.MinValue);
        foreach (Vector2 corner in new[] { viewport.Position, new Vector2(viewport.End.X, viewport.Position.Y),
            viewport.End, new Vector2(viewport.Position.X, viewport.End.Y) })
        {
            Vector2I cell = _layer.LocalToMap(inverse * corner);
            min = min.Min(cell); max = max.Max(cell);
        }
        int margin = Mathf.Clamp(PreloadChunks, 0, 4);
        Vector2I last = new((_size.X - 1) / _chunkSize, (_size.Y - 1) / _chunkSize);
        Vector2I start = new(Mathf.FloorToInt((float)min.X / _chunkSize) - margin, Mathf.FloorToInt((float)min.Y / _chunkSize) - margin);
        Vector2I end = new(Mathf.FloorToInt((float)max.X / _chunkSize) + margin, Mathf.FloorToInt((float)max.Y / _chunkSize) + margin);
        start = start.Max(Vector2I.Zero);
        end = end.Min(last);
        for (int y = start.Y; y <= end.Y; y++)
            for (int x = start.X; x <= end.X; x++) _wanted.Add(new(x, y));
    }

    private void RebuildGeometry()
    {
        if (!IsOverviewVisible && _resident.Count == 0)
        {
            if (_geometry is not null)
            {
                _geometry.Polygons = new Godot.Collections.Array();
                _geometry.Polygon = System.Array.Empty<Vector2>();
            }
            return;
        }
        if (_geometry is null)
        {
            // At the first cell centre, the origin a one-quadrant TileMapLayer hands its shader,
            // so VERTEX reads the same whether tiles or these quads carry the material.
            _geometry = new Polygon2D { Name = GeometryName, Position = _layer!.MapToLocal(Vector2I.Zero) };
            _layer.AddChild(_geometry);
        }
        _geometry.Material = _layer!.Material;
        if (IsOverviewVisible)
        {
            _geometry.Polygons = new Godot.Collections.Array();
            _geometry.Polygon = ChunkOutline(Vector2I.Zero, _size);
            return;
        }
        var vertices = new Vector2[_resident.Count * 4];
        var polygons = new Godot.Collections.Array();
        int next = 0;
        foreach (Vector2I chunk in _resident)
        {
            ChunkOutline(chunk * _chunkSize, ChunkExtent(chunk)).CopyTo(vertices, next);
            polygons.Add(new[] { next, next + 1, next + 2, next + 3 });
            next += 4;
        }
        // Both arrays are read together at draw time, after this method has returned.
        _geometry.Polygon = vertices;
        _geometry.Polygons = polygons;
    }

    /// <summary>
    /// The outline of a block of cells in geometry-local pixels: a rectangle for square
    /// TileSets, a diamond for diamond-down isometric ones. Built from the corner cells'
    /// native MapToLocal centres pushed out by half a tile, so adjacent chunks share their
    /// edges exactly and the union of every chunk is the whole-map outline the overview
    /// draws - the same call with a zero origin and the map's extent.
    /// </summary>
    private Vector2[] ChunkOutline(Vector2I origin, Vector2I extent)
    {
        Vector2 half = (Vector2)_layer!.TileSet!.TileSize * 0.5f;
        Vector2 zero = _layer.MapToLocal(Vector2I.Zero);
        Vector2I far = origin + extent - Vector2I.One;
        if (_layer.TileSet.TileShape == TileSet.TileShapeEnum.Isometric)
            return new[]
            {
                _layer.MapToLocal(origin) - zero + new Vector2(0, -half.Y),
                _layer.MapToLocal(new Vector2I(far.X, origin.Y)) - zero + new Vector2(half.X, 0),
                _layer.MapToLocal(far) - zero + new Vector2(0, half.Y),
                _layer.MapToLocal(new Vector2I(origin.X, far.Y)) - zero + new Vector2(-half.X, 0),
            };
        Vector2 a = _layer.MapToLocal(origin) - zero - half, b = _layer.MapToLocal(far) - zero + half;
        return new[] { a, new Vector2(b.X, a.Y), b, new Vector2(a.X, b.Y) };
    }

    private Vector2I ChunkExtent(Vector2I chunk) => (_size - chunk * _chunkSize).Min(new Vector2I(_chunkSize, _chunkSize));

    public override void _ExitTree()
    {
        // The geometry is a sibling of this node: its parent may already be traversing child teardown.
        if (GodotObject.IsInstanceValid(_geometry))
        {
            _geometry!.Hide();
            _geometry.QueueFree();
        }
        _geometry = null;
        base._ExitTree();
    }
}
