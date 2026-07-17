using System;
using System.IO;
using System.Text.Json.Nodes;
using Godot;

namespace GodotMcp;

/// <summary>
/// Common bridge command dispatcher used by the editor plugin and runtime autoload.
/// </summary>
[Tool]
public partial class GodotMcpBridgeController : Node
{
    public event Action<string>? StatusChanged;

#if TOOLS
    private EditorPlugin? _editorPlugin;
#endif
    private McpWebSocketClient? _client;
    private string _role = "runtime";
    private bool _verbose = true;

#if TOOLS
    public void ConfigureEditor(EditorPlugin editorPlugin)
    {
        _editorPlugin = editorPlugin;
    }
#endif

    public void Configure(string url, string token, string role, bool verbose, double reconnectDelaySeconds)
    {
        _role = string.IsNullOrWhiteSpace(role) ? "runtime" : role;
        _verbose = verbose;

        if (_client is null)
        {
            _client = new McpWebSocketClient();
            _client.Name = $"McpWebSocketClient_{_role}";
            _client.MessageReceived += HandleBridgeMessage;
            _client.StatusChanged += s => StatusChanged?.Invoke(s);
            AddChild(_client);
        }

        _client.Configure(url, token, _role, verbose, reconnectDelaySeconds);
    }

    public void ConnectBridge()
    {
        _client?.ConnectBridge();
    }

    public void DisconnectBridge()
    {
        _client?.DisconnectBridge();
    }

    public override void _ExitTree()
    {
        if (_client is not null)
        {
            _client.MessageReceived -= HandleBridgeMessage;
            _client.DisconnectBridge();
        }
    }

    private void HandleBridgeMessage(JsonObject message)
    {
        string id = message["id"]?.GetValue<string>() ?? Guid.NewGuid().ToString("N");
        string method = message["method"]?.GetValue<string>() ?? string.Empty;
        JsonObject parameters = message["params"] as JsonObject ?? new JsonObject();

        try
        {
            JsonNode? result = ExecuteMethod(method, parameters);
            SendOk(id, result);
        }
        catch (Exception ex)
        {
            SendError(id, ex.Message, ex.GetType().Name);
        }
    }

    private void SendOk(string id, JsonNode? result)
    {
        _client?.Send(new JsonObject
        {
            ["id"] = id,
            ["ok"] = true,
            ["result"] = result
        });
    }

    private void SendError(string id, string error, string type)
    {
        _client?.Send(new JsonObject
        {
            ["id"] = id,
            ["ok"] = false,
            ["error"] = error,
            ["error_type"] = type
        });
    }

    private JsonNode? ExecuteMethod(string method, JsonObject p)
    {
        return method switch
        {
            "ping" => Ping(),
            "status.get" => StatusGet(),

            "tree.serialize" => SerializeTree(p),
            "scene.current" => CurrentScene(p),
            "editor.selection.get" => EditorSelectionGet(),
            "editor.selection.set" => EditorSelectionSet(p),

            "node.get" => GetNodeInfo(p),
            "node.list_properties" => ListNodeProperties(p),
            "node.set_property" => SetNodeProperty(p),
            "node.call_method" => CallNodeMethod(p),
            "node.create" => CreateNode(p),
            "node.delete" => DeleteNode(p),
            "node.reparent" => ReparentNode(p),

            "shader.attach_canvas_item" => AttachCanvasItemShader(p),
            "shader.set_uniform" => SetShaderUniform(p),

            "tween.property" => TweenProperty(p),
            "particles.create_2d" => CreateParticles2D(p),
            "projectile.sample_arc_2d" => SampleProjectileArc2D(p),
            "sprite.move_to" => SpriteMoveTo(p),

            "runtime.pause" => SetPaused(true),
            "runtime.resume" => SetPaused(false),
            "runtime.screenshot" => Screenshot(p),
            "input.action" => InputAction(p),

            "game.command" => ExecuteGameCommand(p),
            "game.state" => ReadGameState(p),

            "project.setting.get" => ProjectSettingGet(p),
            "project.setting.set" => ProjectSettingSet(p),

            _ => throw new InvalidOperationException($"Unknown MCP bridge method: {method}")
        };
    }

