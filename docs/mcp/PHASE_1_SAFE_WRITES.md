# Phase 1 — Safe writes

**Goal:** an agent can change a scene without the change being unreviewable, unrepeatable,
or silently discarded.

**Status:** ⬜ not started · depends on Phase 0 · [back to roadmap](MCP_ROADMAP.md)

---

## Why

The write surface today is one property at a time, with no undo entry, no way to preview,
and no way to know afterwards what actually landed:

- **Nothing an agent does is undoable.** `node.set_property`, `node.create`, `node.delete`
  and every `beep.*` editor write mutate the edited scene directly. Godot's Ctrl-Z does not
  see them, because none of them go through `EditorUndoRedoManager`. A wrong edit is only
  recoverable by closing the scene without saving — which throws away the good edits too.
- **No batching.** Restyling one screen is 30–60 property writes, each a full
  request/response round trip, each independently undoable-by-nobody. A crash halfway
  leaves the scene in a state neither the agent nor the user can describe.
- **No dry run.** There is no way to ask "what would this do" before doing it.
- **Errors are strings.** `SendError(id, ex.Message, ex.GetType().Name)` gives an agent
  prose to regex. There is no code, no field, no suggested fix.
- **A wrong property name is a no-op.** `node.set_property` on a name Godot doesn't know
  discards the value and reports success. `beep.set_node_property` now detects this and the
  snake_case `[Export]` trap — that logic belongs in the bridge, applied to every write.

## Deliverables

### 1.1 Undo/redo integration — `EditorUndoRedoManager`

Route every editor-side mutation through
`EditorInterface.Singleton.GetEditorUndoRedoManager()`:

```
CreateAction("MCP: set ColorRect.color")
  AddDoProperty(node, "color", newValue)
  AddUndoProperty(node, "color", oldValue)
CommitAction()
```

Same for create (`AddDoMethod(parent, "add_child", n)` / `AddUndoMethod(parent,
"remove_child", n)` + `AddDoReference`), delete, and reparent. The action name carries the
`MCP:` prefix so a user scanning the undo history can see what the agent did.

**Consequence worth stating:** the user gets Ctrl-Z over agent edits. That single change is
what makes agent-driven scene work safe enough to leave enabled.

### 1.2 Transactions — `bridge.batch`

One new bridge method taking an ordered list of operations, executed inside **one**
`CreateAction`/`CommitAction` pair:

```json
{ "method": "bridge.batch",
  "params": { "label": "restyle garage header",
              "atomic": true,
              "ops": [ {"method": "node.set_property", "params": {…}}, … ] } }
```

- `atomic: true` (default) — any failure aborts the action; nothing is committed.
- Returns a per-op result array, so a partial failure names the exact op index.
- One undo entry for the whole batch.

This turns a 40-call restyle into one request, one undo step, one reviewable action.

### 1.3 Dry run

`"dry_run": true` on `bridge.batch` and on every write method: validate everything —
node exists, property is registered, value coerces, write gate is on — and return the
per-op verdict **without mutating**. This is the tool an agent should reach for first, and
it costs nothing to add once 1.4 exists.

### 1.4 Write validation, in the bridge

Lift the two guards already proven in `BeepMcpSceneCommands` into a shared helper every
write path calls:

- **Unknown property → error, not success.** Check `GetPropertyList()` before `Set`.
- **snake_case `[Export]` → refuse**, naming the PascalCase form. Godot silently drops the
  snake_case spelling; this cost the repo 67 dead assignments across 33 scenes.
- **Type mismatch → error** naming expected vs received (`McpJson.ToVariant` currently
  coerces or yields a null Variant with no complaint).
- **`remove_node` referential check** — refuse while another node's `NodePath` export still
  points at the target.

### 1.5 Structured errors

Replace the bare `{ok:false, error, error_type}` with a machine-readable shape, kept
backward compatible by leaving the two existing fields in place:

```json
{ "id": "…", "ok": false,
  "error": "…human sentence…",
  "error_type": "InvalidOperationException",
  "code": "WRITE_DISABLED",
  "detail": { "setting": "godot_mcp/security/allow_editor_writes" },
  "fix": "Enable allow_editor_writes in Project Settings." }
```

Codes to define: `NOT_CONNECTED`, `WRITE_DISABLED`, `NO_SCENE_OPEN`, `NODE_NOT_FOUND`,
`UNKNOWN_PROPERTY`, `SNAKE_CASE_EXPORT`, `TYPE_MISMATCH`, `STILL_REFERENCED`,
`METHOD_UNKNOWN`, `TIMEOUT`.

### 1.6 Capability discovery

Extend `status.get` with a machine-readable capability block — bridge version, method list
with param schemas, the `beep.*` command list, security flags, connected roles, Godot
version. An agent should never have to guess whether a method exists.

### 1.7 Resolve the screenshot overlap

`runtime.screenshot` (writes to `user://mcp_screenshots`) and `beep.screenshot` (inline
base64) both exist. Make the inline shape canonical (an agent can *look* at base64; it
cannot look at a path) and have the other delegate, keeping its old response fields.

## Tasks

- [ ] `BridgeUndo` helper wrapping `EditorUndoRedoManager` for property/create/delete/reparent
- [ ] Route `node.*` and every `beep.*` editor write through it
- [ ] `bridge.batch` — ordered ops, `atomic`, one undo entry, per-op results
- [ ] `dry_run` on batch + all write methods
- [ ] `BridgeWriteGuard` — unknown property / snake_case export / type mismatch / referenced
- [ ] Structured error envelope + the code table
- [ ] `status.get` capability block
- [ ] Screenshot delegation
- [ ] Server: `godot_batch` + `dry_run` passthrough; map codes to actionable MCP messages

## Verification

1. `dotnet build` clean; `validate_scenes.sh` PASS.
2. **Undo:** set a property over MCP, then press Ctrl-Z in Godot — the value reverts and the
   history entry reads `MCP: …`. This is the phase's headline test.
3. **Batch atomicity:** send 5 ops with op 3 invalid, `atomic: true`. Nothing changes; the
   response names index 2. Then `atomic: false` — ops 1,2,4,5 land and 3 reports.
4. **One undo step:** a 10-op batch reverts fully with a single Ctrl-Z.
5. **Dry run:** the same batch with `dry_run: true` returns per-op verdicts and the scene is
   byte-identical afterwards (diff the `.tscn`).
6. **Unknown property** returns `UNKNOWN_PROPERTY`, not success.
7. **snake_case export:** `title_label_path` returns `SNAKE_CASE_EXPORT` naming
   `TitleLabelPath`.
8. **Still-referenced delete** returns `STILL_REFERENCED` naming the referring export.
9. **Gate off** → `WRITE_DISABLED` with the setting path in `detail`.

## Out of scope

Resource/theme/animation authoring (Phase 2), visual feedback (Phase 3), running the gates
(Phase 4).
