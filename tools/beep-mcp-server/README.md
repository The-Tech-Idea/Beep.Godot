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

### With Claude Code — nothing to do

The repo ships a project-scoped [`.mcp.json`](../../.mcp.json). Open the project in Claude
Code and approve the server when prompted; that is the whole setup. Dependencies install
and TypeScript compiles on first launch (**~6s cold, from no `node_modules` and no
`dist`**), then start-up is instant.

Check it took with `/mcp`, which should list **beep-godot**.

### Anywhere else

Any MCP host can launch the same entry point:

```
command: node
args:    tools/beep-mcp-server/prepare-and-start.mjs      (cwd = repo root)
```

Or register it explicitly:

```bash
claude mcp add beep-godot -- node "$(pwd)/tools/beep-mcp-server/prepare-and-start.mjs"
```

> **Why `prepare-and-start.mjs` rather than `start.cmd` / `start.sh`?** An MCP host runs
> the command directly, not through a shell — a `.cmd` is not portable and a `.sh` is not
> executable on Windows. A `.mjs` runs identically wherever `node` does, and `node` is
> already required.

### By hand

```bash
cd tools/beep-mcp-server
./start.sh          # macOS / Linux / Git Bash
start.cmd           # Windows
```

Both install and build as needed, then run. Add `--check` to set up and verify without
starting. Under the hood they call the same `prepare.mjs` the MCP entry point does, so the
logic lives in one place instead of drifting across two shell dialects.

### Using it in your OWN games

Two folders and one script:

1. Copy **`addons/godot_mcp`** and **`tools/beep-mcp-server`** into your game.
2. In your game, run `tools/beep-mcp-server/setup.cmd` (Windows — double-click works)
   or `./setup.sh`.
3. In your game folder: `claude`. No prompt, no approval — it is already registered.

`setup` registers the server with Claude, creates a `.csproj` if the game has none,
enables the plugins, turns on Claude's write permission, and builds. Add `--no-writes`
for look-but-don't-touch.

> **Why not `.mcp.json`?** It is the obvious choice and it is the wrong one here. A
> project-scoped `.mcp.json` sits at `⏸ Pending approval` until you accept a workspace
> trust dialog *and* an approval prompt — which reads as "nothing happened". `setup`
> uses **local scope** instead (`claude mcp add --scope local`): stored in
> `~/.claude.json` under this game's own path, so it loads in this game, stays invisible
> in your other games, and needs no approval. Do **not** switch it to `--scope user` —
> that is one shared entry across every project, and your second game would overwrite
> the first.

> **The `.csproj` is not optional.** `godot_mcp` is C#, and a Godot project without a
> `.csproj` compiles nothing — so the addon sits there completely inert, with no error
> anywhere. Measured: zero plugin log lines in a game without one, ten with. That is
> why `setup` creates it.

Want the Beep game-builder components too? Copy `addons/beep_game_builder_cs` and
`addons/beep_ui` as well before running setup; it enables whatever it finds.

**One game at a time.** The bridge port is `8789` and the server keeps one socket per
role, so opening a second game takes the connection from the first.

### Then start Godot

The `godot_mcp` plugin auto-connects on load — check Godot's **Output** panel for the
bridge line. `godot_mcp/bridge/url` should be `ws://127.0.0.1:8789`; note that
`GodotMcpSettings.Initialize` **force-writes** that default on load, so editing it by hand
in Project Settings is overwritten.

Writes are refused until you enable `godot_mcp/security/allow_editor_writes` (and/or
`allow_runtime_writes`). Both ship **off** — reads work regardless.

## Using it

### Two things must be true

1. **Claude has the server.** `claude mcp list` should show `beep-godot` — run it from the
   game folder, since local scope is per-directory. Confirm in a session with `/mcp`.
   (If you ran `setup` while Claude was already running, restart it — servers are read at
   startup.) A `⏸ Pending approval` line means a stray `.mcp.json` is shadowing it;
   `claude mcp remove beep-godot -s project` clears that.
2. **Godot is open on this project.** The addon dials the server every 2s, so order does
   not matter; whichever starts second connects.

`godot_status` answers either way and tells you which half is missing. That is the first
thing to call when anything says `NOT_CONNECTED`.

### Reads work immediately. Writes need one manual step.

