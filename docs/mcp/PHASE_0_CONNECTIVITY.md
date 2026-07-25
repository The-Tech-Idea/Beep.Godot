# Phase 0 — Make it connectable

**Goal:** `claude mcp add` once, and an agent can talk to Godot. Nothing in this roadmap
works until this does.

**Status:** ✅ built — `tools/beep-mcp-server/`, 17 tools, `npm run smoke` green (12 checks).
Verified against a **simulated** addon; steps 3–7 below still need a live Godot editor,
which does not exist on this machine. · [back to roadmap](MCP_ROADMAP.md)

---

## Why

`addons/godot_mcp/` is a WebSocket **client**. `McpWebSocketClient.ConnectNow()` dials out
to `ws://127.0.0.1:8789/{role}?token=…`, retries on a timer, and logs a connect error when
it fails. **Nothing listens on that port.** There is no server in this repo, none on the
machine, and no MCP manifest of any kind. The 582-line bridge controller and the ~40
`beep.*` commands have never been reachable from Claude.

Phase 0 builds the missing half. It changes **no transport code in the addon** — Godot
keeps dialling out exactly as it does now.

## Deliverable

`tools/beep-mcp-server/` — a Node + TypeScript package that is simultaneously:

1. an **MCP server over stdio** (`@modelcontextprotocol/sdk`), which is what makes
   `claude mcp add` a one-liner; and
2. a **WebSocket server on `:8789`** that Godot connects into.

```
tools/beep-mcp-server/
├── package.json          # bin: beep-mcp, type: module
├── tsconfig.json
├── README.md             # the two-command setup
└── src/
    ├── index.ts          # entry: start WS server, then MCP stdio server
    ├── bridge.ts         # WS server, role registry, request/response correlation
    ├── protocol.ts       # envelope types + the method list, mirrored from the addon
    ├── tools.ts          # MCP tool definitions → bridge methods
    └── discovery.ts      # status.get → dynamic beep.* tool list
```

## Design

### Role routing

Both Godot roles may be connected at once. The registry keys sockets by the `role` segment
of the connect path, confirmed against the `hello` frame:

| Target | Used by |
|---|---|
| `editor` | `beep.inspect_scene`, `beep.add_component`, `beep.bake_textures`, `node.*`, `project.setting.*` |
| `runtime` | `beep.add_score`, `beep.set_weather`, `game.state`, `input.action`, `runtime.pause` |
| `any` | `ping`, `status.get` — prefer editor, fall back to runtime |

A tool whose role is absent returns a plain, actionable error — *"Godot editor is not
connected. Open the project in Godot; the bridge auto-connects."* — never a timeout.

### Request correlation

`{id, method, params}` out, `{id, ok, result|error}` back. Keep a `Map<id, {resolve,
reject, timer}>`; default timeout 15s (baking 50 themes needs more — allow a per-tool
override). On socket close, reject every in-flight request for that role with a clear
"Godot disconnected mid-request" rather than leaving the agent hanging.

### Auth

`GodotMcpSettings.Token` is optional and empty by default. The server reads
`BEEP_MCP_TOKEN`; when set, it rejects a socket whose `?token=` or `hello.params.token`
does not match. When unset, bind **loopback only** and log that it is unauthenticated.

### Tool surface for Phase 0

Thin and honest — one MCP tool per bridge capability, plus discovery. No new Godot code.

| MCP tool | Bridge call | Role |
|---|---|---|
| `godot_status` | `status.get` + which roles are connected + the three security flags | any |
| `godot_scene_tree` | `tree.serialize` | editor |
| `godot_current_scene` | `scene.current` | editor |
| `godot_node_get` / `godot_node_properties` | `node.get`, `node.list_properties` | editor |
| `godot_node_set_property` | `node.set_property` | editor |
| `godot_node_create` / `_delete` / `_reparent` | `node.create`, `node.delete`, `node.reparent` | editor |
| `godot_project_setting_get` / `_set` | `project.setting.get|set` | editor |
| `godot_runtime_pause` / `_resume` / `_screenshot` | `runtime.*` | runtime |
| `godot_input_action` | `input.action` | runtime |
| `beep_command` | `game.command` — `{name, args}` | auto by prefix |
| `beep_state` | `game.state` | runtime |

