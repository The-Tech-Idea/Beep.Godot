using Beep.ECS;
using Godot;
using System;

/// <summary>
/// FIX-01: the hillslope-diffusion coefficient (Diffusion * ErosionStrength) must be bounded at
/// the stability limit the stage's own contract states (D &lt;= 1). Above it the weighted-average
/// pass amplifies the checkerboard mode instead of relaxing it, saturating neighbouring samples
/// to alternating 0/1 before relief is classified from the surface.
/// </summary>
public partial class TerrainErosionDiffusionStabilitySmoke : Node
{
    public bool Run()
    {
        const int width = 36, height = 24;
        const float epsilon = 0.01f;

        var world = new TerrainGenerationBuffer(width, height, 1);
        var settings = default(TerrainGenerationSettings) with { ErosionStrength = 4.0f };

        // All land, nearly flat, carrying a tiny checkerboard perturbation. The flat base keeps
        // incision slopes near 2*epsilon, so diffusion - not incision - decides whether it grows.
        Array.Fill(world.Land, true);
        Array.Fill(world.Terrain, "grass");
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            world.Elevation[world.Index(x, y)] = 0.5f + (((x + y) & 1) == 0 ? epsilon : -epsilon);

        TerrainErosionStage.Apply(world, settings);

        float worst = 0f;
        for (int i = 0; i < world.Count; i++)
            if (world.Land[i])
                worst = Mathf.Max(worst, Mathf.Abs(world.Elevation[i] - 0.5f));

        const float limit = epsilon * 5f;
        if (worst > limit)
        {
            GD.PushError(
                $"[terrain-erosion-diffusion] the diffusion pass amplified the checkerboard mode at ErosionStrength 4: max deviation {worst:F4} exceeds {limit:F4}");
            return false;
        }

        return true;
    }
}
