using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Godot;

namespace GodotMcp;

/// <summary>
/// PARTIAL: Phase 2 — creative authoring.
///
/// Before this the bridge could set a property and create a node, and that was the whole
/// of its creative power. It could not create a Resource, edit a Theme, build an
/// Animation, connect a Signal, instance a PackedScene, write a script, or ask what a
/// class even offers — which is to say it could restyle an existing button but could not
/// build a screen, a theme or an effect. Everything this framework is made of was out of
/// reach.
///
/// See docs/mcp/PHASE_2_AUTHORING.md.
/// </summary>
public partial class GodotMcpBridgeController
{
    private bool TryExecuteAuthoring(string method, JsonObject p, out JsonNode? result)
    {
        switch (method)
        {
            case "resource.create": result = ResourceCreate(p); return true;
            case "resource.load": result = ResourceLoadInfo(p); return true;
            case "resource.set": result = ResourceSet(p); return true;

            case "theme.create": result = ThemeCreate(p); return true;
            case "theme.set_stylebox": result = ThemeSetStylebox(p); return true;
            case "theme.set_value": result = ThemeSetValue(p); return true;
            case "theme.add_type_variation": result = ThemeAddTypeVariation(p); return true;

            case "animation.create": result = AnimationCreate(p); return true;
            case "animation.add_track": result = AnimationAddTrack(p); return true;

            case "signal.list": result = SignalList(p); return true;
            case "signal.connect": result = SignalConnect(p); return true;
            case "signal.disconnect": result = SignalDisconnect(p); return true;

            case "scene.instance": result = SceneInstance(p); return true;
            case "scene.save_as": result = SceneSaveAs(p); return true;
            case "scene.duplicate_node": result = SceneDuplicateNode(p); return true;

            case "script.attach": result = ScriptAttach(p); return true;

            case "classdb.list": result = ClassDbList(p); return true;
            case "classdb.describe": result = ClassDbDescribe(p); return true;

            default: result = null; return false;
        }
    }

    // ════════════════════════════════════════════════════════════════
    // Resources — GameInfo, ColorPalette, GeometryProfile are all Resources
    // ════════════════════════════════════════════════════════════════

    /// <summary>Build a Resource of any class, apply properties, save as .tres.</summary>
    private JsonNode ResourceCreate(JsonObject p)
    {
        RequireWrites();
        string type = RequiredString(p, "type");
        string path = RequiredString(p, "path");

        Resource res = InstantiateResource(type);
        var applied = ApplyProperties(res, p["properties"] as JsonObject, type);

        Error err = ResourceSaver.Save(res, path);
        if (err != Error.Ok)
            throw new McpBridgeException("SAVE_FAILED", $"Could not save {type} to '{path}': {err}",
                "Check the directory exists and the path starts with res:// or user://.");

        return new JsonObject { ["created"] = path, ["type"] = type, ["applied"] = applied };
    }

    private JsonNode ResourceLoadInfo(JsonObject p)
    {
        string path = RequiredString(p, "path");
        Resource res = LoadResource(path);
        var props = new JsonObject();
        foreach (var name in McpWriteGuard.PropertyNames(res))
            props[name] = McpJson.FromVariant(res.Get(name));
        return new JsonObject
        {
            ["path"] = path,
            ["type"] = res.GetType().Name,
            ["class"] = res.GetClass(),
            ["properties"] = props,
        };
    }

    private JsonNode ResourceSet(JsonObject p)
    {
        RequireWrites();
        string path = RequiredString(p, "path");
        Resource res = LoadResource(path);
        var applied = ApplyProperties(res, p["properties"] as JsonObject, res.GetType().Name);

        Error err = ResourceSaver.Save(res, path);
        if (err != Error.Ok)
            throw new McpBridgeException("SAVE_FAILED", $"Could not re-save '{path}': {err}");

        return new JsonObject { ["updated"] = path, ["applied"] = applied };
    }

