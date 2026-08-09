# Beep.Godot Addons Manual

This manual documents the three addons currently present in the repository:

- `addons/beep_game_builder_cs`
- `addons/beep_ui`
- `addons/godot_mcp`

It was rebuilt from an exhaustive source/config/template scan on 2026-08-09. Existing docs and plans were intentionally ignored as source material.

## Repository Addon Map

| Addon | Language | Purpose | Main entry |
| --- | --- | --- | --- |
| `beep_game_builder_cs` | C# | Game scaffolding, reusable gameplay components, UI kit, scene templates, skin catalogs, save/load, weather, and Beep MCP commands | `BeepGameBuilderPlugin.cs` |
| `beep_ui` | GDScript | Lightweight theme presets, widget factory, UI effects, toast host, and editor theme studio | `plugin.gd` |
| `godot_mcp` | C# | MCP bridge for editor/runtime state, perception, safe writes, and command dispatch | `GodotMcpPlugin.cs`, `GodotMcpRuntime.cs` |

## Verified Inventory

| Area | Count | Rule |
| --- | ---: | --- |
| Scanned addon text files | 909 | `.cs`, `.gd`, `.cfg`, `.tscn`, `.json` under `addons`, excluding docs/plans |
| `beep_game_builder_cs` files | 862 | 357 C#, 420 JSON, 84 `.tscn`, 1 config |
| `beep_ui` files | 30 | GDScript source and plugin config |
| `godot_mcp` files | 17 | C# source and plugin config |
| C# global classes | 307 | `[GlobalClass]` occurrences under `addons/beep_game_builder_cs/**/*.cs` |
| Skin genres | 10 | directories under `addons/beep_game_builder_cs/catalogs/skins` |
| Skin themes | 50 | 5 themes per genre |
| Scene templates | 69 | `.tscn` under `templates/scenes` |
| Particle templates | 15 | `.tscn` under `templates/particles` |
| Beep MCP commands | 41 | `McpCommandRegistry.RegisterCommand("beep...")` calls |

## Installation

1. Copy the addon folders into a Godot 4 C# project.
2. Keep `Beep.Godot.csproj` or the consumer project on `Godot.NET.Sdk` and `net8.0`.
3. Enable plugins in Godot:
   - `Beep Game Builder C#`
   - `Beep UI`
   - `Godot MCP`
4. Build C# once. Godot only registers C# `[GlobalClass]` scripts after a successful build.
5. Use the Add Node dialog to add C# components or use Beep MCP commands when `godot_mcp` is enabled.

## Current Build Caveat

The current root build is red. `dotnet build .\Beep.Godot.csproj` compiles generated files under `tests/**/obj/**` and `.godot/mono/temp/obj/**`, causing duplicate assembly attribute errors. This is tracked in `plans/enhancement-review-6/phase-1-build-gate.md`.

Until that phase is fixed, treat `[GlobalClass]` counts as source inventory, not proof that every class is currently available in the editor.

## beep_game_builder_cs

### Architecture

The C# addon is organized around composable Godot nodes and resources:

- `ecs/categories`: base categories such as `EntityComponent`, `GameplayComponent`, `ControllerComponent`, `UIComponent`, `UIScreenComponent`, `WorldComponent`, and `AreaTriggerComponent`.
- `ecs`: gameplay, controller, inventory, state, interaction, projectile, combat, survival, racing, puzzle, strategy, and world components.
- `ecs/atmosphere`: weather, day/night, seasons, fog, clouds, lightning, ambient audio, and sprite weather layers.
- `ecs/ui`: reusable UI behaviors, menus, HUDs, theming, data binding hosts, settings, save/load screens, effects, and widgets.
- `ecs/ui/kit`: draw-heavy and drop-in Game UI Kit controls.
- `ecs/scenes`: scripts attached to generated or template screens.
- `ecs/items` and `ecs/stats`: resource models for inventory, equipment, stats, modifiers, and cataloged items.
- `core`: project generation, skin catalog helpers, file utilities, save/load data, command history, form/data widgets, and game metadata.
- `catalogs/skins`: data-driven genre/theme/palette/geometry/texture metadata.
- `templates`: scene and particle templates copied or instantiated by generator flows.
- `mcp`: Beep-specific commands registered into the `godot_mcp` bridge registry.

### Base Component Model

`EntityComponent` is the base for most C# component nodes. Category subclasses define intent:

