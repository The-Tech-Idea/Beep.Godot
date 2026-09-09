using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS;

/// <summary>Viewport-driven data residency. Does not own terrain or change actor simulation.</summary>
[GlobalClass]
public partial class GridCameraDemandComponent : Node
{
    [Export] public NodePath GridPath { get; set; } = new("");
    [Export] public NodePath CellDataPath { get; set; } = new("");
    /// <summary>Optional surface overview for this same world; never infer it from an unrelated scene node.</summary>
    [Export] public NodePath OverviewSurfacePath { get; set; } = new("");
    public bool IsUsingOverview { get; private set; }
    [Export] public Rect2I BoundsCells { get; set; }
    [Export(PropertyHint.Range, "0,4,1")] public int PaddingChunks { get; set; } = 1;
    private bool _enabled = true;
    [Export] public bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            if (!value) ReleaseDemand();
            if (IsInsideTree()) { SetProcess(value); if (value) RefreshDemand(); }
        }
    }
    public int DemandedChunkCount { get; private set; }
    private GridCellDataComponent? _cells;
    private Rect2I? _lastChunkBounds;

    public override void _Ready() { SetProcess(Enabled); RefreshDemand(); }
    public override void _Process(double delta) => RefreshDemand();
    public override void _ExitTree() { ReleaseDemand(); RequestReady(); }

    public bool RefreshDemand()
    {
        if (!Enabled || !IsInsideTree() || Engine.IsEditorHint()) return false;
        var grid = GetNodeOrNull<GridProjectionComponent>(GridPath);
        var cells = GetNodeOrNull<GridCellDataComponent>(CellDataPath);
        if (cells != _cells) { ReleaseDemand(); _cells = cells; }
        if (grid is null || _cells is null || !grid.IsInsideTree() || !_cells.IsInsideTree())
        {
            ReleaseDemand();
            return false;
        }
        if (BoundsCells.Size.X <= 0 || BoundsCells.Size.Y <= 0)
        {
            ReleaseDemand();
            return false;
        }
        long endX = (long)BoundsCells.Position.X + BoundsCells.Size.X - 1;
        long endY = (long)BoundsCells.Position.Y + BoundsCells.Size.Y - 1;
        if (endX > int.MaxValue || endY > int.MaxValue) return false;
        long minX = GridCellDataComponent.ChunkAxis(BoundsCells.Position.X), minY = GridCellDataComponent.ChunkAxis(BoundsCells.Position.Y);
        long maxX = GridCellDataComponent.ChunkAxis(endX), maxY = GridCellDataComponent.ChunkAxis(endY);
        IsUsingOverview = !OverviewSurfacePath.IsEmpty
            && GetNodeOrNull<TerrainSurfaceStreamingComponent>(OverviewSurfacePath) is { } surface
            && surface.CanSupplyOverview(grid.GetViewport(), BoundsCells);
        if (IsUsingOverview)
        {
            _cells.ReleaseChunkPins(this);
            _lastChunkBounds = null;
            DemandedChunkCount = 0;
            return true;
        }
        // Elevated surface picking is not affine. Retain all finite bounds until a height-aware visibility query exists.
        if (grid.ElevatedTerrainPath.IsEmpty)
        {
            var transform = grid.GetGlobalTransformWithCanvas();
            float determinant = transform.Determinant();
            if (!float.IsFinite(determinant) || determinant == 0f) return false;
            var inverse = transform.AffineInverse();
            Rect2 view = grid.GetViewport().GetVisibleRect();
            if (view.Size.X <= 0 || view.Size.Y <= 0) return false;
            Span<Vector2> corners = stackalloc Vector2[4] {
                view.Position, view.End, new(view.End.X, view.Position.Y), new(view.Position.X, view.End.Y)
            };
            long x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
            foreach (var corner in corners)
            {
                Vector2I cell = grid.WorldToCell(grid.ToGlobal(inverse * corner));
                if (cell == new Vector2I(int.MinValue, int.MinValue)) return false;
                x0 = Math.Min(x0, cell.X); y0 = Math.Min(y0, cell.Y);
                x1 = Math.Max(x1, cell.X); y1 = Math.Max(y1, cell.Y);
            }
            int padding = Math.Clamp(PaddingChunks, 0, 4);
            minX = Math.Max(minX, GridCellDataComponent.ChunkAxis(x0 - 1) - padding);
            minY = Math.Max(minY, GridCellDataComponent.ChunkAxis(y0 - 1) - padding);
            maxX = Math.Min(maxX, GridCellDataComponent.ChunkAxis(x1 + 1) + padding);
            maxY = Math.Min(maxY, GridCellDataComponent.ChunkAxis(y1 + 1) + padding);
        }
        if (minX > maxX || minY > maxY)
        {
            _cells.ReleaseChunkPins(this);
            _lastChunkBounds = null;
            DemandedChunkCount = 0;
            return true;
        }
        if ((maxX - minX + 1) * (maxY - minY + 1) > 65536) return false;
        var bounds = new Rect2I((int)minX, (int)minY, (int)(maxX - minX + 1), (int)(maxY - minY + 1));
        if (_lastChunkBounds == bounds && _cells.HasChunkPins(this)) return true;
        var chunks = new HashSet<Vector2I>();
        for (long y = minY; y <= maxY; y++)
            for (long x = minX; x <= maxX; x++) chunks.Add(new((int)x, (int)y));
        if (!_cells.ReplaceChunkPins(this, chunks)) return false;
        _lastChunkBounds = bounds;
        DemandedChunkCount = chunks.Count;
        return true;
    }

    private void ReleaseDemand()
    {
        if (GodotObject.IsInstanceValid(_cells)) _cells!.ReleaseChunkPins(this);
        _cells = null;
        _lastChunkBounds = null;
        DemandedChunkCount = 0;
        IsUsingOverview = false;
    }
}
