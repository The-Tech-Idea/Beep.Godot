# MCP Roadmap — master tracker

Owner doc for the work that turns `addons/godot_mcp/` from an unreachable bridge into an
MCP surface an agent can actually build with. One row per phase; the detail lives in the
per-phase document beside this one.

**Status legend:** ⬜ not started · 🟨 in progress · ✅ done & verified · ⛔ blocked

| Phase | Goal | Doc | Status |
|---|---|---|---|
| **0** | Make it connectable — the MCP server that doesn't exist yet | [PHASE_0_CONNECTIVITY.md](PHASE_0_CONNECTIVITY.md) | ✅ `tools/beep-mcp-server/` |
| **1** | Safe writes — undo, batching, dry-run, honest errors | [PHASE_1_SAFE_WRITES.md](PHASE_1_SAFE_WRITES.md) | ✅ *(Ctrl-Z test unrun)* |
| **2** | Creative authoring — resources, themes, animation, signals, scripts | [PHASE_2_AUTHORING.md](PHASE_2_AUTHORING.md) | ✅ *(unrun vs live editor)* |
| **3** | Perception — see the result, read the errors | [PHASE_3_PERCEPTION.md](PHASE_3_PERCEPTION.md) | ✅ *(unrun vs live editor)* |
| **4** | Autonomy — run the gates and iterate without a human | [PHASE_4_AUTONOMY.md](PHASE_4_AUTONOMY.md) | ⬜ |

Update the status column in the same commit that lands the work. A ✅ here means the
phase's own verification section was **run**, not written — this repo has a documented
history of ✅ meaning "I wrote it" (see the SESSION_SUMMARY / weather-report note in
`CLAUDE.md`). If it was only compile-checked, say 🟨 and note why.

---

## The problem this roadmap exists to fix

**`addons/godot_mcp/` has never been reachable by Claude.** The addon is a WebSocket
**client**: `McpWebSocketClient.ConnectNow()` dials **out** to
`ws://127.0.0.1:8789/{role}?token=…`. Nothing in this repository — or anywhere on the
machine — listens on that port. There is no server, no MCP manifest, and no way to run
`claude mcp add`. Every method in the 582-line `GodotMcpBridgeController` has therefore
been dead code in practice.

That is the whole of Phase 0, and nothing after it matters until it is done.

Beyond connectivity, the surface is **read-and-poke, not authoring**. The bridge can set a
property and create a node, but it cannot create a `Resource`, edit a `Theme`, build an
`Animation`, connect a `Signal`, undo anything, batch anything, or tell an agent what went
wrong in a machine-readable way. Phases 1–4 close that.

---

## Architecture (target)

```
┌──────────────┐   MCP over stdio    ┌──────────────────────────┐   WebSocket    ┌────────────────┐
│ Claude Code  │◄───────────────────►│  tools/beep-mcp-server/  │◄──────────────►│ Godot editor   │
│  (or any     │   tools/resources   │  • MCP server (stdio)    │  ws://…:8789   │  role=editor   │
│   MCP host)  │                     │  • WS **server** :8789   │◄──────────────►│ Godot runtime  │
└──────────────┘                     │  • request router + auth │                │  role=runtime  │
                                     └──────────────────────────┘                └────────────────┘
```

The server hosts the socket; Godot keeps dialling out exactly as it does today, so **no
transport code in the addon changes**. Both roles can be connected at once — the router
picks by role, because `beep.add_component` needs the editor and `beep.add_score` needs a
running game.

**Server home:** `tools/beep-mcp-server/` (Node + TypeScript, `@modelcontextprotocol/sdk`).
Versioned beside the addon it speaks to, so the wire protocol cannot drift.

> **Phase 0 is built.** `npm install && npm run build`, then
> `claude mcp add beep-godot -- node <abs>/tools/beep-mcp-server/dist/index.js`.
> `npm run smoke` verifies the whole path without a Godot binary — it drives the real
> server over stdio and impersonates the addon on the socket. 17 tools; 12 checks green,
> covering Godot-closed, Godot-connected, a refused write, and disconnect-mid-request.
> Verified against a **simulated** addon, not a live editor — no Godot binary exists on
> this machine, so step 3–7 of the phase doc's verification remain to be run for real.

---

## The wire protocol as it exists today

Do not redesign this in Phase 0 — implement against it, then evolve it in Phase 1.

**Connect:** Godot → `ws://host:port/{role}?token={token}`, `role` ∈ `editor` | `runtime`.
Query params are on the path because Godot 4's `WebSocketPeer` rejects them on connect.

**Handshake:** Godot sends, unprompted, on open:
```json
{ "method": "hello",
  "params": { "token": "…", "bridge": "godot-mcp-csharp", "version": "0.2.0",
              "role": "editor", "editor_hint": true, "godot_version": "…" } }
```

**Request** (server → Godot): `{ "id": "…", "method": "…", "params": { … } }`
**Response** (Godot → server): `{ "id": "…", "ok": true, "result": … }`
or `{ "id": "…", "ok": false, "error": "…", "error_type": "…" }`

**Methods** (`GodotMcpBridgeController.ExecuteMethod`): `ping`, `status.get`,
`tree.serialize`, `scene.current`, `editor.selection.get|set`,
`node.get|list_properties|set_property|call_method|create|delete|reparent`,
`shader.attach_canvas_item`, `shader.set_uniform`, `tween.property`,
`particles.create_2d`, `projectile.sample_arc_2d`, `sprite.move_to`,
`runtime.pause|resume|screenshot`, `input.action`, `game.command`, `game.state`,
`project.setting.get|set`.

`game.command` is the extension point Beep uses: `BeepMcpCommands.Register()` puts ~40
`beep.*` handlers into `McpCommandRegistry`, reached as
`game.command {"name": "beep.inspect_scene", "args": {…}}`.

**Security flags** (`ProjectSettings`, enforced Godot-side):
`godot_mcp/security/allow_editor_writes`, `…/allow_runtime_writes`,
`…/allow_node_method_calls`. The server must never assume it can bypass these; it should
surface them in `status` so an agent knows why a write was refused.

---

## Cross-cutting rules for every phase

1. **Never fail silently.** The repo's dominant defect class. A refused write, an offline
   Godot, an unknown property — each returns a structured error naming what to do next.
   An MCP tool that returns `{"ok": true}` after discarding the request is the worst
   possible outcome.
2. **PascalCase `[Export]`s.** Godot drops a snake_case C# export assignment silently.
   `beep.set_node_property` already refuses it; every new write path must too.
3. **Writes stay gated.** `allow_editor_writes` / `allow_runtime_writes` are the user's
   consent. No new unguarded surface.
4. **Reuse the bridge's serializers.** `McpTreeSerializer`, `McpJson` — an agent should see
   one node shape everywhere, not three.
5. **Godot offline is normal, not an error.** The editor is closed most of the time. Tools
   report it plainly and the server stays up.

---

## Resolved overlaps

- **Screenshots.** `runtime.screenshot` wrote a file and `beep.screenshot` returned inline
  base64 — two ways to take a picture, only one of which an agent can actually look at.
  Phase 1 made `runtime.screenshot` return inline base64 as well (capped, default 1280px
  wide), keeping its `path`/`absolute_path` fields so nothing that used them breaks.
