using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using Godot;
using GodotMcp;

namespace Beep.GameBuilder;

/// <summary>
/// Exposes Beep's own capabilities to an AI agent through the MCP bridge.
///
/// This is the ONLY link between this addon and `godot_mcp`, and it points one way:
/// Beep registers handlers into the bridge's generic registry; the bridge knows
/// nothing about Beep. Registration is just dictionary writes, so nothing here runs
/// or fails when the bridge addon isn't enabled.
///
/// Commands are namespaced `beep.*` and invoked via the bridge's `game.command`
/// (state via `game.state`). `status.get` lists them, so an agent can discover the
/// surface rather than guess.
///
/// Availability differs by context — the catalog is files, generation and the open
/// scene are editor-only, and the live subsystems are autoloads/scene nodes that only
/// exist in a running game. Writes are gated by the bridge's own security flags.
///
///   editor + runtime (read) — list_genres, list_themes, list_palettes, catalog,
///        genre_info, list_scene_templates, list_weather_types, list_components,
///        component_info, get_game_info, reload_catalog
///   editor only  — generate_project, apply_skin, add_component  (allow_editor_writes),
///        set_game_info (allow_editor_writes; writes game_info.tres)
///   runtime read  — game_state, list_saves, get_weather, get_time, get_settings,
///        list_locales, translate
///   runtime write — save_game, load_game, delete_save, new_game, add_score, game_over,
///        level_complete, set_level, set_weather, set_time, set_setting, set_language
///        (all gated by allow_runtime_writes)
///
/// Scene management, texture baking and screenshots live in the BeepMcpSceneCommands partial:
///   read   — list_scenes, open_scene, inspect_scene, get_node_property, screenshot
///   editor write — set_node_property, add_node, remove_node, save_scene, bake_textures,
///        new_screen  (allow_editor_writes)
/// </summary>
public static partial class BeepMcpCommands
{
    private const string Prefix = "beep.";

    public static void Register()
    {
        // ── Catalog + config discovery (read-only, editor + runtime) ──
        McpCommandRegistry.RegisterCommand("beep.list_genres", _ => ListGenres());
        McpCommandRegistry.RegisterCommand("beep.list_themes", args => ListThemes(Str(args, "genre")));
        McpCommandRegistry.RegisterCommand("beep.list_palettes", args => ListPalettes(Str(args, "genre"), Str(args, "theme")));
        McpCommandRegistry.RegisterCommand("beep.catalog", _ => FullCatalog());
        McpCommandRegistry.RegisterCommand("beep.genre_info", args => GenreInfo(Str(args, "genre")));
        McpCommandRegistry.RegisterCommand("beep.list_scene_templates", args => ListSceneTemplates(Str(args, "genre")));
        McpCommandRegistry.RegisterCommand("beep.list_weather_types", _ => ListWeatherTypes());
        McpCommandRegistry.RegisterCommand("beep.reload_catalog", _ => ReloadCatalog());

        // ── Components (discovery + inspection + creation) ──
        McpCommandRegistry.RegisterCommand("beep.list_components", args => ListComponents(Str(args, "category"), Str(args, "search")));
        McpCommandRegistry.RegisterCommand("beep.component_info", args => ComponentInfo(Str(args, "type")));
        McpCommandRegistry.RegisterCommand("beep.add_component", args =>
            AddComponent(Str(args, "node"), Str(args, "type"), args["properties"] as JsonObject));

        // ── Project config (GameInfo) — read anywhere; write is an editor file write ──
        McpCommandRegistry.RegisterCommand("beep.get_game_info", _ => GetGameInfo());
        McpCommandRegistry.RegisterCommand("beep.set_game_info", args => SetGameInfo(args["properties"] as JsonObject));

        // ── Skin + generate (editor writes) ──
        McpCommandRegistry.RegisterCommand("beep.apply_skin", args =>
            ApplySkin(Str(args, "genre"), Str(args, "theme"), Str(args, "palette")));
        McpCommandRegistry.RegisterCommand("beep.generate_project", args =>
            GenerateProject(Str(args, "genre"), Str(args, "theme"), Str(args, "palette")));

        // ── Live game state (read) ──
        McpCommandRegistry.RegisterState("beep.game_state", GameState);
        McpCommandRegistry.RegisterCommand("beep.game_state", _ => GameState());

        // ── Save / load (runtime; writes gated by allow_runtime_writes) ──
        McpCommandRegistry.RegisterCommand("beep.list_saves", _ => ListSaves());
        McpCommandRegistry.RegisterCommand("beep.save_game", args => SaveGame(args));
        McpCommandRegistry.RegisterCommand("beep.load_game", args => LoadGame(Int(args, "slot", Beep.ECS.GameStateManagerComponent.AutosaveSlot)));
        McpCommandRegistry.RegisterCommand("beep.delete_save", args => DeleteSave(Int(args, "slot", int.MinValue)));
        McpCommandRegistry.RegisterCommand("beep.new_game", args => NewGame(Str(args, "player")));

        // ── Gameplay flow (runtime; gated) ──
        McpCommandRegistry.RegisterCommand("beep.add_score", args => AddScore(Int(args, "amount", 0)));
        McpCommandRegistry.RegisterCommand("beep.game_over", _ => TriggerGameOver());
        McpCommandRegistry.RegisterCommand("beep.level_complete", _ => TriggerLevelComplete());
        McpCommandRegistry.RegisterCommand("beep.set_level", args => SetLevel(Int(args, "level", 1)));

        // ── Weather (runtime; set gated) ──
        McpCommandRegistry.RegisterCommand("beep.get_weather", _ => GetWeather());
        McpCommandRegistry.RegisterCommand("beep.set_weather", args => SetWeather(args));

        // ── Day / night (runtime; set gated) ──
        McpCommandRegistry.RegisterCommand("beep.get_time", _ => GetTime());
        McpCommandRegistry.RegisterCommand("beep.set_time", args => SetTime(Flt(args, "hours", 12f)));

        // ── Settings (runtime; set gated) ──
        McpCommandRegistry.RegisterCommand("beep.get_settings", _ => GetSettings());
        McpCommandRegistry.RegisterCommand("beep.set_setting", args => SetSetting(Str(args, "key"), args["value"]));

        // ── Localization (runtime; set gated) ──
        McpCommandRegistry.RegisterCommand("beep.list_locales", _ => ListLocales());
        McpCommandRegistry.RegisterCommand("beep.set_language", args => SetLanguage(Str(args, "locale")));
        McpCommandRegistry.RegisterCommand("beep.translate", args => Translate(Str(args, "key")));

        // ── Scene management, texture baking, screenshots (see BeepMcpSceneCommands.cs) ──
        RegisterSceneCommands();
    }

