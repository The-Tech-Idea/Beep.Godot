# Phase 2 — Creative authoring

**Goal:** an agent can *make* things, not just poke at things that already exist.

**Status:** ✅ built — `addons/godot_mcp/GodotMcpBridgeController.Authoring.cs` +
`tools/beep-mcp-server/src/authoring.ts`. 18 new bridge methods, 37 MCP tools total.
`dotnet build` 0 errors, `npm run smoke` 23 checks green. Verified against a **simulated**
addon — no Godot binary here — so nothing below has been run against a live editor.
· [back to roadmap](MCP_ROADMAP.md)

---

## Why

The bridge can set a property and create a node. That is the whole of its creative power.
Everything this framework is actually made of is out of reach:

| An agent cannot… | Even though the repo is built on it |
|---|---|
| create or edit a `Resource` (`.tres`) | `GameInfo`, `UISkin`, `ColorPalette`, `GeometryProfile` are all Resources |
| build or modify a `Theme` / `StyleBox` | the entire skin layer |
| author an `Animation` / `AnimationPlayer` | every effect component tweens by hand instead |
| connect a `Signal` | components communicate by signal — the documented pattern |
| instance a `PackedScene` into a scene | templates exist precisely to be instanced |
| create or edit a script | 34 screen scripts follow one shape |
| create a new `.tscn` from nothing | `beep.new_screen` does exactly one shape of screen |

So an agent can restyle an existing button but cannot build a screen, a theme, or an
effect. Phase 2 is what makes "be more creative" true rather than aspirational.

## Deliverables

### 2.1 Resource authoring — `resource.*`

```
resource.create   { type, path, properties{} }   → make a .tres and save it
resource.load     { path }                       → property dump
resource.set      { path, properties{} }         → edit + re-save
resource.inspect  { type }                       → property list + types for a Resource class
```

Guarded by `allow_editor_writes`, routed through Phase 1's undo + write validation, and
subject to the same PascalCase `[Export]` rule — `UISkin.PatchMargin`, not `patch_margin`.

Immediate wins: an agent can author a `UISkin`, tune a `ColorPalette`, or edit
`GameInfo.tres` without the dock.

### 2.2 Theme + StyleBox authoring — `theme.*`

```
theme.create        { path }
theme.set_stylebox  { path, type, name, stylebox{…} }
theme.set_color / set_font_size / set_constant
theme.add_type_variation { path, variation, base }
```

`ThemePresetComponent` generates its `Theme` at runtime, so this is not for skinning the
framework — it is for the developer's *own* Themes, and for baking a generated theme to
disk so it can be inspected and diffed. Pair it with `beep.bake_textures` (already built)
and an agent can produce a complete skin end to end.

**Must respect the four registered variations** — `BeepTitle`, `BeepSubtitle`, `BeepValue`,
`BeepCaption`. `validate_scenes.sh` fails on any other `theme_type_variation`, so
`theme.add_type_variation` should warn when it invents a fifth.

### 2.3 Animation authoring — `animation.*`

> **Implemented guard.** `animation.add_track` REFUSES a `position` / `scale` / `rotation`
> track on a Control whose parent is a Container, returning
> `CONTAINER_OVERWRITES_TRANSFORM` and naming `offset_transform_*` as the fix — plus the
> `pivot_offset` warning for scale/rotation, since it defaults to the top-left corner. The
> container re-sorts every layout pass and overwrites the animated value, so the track
> would silently do nothing. This repo has paid for that bug twice.


```
animation.create      { player_path, name, length, loop }
animation.add_track   { player_path, name, node_path, property, keys[] }
animation.play/stop
```

Godot's `Animation` + `AnimationPlayer` are the idiomatic way to do what several components
currently hand-roll with `Tween`. An agent that can key an animation can build effects the
framework does not ship.

Reuse the **offset-transform rule** from `CLAUDE.md`: key `offset_transform_position` /
`_scale` / `_rotation`, never `position`/`scale`/`rotation`, on any Control inside a
container — the container re-sorts and overwrites the latter. And set `pivot_offset` before
keying scale or rotation. The tool should refuse a `position` track on a container child
and say why; that is a bug this repo has already paid for twice.

### 2.4 Signal wiring — `signal.*`

```
signal.list      { node }              → signals + existing connections
signal.connect   { from, signal, to, method, binds[], flags }
signal.disconnect{ … }
```

Connections are scene data — they persist in the `.tscn` — so this is a real authoring
capability, not a runtime poke. It also lets an agent verify wiring instead of inferring
it, which is the check `validate_scenes.sh` performs statically today.

