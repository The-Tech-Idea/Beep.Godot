# Component-First Refactor — Implementation Plan

**Status:** ⬜ Not Started | ⬜ Phase 1 | ⬜ Phase 2 | ⬜ Phase 3 | ⬜ Phase 4 | ⬜ Phase 5 | ⬜ Phase 6

> Remove every `*Generator` class from `beep_game_builder_cs`. Ship 100% component-based scene templates. Thin the dock to App + Theme + Settings. Add a single `BeepGenreScene` component for genre-aware wiring. Strict migration: new components first, old generators marked `[Obsolete]`, then deleted.

---

## Context

The C# addon `beep_game_builder_cs` has **~10 generator classes** (`BeepGenreGenerator`, `BeepSceneGenerator`, `BeepScriptGenerator`, `BeepShaderGenerator`, `BeepTweenGenerator`, `BeepParticleGenerator`, `BeepProjectileGenerator`, `BeepProjectGenerator`, `BeepInputMapGenerator`, `BeepProjectDefaults`) that produce scenes, scripts, shaders, tweens, particles, projectiles, folder scaffolds, input maps, and project settings on demand from editor buttons. Plus an `OnStarter` method on the dock and the `BeepGameBuilderDock.Genres.cs` partial. All are antipatterns in modern Godot 4.7.

The remainder of `core/` (~28 utility/data classes like `BeepFileUtils`, `BeepValidator`, `BeepKeybindManager`, `BeepStateMachine`, `GameInfo`, etc.) stays — those are runtime helpers, not generators.

The addon also has **~155 `[GlobalClass]` component classes** (102 in `ecs/`, 53 in `ecs/ui/`). Godot's native **Add Node** dialog already categorizes them and lets users drag-and-drop them into scenes. The dock's job reduces to **project-level state** (GameApp + GameInfo + theme settings), NOT component discovery.

### Goals

