using Beep.ECS;
using Godot;

// ENH-13: GridMinimapComponent.BakeTerrain silently returned on any map wider or taller than 1024
// cells, so ShowTerrain was accepted and drew nothing on a large map. It now bakes a downsampled
// overview (one texel per s x s block, majority terrain kind). This probe bakes a 1500x1000 map and
// asserts the minimap chose 1/2 scale, produced a 750-wide non-blank texture, and still bakes a
// small map at 1:1. The mutation the guard catches: restoring the silent return blanks the texture.
[GlobalClass]
public partial class GridMinimapSmoke : Node
{
    public bool Run()
    {
        var cells = new GridCellDataComponent { Name = "Cells" };
        AddChild(cells);
        for (int y = 0; y < 30; y++)
            for (int x = 0; x < 30; x++)
                cells.SetTerrainKind(new Vector2I(x, y), "grass");

        var minimap = new GridMinimapComponent
        {
            Name = "Minimap",
            CellDataPath = "../Cells",
            ShowTerrain = true,
            PreferNavigationBounds = false,
            BoundsOrigin = Vector2I.Zero,
            BoundsSize = new Vector2I(1500, 1000),
        };
        AddChild(minimap);

        // VisibleRoadCount runs ResolveReferences + RefreshSnapshots, which bakes the (dirty) terrain.
        minimap.VisibleRoadCount();

        if (minimap.TerrainScale != 2)
            return Fail($"A 1500x1000 map should bake at 1/2 scale, got 1/{minimap.TerrainScale}");
        if (minimap.TerrainTextureWidth != 750)
            return Fail($"A 1500x1000 map at 1/2 scale should bake a 750-wide texture, got {minimap.TerrainTextureWidth}");

        // The grass block at the origin must be painted, not blank.
        Color texel = minimap.BakedTexel(0, 0);
        if (texel.A < 0.5f)
            return Fail("The baked terrain texture is blank where grass was set");

        // A small map still bakes one texel per cell (no regression, no downsample).
        minimap.BoundsSize = new Vector2I(64, 64);
        minimap.RebuildMinimap();
        minimap.VisibleRoadCount();
        if (minimap.TerrainScale != 1)
            return Fail($"A 64x64 map should bake at 1:1, got 1/{minimap.TerrainScale}");
        if (minimap.TerrainTextureWidth != 64)
            return Fail($"A 64x64 map should bake a 64-wide texture, got {minimap.TerrainTextureWidth}");

        minimap.Free();
        cells.Free();
        GD.Print("[grid-minimap] 1500x1000 bakes 750-wide at 1/2 scale (non-blank); 64x64 bakes 1:1");
        return true;
    }

    private static bool Fail(string message) { GD.PushError(message); return false; }
}
