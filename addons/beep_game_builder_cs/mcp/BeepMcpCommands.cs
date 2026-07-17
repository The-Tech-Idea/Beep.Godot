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
/// Availability differs by context, because the catalog is files but generation and
/// the open scene are editor-only:
///   editor + runtime — beep.list_genres, beep.list_themes, beep.list_palettes, beep.catalog
///   editor only      — beep.generate_project, beep.apply_skin
///   runtime only     — beep.game_state (needs the GameApp autoload)
/// </summary>
public static class BeepMcpCommands
{
    private const string Prefix = "beep.";

    public static void Register()
    {
        // ── Read the catalog (safe, read-only, works everywhere) ──
        McpCommandRegistry.RegisterCommand("beep.list_genres", _ => ListGenres());
        McpCommandRegistry.RegisterCommand("beep.list_themes", args => ListThemes(Str(args, "genre")));
        McpCommandRegistry.RegisterCommand("beep.list_palettes", args => ListPalettes(Str(args, "genre"), Str(args, "theme")));
        McpCommandRegistry.RegisterCommand("beep.catalog", _ => FullCatalog());

        // ── Components (discovery + inspection + creation) ──
        McpCommandRegistry.RegisterCommand("beep.list_components", args => ListComponents(Str(args, "category"), Str(args, "search")));
        McpCommandRegistry.RegisterCommand("beep.component_info", args => ComponentInfo(Str(args, "type")));
        McpCommandRegistry.RegisterCommand("beep.add_component", args =>
            AddComponent(Str(args, "node"), Str(args, "type"), args["properties"] as JsonObject));

        // ── Act ──
        McpCommandRegistry.RegisterCommand("beep.apply_skin", args =>
            ApplySkin(Str(args, "genre"), Str(args, "theme"), Str(args, "palette")));
        McpCommandRegistry.RegisterCommand("beep.generate_project", args =>
            GenerateProject(Str(args, "genre"), Str(args, "theme"), Str(args, "palette")));

        // ── Read live game state ──
        McpCommandRegistry.RegisterState("beep.game_state", GameState);
        McpCommandRegistry.RegisterCommand("beep.game_state", _ => GameState());
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
        => args[key]?.GetValue<string>() ?? "";
}