- `GameplayComponent`: game rules, state, combat, inventory, resources, and progression.
- `ControllerComponent`: input, movement, camera, character or entity control.
- `WorldComponent`: environment, hazards, particles, weather, world interaction.
- `UIComponent`: UI widgets or behavior attached under controls.
- `UIScreenComponent`: screen controllers that usually navigate, load, or save.
- `AreaTriggerComponent`: trigger helpers designed for `Area2D` parent behavior.

The common usage pattern is to attach a component as a child of the node it controls. Many components check parent type at runtime and emit `GD.PushWarning` when placed under the wrong node type.

### Gameplay And Controller Components

Common gameplay/controller classes include:

| Area | Classes |
| --- | --- |
| AI and targeting | `AIController`, `AggroComponent`, `FollowTargetComponent`, `NavigationComponent`, `AnimalBehaviorComponent`, `TurretComponent` |
| Player movement | `TopDownController`, `PlatformerController`, `ShooterController`, `MovementComponent`, `JumpComponent`, `DashComponent`, `GlideComponent`, `FlyComponent`, `SlideComponent`, `WallJumpComponent`, `HoverComponent` |
| Combat | `AttackComponent`, `ShooterCombatComponent`, `HealthComponent`, `HealthBarComponent`, `AutoHealComponent`, `KnockbackComponent`, `ResistanceComponent`, `StatusEffectComponent`, `DamageType`, `GameDamage` |
| Projectiles and spawning | `ProjectileComponent`, `ProjectileModifierComponent`, `ObjectPoolComponent`, `SpawnerComponent` |
| Interactions | `InteractableComponent`, `PickupComponent`, `CheckpointComponent`, `HazardComponent`, `DoorSwitchComponent`, `DestructibleComponent` |
| Game state and flow | `GameApp`, `GameFlowComponent`, `GameStateManagerComponent`, `BootComponent`, `LevelLoaderComponent`, `MainGameComponent`, `GameOverOnDeathComponent`, `DespawnOnDeathComponent` |
| RPG/items/progression | `InventoryComponent`, `EquipmentComponent`, `ConsumableUseComponent`, `CraftingComponent`, `DropTableComponent`, `LevelingComponent`, `QuestComponent`, `RpgPartyComponent`, `StatsComponent` |
| Genre systems | `CardDeckComponent`, `CityEconomyComponent`, `CropGrowthComponent`, `HungerStaminaComponent`, `PuzzleLevelComponent`, `RaceStateComponent`, `StrategyEmpireComponent`, `SurvivalVitalsComponent`, `WorkComponent` |
| Feedback/effects | `AudioComponent`, `FootstepComponent`, `FlashComponent`, `FloatingTextComponent`, `HitSoundComponent`, `HitSparkComponent`, `HitStopComponent`, `ParticleComponent`, `ScreenShakeComponent`, `SpriteEffectComponent`, `SquashAndStretchComponent`, `TrailComponent`, `TweenComponent` |

### Atmosphere System

The atmosphere layer is split across several focused components:

| Class | Role |
| --- | --- |
| `WeatherSystemComponent` | Main weather state, weather transitions, particles, overlays, wind, lightning, cloud behavior, and day/season integration hooks |
| `WeatherSystemComponent.DayNight` | Weather restrictions and helpers tied to season/day-night context |
| `WeatherSystemComponent.Intensity` | Intensity derivation and transition logic |
| `WeatherSystemComponent.Overlays` | Visual overlay construction and update logic |
| `WeatherSpriteLayer` | Sprite-driven weather visual layer |
| `CloudSpriteLayer` | Sprite cloud layer |
| `DynamicFogLayer` | Fog overlay and weather integration |
| `LightningBoltComponent` | Procedural line lightning |
| `DayNightCycleComponent` | Time-of-day cycle and tinting |
| `SeasonalComponent` | Season state and seasonal signal |
| `AmbientController` | Scene ambient control |
| `AmbientAudioComponent`, `WeatherAudioController` | Zone and weather audio behavior |

### UI Component System

The C# UI layer has three overlapping roles:

- Screen controllers such as menus, settings, save/load, genre sub-screens, and generated screen scripts.
- Behavioral components attached to controls: accordions, tabs, carousels, drag, search, tables, timers, counters, loading, modals, prompts, safe area, pause, transitions, effects.
- Theming and skinning: `ThemePresetComponent`, `SkinCatalog`, `UISkin`, `FileThemePreset`, `PaletteTintedPreset`, `GeometryProfile`, `ColorPalette`, `ShapeOverrides`, and skin property hints.