    private JsonObject Ping()
    {
        return new JsonObject
        {
            ["bridge"] = GodotMcpSettings.BridgeName,
            ["version"] = GodotMcpSettings.Version,
            ["role"] = _role,
            ["godot_version"] = Engine.GetVersionInfo().ToString(),
            ["editor_hint"] = Engine.IsEditorHint(),
            ["connected"] = _client?.IsConnected ?? false,
            ["allow_editor_writes"] = GodotMcpSettings.GetBool(GodotMcpSettings.AllowEditorWrites, false),
            ["allow_runtime_writes"] = GodotMcpSettings.GetBool(GodotMcpSettings.AllowRuntimeWrites, false),
            // Project-specific commands contributed by other addons, so an agent can
            // discover them via status.get instead of guessing names.
            ["project_commands"] = ToJsonArray(McpCommandRegistry.CommandNames()),
            ["project_states"] = ToJsonArray(McpCommandRegistry.StateNames())
        };
    }

    private static JsonArray ToJsonArray(System.Collections.Generic.List<string> values)
    {
        var array = new JsonArray();
        foreach (string v in values) array.Add(v);
        return array;
    }

    private JsonObject StatusGet() => Ping();

    private JsonNode SerializeTree(JsonObject p)
    {
        int maxDepth = p["max_depth"]?.GetValue<int>() ?? 8;
        bool currentSceneOnly = p["current_scene_only"]?.GetValue<bool>() ?? false;
        Node root = currentSceneOnly ? GetCurrentSceneRoot() : GetTree().Root;
        return McpTreeSerializer.Serialize(root, maxDepth);
    }

    private JsonNode CurrentScene(JsonObject p)
    {
        Node root = GetCurrentSceneRoot();
        int maxDepth = p["max_depth"]?.GetValue<int>() ?? 8;
        return McpTreeSerializer.Serialize(root, maxDepth);
    }

    private JsonNode EditorSelectionGet()
    {
        JsonArray nodes = new();
#if TOOLS
        EditorInterface? editor = _editorPlugin?.GetEditorInterface();
        if (editor is not null)
        {
            foreach (Node node in editor.GetSelection().GetSelectedNodes())
                nodes.Add(McpTreeSerializer.SerializeSingle(node));
        }
#endif
        return new JsonObject { ["nodes"] = nodes };
    }

    private JsonNode EditorSelectionSet(JsonObject p)
    {
        RequireWrites();
        Node node = RequireNode(p);
#if TOOLS
        EditorInterface? editor = _editorPlugin?.GetEditorInterface();
        if (editor is not null)
        {
            EditorSelection selection = editor.GetSelection();
            selection.Clear();
            selection.AddNode(node);
        }
#endif
        return new JsonObject { ["selected"] = node.GetPath().ToString() };
    }

    private JsonNode GetNodeInfo(JsonObject p)
    {
        Node node = RequireNode(p);
        bool includeProperties = p["include_properties"]?.GetValue<bool>() ?? false;
        JsonObject output = McpTreeSerializer.SerializeSingle(node);
        if (includeProperties)
            output["properties"] = SerializeProperties(node, p["max_properties"]?.GetValue<int>() ?? 64);
        return output;
    }

    private JsonNode ListNodeProperties(JsonObject p)
    {
        Node node = RequireNode(p);
        int max = p["max_properties"]?.GetValue<int>() ?? 128;
        return new JsonObject { ["path"] = node.GetPath().ToString(), ["properties"] = SerializeProperties(node, max) };
    }

    private JsonNode SetNodeProperty(JsonObject p)
    {
        RequireWrites();
        Node node = RequireNode(p);
        string property = RequiredString(p, "property");
        JsonNode? value = p["value"];
        node.Set(property, McpJson.ToVariant(value));
        return new JsonObject
        {
            ["path"] = node.GetPath().ToString(),
            ["property"] = property,
            ["value"] = value?.DeepClone(),
            ["updated"] = true
        };
    }

