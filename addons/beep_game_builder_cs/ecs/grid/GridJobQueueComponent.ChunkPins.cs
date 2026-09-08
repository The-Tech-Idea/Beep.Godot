using Godot;
using System.Collections.Generic;

namespace Beep.ECS;

public partial class GridJobQueueComponent
{
    private NodePath _chunkCellDataPath = new("");
    private GridCellDataComponent? _pinCells;
    [Export] public NodePath ChunkCellDataPath
    {
        get => _chunkCellDataPath;
        set { _chunkCellDataPath = value; if (IsInsideTree()) RefreshChunkPins(); }
    }

    public override void _Ready() => RefreshChunkPins();
    public override void _ExitTree()
    {
        ReleaseSharedReservations();
        _reservationCells = null;
        _reservationScope = null;
        if (GodotObject.IsInstanceValid(_pinCells)) _pinCells!.ReleaseChunkPins(this);
        _pinCells = null;
        RequestReady();
    }

    public void RefreshChunkPins()
    {
        if (!IsInsideTree() || Engine.IsEditorHint()) return;
        var cells = ChunkCellDataPath.IsEmpty ? null : GetNodeOrNull<GridCellDataComponent>(ChunkCellDataPath);
        BindReservationScope(cells);
        if (_pinCells != cells)
        {
            if (GodotObject.IsInstanceValid(_pinCells)) _pinCells!.ReleaseChunkPins(this);
            _pinCells = cells;
        }
        if (cells is null) return;
        var chunks = new HashSet<Vector2I>();
        foreach (var job in _jobs.Values)
        {
            if (job.State is not (GridJobState.Queued or GridJobState.Claimed)) continue;
            chunks.Add(new(job.Cell.X >> 5, job.Cell.Y >> 5));
            chunks.Add(new(job.ApproachCell.X >> 5, job.ApproachCell.Y >> 5));
            if (job.State == GridJobState.Claimed)
                chunks.Add(new(job.ReservedCell.X >> 5, job.ReservedCell.Y >> 5));
        }
        cells.ReplaceChunkPins(this, chunks);
    }
}
