using Godot;

namespace Beep.GameBuilder;

public static class BeepProjectDefaults
{
    /// <summary>Set a project setting WITHOUT saving — caller batches all changes then saves once.</summary>
    public static void Set(string key, Variant value) => ProjectSettings.SetSetting(key, value);

    /// <summary>Save ALL pending project settings changes in ONE call (avoids reload prompt spam).</summary>
    public static void SaveAll() => ProjectSettings.Save();

    // ── Convenience wrappers (set without saving) ──

    public static void ConfigureDefaults()
    {
        Set("display/window/size/viewport_width", 1280);
        Set("display/window/size/viewport_height", 720);
        Set("display/window/stretch/mode", "canvas_items");
        Set("display/window/stretch/aspect", "keep");
        Set("rendering/textures/canvas_textures/default_texture_filter", 0);
    }

    public static void SetMainScene(string path)
        => Set("application/run/main_scene", path);

    public static void AddAutoload(string name, string scriptPath)
        => Set($"autoload/{name}", $"*{scriptPath}");

    public static void RemoveAutoload(string name)
    {
        string key = $"autoload/{name}";
        // Clear() actually removes the key. Set(key, "") left an EMPTY autoload entry behind, and
        // HasSetting/HasAutoload still reported true for it — so EnsureAutoload (which only adds when
        // !HasAutoload) would later REFUSE to re-register an autoload a subsequent genre needs, leaving
        // it permanently empty. Clearing the key lets the re-enable path work and drops the dead entry
        // from project.godot. Persisted by the caller's SaveAll().
        if (ProjectSettings.HasSetting(key))
            ProjectSettings.Clear(key);
    }

    public static bool HasAutoload(string name) =>
        ProjectSettings.HasSetting($"autoload/{name}");

    public static void ApplyFromGameInfo(GameInfo info)
    {
        Set("display/window/size/viewport_width", info.TargetResolution.X);
        Set("display/window/size/viewport_height", info.TargetResolution.Y);
        Set("display/window/stretch/mode", "canvas_items");
        Set("display/window/stretch/aspect", "keep");
        if (info.PixelArt)
            Set("rendering/textures/canvas_textures/default_texture_filter", 0);

        Set("application/config/name", info.GameName);
        Set("application/config/version", info.Version);
        if (!string.IsNullOrEmpty(info.Description))
            Set("application/config/description", info.Description);

        if (!string.IsNullOrEmpty(info.MainMenuPath))
            Set("application/run/main_scene", info.MainMenuPath);

        // Write the project setting rather than Engine.MaxFps. This runs in the editor at
        // generation time, so assigning Engine.MaxFps capped the EDITOR's framerate and
        // never reached the generated game (it isn't persisted). The project setting is
        // saved to project.godot and applies when the game runs.
        if (info.TargetFps > 0)
            Set("application/run/max_fps", info.TargetFps);
    }
}
