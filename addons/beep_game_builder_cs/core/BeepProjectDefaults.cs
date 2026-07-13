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
        if (ProjectSettings.HasSetting(key))
            Set(key, string.Empty);
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

        Engine.MaxFps = info.TargetFps;
    }
}
