using Godot;
using System.Collections.Generic;

namespace Beep.ECS;

public partial class GridHaulerComponent
{
    private NodePath _chunkCellDataPath = new("");
    private GridCellDataComponent? _pinCells;
    private Vector2I _pickupCell;
    private bool _awaitingTerrain;
    private (Vector2I Depot, Vector2I Pickup, bool Cargo, bool Approaching)? _pinState;

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
        var cells = ChunkCellDataPath.IsEmpty ? null : GetNodeOrNull<GridCellDataComponent>(ChunkCellDataPath);
        if (_pinCells != cells)
        {
            if (GodotObject.IsInstanceValid(_pinCells)) _pinCells!.ReleaseChunkPins(this);
            _pinCells = cells;
            _pinState = null;
        }
        if (cells is null) return;
        var state = (DepotCell, _pickupCell, _cargoAmount > 0, State == HaulerState.MovingToPickup);
        if (_pinState == state && (!state.Item3 || cells.HasChunkPins(this))) return;
        var chunks = new HashSet<Vector2I>();
        if (_cargoAmount > 0)
        {
            chunks.Add(new(DepotCell.X >> 5, DepotCell.Y >> 5));
            if (State == HaulerState.MovingToPickup)
                chunks.Add(new(_pickupCell.X >> 5, _pickupCell.Y >> 5));
        }
        if (cells.ReplaceChunkPins(this, chunks)) _pinState = state;
    }

    private bool EndpointReady(Vector2I cell)
        => ChunkCellDataPath.IsEmpty || GodotObject.IsInstanceValid(_pinCells) && _pinCells!.IsCellAvailable(cell);

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
