using Godot;

namespace Beep.ECS;

/// <summary>Renderer-owned samples, not gameplay records. Keeps the last published appearance of archived chunks.</summary>
internal sealed class TerrainVisualSnapshot
{
    internal readonly record struct Sample(string Kind, float Elevation,
        (string Inland, float Width, float LakeWidth) Shore, GridTerrainWaterPatch? Water, GridTerrainWaterPatch? Lake);
    private Sample[] _samples = System.Array.Empty<Sample>();
    private ulong _sourceId;
    private long _generation;
    private Vector2I _origin, _size;
    private readonly System.Collections.Generic.Dictionary<Vector2I, long> _revisions = new();
    private readonly System.Collections.Generic.List<Vector2I> _changedChunks = new();
    public ulong Revision { get; private set; }
    /// <summary>Chunks whose samples changed in the latest Update. The renderer refreshes only their texels.</summary>
    internal System.Collections.Generic.IReadOnlyList<Vector2I> ChangedChunks => _changedChunks;
    /// <summary>True when the latest Update replaced every sample: new source, generation, origin or size.</summary>
    internal bool Reset { get; private set; }
    public Sample this[int index] => _samples[index];

    internal sealed class Preparation
    {
        private readonly GridCellDataComponent _cells;
        private readonly LiveTerrainSurfaceData _surface;
        private readonly ulong _terrainRevision;
        private readonly string _defaultKind;
        internal TerrainVisualSnapshot Snapshot { get; }
        internal int Loaded { get; private set; }
        internal int Total => Snapshot._samples.Length;

        internal Preparation(GridCellDataComponent cells, Vector2I origin, Vector2I size)
        {
            _cells = cells;
            _surface = new(cells);
            _terrainRevision = cells.TerrainRevision;
            _defaultKind = cells.DefaultTerrainKind;
            Snapshot = new()
            {
                _sourceId = cells.GetInstanceId(), _generation = cells.ReplacementRevision,
                _origin = origin, _size = size, Revision = 1,
                _samples = new Sample[checked(size.X * size.Y)]
            };
        }

        internal bool Matches(GridCellDataComponent? cells, Vector2I origin, Vector2I size)
            => GodotObject.IsInstanceValid(_cells) && cells == _cells
                && origin == Snapshot._origin && size == Snapshot._size
                && _cells.ReplacementRevision == Snapshot._generation
                && _cells.TerrainRevision == _terrainRevision && _cells.DefaultTerrainKind == _defaultKind;

        internal bool Step(int budget)
        {
            if (!Matches(_cells, Snapshot._origin, Snapshot._size))
                throw new System.InvalidOperationException("Terrain changed during painted snapshot preparation.");
            int end = Loaded + System.Math.Min(System.Math.Clamp(budget, 1, 4096), Total - Loaded);
            while (Loaded < end)
            {
                Vector2I cell = Snapshot._origin + new Vector2I(Loaded % Snapshot._size.X, Loaded / Snapshot._size.X);
                if (!_cells.IsCellAvailable(cell))
                    throw new System.InvalidOperationException("Painted snapshot requires available terrain cells.");
                Snapshot._samples[Loaded++] = ReadSample(_cells, _surface, cell);
                var chunk = ChunkedCellStore<object>.ChunkFor(cell);
                Snapshot._revisions.TryAdd(chunk, _cells.GetChunkRevision(chunk));
            }
            return Loaded == Total;
        }
    }

    private static Sample ReadSample(GridCellDataComponent cells, LiveTerrainSurfaceData surface, Vector2I cell)
        => new(GridCellRules.TerrainKindAt(cells, cell), surface.ElevationAtCell(cell),
            cells.ShoreAtCell(cell), cells.WaterPatchAtCell(cell), cells.LakePatchAtCell(cell));

    public bool Update(GridCellDataComponent cells, Vector2I origin, Vector2I size)
    {
        _changedChunks.Clear();
        Reset = false;
        bool reset = _sourceId != cells.GetInstanceId() || _generation != cells.ReplacementRevision
            || _origin != origin || _size != size;
        if (reset)
        {
            Reset = true;
            // An overview cannot invent the appearance of data never observed in this world.
            for (int y = 0; y < size.Y; y++)
                for (int x = 0; x < size.X; x++)
                    if (!cells.IsCellAvailable(origin + new Vector2I(x, y))) return false;
            _samples = new Sample[checked(size.X * size.Y)];
            _sourceId = cells.GetInstanceId();
            _generation = cells.ReplacementRevision;
            _origin = origin;
            _size = size;
            _revisions.Clear();
            Revision++;
        }
        var surface = new LiveTerrainSurfaceData(cells);
        Vector2I end = origin + size;
        for (int cy = GridCellDataComponent.ChunkAxis(origin.Y); cy <= GridCellDataComponent.ChunkAxis(end.Y - 1); cy++)
        for (int cx = GridCellDataComponent.ChunkAxis(origin.X); cx <= GridCellDataComponent.ChunkAxis(end.X - 1); cx++)
        {
            var chunk = new Vector2I(cx, cy);
            if (!cells.IsChunkAvailable(chunk)) continue;
            long revision = cells.GetChunkRevision(chunk);
            if (_revisions.TryGetValue(chunk, out long previous) && previous == revision) continue;
            bool changed = false;
            for (int y = System.Math.Max(origin.Y, cy * 32); y < System.Math.Min(end.Y, (cy + 1) * 32); y++)
            for (int x = System.Math.Max(origin.X, cx * 32); x < System.Math.Min(end.X, (cx + 1) * 32); x++)
            {
                var cell = new Vector2I(x, y);
                var sample = ReadSample(cells, surface, cell);
                int index = (y - origin.Y) * size.X + x - origin.X;
                changed |= _samples[index] != sample;
                _samples[index] = sample;
            }
            _revisions[chunk] = revision;
            if (changed)
            {
                Revision++;
                _changedChunks.Add(chunk);
            }
        }
        return true;
    }
}
