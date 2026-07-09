using System;
using System.Text;
using System.Text.Json.Nodes;
using Godot;

namespace GodotMcp;

/// <summary>
/// Reconnecting local WebSocket client. This is a bridge client, not an MCP client.
/// </summary>
[Tool]
public partial class McpWebSocketClient : Node
{
    public event Action<JsonObject>? MessageReceived;
    public event Action<string>? StatusChanged;

    private readonly WebSocketPeer _socket = new();
    private string _url = GodotMcpSettings.DefaultUrl;
    private string _token = string.Empty;
    private string _role = "runtime";
    private bool _verbose = true;
    private double _reconnectDelaySeconds = 2.0;
    private double _reconnectSeconds;
    private bool _shouldConnect;
    private bool _helloSent;
    private WebSocketPeer.State _lastState = WebSocketPeer.State.Closed;

    public bool IsConnected => _socket.GetReadyState() == WebSocketPeer.State.Open;
    public string Url => _url;
    public string Role => _role;

    public void Configure(string url, string token, string role, bool verbose, double reconnectDelaySeconds)
    {
        _url = string.IsNullOrWhiteSpace(url) ? GodotMcpSettings.DefaultUrl : url.Trim();
        _token = token ?? string.Empty;
        _role = string.IsNullOrWhiteSpace(role) ? "runtime" : role.Trim();
        _verbose = verbose;
        _reconnectDelaySeconds = Math.Max(0.25, reconnectDelaySeconds);
    }

    public void ConnectBridge()
    {
        _shouldConnect = true;
        _helloSent = false;
        _reconnectSeconds = 0;
        ConnectNow();
    }

    public void DisconnectBridge()
    {
        _shouldConnect = false;
        _helloSent = false;
        _socket.Close();
        EmitStatus("Closed");
    }

    public override void _Process(double delta)
    {
        _socket.Poll();
        WebSocketPeer.State state = _socket.GetReadyState();

        if (state != _lastState)
        {
            _lastState = state;
            EmitStatus(state.ToString());
            if (state != WebSocketPeer.State.Open)
                _helloSent = false;
        }

        if (state == WebSocketPeer.State.Open)
        {
            SendHelloOnce();
            ReadPackets();
            return;
        }

        if (!_shouldConnect)
            return;

        if (state == WebSocketPeer.State.Closed)
        {
            _reconnectSeconds -= delta;
            if (_reconnectSeconds <= 0)
            {
                _reconnectSeconds = _reconnectDelaySeconds;
                ConnectNow();
            }
        }
    }

    public bool Send(JsonObject message)
    {
        if (_socket.GetReadyState() != WebSocketPeer.State.Open)
        {
            if (_verbose)
                GD.PushWarning("Godot MCP Bridge: cannot send, socket is not open.");
            return false;
        }

        _socket.SendText(message.ToJsonString());
        return true;
    }

    private void ConnectNow()
    {
        string connectUrl = BuildUrlWithToken(_url, _token, _role);
        if (_verbose)
            GD.Print($"Godot MCP Bridge: connecting to {connectUrl}");

        Error error = _socket.ConnectToUrl(connectUrl);
        if (error != Error.Ok)
        {
            EmitStatus($"Connect error: {error}");
            GD.PushWarning($"Godot MCP Bridge failed to connect to {connectUrl}: {error}");
        }
    }

    private static string BuildUrlWithToken(string url, string token, string role)
    {
        // Godot 4.x WebSocketPeer rejects query params (?key=val) on connect.
        // Use path-based format: ws://host:port/{role}?token=X
        string output = url.TrimEnd('/') + "/" + Uri.EscapeDataString(role);
        if (!string.IsNullOrWhiteSpace(token))
            output += "?token=" + Uri.EscapeDataString(token);
        return output;
    }

    private void SendHelloOnce()
    {
        if (_helloSent)
            return;

        _helloSent = true;
        EmitStatus("Open - hello sent");

        Send(new JsonObject
        {
            ["method"] = "hello",
            ["params"] = new JsonObject
            {
                ["token"] = _token,
                ["bridge"] = GodotMcpSettings.BridgeName,
                ["version"] = GodotMcpSettings.Version,
                ["role"] = _role,
                ["editor_hint"] = Engine.IsEditorHint(),
                ["godot_version"] = Engine.GetVersionInfo().ToString()
            }
        });
    }

    private void ReadPackets()
    {
        while (_socket.GetAvailablePacketCount() > 0)
        {
            byte[] packet = _socket.GetPacket();
            string text = Encoding.UTF8.GetString(packet);
            try
            {
                JsonNode? node = JsonNode.Parse(text);
                if (node is JsonObject obj)
                    MessageReceived?.Invoke(obj);
                else if (_verbose)
                    GD.PushWarning($"Godot MCP Bridge ignored non-object JSON: {text}");
            }
            catch (Exception ex)
            {
                GD.PushWarning($"Godot MCP Bridge failed to parse packet: {ex.Message}");
            }
        }
    }

    private void EmitStatus(string status)
    {
        if (_verbose)
            GD.Print($"Godot MCP Bridge [{_role}] status: {status}");
        StatusChanged?.Invoke(status);
    }
}