    public static void Unregister() => McpCommandRegistry.UnregisterPrefix(Prefix);

    // ════════════════════════════════════════════════════════════════
    // Catalog reads
    // ════════════════════════════════════════════════════════════════

    private static JsonNode ListGenres()
    {
        var genres = new JsonArray();
        foreach (var g in Beep.ECS.UI.SkinCatalog.AllGenres.Values)
            genres.Add(new JsonObject
            {
                ["id"] = g.Id,
                ["display_name"] = g.DisplayName,
                ["icon"] = g.Icon,
                ["description"] = g.Description,
                ["default_theme"] = g.DefaultTheme,
                ["main_scene"] = g.MainScene,
                ["theme_count"] = g.Themes.Count
            });
        return new JsonObject { ["genres"] = genres };
    }

    private static JsonNode ListThemes(string genreId)
    {
        var genre = RequireGenre(genreId);
        var themes = new JsonArray();
        foreach (var t in genre.Themes.Values)
            themes.Add(new JsonObject
            {
                ["id"] = t.Id,
                ["display_name"] = t.DisplayName,
                ["category"] = t.Category,
                ["description"] = t.Description,
                ["palette_count"] = t.Palettes.Count
            });
        return new JsonObject { ["genre"] = genre.Id, ["themes"] = themes };
    }

    private static JsonNode ListPalettes(string genreId, string themeId)
    {
        var theme = Beep.ECS.UI.SkinCatalog.GetTheme(genreId, themeId)
            ?? throw new System.InvalidOperationException(
                $"Theme '{themeId}' not found in genre '{genreId}'. Use beep.list_themes.");

        var palettes = new JsonArray();
        foreach (var p in theme.Palettes.Values)
            palettes.Add(p.DisplayName);
        return new JsonObject { ["genre"] = genreId, ["theme"] = theme.Id, ["palettes"] = palettes };
    }

    /// <summary>Whole genre → theme → palette tree in one call, so an agent doesn't
    /// have to walk it with N round-trips.</summary>
    private static JsonNode FullCatalog()
    {
        var genres = new JsonArray();
        foreach (var g in Beep.ECS.UI.SkinCatalog.AllGenres.Values)
        {
            var themes = new JsonArray();
            foreach (var t in g.Themes.Values)
            {
                var palettes = new JsonArray();
                foreach (var p in t.Palettes.Values) palettes.Add(p.DisplayName);
                themes.Add(new JsonObject
                {
                    ["id"] = t.Id,
                    ["display_name"] = t.DisplayName,
                    ["palettes"] = palettes
                });
            }
            genres.Add(new JsonObject
            {
                ["id"] = g.Id,
                ["display_name"] = g.DisplayName,
                ["default_theme"] = g.DefaultTheme,
                ["geometry"] = g.Geometry?.DisplayName ?? "",
                ["themes"] = themes
            });
        }
        return new JsonObject { ["genres"] = genres };
    }

    // ════════════════════════════════════════════════════════════════
    // Components
    //
    // Discovered by reflection over the assembly rather than a hand-written list,
    // so a newly added component shows up with no extra work here — the same
    // "drop it in and it's picked up" rule the skin catalog follows.
    // ════════════════════════════════════════════════════════════════

    /// <summary>The recognised category bases. Order is irrelevant — CategoryOf walks the
    /// inheritance chain upward and stops at the first hit, so EffectComponent (which
    /// extends UIComponent) resolves to itself rather than to UIComponent.</summary>
    private static readonly string[] CategoryNames =
    {
        "EffectComponent", "UIComponent", "GameplayComponent", "ControllerComponent", "WorldComponent", "EntityComponent"
    };

