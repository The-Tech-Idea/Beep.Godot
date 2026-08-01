# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Beep.Godot** is a Godot 4.7 (.NET 8) game builder addon **framework**. It ships as two independent addons:

1. **`beep_ui`** (GDScript) — UI theming engine: 22 presets, 11 effects, 114 drag-and-drop widgets. Self-contained; runs in any Godot 4.7+ project.
2. **`beep_game_builder_cs`** (C#) — Game building layer: ~146 categorized components, file-based skin system (10 genres × themes × palettes), scene templates, weather system, day/night cycle, translations, MCP bridge for AI agents.

Both addons live in `addons/` and are enabled via Project Settings → Plugins. `beep_game_builder_cs` requires a **.NET-enabled Godot project**. A **third addon, `addons/godot_mcp/`** (the generic MCP transport/registry), ships alongside them — `beep_game_builder_cs` depends on it one-way for the AI-agent bridge (see *Debug MCP Bridge* below); it is not a user-facing content addon.

## Scope: we build the framework, not the games

**The customer is a Godot developer.** We ship reusable components, wiring, and scaffolding that follow current Godot 4.7 syntax and idiomatic Godot practice. The developer builds their game on top.

**Ours** — must work, generically, in any project:
- Components (`ecs/`) — correct, composable, and silent about nothing.
- The generator, skin catalog, nav wiring, save/load, input map.
- Scene *templates* as working starting points: correct structure, wired components, sensible defaults.
- Godot-idiomatic API surface: `[Export]`s, `[Signal]`s, `[GlobalClass]`, groups, NodePaths.

**Theirs** — do NOT build these, and do not treat their absence as a bug:
- Game content: level layout, terrain/TileSets, entity placement, encounter design, balance.
- Assets: art, audio, fonts, textures. The addon ships none, and there is no sensible default for "rain".
- Genre-specific rules: what a card does, what an upgrade grants, what a vehicle handles like.
- Calling gameplay verbs at the right moment (`AddScore`, `TriggerLevelComplete`) — we provide them and demonstrate one path; the game decides when.

The line: **a component that cannot work is our bug; a template with no content is the developer's canvas.** `player_template` not moving was ours (`MovementComponent` applied nothing). `level_1.tscn` having no terrain is not — an empty level is a starting point, by design.

When a seam is deliberately the developer's, **say so in code** (a doc comment and, where it would otherwise fail silently, a `PushWarning`). Silence is indistinguishable from breakage — that is what made most of the defects in this repo survive.

> Per-genre polish (does the racing HUD show a real speed? does the deck builder deal cards?) is **out of scope**. Genre mains and levels are scaffolding to be replaced.

## Build & Development

### Initial Setup
1. **Godot 4.7+ with .NET 8 SDK** — verify with `dotnet --version` (must be ≥8.0).
2. **Open the project** in Godot editor (the .NET SDK is auto-discovered).
3. **Build → Build Project** (Godot's C# build panel, or `dotnet build` in terminal).

### Common Commands

| Task | Command |
|------|---------|
| Build C# addon | In editor: **Build → Build Project**. Terminal: `dotnet build` |
| Build & run game scene | Open a scene (e.g. `templates/scenes/platformer_main.tscn`) → **F5** or **▶ Play** |
| Reload .NET project | **Build → Build Project** (recompiles changed .cs files; live reloaded by Godot) |
| Check for build errors | **Output → C#** tab in editor, or terminal: `dotnet build \| grep error` |

### Project Structure

```
Beep.Godot/
├── Beep.Godot.csproj         ← C# build config (Godot.NET.Sdk 4.7.0, net8.0)
├── project.godot             ← Godot editor config (both plugins enabled)
├── addons/
│   ├── beep_game_builder_cs/
│   │   ├── core/             ← Generators (Genre, Project, InputMap) + core utilities (state machine, data grid, form builder, GameInfo, …). No separate Scene/Script/Shader/Tween/Particle/Projectile generators — the Genre generator emits those as output, and particle/projectile content ships as .tscn templates.
│   │   ├── ecs/              ← ~60 gameplay components (Health, Movement, AI, Projectile, etc.)
│   │   ├── ecs/ui/           ← ~60 UI components (Menu, Dialog, Table, Toast, Carousel, Accordion, etc.)
│   │   ├── ui/               ← Editor dock (BeepGameBuilderDock.cs)
│   │   ├── mcp/              ← Beep's MCP command layer ONLY (BeepMcpCommands.cs). The bridge itself is addons/godot_mcp/ (see below).
│   │   ├── audio/            ← Bundled audio assets used by atmosphere/weather components
│   │   ├── textures/         ← Bundled texture assets
│   │   ├── catalogs/         ← JSON data (skins/ only; shaders/tweens/particles/projectiles ship as .tscn templates, not JSON catalogs)
│   │   │   └── skins/        ← genre/{platformer,topdown,shooter,puzzle,rpg,survival,racing,citybuilder,strategy,cardgame}/(genre.json, geometry.json, themes/)
│   │   ├── templates/        ← Scene & script templates (auto-copied by generator)
│   │   └── generated/        ← Output folder (populated when user runs generators via dock)
│   ├── godot_mcp/            ← THIRD addon: generic MCP transport/registry (bridge controller, settings, runtime). beep_game_builder_cs depends on it one-way.
│   └── beep_ui/
│       ├── theme/            ← Theme system (BeepPreset, 22 preset_*.gd presets, theme_applier.gd)
│       ├── effects/          ← ui_effect.gd: 11 effect types (Slide, Shake, Pulse, Bob, Flash, Glitch, Rotate, Fade, Typewriter, Bounce, Offset)
│       ├── widgets/          ← 114 widget factory entries (drag-and-drop UI prefabs)
│       └── editor/           ← Theme Studio dock (theme_studio.gd)
└── docs/                     ← Architecture reference (also: ARCHETYPES.md, superpowers/specs/ design notes)
    ├── ARCHITECTURE.md       ← Layer diagram, data flow, 2-addon shape
    ├── APP_WORKFLOW.md       ← Project generation, autoloads, scene wiring
    ├── SKINNING_THEMING.md   ← Visual preset pipeline, theme/palette/geometry flow
    ├── FILE_FORMATS.md       ← JSON schema for skins, shaders, tweens, particles
    ├── SKIN_SYSTEM.md        ← Cookbook: add genres/themes/palettes
    └── ENHANCEMENT_SUGGESTIONS.md
```

## Architecture: Three-Layer Design

### Layer 1: App Layer (C# only)
**Entry point**: `BeepGameBuilderDock` (editor dock).
- **Generators**: `BeepGenreGenerator` coordinates all file creation (projects, scenes, scripts, shaders, particles, projectiles, input maps, autoloads, translations).
- **Autoloads**: `GameApp` (runtime config + session state), `Settings` (audio/display/language → user://settings.cfg), `Locale` (TranslationServer wrapper).
- **GameInfo**: Resource (.tres) holding static game config (name, version, resolution, fps, etc.). Loaded by GameApp; edited via dock or `GameInfoBinder`.

### Layer 2: Skin Layer (cross-addon)
**Entry point**: `SkinCatalog.cs` (C#) or `BeepThemeApplier.gd` (GDScript).
- **SkinCatalog**: Loads JSON from `catalogs/skins/` (genre → theme → palette → geometry).
- **FileThemePreset**: Wraps a theme JSON as an `IThemePreset`.
- **ThemePresetComponent** (C#) / **BeepThemeApplier** (GDScript): Runtime themers—apply color, geometry, texture, animation overrides per node type.
- **Per-node overrides pattern**: Change colors/geometry via `AddThemeColorOverride(control, "font_color", color)` etc. (not Theme resources, which aren't visible in the editor at design time for generated content).

### Layer 3: ECS Components
`EntityComponent : Node` is the root. Category bases extend it — inherit from a **category**, not from `EntityComponent` directly:

| Base | Location | Concrete subclasses |
|---|---|---|
| `UIComponent` | `ecs/categories/` | ~53 |
| `UIScreenComponent` | `ecs/categories/` | 3 — a component that **IS** a screen |
| `GameplayComponent` | `ecs/categories/` | ~41 |
| `WorldComponent` | `ecs/categories/` | ~18 |
| `ControllerComponent` | `ecs/categories/` | 18 |
| `EffectComponent : UIComponent` | `ecs/ui/` | 4 |

~134 concrete components in total (205 files carry `[GlobalClass]` — the remainder are Resources like `GameInfo`, `UISkin`, `ColorPalette`, `GeometryProfile`). Drop them in via Add Node → Beep. No "magic" — pure Godot nodes, no runtime code generation.

## Key Patterns & Rules

### [GlobalClass] Components
- Every C# component class must have `[GlobalClass]` to appear in the Godot editor's "Add Node" dialog.
- Class name must **exactly match file name** (case-sensitive). Mismatch → compilation fails.
- Requires a successful build for Godot to register the class in its type registry.

### Theme Overrides (Not Theme Resources)
Per the user's codebase rules:
- **Always** use per-node `AddThemeColorOverride()` / `AddThemeStyleboxOverride()` / `AddThemeFontSizeOverride()` for editor-visible changes.
- **Avoid** creating Theme resources as the source of truth. (They work at runtime but are invisible during editor design time for generated content.)
- **Why**: Generated scenes need to be themeable in the editor via the dock's "Apply to all ThemePresetComponents" action.

### Godot 4.7 C# API Traps
Verify against Godot 4.7 before trusting any entry here — this list has been wrong before (see `GD.Randf()`).
- No `BorderWidthAll` → use individual `BorderWidthLeft`, `BorderWidthRight`, etc.
- No `SetCornerRadiusIndividual()` → use properties `CornerRadiusTopLeft`, etc.
- No `NotifyThemeChanged()` on Control → use `ThemeChanged?.Invoke()` if needed.
- `GD.Randf()` returns `float` — **no cast needed**. (This list previously claimed `double`. It's false: `BeepServiceLocator.cs` does `float angle = GD.Randf() * Mathf.Tau;` uncast and builds clean, which a `double` could not. The belief produced several redundant `(float)GD.Randf()` casts.)
- `GodotObject.IsInstanceValid(obj)` — use full qualified name (static method, not inherited).
- `GetParent<T>()` throws InvalidCastException if wrong type → use `GetParent() as T` for safe cast — **but see "Never fail silently" below**: the null branch must warn.
- Throwing `GetNode<T>(path)` defeats a following `if (x != null)` guard — it throws first, so the guard is unreachable. Use `GetNodeOrNull<T>`.
- `CallDeferred(MethodName.X)` relies on the generator registering `X`; `Callable.From(X).CallDeferred()` doesn't. Prefer the latter.

### `[Export]` properties are PascalCase in `.tscn` — ALWAYS
Godot registers a C# export under its **exact PascalCase name**. The source generator emits:
```csharp
public new static readonly StringName @TitleLabelPath = "TitleLabelPath";
if (name == PropertyName.@TitleLabelPath) { this.@TitleLabelPath = ...; return true; }
```
A scene line written GDScript-style (`title_label_path = ...`) matches nothing, `SetGodotClassPropertyValue` returns false, and **the assignment is silently discarded** — the scene loads, the node runs on defaults, nothing is logged.

```gdscript
MaxHealth = 100.0                  # ✅ C# [Export] — PascalCase
custom_minimum_size = Vector2(...) # ✅ Godot built-in — snake_case, correct
title_label_path = NodePath("...") # ❌ C# [Export] written snake_case — DROPPED
```
This cost 67 dead assignments across 33 scenes (every `GameInfoBinder`, `AnimatedMenuComponent`, `SceneTransitionComponent` ran unconfigured). `validate_scenes.sh` now enforces it — **run it after touching any `.tscn`**.

To inspect what Godot actually registers:
`dotnet build -p:EmitCompilerGeneratedFiles=true -p:CompilerGeneratedFilesOutputPath=<dir OUTSIDE the project>`
(Emitting inside the project makes the SDK compile the generated files as sources → thousands of duplicate-definition errors.)

### Animating a Control: use the offset transform layer
Never tween `position` / `scale` / `rotation` on a Control inside a Container — the container re-sorts its children every layout pass and overwrites the tween. Godot 4.7's `offset_transform_*` ([GH-87081](https://github.com/godotengine/godot/pull/87081), landed in 4.7 dev 3) is a render-only transform containers don't touch.

```csharp
ctrl.OffsetTransformEnabled = true;
tween.TweenProperty(ctrl, "offset_transform_position", Vector2.Zero, dur);  // ✅
tween.TweenProperty(ctrl, "position", endPos, dur);                          // ❌ container wins
```
Offsets are relative to the laid-out position, so neutral is always `Vector2.Zero` / `Vector2.One` — there is nothing to capture or restore. Used by `theme_applier.gd`, `ui_effect.gd`, `AnimatedMenuComponent`, `ThemePresetComponent`.

**Scaling or rotating? Set `pivot_offset` first.** `offset_transform_scale` and `offset_transform_rotation` pivot around the Control's `pivot_offset`, which **defaults to the top-left corner `(0,0)`** — so a "breathing" pulse or a spin grows/turns toward the corner, not in place. Centre it with `ctrl.PivotOffset = ctrl.Size / 2f` before the tween (re-set on resize). `PulseComponent`, `SquashAndStretchComponent`, `ComboCounterComponent`, `FlipCardComponent`, and `RippleComponent` do this. **Exception:** a directional collapse (e.g. `AccordionComponent` scaling `(1,1)→(1,0)`) deliberately keeps the default top pivot so content rolls up toward its header — centre-pivoting it would be wrong. Match the pivot to where the animation should originate.

### Screen typography: four Label variations, not one flat size
`ThemePresetComponent` used to stamp a single `Fs` on every node type, so a screen title rendered at exactly the size of a body label and a button — 21 `TitleLabel`s across the templates carried **zero** font-size overrides. It now registers four Label type variations, sized off the theme's own `font_size`:

| Variation | Size | Color | Use for |
|---|---|---|---|
| `BeepTitle` | `Fs × 1.9` | TextPrimary | screen titles, result banners |
| `BeepSubtitle` | `Fs × 1.35` | TextPrimary | section headings inside a panel |
| `BeepValue` | `Fs × 1.25` | AccentPrimary | the number in a caption/value stat row |
| `BeepCaption` | `Fs × 0.85` | TextDisabled | stat labels, hints, version strings |

```gdscript
theme_type_variation = &"BeepTitle"   # ✅ the intended way; validate_scenes.sh checks the name
```
Applied both on the generated `Theme` and as per-node overrides (editor visibility). A Label with **no** variation and no matching name is left alone. There is a compatibility fallback on node names (`TitleLabel`/`*Title` → title, `*Caption`, `*Value`, `VersionLabel`, `HintLabel`) purely so a project generated before the variations existed still gets a hierarchy from a plain rebuild — new scenes should set the variation. A `label_settings` resource beats both (Godot's precedence), so a Label with LabelSettings ignores all of this.

`ThemePageBackground` likewise repaints an **opaque** `ColorRect` named `Background` from `bg_canvas`. Translucent ones are dims over live gameplay (`Dim`, and pause_subscreen's 0.92-alpha `Background`) and are deliberately left alone.

### Textures are baked, not drawn — and `NinePatchRect` is not a StyleBox
Every `theme.json` declares a `textures{}` block. Those PNGs are **generated** from the theme's own
colors + geometry by `core/BeepTextureBaker.cs` (dock → *Bake textures*, or `beep.bake_textures`), which
writes the exact paths the JSON already names. This whole pipeline shipped dead: all 50 themes declared
5 slots and **none of the 200 files existed**, so every texture toggle in the inspector silently did
nothing. `SkinCatalog` now warns per missing path and `validate_scenes.sh` fails on one.

- Skinning a Button/Panel/Window/input → **`StyleBoxTexture`** (the `textures{}` path). A
  **`NinePatchRect` is a Node and can never be a theme StyleBox.**
- Decorative frames a Theme can't reach (HUD banners, portrait borders, callouts) → **`NinePatchFrameComponent`**.
- A baked slot has **no drop shadow** — `StyleBoxTexture` has no `shadow_size`, and faking one would
  change every widget's metrics when textures are toggled. Depth comes from a gradient.
- Two slots may share one file (shipped themes aim `button_disabled` at `button_normal.png` + `modulate`);
  the first baked wins.

`templates/scenes/theme_gallery.tscn` shows every widget/state/variation with a **Textures** toggle —
if a widget changes size when textures come on, the baked margins disagree with the slot's `theme.json`.

### Starting a new screen: generate it
`BeepScreenGenerator` (dock → *New screen*, or `beep.new_screen`) stamps a screen with the conventions
already correct — opaque themed `Background`, `BeepTitle` header + accent rule + 120×44 back button,
`ThemePresetComponent` on the content Control, PascalCase exports, and a script wiring by name. Prefer it
over copying a neighbouring `.tscn`.

### Resolve scene controls by NAME, not by path
`SceneWiring.ConnectButton("BackButton", …)` / `this.Find<T>("Tabs")` — not `ConnectPressed("Margin/VBox/Header/BackButton", …)`. A path hard-codes the layout, so inserting one wrapper container silently kills every button beneath it; that is exactly how the save/load menus broke when the templates gained a `Margin` while already-generated projects kept the old tree. Names survive a restyle.

The cost is that a name must identify one node: `validate_scenes.sh` fails when a scene has several Buttons sharing a name **that its root script resolves scene-wide**. Repeats scoped to a row are fine and are not flagged (`load_game_menu` has one `SlotButton` per slot, resolved via `container.FindChild`). This check caught four `SelectButton`s in `shooter/character_select` and two `Level1Button`s in `platformer/level_select` — real mis-wirings, since a scene-wide name lookup returns whichever comes first in the tree.

### A kit widget inherits the Godot class it imitates

Eight kit widgets used to be `KitControl` (a bare `Control`) drawn to look like a Button, a slider,
a toggle. Godot **accepts** a Control-derived script on a `Button` node without complaint — the
script attaches and runs — so this looked fine and rendered fine. What broke was C#: the managed
object is a `Control` standing in for a `Button`, so `GetNode<Button>` fails, `is Button` is false,
`Pressed` is unreachable, and you get CS1503 conversion errors in a project that never touched the
kit's internals.

| widget | is a | widget | is a |
|---|---|---|---|
| `KitButton`, `KitIconButton`, `KitPushButton` | `Button` | `KitMeter` | `ProgressBar` |
| `KitToggle`, `KitCheckButton` | `CheckButton` | `KitPanel` | `Panel` |
| `KitSlider`, `KitKnob`, `KitSliderBar` | `HSlider` | `KitTabStrip` | `TabBar` |
| `KitStarRating` | `Range` | `KitTabPanel` | `TabContainer` |

Two rules follow, and both have bitten:

1. **`tools/check_script_node_types.py` requires the node's declared `type=` to EQUAL the script's
   Godot base.** "Descends from" is not enough — `Button` descends from `Control`, which is exactly
   how the mismatch hides. Run by `validate_scenes.sh`.
2. **A property that is now a Godot BUILT-IN takes a snake_case key in `.tscn`.** `Text`, `Value`,
   `Disabled`, `Icon` were C# `[Export]`s; after conversion the PascalCase line matches nothing and
   is **silently discarded**. That blanked three button labels in `kit_gallery` — the same trap as
   the `[Export]` rule above, arriving from the opposite direction.

Widgets with no real Godot equivalent stay `KitControl`: `KitChip` (a drawn label — making it a
Button would invent interactivity and start eating clicks), `KitTree` (a tier **graph**, not
Godot's list `Tree`), `KitSpinner`, `KitPager`, `KitCollapsiblePanel`, and the slot/frame/card
family.

### HUD and UI: a CanvasLayer, anchors, and the project's stretch mode

Three things together, and missing any one makes a HUD drift:

1. **A `CanvasLayer`**, so the HUD is camera-independent and does not scroll with the world.
2. **Anchors** on every Control that a Container does not position — `check_control_layout.py`
   enforces it, and Container children are exempt because `layout_mode` is an editor hint, not a
   runtime requirement.
3. **`display/window/stretch/mode = "canvas_items"` + `aspect = "expand"`** in `project.godot`
   ([Godot: multiple resolutions](https://docs.godotengine.org/en/stable/tutorials/rendering/multiple_resolutions.html)).

`BeepProjectDefaults` writes (3) into every generated project. **This repo's own `project.godot`
did not have it**, so stretch defaulted to `disabled`, Controls laid out in raw pixels, and an
anchored HUD still drifted with the window — every template scene run from here behaved
differently from the same scene in a generated project. Three attempts to fix that by editing
scenes all failed, because the setting was never in a scene.

### Never fail silently
The dominant defect class in this repo, by a wide margin. A component resolves a collaborator, the cast fails, it early-returns, and **nothing says anything** — so it looks fine for months.

```csharp
_target = GetParent() as Control;   // null against a CanvasLayer root → component does nothing
if (_target == null) return;        // ❌ silent
```
Every such branch must `GD.PushWarning` naming what it got, what it needed, and what to do. This pattern alone hid: unthemed pause/settings/game-over in all 10 genres, a menu animation that never played, an inert dialog template, and a HUD that never bound.

Corollaries:
- A `null` export that disables a feature (`ProjectileScene`, `SpawnScene`, `NoiseTexture`) must warn or have a working default. Prefer a default — a shipped feature shouldn't need an asset nobody supplies.
- An unbound `sampler2D` samples **black**, silently. Always bind something.
- Parse/IO failures must return `bool`, not a default-constructed object (a corrupt save loaded as success and overwrote the good file).

### Public API must be idempotent
`ApplyTheme()` is public and every setter calls it, so one scene load ran it 5× — and it wasn't idempotent (`AddChild(new RippleComponent)` per pass, `+=` handlers never removed). Anything callable more than once must be safe to. Guard with a meta flag on the target node (freed with it, no bookkeeping) and bail from setters when the value is unchanged.

### Scene layer conventions
- `ThemePresetComponent` themes `GetParent()`'s subtree → parent it to the **content Control** (`Center` / `Panel` / `Margin`), never the `CanvasLayer` root or the `Dim` overlay.
- `AnimatedMenuComponent` animates `GetParent()`'s children → parent it to the **Container** holding the items.
- Screens that sit over a running game are **overlays**: instance them and `QueueFree()` to close. `ChangeScene` frees the run behind them. Use `SceneNav.CloseOrReturn` (handles both overlay and current-scene cases) and `SettingsOverlay` / `GenreScreenComponent` to open.
- Genre screens resolve through `GameInfo.GenreScenePaths` (open key→path registry from `nav_wiring`); the named `*Path` properties cover only platformer/shooter/puzzle.

### File-Based Skin System
**Zero C# changes needed to add content.** All JSON keys are **snake_case**:
- New genre: `catalogs/skins/<genre>/genre.json` — `id`, `display_name`, `icon`, `description`, `default_theme`, `default_geometry`, `themes[]`, `main_scene`, `scenes[]`, `tuning{}`, optional `nav_wiring{}`.
- New theme: `catalogs/skins/<genre>/themes/<theme>/theme.json` — `id`, `display_name`, `category`, `description`, `colors{}` (22 `#RRGGBBAA` keys), `geometry{}`, `animation{}` (**singular**, 6 keys), `textures{}`.
- New palette: drop a `.json` in a theme folder — an HSV transform, not a color list: `id`, `display_name`, `hue_shift`, `saturation_mul`, `value_mul`.

All auto-loaded by `SkinCatalog` on plugin load and exposed in the dock's cascading dropdowns. `nav_wiring` becomes `GenreDef.NavWiring` and is applied to `GameInfo`'s genre scene paths at runtime by `BeepGenreScene`.

Components have no "magic" — just overrides of `_Ready()`, `_Process()`, `_PhysicsProcess()`. Wire them together with typed `[Export]`s (`PackedScene`, `Texture2D`, `NodePath`) rather than path strings.

### Editor Dock (BeepGameBuilderDock)
`addons/beep_game_builder_cs/ui/BeepGameBuilderDock.cs` — a **`VBoxContainer`: one scrollable form with section headers, not a `TabContainer`**. Sections: Genre & Skin (cascading dropdowns), Game Identity, Display, Audio, Language, Actions (Generate / Save / Reload).

> The root `README.md` describes a 3-tab dock. That is stale — `addons/beep_game_builder_cs/README.md` is accurate.

## Documentation

**Start here:**
- **[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)** — Layer diagram, full directory map, data flow.
- **[docs/APP_WORKFLOW.md](docs/APP_WORKFLOW.md)** — Project generation pipeline, autoload startup sequence.
- **[docs/SKINNING_THEMING.md](docs/SKINNING_THEMING.md)** — Visual preset pipeline, per-node-type theming.
- **[docs/FILE_FORMATS.md](docs/FILE_FORMATS.md)** — JSON schema for all data files (skins, shaders, tweens, particles, projectiles).
- **[docs/SKIN_SYSTEM.md](docs/SKIN_SYSTEM.md)** — Cookbook: add a genre, add a theme, add a palette.
- **[docs/ARCHETYPES.md](docs/ARCHETYPES.md)** — Read *before composing an entity*: per-archetype node type + required/optional/forbidden components, and why the wrong pairing fails silently. `validate_scenes.sh` enforces the parent-type half.

**Component inventory:**
- **[addons/beep_game_builder_cs/INDEX.md](addons/beep_game_builder_cs/INDEX.md)** — Full shipped inventory: components, shared + genre-specific UI scenes, shaders, particles.
- **[addons/beep_ui/README.md](addons/beep_ui/README.md)** — Beep UI (GDScript) theming engine details.

## Common Tasks

### Add a New Component
1. Create `.cs` file in `addons/beep_game_builder_cs/ecs/` (gameplay/world/controller) or `ecs/ui/` (UI).
2. Inherit from a **category** base — `UIComponent`, `GameplayComponent`, `WorldComponent`, `ControllerComponent` (all in `ecs/categories/`), or `EffectComponent` (in `ecs/ui/`). Not `EntityComponent` directly.
3. Mark the class `[GlobalClass]` and `partial`.
4. File name must match class name exactly (case-sensitive) — registration is filename-driven.
5. Build → Build Project.
6. Component appears in editor's Add Node dialog.

### Customize Theme/Palette
1. Open dock → **Theme** tab.
2. Select genre, theme, palette from cascading dropdowns.
3. Click **Apply to all ThemePresetComponents in open scene**.
4. Or: Manually edit `.json` in `catalogs/skins/genre/themes/` and reload project (File → Reload Project).

### Generate a New Project (Scaffold a Game)
1. Open dock → **App** tab (or **Genres** tab if multi-tab dock).
2. Fill GameInfo fields (game name, resolution, fps, etc.).
3. Select genre (Platformer, TopDown, Shooter, Puzzle).
4. Click **▶ Generate Project**.
5. Generator creates: folders, autoloads, input map, GameInfo.tres, UI scene templates, genre scene, translations.

### Debug MCP Bridge (AI Agent Communication)
The MCP bridge is a **third, separate addon**: `addons/godot_mcp/` (the transport/registry), which `beep_game_builder_cs` depends on one-way. Beep never depends *back* on it.
- **`addons/godot_mcp/`** — the generic bridge: `GodotMcpBridgeController.cs` (setup/lifecycle), `GodotMcpSettings.cs` (URL + `DefaultUrl`/`Initialize`), `GodotMcpPlugin.cs`, `GodotMcpRuntime.cs`, and the generic `McpCommandRegistry`. Security gates: `allow_editor_writes` / `allow_runtime_writes`.
- **`addons/beep_game_builder_cs/mcp/BeepMcpCommands.cs`** (+ the `BeepMcpSceneCommands.cs` partial) — Beep's command layer only. Its `Register()` registers `beep.*` handlers (read: `list_genres`, `catalog`, `list_components`, `get_game_info`…; editor writes: `add_component`, `apply_skin`, `generate_project`; runtime writes, gated: `save_game`, `add_score`, `set_weather`…) into the `godot_mcp` registry.
  **Scene management** lives in the partial: read `list_scenes`, `open_scene`, `inspect_scene`, `get_node_property`, `screenshot`; editor-write `set_node_property`, `add_node`, `remove_node`, `save_scene`, `bake_textures`, `new_screen`. Reuse `McpTreeSerializer` / `McpJson` rather than re-serialising. Two guards worth keeping: `set_node_property` **refuses** a snake_case name that matches a C# `[Export]` (Godot would silently drop it), and `remove_node` refuses while another node's `NodePath` export still points at the target.
- Auto-enables on plugin load. Default URL `ws://127.0.0.1:8789` (`GodotMcpSettings.DefaultUrl`), stored in `ProjectSettings` under `godot_mcp/bridge/url`, overridable via the `GODOT_MCP_BRIDGE_URL` env var. `GodotMcpSettings.Initialize` **force-writes** it on load so stale cached ports get corrected — a manual `ProjectSettings` edit will be overwritten. Logs go to Godot's Output panel.

> ⚠️ **The bridge is a WebSocket _client_ and nothing serves it yet.** `McpWebSocketClient` dials **out** to `ws://127.0.0.1:8789/{role}?token=…`; there is no server in this repo, so every bridge method is currently unreachable from Claude. The planned server and the phased plan to fix it live in **[docs/mcp/MCP_ROADMAP.md](docs/mcp/MCP_ROADMAP.md)** (master tracker) — start at Phase 0.

## Testing

Two automated gates, then your eyes. Run both after any change:

| Gate | Command | Catches |
|---|---|---|
| Build | `dotnet build` | compile errors. ~148 nullable warnings are pre-existing noise. |
| Scene validator | `cd addons/beep_game_builder_cs/templates/scenes && ./validate_scenes.sh` | undeclared Ext/SubResources, bad parent paths, duplicate sibling names, missing scripts, atmosphere placement, `[Export]` names that Godot would silently drop, **script/node type mismatches**, and **raw `DrawString` in a kit widget** |
| Genre tuning | `godot --headless --path . tools/genre_shapes/tuning_probe.tscn` | every `tuning` key in a `genre.json` reaches `GameInfo` through `BeepGenreGenerator.ApplyTuning`. 80 genre/key pairs. A key added to the JSON and forgotten in ApplyTuning cannot pass |
| Style system | `godot --headless --path . tools/genre_shapes/style_sweep_probe.tscn` | all 50 themes resolve DISTINCT styles; every register, text treatment and gloss construction is used by some theme; no `kit` block key or value is misspelt |
| Rendered gates | `measure_material` · `measure_shadow` · `measure_pixel` · `measure_gloss` · `measure_edgerun` · `verify_greyscale` | that an axis reaches the PIXELS. Each renders paired images and differences them — a metric read off one render is how four wrong answers got made. **Run windowed:** `--headless` uses the dummy driver, draws nothing, and hangs at `FramePostDraw` |

`validate_scenes.sh`'s header is the rule: **every check exists because it caught a real bug.** Add one when you fix a class of defect — and make it fail before you trust it (a check you've only seen pass is not evidence).

**Neither gate runs the game.** Compile-clean + validator-PASS says the code loads, not that it works. Anything touching input, physics, shaders, or scene structure needs a real editor pass:
1. Generate a project → open `scenes/ui/main_menu.tscn` → F5.
2. New Game → play → Save → restart → Load.
3. Watch **Output → C#** — most of this framework's failures now announce themselves as warnings rather than doing nothing.

> **A ✅ in this repo's docs has historically meant "I wrote it", not "I ran it."** `SESSION_SUMMARY.md` claimed "Save/load flow tested end-to-end" for a system where `Save()` was a hard no-op — nothing called `NewGame()`, so state was permanently null. `WEATHER_SYSTEM_INTEGRATION_REPORT.md` cited `puzzle/genre.json` as proof of `enable_weather: true`; puzzle has `false`. Both are corrected now, and both are why: **do not claim tested unless you ran it. Say "compile-verified" and mean it.**

## Commit Conventions

Recent commits follow a pattern: `<type>(<scope>): <description>`. Examples:
- `fix(gameplay+ui): health bars + interaction + badge cleanup`
- `fix(ui): table + dialog cleanup — final audit batch`
- `feat(scenes): per-scene C# navigation scripts`

**Type**: `feat`, `fix`, `refactor`, `docs`, `chore`, `style`, `test`.
**Scope**: affected system (e.g., `gameplay`, `ui`, `weather`, `core`).
**Description**: high-level change (bug fixes bundled by component; component names separated by `+`).

## Metadata

- **Godot Version**: 4.7+ with .NET 8 SDK (`Godot.NET.Sdk/4.7.0`, `net8.0`, nullable enabled)
- **Language**: C# (.NET 8) + GDScript
- **Framework**: component-composition pattern over Godot nodes, with a file-based skin catalog
