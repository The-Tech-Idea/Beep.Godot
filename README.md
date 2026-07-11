# Beep.Godot

A collection of Godot 4 addons by **The Tech Idea** for building games fast — UI
theming, drag-and-drop widgets, gameplay scaffolding, ECS components, shaders,
particles, projectiles, and an MCP bridge for AI agents.

The project ships **two independent addons**, split by language and purpose:

| Addon | Language | Use it for |
|---|---|---|
| **`beep_ui`** | GDScript | UI theming (22 presets that style a whole scene), 11 UI effects, and 84+ drag-and-drop themed widgets. Runs in **every** Godot 4.3+ project. |
| **`beep_game_builder_cs`** | C# (.NET) | Game building: project/scene/script/shader/tween/particle/projectile generators, 50+ ECS gameplay components, validation, exports, and an MCP bridge for AI agents. Requires a **.NET** Godot project. |

You can use either or both. `beep_ui` is self-contained and does **not** depend on
the C# addon.

## Quick start

1. Copy the addon folder(s) you want into your project's `addons/` directory.
   - `Addon/beep_ui` → `addons/beep_ui`
   - `Addon/beep_game_builder_cs` → `addons/beep_game_builder_cs` (C# / .NET only)
2. Open the project in **Godot 4.3+**.
3. **Project → Project Settings → Plugins** → enable the addon(s).
4. Restart/reload when prompted.

## Documentation

**Start here:**
- **[Architecture →](docs/ARCHITECTURE.md)** — master map of both addons: layers, data flow, deployment topology.
- **[App workflow →](docs/APP_WORKFLOW.md)** — the C# app layer: project generation, autoloads, scene wiring.
- **[Skinning & theming →](docs/SKINNING_THEMING.md)** — visual pipeline: SkinCatalog, ThemePresetComponent, BeepThemeApplier, BeepPreset, widgets, effects.
- **[File formats →](docs/FILE_FORMATS.md)** — JSON schema reference for every file under `catalogs/skins/`.

**Per-addon docs (legacy):**
- **[Beep UI (`beep_ui`) →](Addon/beep_ui/README.md)** — theming engine, presets, effects, widgets, Theme Studio dock, and the API reference.
- **[Beep Game Builder (C#) →](Addon/beep_game_builder_cs/)** — C# addon source. See the in-dock tabs (Project, Scenes, Characters, Shaders, Tweens, Particles, Projectiles, Validation, Export) and the MCP bridge under `mcp/`.

## Why two addons?

`beep_ui` was extracted from the original C# addon so the UI/theming layer can run
in **any** Godot project (including pure-GDScript ones), while the heavier C# game-
building and ECS tooling stays in `beep_game_builder_cs` for .NET projects. The
22 theme presets and 11-effect system in `beep_ui` are faithful GDScript ports of
the original C# engine, with the silent-no-op placement bug fixed.

## Requirements

- Godot **4.3 or newer**.
- For `beep_game_builder_cs`: a .NET-enabled Godot project (Godot with C# support).

## License

See [LICENSE.txt](LICENSE.txt).
