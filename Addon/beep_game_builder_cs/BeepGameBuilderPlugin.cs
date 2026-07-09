using Godot;

[Tool]
public partial class BeepGameBuilderPlugin : EditorPlugin
{
    private Control _dock;

    public override void _EnterTree()
    {
        _dock = new BeepGameBuilderDock { EditorPlugin = this };
        AddControlToDock(DockSlot.RightUl, _dock);

        TryEnableMcpBridge();

        GD.Print("[Beep Game Builder] Plugin enabled.");
    }

    public override void _ExitTree()
    {
        if (_dock != null) { RemoveControlFromDocks(_dock); _dock.QueueFree(); }
        GD.Print("[Beep Game Builder] Plugin disabled.");
    }

    private void TryEnableMcpBridge()
    {
        try
        {
            var settingsType = System.Type.GetType("GodotMcp.GodotMcpSettings");
            var bridgeType = System.Type.GetType("GodotMcp.GodotMcpBridgeController");
            if (settingsType == null || bridgeType == null) return;

            settingsType.GetMethod("EnsureProjectSettings")?.Invoke(null, null);

            EnsureAutoload("McpGameAdapter", "res://addons/beep_game_builder_cs/mcp/McpGameAdapter.cs");
            EnsureAutoload("GodotMcpRuntime", "res://addons/beep_game_builder_cs/mcp/GodotMcpRuntime.cs");

            var bridge = System.Activator.CreateInstance(bridgeType) as Node;
            if (bridge == null) return;
            bridge.Name = "BeepMcpBridge";
            AddChild(bridge);

            bridgeType.GetMethod("ConfigureEditor")?.Invoke(bridge, new object[] { this });
            var url = (string)(settingsType.GetMethod("GetUrl")?.Invoke(null, null) ?? "ws://127.0.0.1:8789");
            var token = (string)(settingsType.GetMethod("GetToken")?.Invoke(null, null) ?? "");
            bridgeType.GetMethod("Configure")?.Invoke(bridge, new object[] { url, token, "editor", true, 2.0f });
            bridgeType.GetMethod("ConnectBridge")?.Invoke(bridge, null);

            GD.Print($"[Beep Game Builder] MCP bridge connected to {url}");
        }
        catch (System.Exception ex)
        {
            GD.Print($"[Beep Game Builder] MCP bridge not available: {ex.Message}");
        }
    }

    private void EnsureAutoload(string name, string path)
    {
        if (!ProjectSettings.HasSetting($"autoload/{name}"))
            AddAutoloadSingleton(name, path);
    }
}
