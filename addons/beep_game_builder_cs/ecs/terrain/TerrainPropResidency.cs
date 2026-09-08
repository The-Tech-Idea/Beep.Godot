using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Beep.ECS;

/// <summary>Shared visual residency, independent of prop art and gameplay simulation.</summary>
internal sealed class TerrainPropResidency<T>
{
    private sealed class Chunk
    {
        public int Cells;
        public readonly List<T> Stamps = new();
    }
    private readonly Node2D _owner;
    private readonly GridProjectionComponent? _grid;
    private readonly Vector2I _origin, _size;
    private readonly float _tile;
    private readonly int _chunkSize;
    private readonly Action<int, int, List<T>> _buildCell;
    private readonly Func<Rect2I>? _visibleCells;
    private readonly Func<float>? _cellPixels;
    private readonly Dictionary<Vector2I, Chunk> _chunks = new();
    private readonly HashSet<Vector2I> _wanted = new();
    private bool _needsMerge = true;
    public int ResidentChunks => _chunks.Count;
    public int ResidentCells => _chunks.Values.Sum(chunk => chunk.Cells);
    public int PendingChunks { get; private set; }
    public int ProcessedCells { get; private set; }
    public bool DetailSuppressed { get; private set; }

    public TerrainPropResidency(Node2D owner, GridProjectionComponent? grid, Vector2I origin,
        Vector2I size, float tile, int chunkSize, Action<int, int, List<T>> buildCell,
        Func<Rect2I>? visibleCells = null, Func<float>? cellPixels = null)
    {
        _owner = owner; _grid = grid; _origin = origin; _size = size; _tile = tile;
        _chunkSize = Mathf.Clamp(chunkSize, 8, 128); _buildCell = buildCell;
        _visibleCells = visibleCells;
        _cellPixels = cellPixels;
    }

    public void InvalidateCell(Vector2I absoluteCell)
    {
        Vector2I cell = absoluteCell - _origin;
        // Fine water interpolation can change dry anchors in adjacent cells.
        for (int y = cell.Y - 1; y <= cell.Y + 1; y++)
        for (int x = cell.X - 1; x <= cell.X + 1; x++)
            _chunks.Remove(new Vector2I(Mathf.FloorToInt((float)x / _chunkSize), Mathf.FloorToInt((float)y / _chunkSize)));
        _needsMerge = true;
    }

    public bool Update(int cellsPerUpdate, int preloadChunks, float canopyCells, List<T> target, Comparison<T> compare,
        float minimumCellPixels = 0)
    {
        ProcessedCells = 0;
        _wanted.Clear();
        Vector2 center = Vector2.Zero;
        float threshold = float.IsFinite(minimumCellPixels) ? Mathf.Clamp(minimumCellPixels, 0, 16) : 0;
        float pixels = CellPixels();
        DetailSuppressed = threshold > 0 && float.IsFinite(pixels)
            && pixels <= threshold * (DetailSuppressed ? 1.5f : 1);
        if (_owner.IsVisibleInTree() && !DetailSuppressed)
        {
            Rect2 viewport = _owner.GetViewportRect();
            Transform2D inverse = _owner.GetGlobalTransformWithCanvas().AffineInverse();
            Vector2I min = new(int.MaxValue, int.MaxValue), max = new(int.MinValue, int.MinValue);
            if (_visibleCells is not null)
            {
                Rect2I bounds = _visibleCells();
                min = bounds.Position - _origin;
                max = bounds.End - Vector2I.One - _origin;
            }
            else foreach (Vector2 corner in new[] { viewport.Position, new Vector2(viewport.End.X, viewport.Position.Y),
                         viewport.End, new Vector2(viewport.Position.X, viewport.End.Y) })
            {
                Vector2 local = inverse * corner;
                Vector2I cell = _grid is not null ? _grid.WorldToCell(_owner.ToGlobal(local))
                    : new Vector2I(Mathf.FloorToInt(local.X / _tile), Mathf.FloorToInt(local.Y / _tile));
                cell -= _origin;
                min = min.Min(cell); max = max.Max(cell);
            }
            center = ((Vector2)min + max) * 0.5f / _chunkSize;
            int canopy = Mathf.CeilToInt(canopyCells), margin = Mathf.Clamp(preloadChunks, 0, 4);
            Vector2I start = new(Mathf.FloorToInt((float)(min.X - canopy) / _chunkSize) - margin,
                Mathf.FloorToInt((float)(min.Y - canopy) / _chunkSize) - margin);
            Vector2I end = new(Mathf.FloorToInt((float)(max.X + canopy) / _chunkSize) + margin,
                Mathf.FloorToInt((float)(max.Y + canopy) / _chunkSize) + margin);
            start = start.Max(Vector2I.Zero);
            end = end.Min(new Vector2I((_size.X - 1) / _chunkSize, (_size.Y - 1) / _chunkSize));
            for (int y = start.Y; y <= end.Y; y++)
            for (int x = start.X; x <= end.X; x++) _wanted.Add(new(x, y));
        }
        foreach (Vector2I key in _chunks.Keys.Where(key => !_wanted.Contains(key)).ToArray())
        {
            _chunks.Remove(key); _needsMerge = true;
        }
        int budget = Mathf.Clamp(cellsPerUpdate, 16, 4096);
        foreach (Vector2I key in _wanted.OrderBy(key => ((Vector2)key - center).LengthSquared()).ThenBy(key => key.Y).ThenBy(key => key.X))
        {
            if (!_chunks.TryGetValue(key, out var chunk)) _chunks.Add(key, chunk = new Chunk());
            Vector2I extent = Extent(key);
            while (chunk.Cells < extent.X * extent.Y && budget > 0)
            {
                Vector2I cell = key * _chunkSize + new Vector2I(chunk.Cells % extent.X, chunk.Cells / extent.X);
                _buildCell(cell.X, cell.Y, chunk.Stamps);
                chunk.Cells++; budget--; ProcessedCells++; _needsMerge = true;
            }
            if (budget == 0) break;
        }
        PendingChunks = _wanted.Count(key => !_chunks.TryGetValue(key, out var chunk) || chunk.Cells < Extent(key).X * Extent(key).Y);
        if (!_needsMerge) return false;
        _needsMerge = false;
        target.Clear();
        foreach (var chunk in _chunks.OrderBy(pair => pair.Key.Y).ThenBy(pair => pair.Key.X)) target.AddRange(chunk.Value.Stamps);
        target.Sort(compare);
        return true;
    }

    private Vector2I Extent(Vector2I chunk) => (_size - chunk * _chunkSize).Min(new Vector2I(_chunkSize, _chunkSize));

    private float CellPixels()
    {
        if (_cellPixels is not null) return _cellPixels();
        Transform2D transform = (_grid as Node2D ?? _owner).GetGlobalTransformWithCanvas();
        Vector2 size = _grid?.TileSize ?? Vector2.One * _tile;
        return Mathf.Max((transform.X * size.X).Length(), (transform.Y * size.Y).Length());
    }
}
