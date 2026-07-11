using Godot;

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
}