    private static Resource InstantiateResource(string type)
    {
        if (!ClassDB.ClassExists(type))
            throw McpBridgeException.InvalidParams($"Unknown class '{type}'.",
                "Call classdb.list with inherits:'Resource' to see what can be created.");
        if (!ClassDB.CanInstantiate(type))
            throw McpBridgeException.InvalidParams($"'{type}' cannot be instantiated (abstract or a singleton).");
        if ((GodotObject)ClassDB.Instantiate(new StringName(type)) is not Resource res)
            throw McpBridgeException.InvalidParams($"'{type}' is not a Resource.",
                "Use node.create for Nodes.");
        return res;
    }

    private static Resource LoadResource(string path)
    {
        if (!ResourceLoader.Exists(path))
            throw new McpBridgeException("RESOURCE_NOT_FOUND", $"No resource at '{path}'.",
                "Check the res:// path.");
        return ResourceLoader.Load<Resource>(path)
            ?? throw new McpBridgeException("RESOURCE_NOT_FOUND", $"'{path}' did not load as a Resource.");
    }

    /// <summary>Apply a property dict, refusing anything the object would silently drop.
    /// A C# [Export] must be PascalCase — GameInfo.GameName, never game_name.</summary>
    private static JsonArray ApplyProperties(GodotObject target, JsonObject? properties, string typeName)
    {
        var applied = new JsonArray();
        if (properties is null) return applied;

        var known = McpWriteGuard.PropertyNames(target);
        foreach (var kv in properties)
        {
            string name = kv.Key;
            if (!known.Contains(name))
            {
                if (name.Contains('_'))
                {
                    string pascal = McpWriteGuard.ToPascalCase(name);
                    if (known.Contains(pascal)) throw McpBridgeException.SnakeCaseExport(name, pascal);
                }
                throw McpBridgeException.UnknownProperty(typeName, name);
            }
            target.Set(name, McpJson.ToVariant(kv.Value));
            applied.Add(name);
        }
        return applied;
    }

    // ════════════════════════════════════════════════════════════════
    // Themes
    // ════════════════════════════════════════════════════════════════

    private JsonNode ThemeCreate(JsonObject p)
    {
        RequireWrites();
        string path = RequiredString(p, "path");
        var theme = new Theme();
        Error err = ResourceSaver.Save(theme, path);
        if (err != Error.Ok)
            throw new McpBridgeException("SAVE_FAILED", $"Could not save Theme to '{path}': {err}");
        return new JsonObject { ["created"] = path };
    }

    /// <summary>Set a StyleBox on a theme type. The box is built from a `stylebox` spec —
    /// `{ "class": "StyleBoxFlat", "properties": { "BgColor": …, "CornerRadiusTopLeft": 8 } }`
    /// — so any StyleBox class works without a bespoke schema per box type.</summary>
    private JsonNode ThemeSetStylebox(JsonObject p)
    {
        RequireWrites();
        string path = RequiredString(p, "path");
        string type = RequiredString(p, "type");
        string name = RequiredString(p, "name");

        if (LoadResource(path) is not Theme theme)
            throw McpBridgeException.InvalidParams($"'{path}' is not a Theme.");

        var spec = p["stylebox"] as JsonObject
            ?? throw McpBridgeException.InvalidParams("theme.set_stylebox needs a 'stylebox' object.",
                   "Pass { class: 'StyleBoxFlat', properties: { BgColor: {...} } }.");
        string boxClass = spec["class"]?.GetValue<string>() ?? "StyleBoxFlat";

        if (InstantiateResource(boxClass) is not StyleBox box)
            throw McpBridgeException.InvalidParams($"'{boxClass}' is not a StyleBox.");
        ApplyProperties(box, spec["properties"] as JsonObject, boxClass);

        theme.SetStylebox(name, type, box);
        Error err = ResourceSaver.Save(theme, path);
        if (err != Error.Ok) throw new McpBridgeException("SAVE_FAILED", $"Could not save '{path}': {err}");

        return new JsonObject { ["theme"] = path, ["type"] = type, ["stylebox"] = name, ["class"] = boxClass };
    }

