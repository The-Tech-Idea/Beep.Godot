# Godot MCP Bridge

**Godot MCP Bridge** is a Godot 4 .NET editor/runtime plugin that connects a Godot project to a local FastMCP server.

It is the Godot-side bridge for the Godot FastMCP Server. The Python MCP server runs outside Godot. This plugin runs inside Godot and connects to the server over WebSocket.

## Features

- Editor bridge for scene inspection and scene editing.
- Runtime bridge for live game inspection and controlled runtime actions.
- Game adapter for explicit project-specific commands and state providers.
- Node tree serialization for editor and runtime scenes.
- Shader material attachment and shader uniform editing.
- Tween, particle, projectile, and sprite movement bridge methods.
- Runtime screenshot, input action, pause/resume, and node property control.
- Token-based local WebSocket connection.

## Requirements

- Godot 4 .NET edition.
- A C# project/solution in your Godot project.
- The matching Python FastMCP server running locally.

This is a C# editor plugin, so the Godot project must be built before the plugin can be enabled.

## Installation

Copy the `addons` folder from this repository into your Godot project:

```text
YourGodotProject/
  addons/
    godot_mcp/
      plugin.cfg
      GodotMcpBridgeController.cs
      GodotMcpRuntime.cs
      McpGameAdapter.cs
      McpWebSocketClient.cs
      McpTreeSerializer.cs
      McpJson.cs
```

Open the project in Godot 4 .NET, build the C# solution, then enable:

```text
Project -> Project Settings -> Plugins -> Godot MCP Bridge
```

The editor plugin automatically registers the runtime autoload:

```text
GodotMcpRuntime -> res://addons/godot_mcp/GodotMcpRuntime.cs
```

For game-specific commands, add this autoload manually:

```text
Name: McpGameAdapter
Path: res://addons/godot_mcp/McpGameAdapter.cs
```

## Bridge configuration

The plugin reads these environment variables:

```text
GODOT_MCP_BRIDGE_URL=ws://127.0.0.1:8765
GODOT_MCP_BRIDGE_TOKEN=replace-with-the-same-token-used-by-the-python-server
```

On Windows PowerShell:

```powershell
$env:GODOT_MCP_BRIDGE_URL="ws://127.0.0.1:8765"
$env:GODOT_MCP_BRIDGE_TOKEN="replace-with-the-same-token-used-by-the-python-server"
& "C:\\Tools\\Godot\\Godot_v4_mono.exe" --editor --path "C:\\Projects\\YourGame"
```

## Python MCP server

This plugin does not include the Python MCP server. Install and run the matching `godot-fastmcp-server` package separately.

Typical server command:

```bash
godot-mcp --project "C:/Projects/YourGame"
```

HTTP mode:

```bash
godot-mcp --project "C:/Projects/YourGame" --transport http --host 127.0.0.1 --port 8000 --path /mcp/
```

## Game adapter pattern

Register your game systems explicitly from your main game bootstrap code:

```csharp
McpGameAdapter adapter = GetNode<McpGameAdapter>("/root/McpGameAdapter");

adapter.RegisterCommand("advance_turn", parameters =>
{
    int count = parameters["count"]?.GetValue<int>() ?? 1;
    for (int i = 0; i < count; i++)
        GameManager.Instance.AdvanceTurn();
    return JsonValue.Create(count);
});

adapter.RegisterState("game", () => new JsonObject
{
    ["turn"] = GameManager.Instance.CurrentTurn,
    ["money"] = GameManager.Instance.Money
});
```

Do not expose arbitrary reflection. Register only the commands and state providers you want MCP clients to use.

## Security notes

- Bind the MCP bridge to localhost by default.
- Always set `GODOT_MCP_BRIDGE_TOKEN`.
- Keep destructive MCP tools disabled unless you are intentionally editing test projects.
- Review all AI-proposed project edits before enabling write mode in production projects.

## License

MIT License. See `LICENSE.md`.


## v0.6.2 Godot 4.7 C# hotfix

Fixed Godot 4.7 C# Variant/Dictionary compile errors in McpJson.cs, GodotMcpBridgeController.cs, and GodotMcpRuntime.cs.
