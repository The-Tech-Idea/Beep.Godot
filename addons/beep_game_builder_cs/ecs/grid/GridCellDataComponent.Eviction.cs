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
        // Residency, not content: the chunk's cells are unchanged, only no longer resident.
        // Bumping TerrainRevision here restarted every renderer on a change that moved nothing -
        // the eviction storm this signal exists to end - and PinnedNavigationRevision stays put
        // for the same reason: a demand search pins every chunk it observes, so an eviction only
        // removes data it was not watching.
        //
        // NavigationRevision DOES move, and that is the one difference between the two revisions
        // (see their declarations). A search that does not pin - LoadMissingTerrain off - reads an
        // evicted chunk as the default terrain kind, so for it this eviction changed what the
        // ground is, and a route it finishes could cross water it can no longer see. It is told
        // "navigation_changed" instead. Removing this bump along with the other two made the two
        // revisions identical and let such a search complete on stale ground.
        NavigationRevision++;
        EmitCellsChanged(TerrainChangeKind.Residency, new Godot.Collections.Array<Vector2I> { coordinate });
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
            if (chunk.X >= ChunkAxis(area.Position.X) && chunk.X <= ChunkAxis(endX)
                && chunk.Y >= ChunkAxis(area.Position.Y) && chunk.Y <= ChunkAxis(endY))
                throw new InvalidOperationException("Load archived chunks before painting this area.");
    }
}
