using System.Collections.Generic;
using Godot;

namespace Beep.GameBuilder;

/// <summary>
/// Genre-based project stamper. Composes the existing flat generators
/// (folders, input map, managers) with component-composed scene templates
/// to produce a complete, themed, playable starter project for a chosen genre.
///
/// Each genre produces the same navigation loop:
///   Main Menu → Game Scene → (ESC: Pause) / (fail: Game Over) → Main Menu
///
/// Every behavior is a [GlobalClass] component node. Scene templates (.tscn)
/// compose components as children — no .gd controller scripts on root nodes.
/// UI theming/effects come from the beep_ui GDScript addon (called from C#);
/// game logic comes from ecs/ C# components. GameInfo is the central C#
/// autoload every scene reads for game name / theme / tuning.
/// </summary>
public static class BeepGenreGenerator
{
    private const string SceneTemplatesDir = "res://addons/beep_game_builder_cs/templates/scenes";
    private const string GameInfoTresPath = "res://game_info.tres";
    private const string I18nTemplatePath = "res://addons/beep_game_builder_cs/templates/i18n/translations.csv";
    private const string I18nTargetPath = "res://i18n/translations.csv";

    // ════════════════════════════════════════════════════════════════
    // Public API — single data-driven entry point.
    // The genre's theme list, scene list, main scene, and tuning all come
    // from skins/<genre>/genre.json. Adding a genre = drop a folder.
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Generate a complete starter project for the given genre. All genre-specific
    /// data (themes, scenes, main scene, tuning) comes from the file-based skin
    /// catalog's genre.json — zero hardcoded genre data here.
    /// </summary>
    public static List<string> CreateProject(string genreId, GameInfo info, bool overwrite = false)
    {
        var genre = Beep.ECS.UI.SkinCatalog.GetGenre(genreId);
        if (genre == null)
        {
            GD.PushError($"[BeepGenreGenerator] Genre '{genreId}' not found in skin catalog.");
            return new List<string> { $"ERROR: Genre '{genreId}' not found." };
        }

        info.Genre = GameInfo.GenreFromId(genreId);

        // Default theme from genre.json if user didn't pick one.
        if (string.IsNullOrEmpty(info.DefaultThemePreset) || info.DefaultThemePreset == "Modern")
            info.DefaultThemePreset = genre.DefaultTheme;

        // Main scene path from genre.json.
        if (!string.IsNullOrEmpty(genre.MainScene))
            info.GameScenePath = $"res://scenes/main/{genre.MainScene}";

        // Apply tuning defaults from genre.json (only if user hasn't overridden).
        ApplyTuning(info, genre);

        return StampProject(info, genre, overwrite);
    }

    /// <summary>Apply tuning values from genre.json (gravity, move_speed, fire_rate, etc.).</summary>
    private static void ApplyTuning(GameInfo info, Beep.ECS.UI.GenreDef genre)
    {
        if (genre.Tuning.Count == 0) return;
        if (genre.Tuning.TryGetValue("gravity", out var g)) info.Gravity = g.AsSingle();
        if (genre.Tuning.TryGetValue("jump_velocity", out var j)) info.JumpVelocity = j.AsSingle();
        if (genre.Tuning.TryGetValue("move_speed", out var m)) info.MoveSpeed = m.AsSingle();
        if (genre.Tuning.TryGetValue("fire_rate", out var f)) info.FireRate = f.AsSingle();
        if (genre.Tuning.TryGetValue("grid_width", out var gw)) info.GridWidth = gw.AsInt32();
        if (genre.Tuning.TryGetValue("grid_height", out var gh)) info.GridHeight = gh.AsInt32();
        if (genre.Tuning.TryGetValue("target_score", out var ts)) info.TargetScore = ts.AsInt32();
    }

    /// <summary>
    /// Full pipeline: folders → input map → managers → shared UI scenes →
    /// genre gameplay scene + controller → autoloads → GameInfo.tres →
    /// project settings. Returns a log of every action.
    /// </summary>
    public static List<string> StampProject(GameInfo info, Beep.ECS.UI.GenreDef genre, bool overwrite)
    {
        var log = new List<string>();
        log.Add($"=== Stamping {genre.DisplayName} project: {info.GameName} ===");

        // 1) Standard scaffold (folders, defaults, input).
        log.AddRange(BeepProjectGenerator.CreateStandardFolders());
        BeepInputMapGenerator.SetupDefaultInput();
        log.Add("Input map configured.");

        // 2) Register C# autoloads ONLY — no GDScript managers.
        EnsureAutoload("GameApp", "res://addons/beep_game_builder_cs/ecs/GameApp.cs");
        EnsureAutoload("Settings", "res://addons/beep_game_builder_cs/ecs/ui/SettingsComponent.cs");
        EnsureAutoload("Locale", "res://addons/beep_game_builder_cs/ecs/ui/LocalizationComponent.cs");
        WriteGameInfoTres(info);
        EnsureAutoload("GameInfo", GameInfoTresPath);
        log.Add("C# autoloads registered (GameApp + Settings + Locale + GameInfo).");

        // 2b) Stamp the translation CSV + configure the Locale autoload to load it.
        StampTranslations(overwrite, log);

        // 4) Shared UI scenes (all genres reuse these).
        CopyUiScene("main_menu.tscn", "res://scenes/ui/main_menu.tscn", overwrite, log);
        CopyUiScene("pause_menu.tscn", "res://scenes/ui/pause_menu.tscn", overwrite, log);
        CopyUiScene("settings_menu.tscn", "res://scenes/ui/settings_menu.tscn", overwrite, log);
        CopyUiScene("game_over.tscn", "res://scenes/ui/game_over.tscn", overwrite, log);
        CopyUiScene("hud.tscn", "res://scenes/ui/hud.tscn", overwrite, log);

        // 5) Genre-specific UI scenes — from genre.json's scenes[] array.
        CopyGenreUiScenes(genre, overwrite, log);

        // 6) Genre-specific gameplay scene.
        CopyGenreScene(genre, info.GameScenePath, overwrite, log);

        // 7) Project settings from GameInfo.
        BeepProjectDefaults.ApplyFromGameInfo(info);
        log.Add($"Project settings applied (window {info.TargetResolution.X}x{info.TargetResolution.Y}, "
                + $"main scene = {info.MainMenuPath}).");

        BeepFileUtils.RefreshFilesystem();
        log.Add("=== Done. Open the main menu scene and press Play. ===");
        return log;
    }

