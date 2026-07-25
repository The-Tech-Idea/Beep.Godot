# Phase 3 — Perception

**Goal:** the agent can see what it just did, and read what Godot said about it.

**Status:** ⬜ not started · depends on Phase 1 · [back to roadmap](MCP_ROADMAP.md)

---

## Why

An agent editing UI today is working blind. It can read the node tree — a description of
intent — but not the pixels, which are the actual product. Every layout defect this repo
has shipped was invisible in the tree and obvious on screen: a 0-height back button, a
title at body size, a background covering the pattern meant to sit on it, four buttons
sharing a name.

It also cannot read Godot's own voice. `GD.PushWarning` is the mechanism this framework
uses for *everything* that would otherwise fail silently — and the whole point of those
warnings is lost if the only reader is a human watching the Output panel. An agent that
just baked textures cannot tell whether 200 warnings appeared.

## Deliverables

### 3.1 Screenshots that are actually usable — `view.capture`

Canonical shape: **inline base64 PNG** (an agent can look at base64; it cannot look at a
file path). `beep.screenshot` already does this; Phase 1 makes `runtime.screenshot`
delegate to it. Phase 3 adds what makes it useful:

```
view.capture { target: "editor" | "runtime" | "node",
               node: "Margin/VBox/Header",     // when target=node: that control's rect only
               max_width: 1280 }
```

- **`target: "node"`** is the important one — cropping to a control's global rect turns
  "the screen looks wrong" into "this header is wrong", and keeps the payload small.
- Editor capture uses the edited-scene viewport so an agent can look **without running the
  game**, which is the common case for UI work.

### 3.2 Layout introspection — `view.layout`

```
view.layout { node, recursive } →
  [ { path, class, rect{x,y,w,h}, min_size, size_flags, anchors, visible, clipped } ]
```

The numeric complement to the screenshot, and it catches things a picture does not:
zero-size controls, a child overflowing its parent, a `custom_minimum_size` of `(120, 0)`
where a height was intended. Flag `rect.h == 0`, `rect.w == 0`, and children whose rect
escapes the parent's — those three cover most of what has actually gone wrong here.

### 3.3 Log streaming — `log.tail` / `log.subscribe`

Capture Godot's own output and expose it:

```
log.tail      { level: "warning"|"error"|"all", since, limit }
log.clear
log.subscribe { levels[] }        // pushed to the server as they occur
```

Implementation: a small ring buffer in the addon fed by a custom `Logger`
(`OS.AddLogger` / an `EngineDebugger` capture), recording level, message, and time.

This is what makes the repo's warning discipline pay off for an agent: after
`beep.bake_textures` it can read *"[SkinCatalog] racing/arcade slot 'panel' points at … which
does not exist"* and act, instead of assuming success.

### 3.4 Before/after diffing — `scene.diff`

```
scene.snapshot { label }              → stores a serialized tree
scene.diff     { from, to }           → added / removed / changed-property list
```

After a 40-op batch, "what actually changed" should be answerable without re-reading the
whole tree. Pairs naturally with Phase 1's `dry_run`: diff the predicted change against the
real one.

### 3.5 Viewport control — `view.camera`

```
view.camera { target: "editor", frame_node }   // frame a node in the editor viewport
view.zoom   { factor }
```

So a capture can be aimed. Without it, `view.capture` on a large scene returns a mostly
empty picture.

## Tasks

- [ ] `view.capture` with `target` editor/runtime/node + node-rect cropping
- [ ] Make `runtime.screenshot` delegate (Phase 1 item, verified here)
- [ ] `view.layout` + the three zero/overflow flags
- [ ] Log ring buffer + `log.tail` / `log.clear` / `log.subscribe`
- [ ] `scene.snapshot` / `scene.diff`
- [ ] `view.camera` / `view.zoom`
- [ ] Server: return captures as MCP image content, not a base64 string in text

## Verification

1. **Editor capture, game not running:** `view.capture {target:"editor"}` on an open
   `racing/garage.tscn` returns an image showing the garage layout.
2. **Node crop:** `view.capture {target:"node", node:"Margin/VBox/Header"}` returns just the
   header strip.
3. **Layout catches a real defect:** set a back button's `custom_minimum_size` to `(120,0)`
   — the historical bug — and confirm `view.layout` flags a zero/short height.
4. **Warnings are readable:** delete one baked PNG, re-apply a theme, and confirm
   `log.tail {level:"warning"}` returns the `[SkinCatalog]` warning naming genre, theme,
   slot and path.
5. **Diff:** snapshot → 10-op batch → diff lists exactly those 10 changes, no more.
6. **Dry-run agreement:** the `dry_run` prediction and the post-hoc diff match.

## Out of scope

Running builds/tests (Phase 4). No always-on video/streaming — captures are on demand.