    private JsonNode CallNodeMethod(JsonObject p)
    {
        RequireWrites();
        if (!GodotMcpSettings.GetBool(GodotMcpSettings.AllowNodeMethodCalls, false))
            throw new InvalidOperationException("Node method calls are disabled. Enable godot_mcp/security/allow_node_method_calls.");

        Node node = RequireNode(p);
        string method = RequiredString(p, "method_name");
        JsonArray args = p["args"] as JsonArray ?? new JsonArray();
        Variant[] variants = new Variant[args.Count];
        for (int i = 0; i < args.Count; i++)
            variants[i] = McpJson.ToVariant(args[i]);

        Variant result = node.Call(method, variants);
        return new JsonObject
        {
            ["path"] = node.GetPath().ToString(),
            ["method_name"] = method,
            ["result"] = McpJson.FromVariant(result)
        };
    }

    private JsonNode CreateNode(JsonObject p)
    {
        RequireWrites();
        string type = RequiredString(p, "type");
        string parentPath = p["parent_path"]?.GetValue<string>() ?? p["parent"]?.GetValue<string>() ?? GetCurrentSceneRoot().GetPath().ToString();
        string name = p["name"]?.GetValue<string>() ?? type;

        Node parent = ResolveNode(parentPath) ?? throw new InvalidOperationException($"Parent node not found: {parentPath}");
        GodotObject? obj = (GodotObject)ClassDB.Instantiate(new StringName(type));
        if (obj is not Node node)
            throw new InvalidOperationException($"Class is not a Node or cannot be instantiated: {type}");

        node.Name = name;
        parent.AddChild(node);
        Node owner = GetCurrentSceneRoot();
        if (owner is not null && owner != node)
            node.Owner = owner;

        return new JsonObject
        {
            ["created"] = true,
            ["path"] = node.GetPath().ToString(),
            ["type"] = type
        };
    }

    private JsonNode DeleteNode(JsonObject p)
    {
        RequireWrites();
        Node node = RequireNode(p);
        string path = node.GetPath().ToString();
        node.QueueFree();
        return new JsonObject { ["deleted"] = true, ["path"] = path };
    }

    private JsonNode ReparentNode(JsonObject p)
    {
        RequireWrites();
        Node node = RequireNode(p);
        string newParentPath = RequiredString(p, "new_parent_path");
        Node newParent = ResolveNode(newParentPath) ?? throw new InvalidOperationException($"New parent node not found: {newParentPath}");
        node.Reparent(newParent);
        return new JsonObject { ["path"] = node.GetPath().ToString(), ["new_parent"] = newParent.GetPath().ToString() };
    }

    private JsonNode AttachCanvasItemShader(JsonObject p)
    {
        RequireWrites();
        Node node = RequireNode(p);
        if (node is not CanvasItem canvasItem)
            throw new InvalidOperationException("shader.attach_canvas_item requires a CanvasItem node.");

        string shaderCode = RequiredString(p, "shader_code");
        Shader shader = new() { Code = shaderCode };
        ShaderMaterial material = new() { Shader = shader };
        canvasItem.Material = material;
        return new JsonObject { ["path"] = node.GetPath().ToString(), ["shader_attached"] = true };
    }

    private JsonNode SetShaderUniform(JsonObject p)
    {
        RequireWrites();
        Node node = RequireNode(p);
        if (node is not CanvasItem canvasItem || canvasItem.Material is not ShaderMaterial material)
            throw new InvalidOperationException("Node must be a CanvasItem with a ShaderMaterial.");

        string uniform = RequiredString(p, "uniform");
        material.SetShaderParameter(uniform, McpJson.ToVariant(p["value"]));
        return new JsonObject { ["path"] = node.GetPath().ToString(), ["uniform"] = uniform, ["updated"] = true };
    }

    private JsonNode TweenProperty(JsonObject p)
    {
        RequireWrites();
        Node node = RequireNode(p);
        string property = RequiredString(p, "property");
        double duration = p["duration"]?.GetValue<double>() ?? 0.5;
        Tween tween = node.CreateTween();
        tween.TweenProperty(node, property, McpJson.ToVariant(p["to"]), duration);
        return new JsonObject { ["path"] = node.GetPath().ToString(), ["property"] = property, ["duration"] = duration, ["started"] = true };
    }

