# Phase 2 — Creative authoring

**Goal:** an agent can *make* things, not just poke at things that already exist.

**Status:** ⬜ not started · depends on Phase 1 · [back to roadmap](MCP_ROADMAP.md)

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

- [ ] `resource.*` (create/load/set/inspect) + undo + write validation
- [ ] `theme.*` (create/set_stylebox/set_color/set_font_size/set_constant/add_type_variation)
- [ ] `animation.*` (create/add_track/play/stop) + offset-transform + pivot guards
- [ ] `signal.*` (list/connect/disconnect)
- [ ] `scene.*` (create/instance/save_as/duplicate_node)
- [ ] `script.*` (create/read/attach) with the template-first constraints
- [ ] `classdb.*` (list/describe)
- [ ] Server: one MCP tool per group; schemas from Phase 1's capability block
- [ ] Extend `validate_scenes.sh` if any new authoring path can produce a scene shape the
      existing checks would miss

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
