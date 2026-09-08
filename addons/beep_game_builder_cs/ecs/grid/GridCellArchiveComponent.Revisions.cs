using Godot;
using System;
using System.Collections.Generic;
using System.IO;

namespace Beep.ECS;

public partial class GridCellArchiveComponent
{
    private readonly Dictionary<Vector2I, SavedChunk> _savedChunks = new();
    // Hash is SHA-256 of the bytes written; eviction verifies the file against it instead of re-encoding.
    private sealed record SavedChunk(ulong ServiceId, long Revision, string Destination, byte[] Hash);

    /// <summary>Checks this node's last successful save against live content, without reading the file.</summary>
    public bool IsChunkSaveCurrent(Vector2I coordinate)
    {
        if (!_savedChunks.TryGetValue(coordinate, out var saved)) return false;
        var cells = GetNodeOrNull<GridCellDataComponent>(CellDataPath);
        if (!GodotObject.IsInstanceValid(cells) || cells!.IsQueuedForDeletion()
            || !cells.IsChunkAvailable(coordinate) || cells.GetInstanceId() != saved.ServiceId
            || cells.GetChunkRevision(coordinate) != saved.Revision) return false;
        try { return GetChunkPath(coordinate) == saved.Destination; }
        catch (Exception exception) when (exception is IOException or ArgumentException
            or InvalidOperationException or System.Security.SecurityException) { return false; }
    }

    public void ForgetSavedChunks() => _savedChunks.Clear();

    private void RememberSavedChunk(Vector2I coordinate, GridCellDataComponent cells, long revision, string destination, byte[] hash)
        => _savedChunks[coordinate] = new(cells.GetInstanceId(), revision, destination, hash);
}
