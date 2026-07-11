# Beep UI

A GDScript Godot 4.3+ addon for **drag-and-drop, themed UI**: 22 theme presets
that style an entire scene, 11 UI effects, and 84+ ready themed widgets.
Self-contained — works in **every** Godot project (no C#/.NET required).

It is a faithful GDScript port of the original C# theming engine, with the
"themes do nothing" placement bug fixed.

---

## Table of contents

1. [Features](#features)
2. [Installation](#installation)
3. [Core concepts](#core-concepts)
4. [Theme Studio dock](#theme-studio-dock)
5. [BeepThemeApplier — style a whole scene](#beepthemeapplier--style-a-whole-scene)
6. [The 22 theme presets](#the-22-theme-presets)
7. [BeepUIEffect — 11 animated effects](#beepuieffect--11-animated-effects)
8. [Widgets — 84+ themed, drag-and-drop](#widgets--84-themed-drag-and-drop)
9. [BeepToastHost API](#beeptoasthost-api)
10. [Creating / customizing a preset](#creating--customizing-a-preset)
11. [File map](#file-map)
12. [Known limitations](#known-limitations)
13. [Troubleshooting](#troubleshooting)

---

## Features

- **22 genre presets** — Modern, SciFi, Cartoon, Classic, Desert, OilGas, Sea,
  Sports, Soccer, Fantasy, Horror, Nature, Space, Military, Steampunk, Retro80s,
  Pixel8Bit, Winter, Cyberpunk, Japan, Toxic, Candy. Each defines a complete
  `ColorSchema` + animation feel + corner/border/shadow geometry.
- **One applier styles everything** — a single `BeepThemeApplier` node builds a
  full Godot `Theme` over a subtree: buttons, inputs, progress bars, sliders,
  scrollbars, trees, lists, popups, tabs, separators, panels.
- **Parent-or-child placement** — drop the applier as a child of a `Control` *or*
  as a parent of `Control`(s). It auto-resolves the target and warns (never
  silently fails) if no `Control` is found.
- **11 UI effects** — Slide, Shake, Pulse, Bob, Flash, Glitch, Rotate, Fade,
  Typewriter, Bounce, Offset — with 4 scopes (Self/Children/Scene/Global) and
  automatic per-effect inspector property visibility.
- **84+ themed widgets** — bars, stat displays, captions, panels, button lists,
  grids, crosshairs, overlays, a working toast host, and clearly-labeled starter
  scaffolds for complex items (minimap, compass, FX).
- **Theme Studio dock** — visual gallery with live swatches, a styled preview,
  and one-click apply + a searchable widget palette.

---

## Installation

1. Copy this folder to `<your_project>/addons/beep_ui`.
2. Open the project in Godot 4.3+.
3. **Project → Project Settings → Plugins → enable "Beep UI"**.
4. The **Beep UI** dock appears on the right side of the editor.

> Tip: after first enable, if the dock doesn't show, reload the project
> (Project → Reload Current Project).

---

## Core concepts

A **preset** (`BeepPreset`) is a complete visual identity — its colors, animation
behaviour, and geometry. The **applier** (`BeepThemeApplier`) reads a preset and
generates a Godot `Theme` resource, then assigns it to a `Control` subtree. Every
themed widget ships with its own applier, so it is styled automatically.

The engine intentionally mirrors the original C# design:
`ColorSchema` (surface / text / accent / border / shadow / background / semantic
color slots), `AnimationConfig` (hover/press scale, shadow lift, focus glow), and
per-preset button `StyleBoxFlat` builders that the applier reuses to derive every
other control type.

---

## Theme Studio dock

The dock has two tabs.

### Themes tab

- A searchable grid of all 22 presets, each showing 5 live color swatches
  (surface, accent, success, warning, danger).
- A **Preview** pane styled with the selected preset (button states, primary /
  danger / success buttons, line edit).
- **Apply to Selected** — adds a `BeepThemeApplier` under the selected node
  (reuses an existing one if present) and sets the chosen preset.
- **Apply to Root** — same, under the scene root.
- **Remove Theme** — removes any `BeepThemeApplier` under the selection / root.

### Widgets tab

- A searchable, categorized list of all 84+ widgets.
- Click a widget to drop a **real, themed** instance under the current selection
  (each carries its own `BeepThemeApplier`).
- Status line reports what was added and where.

---

## BeepThemeApplier — style a whole scene

A `@tool` `Node`. Add it anywhere; it resolves the target(s) automatically.

**Placement (the fix):** it themes in this priority order —
1. If its **parent** is a `Control` → themes that (original child-placement design).
2. Else themes every **`Control` child** (parent-placement).
3. Else the nearest **`Control` ancestor**.
4. Else `push_warning(...)` — never silent.

**Exported properties:**

| Property | Type | Default | Description |
|---|---|---|---|
| `preset` | String (enum) | `"Modern"` | One of the 22 preset names. |
| `enable_animations` | bool | `true` | Hover/press button animations (runtime only). |
| `enable_ripple` | bool | `true` | Reserved (ripple widget pending). |
| `active` | bool | `true` | Master on/off. |

**Signal:** `theme_applied()` — emitted after a successful apply.

**From code:**

```gdscript
# Re-theme at runtime, e.g. when the player picks a style
$BeepThemeApplier.preset = "Cyberpunk"
$BeepThemeApplier.active = true
```

---

## The 22 theme presets

Each is a tiny script under `theme/preset_*.gd` that sets the schema in `_init()`.
The full list with file mapping:

| Preset | File | Vibe |
|---|---|---|
| Modern | `preset_modern.gd` | Clean minimal, soft shadow, 12px corners |
| SciFi | `preset_scifi.gd` | Sharp-top / rounded-bottom technical |
| Cartoon | `preset_cartoon.gd` | Bold, rounded, playful |
| Classic | `preset_classic.gd` | Beveled 3D retro |
| Desert | `preset_desert.gd` | Sandy warm |
| OilGas | `preset_oilgas.gd` | Industrial |
| Sea | `preset_sea.gd` | Aquatic blues |
| Sports | `preset_sports.gd` | Energetic |
| Soccer | `preset_soccer.gd` | Pitch greens |
| Fantasy | `preset_fantasy.gd` | Parchment & gold |
| Horror | `preset_horror.gd` | Dark, blood red |
| Nature | `preset_nature.gd` | Forest greens |
| Space | `preset_space.gd` | Deep void |
| Military | `preset_military.gd` | Olive / steel |
| Steampunk | `preset_steampunk.gd` | Brass & cog |
| Retro80s | `preset_retro80s.gd` | Synthwave |
| Pixel8Bit | `preset_pixel8bit.gd` | 8-bit, no animation |
| Winter | `preset_winter.gd` | Icy pastels |
| Cyberpunk | `preset_cyberpunk.gd` | Neon pink + cyan |
| Japan | `preset_japan.gd` | Sumi-e restraint |
| Toxic | `preset_toxic.gd` | Acid green |
| Candy | `preset_candy.gd` | Bright pastels |

---

## BeepUIEffect — 11 animated effects

A `@tool` `Node` (`class_name BeepUIEffect`). Attach to any node. Two dropdowns
control everything; per-effect parameters appear only when relevant.

**Effects:** `SLIDE`, `SHAKE`, `PULSE`, `BOB`, `FLASH`, `GLITCH`, `ROTATE`,
`FADE`, `TYPEWRITER`, `BOUNCE`, `OFFSET`.

**Scopes:** `SELF` (parent control), `CHILDREN`, `SCENE`, `GLOBAL`.

**Key properties:** `effect`, `scope`, `duration`, `initial_delay`, `easing`,
`transition`, `play_on_ready`, `looping`, `loop_delay`, plus per-effect knobs
(e.g. `slide_distance`, `shake_intensity`, `pulse_min/max_scale`,
`typewriter_speed`, `bounce_height`, …).

**Signals:** `effect_started`, `effect_completed`, `effect_looped(loop_count)`.

**From code:**

```gdscript
$BeepUIEffect.effect = BeepUIEffect.EffectType.SHAKE
$BeepUIEffect.shake_intensity = 14.0
$BeepUIEffect.duration = 0.5
$BeepUIEffect.play()
# later…
$BeepUIEffect.stop()
$BeepUIEffect.reset()
```

> Auto-play on ready runs at runtime only (not in the editor viewport), so the
> editor stays stable.

---

## Widgets — 84+ themed, drag-and-drop

Open the dock's **Widgets** tab and click any widget to drop a themed instance
under the selection. Each widget is a real `Control` subtree with a child
`BeepThemeApplier`, so it is styled by the current preset automatically.

**Archetypes** (the factory builds each from the catalog entry):

| Archetype | Produces | Examples |
|---|---|---|
| `bar` | Label + ProgressBar | health, ammo, cooldown, boss, segmented, match timer |
| `stat` | Caption + value label | score, speedometer, altitude, accuracy, combo, wave, timer |
| `caption` | Centered label | interaction prompt, subtitle, zone warning, floating damage |
| `panel` | Titled PanelContainer | quest log, leaderboard, teammate, debug, console, chat, shop |
| `button_list` | VBox of buttons | weapon wheel, skill tree, context menu, input hints |
| `grid` | GridContainer of slots | inventory grid, status effect icons, tech tree |
| `toast_host` | Working toast host | notifications, loot popup |
| `crosshair` | Centered reticle | crosshair, reticle+ping |
| `overlay` | Full-rect ColorRect | vignette, glitch, scanlines, scene transition |
| `scaffold` | Themed starter shell | minimap, compass, parallax, virtual joystick, particle UI |
| `system` | Non-visual note | data binder, state machine, audio manager, etc. |

`scaffold` and `system` are intentionally honest: complex/visual widgets ship as
clearly-labeled themed starter shells to extend, and the 15 architecture modules
are flagged as non-visual.

---

## BeepToastHost API

The `toast_host` archetype attaches a real, working notifier.

```gdscript
# Static helper (auto-registers the first active host at runtime)
BeepToastHost.show_toast("Saved")
BeepToastHost.show_toast("Out of ammo", BeepToastHost.TYPE.WARNING)
BeepToastHost.show_toast("Connection lost", BeepToastHost.TYPE.ERROR)

# Or call on a specific host instance
$BeepToastHost.spawn("Level up!", BeepToastHost.TYPE.SUCCESS)
```

Types: `INFO`, `SUCCESS`, `WARNING`, `ERROR`. Properties: `duration`,
`toast_size`, `max_visible`.

---

## Creating / customizing a preset

1. Copy `theme/preset_modern.gd` to `theme/preset_mine.gd`.
2. Edit the color / animation / geometry assignments in `_init()`.
3. Register it in two places in `theme/beep_theme.gd`:
   - add an entry to the `_PRESET_SCRIPTS` dictionary, and
   - add the name to the `@export_enum(...)` list in `theme/theme_applier.gd`.
4. Restart the editor; it appears in the dock gallery and the applier dropdown.

Each preset sets these groups: **Surface**, **Text**, **Accent**, **Border**,
**Shadow**, **Background**, **Semantic**, **Geometry** (corner/border/pad/shadow
sizes), **Animation**.

---

## File map

```
addons/beep_ui/
  plugin.cfg              Addon manifest
  plugin.gd               EditorPlugin — registers the dock
  theme/
    beep_theme.gd         BeepPreset base (ColorSchema + AnimationConfig + builders + registry)
    preset_*.gd (×22)     The 22 presets
    theme_applier.gd      BeepThemeApplier (@tool Node) — the styling engine
  effects/
    ui_effect.gd          BeepUIEffect (@tool Node) — 11 effects
  widgets/
    widget_factory.gd     BeepWidgetFactory — builds themed widgets from a catalog
    toast_host.gd         BeepToastHost — working toast notifier
  editor/
    theme_studio.gd       The dock (Themes tab + Widgets tab)
```

---

## Known limitations

- **Asymmetric corners / bevels** in a few original C# presets (SciFi, Classic,
  Steampunk) are flattened to a uniform corner radius in the GDScript model;
  the color palettes remain exact.
- **Ripple-on-click** is reserved but not yet wired (pending a ripple widget).
- **Complex HUD/FX widgets** (minimap, compass, shader effects) ship as themed
  starter scaffolds, and the 15 "core systems" are flagged non-visual — not
  fully implemented widgets.
- Button hover/press **animations run at runtime only** (editor shows the static
  theme), to keep the editor viewport stable.

---

## Troubleshooting

- **Dock doesn't appear after enabling** — reload the project
  (Project → Reload Current Project).
- **"themes do nothing"** — this was the original bug and is fixed. If a subtree
  isn't styling, check the **Output** panel: the applier now prints a clear
  warning naming the node when no `Control` target is found. Ensure the applier
  is a child of a `Control`, or a parent of `Control`(s).
- **A preset looks wrong** — confirm its file exists at the path in
  `beep_theme.gd`'s `_PRESET_SCRIPTS` and that its name matches the
  `@export_enum` list in `theme_applier.gd`.
- **Parse errors after editing** — GDScript is whitespace-sensitive; keep
  indentation consistent (tabs or spaces, not mixed) within each file.
