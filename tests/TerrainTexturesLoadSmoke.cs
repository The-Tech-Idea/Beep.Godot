using Beep.ECS;
using Godot;

/// <summary>
/// FIX-03: <see cref="TerrainTextures.Load"/> must fail SAFE on an unreadable external
/// path - warn and return null - instead of dereferencing the null Image.LoadFromFile
/// hands back. The res:// branch already kept that contract; this pins the absolute-path
/// branch, whose intended warn-and-return-null path was unreachable on a real failure.
/// </summary>
public partial class TerrainTexturesLoadSmoke : Node
{
    public bool Run()
    {
        try
        {
            bool passed = true;

            // A missing absolute path: Image.LoadFromFile returns null, and the loader must return
            // null rather than throw. The catch below turns a throw into a reported failure instead
            // of letting it abort the calling script before quit().
            passed &= Check(
                TerrainTextures.Load("/no/such/terrain_texture_probe.png", "smoke", "test sheet") is null,
                "Load returned a texture (or threw) for a missing external path");

            // The success path still loads a real on-disk PNG, so a trivially-null loader cannot
            // satisfy the assertion above.
            string bundled = ProjectSettings.GlobalizePath(
                "res://addons/beep_game_builder_cs/textures/water/surf_foam_streaks.png");
            Texture2D? loaded = TerrainTextures.Load(bundled, "smoke", "test sheet");
            passed &= Check(
                loaded is not null && loaded.GetWidth() > 0 && loaded.GetHeight() > 0,
                $"Load returned no usable texture for a real external PNG '{bundled}'");

            return passed;
        }
        catch (System.Exception exception)
        {
            return Check(false, $"Load threw for a missing external path: {exception.GetType().Name}: {exception.Message}");
        }
    }

    private static bool Check(bool ok, string message)
    {
        if (!ok)
            GD.PushError("[terrain-textures-load] " + message);
        return ok;
    }
}