Important theming behavior:

- `ThemePresetComponent` themes a `Control` subtree.
- `SkinCatalog` reads `catalogs/skins` and resolves genre, theme, palette, geometry, texture, and kit values.
- `GameInfo` can store selected genre/theme/palette/geometry names.
- UI templates use a mixture of `ThemePresetComponent`, `GameInfoBinder`, `PanelFrameComponent`, `Kit*` controls, and genre-specific HUD scripts.

### Game UI Kit

The kit controls are C# `[GlobalClass]` nodes under `ecs/ui/kit`.

Two important categories:

- Drop-in controls that inherit the Godot control they replace, such as `KitPushButton : Button`, `KitPanelContainer : PanelContainer`, `KitOptionButton : OptionButton`, `KitSlider : HSlider`, `KitTree : KitControl`, `KitGodotTree : Tree`, and `KitLabel : Label`.
- Custom drawn controls that inherit `KitControl` or `Control`, such as `KitInventorySlot`, `KitItemCard`, `KitLevelPath`, `KitBookSpread`, `KitRadarChart`, `KitRadialMeter`, `KitSpeechBubble`, `KitToast`, and `KitWeatherForecastCard`.

The kit style contract is loaded by `KitStyleJson` from skin JSON. Theme JSON may define `kit` blocks and `edge_run` data. Texture references in scanned skin JSON currently resolve.

### Skin Catalog

Skin catalog genres and themes:

| Genre | Themes |
| --- | --- |
| `cardgame` | `arcane`, `casino`, `paper`, `royal`, `velvet` |
| `citybuilder` | `blueprint`, `eco`, `future`, `industrial`, `urban` |
| `platformer` | `cartoon`, `modern`, `nature`, `pixel8bit`, `retro80s` |
| `puzzle` | `candy`, `cartoon`, `japan`, `modern`, `sea` |
| `racing` | `arcade`, `carbon`, `motorsport`, `neon`, `street` |
| `rpg` | `arcane`, `darkfantasy`, `fantasy`, `parchment`, `royal` |
| `shooter` | `cyberpunk`, `military`, `scifi`, `space`, `toxic` |
| `strategy` | `blueprint`, `command`, `military`, `royal`, `scifi` |
| `survival` | `apocalypse`, `desert`, `frozen`, `industrial`, `wilderness` |
| `topdown` | `classic`, `fantasy`, `japan`, `military`, `nature` |

Each genre has `genre.json`, `geometry.json`, and theme data under `themes/<theme>`.

### Scene Templates

Templates are grouped by purpose:

- Main genre scenes: `cardgame_main`, `citybuilder_main`, `platformer_main`, `puzzle_main`, `racing_main`, `rpg_main`, `shooter_main`, `strategy_main`, `survival_main`, `topdown_main`.
- Common screens: main menu, settings menu, game over, save/load, HUD, level summary, theme gallery, kit browser/gallery.
- Genre screens: RPG character/inventory/quests, racing garage/results/vehicle select, shooter character select/codex/level up/run results, puzzle level map/pre-level/results, strategy diplomacy/research/unit panel, survival backpack/crafting/world map, cardgame battle/collection/deck builder, citybuilder build menu/districts/economy.
- Entity templates: player, enemy, pickup, projectile, dialog, robot NPC.
- Level templates: two starter levels for platformer, racing, RPG, shooter, survival, and topdown.
- Particle templates: blood, coin, dust, explosion, fire, heal, hit sparks, magic, muzzle, rain, smoke, sparkle.

The 2026-08-09 scan found no missing `res://` references in these templates.

## beep_ui

`beep_ui` is a GDScript-first UI addon. It is separate from the C# theme system and should be treated as a parallel implementation, not a generated port.

### Public Classes

| Class | File | Role |
| --- | --- | --- |
| `BeepPreset` | `theme/beep_theme.gd` | Registry and base data model for theme presets |
| `BeepThemeApplier` | `theme/theme_applier.gd` | Applies a preset to one or more `Control` subtrees |
| `BeepUIEffect` | `effects/ui_effect.gd` | Runtime UI effects: slide, shake, pulse, bob, flash, glitch, rotate, fade, typewriter, bounce, offset |
| `BeepWidgetFactory` | `widgets/widget_factory.gd` | Factory helpers for styled widgets |
| `BeepToastHost` | `widgets/toast_host.gd` | Toast display host with info/success/warning/error variants |

