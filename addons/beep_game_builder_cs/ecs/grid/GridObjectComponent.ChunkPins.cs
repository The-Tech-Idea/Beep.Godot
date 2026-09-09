using Godot;

namespace Beep.ECS;

public partial class GridObjectComponent
{
    private NodePath _chunkCellDataPath = new("");
    private GridChunkPins? _pins;
    private GridChunkPins Pins => _pins ??= new GridChunkPins(this);

    /// <summary>Explicit world binding for the object's entire logical footprint, independent of occupancy.</summary>
    [Export] public NodePath ChunkCellDataPath
    {
        get => _chunkCellDataPath;
        set { _chunkCellDataPath = value; RefreshChunkPins(); }
    }

    public bool RefreshChunkPins()
    {
        if (!IsInsideTree() || Engine.IsEditorHint()) return false;
        // Accumulate the footprint's chunks before binding the store: an over-large footprint
        // declines here (WantRect returns false) and the previous pins are left untouched.
        if (!Pins.WantRect(Cell, Footprint)) return false;
        Pins.Bind(ChunkCellDataPath.IsEmpty ? null : GetNodeOrNull<GridCellDataComponent>(ChunkCellDataPath));
        return Pins.Commit();
    }

    private void ReleaseChunkPins() => _pins?.Release();
}
