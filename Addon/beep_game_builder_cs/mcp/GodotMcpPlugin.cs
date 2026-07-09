#if TOOLS
using Godot;

namespace GodotMcp;

/// <summary>
/// Editor-side plugin entry point. It owns the editor bridge connection and
/// registers the runtime autoload used when the game is running.
/// </summary>
[Tool]
public partial class GodotMcpPlugin : EditorPlugin
{
    private const string RuntimeAutoloadName = "GodotMcpRuntime";
    private const string RuntimeScriptPath = "res://addons/beep_game_builder_cs/mcp/GodotMcpRuntime.cs";

    private GodotMcpBridgeController? _editorBridge;
    private GodotMcpDock? _dock;

    public override void _EnterTree()
    {
        GodotMcpSettings.EnsureProjectSettings();
        ProjectSettings.Save();

        EnsureRuntimeAutoload();
        CreateDock();
        StartEditorBridgeIfConfigured();

        GD.Print("Godot MCP Bridge plugin enabled.");
    }

    public override void _ExitTree()
    {
        if (_dock is not null)
        {
            RemoveControlFromDocks(_dock);
            _dock.QueueFree();
            _dock = null;
        }

        if (_editorBridge is not null)
        {
            _editorBridge.StatusChanged -= OnBridgeStatusChanged;
            _editorBridge.QueueFree();
            _editorBridge = null;
        }

        // Keep the runtime autoload registered so the game bridge still works
        // when users run the game. Disable the plugin if you want to remove it.
        GD.Print("Godot MCP Bridge plugin disabled.");
    }

    private void EnsureRuntimeAutoload()
    {
        string settingPath = $"autoload/{RuntimeAutoloadName}";
        if (!ProjectSettings.HasSetting(settingPath))
        {
            AddAutoloadSingleton(RuntimeAutoloadName, RuntimeScriptPath);
            ProjectSettings.Save();
            GD.Print("Godot MCP Bridge: runtime autoload registered.");
        }
    }

    private void CreateDock()
    {
        _dock = new GodotMcpDock();
        _dock.ConnectPressed += ConnectEditorBridge;
        _dock.DisconnectPressed += DisconnectEditorBridge;
        _dock.SaveSettingsPressed += SaveDockSettings;
        _dock.RefreshFromSettings();
        AddControlToDock(DockSlot.RightBl, _dock);
    }

    private void StartEditorBridgeIfConfigured()
    {
        if (GodotMcpSettings.GetBool(GodotMcpSettings.AutoConnectEditor, true))
            ConnectEditorBridge();
    }

    private void ConnectEditorBridge()
    {
        if (_editorBridge is null)
        {
            _editorBridge = new GodotMcpBridgeController();
            _editorBridge.Name = "GodotMcpEditorBridge";
            _editorBridge.StatusChanged += OnBridgeStatusChanged;
            AddChild(_editorBridge);
        }

        _editorBridge.ConfigureEditor(this);
        _editorBridge.Configure(
            GodotMcpSettings.GetUrl(),
            GodotMcpSettings.GetToken(),
            role: "editor",
            GodotMcpSettings.GetBool(GodotMcpSettings.VerboseLogging, true),
            GodotMcpSettings.GetFloat(GodotMcpSettings.ReconnectSeconds, 2.0f)
        );
        _editorBridge.ConnectBridge();
        _dock?.SetStatus("Connecting", GodotMcpSettings.GetUrl());
    }

    private void DisconnectEditorBridge()
    {
        _editorBridge?.DisconnectBridge();
        _dock?.SetStatus("Disconnected", GodotMcpSettings.GetUrl());
    }

    private void SaveDockSettings()
    {
        if (_dock is null)
            return;

        GodotMcpSettings.SetString(GodotMcpSettings.Url, _dock.BridgeUrl);
        GodotMcpSettings.SetString(GodotMcpSettings.Token, _dock.BridgeToken);
        GodotMcpSettings.SetBool(GodotMcpSettings.AutoConnectEditor, _dock.AutoConnectEditor);
        GodotMcpSettings.SetBool(GodotMcpSettings.AutoConnectRuntime, _dock.AutoConnectRuntime);
        GodotMcpSettings.SetBool(GodotMcpSettings.AllowEditorWrites, _dock.AllowEditorWrites);
        GodotMcpSettings.SetBool(GodotMcpSettings.AllowRuntimeWrites, _dock.AllowRuntimeWrites);

        _dock.SetStatus("Settings saved", GodotMcpSettings.GetUrl());
    }

    private void OnBridgeStatusChanged(string status)
    {
        _dock?.SetStatus(status, GodotMcpSettings.GetUrl());
    }
}
#endif
