using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS;

public partial class GridCellDataComponent
{
    private readonly HashSet<Vector2I> _evictedChunks = new();
    private int _simulationDepth;
    public int EvictedChunkCount => _evictedChunks.Count;
    public bool IsChunkEvicted(Vector2I coordinate) => _evictedChunks.Contains(coordinate);

    public bool CanEvictChunk(Vector2I coordinate)
    {
        if (_simulationDepth > 0 || !IsChunkAvailable(coordinate) || IsChunkPinned(coordinate)) return false;
        foreach (var (_, record) in _cells.InChunk(coordinate))
            if (!string.IsNullOrEmpty(record.CropId) || (record.Flags & CellFlags.Watered) != 0)
                return false;
        return true;
    }

    // Called only after the archive has verified the saved file against the bytes it wrote for this
    // revision. No callback may run before removal.
    internal bool TryEvictChunk(Vector2I coordinate, long expectedRevision)
    {
        if (GetChunkRevision(coordinate) != expectedRevision || !CanEvictChunk(coordinate)) return false;
        _cells.RemoveChunk(coordinate);
        _evictedChunks.Add(coordinate);
        _unavailableChunks.Add(coordinate);
        TerrainRevision++;
        NavigationRevision++;
        // Scheduled demand searches pin every observed chunk. An eviction can
        // only remove unobserved data, so their working sets remain valid.
        EmitSignal(SignalName.CellsChanged);
        return true;
    }

    private void RequireResidentCell(Vector2I cell)
    {
        if (IsChunkEvicted(ChunkedCellStore<CellRecord>.ChunkFor(cell)))
            throw new InvalidOperationException("Load the archived chunk before editing its cells.");
    }

    private void RequireResidentArea(Rect2I area)
    {
        if (area.Size.X <= 0 || area.Size.Y <= 0) return;
        long endX = (long)area.Position.X + area.Size.X - 1;
        long endY = (long)area.Position.Y + area.Size.Y - 1;
        foreach (var chunk in _evictedChunks)
            if (chunk.X >= (area.Position.X >> 5) && chunk.X <= (endX >> 5)
                && chunk.Y >= (area.Position.Y >> 5) && chunk.Y <= (endY >> 5))
                throw new InvalidOperationException("Load archived chunks before painting this area.");
    }
}
