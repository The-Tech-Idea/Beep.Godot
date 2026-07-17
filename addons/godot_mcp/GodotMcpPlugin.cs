using Godot;

namespace GodotMcp;

/// <summary>
/// Editor plugin for the MCP bridge. Registers the runtime autoloads and opens the
/// editor-side bridge connection.
///
/// This addon is deliberately project-agnostic — it has no dependency on any other
/// addon. Projects that want their own MCP commands register them with
/// <see cref="McpCommandRegistry"/> rather than being referenced from here.
/// </summary>
[Tool]
public partial class GodotMcpPlugin : EditorPlugin
{
    private const string AdapterAutoload = "McpGameAdapter";
    private const string RuntimeAutoload = "GodotMcpRuntime";
    private const string AdapterPath = "res://addons/godot_mcp/McpGameAdapter.cs";
    private const string RuntimePath = "res://addons/godot_mcp/GodotMcpRuntime.cs";

    private GodotMcpBridgeController? _bridge;

    public override void _EnterTree()
    {
        // Defer setup — adding children during _EnterTree can fail with
        // "Parent node is busy setting up children" in some editor states.
        CallDeferred(nameof(TryEnableBridge));
        GD.Print("[Godot MCP] Plugin enabled.");
    }

    public override void _ExitTree()
    {
        // Tear down the bridge first so the WebSocketPeer is closed cleanly.
        if (_bridge is not null)
        {
            _bridge.DisconnectBridge();
            _bridge.QueueFree();
            _bridge = null;
        }

        // Symmetric with EnsureAutoload: don't leave entries in project.godot
        // pointing at scripts that may no longer be present.
        RemoveAutoload(AdapterAutoload);
        RemoveAutoload(RuntimeAutoload);

        GD.Print("[Godot MCP] Plugin disabled.");
    }

    private void TryEnableBridge()
    {
        try
        {
            GodotMcpSettings.EnsureProjectSettings();

            EnsureAutoload(AdapterAutoload, AdapterPath);
            EnsureAutoload(RuntimeAutoload, RuntimePath);

            _bridge = new GodotMcpBridgeController { Name = "GodotMcpEditorBridge" };
            AddChild(_bridge);
            _bridge.ConfigureEditor(this);

            string url = GodotMcpSettings.GetUrl();
            _bridge.Configure(
                url,
                GodotMcpSettings.GetToken(),
                role: "editor",
                verbose: true,
                reconnectDelaySeconds: 2.0);
            _bridge.ConnectBridge();

            GD.Print($"[Godot MCP] Editor bridge connected to {url}");
        }
        catch (System.Exception ex)
        {
            GD.PushWarning($"[Godot MCP] Bridge not available: {ex.Message}");
        }
    }

    private void EnsureAutoload(string name, string path)
    {
        if (!ProjectSettings.HasSetting($"autoload/{name}"))
            AddAutoloadSingleton(name, path);
    }

    private void RemoveAutoload(string name)
    {
        if (ProjectSettings.HasSetting($"autoload/{name}"))
        {
            RemoveAutoloadSingleton(name);
            ProjectSettings.Save();
        }
    }
}