    private static IEnumerable<Type> AllComponentTypes()
        => typeof(Beep.ECS.EntityComponent).Assembly
            .GetTypes()
            .Where(t => !t.IsAbstract
                        && t.IsClass
                        && typeof(Beep.ECS.EntityComponent).IsAssignableFrom(t));

    /// <summary>Walk the base chain to the first recognised category.</summary>
    private static string CategoryOf(Type type)
    {
        for (Type? t = type.BaseType; t != null; t = t.BaseType)
            if (Array.IndexOf(CategoryNames, t.Name) >= 0)
                return t.Name;
        return "EntityComponent";
    }

    private static JsonNode ListComponents(string category, string search)
    {
        var types = AllComponentTypes();

        if (!string.IsNullOrEmpty(category))
            types = types.Where(t => CategoryOf(t).Equals(category, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(search))
            types = types.Where(t => t.Name.Contains(search, StringComparison.OrdinalIgnoreCase));

        var byCategory = new Dictionary<string, JsonArray>();
        int total = 0;
        foreach (var t in types.OrderBy(t => t.Name))
        {
            string cat = CategoryOf(t);
            if (!byCategory.TryGetValue(cat, out var list))
                byCategory[cat] = list = new JsonArray();
            list.Add(t.Name);
            total++;
        }

        var result = new JsonObject();
        foreach (var kv in byCategory.OrderBy(k => k.Key))
            result[kv.Key] = kv.Value;

        return new JsonObject
        {
            ["total"] = total,
            ["categories"] = result,
            ["hint"] = "Use beep.component_info for a type's properties, then beep.add_component to attach it."
        };
    }

    /// <summary>Exported properties + signals of a component type. Instantiates once to
    /// read real defaults, then frees it — construction alone doesn't run _Ready.</summary>
    private static JsonNode ComponentInfo(string typeName)
    {
        Type type = RequireComponentType(typeName);

        GodotObject? probe = null;
        try
        {
            try { probe = Activator.CreateInstance(type) as GodotObject; }
            catch { /* no default ctor / construction refused — report without defaults */ }

            var properties = new JsonArray();
            foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                  .Where(p => p.GetCustomAttribute<ExportAttribute>() != null)
                                  .OrderBy(p => p.Name))
            {
                var entry = new JsonObject
                {
                    ["name"] = p.Name,
                    ["type"] = FriendlyTypeName(p.PropertyType)
                };

                Type bare = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
                if (bare.IsEnum)
                    entry["enum_values"] = new JsonArray(Enum.GetNames(bare).Select(n => (JsonNode)n!).ToArray());

                if (probe != null)
                {
                    try { entry["default"] = JsonValue.Create(p.GetValue(probe)?.ToString() ?? ""); }
                    catch { /* getter needs a tree — skip the default */ }
                }
                properties.Add(entry);
            }

            var signals = new JsonArray();
            foreach (var s in type.GetNestedTypes(BindingFlags.Public)
                                  .Where(t => typeof(Delegate).IsAssignableFrom(t)
                                              && t.GetCustomAttribute<SignalAttribute>() != null))
            {
                string name = s.Name.EndsWith("EventHandler", StringComparison.Ordinal)
                    ? s.Name[..^"EventHandler".Length]
                    : s.Name;
                var args = new JsonArray();
                foreach (var prm in s.GetMethod("Invoke")!.GetParameters())
                    args.Add($"{FriendlyTypeName(prm.ParameterType)} {prm.Name}");
                signals.Add(new JsonObject { ["name"] = name, ["args"] = args });
            }

            return new JsonObject
            {
                ["name"] = type.Name,
                ["category"] = CategoryOf(type),
                ["base"] = type.BaseType?.Name ?? "",
                ["namespace"] = type.Namespace ?? "",
                ["properties"] = properties,
                ["signals"] = signals
            };
        }
        finally
        {
            // Free the probe — an unparented Node is not reference-counted.
            if (probe is Node n) n.Free();
            else if (probe is RefCounted) { /* collected automatically */ }
            else probe?.Free();
        }
    }

    /// <summary>Attach a component under a node. Editor-side it targets the open scene and
    /// sets Owner, without which the node would vanish on save.</summary>
    private static JsonNode AddComponent(string nodePath, string typeName, JsonObject? properties)
    {
        Type type = RequireComponentType(typeName);

#if TOOLS
        if (!GodotMcpSettings.GetBool(GodotMcpSettings.AllowEditorWrites, false))
            throw new InvalidOperationException(
                "beep.add_component edits the open scene. Enable godot_mcp/security/allow_editor_writes first.");

        var root = EditorInterface.Singleton.GetEditedSceneRoot()
            ?? throw new InvalidOperationException("No scene is open in the editor.");

        Node parent = string.IsNullOrEmpty(nodePath) || nodePath == "." || nodePath == "/"
            ? root
            : root.GetNodeOrNull(nodePath)
              ?? throw new InvalidOperationException($"Node not found in the open scene: {nodePath}");

        if (Activator.CreateInstance(type) is not Node component)
            throw new InvalidOperationException($"'{type.Name}' could not be constructed as a Node.");

        component.Name = type.Name;
        parent.AddChild(component);
        // Required or the node is not persisted with the scene.
        component.Owner = root;

        var applied = new JsonArray();
        if (properties != null)
            foreach (var kv in properties)
            {
                component.Set(kv.Key, McpJson.ToVariant(kv.Value));
                applied.Add(kv.Key);
            }

        EditorInterface.Singleton.MarkSceneAsUnsaved();

        return new JsonObject
        {
            ["added"] = type.Name,
            ["category"] = CategoryOf(type),
            ["parent"] = parent == root ? "." : root.GetPathTo(parent).ToString(),
            ["path"] = root.GetPathTo(component).ToString(),
            ["properties_set"] = applied,
            ["scene"] = root.SceneFilePath,
            ["note"] = "Scene marked unsaved — save it in the editor to persist."
        };
#else
        throw new InvalidOperationException("beep.add_component is editor-only.");
#endif
    }

