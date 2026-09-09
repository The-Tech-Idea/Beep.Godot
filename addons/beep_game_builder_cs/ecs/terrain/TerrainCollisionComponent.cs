using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>Native terrain collision projected from the live grid. Owns no terrain or pathfinding data.</summary>
    [GlobalClass]
    public partial class TerrainCollisionComponent : Node2D
    {
        [Export] public NodePath GridPath { get; set; } = new("");
        [Export] public NodePath CellDataPath { get; set; } = new("");
        [Export] public Vector2I BoundsOrigin { get; set; } = Vector2I.Zero;
        [Export] public Vector2I BoundsSize { get; set; } = new(64, 64);
        [Export] public bool RefreshOnReady { get; set; } = true;
        [Export(PropertyHint.Range, "1,64,1")] public int ChunksPerFrame { get; set; } = 4;
        [Export(PropertyHint.Layers2DPhysics)] public uint LandCollisionLayer { get; set; }
        [Export(PropertyHint.Layers2DPhysics)] public uint WaterCollisionLayer { get; set; } = 4;
        [Export(PropertyHint.Layers2DPhysics)] public uint SteepCollisionLayer { get; set; } = 8;

        private GridProjectionComponent? _grid;
        private GridCellDataComponent? _cells;
        private readonly Dictionary<TerrainTileSets.Ground, StaticBody2D> _bodies = new();
        private readonly Dictionary<Vector2I, List<(StaticBody2D Body, uint Owner)>> _shapes = new();
        private readonly Dictionary<Vector2I, (long Revision, bool Available)> _versions = new();
        private readonly Dictionary<Vector2I, ulong> _builtFrames = new();
        private readonly HashSet<Vector2I> _failedChunks = new();
        private readonly HashSet<Vector2I> _dirty = new();
        private readonly Queue<Vector2I> _pending = new();
        private bool _full;
        private bool _scanVersions;
        private int _shapeCount;
        private const int ChunkSize = ChunkedCellStore<object>.ChunkSize;
        public int ChunksRebuiltLastUpdate { get; private set; }
        public int FailedChunkCount => _failedChunks.Count;
        public int PendingChunkCount => _dirty.Count;
        public bool IsUpdating => _full || _scanVersions || _dirty.Count > 0;
        public bool IsReady
        {
            get
            {
                if (IsUpdating || _versions.Count == 0 || _failedChunks.Count > 0) return false;
                foreach (var chunk in _versions.Keys)
                    if (!IsChunkReady(chunk)) return false;
                return true;
            }
        }

        internal bool UsesSources(GridProjectionComponent grid, GridCellDataComponent cells)
            => IsInsideTree() && _grid == grid && _cells == cells
                && GetNodeOrNull<GridProjectionComponent>(GridPath) == grid
                && GetNodeOrNull<GridCellDataComponent>(CellDataPath) == cells;

        public bool IsChunkReady(Vector2I chunk)
            => IsInsideTree() && !_full && !_dirty.Contains(chunk) && GodotObject.IsInstanceValid(_cells)
                && _versions.TryGetValue(chunk, out var version) && version.Available
                && version.Revision == _cells!.GetChunkRevision(chunk) && _cells.IsChunkAvailable(chunk)
                && _builtFrames.TryGetValue(chunk, out ulong frame) && frame < Engine.GetPhysicsFrames();

        public override void _Ready()
        {
            Resolve();
            SetProcess(false);
            if (RefreshOnReady || _versions.Count > 0) QueueFull();
            else if (IsUpdating) Queue();
        }

        public override void _ExitTree()
        {
            Disconnect();
            _pending.Clear();
            _dirty.Clear();
            _full = _scanVersions = false;
            SetProcess(false);
            RequestReady();
        }

        public override string[] _GetConfigurationWarnings()
            => GridPath.IsEmpty || CellDataPath.IsEmpty
                ? new[] { "Assign the gameplay GridPath and live CellDataPath." }
                : System.Array.Empty<string>();

        private void Disconnect()
        {
            if (GodotObject.IsInstanceValid(_grid)) _grid!.GeometryChanged -= QueueFull;
            if (GodotObject.IsInstanceValid(_cells))
            {
                _cells!.CellChanged -= OnCellChanged;
                _cells.CellsChanged -= OnCellsChangedSignal;
            }
            _grid = null;
            _cells = null;
        }

        private bool Resolve()
        {
            var grid = GridPath.IsEmpty ? null : GetNodeOrNull<GridProjectionComponent>(GridPath);
            var cells = CellDataPath.IsEmpty ? null : GetNodeOrNull<GridCellDataComponent>(CellDataPath);
            if (grid == _grid && cells == _cells) return false;
            Disconnect();
            _grid = grid;
            _cells = cells;
            if (_grid is not null) _grid.GeometryChanged += QueueFull;
            if (_cells is not null)
            {
                _cells.CellChanged += OnCellChanged;
                _cells.CellsChanged += OnCellsChangedSignal;
            }
            return true;
        }

        private void OnCellChanged(int x, int y)
        {
            var cell = new Vector2I(x, y);
            if (!new Rect2I(BoundsOrigin, BoundsSize).HasPoint(cell)) return;
            EnqueueChunk(ChunkedCellStore<object>.ChunkFor(cell));
            Queue();
        }

        private void QueueFull() { _full = true; Queue(); }
        private void QueueData() { _scanVersions = true; Queue(); }

        // A residency move touches no collision; a content change rebuilds only the
        // listed chunks, or rescans versions when the whole map is flagged (empty list).
        private void OnCellsChangedSignal(int kind, Godot.Collections.Array<Vector2I> chunks)
        {
            if (((TerrainChangeKind)kind & TerrainChangeKind.Content) == 0) return;
            if (chunks.Count == 0) { QueueData(); return; }
            foreach (Vector2I chunk in chunks) EnqueueChunk(chunk);
            Queue();
        }

        private void Queue()
        {
            if (IsInsideTree()) SetProcess(true);
        }

        private void EnqueueChunk(Vector2I chunk)
        {
            if (_dirty.Add(chunk)) _pending.Enqueue(chunk);
        }

        /// <summary>Schedules replacement collision across frames. Pending chunks are not ready for motion.</summary>
        public void RequestRebuild() => QueueFull();

        public override void _Process(double delta)
        {
            if (Resolve()) _full = true;
            if (_full)
            {
                // Include old chunks so shrinking/rebinding cannot leave orphan collision.
                foreach (var chunk in _versions.Keys) EnqueueChunk(chunk);
                if (_grid is not null && _cells is not null && BoundsSize.X > 0 && BoundsSize.Y > 0)
                {
                    Vector2I first = ChunkedCellStore<object>.ChunkFor(BoundsOrigin);
                    Vector2I last = ChunkedCellStore<object>.ChunkFor(BoundsOrigin + BoundsSize - Vector2I.One);
                    for (int y = first.Y; y <= last.Y; y++)
                        for (int x = first.X; x <= last.X; x++) EnqueueChunk(new(x, y));
                }
                _full = false;
            }
            if (_scanVersions && _cells is not null)
                foreach (var (chunk, version) in _versions)
                    if (version != (_cells.GetChunkRevision(chunk), _cells.IsChunkAvailable(chunk))) EnqueueChunk(chunk);
            _scanVersions = false;
            ChunksRebuiltLastUpdate = 0;
            int budget = Math.Clamp(ChunksPerFrame, 1, 64);
            while (budget-- > 0 && _pending.TryDequeue(out var chunk))
            {
                _dirty.Remove(chunk);
                UpdateChunk(chunk);
            }
            SetProcess(IsUpdating);
        }

        /// <summary>Call after changing paths, masks, bounds or independent parent transforms.</summary>
        public void Rebuild()
        {
            Resolve();
            SetProcess(false);
            _full = false;
            _scanVersions = false;
            _dirty.Clear();
            _pending.Clear();
            foreach (var shapes in _shapes.Values)
                foreach (var shape in shapes)
                    if (GodotObject.IsInstanceValid(shape.Body)) shape.Body.RemoveShapeOwner(shape.Owner);
            _shapes.Clear();
            _versions.Clear();
            _builtFrames.Clear();
            _failedChunks.Clear();
            _shapeCount = 0;
            ChunksRebuiltLastUpdate = 0;
            if (_grid is null || _cells is null) return;
            if (BoundsSize.X <= 0 || BoundsSize.Y <= 0) return;
            Vector2I first = ChunkedCellStore<object>.ChunkFor(BoundsOrigin);
            Vector2I last = ChunkedCellStore<object>.ChunkFor(BoundsOrigin + BoundsSize - Vector2I.One);
            for (int y = first.Y; y <= last.Y; y++)
                for (int x = first.X; x <= last.X; x++) UpdateChunk(new(x, y));
        }

        private void UpdateChunk(Vector2I chunk)
        {
            ChunksRebuiltLastUpdate++;
            _builtFrames.Remove(chunk);
            _failedChunks.Remove(chunk);
            RemoveChunkShapes(chunk);
            _versions.Remove(chunk);
            if (!new Rect2I(BoundsOrigin, BoundsSize).Intersects(new(chunk * ChunkSize, new(ChunkSize, ChunkSize)))) return;
            BuildChunk(chunk);
            if (_failedChunks.Contains(chunk)) RemoveChunkShapes(chunk);
        }

        private void RemoveChunkShapes(Vector2I chunk)
        {
            if (_shapes.Remove(chunk, out var old))
            {
                _shapeCount -= old.Count;
                foreach (var shape in old)
                    if (GodotObject.IsInstanceValid(shape.Body)) shape.Body.RemoveShapeOwner(shape.Owner);
            }
        }

        private void BuildChunk(Vector2I chunk)
        {
            if (_grid is null || _cells is null) return;
            bool available = _cells.IsChunkAvailable(chunk);
            _versions[chunk] = (_cells.GetChunkRevision(chunk), available);
            if (!available) return;
            Rect2I bounds = new Rect2I(BoundsOrigin, BoundsSize).Intersection(new(chunk * ChunkSize, new(ChunkSize, ChunkSize)));
            Span<int> classes = stackalloc int[ChunkSize * ChunkSize];
            classes.Clear();
            for (int y = 0; y < bounds.Size.Y; y++)
                for (int x = 0; x < bounds.Size.X; x++)
                {
                    string kind = GridCellRules.TerrainKindAt(_cells, bounds.Position + new Vector2I(x, y));
                    if (kind.Length == 0) continue;
                    var ground = TerrainTileSets.GroundOf(kind);
                    if (Mask(ground) != 0) classes[y * ChunkSize + x] = (int)ground + 1;
                }
            bool merge = _grid.TileMapLayerPath.IsEmpty && _grid.ElevatedTerrainPath.IsEmpty;
            for (int y = 0; y < bounds.Size.Y; y++)
                for (int x = 0; x < bounds.Size.X; x++)
                {
                    int value = classes[y * ChunkSize + x];
                    if (value == 0) continue;
                    int width = 1, height = 1;
                    if (merge)
                    {
                        while (x + width < bounds.Size.X && classes[y * ChunkSize + x + width] == value) width++;
                        while (y + height < bounds.Size.Y)
                        {
                            bool same = true;
                            for (int dx = 0; dx < width; dx++)
                                if (classes[(y + height) * ChunkSize + x + dx] != value) { same = false; break; }
                            if (!same) break;
                            height++;
                        }
                    }
                    for (int dy = 0; dy < height; dy++) classes.Slice((y + dy) * ChunkSize + x, width).Clear();
                    Vector2I cell = bounds.Position + new Vector2I(x, y);
                    Vector2[] corners = _grid.CellCorners(cell);
                    if (corners.Length < 3) { _failedChunks.Add(chunk); continue; }
                    if (width > 1 || height > 1)
                    {
                        corners[1] = _grid.CellCorners(cell + new Vector2I(width - 1, 0))[1];
                        corners[2] = _grid.CellCorners(cell + new Vector2I(width - 1, height - 1))[2];
                        corners[3] = _grid.CellCorners(cell + new Vector2I(0, height - 1))[3];
                    }
                    if (!AddShape(chunk, (TerrainTileSets.Ground)(value - 1), corners)) _failedChunks.Add(chunk);
                }
            if (!_failedChunks.Contains(chunk)) _builtFrames[chunk] = Engine.GetPhysicsFrames();
        }

        private uint Mask(TerrainTileSets.Ground ground) => ground switch
            {
                TerrainTileSets.Ground.Water => WaterCollisionLayer,
                TerrainTileSets.Ground.Steep => SteepCollisionLayer,
                _ => LandCollisionLayer
            };

        private static bool Invertible(Transform2D transform)
            => transform.X.IsFinite() && transform.Y.IsFinite() && transform.Origin.IsFinite()
                && float.IsFinite(transform.Determinant()) && transform.Determinant() != 0;

        private bool AddShape(Vector2I chunk, TerrainTileSets.Ground ground, Vector2[] corners)
        {
            if (!Invertible(GlobalTransform) || !Invertible(_grid!.GlobalTransform)) return false;
            if (!_bodies.TryGetValue(ground, out var body) || !GodotObject.IsInstanceValid(body))
            {
                body = new StaticBody2D { Name = ground.ToString(), CollisionMask = 0 };
                AddChild(body);
                _bodies[ground] = body;
            }
            if (!Invertible(body.GlobalTransform)) return false;
            body.CollisionLayer = Mask(ground);
            for (int i = 0; i < corners.Length; i++)
            {
                corners[i] = body.ToLocal(_grid!.ToGlobal(corners[i]));
                if (!corners[i].IsFinite()) return false;
            }
            // A collapsed native-layer transform can produce finite but collinear corners.
            double area = 0;
            for (int i = 1; i + 1 < corners.Length; i++)
            {
                Vector2 a = corners[i] - corners[0], b = corners[i + 1] - corners[0];
                area += (double)a.X * b.Y - (double)a.Y * b.X;
            }
            if (!double.IsFinite(area) || area == 0) return false;
            uint owner = body.CreateShapeOwner(this);
            body.ShapeOwnerAddShape(owner, new ConvexPolygonShape2D { Points = corners });
            if (!_shapes.TryGetValue(chunk, out var shapes)) _shapes[chunk] = shapes = new();
            shapes.Add((body, owner));
            _shapeCount++;
            return true;
        }

        public int ShapeCount => _shapeCount;
    }
}
