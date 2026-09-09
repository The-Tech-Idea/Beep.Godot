using Godot;

namespace Beep.ECS;

public partial class GridHaulerComponent
{
    private NodePath _chunkCellDataPath = new("");
    private GridChunkPins? _pins;
    private GridChunkPins Pins => _pins ??= new GridChunkPins(this);
    private Vector2I _pickupCell;
    private bool _awaitingTerrain;

    [Export] public NodePath ChunkCellDataPath
    {
        get => _chunkCellDataPath;
        set { _chunkCellDataPath = value; if (IsInsideTree()) RefreshChunkPins(); }
    }
    public bool IsWaitingForTerrain => _awaitingTerrain;

    /// <summary>Pickup while approaching it, and depot while any cargo remains, including suspended cargo.</summary>
    public void RefreshChunkPins()
    {
        if (!IsInsideTree() || Engine.IsEditorHint()) return;
        Pins.Bind(ChunkCellDataPath.IsEmpty ? null : GetNodeOrNull<GridCellDataComponent>(ChunkCellDataPath));
        if (Pins.Cells is null) return;
        if (_cargoAmount > 0)
        {
            Pins.WantCell(DepotCell);
            if (State == HaulerState.MovingToPickup) Pins.WantCell(_pickupCell);
        }
        Pins.Commit();
    }

    private bool EndpointReady(Vector2I cell)
        => ChunkCellDataPath.IsEmpty
            || (Pins.Cells is { } cells && GodotObject.IsInstanceValid(cells) && cells.IsCellAvailable(cell));

    private bool BeginHaulLeg(Vector2I cell)
    {
        RefreshChunkPins();
        _awaitingTerrain = !EndpointReady(cell);
        if (_awaitingTerrain)
        {
            _follower?.CancelMove();
            _wasMoving = false;
            return true;
        }
        _wasMoving = _follower?.MoveToCell(cell) == true;
        return _wasMoving;
    }
}