    private static Type RequireComponentType(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            throw new InvalidOperationException("A 'type' argument is required. Use beep.list_components.");

        var matches = AllComponentTypes()
            .Where(t => t.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0)
            throw new InvalidOperationException(
                $"'{typeName}' is not a Beep component. Use beep.list_components to see what exists.");
        if (matches.Count > 1)
            throw new InvalidOperationException(
                $"'{typeName}' is ambiguous: {string.Join(", ", matches.Select(m => m.FullName))}");

        return matches[0];
    }

    private static string FriendlyTypeName(Type t)
    {
        Type bare = Nullable.GetUnderlyingType(t) ?? t;
        string suffix = bare != t ? "?" : "";
        return bare.Name + suffix;
    }

    // ════════════════════════════════════════════════════════════════
    // Actions
    // ════════════════════════════════════════════════════════════════

    /// <summary>Re-skin every ThemePresetComponent in the open scene. Editor-only:
    /// it edits the scene you have open.</summary>
    private static JsonNode ApplySkin(string genreId, string themeId, string palette)
    {
        var genre = RequireGenre(genreId);
        if (string.IsNullOrEmpty(themeId)) themeId = genre.DefaultTheme;
        if (Beep.ECS.UI.SkinCatalog.GetTheme(genreId, themeId) == null)
            throw new System.InvalidOperationException(
                $"Theme '{themeId}' not found in genre '{genreId}'. Use beep.list_themes.");
        if (string.IsNullOrEmpty(palette)) palette = "Default";

#if TOOLS
        var root = EditorInterface.Singleton.GetEditedSceneRoot()
            ?? throw new System.InvalidOperationException("No scene is open in the editor.");

        int applied = 0;
        foreach (var component in FindThemeComponents(root))
        {
            component.GenreName = genreId;
            component.PresetName = themeId;
            component.PaletteName = palette;
            applied++;
        }

        return new JsonObject
        {
            ["genre"] = genreId,
            ["theme"] = themeId,
            ["palette"] = palette,
            ["components_updated"] = applied,
            ["scene"] = root.SceneFilePath
        };
#else
        throw new System.InvalidOperationException("beep.apply_skin is editor-only.");
#endif
    }

    /// <summary>Stamp a full starter project for a genre. Editor-only, and it writes
    /// files — gated behind the bridge's existing allow_editor_writes setting.</summary>
    private static JsonNode GenerateProject(string genreId, string themeId, string palette)
    {
#if TOOLS
        if (!GodotMcpSettings.GetBool(GodotMcpSettings.AllowEditorWrites, false))
            throw new System.InvalidOperationException(
                "beep.generate_project writes files. Enable godot_mcp/security/allow_editor_writes first.");

        var genre = RequireGenre(genreId);

        var info = ResourceLoader.Exists(GameInfo.TresPath)
            ? ResourceLoader.Load<GameInfo>(GameInfo.TresPath) ?? new GameInfo()
            : new GameInfo();

        if (!string.IsNullOrEmpty(themeId)) info.DefaultThemePreset = themeId;
        if (!string.IsNullOrEmpty(palette)) info.PaletteName = palette;

        var log = BeepGenreGenerator.CreateProject(genreId, info, overwrite: false);

        var lines = new JsonArray();
        foreach (string line in log) lines.Add(line);

        return new JsonObject
        {
            ["genre"] = genre.Id,
            ["theme"] = info.DefaultThemePreset,
            ["palette"] = info.PaletteName,
            ["log"] = lines
        };
#else
        throw new System.InvalidOperationException("beep.generate_project is editor-only.");
#endif
    }

    // ════════════════════════════════════════════════════════════════
    // Live state
    // ════════════════════════════════════════════════════════════════

    private static JsonNode GameState()
    {
        var app = Beep.ECS.GameApp.Instance
            ?? throw new System.InvalidOperationException(
                "GameApp autoload is not present — beep.game_state only works while the game is running.");

        var info = app.Info;
        return new JsonObject
        {
            ["game_name"] = app.GameName,
            ["version"] = app.Version,
            ["genre"] = info?.GenreId ?? "",
            ["theme"] = info?.DefaultThemePreset ?? "",
            ["palette"] = info?.PaletteName ?? "",
            ["is_running"] = app.IsGameRunning,
            ["is_paused"] = app.IsPaused,
            ["current_level"] = app.CurrentLevel,
            ["session_score"] = app.SessionScore,
            ["game_mode"] = app.GameMode,
            ["fps"] = app.CurrentFPS,
            ["game_scene_path"] = app.GameScenePath
        };
    }