    // ════════════════════════════════════════════════════════════════
    // Shared helpers
    // ════════════════════════════════════════════════════════════════

    private static void CopyUiScene(string templateName, string targetPath, bool overwrite, List<string> log)
        => CopyUiSceneFromPath($"{SceneTemplatesDir}/{templateName}", targetPath, overwrite, log);

    /// <summary>Loads a .tscn from <paramref name="src"/> and saves it to <paramref name="dst"/>.</summary>
    private static void CopyUiSceneFromPath(string src, string dst, bool overwrite, List<string> log)
    {
        if (!overwrite && FileAccess.FileExists(dst)) { log.Add($"Skipped (exists): {dst}"); return; }
        EnsureDir(dst);
        var packed = ResourceLoader.Load<PackedScene>(src);
        if (packed == null) { log.Add($"WARN template missing: {src}"); return; }
        Error err = ResourceSaver.Save(packed, dst);
        if (err == Error.Ok) log.Add($"Created scene: {dst}");
        else log.Add($"WARN save failed ({err}): {dst}");
    }

    /// <summary>Copy the genre's main gameplay scene. Source filename comes from genre.json's main_scene.</summary>
    private static void CopyGenreScene(Beep.ECS.UI.GenreDef genre, string gameScenePath, bool overwrite, List<string> log)
    {
        string sceneFile = !string.IsNullOrEmpty(genre.MainScene) ? genre.MainScene : $"{genre.Id}_main.tscn";
        CopyUiScene(sceneFile, gameScenePath, overwrite, log);
    }

    /// <summary>
    /// Copies the genre-specific UI scenes listed in genre.json's scenes[] array.
    /// Sources live in templates/scenes/&lt;genre&gt;/ and are copied to scenes/ui/&lt;genre&gt;/.
    /// </summary>
    private static void CopyGenreUiScenes(Beep.ECS.UI.GenreDef genre, bool overwrite, List<string> log)
    {
        string genreDir = genre.Id;
        string srcDir = $"{SceneTemplatesDir}/{genreDir}";
        string dstDir = $"res://scenes/ui/{genreDir}";

        foreach (var scene in genre.Scenes)
        {
            string src = $"{srcDir}/{scene}";
            string dst = $"{dstDir}/{scene}";
            CopyUiSceneFromPath(src, dst, overwrite, log);
        }
    }

    private static void EnsureAutoload(string name, string path)
    {
        if (!BeepProjectDefaults.HasAutoload(name))
            BeepProjectDefaults.AddAutoload(name, path);
    }

    private static void WriteGameInfoTres(GameInfo info)
    {
        // Save the resource so the autoload points at a real file, and so the
        // inspector can round-trip edits.
        Error err = ResourceSaver.Save(info, GameInfoTresPath);
        if (err != Error.Ok)
            GD.PushWarning($"[Beep Genre] Failed to save game_info.tres: {err}");
    }

    private static void EnsureDir(string path)
    {
        string dir = path.GetBaseDir();
        if (!DirAccess.DirExistsAbsolute(dir))
            DirAccess.MakeDirRecursiveAbsolute(dir);
    }

    /// <summary>Copy the sample translation CSV into the project and register the
    /// i18n path in project.godot so the Locale autoload loads it.</summary>
    private static void StampTranslations(bool overwrite, List<string> log)
    {
        EnsureDir(I18nTargetPath);
        if (overwrite || !FileAccess.FileExists(I18nTargetPath))
        {
            using var src = FileAccess.Open(I18nTemplatePath, FileAccess.ModeFlags.Read);
            if (src == null) { log.Add($"WARN: i18n template not found: {I18nTemplatePath}"); return; }
            string content = src.GetAsText();
            BeepFileUtils.SafeWriteText(I18nTargetPath, content, overwrite: true);
            log.Add($"Created translations: {I18nTargetPath}");
        }
        else
        {
            log.Add($"Skipped (exists): {I18nTargetPath}");
        }

        // Register the translation path in project settings so Godot imports it.
        ProjectSettings.SetSetting("internationalization/locale/translations", true);
        ProjectSettings.Save();
    }
}
