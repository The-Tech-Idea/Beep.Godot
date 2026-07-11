# Beep Game Builder (C#)

A .NET (C#) Godot 4 addon for scaffolding games fast: generators for projects,
scenes, scripts, shaders, tweens, particles, and projectiles, 50+ ECS gameplay
components, project validation/export tooling, and an MCP bridge for AI agents.

> UI theming and drag-and-drop widgets now live in the separate, GDScript
> **[`beep_ui`](../beep_ui/README.md)** addon, which runs in any Godot project.
> This C# addon focuses on game building, ECS, and MCP.

## Requirements

- Godot **4.3+** with **C# / .NET** support.
- A .NET-enabled Godot project (the addon's `.cs` files compile as part of your
  project's `.csproj`).
- The **`beep_ui`** GDScript addon enabled (for themed UI scenes used by genre templates).

## Installation

1. Copy this folder to `<your_project>/addons/beep_game_builder_cs`.
2. Open the project in Godot (C# build runs automatically).
3. **Project → Project Settings → Plugins → enable "Beep Game Builder (C#)"**.
4. The **Beep Game Builder** dock appears on the right.

## What's in the dock

Each tab exposes one-click generators:

- **Project** — standard folders, project defaults (1280×720, 2D stretch), input
  map, manager scripts (scene/save/audio), full starter project, and **genre
  templates** (see below).
- **Scenes** — main scene, main/pause menus, enemy patrol, pickup, moving
  platform, HUD overlay, and templates (enemy, pickup, dialog, projectile).
- **Characters** — top-down & platformer players, robot NPC, patrol AI, health,
  camera follow, doors/switches, turrets, checkpoints, weather, day/night,
  inventory, projectiles, game manager, dialog, pooling, transitions.
- **Shaders / Tweens / Particles / Projectiles** — catalog-driven generators
  backed by JSON presets in `catalogs/` and templates in `templates/`. Select
  from a searchable list and generate (with an "overwrite" toggle).
- **ECS Components** — 50+ `[GlobalClass]` components. Add them via the editor's
  **Add Node** menu (search "Component"). Categories: gameplay (Health, Attack,
  Movement, Knockback, …), controllers (TopDown/Platformer, AI, StateMachine),
  world/effects, and UI components.
- **Validation** — validate the project, auto-fix safe issues, write a report.
- **Export** — generate an export checklist.

## Genre Templates

One click in the **Project** tab stamps a complete, themed, playable starter
project for a chosen genre. Enter your game name, click the genre, and you get:

- The full navigation loop: **Main Menu → Game → (ESC: Pause) → Game Over**.
- All UI scenes themed via the `beep_ui` `ThemePresetComponent`.
- A central **`GameInfo`** autoload (`res://game_info.tres`) holding game name,
  version, genre, theme preset, resolution, and genre tuning — every scene reads
  from it.
- Manager autoloads (GameManager, SceneManager, SaveManager, AudioManager).
- Every behavior is a `[GlobalClass]` component (no `.gd` controller scripts).

| Button | Genre scene | Default theme | Key components |
|---|---|---|---|
| New Platformer Project | `platformer_main.tscn` | Cartoon | PlatformerController, Health, Parallax, Camera2D, checkpoints/pickups/enemies containers |
| New Top-Down Project | `topdown_main.tscn` | Fantasy | TopDownController, Interactable, Health, NavigationRegion2D, dialog overlay |
| New Shooter Project | `shooter_main.tscn` | SciFi | ShooterController (mouse-aim + fire), Health, Projectiles pool, Spawner |
| New Puzzle Project | `puzzle_main.tscn` | Candy | Match3BoardComponent (swap/match/cascade/refill), scoring |

All `[GlobalClass]` components appear in Godot's **Add Node** menu (Ctrl+A) —
search "Component" to add any of the 50+ components to a scene node.

## Subsystems

### ECS components (`ecs/`)
`[GlobalClass]` C# components derived from `EntityComponent` (blind, group-based,
`[Tool]`). Add as children of any node; systems find them by group. Includes 26
UI components under `ecs/ui/` (the original source of the theming engine now
ported to `beep_ui`).

### MCP bridge (`mcp/`)
A WebSocket bridge (`GodotMcpRuntime` autoload + `McpGameAdapter`) that exposes
the scene tree to external AI agents, configurable under Project Settings
(`godot_mcp/bridge/...`). See `mcp/README.md`.

## Notes

- The MCP bridge connects to `ws://127.0.0.1:8789` by default; configure the URL
  and token in Project Settings.
- Generated scripts are written into `res://scripts/...` and `.uid` files are
  created so Godot 4 recognizes them in the FileSystem.

## License

See the repository [LICENSE.txt](../../LICENSE.txt).
