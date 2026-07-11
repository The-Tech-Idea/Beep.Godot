using Godot;

namespace Beep.GameBuilder;

public static class BeepProjectDefaults
{
    public static void ConfigureDefaults()
    {
        ProjectSettings.SetSetting("display/window/size/viewport_width", 1280);
        ProjectSettings.SetSetting("display/window/size/viewport_height", 720);
        ProjectSettings.SetSetting("display/window/stretch/mode", "canvas_items");
        ProjectSettings.SetSetting("display/window/stretch/aspect", "keep");
        ProjectSettings.SetSetting("rendering/textures/canvas_textures/default_texture_filter", 0);
        ProjectSettings.Save();
    }

    public static void SetMainScene(string path)
    {
        ProjectSettings.SetSetting("application/run/main_scene", path);
        ProjectSettings.Save();
    }

    public static void AddAutoload(string name, string scriptPath)
    {
        ProjectSettings.SetSetting($"autoload/{name}", $"*{scriptPath}");
        ProjectSettings.Save();
    }

    /// <summary>Removes an autoload entry if present (symmetric with AddAutoload).</summary>
    public static void RemoveAutoload(string name)
    {
        string key = $"autoload/{name}";
        if (ProjectSettings.HasSetting(key))
        {
            ProjectSettings.SetSetting(key, string.Empty);
            ProjectSettings.Save();
        }
    }

    /// <summary>True if an autoload with this name is registered.</summary>
    public static bool HasAutoload(string name) =>
        ProjectSettings.HasSetting($"autoload/{name}");

    /// <summary>
    /// Applies window/display/config settings from a GameInfo resource — window
    /// size, OS title + version, main scene, pixel-art filter. Replaces the
    /// hardcoded 1280×720 in ConfigureDefaults() for genre-stamped projects.
    /// </summary>
    public static void ApplyFromGameInfo(GameInfo info)
    {
        ProjectSettings.SetSetting("display/window/size/viewport_width", info.TargetResolution.X);
        ProjectSettings.SetSetting("display/window/size/viewport_height", info.TargetResolution.Y);
        ProjectSettings.SetSetting("display/window/stretch/mode", "canvas_items");
        ProjectSettings.SetSetting("display/window/stretch/aspect", "keep");
        if (info.PixelArt)
            ProjectSettings.SetSetting("rendering/textures/canvas_textures/default_texture_filter", 0);

        ProjectSettings.SetSetting("application/config/name", info.GameName);
        ProjectSettings.SetSetting("application/config/version", info.Version);
        if (!string.IsNullOrEmpty(info.Description))
            ProjectSettings.SetSetting("application/config/description", info.Description);

        // Boot scene is the main menu (which reads GameInfo for the title).
        if (!string.IsNullOrEmpty(info.MainMenuPath))
            ProjectSettings.SetSetting("application/run/main_scene", info.MainMenuPath);

        Engine.MaxFps = info.TargetFps;
        ProjectSettings.Save();
    }
}
