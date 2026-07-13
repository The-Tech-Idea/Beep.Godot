# Beep.Godot

A Godot 4.7 game builder addon by **The Tech Idea** — 177 categorized
`[GlobalClass]` C# components, a file-based skin system (genre → theme →
palette → geometry), scene templates for 4 genres, weather system with
day/night cycle, translations, and an MCP bridge for AI agents.

**Production-ready.** No generators, no legacy compatibility, no obsolete
code paths. Everything is either a component (drop in the editor) or a
data file (drop in `catalogs/`).

The project ships **two independent addons**, split by language and purpose:

| Addon | Language | Use it for |
|---|---|---|
| **`beep_ui`** | GDScript | UI theming (22 presets, 11 effects, 84+ widgets). Runs in any Godot 4.7+ project. |
| **`beep_game_builder_cs`** | C# (.NET 8) | Game building: file-based skin system, 177 categorized ECS components, weather system, day/night cycle, scene templates, translations, MCP bridge. Requires a **.NET** Godot project. |

You can use either or both. `beep_ui` is self-contained and does **not** depend on
the C# addon.

## Quick start

1. Copy the addon folder(s) you want into your project's `addons/` directory.
   - `addons/beep_ui` → `addons/beep_ui`
   - `addons/beep_game_builder_cs` → `addons/beep_game_builder_cs` (C# / .NET only)
2. Open the project in **Godot 4.7+** with .NET support.
3. **Project → Project Settings → Plugins** → enable the addon(s).
4. **Create a new scene** (File → New Scene → Node2D root).
5. **Add a `BeepGenreScene` component** (Add Node → Beep → GenreScene).
6. **Set `GenreId`** in the inspector: `"platformer"`, `"topdown"`, `"shooter"`, or `"puzzle"`.
7. **Run** the scene. The genre's already-wired main scene is auto-instantiated
   as a child, `GameApp.Info` is populated, and any sibling `ThemePresetComponent`
   is driven from the resolved theme.

**Add components** anywhere via Godot's native **Add Node** dialog (Ctrl+A) —
search by class name (e.g. "Health", "TopDown", "WeatherSystem"). Every
`[GlobalClass]` component is automatically registered with Godot's class
registry.

## Editor dock

The `beep_game_builder_cs` dock has **3 tabs**:

1. **App** — autoload probe + every `GameInfo` field as an editable control.
   Save to `res://game_info.tres` + reload from disk + apply live to every
   `ThemePresetComponent` in the open scene.
2. **Theme** — cascading genre → theme → palette → geometry dropdowns from
   `SkinCatalog.AllGenres`. Click "Apply to all ThemePresetComponents in open
   scene" to re-theme.
3. **Settings** — resolution / FPS / fullscreen writes to `ProjectSettings`.
   Toggle `internationalization/locale/translations` for the translation CSV.

## What ships in `beep_game_builder_cs/`

See **[`addons/beep_game_builder_cs/INDEX.md`](addons/beep_game_builder_cs/INDEX.md)** for the full
inventory of scenes, particles, shaders, translations, skins, and components.

Highlights:
- **177 `[GlobalClass]` C# components** organized into UIComponent /
  GameplayComponent / ControllerComponent / WorldComponent bases.
- **4 genre scenes** — `templates/scenes/{platformer,topdown,shooter,puzzle}/*_main.tscn`,
  auto-loaded by `BeepGenreScene` based on `GenreId`.
- **5 shared UI scenes** — main menu, pause, settings, HUD, game over — already
  wired with `ThemePresetComponent` + `GameInfoBinder`.
- **14 genre-specific UI scenes** — level select, level results, character
  select, level-up choice, run results, codex, level map, pre-level, level
  complete, level failed, pause subscreen.
- **15 `.gdshader.template` shaders** — visual effects (fog, fire aura, hit
  flash, portal vortex, etc).
- **9 particle scenes** — blood splatter, coin pickup, explosion, fire torch,
  hit sparks, magic spell, rain drops, simple burst, smoke puff.
- **36-row translation CSV** — English / Spanish / Japanese strings for menus,
  HUD labels, common prompts.

## File-based skin system

All theme/palette/geometry/genre data lives in JSON files under `catalogs/skins/`:

```
skins/
├── platformer/
│   ├── genre.json           ← tuning, scene list, default theme, nav_wiring
│   ├── geometry.json        ← per-genre geometry + per-node shapes + background image
│   └── themes/
│       └── cartoon/
│           ├── theme.json   ← 22 colors + geometry + animation + textures
│           ├── default.json ← palette (HSV tint)
│           └── warm.json    ← another palette
├── topdown/   (same structure)
├── shooter/   (same structure)
└── puzzle/    (same structure)
```

**Add a genre = drop a folder.** **Add a theme = drop a `theme.json`.** **Add a
palette = drop a `.json`.** All autoloaded by `SkinCatalog` — zero C# changes
needed. See **[`docs/SKIN_SYSTEM.md`](docs/SKIN_SYSTEM.md)** for the cookbook.

## Weather system

`WeatherSystemComponent` + `WindFieldComponent` + `WeatherHUDComponent`:
- 10 weather types (Clear, Cloudy, Rain, Snow, Storm, Fog, Sandstorm, Hail, LeafFall, Heatwave)
- Day/night cycle with 4-keyframe sky gradient
- Seasons gating available weather
- Procedural fog/cloud/shadow shaders
- Lightning bolts (procedural Line2D) + camera shake
- Intensity engine (0..1 cross-fade transitions)
- Wind physics (Area2D wind field for RigidBody2D + CharacterBody2D)

## Translation system

`templates/i18n/translations.csv` ships with 36 rows. The
`LocalizationComponent` (in `ecs/ui/`) loads this CSV at runtime, registers
each column as a `Translation` with Godot's `TranslationServer`, and
`SetLanguage(locale)` switches the active language. Auto-translates every
`Label` / `Button` / `RichTextLabel` whose text matches a translation key.

## Documentation

- **[Architecture →](docs/ARCHITECTURE.md)** — master map of both addons
- **[App workflow →](docs/APP_WORKFLOW.md)** — runtime autoloads, scene wiring
- **[Skinning & theming →](docs/SKINNING_THEMING.md)** — visual pipeline deep-dive
- **[Skin system cookbook →](docs/SKIN_SYSTEM.md)** — how to add genres/themes/palettes
- **[File formats →](docs/FILE_FORMATS.md)** — JSON schema reference
- **[Beep UI (GDScript) →](addons/beep_ui/README.md)** — theming engine, presets, widgets
- **[Beep Game Builder (C#) →](addons/beep_game_builder_cs/INDEX.md)** — full inventory of what ships

## Why two addons?

`beep_ui` was extracted from the original C# addon so the UI/theming layer can run
in any Godot project — including pure-GDScript ones. The C# addon builds on top:
it uses the GDScript theme presets for its generated UI scenes and adds the full
game-building layer (ECS components, weather, scene templates, translations).

## Requirements

- **Godot 4.7+** with **.NET 8** SDK (for `beep_game_builder_cs`)
- Godot 4.7+ without .NET (for `beep_ui` only)