    // ════════════════════════════════════════════════════════════════
    // Catalog + config discovery
    // ════════════════════════════════════════════════════════════════

    /// <summary>One genre in full: its scenes[], tuning{}, nav_wiring{}, geometry, and themes —
    /// so an agent gets the whole genre definition without walking it in pieces.</summary>
    private static JsonNode GenreInfo(string genreId)
    {
        var g = RequireGenre(genreId);

        var scenes = new JsonArray();
        foreach (var s in g.Scenes) scenes.Add(s);
        var themes = new JsonArray();
        foreach (var t in g.Themes.Values) themes.Add(t.Id);

        JsonNode? geometry = null;
        if (g.Geometry is { } geo)
            geometry = new JsonObject
            {
                ["id"] = geo.Id,
                ["display_name"] = geo.DisplayName,
                ["corner_radius"] = geo.CornerRadius,
                ["border_width"] = geo.BorderWidth,
                ["shadow_size"] = geo.ShadowSize,
                ["shadow_offset_y"] = geo.ShadowOffsetY,
                ["content_padding"] = geo.ContentPadding,
                ["font_size"] = geo.FontSize,
                ["background_mode"] = geo.BackgroundMode
            };

        return new JsonObject
        {
            ["id"] = g.Id,
            ["display_name"] = g.DisplayName,
            ["icon"] = g.Icon,
            ["description"] = g.Description,
            ["default_theme"] = g.DefaultTheme,
            ["default_geometry"] = g.DefaultGeometryId,
            ["main_scene"] = g.MainScene,
            ["scenes"] = scenes,
            ["themes"] = themes,
            ["tuning"] = McpJson.FromVariant(g.Tuning),
            ["nav_wiring"] = McpJson.FromVariant(g.NavWiring),
            ["geometry"] = geometry
        };
    }

    /// <summary>List the .tscn templates that ship with the addon (shared + per-genre), scanning the
    /// templates folder — there is no manifest, and a newly-dropped template shows up automatically.</summary>
    private static JsonNode ListSceneTemplates(string genre)
    {
        const string baseDir = "res://addons/beep_game_builder_cs/templates/scenes";

        var result = new JsonObject { ["shared"] = ScanTscn(baseDir) };
        if (!string.IsNullOrEmpty(genre))
        {
            result["genre"] = genre;
            result[genre] = ScanTscn($"{baseDir}/{genre}");
        }
        else
        {
            var byGenre = new JsonObject();
            foreach (var id in Beep.ECS.UI.SkinCatalog.AllGenres.Keys)
            {
                var arr = ScanTscn($"{baseDir}/{id}");
                if (arr.Count > 0) byGenre[id] = arr;
            }
            result["genres"] = byGenre;
        }
        return result;
    }

    private static JsonArray ScanTscn(string dir)
    {
        var arr = new JsonArray();
        using var d = DirAccess.Open(dir);
        if (d == null) return arr;
        d.ListDirBegin();
        for (string f = d.GetNext(); !string.IsNullOrEmpty(f); f = d.GetNext())
            if (!d.CurrentIsDir() && f.EndsWith(".tscn", StringComparison.Ordinal))
                arr.Add(f);
        d.ListDirEnd();
        return arr;
    }

    private static JsonNode ListWeatherTypes()
    {
        var arr = new JsonArray();
        foreach (var n in Enum.GetNames<Beep.ECS.WeatherSystemComponent.WeatherType>()) arr.Add(n);
        return new JsonObject { ["weather_types"] = arr };
    }

    /// <summary>Rescan the skin catalog from disk — useful after editing genre/theme/palette JSON.</summary>
    private static JsonNode ReloadCatalog()
    {
        Beep.ECS.UI.SkinCatalog.Reload();
        return new JsonObject { ["reloaded"] = true, ["genre_count"] = Beep.ECS.UI.SkinCatalog.AllGenres.Count };
    }

    // ════════════════════════════════════════════════════════════════
    // GameInfo (project config)
    // ════════════════════════════════════════════════════════════════

    /// <summary>Read every [Export] on GameInfo (from game_info.tres if it exists, else defaults).</summary>
    private static JsonNode GetGameInfo()
    {
        bool exists = ResourceLoader.Exists(GameInfo.TresPath);
        var info = exists ? ResourceLoader.Load<GameInfo>(GameInfo.TresPath) ?? new GameInfo() : new GameInfo();

        var props = new JsonObject();
        foreach (var p in typeof(GameInfo).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                              .Where(p => p.GetCustomAttribute<ExportAttribute>() != null)
                              .OrderBy(p => p.Name))
        {
            try { props[p.Name] = McpJson.FromVariant(info.Get(p.Name)); }
            catch { /* getter needs a tree — skip */ }
        }
        return new JsonObject { ["tres_path"] = GameInfo.TresPath, ["exists"] = exists, ["properties"] = props };
    }

