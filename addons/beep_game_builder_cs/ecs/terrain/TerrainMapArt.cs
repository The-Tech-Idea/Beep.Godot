using Godot;
using System;

namespace Beep.ECS;

/// <summary>Presentation only. Never changes terrain, resources, placement or navigation.</summary>
[Tool]
[GlobalClass]
public partial class TerrainMapArt : Resource
{
    [Export] public string DisplayName { get; set; } = "Cartoon";
    [Export] public bool PixelArt { get; set; }
    [Export(PropertyHint.Range, "32,128,1")] public int PixelsPerCell { get; set; } = 64;
    [Export(PropertyHint.Range, "1,32,0.5")] public float TextureRepeatCells { get; set; } = 6f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float GroundDetail { get; set; } = 0.12f;
    [Export(PropertyHint.Range, "0,0.9,0.01")] public float BlendWidth { get; set; } = 0.22f;
    [Export] public Color GrassColor { get; set; } = new(0.36f, 0.64f, 0.22f);
    [Export] public Color DryGrassColor { get; set; } = new(0.55f, 0.67f, 0.27f);
    [Export] public Color BeachColor { get; set; } = new(0.92f, 0.77f, 0.46f);
    [Export] public Color ShallowWaterColor { get; set; } = new(0.18f, 0.72f, 0.68f);
    [Export] public Color DeepWaterColor { get; set; } = new(0.06f, 0.43f, 0.51f);
    /// <summary>Optional overrides: grass, dry grass, sand, dirt, snow, mud, gravel, rock, lava.</summary>
    [Export] public Godot.Collections.Array<Texture2D> GroundTextures { get; set; } = new();
    [ExportGroup("Vegetation")]
    [Export] public Godot.Collections.Array<Texture2D> Trees { get; set; } = new();
    /// <summary>
    /// Bushes among the trees of woods and forest. Empty uses the feature renderer's own bushes
    /// sheet, as an empty Trees uses its woods sheet.
    /// </summary>
    [Export] public Godot.Collections.Array<Texture2D> Bushes { get; set; } = new();
    [Export] public Godot.Collections.Array<Texture2D> Oasis { get; set; } = new();
    [Export] public Godot.Collections.Array<Texture2D> Marsh { get; set; } = new();
    [Export] public Godot.Collections.Array<Texture2D> SmallRocks { get; set; } = new();
    [Export] public Godot.Collections.Array<Texture2D> LargeRocks { get; set; } = new();
    private readonly System.Collections.Generic.Dictionary<AtlasTexture, (Texture2D Atlas, Rect2 Region, Texture2D Result)> _groundRegions = new();

    internal Godot.Collections.Array<Texture2D> FeatureTextures(string kind)
        => kind == "oasis" ? Oasis : kind == "marsh" ? Marsh : Trees;

    internal void ApplyGround(ShaderMaterial material)
    {
        material.SetShaderParameter("art_style", PixelArt ? 2 : 1);
        material.SetShaderParameter("art_pixels_per_cell", Mathf.Clamp(PixelsPerCell, 32, 128));
        material.SetShaderParameter("art_ground_detail", Mathf.Clamp(GroundDetail, 0f, 1f));
        material.SetShaderParameter("blend_width", Mathf.Clamp(BlendWidth, 0f, 0.9f));
        material.SetShaderParameter("art_grass", new Vector3(GrassColor.R, GrassColor.G, GrassColor.B));
        material.SetShaderParameter("art_dry_grass", new Vector3(DryGrassColor.R, DryGrassColor.G, DryGrassColor.B));
        material.SetShaderParameter("art_beach", new Vector3(BeachColor.R, BeachColor.G, BeachColor.B));
        material.SetShaderParameter("art_shallow", new Vector3(ShallowWaterColor.R, ShallowWaterColor.G, ShallowWaterColor.B));
        material.SetShaderParameter("art_deep", new Vector3(DeepWaterColor.R, DeepWaterColor.G, DeepWaterColor.B));
        string[] slots = { "grass", "dry_grass", "sand", "dirt", "snow", "mud", "gravel", "rock", "lava" };
        for (int i = 0; i < Math.Min(slots.Length, GroundTextures.Count); i++)
            if (GodotObject.IsInstanceValid(GroundTextures[i]))
                material.SetShaderParameter("tex_" + slots[i], GroundTexture(GroundTextures[i]));
        material.SetShaderParameter("art_repeat_cells", Mathf.Clamp(TextureRepeatCells, 1f, 32f));
    }

    // Shader samplers need isolated repeating images, not an atlas's backing texture.
    // Cache the small authored regions once; terrain rebuilds never recrop the art.
    private Texture2D GroundTexture(Texture2D texture)
    {
        if (texture is not AtlasTexture atlas) return texture;
        if (_groundRegions.TryGetValue(atlas, out var cached) && cached.Atlas == atlas.Atlas && cached.Region == atlas.Region)
            return cached.Result;
        using var source = atlas.Atlas.GetImage();
        using var region = source.GetRegion((Rect2I)atlas.Region);
        region.GenerateMipmaps();
        var result = ImageTexture.CreateFromImage(region);
        _groundRegions[atlas] = (atlas.Atlas, atlas.Region, result);
        return result;
    }
}
