using Godot;

namespace Beep.ECS;

public partial class GridJobQueueComponent
{
    private NodePath _chunkCellDataPath = new("");
    private GridChunkPins? _pins;
    private GridChunkPins Pins => _pins ??= new GridChunkPins(this);
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
        _pins?.Release();
        RequestReady();
    }

    public void RefreshChunkPins()
    {
        if (!IsInsideTree() || Engine.IsEditorHint()) return;
        var cells = ChunkCellDataPath.IsEmpty ? null : GetNodeOrNull<GridCellDataComponent>(ChunkCellDataPath);
        BindReservationScope(cells);
        Pins.Bind(cells);
        if (cells is null) return;
        foreach (var job in _jobs.Values)
        {
            if (job.State is not (GridJobState.Queued or GridJobState.Claimed)) continue;
            Pins.WantCell(job.Cell);
            Pins.WantCell(job.ApproachCell);
            if (job.State == GridJobState.Claimed) Pins.WantCell(job.ReservedCell);
        }
        Pins.Commit();
    }
}