Every write is refused until you turn on the gate, and **you cannot turn it on through
MCP** — setting a project setting is itself a write, so the request is refused for the same
reason. That is deliberate: the consent to let an agent edit your project has to come from
outside the agent's reach.

In Godot: **Project → Project Settings → godot_mcp → security**

| Setting | Enables |
|---|---|
| `allow_editor_writes` | editing scenes, creating resources/themes, baking textures, saving |
| `allow_runtime_writes` | changing live game state (score, weather, saves) |
| `allow_node_method_calls` | calling arbitrary methods on nodes — leave off unless needed |

Leave them off for read-only work; nothing below in the "look" list needs them.

### Things to ask for

**Look at the project** (no gate needed):
- *"What genres and themes does the skin catalog have?"* → `beep_command` / `beep.catalog`
- *"Show me the node tree of the open scene"* → `godot_scene_tree`
- *"Are there any layout problems in this screen?"* → `godot_layout` — flags zero-height
  controls and children overflowing their parent
- *"What warnings has Godot logged?"* → `godot_log_tail` — this framework says everything
  important through `PushWarning`, so this is how you hear it
- *"What properties does a ProgressBar have?"* → `godot_class_describe`

**Change things** (needs `allow_editor_writes`):
- *"Restyle this header — 44px back button, 20px separation"* → `godot_batch`, which lands
  as **one** Godot undo entry you can Ctrl-Z
- *"Preview that first"* → same call with `dry_run: true`; mutates nothing
- *"Bake the textures for the racing genre"* → `beep_command` / `beep.bake_textures`
- *"Make a new screen for the rpg genre called Shop"* → `beep_command` / `beep.new_screen`
- *"Wire this button's pressed signal to OnBackPressed"* → `godot_signal_connect`

**Check your work** (no gate needed):
- *"Do the gates still pass?"* → `beep_gate_all` — `dotnet build` then `validate_scenes.sh`,
  parsed, so a failure names the file and line or the failing check
- *"Run the project headlessly and tell me if it errors"* → `beep_headless_run`
- *"What changed?"* → `godot_scene_snapshot` before, `godot_scene_diff` after

### The habit worth forming

Ask for a **dry run first**, then apply as a **batch**, then run the **gates**. Three steps,
and the middle one is a single undo away from never having happened. The full sequence is
under [The verify loop](#the-verify-loop) below.

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

## The verify loop

Having the tools is not the same as knowing the order. This is the sequence that makes an
agent-driven change safe to run unattended:

```
1. godot_scene_snapshot { label: "before" }      record the starting shape
2. godot_batch { dry_run: true, ops }            predict — mutates nothing
3. godot_batch { ops, label: "restyle header" }  apply — ONE undo entry
4. beep_gate_build  →  beep_gate_scenes          do the gates still pass?
5. godot_play + godot_capture + godot_log_tail   does it actually WORK?
6. godot_scene_diff { from: "before" }           did only what I intended change?
7. on failure: Ctrl-Z in Godot                   one step reverts the whole batch
```

Step 7 is only possible because every write goes through `EditorUndoRedoManager`. The loop
is safe to run unattended precisely because every step is reversible.

**Step 5 is the one people skip.** The two gates prove the code *loads*, not that it works
— that distinction is written into this repo's `CLAUDE.md` because a save system shipped
here whose `Save()` was a hard no-op while every gate stayed green.

## Verification

```bash
npm run smoke     # server logic vs a simulated addon — no Godot needed
BEEP_GODOT_BIN="H:/dev/Godot/Godot_v4.7-stable_mono_win64.exe" npm run live
```

`live` launches a real headless editor and drives the surface end to end. It is worth the
extra minute: it caught three protocol bugs that every simulated check passed —
`game.command` gating *reads* behind write permission, a `name`/`command` parameter
mismatch, and discovery probing a status key that never existed.

## Scope

This is Phase 0 of [`docs/mcp/MCP_ROADMAP.md`](../../docs/mcp/MCP_ROADMAP.md): plumbing
only, no new Godot-side capability. Undo/batching/dry-run is Phase 1; resource, theme and
animation authoring is Phase 2. If you are about to add cleverness here, check the roadmap
first — it probably belongs behind a bridge method instead.
