using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Godot;
using GodotMcp;

namespace Beep.GameBuilder;

/// <summary>
/// PARTIAL: scene management, texture baking and screenshots.
///
/// Before this, the whole MCP surface was catalog + game state + one write
/// (`beep.add_component`). An agent could describe the project but could not open a scene,
/// read its tree, change a property or save it — so every UI change still had to be made by
/// hand. These commands close that loop, and `beep.screenshot` lets the agent SEE the result
/// rather than infer it.
///
/// Reads work wherever the data does; every write goes through the bridge's existing
/// `allow_editor_writes` gate, exactly like `beep.add_component`.
///
///   read   — list_scenes, open_scene, inspect_scene, get_node_property, screenshot
///   write  — set_node_property, add_node, remove_node, save_scene,
///            bake_textures, new_screen
/// </summary>
public static partial class BeepMcpCommands
{
    private static void RegisterSceneCommands()
    {
        // ── Scene reads ──
        McpCommandRegistry.RegisterCommand("beep.list_scenes", args => ListScenes(Str(args, "root")));
        McpCommandRegistry.RegisterCommand("beep.open_scene", args => OpenScene(Str(args, "path")));
        McpCommandRegistry.RegisterCommand("beep.inspect_scene", args => InspectScene(Int(args, "max_depth", 8)));
        McpCommandRegistry.RegisterCommand("beep.get_node_property", args => GetNodeProperty(Str(args, "node"), Str(args, "property")));

        // ── Scene writes (allow_editor_writes) ──
        McpCommandRegistry.RegisterCommand("beep.set_node_property", args => SetNodeProperty(Str(args, "node"), Str(args, "property"), args["value"]));
        McpCommandRegistry.RegisterCommand("beep.add_node", args => AddNode(Str(args, "parent"), Str(args, "type"), Str(args, "name")));
        McpCommandRegistry.RegisterCommand("beep.remove_node", args => RemoveNode(Str(args, "node")));
        McpCommandRegistry.RegisterCommand("beep.save_scene", _ => SaveScene());

        // ── Visual feedback ──
        McpCommandRegistry.RegisterCommand("beep.screenshot", args => Screenshot(Int(args, "max_width", 1280)));

        // ── Skin + scaffold generation ──
        McpCommandRegistry.RegisterCommand("beep.bake_textures", args => BakeTextures(Str(args, "genre"), Str(args, "theme")));
        McpCommandRegistry.RegisterCommand("beep.new_screen", args =>
            NewScreen(Str(args, "genre"), Str(args, "name"), Str(args, "title"), Has(args, "overwrite") && ParseBool(Str(args, "overwrite"))));
    }

    // ════════════════════════════════════════════════════════════════
    // Scene reads
    // ════════════════════════════════════════════════════════════════

    /// <summary>Every .tscn under a root (default res://scenes, plus the addon's templates).</summary>
    private static JsonNode ListScenes(string root)
    {
        var found = new JsonArray();
        foreach (var start in string.IsNullOrEmpty(root)
                     ? new[] { "res://scenes", "res://addons/beep_game_builder_cs/templates/scenes" }
                     : new[] { root })
            Walk(start, found);

        return new JsonObject { ["count"] = found.Count, ["scenes"] = found };

        static void Walk(string dir, JsonArray into)
        {
            using var d = DirAccess.Open(dir);
            if (d == null) return;
            d.ListDirBegin();
            for (string e = d.GetNext(); e != ""; e = d.GetNext())
            {
                if (e.StartsWith(".")) continue;
                string full = dir.EndsWith("/") ? dir + e : $"{dir}/{e}";
                if (d.CurrentIsDir()) Walk(full, into);
                else if (e.EndsWith(".tscn")) into.Add(full);
            }
            d.ListDirEnd();
        }
    }

    private static JsonNode OpenScene(string path)
    {
#if TOOLS
        if (string.IsNullOrEmpty(path)) throw new InvalidOperationException("beep.open_scene needs a 'path'.");
        if (!ResourceLoader.Exists(path)) throw new InvalidOperationException($"No such scene: {path}");
        EditorInterface.Singleton.OpenSceneFromPath(path);
        return new JsonObject { ["opened"] = path };
#else
        throw new InvalidOperationException("beep.open_scene is editor-only.");
#endif
    }

    /// <summary>Full tree of the currently open scene. Reuses the bridge's own serializer,
    /// so node/type/script/property shape matches every other tree the agent sees.</summary>
    private static JsonNode InspectScene(int maxDepth)
    {
#if TOOLS
        var root = EditorInterface.Singleton.GetEditedSceneRoot()
            ?? throw new InvalidOperationException("No scene is open in the editor.");
        return new JsonObject
        {
            ["scene"] = root.SceneFilePath,
            ["root"] = root.Name.ToString(),
            ["tree"] = McpTreeSerializer.Serialize(root, Mathf.Clamp(maxDepth, 1, 32)),
        };
#else
        throw new InvalidOperationException("beep.inspect_scene is editor-only.");
#endif
    }

