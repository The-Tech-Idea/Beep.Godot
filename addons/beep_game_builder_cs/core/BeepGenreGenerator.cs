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

    /// <summary>Regeneration mode passed to CreateProject.</summary>
    public enum RegenMode
    {
        /// <summary>Don't touch any existing file (safest — current default).</summary>
        SkipExisting,
        /// <summary>Overwrite everything (nuclear — destroys user edits).</summary>
        OverwriteAll,
    }

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
    public static List<string> CreateProject(string genreId, GameInfo info, RegenMode mode = RegenMode.SkipExisting)
    {
        return CreateProject(genreId, info, mode, out _);
    }

    /// <summary>Back-compat overload (bool overwrite → RegenMode).</summary>
    public static List<string> CreateProject(string genreId, GameInfo info, bool overwrite)
        => CreateProject(genreId, info, overwrite ? RegenMode.OverwriteAll : RegenMode.SkipExisting);

    private static List<string> CreateProject(string genreId, GameInfo info, RegenMode mode, out bool anyError)
    {
        anyError = false;
        var genre = Beep.ECS.UI.SkinCatalog.GetGenre(genreId);
        if (genre == null)
        {
            anyError = true;
            GD.PushError($"[BeepGenreGenerator] Genre '{genreId}' not found in skin catalog.");
            return new List<string> { $"ERROR: Genre '{genreId}' not found." };
        }

        info.GenreId = genreId;

        // Default theme from genre.json if user didn't pick one.
        if (string.IsNullOrEmpty(info.DefaultThemePreset) || info.DefaultThemePreset == "Modern")
            info.DefaultThemePreset = genre.DefaultTheme;

        // Main scene path from genre.json.
        if (!string.IsNullOrEmpty(genre.MainScene))
            info.GameScenePath = $"res://scenes/main/{genre.MainScene}";

        // Apply tuning defaults from genre.json (only if user hasn't overridden).
        ApplyTuning(info, genre);

        // Point GameInfo's genre-specific scene paths at THIS genre's screens.
        ApplyNavWiring(info, genre);

        return StampProject(info, genre, mode);
    }

    /// <summary>
    /// Point GameInfo's genre-specific scene paths at this genre's own screens, using the
    /// <c>nav_wiring</c> block in genre.json.
    ///
    /// Why this is data and not a default: GameInfo's genre paths are hardcoded to the four
    /// original genres, so e.g. LevelCompletePath defaulted to the PUZZLE end screen. Since
    /// it is never empty, GameFlowComponent's "LevelCompletePath, else LevelResultsPath"
    /// fallback could never reach the second branch — every genre, platformer included,
    /// finished a level on the puzzle screen. Declaring the routes per genre means adding a
    /// genre stays "drop a folder": no new C# properties, no more drift.
    ///
    /// Shape — GameInfo property name to a scene, relative to this genre's UI folder
    /// (a full res:// path is used verbatim):
    /// <code>
    ///   "nav_wiring": {
    ///     "LevelCompletePath": "level_results.tscn",
    ///     "LevelSelectPath":   "level_select.tscn"
    ///   }
    /// </code>
    /// Applied at generation time and saved into game_info.tres — and also at runtime by
    /// BeepGenreScene, which is the entry point the README's quick start uses. Both callers
    /// must go through here: when only the generator applied it, a project set up the
    /// README way kept the hardcoded genre-path defaults and still sent every genre to the
    /// puzzle end screen. Uses no editor API, so it is safe to call while the game runs.
    /// </summary>
    public static void ApplyNavWiring(GameInfo info, Beep.ECS.UI.GenreDef genre)
    {
        // Clear every genre-specific path first so nav_wiring is the ONLY source. Their
        // defaults point at the four original genres' scenes, so anything left undeclared
        // would silently inherit another genre's screen — that is exactly how every genre
        // ended up on the puzzle level-complete screen. Undeclared now means "this genre
        // has no such screen", and GameFlowComponent falls through to game over.
        foreach (string property in GenreScenePathProperties)
            info.Set(property, "");

        // Clear the open registry for the same reason: nav_wiring must be the ONLY source,
        // so re-generating for a different genre can't leave the previous genre's screens
        // resolvable.
        info.GenreScenePaths.Clear();

        foreach (var key in genre.NavWiring.Keys)
        {
            string property = key.AsString();
            string target = genre.NavWiring[key].AsString();
            if (string.IsNullOrEmpty(property) || string.IsNullOrEmpty(target)) continue;

            string path = target.StartsWith("res://")
                ? target
                : $"res://scenes/ui/{genre.Id}/{target}";

            // A key naming a GameInfo property sets it; anything else is a genre screen with
            // no dedicated property and goes in the open registry. That distinction is what
            // lets a genre declare screens the original four never had — previously an
            // unknown key was rejected outright, so cardgame/citybuilder/rpg/strategy/
            // survival/topdown could not route to ANY of their own screens, and no
            // genre.json could express it.
            if (HasGameInfoProperty(info, property))
                info.Set(property, path);
            else
                info.GenreScenePaths[property] = path;
        }
    }

    /// <summary>GameInfo's genre-specific scene paths. Shared paths (main menu, game,
    /// settings, game over, pause) are deliberately NOT here — every genre uses those.</summary>
    private static readonly string[] GenreScenePathProperties =
    {
        // Shared concept, per-genre target: where New Game leads. Blanked like the rest, so
        // a genre that doesn't declare one sends New Game straight to the game.
        "NewGameScenePath",
        "LevelSelectPath", "LevelResultsPath",
        "CharacterSelectPath", "LevelUpPath", "RunResultsPath", "CodexPath",
        "LevelMapPath", "PreLevelPath", "LevelCompletePath", "LevelFailedPath"
    };

    private static bool HasGameInfoProperty(GameInfo info, string property)
    {
        foreach (var entry in info.GetPropertyList())
            if (entry["name"].AsString() == property) return true;
        return false;
    }

    /// <summary>
    /// Apply tuning values from genre.json (gravity, move_speed, fire_rate, weather,
    /// seasons, save/load…). Missing keys are left untouched.
    ///
    /// Public for the same reason as ApplyNavWiring above: BeepGenreScene is the runtime
    /// entry point the README's quick start uses, and it had its own fork of this that
    /// recognised only the 7 gameplay keys. A project set up the README way therefore
    /// silently got none of the weather, season or save/load tuning its genre declared.
    /// Both callers must go through here. Uses no editor API, so it is safe at runtime.
    /// </summary>
    public static void ApplyTuning(GameInfo info, Beep.ECS.UI.GenreDef genre)
    {
        if (genre.Tuning.Count == 0) return;
        if (genre.Tuning.TryGetValue("gravity", out var g)) info.Gravity = g.AsSingle();
        if (genre.Tuning.TryGetValue("jump_velocity", out var j)) info.JumpVelocity = j.AsSingle();
        if (genre.Tuning.TryGetValue("move_speed", out var m)) info.MoveSpeed = m.AsSingle();
        if (genre.Tuning.TryGetValue("fire_rate", out var f)) info.FireRate = f.AsSingle();
        if (genre.Tuning.TryGetValue("grid_width", out var gw)) info.GridWidth = gw.AsInt32();
        if (genre.Tuning.TryGetValue("grid_height", out var gh)) info.GridHeight = gh.AsInt32();
        if (genre.Tuning.TryGetValue("target_score", out var ts)) info.TargetScore = ts.AsInt32();
        if (genre.Tuning.TryGetValue("enable_weather", out var ew)) info.EnableWeather = ew.AsBool();
        if (genre.Tuning.TryGetValue("enable_day_night", out var dn)) info.EnableDayNightCycle = dn.AsBool();
        if (genre.Tuning.TryGetValue("default_weather", out var dw))
        {
            // Warn rather than fall through silently: "Rainy" (the member is Rain) used to
            // leave DefaultWeather at its default with no diagnostic at all.
            if (System.Enum.TryParse<Beep.ECS.WeatherSystemComponent.WeatherType>(dw.AsString(), true, out var parsedWeather))
                info.DefaultWeather = parsedWeather;
            else
                GD.PushWarning($"[Beep Genre] '{genre.Id}': tuning.default_weather = '{dw.AsString()}' is not a WeatherType — ignored.");
        }
        if (genre.Tuning.TryGetValue("enable_seasons", out var es)) info.EnableSeasons = es.AsBool();
        if (genre.Tuning.TryGetValue("default_season", out var ds))
        {
            if (System.Enum.TryParse<Beep.ECS.SeasonalComponent.Season>(ds.AsString(), true, out var parsedSeason))
                info.DefaultSeason = parsedSeason;
            else
                GD.PushWarning($"[Beep Genre] '{genre.Id}': tuning.default_season = '{ds.AsString()}' is not a Season — ignored.");
        }
        if (genre.Tuning.TryGetValue("days_per_season", out var dps)) info.DaysPerSeason = dps.AsDouble();
        if (genre.Tuning.TryGetValue("enable_temperature", out var et)) info.EnableTemperature = et.AsBool();
        if (genre.Tuning.TryGetValue("ambient_temperature", out var at)) info.AmbientTemperature = at.AsSingle();
        if (genre.Tuning.TryGetValue("enable_forecast", out var ef)) info.EnableWeatherForecast = ef.AsBool();
        if (genre.Tuning.TryGetValue("forecast_days", out var fd)) info.ForecastDays = fd.AsInt32();
        if (genre.Tuning.TryGetValue("enable_save_load", out var esl)) info.EnableGameStateManager = esl.AsBool();
        if (genre.Tuning.TryGetValue("max_save_slots", out var mss)) info.MaxSaveSlots = mss.AsInt32();
        if (genre.Tuning.TryGetValue("autosave_enabled", out var ae)) info.AutosaveEnabled = ae.AsBool();
        if (genre.Tuning.TryGetValue("autosave_interval_seconds", out var ais)) info.AutosaveIntervalSeconds = ais.AsSingle();

        WarnUnknownTuning(genre);
    }

    /// <summary>Every tuning key ApplyTuning above actually consumes.</summary>
    private static readonly System.Collections.Generic.HashSet<string> KnownTuningKeys = new()
    {
        "gravity", "jump_velocity", "move_speed", "fire_rate",
        "grid_width", "grid_height", "target_score",
        "enable_weather", "enable_day_night", "default_weather",
        "enable_seasons", "default_season", "days_per_season",
        "enable_temperature", "ambient_temperature",
        "enable_forecast", "forecast_days",
        "enable_save_load", "max_save_slots", "autosave_enabled", "autosave_interval_seconds",
    };

    /// <summary>Report tuning keys nothing reads. Several genres ship blocks that look like
    /// configuration but are decoration (cardgame's hand_limit, rpg's inventory_columns,
    /// racing's lap_count…) — silence made them indistinguishable from working settings.</summary>
    private static void WarnUnknownTuning(Beep.ECS.UI.GenreDef genre)
    {
        foreach (var key in genre.Tuning.Keys)
        {
            string name = key.ToString();
            if (!KnownTuningKeys.Contains(name))
                GD.PushWarning($"[Beep Genre] '{genre.Id}': tuning.{name} is not read by anything — it has no effect.");
        }
    }

    /// <summary>
    /// Full pipeline: folders → input map → managers → shared UI scenes →
    /// genre gameplay scene + controller → autoloads → GameInfo.tres →
    /// project settings. Returns a log of every action.
    /// </summary>
    public static List<string> StampProject(GameInfo info, Beep.ECS.UI.GenreDef genre, RegenMode mode)
    {
        var log = new List<string>();
        log.Add($"=== Stamping {genre.DisplayName} project: {info.GameName} (mode: {mode}) ===");

        // 1) Standard scaffold (folders, defaults, input).
        log.AddRange(BeepProjectGenerator.CreateStandardFolders());
        BeepInputMapGenerator.SetupDefaultInput();
        log.Add("Input map configured.");

        // 2) Register C# autoloads ONLY — no GDScript managers.
        EnsureAutoload("GameApp", "res://addons/beep_game_builder_cs/ecs/GameApp.cs");
        EnsureAutoload("Settings", "res://addons/beep_game_builder_cs/ecs/ui/SettingsComponent.cs");
        EnsureAutoload("Locale", "res://addons/beep_game_builder_cs/ecs/ui/LocalizationComponent.cs");

        // GameStateManager must outlive scene changes: the save/load menus live in
        // main_menu.tscn, while gameplay is a different scene. As a per-scene node it
        // was never in the tree at the same time as the menus that call it, so
        // SaveLoadManager could never find it ("GameStateManager not found").
        // It discovers ISaveables from GetTree().Root, so an autoload works unchanged.
        if (info.EnableGameStateManager)
            EnsureAutoload("GameStateManager", "res://addons/beep_game_builder_cs/ecs/GameStateManagerComponent.cs");
        WriteGameInfoTres(info);
        // GameInfo is a Resource, not a Node — it CANNOT be autoloaded directly.
        // Instead, GameApp (the Node autoload above) loads game_info.tres in its
        // _Ready and exposes it via GameApp.Info. GameInfo.Instance is a convenience
        // accessor that reads from the tree if a GameInfo node somehow exists.
        log.Add("C# autoloads registered (GameApp + Settings + Locale). GameInfo loaded by GameApp.");

        // 2b) Stamp the translation CSV + configure the Locale autoload to load it.
        StampTranslations(mode != RegenMode.SkipExisting, log);

        // 4) Shared UI scenes (all genres reuse these).
        CopyUiScene("main_menu.tscn", "res://scenes/ui/main_menu.tscn", mode, log);
        CopyUiScene("pause_menu.tscn", "res://scenes/ui/pause_menu.tscn", mode, log);
        CopyUiScene("settings_menu.tscn", "res://scenes/ui/settings_menu.tscn", mode, log);
        CopyUiScene("game_over.tscn", "res://scenes/ui/game_over.tscn", mode, log);
        CopyUiScene("hud.tscn", "res://scenes/ui/hud.tscn", mode, log);
        CopyUiScene("save_game_menu.tscn", "res://scenes/ui/save_game_menu.tscn", mode, log);
        CopyUiScene("load_game_menu.tscn", "res://scenes/ui/load_game_menu.tscn", mode, log);

        // 4b) Building-block templates (player, enemy, pickup, ...). These shipped in the
        // addon but were never copied, so they sat in addons/ where nobody would find them
        // — while CreateStandardFolders dutifully created res://scenes/player, /npc and
        // /projectiles and left them empty. Copy each into the folder made for it.
        // They keep their _template suffix: they are starting points to duplicate, not
        // scenes the game loads by path.
        CopyUiScene("player_template.tscn", "res://scenes/player/player_template.tscn", mode, log);
        CopyUiScene("enemy_template.tscn", "res://scenes/npc/enemy_template.tscn", mode, log);
        CopyUiScene("robot_npc_template.tscn", "res://scenes/npc/robot_npc_template.tscn", mode, log);
        CopyUiScene("projectile_template.tscn", "res://scenes/projectiles/projectile_template.tscn", mode, log);
        CopyUiScene("pickup_template.tscn", "res://scenes/effects/pickup_template.tscn", mode, log);
        CopyUiScene("dialog_template.tscn", "res://scenes/ui/dialog_template.tscn", mode, log);

        // 5) All genre-specific UI scenes and gameplay scenes (not just the selected genre).
        foreach (var g in Beep.ECS.UI.SkinCatalog.AllGenres.Values)
        {
            CopyGenreUiScenes(g, mode, log);
            if (!IsSafeSceneFileName(g.MainScene))
            {
                log.Add($"WARN: genre '{g.Id}' has an unusable main_scene '{g.MainScene}' — skipped.");
                continue;
            }
            string gScenePath = $"res://scenes/main/{g.MainScene}";
            CopyGenreScene(g, gScenePath, mode, log);
        }

        // 6) Project settings from GameInfo.
        BeepProjectDefaults.ApplyFromGameInfo(info);

        // 9) Save ALL project settings in ONE call (avoids reload prompt spam).
        BeepProjectDefaults.SaveAll();
        log.Add($"Project settings applied (window {info.TargetResolution.X}x{info.TargetResolution.Y}, "
                + $"main scene = {info.MainMenuPath}).");

        BeepFileUtils.RefreshFilesystem();
        log.Add("=== Done. Open the main menu scene and press Play. ===");
        return log;
    }

    // ════════════════════════════════════════════════════════════════
    // Shared helpers
    // ════════════════════════════════════════════════════════════════

    private static void CopyUiScene(string templateName, string targetPath, RegenMode mode, List<string> log)
        => CopyUiSceneFromPath($"{SceneTemplatesDir}/{templateName}", targetPath, mode, log);

    /// <summary>
    /// Load a .tscn template and save it to dst, always. A scene's script wiring
    /// lives entirely in the template — there is no per-project variant worth
    /// preserving — so every generation always copies fresh rather than risk a
    /// stale, scriptless copy silently surviving a regen. The mode parameter is
    /// kept for other generator steps and intentionally unused here.
    /// </summary>
    private static void CopyUiSceneFromPath(string src, string dst, RegenMode mode, List<string> log)
    {
        EnsureDir(dst);

        // Just COPY the .tscn file — no instantiate, no pack, no owner issues.
        // The template files are ready-to-use scenes; copying them verbatim is
        // the fastest and safest approach (no node tree manipulation needed).
        if (!FileAccess.FileExists(src))
        {
            log.Add($"WARN template missing: {src}");
            return;
        }

        using var srcFile = FileAccess.Open(src, FileAccess.ModeFlags.Read);
        if (srcFile == null)
        {
            log.Add($"WARN: cannot read template: {src}");
            return;
        }
        string content = srcFile.GetAsText();

        // Honor the caller's mode. Both real callers ask for SkipExisting, and these
        // destinations are scenes the user is meant to edit in place (player_template
        // and enemy_template are documented as "duplicate me" starting points) — an
        // unconditional overwrite silently discarded their work on every regenerate.
        bool ok = BeepFileUtils.SafeWriteText(dst, content, overwrite: mode == RegenMode.OverwriteAll);
        if (ok) log.Add($"Copied: {dst}");
        else if (FileAccess.FileExists(dst)) log.Add($"Skipped (exists): {dst}");
        else log.Add($"WARN copy failed: {dst}");
    }

    /// <summary>Copy the genre's main gameplay scene. Source filename comes from genre.json's main_scene.</summary>
    private static void CopyGenreScene(Beep.ECS.UI.GenreDef genre, string gameScenePath, RegenMode mode, List<string> log)
    {
        string sceneFile = !string.IsNullOrEmpty(genre.MainScene) ? genre.MainScene : $"{genre.Id}_main.tscn";
        CopyUiScene(sceneFile, gameScenePath, mode, log);
    }

    /// <summary>
    /// Copies the genre-specific UI scenes listed in genre.json's scenes[] array.
    /// Sources live in templates/scenes/&lt;genre&gt;/ and are copied to scenes/ui/&lt;genre&gt;/.
    /// </summary>
    private static void CopyGenreUiScenes(Beep.ECS.UI.GenreDef genre, RegenMode mode, List<string> log)
    {
        string genreDir = genre.Id;
        string srcDir = $"{SceneTemplatesDir}/{genreDir}";
        string dstDir = $"res://scenes/ui/{genreDir}";

        foreach (var scene in genre.Scenes)
        {
            if (!IsSafeSceneFileName(scene))
            {
                log.Add($"WARN: genre '{genre.Id}' declares an unusable scene name '{scene}' — skipped.");
                continue;
            }
            string src = $"{srcDir}/{scene}";
            string dst = $"{dstDir}/{scene}";
            CopyUiSceneFromPath(src, dst, mode, log);
        }
    }

    /// <summary>Whether a catalog-supplied scene filename is safe to concatenate into a write
    /// path. genre.json is the documented "drop a folder" extension point, so these strings
    /// are third-party input — a main_scene of "../../project.godot" would otherwise be
    /// written straight outside the target folder. Also rejects empty, which used to produce
    /// a destination ending in a bare slash.</summary>
    private static bool IsSafeSceneFileName(string name)
        => !string.IsNullOrWhiteSpace(name)
           && !name.Contains("..")
           && !name.Contains('/')
           && !name.Contains('\\')
           && !name.Contains(':');

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

        // No project setting is written here on purpose.
        //
        // This used to do Set("internationalization/locale/translations", true). That key
        // expects a PackedStringArray of *.translation files (what Godot's CSV importer
        // produces) — a bool is meaningless there and registered nothing.
        //
        // The addon doesn't use Godot's importer anyway: the Locale autoload
        // (LocalizationComponent) parses the CSV itself at runtime and registers a
        // Translation per language column with the TranslationServer. Its TranslationPaths
        // defaults to I18nTargetPath, so the file stamped just above is picked up with no
        // project setting involved.
    }
}