    /// <summary>Write [Export] fields onto game_info.tres. Editor-only (res:// is read-only at runtime)
    /// and behind allow_editor_writes.</summary>
    private static JsonNode SetGameInfo(JsonObject? properties)
    {
#if TOOLS
        RequireEditorWrites("beep.set_game_info");
        if (properties == null || properties.Count == 0)
            throw new InvalidOperationException("A 'properties' object is required, e.g. {\"GameName\":\"My Game\",\"TargetFps\":60}. Names are the PascalCase [Export] names from beep.get_game_info.");

        var info = ResourceLoader.Exists(GameInfo.TresPath)
            ? ResourceLoader.Load<GameInfo>(GameInfo.TresPath) ?? new GameInfo()
            : new GameInfo();

        var applied = new JsonArray();
        foreach (var kv in properties)
        {
            info.Set(kv.Key, McpJson.ToVariant(kv.Value));
            applied.Add(kv.Key);
        }

        Error err = ResourceSaver.Save(info, GameInfo.TresPath);
        if (err != Error.Ok)
            throw new InvalidOperationException($"Failed to save {GameInfo.TresPath}: {err}");

        return new JsonObject { ["saved"] = GameInfo.TresPath, ["properties_set"] = applied };
#else
        throw new InvalidOperationException("beep.set_game_info is editor-only.");
#endif
    }

    // ════════════════════════════════════════════════════════════════
    // Save / load (runtime)
    // ════════════════════════════════════════════════════════════════

    private static JsonNode ListSaves()
    {
        var mgr = RequireSaveManager();
        var slots = new JsonArray();
        foreach (var (slot, md) in mgr.GetSaveSlots(includeAutosave: true))
            slots.Add(new JsonObject
            {
                ["slot"] = slot,
                ["is_autosave"] = slot == Beep.ECS.GameStateManagerComponent.AutosaveSlot,
                ["name"] = md.SaveName,
                ["timestamp"] = md.Timestamp,
                ["playtime_seconds"] = md.PlaytimeSeconds,
                ["current_level"] = md.CurrentLevel,
                ["play_count"] = md.PlayCount,
                ["description"] = md.Description
            });
        return new JsonObject { ["max_slots"] = mgr.MaxSaveSlots, ["autosave_slot"] = Beep.ECS.GameStateManagerComponent.AutosaveSlot, ["saves"] = slots };
    }

    /// <summary>Save to a numbered slot, or the autosave slot when 'slot' is omitted / -1.</summary>
    private static JsonNode SaveGame(JsonObject args)
    {
        RequireRuntimeWrites("beep.save_game");
        var mgr = RequireSaveManager();
        int autosave = Beep.ECS.GameStateManagerComponent.AutosaveSlot;

        int slot = Int(args, "slot", autosave);
        bool ok = slot == autosave ? mgr.SaveAutosave() : mgr.Save(slot);
        return new JsonObject { ["saved"] = ok, ["slot"] = slot };
    }

    private static JsonNode LoadGame(int slot)
    {
        RequireRuntimeWrites("beep.load_game");
        var mgr = RequireSaveManager();
        return new JsonObject { ["loaded"] = mgr.Load(slot), ["slot"] = slot };
    }

    private static JsonNode DeleteSave(int slot)
    {
        RequireRuntimeWrites("beep.delete_save");
        if (slot < Beep.ECS.GameStateManagerComponent.AutosaveSlot)
            throw new InvalidOperationException("A valid 'slot' is required (autosave = -1, or 0..max-1). Use beep.list_saves.");
        var mgr = RequireSaveManager();
        return new JsonObject { ["deleted"] = mgr.DeleteSave(slot), ["slot"] = slot };
    }

    private static JsonNode NewGame(string player)
    {
        RequireRuntimeWrites("beep.new_game");
        var mgr = RequireSaveManager();
        string name = string.IsNullOrEmpty(player) ? "Player" : player;
        mgr.NewGame(name);
        return new JsonObject { ["new_game"] = true, ["player"] = name };
    }

    // ════════════════════════════════════════════════════════════════
    // Gameplay flow (runtime)
    // ════════════════════════════════════════════════════════════════

    private static JsonNode AddScore(int amount)
    {
        RequireRuntimeWrites("beep.add_score");
        var flow = RequireFlow();
        flow.AddScore(amount);
        return new JsonObject { ["score"] = flow.Score, ["added"] = amount };
    }

    private static JsonNode TriggerGameOver()
    {
        RequireRuntimeWrites("beep.game_over");
        RequireFlow().TriggerGameOver();
        return new JsonObject { ["game_over"] = true };
    }

    private static JsonNode TriggerLevelComplete()
    {
        RequireRuntimeWrites("beep.level_complete");
        RequireFlow().TriggerLevelComplete();
        return new JsonObject { ["level_complete"] = true };
    }

    private static JsonNode SetLevel(int level)
    {
        RequireRuntimeWrites("beep.set_level");
        var app = Beep.ECS.GameApp.Instance ?? throw RuntimeOnly("beep.set_level");
        app.SetLevel(level);
        return new JsonObject { ["current_level"] = app.CurrentLevel };
    }

    // ════════════════════════════════════════════════════════════════
    // Weather (runtime)
    // ════════════════════════════════════════════════════════════════