    private static JsonNode GetNodeProperty(string nodePath, string property)
    {
#if TOOLS
        var node = RequireEditedNode(nodePath);
        if (string.IsNullOrEmpty(property)) throw new InvalidOperationException("beep.get_node_property needs a 'property'.");
        return new JsonObject
        {
            ["node"] = nodePath,
            ["property"] = property,
            ["value"] = McpJson.FromVariant(node.Get(property)),
        };
#else
        throw new InvalidOperationException("beep.get_node_property is editor-only.");
#endif
    }

    // ════════════════════════════════════════════════════════════════
    // Scene writes
    // ════════════════════════════════════════════════════════════════

    private static JsonNode SetNodeProperty(string nodePath, string property, JsonNode? value)
    {
#if TOOLS
        RequireEditorWrites("beep.set_node_property");
        var node = RequireEditedNode(nodePath);
        if (string.IsNullOrEmpty(property)) throw new InvalidOperationException("beep.set_node_property needs a 'property'.");

        // Godot registers a C# [Export] under its exact PascalCase name. A snake_case spelling
        // matches nothing, the assignment is DROPPED, and the scene still saves and loads — the
        // failure that cost this repo 67 dead assignments across 33 scenes. Refuse it outright
        // rather than let an agent write a scene that silently does nothing.
        if (property.Contains('_') && node.GetPropertyList()
                .Any(p => string.Equals(p["name"].AsString(), ToPascalCase(property), StringComparison.Ordinal)))
            throw new InvalidOperationException(
                $"'{property}' is a C# [Export] and must be written PascalCase as '{ToPascalCase(property)}' — Godot silently drops the snake_case form.");

        var before = node.Get(property);
        node.Set(property, McpJson.ToVariant(value));
        var after = node.Get(property);

        // A property Godot does not know is not an error at the Variant layer — the set is just
        // discarded. Report it instead of returning a cheerful success.
        bool known = node.GetPropertyList().Any(p => string.Equals(p["name"].AsString(), property, StringComparison.Ordinal));
        return new JsonObject
        {
            ["node"] = nodePath,
            ["property"] = property,
            ["known_property"] = known,
            ["before"] = McpJson.FromVariant(before),
            ["after"] = McpJson.FromVariant(after),
            ["note"] = known ? null : $"'{property}' is not a registered property on {node.GetType().Name} — the value was discarded.",
        };
#else
        throw new InvalidOperationException("beep.set_node_property is editor-only.");
#endif
    }

    private static JsonNode AddNode(string parentPath, string typeName, string name)
    {
#if TOOLS
        RequireEditorWrites("beep.add_node");
        var root = EditorInterface.Singleton.GetEditedSceneRoot()
            ?? throw new InvalidOperationException("No scene is open in the editor.");
        var parent = string.IsNullOrEmpty(parentPath) || parentPath == "." ? root : RequireEditedNode(parentPath);

        if (string.IsNullOrEmpty(typeName) || !ClassDB.ClassExists(typeName))
            throw new InvalidOperationException($"Unknown node type '{typeName}'.");
        if (!ClassDB.CanInstantiate(typeName))
            throw new InvalidOperationException($"'{typeName}' cannot be instantiated (abstract or singleton).");

        var node = (Node)ClassDB.Instantiate(typeName);
        if (!string.IsNullOrEmpty(name)) node.Name = name;
        parent.AddChild(node);
        // Without an Owner the node is dropped on save — the same trap add_component documents.
        node.Owner = root;

        return new JsonObject { ["added"] = node.Name.ToString(), ["path"] = root.GetPathTo(node).ToString(), ["type"] = typeName };
#else
        throw new InvalidOperationException("beep.add_node is editor-only.");
#endif
    }

    private static JsonNode RemoveNode(string nodePath)
    {
#if TOOLS
        RequireEditorWrites("beep.remove_node");
        var root = EditorInterface.Singleton.GetEditedSceneRoot()
            ?? throw new InvalidOperationException("No scene is open in the editor.");
        var node = RequireEditedNode(nodePath);
        if (node == root) throw new InvalidOperationException("Refusing to remove the scene root.");

        // A NodePath export still aimed at this node would silently resolve to null after the
        // removal, which is precisely how this framework's components fail quietly. Say no.
        var referrers = new List<string>();
        FindReferrers(root, root, node, referrers);
        if (referrers.Count > 0)
            throw new InvalidOperationException(
                $"'{nodePath}' is still referenced by a NodePath export on: {string.Join(", ", referrers)}. Clear those first.");

        node.GetParent().RemoveChild(node);
        node.QueueFree();
        return new JsonObject { ["removed"] = nodePath };

        static void FindReferrers(Node root, Node current, Node target, List<string> into)
        {
            foreach (var prop in current.GetPropertyList())
            {
                if ((Variant.Type)(int)prop["type"] != Variant.Type.NodePath) continue;
                var np = current.Get(prop["name"].AsString()).AsNodePath();
                if (np.IsEmpty) continue;
                if (current.GetNodeOrNull(np) == target)
                    into.Add($"{root.GetPathTo(current)}.{prop["name"].AsString()}");
            }
            foreach (var child in current.GetChildren()) FindReferrers(root, child, target, into);
        }
#else
        throw new InvalidOperationException("beep.remove_node is editor-only.");
#endif
    }

