# Beep Game Builder (C#)

Godot 4.7 (.NET 8) game builder addon: file-based skin system, 100+ categorized
components, weather system, scene templates, project generation, and MCP bridge.

## Themed UI textures (bring your own art)

Every genre theme is **texture-ready**: its `theme.json` declares nine-patch slots
(`button_normal`, `button_hover`, `button_pressed`, `button_disabled`, `panel`) pointing at
`res://addons/beep_game_builder_cs/textures/<genre>/<theme>/`. The addon ships **no** UI art — until
you drop files in, each slot falls back **cleanly** to the procedural `StyleBoxFlat` (no error).

To skin a genre with your own art (any source — Kenney, itch.io, your own):
1. Drop `button_normal.png` / `button_pressed.png` / `panel.png` into
   `textures/<genre>/<theme>/` (the folder the theme already points at).
2. In `catalogs/skins/<genre>/themes/<theme>/theme.json` → `textures{}`, set each slot's
   `margin_*` to your art's corner size (9-patch), and `axis_stretch_*` (`0` stretch / `1` tile).
3. Set `Filter = Nearest` in the Import dock for pixel art.

A helper script that bulk-populates the slots from a Kenney-structured "UI assets" pack lives at
`plans/ui-asset-integration/import_kenney.py` — point it at your local pack and run it (it copies
into your project; it does not ship art with the addon). See `docs/FILE_FORMATS.md:230` for the full
slot schema.

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
- **Shared**: main_menu (also the pause overlay), settings_menu, game_over, hud
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

## AI agent commands (optional)

The MCP bridge itself lives in the separate **`godot_mcp`** addon — see
[its README →](../godot_mcp/README.md). This addon depends on it for nothing; if
`godot_mcp` isn't enabled, the registration below is an inert dictionary write.

Enable both and `mcp/BeepMcpCommands.cs` contributes these to an agent
(invoke via the bridge's `game.command`; `status.get` lists them):

| Command | Where | Does |
|---|---|---|
| `beep.list_genres` | editor + runtime | every genre in the skin catalog |
| `beep.list_themes` | editor + runtime | themes in a genre |
| `beep.list_palettes` | editor + runtime | palettes in a genre+theme |
| `beep.catalog` | editor + runtime | the whole genre→theme→palette tree in one call |
| `beep.list_components` | editor + runtime | all 146 components, grouped by category (`category`/`search` filters) |
| `beep.component_info` | editor + runtime | a component's exported properties (with types, enum values, defaults) + signals |
| `beep.add_component` | editor | attach a component to a node in the open scene (needs `allow_editor_writes`) |
| `beep.apply_skin` | editor | re-skin every `ThemePresetComponent` in the open scene |
| `beep.generate_project` | editor | stamp a starter project (needs `allow_editor_writes`) |
| `beep.game_state` | runtime | live `GameApp` session state |

Components are found by **reflection**, not a hand-maintained list — add a new
`EntityComponent` subclass and it appears automatically, no edit here.

Typical agent flow:

```
beep.list_components  {"category": "GameplayComponent", "search": "health"}
beep.component_info   {"type": "HealthComponent"}
beep.add_component    {"node": "Player", "type": "HealthComponent",
                       "properties": {"MaxHealth": 150}}
```

Once attached, the bridge's generic `node.set_property` / `node.call_method` drive it —
nothing Beep-specific is needed for that.

## Further reading

- [Architecture →](../../docs/ARCHITECTURE.md)
- [App workflow →](../../docs/APP_WORKFLOW.md)
- [Skinning & theming →](../../docs/SKINNING_THEMING.md)
- [File formats →](../../docs/FILE_FORMATS.md)
- [Enhancement suggestions →](../../docs/ENHANCEMENT_SUGGESTIONS.md)
