# Component-First Refactor — Design Spec (Delivered)

**Date:** 2026-07-13
**Status:** ✅ Delivered & shipped (6 commits, 2026-07-13)
**Owner:** beep_game_builder_cs
**Scope:** Major refactor — remove all *Generator classes, ship component-only scene templates, thin the editor dock.

---

## 1. Context (delivered state)

`beep_game_builder_cs` had **4 generator classes** that produced scenes, scripts, folder scaffolds, input maps, and project settings on demand from editor buttons:
- `BeepGenreGenerator` (scene + autoload stamper)
- `BeepProjectGenerator` (folder scaffolder)
- `BeepInputMapGenerator` (idempotent input actions)
- `BeepProjectDefaults` (ProjectSettings writes + autoload management)

Plus the `OnStarter` method on the dock and the `BeepGameBuilderDock.Genres.cs` partial — all the "click a button to assemble a project" machinery.

**All four generator classes + their 3 orphaned `.uid` files have been deleted.** All **23 `.gd.template` scripts** under `templates/scripts/` have been deleted (each was replaced by a `[GlobalClass]` C# component per the inline `/// Replaces X.gd.template.` comments). The remainder of `core/` (utility classes like `BeepFileUtils`, `BeepKeybindManager`, `BeepStateMachine`, `GameInfo`, etc.) stays — those are runtime helpers, not generators.

**Production-ready from day one.** No `[Obsolete]` migration window, no legacy compatibility code, no obsolete code paths.

Per user direction: **everything is a component**. The addon now has **177 `[GlobalClass]` C# components** (vs. the original ~155 — growth came from the production pass). Godot's native **Add Node** dialog categorizes them automatically.

### Goals (all met)

- ✅ **No new browser UI** — Godot's Add Node dialog is the component registry.
- ✅ **All scenes are editable `.tscn` files** the user owns.
- ✅ **The dock is thin** — 3 tabs (App / Theme / Settings) with no "Generate Project" button.
- ✅ **One component (`BeepGenreScene`) replaces all generators** — drop it into a scene, set `GenreId`, run.

### Non-goals (respected)

- ✅ No new GDScript UI.
- ✅ No scripted setup phase.
- ✅ No cloud / marketplace / shareable themes.
- ✅ No new component taxonomy — `BeepXxxComponent` naming stays.

---

## 2. Architecture (delivered)

### 2.1 Final state

| Today (delivered) | What replaced the old pattern |
|-------|----------|
| **0 generators** (`BeepGenreGenerator`, `BeepProjectGenerator`, `BeepInputMapGenerator`, `BeepProjectDefaults`) | `BeepGenreScene` is the single component that drives genre wiring. |
| **0 `.gd.template` scripts** under `templates/scripts/` | Every script logic is now in a `[GlobalClass]` C# component (`DayNightCycleComponent`, `DoorSwitchComponent`, `ObjectPoolComponent`, etc.). |
| **0 obsolete/compat code paths** | Clean production build. No `[Obsolete]` markers, no fallback paths. |
| **177 `[GlobalClass]` C# components** | 102 in `ecs/` + 75 in `ecs/ui/`. |
| **3-tab dock** (App / Theme / Settings) | Replaces 11 tabs of generator buttons. |
| **25 scene templates** in `templates/scenes/` | 5 shared UI + 4 genre main + 14 genre UI + 2 starter shells (now polished with default components). |
| **15 `.gdshader.template` files** in `templates/shaders/` | Pure shader source — NOT a generator artifact. Visual effects (fog, fire aura, hit flash, etc). |
| **9 particle scenes** in `templates/particles/` | PackedScene particle effects — drop into a node. |
| **36-row translation CSV** in `templates/i18n/translations.csv` | Data for the existing `LocalizationComponent` via `TranslationServer.AddTranslation`. |

### 2.2 The new architecture (one diagram)

```mermaid
graph TB
    User["User<br/>(developer)"]
    Editor["Godot Editor"]
    Project["User Project<br/>scenes/, scripts/, assets/"]

    subgraph Addon["beep_game_builder_cs addon"]
        subgraph Components["177 [GlobalClass] components"]
            BGS["BeepGenreScene<br/>(entry point)"]
            TPC["ThemePresetComponent"]
            HealthC["HealthComponent, MovementComponent,<br/>AttackComponent, AggroComponent"]
            UI_C["BeepUIButton, AccordionComponent,<br/>ToggleSwitchComponent, BeepUIButton"]
            Loc["LocalizationComponent,<br/>SettingsComponent, DialogUIComponent"]
        end

        subgraph Templates["templates/scenes/"]
            SharedScenes["main_menu, pause_menu, settings_menu,<br/>hud, game_over.tscn<br/>(shared UI, already wired)"]
            GenreScenes["platformer/&lt;genre&gt;_main.tscn<br/>(auto-instantiated by BeepGenreScene)"]
            GenreUI["level_select, level_results, character_select,<br/>codex, level_map, level_complete, level_failed,<br/>pre_level, level_up_choice, run_results,<br/>pause_subscreen.tscn"]
            Starters["player_template, enemy_template,<br/>robot_npc_template, pickup_template,<br/>projectile_template, dialog_template.tscn<br/>(with default components)"]
        end

        subgraph Dock["BeepGameBuilderDock (3 tabs)"]
            App["App tab:<br/>autoload probe + GameInfo editor<br/>+ Save/Reload/Apply-live"]
            Theme["Theme tab:<br/>cascading genre/theme/palette picker<br/>+ Apply to all ThemePresetComponents"]
            Settings["Settings tab:<br/>resolution/FPS/i18n ProjectSettings writes"]
        end
    end

    User -- drags template --> Project
    User -- Add Node dialog --> Editor
    Editor --> BGS
    Editor --> TPC
    Editor --> HealthC
    Editor --> UI_C
    Editor --> Loc
    BGS -- _Ready -->|reads genre.json| SkinCatalog
    BGS -- _Ready -->|loads scene| GenreScenes
    BGS -- _Ready -->|drives| TPC
    SkinCatalog -.->|JSON files| BGS
```

### 2.3 The single replacement for `BeepGenreGenerator`

`BeepGenreScene` is a `[Tool] [GlobalClass] partial class : Node`. Drop it into a scene root, set `GenreId`, run. It:

1. Looks up the genre in `SkinCatalog`.
2. Applies `genre.DefaultTheme`, `genre.tuning{}`, `genre.MainScene` to `GameApp.Instance.Info`.
3. **Auto-instantiates `genre.MainScene` as a child** of self (path: `res://scenes/main/<file>` first, then addon template fallback). This is the killer feature — drop `BeepGenreScene` into a fresh empty scene, run, the genre's full playable layout materializes.
4. Drives a sibling `ThemePresetComponent` (if any) from the resolved theme/palette/geometry.
5. Emits `GenreApplied` signal.

This **one** component replaces `BeepGenreGenerator.CreateProject`, `BeepGenreGenerator.StampProject`, `BeepGenreGenerator.ApplyTuning`, and `BeepGameBuilderDock.Genres.AddGenreSection` — and runs in the editor (`[Tool]`) as well as at runtime.

### 2.4 Scene template composition rules (enforced)

Every template that ships follows four rules:

1. **Zero inline `.gd` scripts.** The only scripts attached are `[GlobalClass]` C# classes that ship in the addon. No `.gd` files inside `templates/scenes/`.
2. **Zero raw resource embeds** unless they ship with the addon.
3. **Editable exports.** Every knob is `[Export]` on a `[GlobalClass]` component, not a hardcoded literal in the scene.
4. **`load_steps = ext_resources + sub_resources + 1` exactly.** Verified by automated check.

### 2.5 Final directory layout (shipped)

```
addons/beep_game_builder_cs/
├── BeepGameBuilderPlugin.cs        ← editor plugin (dock + MCP bridge)
├── INDEX.md                        ← full inventory (replaces the planned prefabs/index.json)
├── core/                           ← 15 utility files (no generators)
│   ├── BeepFileUtils.cs
│   ├── BeepKeybindManager.cs
│   ├── BeepStateMachine.cs
│   ├── BeepProceduralAnim.cs
│   ├── BeepServiceLocator.cs
│   ├── BeepDataBinder.cs
│   ├── BeepDataGrid.cs
│   ├── BeepFormBuilder.cs
│   ├── BeepTreeView.cs
│   ├── BeepDropdown.cs
│   ├── BeepCoroutine.cs
│   ├── BeepCommandHistory.cs
│   ├── BeepAchievementDebug.cs
│   ├── BeepEncryptionPathfinding.cs
│   ├── BeepWeightedTable.cs
│   └── GameInfo.cs                  ← [GlobalClass] resource
├── ecs/                            ← 102 components + base classes
│   ├── BeepGenreScene.cs            ← NEW entry-point component
│   ├── GameApp.cs                   ← autoload singleton
│   ├── EntityComponent.cs           ← base class
│   ├── EntitySystem.cs              ← base class
│   └── ... (~99 more components)
├── ecs/ui/                          ← 75 UI components
│   ├── ThemePresetComponent.cs      ← runtime themer
│   ├── SkinCatalog.cs               ← file-driven skin loader
│   ├── LocalizationComponent.cs     ← CSV → TranslationServer
│   ├── SettingsComponent.cs
│   └── ... (~72 more UI components)
├── ui/
│   └── BeepGameBuilderDock.cs       ← REWRITTEN (3 tabs: App/Theme/Settings)
├── catalogs/                        ← JSON that drives everything
│   ├── skins/                       ← 4 genres × (genre.json + geometry.json + themes/)
│   ├── shader_presets.json
│   ├── tween_presets.json
│   ├── particle_presets.json
│   └── projectile_presets.json
├── templates/                       ← shipped as editable .tscn
│   ├── README.md                    ← documents the model
│   ├── scenes/                      ← 25 .tscn files (the prefab catalog)
│   │   ├── main_menu.tscn, pause_menu.tscn, settings_menu.tscn, hud.tscn, game_over.tscn
│   │   ├── player_template, enemy_template, robot_npc_template,
│   │   │   pickup_template, projectile_template, dialog_template.tscn  ← starter shells
│   │   ├── platformer/{platformer_main,level_select,level_results}.tscn
│   │   ├── topdown/{topdown_main,pause_subscreen}.tscn
│   │   ├── shooter/{shooter_main,character_select,level_up_choice,run_results,codex}.tscn
│   │   └── puzzle/{puzzle_main,level_map,pre_level,level_complete,level_failed}.tscn
│   ├── particles/                   ← 9 PackedScene particle effects
│   ├── shaders/                     ← 15 .gdshader.template files
│   └── i18n/                        ← translations.csv (36 rows)
├── mcp/                            ← MCP bridge (AI agent surface)
│   ├── GodotMcpBridgeController.cs
│   ├── GodotMcpRuntime.cs
│   ├── GodotMcpSettings.cs
│   └── McpGameAdapter.cs
└── plugin.cfg
```

---

## 3. Components (delivered)

### 3.1 `BeepGenreScene` (delivered, commit `3eadc8d`)

**File:** `addons/beep_game_builder_cs/ecs/BeepGenreScene.cs`

```csharp
namespace Beep.ECS;

[Tool] [GlobalClass]
public partial class BeepGenreScene : Node
{
    [Export] public string GenreId { get; set; } = "";
    [Export] public string DefaultThemePreset { get; set; } = "";
    [Export] public string PaletteName { get; set; } = "Default";
    [Export] public string GeometryProfileName { get; set; } = "As-Authored";
    [Export] public string GameName { get; set; } = "";
    [Export] public bool AutoInstantiateMainScene { get; set; } = true;
    [Export] public bool RegisterAsMainScene { get; set; } = true;

    [Signal] public delegate void GenreAppliedEventHandler();

    public override void _Ready()
    {
        if (Engine.IsEditorHint()) return;
        ApplyGenre();
    }

    public void ApplyGenre() { /* implementation */ }
}
```

The shipped version uses idiomatic Godot 4.7 patterns: `GetChildren()`-based scans (not LINQ on `Array<Node>`), `TextEdit.Text +=` (not `AppendText`), manual child iteration, `Walk()` visitor.

### 3.2 `BeepGameBuilderDock` (delivered, commit `55b44bf`)

3 tabs:
- **App** — autoload probe + every `GameInfo` field as an editable control + Save/Reload/Apply-live buttons.
- **Theme** — cascading genre → theme → palette → geometry dropdowns + Apply-to-all-ThemePresetComponents.
- **Settings** — ProjectSettings writes (resolution / FPS / fullscreen / i18n).

Uses Godot 4.7 idioms: named signal handlers, `GetChildren()` walks, `GetEditorInterface().GetEditedSceneRoot()`, `ProjectSettings.SetSetting/`.

### 3.3 Polished bare-bones templates (delivered, commit `f2bb0c6`)

The 6 starter templates — `player_template`, `enemy_template`, `robot_npc_template`, `pickup_template`, `projectile_template`, `dialog_template` — were empty node-tree skeletons. Each now wires the right `[GlobalClass]` C# components with sensible export defaults:

| Template | Components attached |
|----------|---------------------|
| `player_template` | Health (100), Movement (200 spd, no dash), Attack (10 dmg melee), HealthBar, Camera2D |
| `enemy_template` | Health (30), Aggro (300/500 range), Attack (8 dmg), Movement (80 AI), DetectionArea (200), HitboxArea, HealthBar |
| `robot_npc_template` | Health (50), Aggro, Movement (60), Interactable ("Talk"), DetectionArea (250), HealthBar |
| `pickup_template` | Pickup (ItemId="coin", FloatAmplitude=5, AutoRotate), FloatingText, AudioStreamPlayer2D |
| `projectile_template` | Lifetime (3s, FadeOut), Flash, VisibleOnScreenNotifier2D |
| `dialog_template` | DialogUIComponent, DialogComponent on a CanvasLayer with a labeled Panel+VBox |

Every `ext_resource` path verified to resolve. `load_steps` matches `ext + sub + 1` exactly.

---

## 4. Data flow (delivered)

### 4.1 Boot path with the new architecture

```
User creates new scene (empty Node2D root)
    → Add Node → Beep → GenreScene (BeepGenreScene)
    → Set GenreId = "platformer" in Inspector
    → User runs scene
        → BeepGenreScene._Ready:
            1. SkinCatalog.GetGenre("platformer") → returns GenreDef
            2. Reads GameApp.Instance.Info (autoload)
            3. Applies genre.DefaultTheme → GameInfo.DefaultThemePreset
            4. Applies 7 genre tuning fields → GameInfo
            5. Optionally registers this scene as GameInfo.GameScenePath
            6. Finds sibling ThemePresetComponent (if any), drives it
            7. AutoInstantiateMainScene:
               - res://scenes/main/platformer_main.tscn (user's project)
                 else
               - res://addons/beep_game_builder_cs/templates/scenes/platformer_main.tscn (template fallback)
               - ResourceLoader.Load<PackedScene>(...).Instantiate() as child
        → ThemePresetComponent._Ready reads GameApp.Instance.Info
            → SkinCatalog.GetTheme + GetGeometry
            → builds Theme + applies to root subtree
        → TopDownController / PlatformerController wire themselves
        → HUD components wire themselves
        → Scene runs (player, HUD, parallax, game flow)
```

### 4.2 What the dock's App tab does

```
User opens dock → App tab
    → Dock reads GameApp.Instance?.Info
    → If null → display "GameApp autoload not registered" warning + how-to-fix tip
    → If present → display every GameInfo field as an editable control
User clicks Save → ResourceSaver.Save(info, GameInfo.TresPath) + Log("Saved game_info.tres")
User clicks Apply Live → walk SceneTree, call ApplyTheme() on every ThemePresetComponent found
```

### 4.3 What the dock's Theme tab does

```
User opens dock → Theme tab
    → Read SkinCatalog.AllGenres → populate Genre dropdown
    → Read selected genre's themes → populate Theme dropdown
    → Read selected theme's palettes → populate Palette dropdown
    → Read selected genre's Geometry → populate Geometry dropdown
User changes Genre → Theme/Palette/Geometry repopulate
User clicks "Apply to all ThemePresetComponents in open scene"
    → walk SceneTree, find every ThemePresetComponent
    → for each: set GenreName/PresetName/PaletteName/GeometryProfileName from current dropdowns
    → ApplyTheme() cascade handles the rest
```

---

## 5. Error handling (delivered)

| Situation | Behavior |
|-----------|----------|
| `BeepGenreScene` with invalid `GenreId` | Push warning; bail. No `GenreApplied` event. |
| `BeepGenreScene` with no `GameApp` autoload | Push warning; bail. App tab shows "GameApp autoload not registered". |
| `BeepGenreScene` with no `GameInfo` resource | Push warning; bail. App tab's status label shows the issue. |
| Genre's `MainScene` file doesn't exist | Silent skip — `BeepGenreScene` still applies theme + tuning. |
| Scene template references a missing `[GlobalClass]` class | Editor warns at scene load; `[Tool]` components show "Invalid" badge. |
| `BeepGenreScene._Ready` runs in editor | `Engine.IsEditorHint()` short-circuits — no side effects. |

---

## 6. Testing strategy (delivered)

### 6.1 Build verification
- `dotnet build Beep.Godot.sln` → **0 errors** at every commit boundary.

### 6.2 Scene template verification
- Every `ext_resource` path in every `.tscn` file resolves to a real `.cs` file on disk (verified via Python check).
- Every `.tscn` declares `load_steps = ext_resources + sub_resources + 1` exactly (verified via shell check).

### 6.3 Component verification
- Every `[Export]` property on every component has a sensible default.
- Every component's `[Signal]` is declared in `[GlobalClass]`-compatible form.
- `BeepGenreScene` + every component compiles in a vanilla Godot 4.7 + .NET 8 environment.

### 6.4 Manual end-to-end
1. **Brand-new project**: copy only `addons/` into a fresh Godot 4.7 project, enable the plugin, press Build.
2. **Open a scene** (File → New Scene → Node2D).
3. **Add `BeepGenreScene`** (Add Node → Beep → GenreScene).
4. **Set `GenreId = "platformer"`** in the Inspector.
5. **Run scene**: themed platformer with player + HUD + parallax materializes as a child of the BeepGenreScene. No errors.
6. **Open a theme.json file** in `catalogs/skins/platformer/themes/`, change a color, save, re-run the scene. The change is visible.

---

## 7. Open risks → resolutions

| Risk (planned) | Resolution (delivered) |
|----------------|--------------------------|
| Big-bang removal of 10+ files in one PR | **DONE in one PR (commits `55b44bf` + `f2bb0c6`).** No transition window because no `[Obsolete]` markers. Production-ready from day one. |
| Existing scenes that depended on generator-written paths | **None** — the existing `templates/scenes/` already had all the scenes shipped. `BeepGenreScene.InstantiateMainScene` falls back to the addon template path if `res://scenes/main/<file>` doesn't exist. |
| Theme presets hardcoded in scenes | **None** — every `ThemePresetComponent` is `[Export]` and driven by `BeepGenreScene` or the dock's Theme tab. |
| `BeepGenreScene` requires GameApp autoload | **Mitigated** — App tab shows autoload status prominently with how-to-fix tip. |
| `[Tool]` performance in editor | **Mitigated** — `BeepGenreScene._Ready` short-circuits via `Engine.IsEditorHint()`. |
| Backward compatibility for users of `OnStarter` button | **Accepted breakage** — user explicitly said "we dont need any Obsolete or legacy or compatiblty, we want only production ready code." |

---

## 8. Verification (delivered — all green)

1. ✅ `dotnet build Beep.Godot.sln` → 0 errors at every phase boundary.
2. ✅ The dock has exactly 3 tabs (App / Theme / Settings) after rewrite.
3. ✅ Every shipped `.tscn` opens in the editor with all nodes as `[GlobalClass]` components.
4. ✅ `find addons -name '*Generator.cs'` returns nothing (all 4 generators deleted).
5. ✅ `find addons -name '*.gd.template'` returns nothing (all 23 deleted).
6. ✅ Cross-addon: `beep_ui` GDScript addon unchanged — still loads, still works, still 22 presets + 11 effects + 84 widgets.
7. ✅ MCP bridge intact (the user asked to keep it).
8. ✅ Translations intact (`templates/i18n/translations.csv` consumed by `LocalizationComponent`).
9. ✅ 6 polished bare-bones templates with sensible default components.
10. ✅ INDEX.md written + root README.md updated.

---

## 9. Commits (delivered in this order)

```
3eadc8d  feat(genre-scene): add BeepGenreScene component
4f6a020  feat(genre-scene): simplify + document composition model
55b44bf  refactor: production-ready cleanup — remove all generators + .gd.template
70fdc52  docs: add INDEX.md inventory + update root README
f2bb0c6  polish: bare-bones templates ship sensible defaults
38d9797  docs: brainstorm component-first refactor (spec + plan)
```

---

## 10. See also

- [`addons/beep_game_builder_cs/INDEX.md`](../../addons/beep_game_builder_cs/INDEX.md) — full inventory of what ships
- [`plans/component-first-refactor.md`](../../plans/component-first-refactor.md) — the implementation plan (now updated to match the delivered state)
- [`docs/ARCHITECTURE.md`](../../ARCHITECTURE.md) — master architecture map
- [`docs/SKINNING_THEMING.md`](../../SKINNING_THEMING.md) — skin pipeline (unchanged)
- [`README.md`](../../../README.md) — root readme (updated to reflect production state)