**`beep_command` is the important one.** `status.get` returns the live command list, so the
server can advertise every `beep.*` handler without hardcoding 40 tools — and new Beep
commands appear with no server release. Expose a `beep_list_commands` tool that returns
that list with descriptions, so an agent can discover rather than guess.

### Godot offline

The MCP server starts and stays up regardless. Every tool that needs a role returns the
"not connected" error above. `godot_status` always answers. This matters because the editor
is closed most of the time and a server that exits on disconnect is unusable.

## Setup story (the point of the phase)

```bash
cd tools/beep-mcp-server
npm install && npm run build

claude mcp add beep-godot -- node "<abs>/tools/beep-mcp-server/dist/index.js"
```

Then in Godot: **Project → Project Settings → Plugins → godot_mcp** (already auto-enables),
and confirm `godot_mcp/bridge/url` is `ws://127.0.0.1:8789`. `GodotMcpSettings.Initialize`
force-writes that default on load, so a stale cached port corrects itself.

The README must state the three failure modes plainly: port already in use, Godot not
open, and writes disabled by the security flags.

## Tasks

- [x] Scaffold `tools/beep-mcp-server/` (package.json, tsconfig, MCP SDK 1.29)
- [x] `bridge.ts` — WS server, role registry, `hello` handling, id correlation, timeouts
- [x] `protocol.ts` — envelope + method types mirrored from `GodotMcpBridgeController`
- [x] `tools.ts` — the table above (17 tools)
- [x] Discovery — `status.get` → dynamic `beep.*` list via `beep_list_commands`
      (folded into `tools.ts`; a separate `discovery.ts` was not worth the indirection)
- [x] `README.md` — setup, the `claude mcp add` line, the three failure modes
- [x] `.gitignore` for `node_modules/` and `dist/`
- [x] `smoke.mjs` + `npm run smoke` — the verification below, runnable without Godot
- [x] Roadmap linked from `CLAUDE.md` (Debug MCP Bridge section)
- [ ] Link from root `README.md`

## Verification

`npm run smoke` automates 1, 2, 6 and 8 plus the connected path, by driving the real
server over stdio while impersonating the addon on the socket — the same frames
`McpWebSocketClient` sends. **12 checks, all green.** What it cannot prove is that the
real C# addon behaves as simulated; steps 3–5 and 7 need a live editor.

1. `npm run build` — clean.
2. **Server alone, Godot closed:** `godot_status` returns `{editor: false, runtime: false}`
   and every other tool returns the actionable not-connected error. The server does not exit.
3. **Editor connected:** open the project; the addon's Output line reports connected.
   `godot_status` flips to `editor: true` and reports the three security flags.
4. `godot_scene_tree` on an open `racing_main.tscn` returns the node tree.
5. `beep_list_commands` lists the `beep.*` handlers; `beep_command {name: "beep.catalog"}`
   returns the skin catalog.
6. **Write gate:** with `allow_editor_writes` **off**, `beep_command {name:
   "beep.set_node_property", …}` returns the "enable allow_editor_writes" message — not a
   silent success. Turn it on and the same call succeeds.
7. **Both roles:** run the game (F5) with the editor open; `godot_status` shows both, and a
   runtime-only tool (`beep_state`) routes to the runtime socket.
8. **Kill Godot mid-request** during a slow call and confirm the in-flight request rejects
   with the disconnect message instead of hanging to timeout.

Only tick ✅ on the roadmap when 2–8 were actually run.

## Out of scope for Phase 0

No new Godot-side methods, no undo, no batching, no resource authoring. Phase 0 is
plumbing — it must be boring and correct.