    private static JsonNode SaveScene()
    {
#if TOOLS
        RequireEditorWrites("beep.save_scene");
        var root = EditorInterface.Singleton.GetEditedSceneRoot()
            ?? throw new InvalidOperationException("No scene is open in the editor.");
        var err = EditorInterface.Singleton.SaveScene();
        if (err != Error.Ok) throw new InvalidOperationException($"SaveScene failed: {err}");
        return new JsonObject { ["saved"] = root.SceneFilePath };
#else
        throw new InvalidOperationException("beep.save_scene is editor-only.");
#endif
    }

    // ════════════════════════════════════════════════════════════════
    // Visual feedback
    // ════════════════════════════════════════════════════════════════

    /// <summary>PNG of the current viewport, base64-encoded, so an agent can look at what it
    /// just changed instead of inferring it from the tree.</summary>
    private static JsonNode Screenshot(int maxWidth)
    {
        if (Engine.GetMainLoop() is not SceneTree tree || tree.Root is not { } viewport)
            throw new InvalidOperationException("No viewport available.");
        var img = viewport.GetTexture()?.GetImage()
            ?? throw new InvalidOperationException("Viewport produced no image.");

        int cap = Mathf.Clamp(maxWidth, 64, 4096);
        if (img.GetWidth() > cap)
        {
            // Cap the payload: a 4K frame is several MB of base64 through a WebSocket.
            int h = Mathf.Max(1, (int)(img.GetHeight() * (cap / (float)img.GetWidth())));
            img.Resize(cap, h, Image.Interpolation.Bilinear);
        }

        var png = img.SavePngToBuffer();
        return new JsonObject
        {
            ["format"] = "png",
            ["width"] = img.GetWidth(),
            ["height"] = img.GetHeight(),
            ["base64"] = Convert.ToBase64String(png),
        };
    }

    // ════════════════════════════════════════════════════════════════
    // Skin + scaffold generation
    // ════════════════════════════════════════════════════════════════

    private static JsonNode BakeTextures(string genre, string theme)
    {
#if TOOLS
        RequireEditorWrites("beep.bake_textures");
        List<string> log =
            !string.IsNullOrEmpty(genre) && !string.IsNullOrEmpty(theme) ? BeepTextureBaker.BakeTheme(genre, theme)
            : !string.IsNullOrEmpty(genre) ? BeepTextureBaker.BakeGenre(genre)
            : BeepTextureBaker.BakeAll();
        return new JsonObject { ["log"] = new JsonArray(log.Select(l => (JsonNode)l!).ToArray()) };
#else
        throw new InvalidOperationException("beep.bake_textures is editor-only (res:// is read-only in an exported game).");
#endif
    }

    private static JsonNode NewScreen(string genre, string name, string title, bool overwrite)
    {
#if TOOLS
        RequireEditorWrites("beep.new_screen");
        if (string.IsNullOrEmpty(genre)) throw new InvalidOperationException("beep.new_screen needs a 'genre'.");
        if (string.IsNullOrEmpty(name)) throw new InvalidOperationException("beep.new_screen needs a 'name'.");
        var log = BeepScreenGenerator.CreateScreen(genre, name, title, overwrite);
        return new JsonObject { ["log"] = new JsonArray(log.Select(l => (JsonNode)l!).ToArray()) };
#else
        throw new InvalidOperationException("beep.new_screen is editor-only.");
#endif
    }

    // ── helpers ──

#if TOOLS
    private static Node RequireEditedNode(string nodePath)
    {
        var root = EditorInterface.Singleton.GetEditedSceneRoot()
            ?? throw new InvalidOperationException("No scene is open in the editor.");
        if (string.IsNullOrEmpty(nodePath) || nodePath == "." || nodePath == "/") return root;
        return root.GetNodeOrNull(nodePath)
            ?? throw new InvalidOperationException($"Node not found in the open scene: {nodePath}");
    }
#endif

    /// <summary>"title_label_path" → "TitleLabelPath", to name the PascalCase form a
    /// snake_case [Export] should have been written as.</summary>
    private static string ToPascalCase(string snake)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var part in snake.Split('_', StringSplitOptions.RemoveEmptyEntries))
            sb.Append(char.ToUpperInvariant(part[0])).Append(part.Length > 1 ? part[1..] : "");
        return sb.ToString();
    }
}
