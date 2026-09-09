using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS;

public partial class GridCellDataComponent
{
    private sealed record PinOwner(Node Owner, HashSet<Vector2I> Chunks, Action OnExit);
    private readonly Dictionary<ulong, PinOwner> _pinOwners = new();
    private readonly Dictionary<Vector2I, int> _chunkPinCounts = new();
    public int PinnedChunkCount => _chunkPinCounts.Count;
    public Godot.Collections.Array<Vector2I> GetPinnedChunks() => new(_chunkPinCounts.Keys);
    public bool IsChunkPinned(Vector2I chunk) => _chunkPinCounts.ContainsKey(chunk);
    public int GetChunkPinCount(Vector2I chunk) => _chunkPinCounts.GetValueOrDefault(chunk);
    public bool HasChunkPins(Node owner) => GodotObject.IsInstanceValid(owner) && _pinOwners.ContainsKey(owner.GetInstanceId());

    /// <summary>The chunk a cell falls in. The chunk rule lives in <see cref="ChunkedCellStore{T}"/>; this is its public face for pin owners.</summary>
    public static Vector2I ChunkOf(Vector2I cell) => ChunkedCellStore<CellRecord>.ChunkFor(cell);

    /// <summary>The chunk index one axis coordinate falls in, widened so a near-limit rectangle cannot wrap.</summary>
    public static int ChunkAxis(long coordinate) => ChunkedCellStore<CellRecord>.ChunkAxis(coordinate);

    public bool SetChunkPins(Node owner, Godot.Collections.Array<Vector2I> chunks)
        => ReplaceChunkPins(owner, new HashSet<Vector2I>(chunks));

    internal bool ReplaceChunkPins(Node owner, HashSet<Vector2I> chunks)
    {
        if (!IsInsideTree() || !GodotObject.IsInstanceValid(owner) || !owner.IsInsideTree() || owner.IsQueuedForDeletion()) return false;
        ulong id = owner.GetInstanceId();
        if (chunks.Count == 0) { ReleasePins(id); return true; }
        if (_pinOwners.TryGetValue(id, out var previous))
        {
            if (previous.Chunks.SetEquals(chunks)) return true;
            foreach (var chunk in previous.Chunks)
                if (!chunks.Contains(chunk)) RemovePin(chunk);
            foreach (var chunk in chunks)
                if (!previous.Chunks.Contains(chunk)) _chunkPinCounts[chunk] = GetChunkPinCount(chunk) + 1;
            _pinOwners[id] = previous with { Chunks = new HashSet<Vector2I>(chunks) };
        }
        else
        {
            Action onExit = () => ReleasePins(id);
            owner.TreeExiting += onExit;
            // Callers may extend a demand set later; counts require an owned snapshot.
            _pinOwners[id] = new(owner, new HashSet<Vector2I>(chunks), onExit);
            foreach (var chunk in chunks) _chunkPinCounts[chunk] = GetChunkPinCount(chunk) + 1;
        }
        return true;
    }

    public void ReleaseChunkPins(Node owner)
    {
        if (GodotObject.IsInstanceValid(owner)) ReleasePins(owner.GetInstanceId());
    }

    private void ReleasePins(ulong id)
    {
        if (!_pinOwners.Remove(id, out var pin)) return;
        if (GodotObject.IsInstanceValid(pin.Owner)) pin.Owner.TreeExiting -= pin.OnExit;
        foreach (var chunk in pin.Chunks) RemovePin(chunk);
    }

    private void RemovePin(Vector2I chunk)
    {
        int count = _chunkPinCounts[chunk];
        if (count == 1) _chunkPinCounts.Remove(chunk); else _chunkPinCounts[chunk] = count - 1;
    }

    public override void _ExitTree()
    {
        foreach (var pin in _pinOwners.Values)
            if (GodotObject.IsInstanceValid(pin.Owner)) pin.Owner.TreeExiting -= pin.OnExit;
        _pinOwners.Clear();
        _chunkPinCounts.Clear();
    }
}