    private static JsonNode GetWeather()
    {
        var w = RequireWeather();
        return new JsonObject
        {
            ["weather"] = w.CurrentWeatherName,
            ["intensity"] = w.WeatherIntensity,
            ["auto_cycle"] = w.AutoCycle,
            ["time_to_next"] = w.TimeToNextWeather
        };
    }

    private static JsonNode SetWeather(JsonObject args)
    {
        RequireRuntimeWrites("beep.set_weather");
        string weather = Str(args, "weather");
        if (string.IsNullOrEmpty(weather)
            || !Enum.TryParse<Beep.ECS.WeatherSystemComponent.WeatherType>(weather, ignoreCase: true, out var wt))
            throw new InvalidOperationException($"Unknown weather '{weather}'. Use beep.list_weather_types.");

        var w = RequireWeather();
        float transition = Flt(args, "transition_seconds", 0f);
        float intensity = Flt(args, "intensity", 1f);
        if (transition > 0f)
            w.TransitionTo(wt, transition, intensity);
        else
        {
            w.SetWeather(wt);
            if (Has(args, "intensity")) w.TargetIntensity = intensity;
        }
        return new JsonObject { ["weather"] = wt.ToString(), ["intensity"] = intensity, ["transition_seconds"] = transition };
    }

    // ════════════════════════════════════════════════════════════════
    // Day / night (runtime)
    // ════════════════════════════════════════════════════════════════

    private static JsonNode GetTime()
    {
        var dn = RequireDayNight();
        return new JsonObject
        {
            ["time_of_day"] = dn.TimeOfDay,
            ["normalized"] = dn.TimeOfDayNormalized,
            ["days_elapsed"] = dn.DaysElapsed
        };
    }

    private static JsonNode SetTime(float hours)
    {
        RequireRuntimeWrites("beep.set_time");
        var dn = RequireDayNight();
        dn.SetTimeOfDay(hours);
        return new JsonObject { ["time_of_day"] = dn.TimeOfDay };
    }

    // ════════════════════════════════════════════════════════════════
    // Settings (runtime)
    // ════════════════════════════════════════════════════════════════

    private static JsonNode GetSettings()
    {
        var s = RequireSettings();
        return new JsonObject
        {
            ["master_volume"] = s.MasterVolume,
            ["sfx_volume"] = s.SfxVolume,
            ["music_volume"] = s.MusicVolume,
            ["fullscreen"] = s.Fullscreen,
            ["resolution_index"] = s.ResolutionIndex,
            ["language"] = s.Language,
            ["subtitles"] = s.SubtitlesEnabled,
            ["screen_shake"] = s.ScreenShakeEnabled,
            ["damage_numbers"] = s.DamageNumbersEnabled
        };
    }

    private static JsonNode SetSetting(string key, JsonNode? value)
    {
        RequireRuntimeWrites("beep.set_setting");
        if (value == null) throw new InvalidOperationException("A 'value' is required.");
        var s = RequireSettings();
        string v = value.ToString();

        switch (key.ToLowerInvariant())
        {
            case "master_volume":  s.MasterVolume = ParseFloat(v); break;
            case "sfx_volume":     s.SfxVolume = ParseFloat(v); break;
            case "music_volume":   s.MusicVolume = ParseFloat(v); break;
            case "fullscreen":     s.Fullscreen = ParseBool(v); break;
            case "resolution_index": s.ResolutionIndex = ParseInt(v); break;
            case "language":       s.Language = v; break;
            case "subtitles":      s.SubtitlesEnabled = ParseBool(v); break;
            case "screen_shake":   s.ScreenShakeEnabled = ParseBool(v); break;
            case "damage_numbers": s.DamageNumbersEnabled = ParseBool(v); break;
            default:
                throw new InvalidOperationException(
                    $"Unknown setting '{key}'. Known: master_volume, sfx_volume, music_volume, fullscreen, " +
                    "resolution_index, language, subtitles, screen_shake, damage_numbers.");
        }
        s.FlushSettings();
        return new JsonObject { ["set"] = key, ["value"] = v };
    }

    // ════════════════════════════════════════════════════════════════
    // Localization (runtime)
    // ════════════════════════════════════════════════════════════════

    private static JsonNode ListLocales()
    {
        var loc = RequireLocale();
        var arr = new JsonArray();
        foreach (var l in loc.AvailableLocales()) arr.Add(l);
        return new JsonObject { ["current"] = loc.CurrentLocale, ["available"] = arr };
    }

    private static JsonNode SetLanguage(string locale)
    {
        RequireRuntimeWrites("beep.set_language");
        if (string.IsNullOrEmpty(locale))
            throw new InvalidOperationException("A 'locale' is required (e.g. en, es, ja). Use beep.list_locales.");
        var loc = RequireLocale();
        loc.SetLanguage(locale);
        return new JsonObject { ["locale"] = loc.CurrentLocale };
    }

    private static JsonNode Translate(string key)
    {
        if (string.IsNullOrEmpty(key)) throw new InvalidOperationException("A 'key' is required.");
        var loc = RequireLocale();
        return new JsonObject { ["key"] = key, ["translation"] = loc.Tr(key) };
    }