### Presets

Registered presets:

`Modern`, `SciFi`, `Cartoon`, `Classic`, `Desert`, `OilGas`, `Sea`, `Sports`, `Soccer`, `Fantasy`, `Horror`, `Nature`, `Space`, `Military`, `Steampunk`, `Retro80s`, `Pixel8Bit`, `Winter`, `Cyberpunk`, `Japan`, `Toxic`, `Candy`.

Current behavior: `BeepPreset` owns the registry in `theme/beep_theme.gd`, and `BeepThemeApplier` derives its inspector enum hint from `BeepPreset.preset_names()` through `_get_property_list()`.

### Typical Usage

Add `BeepThemeApplier` near the UI to theme:

- As a child of a `Control`, it themes the parent subtree.
- As a parent of controls, it themes child control subtrees.
- `preset` selects the preset name.
- `enable_animations` and `enable_ripple` control injected interaction behavior.
- `active` toggles whether it applies.

Use `BeepUIEffect` on UI nodes for animation without C# dependencies.

Use the editor Theme Studio dock to browse presets and insert common UI widgets.

## godot_mcp

`godot_mcp` provides editor/runtime bridge plumbing. It is intentionally gated:

- Read/perception commands are broadly available.
- Editor writes require `godot_mcp/security/allow_editor_writes=true`.
- Runtime writes require `godot_mcp/security/allow_runtime_writes=true`.
- Node method calls require `godot_mcp/security/allow_node_method_calls=true`.

### Settings

| Setting | Default/Behavior |
| --- | --- |
| `godot_mcp/bridge/url` | Defaults to `ws://127.0.0.1:8789`; can be overridden by `GODOT_MCP_BRIDGE_URL` |
| `godot_mcp/bridge/token` | Optional project setting; if absent, a process-local session token is generated; can be overridden by `GODOT_MCP_BRIDGE_TOKEN` |
| `godot_mcp/bridge/auto_connect_editor` | Enabled |
| `godot_mcp/bridge/auto_connect_runtime` | Enabled |
| `godot_mcp/bridge/reconnect_seconds` | 2 seconds |
| `godot_mcp/bridge/verbose_logging` | Enabled |
| `godot_mcp/security/allow_editor_writes` | Disabled |
| `godot_mcp/security/allow_runtime_writes` | Disabled |
| `godot_mcp/security/allow_node_method_calls` | Disabled |
| `godot_mcp/runtime/screenshot_directory` | `user://mcp_screenshots` |

Current status: `project.godot` no longer stores a concrete token. The bridge still accepts an environment token or a manually configured project token.

### Beep MCP Commands

Catalog and project:

- `beep.list_genres`
- `beep.list_themes`
- `beep.list_palettes`
- `beep.catalog`
- `beep.genre_info`
- `beep.list_scene_templates`
- `beep.list_weather_types`
- `beep.reload_catalog`
- `beep.get_game_info`
- `beep.set_game_info`
- `beep.apply_skin`
- `beep.generate_project`

Components:

- `beep.list_components`
- `beep.component_info`
- `beep.add_component`

Game state:

- `beep.game_state`
- `beep.list_saves`
- `beep.save_game`
- `beep.load_game`
- `beep.delete_save`
- `beep.new_game`

Runtime game controls:

- `beep.add_score`
- `beep.game_over`
- `beep.level_complete`
- `beep.set_level`
- `beep.get_weather`
- `beep.set_weather`
- `beep.get_time`
- `beep.set_time`
- `beep.get_settings`
- `beep.set_setting`
- `beep.list_locales`
- `beep.set_language`
- `beep.translate`

Scene/editor helpers:

- `beep.list_scenes`
- `beep.open_scene`
- `beep.inspect_scene`
- `beep.get_node_property`
- `beep.set_node_property`
- `beep.add_node`
- `beep.remove_node`
- `beep.save_scene`
- `beep.screenshot`
- `beep.bake_textures`
- `beep.new_screen`

Kit helpers:

- `beep.kit_widgets`
- `beep.kit_scene_audit`
- `beep.kit_template_audit`
- `beep.kit_convert_scene`

Current status: kit commands unregister on addon disable through `BeepMcpKitCommands.Unregister()`.