    private JsonNode CreateParticles2D(JsonObject p)
    {
        RequireWrites();
        string parentPath = p["parent_path"]?.GetValue<string>() ?? GetCurrentSceneRoot().GetPath().ToString();
        Node parent = ResolveNode(parentPath) ?? throw new InvalidOperationException($"Parent node not found: {parentPath}");
        GpuParticles2D particles = new()
        {
            Name = p["name"]?.GetValue<string>() ?? "McpParticles2D",
            Amount = p["amount"]?.GetValue<int>() ?? 32,
            Emitting = p["emitting"]?.GetValue<bool>() ?? true,
            OneShot = p["one_shot"]?.GetValue<bool>() ?? false,
            Lifetime = p["lifetime"]?.GetValue<float>() ?? 1.0f
        };
        parent.AddChild(particles);
        particles.Owner = GetCurrentSceneRoot();
        return new JsonObject { ["path"] = particles.GetPath().ToString(), ["created"] = true };
    }

    private JsonNode SampleProjectileArc2D(JsonObject p)
    {
        Vector2 start = McpJson.Vector2FromJson(p["start"] as JsonObject, Vector2.Zero);
        Vector2 velocity = McpJson.Vector2FromJson(p["velocity"] as JsonObject, new Vector2(200, -300));
        Vector2 gravity = McpJson.Vector2FromJson(p["gravity"] as JsonObject, new Vector2(0, 980));
        float step = p["step"]?.GetValue<float>() ?? 0.1f;
        int count = p["count"]?.GetValue<int>() ?? 16;
        JsonArray points = new();
        for (int i = 0; i < count; i++)
        {
            float t = i * step;
            Vector2 point = start + velocity * t + 0.5f * gravity * t * t;
            points.Add(McpJson.Vector2ToJson(point));
        }
        return new JsonObject { ["points"] = points };
    }

    private JsonNode SpriteMoveTo(JsonObject p)
    {
        RequireWrites();
        Node node = RequireNode(p);
        if (node is not Node2D node2D)
            throw new InvalidOperationException("sprite.move_to requires a Node2D.");
        Vector2 to = McpJson.Vector2FromJson(p["to"] as JsonObject, node2D.Position);
        double duration = p["duration"]?.GetValue<double>() ?? 0.25;
        Tween tween = node2D.CreateTween();
        tween.TweenProperty(node2D, "position", to, duration);
        return new JsonObject { ["path"] = node2D.GetPath().ToString(), ["to"] = McpJson.Vector2ToJson(to), ["duration"] = duration };
    }

    private JsonNode SetPaused(bool paused)
    {
        RequireRuntimeWritesOrRuntimeRole();
        GetTree().Paused = paused;
        return new JsonObject { ["paused"] = paused };
    }

