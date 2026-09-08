using Godot;
using System.Collections.Generic;

namespace Beep.ECS;

public partial class TerrainIsometricFeatureRendererComponent
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
    private readonly List<Stamp> _residentStamps = new();

    public override void _Process(double delta) => UpdateFeatureResidency();

    public void UpdateFeatureResidency()
    {
        if (_residency is null) return;
        if (_iso is null || !GodotObject.IsInstanceValid(_iso) || !_iso.IsInsideTree())
        {
            _residency = null;
            _residentStamps.Clear();
            DistributeStamps();
            SetProcess(false);
            return;
        }
        if (_residency.Update(FeatureCellsPerFrame, FeaturePreloadChunks, 16,
                _residentStamps, (a, b) => a.Anchor.Y.CompareTo(b.Anchor.Y), MinimumDetailCellPixels)) DistributeStamps();
    }

    private void DistributeStamps()
    {
        foreach (var level in _levels) level.Stamps.Clear();
        foreach (var stamp in _residentStamps) _levels[stamp.Level].Stamps.Add(stamp);
        Redraw();
    }
}