- **No new browser UI** — Godot's Add Node dialog is already the component registry.
- **All scenes are editable `.tscn` files** the user owns.
- **Strict migration**: build new prefab scenes first, mark old generators `[Obsolete]` (don't delete yet), THEN delete.
- **The dock becomes thin**: 3 tabs (App / Theme / Settings). No more Project / Scenes / Characters / Shaders / Tweens / Particles / Projectiles / Validation / Export / Genres.

### Non-goals

- No new GDScript UI — only what Godot already provides.
- No scripted setup phase — users drag-and-drop prefabs manually (or copy them via right-click).
- No cloud / marketplace / shareable themes.
- No new branded component taxonomy — the existing `BeepXxxComponent` naming stays.

---

## Master TODO Tracker

| # | Phase | Files | Description |
|---|---|---|---|
| 1 | `BeepGenreScene` component | `addons/beep_game_builder_cs/ecs/BeepGenreScene.cs` (NEW) | Single `[Tool] [GlobalClass]` Node that reads `GenreId` export → at `_Ready` resolves `genre.json`, applies tuning + theme to `GameApp.Info`, auto-instantiates `genre.MainScene` as a child, drives a sibling `ThemePresetComponent`. Replaces `BeepGenreGenerator.CreateProject`. |
| 2 | README + usage docs | `addons/beep_game_builder_cs/templates/README.md` (NEW), `README.md` (MODIFY) | The existing `templates/scenes/{main_menu,pause_menu,settings_menu,hud,game_over}.tscn` + `templates/scenes/{platformer,topdown,shooter,puzzle}/<genre>_main.tscn` ARE the prefabs (already component-based — verified). One README documents: drop a `BeepGenreScene` into a scene, set `GenreId`, run. No new `.tscn` files needed. |
| 3 | Mark generators `[Obsolete]` | `addons/beep_game_builder_cs/core/*.cs` (MODIFY) | All `Beep*Generator`, plus `OnStarter` in `ui/`. Mark `[Obsolete("Use BeepGenreScene + templates/scenes/ instead.")`. **No deletion yet.** |
| 4 | Dock rewrites | `addons/beep_game_builder_cs/ui/BeepGameBuilderDock.cs` (REWRITE), `ui/BeepGameBuilderDock.Genres.cs` (DELETE), `ui/BeepGameBuilderDock.Starter.cs` (DELETE) | 3 tabs: App / Theme / Settings. Existing 11 tabs removed. |
| 5 | Delete generators + old `.gd.template` scripts | `addons/beep_game_builder_cs/core/BeepGenreGenerator.cs` (DELETE), `BeepProjectGenerator.cs` (DELETE), `BeepScriptGenerator.cs` (DELETE), `BeepSceneGenerator.cs` (DELETE), `BeepShaderGenerator.cs` (DELETE), `BeepTweenGenerator.cs` (DELETE), `BeepParticleGenerator.cs` (DELETE), `BeepProjectileGenerator.cs` (DELETE), `BeepInputMapGenerator.cs` (DELETE), `BeepProjectDefaults.cs` (DELETE), plus `templates/scripts/*.gd.template` (DELETE) | Final cleanup. **Only after Phase 4 ships + users confirm they don't need the old path.** Keep `templates/scenes/*.tscn` (used by `BeepGenreScene.InstantiateMainScene`). **Keep** `templates/i18n/translations.csv` — it's the localized-strings data consumed at runtime by the existing `LocalizationComponent` via `TranslationServer.AddTranslation`. |

**Total:** 1 new C# class, ~15 new .tscn files, ~15 modified .cs files, 11 C# files deleted, ~40 template files deleted, 1 new index.json, 1 README update.

---

## Phase 1 — `BeepGenreScene` component (the single replacement for `BeepGenreGenerator`)

**Goal:** One component that, when dropped into a scene root, applies a genre's full wiring (tuning, scene paths, theme defaults) at runtime.

### File to create

`addons/beep_game_builder_cs/ecs/BeepGenreScene.cs` (new, ~120 lines)

### Exports

```csharp
[Tool] [GlobalClass]
public partial class BeepGenreScene : Node
{
    /// <summary>Genre id (must match a folder under catalogs/skins/<genre>/).</summary>
    [Export] public string GenreId { get; set; } = "";

    /// <summary>Optional override — defaults to the genre's default_theme.</summary>
    [Export] public string DefaultThemePreset { get; set; } = "";

    /// <summary>Optional override — defaults to "Default".</summary>
    [Export] public string PaletteName { get; set; } = "Default";

    /// <summary>Optional override — defaults to the genre's geometry profile.</summary>
    [Export] public string GeometryProfileName { get; set; } = "As-Authored";

    /// <summary>Optional override — game name shown in UI (defaults to GameInfo.GameName).</summary>
    [Export] public string GameName { get; set; } = "";

    /// <summary>If true, automatically registers this scene's main path as GameInfo.GameScenePath on _Ready.</summary>
    [Export] public bool RegisterAsMainScene { get; set; } = true;
}
```

### `_Ready` pipeline

```csharp
public override void _Ready()
{
    if (Engine.IsEditorHint()) return;          // [Tool] but not at edit time

    var genre = Beep.ECS.UI.SkinCatalog.GetGenre(GenreId);
    if (genre == null)
    {
        GD.PushWarning($"[BeepGenreScene] Genre '{GenreId}' not found in skin catalog.");
        return;
    }

    var app = GameApp.Instance;
    if (app?.Info == null) return;               // GameApp autoload must be present

    // 1. Apply genre defaults to GameInfo.
    app.Info.Genre = GameInfo.GenreFromId(GenreId);
    if (string.IsNullOrEmpty(DefaultThemePreset)) app.Info.DefaultThemePreset = genre.DefaultTheme;
    else app.Info.DefaultThemePreset = DefaultThemePreset;
    if (!string.IsNullOrEmpty(PaletteName)) app.Info.PaletteName = PaletteName;
    if (!string.IsNullOrEmpty(GeometryProfileName)) app.Info.GeometryProfileName = GeometryProfileName;

    // 2. Apply genre tuning (gravity, jump_velocity, etc.) to GameInfo.
    ApplyTuning(app.Info, genre);

    // 3. Optionally register this scene as the main scene.
    if (RegisterAsMainScene && Owner is Node owner)
    {
        app.Info.GameScenePath = owner.SceneFilePath;
        app.Info.MainMenuPath = app.Info.MainMenuPath;   // unchanged
    }

    // 4. Find sibling ThemePresetComponent and drive it from the genre.
    var theme = GetParent()?.GetChildren()
        .OfType<Beep.ECS.UI.ThemePresetComponent>().FirstOrDefault();
    if (theme != null)
    {
        theme.GenreName = GenreId;
        theme.PresetName = app.Info.DefaultThemePreset;
        theme.PaletteName = app.Info.PaletteName;
        theme.GeometryProfileName = app.Info.GeometryProfileName;
    }

    EmitSignal(SignalName.GenreApplied);
}

private static void ApplyTuning(GameInfo info, Beep.ECS.UI.GenreDef genre)
{
    if (genre.Tuning.Count == 0) return;
    if (genre.Tuning.TryGetValue("gravity", out var g)) info.Gravity = g.AsSingle();
    if (genre.Tuning.TryGetValue("jump_velocity", out var j)) info.JumpVelocity = j.AsSingle();
    if (genre.Tuning.TryGetValue("move_speed", out var m)) info.MoveSpeed = m.AsSingle();
    if (genre.Tuning.TryGetValue("fire_rate", out var f)) info.FireRate = f.AsSingle();
    if (genre.Tuning.TryGetValue("grid_width", out var gw)) info.GridWidth = gw.AsInt32();
    if (genre.Tuning.TryGetValue("grid_height", out var gh)) info.GridHeight = gh.AsInt32();
    if (genre.Tuning.TryGetValue("target_score", out var ts)) info.TargetScore = ts.AsInt32();
}

[Signal] public delegate void GenreAppliedEventHandler();
```

### Validation rules

- Genre-aware scene wiring happens **once at scene load** (`_Ready`). No re-trigger on property change. If the user wants to retune mid-game, they call `ApplyGenre()` (made public) directly.
- If `GameApp` autoload is missing, push warning and bail — the user should set up the autoload (Phase 5 docs explain).
- `[Tool]` so the inspector shows the genre dropdown in the editor; `_Ready` short-circuits via `Engine.IsEditorHint()`.

### Verification

1. Drop `BeepGenreScene` into a scene root.
2. Set `GenreId = "platformer"`.
3. Run scene.
4. `GameApp.Instance.Info.DefaultThemePreset` should equal `"cartoon"` (the genre's default).
5. `GameApp.Instance.Info.Gravity` should equal `980` (from `genre.json` tuning).

---

## Phase 2 — Document the genre-driven composition model

**Goal:** The existing `templates/scenes/` tree already serves as the prefab catalog. `BeepGenreScene` auto-instantiates the right scenes. We just need one README documenting the model.

### Reality check

The existing `templates/scenes/` IS the prefab set:

```
addons/beep_game_builder_cs/templates/scenes/
├── main_menu.tscn              ← UI scenes (5 files, component-based, already shipped)
├── pause_menu.tscn
├── settings_menu.tscn
├── hud.tscn
├── game_over.tscn
├── player_template.tscn
├── enemy_template.tscn
├── robot_npc_template.tscn
├── pickup_template.tscn
├── dialog_template.tscn
├── projectile_template.tscn
├── platformer/
│   ├── platformer_main.tscn   ← genre-specific main scene (already component-based)
│   ├── level_select.tscn
│   └── level_results.tscn
├── topdown/
│   └── topdown_main.tscn
├── shooter/
│   └── shooter_main.tscn
└── puzzle/
    ├── puzzle_main.tscn
    ├── level_map.tscn
    ├── pre_level.tscn
    ├── level_complete.tscn
    └── level_failed.tscn
```

Every shipped `.tscn` already uses `[GlobalClass]` C# components (`PlatformerController`, `HealthComponent`, `GameFlowComponent`, `ParallaxComponent`, `ThemePresetComponent`, `GameInfoBinder`, etc.). The earlier hand-written `preset_<name>.gd` scripts in `beep_ui` and the `.gd.template` files in `templates/scripts/` are the only non-component artefacts — and `.gd.template` files get deleted in Phase 5.

### Composition rules (already enforced — verify only)

1. **Every attached script is a `[GlobalClass]` C# class** that ships in the addon. No inline `.gd` scripts.
2. **Every styled scene has a `ThemePresetComponent` sibling** — driven from `GameApp.Instance.Info`, no hardcoded preset.
3. **Every UI scene has a `GameInfoBinder`** — so editing `game_info.tres` once updates every menu.

### File to create

`addons/beep_game_builder_cs/templates/README.md`

```markdown
# Beep Game Builder templates

## How it works

Drop a `BeepGenreScene` component into a scene root, set `GenreId` in the
inspector. At runtime it reads `catalogs/skins/<genreId>/genre.json` and:

1. Applies the genre's default theme + tuning to `GameApp.Info`.
2. Loads `<genreId>_main.tscn` from this folder and adds it as a child.
3. Drives a sibling `ThemePresetComponent` from the resolved theme.

No buttons, no generators. The scenes below are already wired.

## What's here

| Path | Used by |
|------|---------|
| `main_menu.tscn`           | Shared UI — main menu |
| `pause_menu.tscn`          | Shared UI — pause overlay |
| `settings_menu.tscn`       | Shared UI — settings |
| `hud.tscn`                 | Shared UI — in-game HUD |
| `game_over.tscn`           | Shared UI — game over screen |
| `player_template.tscn`     | Generic player (replace with genre-specific) |
| `platformer/platformer_main.tscn` | Loaded by `BeepGenreScene` when GenreId="platformer" |
| `topdown/topdown_main.tscn`       | Loaded by `BeepGenreScene` when GenreId="topdown" |
| `shooter/shooter_main.tscn`       | Loaded by `BeepGenreScene` when GenreId="shooter" |
| `puzzle/puzzle_main.tscn`         | Loaded by `BeepGenreScene` when GenreId="puzzle" |
| `platformer/level_select.tscn`    | Navigated to from `GameFlowComponent` |
| `puzzle/level_map.tscn`           | Navigated to from `GameFlowComponent` |
| ...                              | One level scene per genre, navigated via `genre.json#nav_wiring` |

## To add a new genre

1. Drop a folder under `addons/beep_game_builder_cs/catalogs/skins/<your_genre>/`.
2. Add `<your_genre>.json`, `geometry.json`, `themes/<theme>/theme.json` per the
   schemas in `docs/FILE_FORMATS.md`.
3. Add `<your_genre>_main.tscn` here under `templates/scenes/<your_genre>/`.
4. The `BeepGenreScene` component with `GenreId = "<your_genre>"` will pick it
   up automatically.
```

### Validation rules

- One README only — no new code files, no new scenes.
- Existing scenes verified component-based by Phase 0 survey.

### Verification

1. Open a scene that contains `BeepGenreScene` with `GenreId = "platformer"`.
2. Run → the player + HUD + parallax + camera appear as children of `BeepGenreScene`,
   themed, with `GameApp.Info` populated from `genre.json`.
3. Verify the same with `GenreId = "puzzle"` — the puzzle board appears instead.

---

## Phase 3 — Mark old generators `[Obsolete]`

**Goal:** Signal deprecation WITHOUT breaking anything. The old paths still work; new code paths warn at compile time.

### Files to modify

For each `Beep*Generator` / `Beep*Composer` / `BeepOnStarter` file, add `[Obsolete]` attribute to every public method, keep the implementation intact:

```csharp
// In BeepGenreGenerator.cs
[Obsolete("Use BeepGenreScene component (addons/beep_game_builder_cs/ecs/BeepGenreScene.cs) instead.")]
public static List<string> CreateProject(string genreId, GameInfo info, bool overwrite = false) { ... }

[Obsolete("Use BeepGenreScene component instead.")]
public static List<string> StampProject(GameInfo info, Beep.ECS.UI.GenreDef genre, bool overwrite) { ... }
```

### Files affected

- `core/BeepGenreGenerator.cs` — CreateProject, StampProject, ApplyTuning (private, no attribute needed)
- `core/BeepProjectGenerator.cs` — CreateStandardFolders
- `core/BeepScriptGenerator.cs` — every CreateXxx method
- `core/BeepSceneGenerator.cs` — every CreateXxx method
- `core/BeepShaderGenerator.cs` — every public method
- `core/BeepTweenGenerator.cs` — every public method
- `core/BeepParticleGenerator.cs` — every public method
- `core/BeepProjectileGenerator.cs` — every public method
- `core/BeepInputMapGenerator.cs` — SetupDefaultInput
- `core/BeepProjectDefaults.cs` — ConfigureDefaults, SetMainScene (ApplyFromGameInfo stays — it's the runtime path)
- `ui/BeepGameBuilderDock.cs` — OnStarter method, AddScenesTab, AddCharactersTab, AddShadersTab, AddTweensTab, AddParticlesTab, AddProjectilesTab, AddComponentsTab, AddValidationTab, AddExportTab
- `ui/BeepGameBuilderDock.Genres.cs` — entire file (everything becomes obsolete)
- `ui/BeepGameBuilderDock.Starter.cs` — if exists

### What stays NOT obsolete

- `BeepFileUtils` (utility, not a generator — but keep as-is)
- `BeepValidator` (utility, not a generator — keep; or move to a BeepProjectHealthComponent if the validator should be runtime-callable too)
- `BeepExportChecklist` (utility — keep as-is)
- `BeepKeybindManager` + `KeybindManagerComponent` — runtime utility, not a generator
- `BeepStateMachine` + `StateMachineComponent` — runtime utility, not a generator
- `BeepServiceLocator` — runtime utility
- `BeepProceduralAnim` + `BeepNoiseGenerator` — runtime utility
- `GameInfo` (the resource type itself — it's used at runtime)

### Verification

1. Build → `dotnet build Beep.Godot.sln`. Zero errors.
2. Open dock → the obsolete tab buttons (Scenes, Characters, Shaders, etc.) still work — they print deprecation warnings in the Output panel.
3. Existing scenes with old generators still compile (any scene using BeepGenreGenerator via C# gets a warning, but still runs).

---

## Phase 4 — Dock rewrites

**Goal:** Thin the dock to **3 tabs** — App / Theme / Settings. Existing 11 tabs removed.

### Files affected

- `ui/BeepGameBuilderDock.cs` — major rewrite (delete obsolete tab methods, add new 3-tab structure)
- `ui/BeepGameBuilderDock.Genres.cs` — delete (functionality absorbed into BeepGenreScene + dock's Theme tab)
- `ui/BeepGameBuilderDock.Starter.cs` — delete (if exists)

### New layout

```
┌─ Beep Game Builder ────────────────────┐
│ [App] [Theme] [Settings]               │  ← TabContainer
│                                        │
│ (selected tab content)                 │
│                                        │
└────────────────────────────────────────┘
```

### `App` tab

Shows the GameApp autoload state and a quick-edit grid for the most-used GameInfo fields:
- Game name, version, developer
- Genre (OptionButton — picks from SkinCatalog)
- Default theme preset
- Palette name
- Resolution width × height
- Target FPS
- Pixel art (checkbox)
- Game scene path (read-only label + "Browse" button that opens the FileSystem dock to the path)

Buttons:
- **Save to game_info.tres** — `ResourceSaver.Save(app.Info, GameInfo.TresPath)`.
- **Reload from disk** — `ResourceLoader.Load<GameInfo>(GameInfo.TresPath)`.
- **Apply changes live** — walks SceneTree, calls `theme.ApplyTheme()` on every `ThemePresetComponent` found.

### `Theme` tab

Quick view of the active genre/theme/palette/geometry + ability to swap and apply at runtime:
- Genre dropdown (SkinCatalog.AllGenres)
- Theme dropdown (repopulates when Genre changes)
- Palette dropdown (repopulates when Theme changes)
- Geometry dropdown (per-genre)
- Background mode (None / Stretch / Tile)
- "Apply Theme to All Components" button — re-applies theme across the current scene tree.
- "Show theme catalog" — opens FileSystem dock at `addons/beep_game_builder_cs/catalogs/skins/`.

### `Settings` tab

Project-level configuration:
- Autoload status (read-only label: "✅ GameApp registered" / "⚠ Not registered — see README")
- Resolution + FPS + pixel-art filter (writes to ProjectSettings)
- Display/window/stretch settings (writes to ProjectSettings)
- Main scene path
- Internationalization locale + translations file (read-only path label)

### Methods to remove from `BeepGameBuilderDock.cs`

```csharp
// REMOVE:
private void AddProjectTab(...)            // → not obsolete because tab removed
private void AddScenesTab(...)
private void AddCharactersTab(...)
private void AddShadersTab(...)
private void AddTweensTab(...)
private void AddParticlesTab(...)
private void AddProjectilesTab(...)
private void AddComponentsTab(...)
private void AddValidationTab(...)
private void AddExportTab(...)
private void OnStarter()                     // the legacy "Generate Starter Project (All)" button
```

### Methods to ADD

```csharp
private void AddAppTab(TabContainer tabs)
private void AddThemeTab(TabContainer tabs)
private void AddSettingsTab(TabContainer tabs)
```

### Validation rules

- New dock renders correctly in the editor with only 3 tabs.
- App tab populates from `GameApp.Instance?.Info` if available, otherwise from defaults.
- Theme tab reacts live: changing Genre repopulates Theme dropdown.
- Settings tab writes survive `ProjectSettings.Save()`.

### Verification

1. Reload editor → dock shows App / Theme / Settings only.
2. Click App → GameApp autoload status visible.
3. Click Theme → switch to a different genre; Theme dropdown updates.
4. Click Settings → change resolution → save → reopen project → resolution persists.

---

## Phase 5 — Index documentation + README

**Goal:** Make prefabs discoverable. Users should be able to:
1. Open Godot's FileSystem dock → navigate to `addons/beep_game_builder_cs/templates/prefabs/`.
2. See `index.json` listing every prefab.
3. Drag any `.tscn` into their project's `scenes/` folder.
4. Open the scene → it's a working, themed, component-composed starter.

### File to create

`addons/beep_game_builder_cs/templates/prefabs/index.json`

```json
{
  "version": 1,
  "category_count": 4,
  "total_prefabs": 38,
  "categories": [
    {
      "name": "ui",
      "display_name": "UI Scenes",
      "description": "Full-rect Control trees with ThemePresetComponent siblings + GameInfoBinder nodes.",
      "prefabs": [
        {
          "id": "main_menu",
          "path": "ui/main_menu.tscn",
          "display_name": "Main Menu",
          "description": "Title label + Start/Options/Quit buttons. Drops into the root of a UI scene.",
          "requires": ["BeepGameAppWrapper", "BeepGenreScene", "ThemePresetComponent"]
        }
        // ...
      ]
    }
    // ...
  ]
}
```

### File to create

`addons/beep_game_builder_cs/templates/prefabs/README.md`

Quick-start:
1. Browse `index.json` for a category.
2. Drag the `.tscn` from FileSystem dock into your `res://scenes/` folder.
3. Open it — it's a working prefab, all components editable.
4. For full games: drop `worlds/main_scene_<genre>.tscn` into your project root scene slot (Project Settings → Application → Run → Main Scene).

### File to modify

`addons/beep_game_builder_cs/README.md` (root-level addon readme)

Replace generator tab descriptions with:
> **Quick start:** Drop a `BeepGenreScene` component into your scene root, set its `GenreId`, and the addon wires the rest. Drag prefabs from `templates/prefabs/{ui,gameplay,worlds,systems,shared}/` for individual scenes. See `templates/prefabs/index.json`.

### Verification

- Open `templates/prefabs/index.json` in a JSON viewer — schema valid, every prefab path exists.
- Drag `prefabs/ui/main_menu.tscn` into a new project, set it as a scene, run — themed menu appears.

---

## Phase 6 — Delete obsolete files (final cleanup)

**Goal:** Remove the old paths once Phase 5 ships and users confirm they don't need them.

### Files to DELETE

```
addons/beep_game_builder_cs/core/BeepGenreGenerator.cs
addons/beep_game_builder_cs/core/BeepProjectGenerator.cs
addons/beep_game_builder_cs/core/BeepScriptGenerator.cs
addons/beep_game_builder_cs/core/BeepSceneGenerator.cs
addons/beep_game_builder_cs/core/BeepShaderGenerator.cs
addons/beep_game_builder_cs/core/BeepTweenGenerator.cs
addons/beep_game_builder_cs/core/BeepParticleGenerator.cs
addons/beep_game_builder_cs/core/BeepProjectileGenerator.cs
addons/beep_game_builder_cs/core/BeepInputMapGenerator.cs
addons/beep_game_builder_cs/core/BeepProjectDefaults.cs
addons/beep_game_builder_cs/ui/BeepGameBuilderDock.Genres.cs
addons/beep_game_builder_cs/ui/BeepGameBuilderDock.Starter.cs (if exists)
addons/beep_game_builder_cs/templates/scenes/*.tscn         (all 11+ files)
addons/beep_game_builder_cs/templates/scripts/*.gd.template  (all 15+ files)
addons/beep_game_builder_cs/templates/i18n/translations.csv   (BeepGenreGenerator copied this)
```

### `[Obsolete]` removals

After delete, remove `[Obsolete]` attributes from `BeepGameBuilderDock.cs` since the obsolete code is gone.

### Build verification

```bash
dotnet build Beep.Godot.sln  # expect 0 errors
```

### Runtime verification

1. Brand new Godot project that includes the addon.
2. Drop `prefabs/worlds/main_scene_platformer.tscn` as the main scene.
3. Run — themed platformer scene with player + HUD + genre tuning.
4. No generator classes are referenced anywhere.

---

## Cross-cutting: Reused existing helpers

- **`Beep.ECS.UI.SkinCatalog.GetGenre()` / `GetTheme()` / `GetGeometry()`** — read by `BeepGenreScene` for defaults.
- **`Beep.ECS.UI.ThemePresetComponent.ApplyTheme()`** — driven by `BeepGenreScene` in `_Ready`.
- **`GameApp.Instance?.Info`** — read by `BeepGenreScene` for current state.
- **`GameInfo.GenreFromId()`** — string ↔ enum bridge.
- **`ResourceSaver.Save(GameInfo, "res://game_info.tres")`** — App tab Save button.
- **`ProjectSettings.Save()`** — Settings tab.

## Existing utilities explicitly NOT touched

- **All `~155` `[GlobalClass]` components** in `ecs/` and `ecs/ui/` — none modified.
- **`SkinCatalog`, `ThemePresetComponent`, `FileThemePreset`, `PaletteTintedPreset`, `UISkin`, `GeometryProfile`, `ShapeOverrides`, `ColorPalette`, `IThemePreset`** — none modified.
- **`GameApp`, `GameInfo`, `GameInfoBinder`** — none modified (only consumed by the new component).
- **`BeepFileUtils`, `BeepValidator`, `BeepExportChecklist`, `BeepKeybindManager`, `BeepStateMachine`, `BeepServiceLocator`, `BeepProceduralAnim`** — utility classes; stay as-is (not generators).
- **`beep_ui` (GDScript addon)** — entirely separate; not touched.
- **All skin JSON files** (`catalogs/skins/**/*.json`) — not touched.

---

## Critical Files

| File | Phase | Action |
|------|-------|--------|
| `addons/beep_game_builder_cs/ecs/BeepGenreScene.cs` | 1 | NEW |
| `addons/beep_game_builder_cs/templates/prefabs/**` | 2 | NEW (~15 .tscn files + README.md) |
| `addons/beep_game_builder_cs/core/*.cs` (11 files) | 3 | MODIFY — `[Obsolete]` attributes |
| `addons/beep_game_builder_cs/ui/BeepGameBuilderDock.cs` | 3, 4 | REWRITE — 3 tabs |
| `addons/beep_game_builder_cs/ui/BeepGameBuilderDock.Genres.cs` | 3, 4 | DELETE (after Phase 3 ships) |
| `addons/beep_game_builder_cs/templates/prefabs/index.json` | 5 | NEW |
| `addons/beep_game_builder_cs/templates/prefabs/README.md` | 5 | NEW |
| `addons/beep_game_builder_cs/README.md` | 5 | MODIFY |
| `addons/beep_game_builder_cs/core/Beep*Generator.cs` (10 files) | 6 | DELETE |
| `addons/beep_game_builder_cs/core/BeepProjectDefaults.cs` | 6 | DELETE |
| `addons/beep_game_builder_cs/templates/scenes/**` | 6 | DELETE |
| `addons/beep_game_builder_cs/templates/scripts/**` | 6 | DELETE |
| `addons/beep_game_builder_cs/templates/i18n/translations.csv` | 6 | DELETE |

---

## Verification (full suite)

1. `dotnet build Beep.Godot.sln` → 0 errors at every phase boundary.
2. **Phase 1:** `BeepGenreScene` dropped in a scene runs → `GameApp.Instance.Info` reflects genre defaults.
3. **Phase 2:** Every prefab opens in the editor with all nodes showing as `[GlobalClass]` components. Running any prefab scene works without code generation.
4. **Phase 3:** Building with old code paths still emits `[Obsolete]` warnings, doesn't fail.
5. **Phase 4:** Dock has exactly 3 tabs (App / Theme / Settings). No generator buttons remain.
6. **Phase 5:** `index.json` validates. README points at prefabs.
7. **Phase 6:** Brand-new project with the addon only (no BeepGenreGenerator.cs anywhere) → runs the platformer scene correctly. No `[Obsolete]` warnings anywhere.
8. **Cross-addon:** `beep_ui` GDScript dock still loads (independent of any of these changes).