using Godot;
using System.Collections.Generic;

namespace Beep.ECS;

public partial class GridCellDataComponent
{
    private readonly HashSet<Vector2I> _unavailableChunks = new();
    public int UnavailableChunkCount => _unavailableChunks.Count;
    public bool IsCellAvailable(Vector2I cell) => IsChunkAvailable(ChunkedCellStore<CellRecord>.ChunkFor(cell));
    public bool IsChunkAvailable(Vector2I coordinate) => !_unavailableChunks.Contains(coordinate);

    /// <summary>Declare readiness without dropping records or stopping their background simulation.</summary>
    public bool SetChunkAvailable(Vector2I coordinate, bool available)
    {
        if (!available && IsChunkPinned(coordinate)) return false;
        if (available && IsChunkEvicted(coordinate)) return false;
        bool changed = available ? _unavailableChunks.Remove(coordinate) : _unavailableChunks.Add(coordinate);
        if (!changed) return true;
        TerrainRevision++;
        MarkNavigationChanged();
        EmitCellsChanged(TerrainChangeKind.Terrain | TerrainChangeKind.Navigation, new Godot.Collections.Array<Vector2I> { coordinate });
        return true;
    }
}
