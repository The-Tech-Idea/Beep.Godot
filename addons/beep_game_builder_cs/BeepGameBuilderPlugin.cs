using Godot;
using GodotMcp;

namespace Beep.GameBuilder;

[Tool]
public partial class BeepGameBuilderPlugin : EditorPlugin
{
    private Godot.Control? _dock;
    private GodotMcpBridgeController? _bridge;

    public override void _EnterTree()
    {
        _dock = new BeepGameBuilderDock { EditorPlugin = this };
        AddControlToDock(DockSlot.RightUl, _dock);

        // Defer MCP bridge setup — adding children during _EnterTree can fail
        // with "Parent node is busy setting up children" in some editor states.
        CallDeferred(nameof(TryEnableMcpBridge));

        GD.Print("[Beep Game Builder] Plugin enabled.");
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

        if (_dock is not null)
        {
            RemoveControlFromDocks(_dock);
            _dock.QueueFree();
            _dock = null;
        }

        // Symmetric with EnsureAutoload in _EnterTree: remove the autoloads we
        // added so disabling the plugin doesn't leave stale entries in
        // project.godot pointing at scripts that may no longer be present.
        RemoveAutoload("McpGameAdapter");
        RemoveAutoload("GodotMcpRuntime");

        GD.Print("[Beep Game Builder] Plugin disabled.");
    }

    private void TryEnableMcpBridge()
    {
        try
        {
            GodotMcpSettings.EnsureProjectSettings();

            EnsureAutoload("McpGameAdapter", "res://addons/beep_game_builder_cs/mcp/McpGameAdapter.cs");
            EnsureAutoload("GodotMcpRuntime", "res://addons/beep_game_builder_cs/mcp/GodotMcpRuntime.cs");

            // Direct typed construction instead of reflection — the bridge
            // controller and settings live in the same assembly (GodotMcp).
            _bridge = new GodotMcpBridgeController
            {
                Name = "BeepMcpBridge"
            };
            AddChild(_bridge);

            _bridge.ConfigureEditor(this);

            string url = GodotMcpSettings.GetUrl();
            string token = GodotMcpSettings.GetToken();
            _bridge.Configure(
                url,
                token,
                role: "editor",
                verbose: true,
                reconnectDelaySeconds: 2.0
            );
            _bridge.ConnectBridge();

            GD.Print($"[Beep Game Builder] MCP bridge connected to {url}");
        }
        catch (System.Exception ex)
        {
            GD.PushWarning($"[Beep Game Builder] MCP bridge not available: {ex.Message}");
        }
    }

    private void EnsureAutoload(string name, string path)
    {
        if (!ProjectSettings.HasSetting($"autoload/{name}"))
            AddAutoloadSingleton(name, path);
    }

    private void RemoveAutoload(string name)
    {
        string settingPath = $"autoload/{name}";
        if (ProjectSettings.HasSetting(settingPath))
        {
            RemoveAutoloadSingleton(name);
            ProjectSettings.Save();
        }
    }
}
