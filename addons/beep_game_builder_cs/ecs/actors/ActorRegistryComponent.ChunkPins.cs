using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS;

public partial class ActorRegistryComponent
{
    private NodePath _chunkCellDataPath = new(""), _chunkGridPath = new("");
    private GridCellDataComponent? _pinCells;
    private GridProjectionComponent? _pinGrid;
    private SceneTree? _pinTree;
    private bool _pinSourcesDirty = true;

    public override void _EnterTree()
    {
        if (Engine.IsEditorHint()) return;
        _pinTree = GetTree();
        _pinTree.TreeChanged += InvalidatePinSources;
        _pinSourcesDirty = true;
    }

    private void InvalidatePinSources() => _pinSourcesDirty = true;
    private readonly Dictionary<string, (Vector2I Chunk, int Radius)> _actorPins = new(StringComparer.Ordinal);
    private int _actorChunkRadius = 1;
    [Export] public NodePath ChunkCellDataPath
    {
        get => _chunkCellDataPath;
        set { _chunkCellDataPath = value; if (IsInsideTree()) RefreshChunkPins(); }
    }
    [Export] public NodePath ChunkGridPath
    {
        get => _chunkGridPath;
        set { _chunkGridPath = value; if (IsInsideTree()) RefreshChunkPins(); }
    }
    [Export(PropertyHint.Range, "0,4,1")] public int ActorChunkRadius
    {
        get => _actorChunkRadius;
        set { _actorChunkRadius = Math.Clamp(value, 0, 4); if (IsInsideTree()) RefreshChunkPins(); }
    }

    public void RefreshChunkPins()
    {
        _pinSourcesDirty = true;
        foreach (var actor in _actors.Values) UpdateActorChunkPins(actor, actor.Body?.GlobalPosition);
    }

    private void ResolvePinSources()
    {
        if (!_pinSourcesDirty) return;
        _pinSourcesDirty = false;
        var cells = ChunkCellDataPath.IsEmpty ? null : GetNodeOrNull<GridCellDataComponent>(ChunkCellDataPath);
        if (_pinCells != cells)
        {
            if (GodotObject.IsInstanceValid(_pinCells))
                foreach (var existing in _actors.Values) _pinCells!.ReleaseChunkPins(existing);
            _actorPins.Clear();
            _pinCells = cells;
        }
        _pinGrid = ChunkGridPath.IsEmpty ? null : GetNodeOrNull<GridProjectionComponent>(ChunkGridPath);
    }

    private void UpdateActorChunkPins(ActorComponent actor, Vector2? position)
    {
        if (_pinTree is null) return;
        // Scene structure invalidates shared bindings; movement does not. Never
        // cache projected positions because the grid transform may change live.
        ResolvePinSources();
        var cells = _pinCells;
        var grid = _pinGrid;
        if (cells is null || grid is null || position is not { } current || !current.IsFinite())
        {
            ReleaseActorChunkPins(actor);
            return;
        }
        var cell = grid.WorldToCell(current);
        if (cell == new Vector2I(int.MinValue, int.MinValue)) return;
        var center = new Vector2I(cell.X >> 5, cell.Y >> 5);
        int radius = ActorChunkRadius;
        if (_actorPins.TryGetValue(actor.ActorId, out var previous) && previous == (center, radius) && cells.HasChunkPins(actor)) return;
        var chunks = new HashSet<Vector2I>();
        for (int y = -radius; y <= radius; y++)
            for (int x = -radius; x <= radius; x++) chunks.Add(center + new Vector2I(x, y));
        if (cells.ReplaceChunkPins(actor, chunks)) _actorPins[actor.ActorId] = (center, radius);
    }

    private void ReleaseActorChunkPins(ActorComponent actor)
    {
        if (GodotObject.IsInstanceValid(_pinCells)) _pinCells!.ReleaseChunkPins(actor);
        _actorPins.Remove(actor.ActorId);
    }

    public override void _ExitTree()
    {
        if (_pinTree is not null) _pinTree.TreeChanged -= InvalidatePinSources;
        _pinTree = null;
        _pinGrid = null;
        _pinSourcesDirty = true;
        if (GodotObject.IsInstanceValid(_pinCells))
            foreach (var actor in _actors.Values) _pinCells!.ReleaseChunkPins(actor);
        _pinCells = null;
        _actorPins.Clear();
        RequestReady();
    }
}
