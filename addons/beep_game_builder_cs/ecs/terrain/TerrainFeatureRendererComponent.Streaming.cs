using Godot;

namespace Beep.ECS;

public partial class TerrainFeatureRendererComponent
{
    [ExportGroup("Residency")]
    [Export] public bool StreamLargeMaps { get; set; } = true;
    [Export(PropertyHint.Range, "0,16,0.25")] public float MinimumDetailCellPixels { get; set; } = 1.5f;
    public bool IsFeatureDetailSuppressed => _residency?.DetailSuppressed == true;
    [Export(PropertyHint.Range, "8,128,1")] public int FeatureChunkSize { get; set; } = 32;
    [Export(PropertyHint.Range, "0,4,1")] public int FeaturePreloadChunks { get; set; } = 1;
    [Export(PropertyHint.Range, "16,4096,16")] public int FeatureCellsPerFrame { get; set; } = 256;
    public int ResidentFeatureChunkCount => _residency?.ResidentChunks ?? 0;
    public int ResidentFeatureCellCount => _residency?.ResidentCells ?? 0;
    public int PendingFeatureChunkCount => _residency?.PendingChunks ?? 0;
    public int FeatureCellsProcessedLastFrame => _residency?.ProcessedCells ?? 0;
    private TerrainPropResidency<Stamp>? _residency;

    private void ResetStreaming()
    {
        _residency = null;
        SetProcess(false);
    }

    private void BeginStreaming(ITerrainSurfaceData source, Vector2I origin, Vector2I size, float tile)
    {
        var water = _cells is not null ? TerrainCoastField.CreateLiveWaterQuery(_cells, BoundsOrigin, size)
            : new System.Func<Vector2, bool>(((GeneratedTerrainField)source).IsWaterAtPosition);
        bool Dry(Vector2 at) => new Rect2(Vector2.Zero, size).HasPoint(at) && !water(at);
        _residency = new TerrainPropResidency<Stamp>(this, _grid, BoundsOrigin, size, tile, FeatureChunkSize,
            (x, y, stamps) => BuildCell(source, origin, tile, x, y, Dry, stamps));
        SetProcess(true);
    }

    private bool InvalidateFeatureCell(Vector2I absoluteCell)
    {
        if (_residency is null) return false;
        _residency.InvalidateCell(absoluteCell);
        return true;
    }

    public override void _Process(double delta) => UpdateFeatureResidency();

    public void UpdateFeatureResidency()
    {
        if (_residency is null) return;
        if ((_cells is not null && !GodotObject.IsInstanceValid(_cells))
            || (_grid is not null && !GodotObject.IsInstanceValid(_grid))
            || (_generator is not null && !GodotObject.IsInstanceValid(_generator)))
        {
            Rebuild();
            return;
        }
        float canopy = 8f * (1f + Mathf.Max(1f, Mathf.Max(SpriteAnchor.Abs().X, SpriteAnchor.Abs().Y)));
        // The SAME comparator the full rebuild uses, so a streamed map stacks its props exactly as
        // a built one does - understory first, then depth.
        if (_residency.Update(FeatureCellsPerFrame, FeaturePreloadChunks, canopy, _stamps,
                ByLayerThenDepth, MinimumDetailCellPixels)) QueueRedraw();
    }
}
