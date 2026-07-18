# Beep Game Builder — Index

What's in the addon, organized for quick discovery. **All scenes are component-based**
(every attached script is a `[GlobalClass]` C# class). **No generators, no inline `.gd`
scripts, no legacy compatibility code.**

> **Composing an entity?** See [`docs/ARCHETYPES.md`](../../docs/ARCHETYPES.md) — which
> components each archetype (player, enemy, item, projectile, destructible, …) requires, allows,
> and must not have, plus the parent-type each needs. `validate_scenes.sh` enforces the parent-type
> rules.

## Quick start

1. Open your project in Godot 4.7 with the addon enabled.
2. Create a new scene (File → New Scene → Node2D root).
3. Add a `BeepGenreScene` node (Add Node → Beep → GenreScene).
4. Set `GenreId = "platformer"` (or `topdown` / `shooter` / `puzzle`) in the Inspector.
5. Run the scene. The genre's main scene template is auto-instantiated as a child,
   `GameApp.Info` is populated, and a sibling `ThemePresetComponent` (if present) is driven.

## Scene templates

| Path | Purpose | Genre | Auto-instantiated? |
|------|---------|-------|---------------------|
| `templates/scenes/main_menu.tscn` | Title + Start/Options/Quit + ThemePreset + GameInfoBinder | shared | no |
| `templates/scenes/pause_menu.tscn` | Dimmed overlay + Resume/Restart/Quit | shared | no |
| `templates/scenes/settings_menu.tscn` | Resolution/Fullscreen/Volume/Back | shared | no |
| `templates/scenes/hud.tscn` | Score/Lives/Health labels + HudComponent | shared | no |
| `templates/scenes/game_over.tscn` | Title + ScoreSummary + Retry/MainMenu | shared | no |
| `templates/scenes/dialog_template.tscn` | CanvasLayer + Panel + NameLabel + TextLabel (BBCode) | shared | no |
| `templates/scenes/enemy_template.tscn` | CharacterBody2D + AnimatedSprite2D + DetectionArea + HitboxArea + HealthBar (no components — add your own) | shared | no |
| `templates/scenes/player_template.tscn` | CharacterBody2D + Sprite2D + CollisionShape2D (no components — add your own) | shared | no |
| `templates/scenes/robot_npc_template.tscn` | same shape as player_template | shared | no |
| `templates/scenes/pickup_template.tscn` | Area2D + Sprite2D + CollisionShape2D | shared | no |
| `templates/scenes/projectile_template.tscn` | Area2D + Sprite2D + CollisionShape2D | shared | no |
| `templates/scenes/platformer/platformer_main.tscn` | Player + Camera + HUD + Parallax + GameFlow | platformer | **yes** (by `BeepGenreScene`) |
| `templates/scenes/topdown/topdown_main.tscn` | TopDown player + camera + HUD | topdown | **yes** |
| `templates/scenes/shooter/shooter_main.tscn` | Shooter player + camera + HUD | shooter | **yes** |
| `templates/scenes/puzzle/puzzle_main.tscn` | Puzzle board + camera + HUD | puzzle | **yes** |
| `templates/scenes/platformer/level_select.tscn` | Level chooser | platformer | no (navigate to from `GameFlowComponent`) |
| `templates/scenes/platformer/level_results.tscn` | Run results screen | platformer | no |
| `templates/scenes/puzzle/level_map.tscn` | Map UI | puzzle | no |
| `templates/scenes/puzzle/pre_level.tscn` | Pre-level splash | puzzle | no |
| `templates/scenes/puzzle/level_complete.tscn` | Win screen | puzzle | no |
| `templates/scenes/puzzle/level_failed.tscn` | Lose screen | puzzle | no |
| `templates/scenes/shooter/character_select.tscn` | Character picker | shooter | no |
| `templates/scenes/shooter/level_up_choice.tscn` | Level-up rewards | shooter | no |
| `templates/scenes/shooter/run_results.tscn` | End-of-run summary | shooter | no |
| `templates/scenes/shooter/codex.tscn` | Codex/encyclopedia | shooter | no |
| `templates/scenes/topdown/pause_subscreen.tscn` | Pause overlay for topdown | topdown | no |

## Particle templates

`templates/particles/` — 9 PackedScene particle effects (no `.gd` code; just node
composition with `Particles2D` + `AnimatedSprite2D` shapes).

## Shader templates

`templates/shaders/` — 15 `.gdshader.template` files. These are **shader source
code** (not generator artifacts) — copy them to your `assets/shaders/` folder
and add via the Godot shader picker. They are pure visual effects, not scripts.

## Translations

`templates/i18n/translations.csv` — 36 rows of localized strings (English /
Spanish / Japanese). Consumed at runtime by the existing
`LocalizationComponent` via `TranslationServer.AddTranslation`. Drop the file
into your project's `res://i18n/` and add a `LocalizationComponent` to your
scene root.

## Skins (4 genres × N themes × N palettes)

`catalogs/skins/` — file-driven skin catalog. 4 genres (platformer, topdown,
shooter, puzzle). Each has `genre.json` (tuning, scene list, default theme),
`geometry.json` (per-genre geometry profile + per-node shape overrides +
optional `background_image` / `background_mode`), and `themes/<theme>/theme.json`
(22 colors + per-theme geometry + animation + optional 9-patch texture slots
under `textures{}`). Add a genre = drop a folder. Add a theme = drop a
`theme.json`. All autoloaded by `SkinCatalog`.

## Components

`ecs/` and `ecs/ui/` — 177 `[GlobalClass]` C# components total. Categorized
automatically by Godot's Add Node dialog (Ctrl+A → search "Component"):

- **UIComponent** (~66 classes in `ecs/ui/`) — panels, dialogs, accordions,
  tabs, dropdowns, toasts, tooltips, badges, FX overlays, theme components.
- **GameplayComponent** — health, attack, movement, knockback, AI, inventory,
  projectiles, drops, state machine, dialog, save manager, settings, weather,
  day/night cycle, wind field, and more.
- **ControllerComponent** — top-down / platformer / shooter / fly / glide /
  jump / dash / hover / squash-and-stretch / camera-zoom / screen-shake.
- **WorldComponent** — parallax, lifetime, spawner, particle, pickup, day/night,
  weather HUD, fog, lightning, wind field, destructible, projectile modifier,
  hazards, pickups, moving platforms, checkpoints, door switches, turrets.
- **EntityComponent** (base) — every component derives from this. Add `ComponentGroup`
  for systems to find; `IsActive` to disable; `GetSiblingComponent<T>()` for entity
  lookups.

To add a component to a scene: open the scene → Add Node (Ctrl+A) → search by
class name (e.g. "Health") → click Add. That's it. The component is now part
of your scene, ready to be configured via the Inspector.

## Editor dock

`Beep Game Builder` (3 tabs) appears in the right side dock when the addon is
enabled:

- **App** — autoload probe + every `GameInfo` field as an editable control.
  Save to `res://game_info.tres` + reload from disk + apply live to every
  `ThemePresetComponent` in the open scene.
- **Theme** — cascading genre → theme → palette → geometry dropdowns from
  `SkinCatalog.AllGenres`. Click "Apply to all ThemePresetComponents in open
  scene" to re-theme.
- **Settings** — resolution / FPS / fullscreen writes to `ProjectSettings`.
  Toggle `internationalization/locale/translations` for the translation CSV.

## Add files

- `addons/beep_game_builder_cs/INDEX.md` — this file.
- `addons/beep_game_builder_cs/BeepGameBuilderPlugin.cs` — editor plugin entry
  point. Adds the dock + configures the MCP bridge.
- `addons/beep_game_builder_cs/BeepGameBuilderPlugin.cs.uid` — Godot UID.
- `addons/beep_game_builder_cs/ecs/BeepGenreScene.cs` — the recommended entry
  point for new scenes.
- `addons/beep_game_builder_cs/ecs/GameApp.cs` — runtime autoload (must be
  registered under `/root/GameApp` in the project's SceneTree).
- `addons/beep_game_builder_cs/ecs/EntityComponent.cs` — base class for all
  gameplay/world/controller components.
- `addons/beep_game_builder_cs/ecs/ui/ThemePresetComponent.cs` — the runtime
  themer (drop in any scene with a `BeepGenreScene` sibling).
- `addons/beep_game_builder_cs/ecs/ui/SkinCatalog.cs` — file-driven skin loader.
- `addons/beep_game_builder_cs/core/GameInfo.cs` — the `[GlobalClass]` config
  resource (saved to `res://game_info.tres`).
- `addons/beep_game_builder_cs/core/BeepFileUtils.cs` — file I/O + log callbacks.
- `addons/beep_game_builder_cs/core/BeepKeybindManager.cs` — runtime key registry.
- `addons/beep_game_builder_cs/core/BeepStateMachine.cs` — generic FSM.
- `addons/beep_game_builder_cs/core/BeepProceduralAnim.cs` — noise helpers.

## See also

- `docs/SKIN_SYSTEM.md` — file-format schemas for `genre.json` / `geometry.json` / `theme.json`.
- `docs/ARCHITECTURE.md` — master architecture map.
