using Godot;
using System.Collections.Generic;

namespace Beep.ECS;

public partial class GridPathFollowerComponent
{
    private GridCellDataComponent? _routePinCells;
    private readonly HashSet<Vector2I> _routeChunks = new();
    private readonly PriorityQueue<Vector2I, int> _routeChunkExpiry = new();
    public int PinnedRouteChunkCount => _routeChunks.Count;

    private void ReleaseRoutePins()
    {
        if (GodotObject.IsInstanceValid(_routePinCells)) _routePinCells!.ReleaseChunkPins(this);
        _routePinCells = null;
        _routeChunks.Clear();
        _routeChunkExpiry.Clear();
    }

    private void RefreshRoutePins()
    {
        ReleaseRoutePins();
        if (!IsInsideTree() || !IsMoving || _cellPath.Count == 0 || _navigation?.RouteCellData is not { } cells) return;
        var lastSegment = new Dictionary<Vector2I, int>();
        for (int i = Mathf.Max(0, _pathIndex); i < _cellPath.Count; i++)
        {
            var cell = _cellPath[i];
            var previous = _cellPath[Mathf.Max(0, i - 1)];
            lastSegment[GridCellDataComponent.ChunkOf(cell)] = i;
            lastSegment[GridCellDataComponent.ChunkOf(previous)] = i;
            // Diagonal traversal also reads the two orthogonal side cells.
            lastSegment[GridCellDataComponent.ChunkOf(new Vector2I(previous.X, cell.Y))] = i;
            lastSegment[GridCellDataComponent.ChunkOf(new Vector2I(cell.X, previous.Y))] = i;
        }
        foreach (var (chunk, segment) in lastSegment)
        {
            _routeChunks.Add(chunk);
            _routeChunkExpiry.Enqueue(chunk, segment);
        }
        if (cells.ReplaceChunkPins(this, _routeChunks)) _routePinCells = cells;
        else ReleaseRoutePins();
    }

    private void RetirePassedRouteChunks()
    {
        if (_routePinCells is null) return;
        bool changed = false;
        while (_routeChunkExpiry.TryPeek(out var chunk, out int lastSegment) && lastSegment < _pathIndex)
        {
            _routeChunkExpiry.Dequeue();
            changed |= _routeChunks.Remove(chunk);
        }
        if (changed) _routePinCells.ReplaceChunkPins(this, _routeChunks);
    }
}
