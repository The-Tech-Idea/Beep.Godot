# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Beep.Godot** is a production-ready Godot 4.7 (.NET 8) game builder addon framework. It ships as two independent addons:

1. **`beep_ui`** (GDScript) — UI theming engine: 22 presets, 11 effects, 84+ drag-and-drop widgets. Self-contained; runs in any Godot 4.7+ project.
2. **`beep_game_builder_cs`** (C#) — Game building layer: ~146 categorized components, file-based skin system (4 genres × 5 themes × palettes), scene templates, weather system, day/night cycle, translations, MCP bridge for AI agents.

Both addons live in `addons/` and are enabled via Project Settings → Plugins. `beep_game_builder_cs` requires a **.NET-enabled Godot project**.

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
│   │   ├── core/             ← Generators (Project, Scene, Script, Shader, Tween, Particle, Projectile, InputMap)
│   │   ├── ecs/              ← ~60 gameplay components (Health, Movement, AI, Projectile, etc.)
│   │   ├── ecs/ui/           ← ~60 UI components (Menu, Dialog, Table, Toast, Carousel, Accordion, etc.)
│   │   ├── ui/               ← Editor dock (BeepGameBuilderDock.cs)
│   │   ├── mcp/              ← MCP bridge for AI agents (WebSocket, auto-enabled)
│   │   ├── catalogs/         ← JSON data (skins, shaders, tweens, particles, projectiles)
│   │   │   └── skins/        ← genre/{platformer,topdown,shooter,puzzle}/(genre.json, geometry.json, themes/)
│   │   ├── templates/        ← Scene & script templates (auto-copied by generator)
│   │   └── generated/        ← Output folder (populated when user runs generators via dock)
│   └── beep_ui/
│       ├── theme/            ← Theme system (BeepPreset, 22 preset_*.gd presets, theme_applier.gd)
│       ├── effects/          ← 11 effect types (Fade, Scale, Tint, Blur, Shake, Glow, etc.)
│       ├── widgets/          ← 84 widget factory entries (drag-and-drop UI prefabs)
│       └── editor/           ← Theme Studio dock (theme_studio.gd)
└── docs/                     ← Architecture reference
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
| `UIComponent` | `ecs/categories/` | ~54 |
| `GameplayComponent` | `ecs/categories/` | ~44 |
| `WorldComponent` | `ecs/categories/` | ~21 |
| `ControllerComponent` | `ecs/categories/` | ~18 |
| `EffectComponent : UIComponent` | `ecs/ui/` | ~4 |

~146 concrete components in total (199 files carry `[GlobalClass]` — the remainder are Resources like `GameInfo`, `UISkin`, `ColorPalette`, `GeometryProfile`). Drop them in via Add Node → Beep. No "magic" — pure Godot nodes, no runtime code generation.

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
(From project memory; verify against Godot 4.7 docs before use.)
- No `BorderWidthAll` → use individual `BorderWidthLeft`, `BorderWidthRight`, etc.
- No `SetCornerRadiusIndividual()` → use properties `CornerRadiusTopLeft`, etc.
- No `NotifyThemeChanged()` on Control → use `ThemeChanged?.Invoke()` if needed.
- `GD.Randf()` returns `float` — **no cast needed**. (This list previously claimed `double`. It's wrong: `BeepServiceLocator.cs` does `float angle = GD.Randf() * Mathf.Tau;` uncast and the project builds clean, which a `double` could not. The belief produced several redundant `(float)GD.Randf()` casts.)
- `GodotObject.IsInstanceValid(obj)` — use full qualified name (static method, not inherited).
- `GetParent<T>()` throws InvalidCastException if wrong type → use `GetParent() as T` for safe cast.

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
- Auto-enables on plugin load. Default URL `ws://127.0.0.1:8789` (`GodotMcpSettings.DefaultUrl`).
- Stored in `ProjectSettings` under `godot_mcp/bridge/url`; overridable via the `GODOT_MCP_BRIDGE_URL` env var.
- `GodotMcpSettings.Initialize` **force-writes** the URL on load so stale cached ports get corrected — a manual `ProjectSettings` edit will be overwritten.
- Setup lives in `mcp/GodotMcpBridgeController.cs`. Logs go to Godot's Output panel.

## Testing

No formal test suite. Verify changes by opening a scene and running it (F5), and watch the **Output → C#** panel for build/runtime errors.

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