    /// <summary>Set a color / font_size / constant on a theme type — one method rather
    /// than three near-identical ones.</summary>
    private JsonNode ThemeSetValue(JsonObject p)
    {
        RequireWrites();
        string path = RequiredString(p, "path");
        string kind = RequiredString(p, "kind");      // color | font_size | constant
        string type = RequiredString(p, "type");
        string name = RequiredString(p, "name");

        if (LoadResource(path) is not Theme theme)
            throw McpBridgeException.InvalidParams($"'{path}' is not a Theme.");

        Variant value = McpJson.ToVariant(p["value"]);
        switch (kind)
        {
            case "color": theme.SetColor(name, type, value.AsColor()); break;
            case "font_size": theme.SetFontSize(name, type, value.AsInt32()); break;
            case "constant": theme.SetConstant(name, type, value.AsInt32()); break;
            default:
                throw McpBridgeException.InvalidParams($"Unknown kind '{kind}'.",
                    "Use 'color', 'font_size' or 'constant'.");
        }

        Error err = ResourceSaver.Save(theme, path);
        if (err != Error.Ok) throw new McpBridgeException("SAVE_FAILED", $"Could not save '{path}': {err}");
        return new JsonObject { ["theme"] = path, ["kind"] = kind, ["type"] = type, ["name"] = name };
    }

    /// <summary>Register a Label type variation.
    ///
    /// ThemePresetComponent registers exactly four — BeepTitle, BeepSubtitle, BeepValue,
    /// BeepCaption — and validate_scenes.sh FAILS on a scene using any other. Inventing a
    /// fifth here would produce scenes that render at base size and break the gate, so
    /// warn loudly when the name is not one of them.</summary>
    private JsonNode ThemeAddTypeVariation(JsonObject p)
    {
        RequireWrites();
        string path = RequiredString(p, "path");
        string variation = RequiredString(p, "variation");
        string baseType = p["base"]?.GetValue<string>() ?? "Label";

        if (LoadResource(path) is not Theme theme)
            throw McpBridgeException.InvalidParams($"'{path}' is not a Theme.");

        theme.AddType(variation);
        theme.SetTypeVariation(variation, baseType);
        Error err = ResourceSaver.Save(theme, path);
        if (err != Error.Ok) throw new McpBridgeException("SAVE_FAILED", $"Could not save '{path}': {err}");

        var result = new JsonObject { ["theme"] = path, ["variation"] = variation, ["base"] = baseType };
        if (Array.IndexOf(BeepVariations, variation) < 0)
            result["warning"] =
                $"'{variation}' is not one of Beep's registered variations ({string.Join(", ", BeepVariations)}). " +
                "A scene using it renders at base size and validate_scenes.sh will fail on it.";
        return result;
    }

    private static readonly string[] BeepVariations = { "BeepTitle", "BeepSubtitle", "BeepValue", "BeepCaption" };

    // ════════════════════════════════════════════════════════════════
    // Animation
    // ════════════════════════════════════════════════════════════════

    private JsonNode AnimationCreate(JsonObject p)
    {
        RequireWrites();
        Node playerNode = ResolveNode(RequiredString(p, "player_path"))
            ?? throw McpBridgeException.NodeNotFound(RequiredString(p, "player_path"));
        if (playerNode is not AnimationPlayer player)
            throw McpBridgeException.InvalidParams($"'{playerNode.Name}' is not an AnimationPlayer.");

        string name = RequiredString(p, "name");
        var anim = new Animation
        {
            Length = (float)(p["length"]?.GetValue<double>() ?? 1.0),
            LoopMode = (p["loop"]?.GetValue<bool>() ?? false) ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None,
        };

        // Godot 4 keeps animations in libraries; the default one is "".
        AnimationLibrary lib;
        if (player.HasAnimationLibrary(""))
        {
            lib = player.GetAnimationLibrary("");
        }
        else
        {
            lib = new AnimationLibrary();
            player.AddAnimationLibrary("", lib);
        }
        if (lib.HasAnimation(name)) lib.RemoveAnimation(name);
        lib.AddAnimation(name, anim);

        return new JsonObject { ["created"] = name, ["player"] = player.GetPath().ToString(), ["length"] = anim.Length };
    }

