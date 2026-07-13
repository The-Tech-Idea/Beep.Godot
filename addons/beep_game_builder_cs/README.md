# Beep Game Builder (C#)

Godot 4.7 (.NET 8) game builder addon: file-based skin system, 100+ categorized
components, weather system, scene templates, project generation, and MCP bridge.

## Requirements

- Godot 4.7+ with .NET 8 SDK
- For pure-GDScript projects, use `beep_ui` instead (no C# needed)

## Installation

1. Copy `addons/beep_game_builder_cs/` into your project's `addons/` directory.
2. Project → Project Settings → Plugins → enable "Beep Game Builder (C#)".
3. Build the .NET project (Build button in Godot).

## Editor dock

Single scrollable form — no tabs:
- **Genre & Skin**: cascading dropdowns (Genre → Theme → Palette)
- **Game Identity**: name, version, developer
- **Display**: resolution, FPS, pixel-art, fullscreen
- **Audio**: master/SFX/music sliders
- **Language**: en/es/ja
- **Actions**: Generate (with regen mode), Save, Reload

## Components

Add via Godot's **Add Node** dialog (Ctrl+A) — search "Component". Organized:

| Category | Count | Examples |
|---|---|---|
| **UIComponent** | 47 | Menu, Dialog, HUD, Theme, Settings, Table, Carousel, Tooltip, Toast |
| **GameplayComponent** | 24 | Health, Attack, Movement, Inventory, AI, Projectile, Pickup, Status |
| **ControllerComponent** | 9 | Platformer, TopDown, Shooter, AI controllers, Camera, Navigation |
| **WorldComponent** | 12 | Weather, DayNight, Spawner, Checkpoint, Door, Parallax, WindField |

## File-based skin system

```
catalogs/skins/
├── platformer/
│   ├── genre.json         ← tuning, scenes, default theme
│   ├── geometry.json      ← per-genre geometry + per-node shapes
│   └── themes/
│       └── cartoon/
│           ├── theme.json ← 22 colors + 12 geometry + 6 animation
│           └── *.json     ← 7 palettes (default, warm, cool, pastel, ...)
├── topdown/   (same structure)
├── shooter/   (same structure)
└── puzzle/    (same structure)
```

Adding content = drop a file, zero C# changes:
- New genre: `skins/mygenre/genre.json`
- New theme: `skins/genre/themes/mytheme/theme.json`
- New palette: drop `.json` in a theme folder
- See [Skin system cookbook →](../../docs/SKIN_SYSTEM.md)

## Scene templates

Generated per genre via the dock's Generate button:
- **Shared**: main_menu, pause_menu, settings_menu, game_over, hud
- **Platformer**: level_select, level_results + platformer_main
- **TopDown**: pause_subscreen + topdown_main
- **Shooter**: character_select, level_up_choice, run_results, codex + shooter_main
- **Puzzle**: level_map, pre_level, level_complete, level_failed + puzzle_main

All scenes use `[GlobalClass]` components — no GDScript controllers. Navigation
wired via `MenuComponent` + `NavigationComponent` (exported PackedScene paths).

## Weather system

- 10 weather types with particle effects, fog shaders, cloud overlays
- Day/night cycle with sky gradient + `RenderingServer.SetDefaultClearColor`
- Seasons gating weather + weighted random selection
- Lightning bolts (procedural Line2D) + camera shake integration
- Intensity engine (0..1 cross-fade transitions)
- Wind physics via `WindFieldComponent` (Area2D)
- HUD via `WeatherHUDComponent` (genre/palette/time display)

## Autoloads (auto-registered by generator)

| Autoload | Type | Purpose |
|---|---|---|
| `GameApp` | Node | Session state (level, score, character), Info reference |
| `Settings` | Node (SettingsComponent) | User settings (audio, display, language) → `user://settings.cfg` |
| `Locale` | Node (LocalizationComponent) | TranslationServer wrapper, CSV loading |

GameInfo is a Resource (not an autoload) — loaded by GameApp from `game_info.tres`.

## MCP bridge

Optional AI agent bridge over WebSocket. Auto-enabled on plugin load.
See [MCP README →](mcp/README.md).

## Further reading

- [Architecture →](../../docs/ARCHITECTURE.md)
- [App workflow →](../../docs/APP_WORKFLOW.md)
- [Skinning & theming →](../../docs/SKINNING_THEMING.md)
- [File formats →](../../docs/FILE_FORMATS.md)
- [Enhancement suggestions →](../../docs/ENHANCEMENT_SUGGESTIONS.md)
