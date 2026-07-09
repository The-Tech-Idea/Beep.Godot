using Godot;

namespace GodotMcp;

/// <summary>
/// Runtime autoload. The editor plugin registers this automatically.
/// It connects while the game runs and exposes runtime/game commands.
/// </summary>
[Tool]
public partial class GodotMcpRuntime : Node
{
    public static GodotMcpRuntime? Instance { get; private set; }

    private GodotMcpBridgeController? _bridge;

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;

        GodotMcpSettings.EnsureProjectSettings();

        if (!GodotMcpSettings.GetBool(GodotMcpSettings.AutoConnectRuntime, true))
        {
            GD.Print("Godot MCP Runtime Bridge: auto connect is disabled.");
            return;
        }

        _bridge = new GodotMcpBridgeController
        {
            Name = "GodotMcpRuntimeBridge",
            ProcessMode = ProcessModeEnum.Always
        };
        AddChild(_bridge);
        _bridge.Configure(
            GodotMcpSettings.GetUrl(),
            GodotMcpSettings.GetToken(),
            role: "runtime",
            GodotMcpSettings.GetBool(GodotMcpSettings.VerboseLogging, true),
            GodotMcpSettings.GetFloat(GodotMcpSettings.ReconnectSeconds, 2.0f)
        );
        _bridge.ConnectBridge();
    }

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;

        _bridge?.DisconnectBridge();
    }
}
