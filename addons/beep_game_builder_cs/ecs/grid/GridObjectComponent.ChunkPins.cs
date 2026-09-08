using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS;

public partial class GridObjectComponent
{
    private NodePath _chunkCellDataPath = new("");
    private GridCellDataComponent? _pinCells;

    /// <summary>Explicit world binding for the object's entire logical footprint, independent of occupancy.</summary>
    [Export] public NodePath ChunkCellDataPath
    {
        get => _chunkCellDataPath;
        set { _chunkCellDataPath = value; RefreshChunkPins(); }
    }

    public bool RefreshChunkPins()
    {
        if (!IsInsideTree() || Engine.IsEditorHint()) return false;
        // Widen before addition: footprints near the coordinate limit must never wrap into another region.
        long lastX = (long)Cell.X + Math.Max(1, Footprint.X) - 1;
        long lastY = (long)Cell.Y + Math.Max(1, Footprint.Y) - 1;
        long minX = Cell.X >> 5, minY = Cell.Y >> 5;
        long maxX = lastX >> 5, maxY = lastY >> 5;
        if (lastX > int.MaxValue || lastY > int.MaxValue || (maxX - minX + 1) * (maxY - minY + 1) > 65536)
        {
            GD.PushError("GridObject footprint exceeds supported chunk demand; previous pins retained.");
            return false;
        }
        var cells = ChunkCellDataPath.IsEmpty ? null : GetNodeOrNull<GridCellDataComponent>(ChunkCellDataPath);
        if (cells != _pinCells)
        {
            ReleaseChunkPins();
            _pinCells = cells;
        }
        if (_pinCells is null) return false;
        var chunks = new HashSet<Vector2I>();
        for (long y = minY; y <= maxY; y++)
            for (long x = minX; x <= maxX; x++)
                chunks.Add(new Vector2I((int)x, (int)y));
        return _pinCells.ReplaceChunkPins(this, chunks);
    }

    private void ReleaseChunkPins()
    {
        if (_pinCells is not null && GodotObject.IsInstanceValid(_pinCells))
            _pinCells.ReleaseChunkPins(this);
        _pinCells = null;
    }
}
