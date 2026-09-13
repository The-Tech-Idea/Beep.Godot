using Godot;

namespace Beep.ECS;

/// <summary>Projection-specific placement metadata for an authored structure scene.</summary>
[Tool, GlobalClass]
public partial class TerrainStructureLayout : Resource
{
    [Export] public Vector2I Footprint { get; set; } = Vector2I.One;
    // Scene-local point that rests on the anchor cell's center.
    [Export] public Vector2 Pivot { get; set; }
    [Export] public int RisePixels { get; set; }
    [Export] public int SortOffset { get; set; }

    public string Validate()
    {
        if (Footprint.X <= 0 || Footprint.Y <= 0) return "Structure footprint must be positive.";
        if (!Pivot.IsFinite()) return "Structure pivot must be finite.";
        if (RisePixels < 0 || RisePixels % 16 != 0) return "Structure rise must be a non-negative multiple of 16 pixels.";
        if (SortOffset < -4096 || SortOffset > 4096) return "Structure sort offset is outside the CanvasItem range.";
        return "";
    }
}
