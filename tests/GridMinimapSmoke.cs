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

        if (!StartAreaTint(cells, minimap))
            return false;

        minimap.Free();
        cells.Free();
        GD.Print("[grid-minimap] 1500x1000 bakes 750-wide at 1/2 scale (non-blank); 64x64 bakes 1:1; start areas tint toward their faction");
        return true;
    }

    /// <summary>
    /// FEAT-10: a reserved cell is tinted toward the colour of the FACTION holding that start, not
    /// toward a per-index palette - so the same player reads the same colour here as in the overlay
    /// and the lobby. Unreserved ground is left exactly as it was, and ShowStartAreas off restores
    /// the plain bake.
    /// </summary>
    private bool StartAreaTint(GridCellDataComponent cells, GridMinimapComponent minimap)
    {
        // Alpha is catalog index 1 and holds start 1, so catalog order and the assignment disagree
        // on purpose: a tint that read the catalog by start index would paint this bravo's blue.
        var alpha = new Color(0.95f, 0.15f, 0.1f);
        var catalog = new GridFactionCatalog();
        catalog.Factions.Add(new GridFactionDefinition { FactionId = "alpha", Colour = alpha });
        catalog.Factions.Add(new GridFactionDefinition { FactionId = "bravo", Colour = new Color(0.1f, 0.2f, 0.95f) });

        // Start 1 (area id 2) is a block of grass in the corner; the rest of the map is unreserved.
        var reserved = new Vector2I(3, 4);
        for (int y = 2; y < 8; y++)
            for (int x = 2; x < 8; x++)
                cells.SetMetadata(new Vector2I(x, y), "terrain_start_area", 2);

        var startArea = new GridStartAreaComponent
        {
            Name = "StartArea",
            CellDataPath = "../Cells",
            FactionCatalog = catalog,
        };
        AddChild(startArea);
        string refused = startArea.Assign("alpha", 1);
        if (refused.Length > 0)
            return Fail($"Assigning alpha to start 1 was refused: {refused}");

        minimap.StartAreaPath = "../StartArea";
        minimap.StartAreaTint = 0.2f;
        minimap.ShowStartAreas = false;
        minimap.RebuildMinimap();
        minimap.VisibleRoadCount();
        Color plain = minimap.BakedTexel(reserved.X, reserved.Y);
        Color openPlain = minimap.BakedTexel(20, 20);

        minimap.ShowStartAreas = true;
        minimap.RebuildMinimap();
        minimap.VisibleRoadCount();
        Color tinted = minimap.BakedTexel(reserved.X, reserved.Y);
        Color open = minimap.BakedTexel(20, 20);

        Color expected = GridFactionCatalog.TintToward(plain, alpha, 0.2f);
        if (!Near(tinted, expected))
            return Fail($"A cell in alpha's start area baked {tinted}, expected the ground {plain} tinted 20% toward {alpha} = {expected}");
        if (Near(tinted, plain))
            return Fail("The tint made no difference to the texel, so this check cannot fail");
        if (!Near(open, openPlain))
            return Fail($"Unreserved ground changed colour when the tint was switched on: {openPlain} became {open}");

        // A start nobody holds is not coloured in: ColourOfStart answers transparent, and
        // TintToward leaves the ground alone rather than inventing faction 1's colour.
        startArea.AutoAssign();
        minimap.RebuildMinimap();
        minimap.VisibleRoadCount();
        if (!Near(minimap.BakedTexel(reserved.X, reserved.Y), plain))
            return Fail("A start with no faction assigned was still tinted");

        startArea.Free();
        minimap.StartAreaPath = new NodePath("");
        minimap.ShowStartAreas = false;
        return true;
    }

    /// <summary>Equal within one 8-bit step: the bake quantises through an Rgba8 image.</summary>
    private static bool Near(Color left, Color right)
        => Mathf.Abs(left.R - right.R) <= 0.005f && Mathf.Abs(left.G - right.G) <= 0.005f
        && Mathf.Abs(left.B - right.B) <= 0.005f && Mathf.Abs(left.A - right.A) <= 0.005f;

    private static bool Fail(string message) { GD.PushError(message); return false; }
}
