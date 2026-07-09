#if TOOLS
using System;
using Godot;

namespace GodotMcp;

/// <summary>
/// Small editor dock for human-visible connection settings and status.
/// </summary>
[Tool]
public partial class GodotMcpDock : VBoxContainer
{
    public event Action? ConnectPressed;
    public event Action? DisconnectPressed;
    public event Action? SaveSettingsPressed;

    private Label _status = null!;
    private LineEdit _url = null!;
    private LineEdit _token = null!;
    private CheckBox _autoEditor = null!;
    private CheckBox _autoRuntime = null!;
    private CheckBox _allowEditorWrites = null!;
    private CheckBox _allowRuntimeWrites = null!;

    public string BridgeUrl => _url.Text;
    public string BridgeToken => _token.Text;
    public bool AutoConnectEditor => _autoEditor.ButtonPressed;
    public bool AutoConnectRuntime => _autoRuntime.ButtonPressed;
    public bool AllowEditorWrites => _allowEditorWrites.ButtonPressed;
    public bool AllowRuntimeWrites => _allowRuntimeWrites.ButtonPressed;

    public override void _Ready()
    {
        Name = "Godot MCP";
        SizeFlagsVertical = SizeFlags.ExpandFill;
        BuildUi();
        RefreshFromSettings();
    }

    public void RefreshFromSettings()
    {
        if (_url is null)
            return;

        _url.Text = GodotMcpSettings.GetUrl();
        _token.Text = GodotMcpSettings.GetToken();
        _autoEditor.ButtonPressed = GodotMcpSettings.GetBool(GodotMcpSettings.AutoConnectEditor, true);
        _autoRuntime.ButtonPressed = GodotMcpSettings.GetBool(GodotMcpSettings.AutoConnectRuntime, true);
        _allowEditorWrites.ButtonPressed = GodotMcpSettings.GetBool(GodotMcpSettings.AllowEditorWrites, false);
        _allowRuntimeWrites.ButtonPressed = GodotMcpSettings.GetBool(GodotMcpSettings.AllowRuntimeWrites, false);
    }

    public void SetStatus(string status, string url)
    {
        if (_status is not null)
            _status.Text = $"Status: {status}\nURL: {url}";
    }

    private void BuildUi()
    {
        AddChild(new Label
        {
            Text = "Godot MCP Bridge",
            ThemeTypeVariation = "HeaderSmall"
        });

        _status = new Label
        {
            Text = "Status: Not connected",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        AddChild(_status);

        AddChild(new HSeparator());
        AddChild(new Label { Text = "Bridge URL" });
        _url = new LineEdit { PlaceholderText = GodotMcpSettings.DefaultUrl };
        AddChild(_url);

        AddChild(new Label { Text = "Bridge Token" });
        _token = new LineEdit
        {
            Secret = false,
            PlaceholderText = "shared local token"
        };
        AddChild(_token);

        _autoEditor = new CheckBox { Text = "Auto connect editor bridge" };
        AddChild(_autoEditor);

        _autoRuntime = new CheckBox { Text = "Auto connect runtime bridge" };
        AddChild(_autoRuntime);

        AddChild(new HSeparator());
        _allowEditorWrites = new CheckBox { Text = "Allow editor writes" };
        AddChild(_allowEditorWrites);

        _allowRuntimeWrites = new CheckBox { Text = "Allow runtime writes" };
        AddChild(_allowRuntimeWrites);

        Label warning = new()
        {
            Text = "Writes let the MCP server create/delete/change nodes and resources. Keep off unless you trust the local server.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        AddChild(warning);

        AddChild(new HSeparator());
        Button save = new() { Text = "Save Settings" };
        save.Pressed += () => SaveSettingsPressed?.Invoke();
        AddChild(save);

        HBoxContainer buttons = new();
        Button connect = new() { Text = "Connect" };
        Button disconnect = new() { Text = "Disconnect" };
        connect.Pressed += () => ConnectPressed?.Invoke();
        disconnect.Pressed += () => DisconnectPressed?.Invoke();
        buttons.AddChild(connect);
        buttons.AddChild(disconnect);
        AddChild(buttons);
    }
}
#endif