    /// <summary>Add a value track. Refuses the two mistakes this repo has already paid for
    /// twice — see the guard below.</summary>
    private JsonNode AnimationAddTrack(JsonObject p)
    {
        RequireWrites();
        Node playerNode = ResolveNode(RequiredString(p, "player_path"))
            ?? throw McpBridgeException.NodeNotFound(RequiredString(p, "player_path"));
        if (playerNode is not AnimationPlayer player)
            throw McpBridgeException.InvalidParams($"'{playerNode.Name}' is not an AnimationPlayer.");

        string animName = RequiredString(p, "name");
        Animation anim = player.HasAnimation(animName)
            ? player.GetAnimation(animName)
            : throw McpBridgeException.InvalidParams($"AnimationPlayer has no animation '{animName}'.",
                  "Create it with animation.create first.");

        string nodePath = RequiredString(p, "node_path");
        string property = RequiredString(p, "property");
        GuardAnimatedProperty(player, nodePath, property);

        int track = anim.AddTrack(Animation.TrackType.Value);
        anim.TrackSetPath(track, new NodePath($"{nodePath}:{property}"));

        var keys = p["keys"] as JsonArray ?? new JsonArray();
        foreach (var k in keys)
        {
            if (k is not JsonObject key) continue;
            double time = key["time"]?.GetValue<double>() ?? 0.0;
            anim.TrackInsertKey(track, time, McpJson.ToVariant(key["value"]));
        }

        return new JsonObject
        {
            ["animation"] = animName,
            ["track"] = track,
            ["path"] = $"{nodePath}:{property}",
            ["keys"] = keys.Count,
        };
    }

    /// <summary>
    /// Refuse animating a Control's position/scale/rotation inside a Container.
    ///
    /// The container re-sorts its children every layout pass and overwrites the animated
    /// value, so the animation silently does nothing — the exact defect that made this
    /// repo's menu animations dead, twice. Godot 4.7's offset_transform_* is a render-only
    /// transform containers do not touch.
    /// </summary>
    private static void GuardAnimatedProperty(Node from, string nodePath, string property)
    {
        string bare = property.Split(':')[0];
        if (bare is not ("position" or "scale" or "rotation")) return;

        Node? target = from.GetNodeOrNull(nodePath);
        if (target is not Control) return;
        if (target.GetParent() is not Container) return;

        throw new McpBridgeException("CONTAINER_OVERWRITES_TRANSFORM",
            $"'{bare}' on a Control inside a {target.GetParent()!.GetType().Name} is overwritten every layout pass — the track would do nothing.",
            $"Animate 'offset_transform_{bare}' instead (render-only; containers leave it alone). " +
            (bare is "scale" or "rotation"
                ? "Also set pivot_offset first — it defaults to the top-left corner, so the effect grows/turns toward the corner."
                : "Neutral is Vector2.Zero; offsets are relative to the laid-out position."),
            new Dictionary<string, object?> { ["property"] = bare, ["suggested"] = $"offset_transform_{bare}" });
    }

    // ════════════════════════════════════════════════════════════════
    // Signals — scene data, so this is real authoring
    // ════════════════════════════════════════════════════════════════

    private JsonNode SignalList(JsonObject p)
    {
        Node node = RequireNode(p);
        var signals = new JsonArray();
        foreach (var s in node.GetSignalList())
        {
            string name = s["name"].AsString();
            var conns = new JsonArray();
            foreach (var c in node.GetSignalConnectionList(name))
            {
                var callable = c["callable"].AsCallable();
                conns.Add(new JsonObject
                {
                    ["target"] = callable.Target is Node n ? n.GetPath().ToString() : callable.Target?.ToString(),
                    ["method"] = callable.Method.ToString(),
                });
            }
            signals.Add(new JsonObject { ["name"] = name, ["connections"] = conns });
        }
        return new JsonObject { ["path"] = node.GetPath().ToString(), ["signals"] = signals };
    }

