using Godot;

namespace Beep.ECS;

public partial class TerrainReliefRendererComponent
{
    [ExportGroup("Residency")]
    [Export] public bool StreamLargeMaps { get; set; } = true;
    [Export(PropertyHint.Range, "0,16,0.25")] public float MinimumDetailCellPixels { get; set; } = 1.5f;
    public bool IsReliefDetailSuppressed => _residency?.DetailSuppressed == true;
    [Export(PropertyHint.Range, "8,128,1")] public int ReliefChunkSize { get; set; } = 32;
    [Export(PropertyHint.Range, "0,4,1")] public int ReliefPreloadChunks { get; set; } = 1;
    [Export(PropertyHint.Range, "16,4096,16")] public int ReliefCellsPerFrame { get; set; } = 256;
    public int ResidentReliefChunkCount => _residency?.ResidentChunks ?? 0;
    public int ResidentReliefCellCount => _residency?.ResidentCells ?? 0;
    public int PendingReliefChunkCount => _residency?.PendingChunks ?? 0;
    public int ReliefCellsProcessedLastFrame => _residency?.ProcessedCells ?? 0;
    private TerrainPropResidency<Stamp>? _residency;

    private void ResetStreaming()
    {
        _residency = null;
        SetProcess(false);
    }

    private void BeginStreaming(ITerrainSurfaceData field, Vector2I size, float tile)
    {
        var water = _cells is not null ? TerrainCoastField.CreateLiveWaterQuery(_cells, BoundsOrigin, size)
            : new System.Func<Vector2, bool>(((GeneratedTerrainField)field).IsWaterAtPosition);
        bool Dry(Vector2 at) => new Rect2(Vector2.Zero, size).HasPoint(at) && !water(at);
        _residency = new TerrainPropResidency<Stamp>(this, _grid, BoundsOrigin, size, tile, ReliefChunkSize,
            (x, y, stamps) => BuildCell(field, tile, x, y, Dry, stamps));
        SetProcess(true);
    }

    public override void _Process(double delta) => UpdateReliefResidency();

    public void UpdateReliefResidency()
    {
        if (_residency is null) return;
        if ((_cells is not null && !GodotObject.IsInstanceValid(_cells))
            || (_grid is not null && !GodotObject.IsInstanceValid(_grid))
            || (_generator is not null && !GodotObject.IsInstanceValid(_generator)))
        {
            Rebuild();
            return;
        }
        // Size policy caps rocks at eight cells; centered artwork extends half
        // that width beyond its anchor, plus sub-cell scatter.
        if (_residency.Update(ReliefCellsPerFrame, ReliefPreloadChunks, 5f, _stamps,
                (a, b) => a.SortY.CompareTo(b.SortY), MinimumDetailCellPixels)) QueueRedraw();
    }
}