    // ════════════════════════════════════════════════════════════════
    // Helpers
    // ════════════════════════════════════════════════════════════════

    private static Beep.ECS.UI.GenreDef RequireGenre(string genreId)
    {
        if (string.IsNullOrEmpty(genreId))
            throw new System.InvalidOperationException("A 'genre' argument is required. Use beep.list_genres.");
        return Beep.ECS.UI.SkinCatalog.GetGenre(genreId)
            ?? throw new System.InvalidOperationException(
                $"Genre '{genreId}' not found in the skin catalog. Use beep.list_genres.");
    }

    private static System.Collections.Generic.List<Beep.ECS.UI.ThemePresetComponent> FindThemeComponents(Node root)
    {
        var found = new System.Collections.Generic.List<Beep.ECS.UI.ThemePresetComponent>();
        Collect(root, found);
        return found;

        static void Collect(Node node, System.Collections.Generic.List<Beep.ECS.UI.ThemePresetComponent> list)
        {
            if (node is Beep.ECS.UI.ThemePresetComponent c) list.Add(c);
            foreach (var child in node.GetChildren()) Collect(child, list);
        }
    }

    private static string Str(JsonObject args, string key)
        // ToString() rather than GetValue<string>(): a non-string JSON value (number/bool) threw
        // FormatException instead of coercing to a clean argument.
        => args[key]?.ToString() ?? "";

    // ── argument parsing (JSON values may arrive as string/number/bool) ──
    private static bool Has(JsonObject args, string key) => args.ContainsKey(key) && args[key] is not null;
    private static int Int(JsonObject args, string key, int def)
        => Has(args, key) && int.TryParse(args[key]!.ToString(), out var n) ? n : def;
    private static float Flt(JsonObject args, string key, float def)
        => Has(args, key) ? ParseFloat(args[key]!.ToString(), def) : def;
    private static float ParseFloat(string s, float def = 0f)
        => float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f) ? f : def;
    private static int ParseInt(string s) => int.TryParse(s, out var n) ? n : 0;
    private static bool ParseBool(string s) => bool.TryParse(s, out var b) ? b : s == "1";

    // ── write gates (respect the bridge's own security flags) ──
    private static void RequireRuntimeWrites(string command)
    {
        if (!GodotMcpSettings.GetBool(GodotMcpSettings.AllowRuntimeWrites, false))
            throw new InvalidOperationException(
                $"{command} changes live game state. Enable godot_mcp/security/allow_runtime_writes first.");
    }

    private static void RequireEditorWrites(string command)
    {
        if (!GodotMcpSettings.GetBool(GodotMcpSettings.AllowEditorWrites, false))
            throw new InvalidOperationException(
                $"{command} writes project files. Enable godot_mcp/security/allow_editor_writes first.");
    }

    // ── runtime instance access ──
    private static SceneTree? Tree => Engine.GetMainLoop() as SceneTree;

    private static InvalidOperationException RuntimeOnly(string command)
        => new($"{command} only works while the game is running (no SceneTree / autoloads in the editor).");

    /// <summary>Depth-first find of the first node of type T under the running scene (then the whole
    /// tree). Used for the game-scene components that ship no static Instance (GameFlow, DayNight).</summary>
    private static T? FindOfType<T>() where T : Node
    {
        var tree = Tree;
        if (tree == null) return null;
        return Search(tree.CurrentScene) ?? Search(tree.Root);

        static T? Search(Node? node)
        {
            if (node is null) return null;
            if (node is T hit) return hit;
            foreach (var child in node.GetChildren())
                if (Search(child) is { } found) return found;
            return null;
        }
    }

    private static Beep.ECS.GameStateManagerComponent RequireSaveManager()
        => Beep.ECS.GameStateManagerComponent.Instance
           ?? throw new InvalidOperationException("GameStateManager autoload is not present — save/load only works while the game is running.");

    private static Beep.ECS.GameFlowComponent RequireFlow()
        => FindOfType<Beep.ECS.GameFlowComponent>()
           ?? throw new InvalidOperationException("No GameFlowComponent in the running scene — it lives on the gameplay scene root.");

    private static Beep.ECS.WeatherSystemComponent RequireWeather()
    {
        var tree = Tree ?? throw RuntimeOnly("weather commands");
        foreach (var n in tree.GetNodesInGroup("weather_system"))
            if (n is Beep.ECS.WeatherSystemComponent w) return w;
        throw new InvalidOperationException("No WeatherSystemComponent in the running scene (weather must be enabled for this genre).");
    }

    private static Beep.ECS.DayNightCycleComponent RequireDayNight()
        => FindOfType<Beep.ECS.DayNightCycleComponent>()
           ?? throw new InvalidOperationException("No DayNightCycleComponent in the running scene (day/night must be enabled for this genre).");

    private static Beep.ECS.UI.SettingsComponent RequireSettings()
        => Beep.ECS.UI.SettingsComponent.Instance
           ?? throw new InvalidOperationException("Settings autoload is not present — settings only work while the game is running.");

    private static Beep.ECS.UI.LocalizationComponent RequireLocale()
        => Beep.ECS.UI.LocalizationComponent.Instance
           ?? throw new InvalidOperationException("Locale autoload is not present — localization only works while the game is running.");
}