    private JsonNode SignalConnect(JsonObject p)
    {
        RequireWrites();
        Node from = RequireNode(p);
        string signal = RequiredString(p, "signal");
        string toPath = RequiredString(p, "to");
        string method = RequiredString(p, "method");

        Node to = ResolveNode(toPath) ?? throw McpBridgeException.NodeNotFound(toPath);

        if (!HasSignal(from, signal))
            throw new McpBridgeException("UNKNOWN_SIGNAL", $"'{from.GetType().Name}' has no signal '{signal}'.",
                "Call signal.list for the real names.");
        if (!to.HasMethod(method))
            throw new McpBridgeException("UNKNOWN_METHOD",
                $"'{to.Name}' has no method '{method}' — the connection would fire into nothing.",
                "Attach a script defining it, or pick an existing method.");

        // Persisted so the connection survives in the .tscn, not just this session.
        Error err = from.Connect(signal, new Callable(to, method), (uint)GodotObject.ConnectFlags.Persist);
        if (err != Error.Ok && err != Error.InvalidParameter)
            throw new McpBridgeException("CONNECT_FAILED", $"Connect failed: {err}");

        return new JsonObject
        {
            ["from"] = from.GetPath().ToString(),
            ["signal"] = signal,
            ["to"] = to.GetPath().ToString(),
            ["method"] = method,
            ["already_connected"] = err == Error.InvalidParameter,
        };
    }

    private JsonNode SignalDisconnect(JsonObject p)
    {
        RequireWrites();
        Node from = RequireNode(p);
        string signal = RequiredString(p, "signal");
        Node to = ResolveNode(RequiredString(p, "to")) ?? throw McpBridgeException.NodeNotFound(RequiredString(p, "to"));
        string method = RequiredString(p, "method");

        var callable = new Callable(to, method);
        if (!from.IsConnected(signal, callable))
            return new JsonObject { ["disconnected"] = false, ["note"] = "That connection did not exist." };

        from.Disconnect(signal, callable);
        return new JsonObject { ["disconnected"] = true, ["signal"] = signal, ["method"] = method };
    }

    private static bool HasSignal(Node node, string signal)
    {
        foreach (var s in node.GetSignalList())
            if (s["name"].AsString() == signal) return true;
        return false;
    }

    // ════════════════════════════════════════════════════════════════
    // Scene composition
    // ════════════════════════════════════════════════════════════════

    /// <summary>Instance a PackedScene into the open scene. This is the one that matters:
    /// templates/scenes/ exists to be instanced, and an instance keeps tracking the
    /// template's future edits — a hand-built copy does not.</summary>
    private JsonNode SceneInstance(JsonObject p)
    {
        RequireWrites();
        string scenePath = RequiredString(p, "scene");
        if (!ResourceLoader.Exists(scenePath))
            throw new McpBridgeException("RESOURCE_NOT_FOUND", $"No scene at '{scenePath}'.");

        var packed = ResourceLoader.Load<PackedScene>(scenePath)
            ?? throw McpBridgeException.InvalidParams($"'{scenePath}' is not a PackedScene.");

        Node root = GetCurrentSceneRoot();
        string parentPath = p["parent"]?.GetValue<string>() ?? ".";
        Node parent = parentPath is "." or "" ? root
            : ResolveNode(parentPath) ?? throw McpBridgeException.NodeNotFound(parentPath);

        Node instance = packed.Instantiate();
        if (p["name"]?.GetValue<string>() is { Length: > 0 } n) instance.Name = n;

        using var scope = McpUndoScope.Begin($"instance {scenePath.GetFile()}", EditorUndoManager());
        scope.AddChild(parent, instance, root);

        return new JsonObject
        {
            ["instanced"] = scenePath,
            ["name"] = instance.Name.ToString(),
            ["parent"] = parent.GetPath().ToString(),
            ["undoable"] = scope.IsUndoable,
        };
    }

    private JsonNode SceneSaveAs(JsonObject p)
    {
        RequireWrites();
        string path = RequiredString(p, "path");
        Node root = GetCurrentSceneRoot();

        var packed = new PackedScene();
        Error pack = packed.Pack(root);
        if (pack != Error.Ok)
            throw new McpBridgeException("SAVE_FAILED", $"Could not pack the scene: {pack}",
                "Every node to be saved needs its Owner set to the scene root.");

        Error err = ResourceSaver.Save(packed, path);
        if (err != Error.Ok) throw new McpBridgeException("SAVE_FAILED", $"Could not save '{path}': {err}");
        return new JsonObject { ["saved"] = path, ["root"] = root.Name.ToString() };
    }