### 2.5 Scene composition — `scene.*`

```
scene.create        { path, root_type, root_name }
scene.instance      { packed_scene_path, parent, name }   → instance a template
scene.save_as       { path }
scene.duplicate_node{ node, new_name }
```

`scene.instance` is the one that matters most: `templates/scenes/` exists to be instanced,
and today an agent can only build node-by-node. Instancing preserves the template's own
future edits; hand-built copies do not.

### 2.6 Script authoring — `script.*`

```
script.create   { path, base_class, class_name, body }
script.read     { path }
script.attach   { node, script_path }
```

Writing arbitrary C# from an agent is the sharpest tool here, so constrain it:
- template-first — default to the shape `BeepScreenGenerator` already emits;
- `[Tool] [GlobalClass] partial`, **file name must equal class name** (registration is
  filename-driven — a mismatch fails the build);
- inherit a **category** base (`UIComponent`, `GameplayComponent`, `WorldComponent`,
  `ControllerComponent`), never `EntityComponent` directly;
- after any script write, the agent is expected to run the Phase 4 build gate. A script
  that does not compile takes the whole addon down with it.

### 2.7 Class discovery — `classdb.*`

```
classdb.list        { inherits, filter }
classdb.describe    { class }   → properties, methods, signals, enums
```

Without this an agent guesses node types and property names. `ClassDB` already has the
answer; expose it and guessing stops.

## Tasks

- [x] `resource.create` / `resource.load` / `resource.set` — any Resource class, properties
      applied through the same guard as node writes (a snake_case `[Export]` is refused
      naming the PascalCase form, an unknown property is refused rather than dropped)
- [x] `theme.create` / `theme.set_stylebox` / `theme.set_value` / `theme.add_type_variation`.
      `set_value` covers color + font_size + constant in one call rather than three
      near-identical methods. `set_stylebox` takes a `{class, properties}` spec, so any
      StyleBox class works without a bespoke schema per box type.
- [x] `animation.create` / `animation.add_track`, **with the container guard** (below)
- [x] `signal.list` / `signal.connect` / `signal.disconnect`, connections PERSISTED so they
      survive in the `.tscn`
- [x] `scene.instance` / `scene.save_as` / `scene.duplicate_node`, all undoable
- [x] `script.attach`
- [x] `classdb.list` / `classdb.describe`
- [x] Server: 18 tools in `src/authoring.ts`, registered from `tools.ts`

**Scoped out, deliberately:**

- **`script.create`.** Writing arbitrary C# from an agent is the sharpest tool in the phase,
  and a file that does not compile takes the *whole addon* down — every component
  disappears from Add Node until it is fixed. Generation stays with `BeepScreenGenerator`
  (`beep.new_screen`), which emits a shape known to build. `script.attach` covers wiring an
  already-compiled script, and its error says so when the script has not been built yet.
- **`scene.create` from nothing.** `beep.new_screen` already creates a scene with this
  repo's conventions correct by construction; a blank-scene primitive would mostly be used
  to rebuild that badly.
- **`animation.play` / `stop`.** Playback is a runtime concern and the AnimationPlayer being
  authored lives in the editor. It belongs with Phase 4's play control.
- **`resource.inspect` by class.** `classdb.describe` already answers it.

No `validate_scenes.sh` change was needed: every authoring path writes shapes the existing
checks already cover, and `theme.add_type_variation` warns when it would create one the
variation check rejects.

## Verification

Each is an end-to-end build, not a unit test:

1. **Skin, start to finish:** `theme.create` → `beep.bake_textures` → apply to a scene →
   screenshot shows textured widgets.
2. **Resource:** `resource.create` a `UISkin`, set `PatchMargin`, assign it to a
   `ThemePresetComponent`, confirm the change renders.
3. **Animation:** author a 0.3s pulse keying `offset_transform_scale`, play it, and confirm
   it animates. Then try to key `scale` on a container child and confirm it is **refused**.
4. **Signal:** connect a Button `pressed` to a method, save, reopen the `.tscn`, confirm the
   connection persisted.
5. **Composition:** `scene.create` + `scene.instance` a template + `scene.save_as`, then run
   `validate_scenes.sh` on the result — PASS.
6. **Script:** create a `UIComponent` subclass, attach it, `dotnet build` — 0 errors, and it
   appears in Add Node.
7. **classdb.describe("ProgressBar")** returns the real property list.

## Out of scope

Seeing the result (Phase 3) and running the gates automatically (Phase 4). Also: generating
game content — art, levels, balance. Per `CLAUDE.md`, that is the developer's canvas; these
tools build the *framework* pieces.
