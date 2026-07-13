# Beep.Godot

A Godot 4.7 game builder addon by **The Tech Idea** for building complete 2D games fast —
file-based skin system (genre → theme → palette → geometry), 100+ categorized
components (UI, Gameplay, Controller, World), weather system with day/night cycle,
scene templates for 4 genres, and an MCP bridge for AI agents.

The project ships **two independent addons**, split by language and purpose:

| Addon | Language | Use it for |
|---|---|---|
| **`beep_ui`** | GDScript | UI theming (22 presets, 11 effects, 84+ widgets). Runs in any Godot 4.7+ project. |
| **`beep_game_builder_cs`** | C# (.NET 8) | Game building: file-based skin system, 100+ categorized ECS components, weather system, day/night cycle, scene templates, project generation, MCP bridge. Requires a **.NET** Godot project. |

You can use either or both. `beep_ui` is self-contained and does **not** depend on
the C# addon.

## Quick start

1. Copy the addon folder(s) you want into your project's `addons/` directory.
   - `addons/beep_ui` → `addons/beep_ui`
   - `addons/beep_game_builder_cs` → `addons/beep_game_builder_cs` (C# / .NET only)
2. Open the project in **Godot 4.7+** with .NET support.
3. **Project → Project Settings → Plugins** → enable the addon(s).
4. Pick a genre + theme **at design time**: drop a `BeepGenreScene` component into your
   scene root, set `GenreId = "platformer"` (or topdown / shooter / puzzle) in the
   inspector. The addon's already-wired scene templates load automatically at runtime —
   no buttons to click, no generators to run.

**Add components** via Godot's native **Add Node** dialog (Ctrl+A) — search "Component".
Components are categorized:
- `EntityComponent` → `UIComponent` (47 components)
- `EntityComponent` → `GameplayComponent` (24 components)
- `EntityComponent` → `ControllerComponent` (9 components)
- `EntityComponent` → `WorldComponent` (12 components)

## Editor dock

The `beep_game_builder_cs` dock has **3 tabs**:

1. **App** — GameApp status (autoload probe) + GameInfo quick-edit grid (game name,
   version, genre/theme/palette/geometry, scene paths, tuning, display). Save to
   `game_info.tres` + apply live to all `ThemePresetComponent`s in the open scene.
2. **Theme** — Cascading genre → theme → palette → geometry dropdowns driven from
   the file-based skin catalog. Background mode toggle (stretch/tile/center).
   Click **Apply to All Components** to re-theme the open scene.
3. **Settings** — Project-level config: resolution / FPS / pixel-art / fullscreen /
   main scene / autoload status. Writes to `ProjectSettings`.

## File-based skin system

All theme/palette/geometry/genre data lives in JSON files under `catalogs/skins/`:

```
skins/
├── platformer/
│   ├── genre.json           ← tuning, scene list, default theme
│   ├── geometry.json        ← per-genre geometry + per-node shapes
│   └── themes/
│       └── cartoon/
│           ├── theme.json   ← 22 colors + geometry + animation
│           ├── default.json ← palette
│           └── warm.json    ← palette
├── topdown/   (same structure)
├── shooter/   (same structure)
└── puzzle/    (same structure)
```

Add a genre = drop a folder. Add a theme = drop a `theme.json`. Add a palette = drop a `.json`.
All autoloaded by `SkinCatalog` — zero C# changes needed.

## Weather system

`WeatherSystemComponent` + `WindFieldComponent` + `WeatherHUDComponent` provide:
- 10 weather types (Clear, Cloudy, Rain, Snow, Storm, Fog, Sandstorm, Hail, LeafFall, Heatwave)
- Day/night cycle with 4-keyframe sky gradient
- Seasons gating available weather
- Procedural fog/cloud/shadow shaders
- Lightning bolts (procedural Line2D) + camera shake
- Intensity engine (0..1 cross-fade transitions)
- Wind physics (Area2D wind field for RigidBody2D + CharacterBody2D)

## Documentation

- **[Architecture →](docs/ARCHITECTURE.md)** — master map of both addons
- **[App workflow →](docs/APP_WORKFLOW.md)** — project generation, autoloads, scene wiring
- **[Skinning & theming →](docs/SKINNING_THEMING.md)** — visual pipeline deep-dive
- **[Skin system cookbook →](docs/SKIN_SYSTEM.md)** — how to add genres/themes/palettes
- **[File formats →](docs/FILE_FORMATS.md)** — JSON schema reference
- **[Beep UI (GDScript) →](addons/beep_ui/README.md)** — theming engine, presets, widgets
- **[Beep Game Builder (C#) →](addons/beep_game_builder_cs/README.md)** — components, dock, skin system

## Why two addons?

`beep_ui` was extracted from the original C# addon so the UI/theming layer can run
in any Godot project — including pure-GDScript ones. The C# addon builds on top:
it uses the GDScript theme presets for its generated UI scenes and adds the full
game-building layer (ECS components, weather, scene templates, project generation).

## Requirements

- **Godot 4.7+** with **.NET 8** SDK (for `beep_game_builder_cs`)
- Godot 4.7+ without .NET (for `beep_ui` only)