    private JsonNode SceneDuplicateNode(JsonObject p)
    {
        RequireWrites();
        Node node = RequireNode(p);
        Node root = GetCurrentSceneRoot();
        Node parent = node.GetParent() ?? throw McpBridgeException.InvalidParams("Cannot duplicate the scene root.");

        Node copy = node.Duplicate();
        if (p["new_name"]?.GetValue<string>() is { Length: > 0 } n) copy.Name = n;

        using var scope = McpUndoScope.Begin($"duplicate {node.Name}", EditorUndoManager());
        scope.AddChild(parent, copy, root);

        return new JsonObject
        {
            ["duplicated"] = node.GetPath().ToString(),
            ["name"] = copy.Name.ToString(),
            ["undoable"] = scope.IsUndoable,
        };
    }

    // ════════════════════════════════════════════════════════════════
    // Scripts
    // ════════════════════════════════════════════════════════════════

    /// <summary>Attach an existing script to a node.
    ///
    /// Deliberately no script.create here: a C# file must be compiled before Godot knows
    /// the type, its file name must equal the class name, and a file that does not compile
    /// takes the whole addon down. Generation belongs to BeepScreenGenerator
    /// (beep.new_screen), which emits a shape that is known to build.</summary>
    private JsonNode ScriptAttach(JsonObject p)
    {
        RequireWrites();
        Node node = RequireNode(p);
        string path = RequiredString(p, "script");

        if (!ResourceLoader.Exists(path))
            throw new McpBridgeException("RESOURCE_NOT_FOUND", $"No script at '{path}'.",
                "For C#, build the project first — Godot only registers a script it has compiled.");

        var script = ResourceLoader.Load<Script>(path)
            ?? throw McpBridgeException.InvalidParams($"'{path}' did not load as a Script.");

        // "script" is an Object-level property, not one of Node.PropertyName's generated
        // constants, so it is addressed by name.
        using var scope = McpUndoScope.Begin($"attach {path.GetFile()}", EditorUndoManager());
        scope.SetProperty(node, "script", script);

        return new JsonObject
        {
            ["node"] = node.GetPath().ToString(),
            ["script"] = path,
            ["undoable"] = scope.IsUndoable,
        };
    }

    // ════════════════════════════════════════════════════════════════
    // ClassDB — so an agent stops guessing type and property names
    // ════════════════════════════════════════════════════════════════

    private JsonNode ClassDbList(JsonObject p)
    {
        string inherits = p["inherits"]?.GetValue<string>() ?? "";
        string filter = (p["filter"]?.GetValue<string>() ?? "").ToLowerInvariant();

        var names = new JsonArray();
        var all = string.IsNullOrEmpty(inherits)
            ? ClassDB.GetClassList()
            : ClassDB.GetInheritersFromClass(inherits);

        foreach (var c in all)
        {
            string s = c.ToString();
            if (filter.Length > 0 && !s.ToLowerInvariant().Contains(filter)) continue;
            if (!ClassDB.CanInstantiate(s)) continue;
            names.Add(s);
        }
        return new JsonObject { ["count"] = names.Count, ["classes"] = names };
    }

    private JsonNode ClassDbDescribe(JsonObject p)
    {
        string cls = RequiredString(p, "class");
        if (!ClassDB.ClassExists(cls))
            throw McpBridgeException.InvalidParams($"Unknown class '{cls}'.",
                "Call classdb.list to see what exists.");

        var props = new JsonArray();
        foreach (var prop in ClassDB.ClassGetPropertyList(cls, noInheritance: false))
        {
            if (!prop.ContainsKey("name")) continue;
            props.Add(new JsonObject
            {
                ["name"] = prop["name"].AsString(),
                ["type"] = ((Variant.Type)(int)prop["type"]).ToString(),
            });
        }

        var signals = new JsonArray();
        foreach (var s in ClassDB.ClassGetSignalList(cls, noInheritance: false))
            if (s.ContainsKey("name")) signals.Add(s["name"].AsString());

        return new JsonObject
        {
            ["class"] = cls,
            ["parent"] = ClassDB.GetParentClass(cls).ToString(),
            ["instantiable"] = ClassDB.CanInstantiate(cls),
            ["properties"] = props,
            ["signals"] = signals,
        };
    }
}
