using Godot;

namespace Beep.GameBuilder;

public static class BeepProjectDefaults
{
    /// <summary>Set a project setting WITHOUT saving — caller batches all changes then saves once.</summary>
    public static void Set(string key, Variant value) => ProjectSettings.SetSetting(key, value);

    /// <summary>Save ALL pending project settings changes in ONE call (avoids reload prompt spam).</summary>
    public static void SaveAll() => ProjectSettings.Save();

    // ── Convenience wrappers (set without saving) ──

    /// <summary>The fixed canvas every scene and HUD is authored against.
    ///
    /// This is the "design size" in Godot's own terms — NOT the player's resolution. Every
    /// hardcoded pixel in the UI (a 26px header, a 13px font, a 34px button) is a coordinate in
    /// THIS space, and the canvas_items stretch scales the whole thing to whatever window the
    /// player actually has. Keeping it fixed is what makes those numbers resolution-independent.
    /// </summary>
    public const int DesignWidth = 1280;
    public const int DesignHeight = 720;

    public static void ConfigureDefaults()
    {
        Set("display/window/size/viewport_width", DesignWidth);
        Set("display/window/size/viewport_height", DesignHeight);
        ApplyStretch();
        Set("rendering/textures/canvas_textures/default_texture_filter", 0);
        RegisterUiRegister();
    }

    /// <summary>Publish the global UI art register into Project Settings, with the editor hints
    /// that make each one a real control rather than a raw value.
    ///
    /// These are project-wide because a game has ONE art direction. They were per-scene
    /// [Export]s on ThemePresetComponent, which meant the answer to "does this game use heavy
    /// game-art chrome" was stored separately in every scene file, drifted between them, and
    /// could only be changed by editing all of them.</summary>
    public static void RegisterUiRegister()
    {
        Reg(Beep.ECS.UI.SkinCatalog.SettingChrome, true,
            Variant.Type.Bool, PropertyHint.None, "");
        Reg(Beep.ECS.UI.SkinCatalog.SettingOutline, 3,
            Variant.Type.Int, PropertyHint.Range, "0,8,1");
        Reg(Beep.ECS.UI.SkinCatalog.SettingShadow, 4,
            Variant.Type.Int, PropertyHint.Range, "0,12,1");
        Reg(Beep.ECS.UI.SkinCatalog.SettingHudOpacity, 0.82f,
            Variant.Type.Float, PropertyHint.Range, "0.3,1.0,0.01");
        Beep.ECS.UI.SkinCatalog.RefreshRegisterSettings();
    }

    /// <summary>Declare a setting without clobbering a value the developer already chose.</summary>
    private static void Reg(string key, Variant value, Variant.Type type, PropertyHint hint, string hintString)
    {
        if (!ProjectSettings.HasSetting(key)) ProjectSettings.SetSetting(key, value);
        ProjectSettings.SetInitialValue(key, value);
        var info = new Godot.Collections.Dictionary
        {
            { "name", key },
            { "type", (int)type },
            { "hint", (int)hint },
            { "hint_string", hintString },
        };
        ProjectSettings.AddPropertyInfo(info);
    }

    /// <summary>Canvas scaling, shared by both entry points so they cannot drift.
    ///
    /// aspect = "expand" rather than "keep": "keep" letterboxes anything that is not the design
    /// aspect, so on a 21:9 monitor a HUD anchored to the screen edge is anchored to the edge of
    /// a black bar instead. "expand" grows the viewport on the wider axis, and Control anchors
    /// then land on the REAL screen corners at any aspect ratio — which is the whole point of
    /// anchoring the HUD rather than positioning it.</summary>
    public static void ApplyStretch()
    {
        Set("display/window/stretch/mode", "canvas_items");
        Set("display/window/stretch/aspect", "expand");
    }

    /// <summary>The player's window size, which is a DIFFERENT setting from the design canvas.
    ///
    /// Writing the chosen resolution into viewport_width/height (what this used to do) redefined
    /// the design canvas instead of the window: picking 1920x1080 made every UI coordinate
    /// authored for 1280x720 render at two-thirds the intended proportion, so panels and fonts
    /// came out too small at exactly the resolutions meant to look better.</summary>
    public static void SetWindowSize(int width, int height)
    {
        Set("display/window/size/window_width_override", width);
        Set("display/window/size/window_height_override", height);
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
        // The chosen resolution sizes the WINDOW; the design canvas stays fixed so the UI keeps
        // its authored proportions at every resolution. See SetWindowSize.
        Set("display/window/size/viewport_width", DesignWidth);
        Set("display/window/size/viewport_height", DesignHeight);
        SetWindowSize(info.TargetResolution.X, info.TargetResolution.Y);
        ApplyStretch();
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
