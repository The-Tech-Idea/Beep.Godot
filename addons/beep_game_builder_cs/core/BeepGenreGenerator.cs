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

    /// <summary>Meta key stamped on every generated scene so we can detect
    /// unmodified scenes on regeneration (safe to overwrite) vs user-edited
    /// ones (should not be overwritten).</summary>
    private const string GeneratedMetaKey = "_beep_generated";
    /// <summary>Version of the generator — bump when templates change.
    /// Scenes stamped with an older version are "stale" and get refreshed.</summary>
    private const string GeneratorVersion = "0.5.0";

    /// <summary>Regeneration mode passed to CreateProject.</summary>
    public enum RegenMode
    {
        /// <summary>Don't touch any existing file (safest — current default).</summary>
        SkipExisting,
        /// <summary>Only overwrite scenes that still have the _beep_generated stamp
        /// (user hasn't edited them). Preserves user-modified scenes.</summary>
        UpdateUnmodified,
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

        info.Genre = GameInfo.GenreFromId(genreId);

        // Default theme from genre.json if user didn't pick one.
        if (string.IsNullOrEmpty(info.DefaultThemePreset) || info.DefaultThemePreset == "Modern")
            info.DefaultThemePreset = genre.DefaultTheme;

        // Main scene path from genre.json.
        if (!string.IsNullOrEmpty(genre.MainScene))
            info.GameScenePath = $"res://scenes/main/{genre.MainScene}";

        // Apply tuning defaults from genre.json (only if user hasn't overridden).
        ApplyTuning(info, genre);

        return StampProject(info, genre, mode);
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

        // 5) Genre-specific UI scenes — from genre.json's scenes[] array.
        CopyGenreUiScenes(genre, mode, log);

        // 6) Genre-specific gameplay scene.
        CopyGenreScene(genre, info.GameScenePath, mode, log);

        // 7) Wire navigation scene references into the generated .tscn files.
        WireSceneNavigation(info, genre, log);

        // 8) Project settings from GameInfo.
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
    //  Wire navigation — inject PackedScene ext_resource references
    //  into the Navigation node of each generated .tscn so buttons work
    //  out of the box without manual drag-and-drop.
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// For each generated scene that has a Navigation node, inject PackedScene
    /// ext_resource references. The wiring is fully data-driven from the genre's
    /// genre.json "nav_wiring" block — NO hardcoded per-genre logic. Adding a
    /// new genre just means adding a "nav_wiring" block to its genre.json.
    /// </summary>
    private static void WireSceneNavigation(GameInfo info, Beep.ECS.UI.GenreDef genre, List<string> log)
    {
        // Read the nav_wiring from genre.json (loaded by SkinCatalog).
        if (genre.NavWiring.Count == 0)
        {
            log.Add("Navigation: no nav_wiring in genre.json — skipping.");
            return;
        }

        int wiredCount = 0;
        foreach (var sceneEntry in genre.NavWiring)
        {
            // sceneKey is a relative filename like "main_menu.tscn" or "level_select.tscn".
            string sceneKey = sceneEntry.Key.AsString();
            string targetPath = ResolveTargetPath(genre.Id, sceneKey);
            if (!FileAccess.FileExists(targetPath)) continue;

            // sceneEntry.Value is a Dictionary mapping property name → res:// path.
            var navDict = sceneEntry.Value.AsGodotDictionary();
            if (navDict.Count == 0) continue;

            // Convert to System.Dictionary for InjectSceneRefs.
            var props = new System.Collections.Generic.Dictionary<string, string>();
            foreach (var prop in navDict)
                props[prop.Key.AsString()] = prop.Value.AsString();

            bool ok = InjectSceneRefs(targetPath, props);
            if (ok) wiredCount++;
        }

        log.Add($"Navigation wired: {wiredCount} scenes (from genre.json nav_wiring).");
    }

    /// <summary>Resolve a scene filename to its generated target path.</summary>
    private static string ResolveTargetPath(string genreId, string sceneFileName)
    {
        // Shared scenes (main_menu, pause_menu, settings_menu, game_over, hud) → scenes/ui/
        // Genre scenes → scenes/ui/<genre>/
        bool isShared = sceneFileName is "main_menu.tscn" or "pause_menu.tscn"
            or "settings_menu.tscn" or "game_over.tscn" or "hud.tscn";
        return isShared
            ? $"res://scenes/ui/{sceneFileName}"
            : $"res://scenes/ui/{genreId}/{sceneFileName}";
    }

    /// <summary>
    /// Read a .tscn file, add ext_resource declarations for scene paths, and
    /// set the PackedScene properties on the Navigation node. Uses Godot
    /// FileAccess — no System.IO.
    /// </summary>
    private static bool InjectSceneRefs(string scenePath, System.Collections.Generic.Dictionary<string, string> props)
    {
        using var f = FileAccess.Open(scenePath, FileAccess.ModeFlags.Read);
        if (f == null) return false;
        string content = f.GetAsText();

        // Find the Navigation node and its script ext_resource id.
        var navMatch = System.Text.RegularExpressions.Regex.Match(
            content, @"\[node name=""Navigation"".*?\nscript = ExtResource\(""([^""]+)""\)\n");
        if (!navMatch.Success) return false;
        string navScriptId = navMatch.Groups[1].Value;

        // Find the highest ext_resource id to assign new ones.
        var idMatches = System.Text.RegularExpressions.Regex.Matches(content, @"id=""(\d+)_");
        int nextId = 100; // use high IDs to avoid collisions
        foreach (System.Text.RegularExpressions.Match m in idMatches)
            nextId = System.Math.Max(nextId, int.Parse(m.Groups[1].Value) + 1);

        // Build ext_resource lines + property assignments.
        var extLines = new System.Text.StringBuilder();
        var propLines = new System.Text.StringBuilder();
        var propToId = new System.Collections.Generic.Dictionary<string, string>();

        foreach (var prop in props)
        {
            string resPath = prop.Value;
            if (string.IsNullOrEmpty(resPath) || !FileAccess.FileExists(resPath)) continue;

            string resId = $"{nextId}_scene_{prop.Key.ToLower()}";
            extLines.AppendLine($"[ext_resource type=\"PackedScene\" path=\"{resPath}\" id=\"{resId}\"]");
            propToId[prop.Key] = resId;
            nextId++;
        }

        if (propToId.Count == 0) return false;

        foreach (var p in propToId)
            propLines.AppendLine($"{p.Key} = ExtResource(\"{p.Value}\")");

        // Insert ext_resources before the first [node].
        content = System.Text.RegularExpressions.Regex.Replace(
            content, @"(\[node )", extLines.ToString() + "$1");

        // Insert properties right after the Navigation node's script line.
        string navScriptLine = $"script = ExtResource(\"{navScriptId}\")\n";
        string newNavBlock = navScriptLine + propLines.ToString();
        content = content.Replace(navScriptLine, newNavBlock, System.StringComparison.Ordinal);

        // Update load_steps.
        int addedCount = propToId.Count;
        var loadStepMatch = System.Text.RegularExpressions.Regex.Match(content, @"load_steps=(\d+)");
        if (loadStepMatch.Success)
        {
            int oldCount = int.Parse(loadStepMatch.Groups[1].Value);
            content = content.Replace($"load_steps={oldCount}", $"load_steps={oldCount + addedCount}", System.StringComparison.Ordinal);
        }

        // Write back.
        BeepFileUtils.SafeWriteText(scenePath, content, overwrite: true);
        return true;
    }

    // ════════════════════════════════════════════════════════════════
    // Shared helpers
    // ════════════════════════════════════════════════════════════════

    private static void CopyUiScene(string templateName, string targetPath, RegenMode mode, List<string> log)
        => CopyUiSceneFromPath($"{SceneTemplatesDir}/{templateName}", targetPath, mode, log);

    /// <summary>
    /// Load a .tscn template, stamp it with the generator version, and save to dst.
    /// The RegenMode controls what happens when dst already exists:
    ///   SkipExisting       → don't touch it
    ///   UpdateUnmodified   → overwrite ONLY if it still has the _beep_generated stamp
    ///   OverwriteAll       → always overwrite
    /// </summary>
    private static void CopyUiSceneFromPath(string src, string dst, RegenMode mode, List<string> log)
    {
        // Check if the target already exists.
        if (FileAccess.FileExists(dst))
        {
            if (mode == RegenMode.SkipExisting)
            {
                log.Add($"Skipped (exists): {dst}");
                return;
            }
            if (mode == RegenMode.UpdateUnmodified)
            {
                // Only overwrite if the existing scene is still stamped as generated
                // (i.e. the user hasn't edited it). If the stamp is gone, the user
                // modified the scene and we preserve their work.
                if (!IsSceneGenerated(dst))
                {
                    log.Add($"Skipped (user-modified): {dst}");
                    return;
                }
            }
            // OverwriteAll falls through to the copy below.
        }

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

        bool ok = BeepFileUtils.SafeWriteText(dst, content, overwrite: true);
        if (ok)
        {
            StampGenerated(dst);
            log.Add($"Copied: {dst}");
        }
        else
            log.Add($"WARN copy failed: {dst}");
    }

    /// <summary>
    /// Check if a generated file is unmodified. Uses a sidecar .beep marker file:
    /// when we copy a scene, we also write &lt;scene&gt;.beep. If the user edits
    /// and saves the scene in the editor, the .beep marker is stale (older mtime
    /// than the scene), meaning the user modified it.
    /// </summary>
    private static bool IsSceneGenerated(string scenePath)
    {
        string markerPath = scenePath + ".beep";
        if (!FileAccess.FileExists(markerPath)) return false;
        // If the scene was modified after the marker, the user edited it.
        var sceneTime = FileAccess.GetModifiedTime(scenePath);
        var markerTime = FileAccess.GetModifiedTime(markerPath);
        return sceneTime <= markerTime;
    }

    /// <summary>Write a .beep sidecar marker so we can detect unmodified scenes on regen.</summary>
    private static void StampGenerated(string scenePath)
    {
        string markerPath = scenePath + ".beep";
        BeepFileUtils.SafeWriteText(markerPath, GeneratorVersion, overwrite: true);
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
            string src = $"{srcDir}/{scene}";
            string dst = $"{dstDir}/{scene}";
            CopyUiSceneFromPath(src, dst, mode, log);
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
        // Set the translation flag — saved by the single SaveAll() at the end of generation.
        BeepProjectDefaults.Set("internationalization/locale/translations", true);
    }
}
