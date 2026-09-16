using Godot;

namespace Beep.ECS;

/// <summary>Optional tiles-per-repeat overrides for painted ground textures. Zero inherits the
/// world's <see cref="TerrainWaterLook.GroundTextureTiles"/>, which every view reads (VIEW-04).</summary>
[Tool]
[GlobalClass]
public partial class TerrainMaterialTiling : Resource
{
    [Export(PropertyHint.Range, "0,32,0.25")] public float Grass { get; set; }
    [Export(PropertyHint.Range, "0,32,0.25")] public float DryGrass { get; set; }
    [Export(PropertyHint.Range, "0,32,0.25")] public float Sand { get; set; }
    [Export(PropertyHint.Range, "0,32,0.25")] public float Dirt { get; set; }
    [Export(PropertyHint.Range, "0,32,0.25")] public float Snow { get; set; }
    [Export(PropertyHint.Range, "0,32,0.25")] public float Mud { get; set; }
    [Export(PropertyHint.Range, "0,32,0.25")] public float Gravel { get; set; }
    [Export(PropertyHint.Range, "0,32,0.25")] public float Rock { get; set; }
    [Export(PropertyHint.Range, "0,32,0.25")] public float Lava { get; set; }
    /// <summary>Ground texture edge correction as a fraction of each repeat; zero preserves authored pixels.</summary>
    [Export(PropertyHint.Range, "0,0.125,0.005")] public float SeamBlend { get; set; }

    internal static void Apply(ShaderMaterial material, TerrainMaterialTiling? tiling)
    {
        // Bind all slots even on removal, so a previously selected profile cannot linger.
        Set("grass", tiling?.Grass ?? 0);
        Set("dry_grass", tiling?.DryGrass ?? 0);
        Set("sand", tiling?.Sand ?? 0);
        Set("dirt", tiling?.Dirt ?? 0);
        Set("snow", tiling?.Snow ?? 0);
        Set("mud", tiling?.Mud ?? 0);
        Set("gravel", tiling?.Gravel ?? 0);
        Set("rock", tiling?.Rock ?? 0);
        Set("lava", tiling?.Lava ?? 0);
        float blend = tiling?.SeamBlend ?? 0;
        material.SetShaderParameter("ground_seam_blend", float.IsFinite(blend) ? Mathf.Clamp(blend, 0, 0.125f) : 0f);

        void Set(string slot, float value) => material.SetShaderParameter(slot + "_texture_tiles",
            float.IsFinite(value) && value > 0f ? Mathf.Clamp(value, 0.25f, 32f) : 0f);
    }
}
