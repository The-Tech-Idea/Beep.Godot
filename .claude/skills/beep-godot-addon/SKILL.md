---
name: beep-godot-addon
description: Guidance for working in Beep.Godot — the Godot 4.7 / .NET 8 game-builder addon pair (`beep_game_builder_cs` in C#, `beep_ui` in GDScript). Use whenever adding or editing a component under `addons/beep_game_builder_cs/ecs/`, touching the file-based skin catalog under `catalogs/skins/` (genre.json / geometry.json / theme.json / palettes), working on `ThemePresetComponent` / `SkinCatalog` / `GameApp` / `GameInfo` / `BeepGenreScene`, editing the editor dock, or debugging the MCP bridge. Covers the component category hierarchy, the `[GlobalClass]` registration contract, the per-node theme-override rule, the skin JSON schemas (snake_case), and the Godot 4.7 C# API traps that repeatedly break this build. Trigger terms: `beep_game_builder_cs`, `beep_ui`, `EntityComponent`, `UIComponent`, `GameplayComponent`, `WorldComponent`, `ControllerComponent`, `ThemePresetComponent`, `SkinCatalog`, `GameInfo`, `BeepGenreScene`, `catalogs/skins`, `[GlobalClass]`, "Beep.Godot".
---

# Beep.Godot Addon Guide

Two independent addons live in `addons/`:

| Addon | Language | Depends on |
|---|---|---|
| `beep_ui` | GDScript | nothing — runs in any Godot 4.7+ project, GDScript-only ones included |
| `beep_game_builder_cs` | C# (.NET 8) | requires a **.NET-enabled** Godot project |

They share **zero source files**. `beep_ui` is not a dependency of the C# addon — don't wire one into the other.

Build: `dotnet build`, or **Build → Build Project** in the editor. `Beep.Godot.csproj` uses `Godot.NET.Sdk/4.7.0`, `net8.0`, `<Nullable>enable</Nullable>`. Godot.NET.Sdk auto-includes every `.cs` under the project directory — never add explicit `<Compile>` items.

There is no test suite. Verify changes by opening a scene and running it (F5); check the **Output → C#** panel for build/runtime errors.

## Component contract

`EntityComponent : Node` is the root. Five category bases sit under it — always inherit from a **category**, never from `EntityComponent` directly unless you are adding a new category:

| Base | Lives in | Concrete subclasses | For |
|---|---|---|---|
| `UIComponent` | `ecs/categories/` | ~54 | menus, dialogs, HUD, tables, toasts, theming |
| `GameplayComponent` | `ecs/categories/` | ~44 | health, attack, movement, inventory, projectiles, pickups |
| `WorldComponent` | `ecs/categories/` | ~21 | weather, day/night, spawners, doors, parallax, wind |
| `ControllerComponent` | `ecs/categories/` | ~18 | platformer/topdown/shooter/AI controllers, camera, navigation |
| `EffectComponent : UIComponent` | `ecs/ui/` | ~4 | UI effect decorators |

`EffectComponent` is the one category that is **not** in `ecs/categories/` and **not** a direct child of `EntityComponent` — it extends `UIComponent`.

### Adding a component
1. New `.cs` under `ecs/` (gameplay/world/controller) or `ecs/ui/` (UI).
2. Inherit the right category base.
3. Mark it `[GlobalClass]` and `partial`.
4. **File name must equal the class name, case-sensitive.** Godot's registration is filename-driven; a mismatch fails the build or silently drops the class.
5. Build. Only after a successful build does the class appear in **Add Node** (Ctrl+A).

Components are plain Godot nodes — override `_Ready` / `_Process` / `_PhysicsProcess`. No runtime codegen, no reflection magic. Wire components together with typed `[Export]`s (`PackedScene`, `Texture2D`, `NodePath`), not path strings — see the `godot-4-2d-csharp` skill for the export rules.

## Theming: use per-node overrides, never Theme resources

This is the rule that most often gets violated and silently produces "nothing changed in the editor".

```csharp
// Correct — visible at design time under [Tool]
control.AddThemeColorOverride("font_color", color);
control.AddThemeStyleboxOverride("normal", box);
control.AddThemeFontSizeOverride("font_size", size);
```

Assigning a `Theme` resource works at runtime but is **invisible at editor design time**, which breaks the dock's "apply skin to the open scene" workflow. `[Tool]` + `[GlobalClass]` components must apply visuals via overrides.

`ThemePresetComponent` (+ its `.NodeTheming.cs` partial) is the C# runtime themer; `BeepThemeApplier.gd` is the GDScript equivalent. Both read the same JSON catalog.

## Skin catalog — all JSON keys are snake_case

Adding a genre / theme / palette requires **zero C# changes**. `SkinCatalog` autoloads the tree on plugin load and feeds the dock's cascading dropdowns.

```
catalogs/skins/<genre>/
├── genre.json
├── geometry.json
└── themes/<theme>/
    ├── theme.json
    └── <palette>.json      ← default.json, warm.json, ...
```

**`genre.json`** — `id`, `display_name`, `icon`, `description`, `default_theme`, `default_geometry`, `themes` (string[]), `main_scene`, `scenes` (string[]), `tuning` (object), and optional `nav_wiring` (dict → `GenreDef.NavWiring`, applied to `GameInfo`'s genre scene paths at runtime by `BeepGenreScene`).

`tuning` carries genre gameplay + system flags — e.g. `gravity`, `jump_velocity`, `move_speed`, `enable_weather`, `default_weather`, `enable_day_night`, `enable_seasons`, `enable_save_load`, `max_save_slots`, `autosave_interval_seconds`.

**`theme.json`** — `id`, `display_name`, `category`, `description`, plus four objects:
- `colors` — 22 keys: `surface_*` (primary/hover/pressed/disabled), `text_*` (primary/hover/disabled/on_dark), `accent_*`, `border_*` (normal/hover/focus/bevel_light/bevel_dark), `shadow_color`, `bg_panel`, `bg_canvas`, `semantic_*` (success/danger/warning/info). Values are `#RRGGBBAA` strings.
- `geometry` — `corner_radius`, `border_left|top|right|bottom`, `shadow_size`, `shadow_offset_x|y`, `pad_left|right|top|bottom`, `font_size`.
- `animation` — **singular** — `hover_scale`, `hover_duration`, `press_scale`, `press_duration`, `shadow_lift`, `focus_glow`.
- `textures` — per-state entries (`button_normal`, `button_hover`, …) each with `texture_path`, `margin_*`, `axis_stretch_*`, `draw_center`, `modulate`.

**Palette `.json`** — an HSV transform, not a color list: `id`, `display_name`, `hue_shift`, `saturation_mul`, `value_mul`. `PaletteTintedPreset` decorates a `FileThemePreset` with it.

## Runtime shape

- **`GameApp`** — autoload; session state (level, score, character) + holds `Info`.
- **`GameInfo`** — a `[GlobalClass]` **Resource** at `res://game_info.tres`, *not* an autoload. Static config; genre scene paths get stamped in at runtime.
- **`Settings`** — autoload; audio/display/language → `user://settings.cfg`.
- **`Locale`** — autoload; `LocalizationComponent` wrapping `TranslationServer`, loads `templates/i18n/translations.csv`.
- **`BeepGenreScene`** — set `GenreId` (`platformer` / `topdown` / `shooter` / `puzzle`); it instantiates that genre's main scene as a child, populates `GameApp.Info`, and drives any sibling `ThemePresetComponent`.

The editor dock (`ui/BeepGameBuilderDock.cs`) is a **`VBoxContainer` — a single scrollable form with section headers, not a `TabContainer`**. The "3 tabs" description in the root `README.md` is stale; `addons/beep_game_builder_cs/README.md` is accurate.

## MCP bridge — lives in a separate addon

The bridge is **`addons/godot_mcp/`**, not this addon. It is deliberately project-agnostic (namespace `GodotMcp`, own LICENSE, zero Beep references) and must stay that way — it's portable into any C# Godot project. Do **not** reference Beep types from it.

It is a **client**, not a server: it dials out over WebSocket to a Python FastMCP server running outside Godot. Default URL `ws://127.0.0.1:8789` (`GodotMcpSettings.DefaultUrl`), in `ProjectSettings` under `godot_mcp/bridge/url`, overridable via the `GODOT_MCP_BRIDGE_URL` env var.

The one seam is `beep_game_builder_cs/mcp/BeepMcpCommands.cs`, which registers `beep.*` handlers into the bridge's generic `McpCommandRegistry`. Add new Beep agent tools there — never by referencing Beep from `godot_mcp`.

Use `McpCommandRegistry` (static) rather than the `McpGameAdapter` autoload for anything editor-side: autoloads don't exist in the editor, so adapter-registered commands are runtime-only.

**Never call `ProjectSettings.Save()` from a runtime path.** `GodotMcpRuntime` is a runtime autoload; saving there rewrites `project.godot` under the open editor and triggers a "reload from disk?" prompt on every launch. `GodotMcpSettings` now only saves when `Engine.IsEditorHint()` and something actually changed.

## Godot 4.7 C# API traps

These have each broken this build at least once:

- No `BorderWidthAll` → set `BorderWidthLeft` / `Right` / `Top` / `Bottom` individually.
- No `SetCornerRadiusIndividual()` → set `CornerRadiusTopLeft` / `TopRight` / `BottomRight` / `BottomLeft`.
- No `NotifyThemeChanged()` on `Control`.
- `AntiAliased` is absent on some builds.
- `GD.Randf()` returns `double` → cast to `float`.
- `GodotObject.IsInstanceValid(obj)` — must be fully qualified; the static isn't inherited in C#.
- `GetParent<T>()` **throws** `InvalidCastException` on type mismatch → use `GetParent() as T` and null-check.

Before using any Godot API, confirm it exists in **4.7** specifically.

## Commit convention

`<type>(<scope>): <description>` — e.g. `fix(gameplay+ui): health bars + interaction + badge cleanup`. Types: `feat`, `fix`, `refactor`, `docs`, `chore`. Scope is the affected system (`gameplay`, `ui`, `core`, `weather`, `scenes`), `+`-joined when a change spans several.

## Deeper reference

- `docs/ARCHITECTURE.md` — layer diagram, full directory map, data flow
- `docs/APP_WORKFLOW.md` — generation pipeline, autoloads, scene wiring
- `docs/SKINNING_THEMING.md` — visual preset pipeline
- `docs/FILE_FORMATS.md` — JSON schema reference
- `docs/SKIN_SYSTEM.md` — cookbook for adding genres/themes/palettes
- `addons/beep_game_builder_cs/INDEX.md` — full shipped inventory