    private JsonNode Screenshot(JsonObject p)
    {
        string dir = p["directory"]?.GetValue<string>() ?? GodotMcpSettings.GetString(GodotMcpSettings.ScreenshotDirectory, GodotMcpSettings.DefaultScreenshotDirectory);
        string filename = p["filename"]?.GetValue<string>() ?? $"mcp_screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png";
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(dir));
        string path = dir.TrimEnd('/') + "/" + filename;
        Image image = GetViewport().GetTexture().GetImage();
        Error error = image.SavePng(path);
        if (error != Error.Ok)
            throw new InvalidOperationException($"Failed to save screenshot: {error}");
        return new JsonObject { ["path"] = path, ["absolute_path"] = ProjectSettings.GlobalizePath(path) };
    }

    private JsonNode InputAction(JsonObject p)
    {
        RequireRuntimeWritesOrRuntimeRole();
        string action = RequiredString(p, "action");
        bool pressed = p["pressed"]?.GetValue<bool>() ?? true;
        float strength = p["strength"]?.GetValue<float>() ?? 1.0f;
        if (pressed)
            Input.ActionPress(action, strength);
        else
            Input.ActionRelease(action);
        return new JsonObject { ["action"] = action, ["pressed"] = pressed, ["strength"] = strength };
    }

    private JsonNode? ExecuteGameCommand(JsonObject p)
    {
        RequireRuntimeWritesOrRuntimeRole();
        string command = RequiredString(p, "command");
        JsonObject args = p["args"] as JsonObject ?? new JsonObject();

        // Static registry first: it's the only option in the editor, where the
        // McpGameAdapter autoload doesn't exist. Falls back to the adapter so
        // existing adapter-registered commands keep working.
        if (McpCommandRegistry.TryExecute(command, args, out JsonNode? result))
            return result;

        return RequireGameAdapter().ExecuteCommand(command, args);
    }

    private JsonNode? ReadGameState(JsonObject p)
    {
        string name = p["name"]?.GetValue<string>() ?? "game";

        if (McpCommandRegistry.TryReadState(name, out JsonNode? result))
            return result;

        return RequireGameAdapter().ReadState(name);
    }

    private JsonNode ProjectSettingGet(JsonObject p)
    {
        string key = RequiredString(p, "key");
        Variant value = ProjectSettings.GetSetting(key);
        return new JsonObject { ["key"] = key, ["value"] = McpJson.FromVariant(value) };
    }

    private JsonNode ProjectSettingSet(JsonObject p)
    {
        RequireWrites();
        string key = RequiredString(p, "key");
        ProjectSettings.SetSetting(key, McpJson.ToVariant(p["value"]));
        ProjectSettings.Save();
        return new JsonObject { ["key"] = key, ["updated"] = true };
    }

    private JsonArray SerializeProperties(Node node, int max)
    {
        JsonArray result = new();
        int count = 0;
        foreach (Godot.Collections.Dictionary entry in node.GetPropertyList())
        {
            if (count++ >= max)
                break;
            string name = entry.ContainsKey("name") ? entry["name"].AsString() : string.Empty;
            if (string.IsNullOrWhiteSpace(name))
                continue;
            Variant value = node.Get(name);
            result.Add(new JsonObject
            {
                ["name"] = name,
                ["type"] = entry.ContainsKey("type") ? (int)entry["type"].AsInt64() : 0,
                ["usage"] = entry.ContainsKey("usage") ? (int)entry["usage"].AsInt64() : 0,
                ["value"] = McpJson.FromVariant(value)
            });
        }
        return result;
    }

    private Node RequireNode(JsonObject p)
    {
        string path = RequiredString(p, "path");
        return ResolveNode(path) ?? throw new InvalidOperationException($"Node not found: {path}");
    }

    private Node? ResolveNode(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;
        NodePath nodePath = new(path);
        Node? node = GetTree().Root.GetNodeOrNull<Node>(nodePath);
        if (node is not null)
            return node;
        node = GetNodeOrNull<Node>(nodePath);
        if (node is not null)
            return node;
        Node current = GetCurrentSceneRootOrNull() ?? GetTree().Root;
        return current.GetNodeOrNull<Node>(nodePath);
    }

    private Node GetCurrentSceneRoot()
    {
        Node? root = GetCurrentSceneRootOrNull();
        if (root is not null)
            return root;
        return GetTree().CurrentScene ?? GetTree().Root;
    }

    private Node? GetCurrentSceneRootOrNull()
    {
#if TOOLS
        EditorInterface? editor = _editorPlugin?.GetEditorInterface();
        Node? edited = editor?.GetEditedSceneRoot();
        if (edited is not null)
            return edited;
#endif
        return GetTree().CurrentScene;
    }

    private McpGameAdapter RequireGameAdapter()
    {
        McpGameAdapter? adapter = GetTree().Root.GetNodeOrNull<McpGameAdapter>("/root/McpGameAdapter");
        if (adapter is null)
            throw new InvalidOperationException("McpGameAdapter autoload is not registered at /root/McpGameAdapter.");
        return adapter;
    }

    private static string RequiredString(JsonObject p, string name)
    {
        string? value = p[name]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"Missing required parameter: {name}");
        return value;
    }

    private void RequireWrites()
    {
        bool allowed = _role == "editor"
            ? GodotMcpSettings.GetBool(GodotMcpSettings.AllowEditorWrites, false)
            : GodotMcpSettings.GetBool(GodotMcpSettings.AllowRuntimeWrites, false);
        if (!allowed)
            throw new InvalidOperationException($"Writes are disabled for {_role}. Enable the matching godot_mcp/security setting in Project Settings or the MCP dock.");
    }

    private void RequireRuntimeWritesOrRuntimeRole()
    {
        if (_role == "runtime")
        {
            if (!GodotMcpSettings.GetBool(GodotMcpSettings.AllowRuntimeWrites, false))
                throw new InvalidOperationException("Runtime writes are disabled. Enable godot_mcp/security/allow_runtime_writes.");
            return;
        }
        RequireWrites();
    }
}
