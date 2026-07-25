# beep-mcp-server

The half of the Beep.Godot MCP bridge that did not exist.

`addons/godot_mcp/` is a WebSocket **client** — `McpWebSocketClient` dials **out** to
`ws://127.0.0.1:8789/{role}?token=…` and retries forever. Nothing listened, so every
bridge method and all ~40 `beep.*` commands were unreachable from Claude. This is the
listener, and it is also an MCP server, so one process connects the two halves:

```
Claude Code  ──MCP over stdio──►  beep-mcp-server  ◄──WebSocket :8789──  Godot editor
                                                   ◄──WebSocket :8789──  Godot runtime
```

## Setup

```bash
cd tools/beep-mcp-server
npm install
npm run build

# from the repo root, using an absolute path
claude mcp add beep-godot -- node "$(pwd)/tools/beep-mcp-server/dist/index.js"
```

Then open the project in Godot. The `godot_mcp` plugin auto-connects on load; check
Godot's **Output** panel for the bridge line. Confirm
`godot_mcp/bridge/url` is `ws://127.0.0.1:8789` in Project Settings — note that
`GodotMcpSettings.Initialize` **force-writes** that default on load, so a manual edit
there is overwritten.

Verify without Godot at all:

```bash
npm run smoke      # drives the real server over stdio and impersonates Godot
```

## Tools

| Tool | Needs | Notes |
|---|---|---|
| `godot_status` | — | **Start here.** Always answers, even with Godot closed. |
| `godot_ping` | any | round-trip check |
| `beep_list_commands` | any | live `beep.*` list from `status.get` — discover, don't guess |
| `beep_command` | auto | run any `beep.*`; the role is picked from the command name |
| `beep_state` | runtime | live game state |
| `godot_scene_tree`, `godot_current_scene` | editor | read the open scene |
| `godot_node_get`, `godot_node_properties` | editor | inspect a node |
| `godot_node_set_property`, `_create`, `_delete`, `_reparent` | editor | **write** |
| `godot_project_setting_get` / `_set` | editor | includes the security flags |
| `godot_runtime_pause`, `godot_input_action` | runtime | drive the running game |

Editor commands go to the editor socket and runtime commands to the running game
(`protocol.ts`), so asking for `beep.add_score` while only the editor is open reports
*"press Play"* rather than *"no scene is open"*.

## The three things that go wrong

**1. Godot is not connected.** The usual case — the editor is closed most of the time.
Tools report `[NOT_CONNECTED]` and name the fix (open the project, or press Play for
runtime tools). The server stays up; it never exits because Godot went away.

**2. Writes are refused.** Godot enforces `godot_mcp/security/allow_editor_writes` and
`allow_runtime_writes`; both default to **off**. A refused write surfaces as a tool error
carrying Godot's own message, never as a silent success. Enable them in Project Settings
→ `godot_mcp/security`.

**3. Port 8789 is already in use.** Usually a second copy of this server. The process
exits with `[PORT_IN_USE]` naming the port. Set `BEEP_MCP_PORT` **and** the matching
`godot_mcp/bridge/url` if you need a different one.

## Environment

| Var | Default | Purpose |
|---|---|---|
| `BEEP_MCP_PORT` | `8789` | must match `godot_mcp/bridge/url` |
| `BEEP_MCP_HOST` | `127.0.0.1` | loopback; do not expose without a token |
| `BEEP_MCP_TOKEN` | *(unset)* | when set, must match `godot_mcp/bridge/token` |
| `BEEP_MCP_TIMEOUT_MS` | `15000` | per request; bakes and generation get 180s |
| `BEEP_MCP_QUIET` | *(unset)* | `1` silences stderr logging |

Logging goes to **stderr only** — stdout is the MCP transport and a stray write there
corrupts the protocol.

## Scope

This is Phase 0 of [`docs/mcp/MCP_ROADMAP.md`](../../docs/mcp/MCP_ROADMAP.md): plumbing
only, no new Godot-side capability. Undo/batching/dry-run is Phase 1; resource, theme and
animation authoring is Phase 2. If you are about to add cleverness here, check the roadmap
first — it probably belongs behind a bridge method instead.
