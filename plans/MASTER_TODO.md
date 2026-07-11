# Master TODO Tracker — Genre-Based Scene Template System

> Tracks the implementation of genre-based starter scene templates for
> `addons/beep_game_builder_cs/`. See `plans/genre-templates/` for per-stage detail.
>
**Goal:** A developer clicks "New Platformer Project" and immediately gets a working
> **Main Menu → Game → Pause → Game Over** loop with themed UI, a central `GameInfo`
> node, and wired navigation — no manual scene/menu setup.
>
**Decisions:**
> - **Genres:** Platformer, Top-Down/Adventure, Arcade/Shooter, Puzzle
> - **Build:** Hybrid (hand-authored `.tscn` + C# orchestrator)
> - **GameInfo:** C# `[GlobalClass] Resource` autoload (read by GDScript via `/root/GameInfo`)
> - **Controller/menu scripts:** GDScript
> - **Theming & widgets:** use the **`beep_ui` GDScript addon** — `BeepThemeApplier`
>   (not the C# ThemePresetComponent; it has the container-drift bug and no widgets)
>   and `BeepWidgetFactory` (84 themed widgets) + `BeepUIEffect`. Scene templates
>   require `beep_ui` enabled; both addons ship from this repo.

---

## Progress

- [x] **Stage 0 — Planning** — staged docs + this tracker created.
- [x] **Stage 1 — Cleanup** — stripped GDScript controller copy from BeepGenreGenerator; deleted dead dock stub machinery (4 UI tabs, ALL_* arrays, Gen/GenHud/GenCanvas/GenAny), BeepUIGenerator, and the AddUITabs call. Components now show in Godot's native Add Node menu. Builds clean.
- [x] **Stage 2 — Flow/menu components** — `MenuComponent`, `NavigationComponent`, `PauseComponent`, `GameFlowComponent`, `HudComponent` (5). Foundation of the menu→game→pause→game-over loop.
- [x] **Stage 3 — UI components** — 6 HUD (Crosshair, Minimap, ScoreDisplay, MatchTimer, NotificationStack, InteractionPrompt) + 6 Canvas/FX (SafeArea, SceneTransition, AnimatedNumber, ChromaticAberration, Vignette, InventoryGrid) + 2 Core-host (DataBinderHost, KeybindManager) + 4 `[GlobalClass]` refactors (BeepDataGrid/FormBuilder/TreeView/Dropdown). 16 deliverables.
- [x] **Stage 5 — Shared UI scenes** — `.tscn` main_menu/pause_menu/settings_menu/game_over/hud (button nodes + MenuComponent + NavigationComponent + ThemePresetComponent + PauseComponent + HudComponent).
- [x] **Stage 6 — Platformer** — `platformer_main.tscn` (ParallaxBackground, TileMapLayer, Player+PlatformerController+Health+Camera2D, checkpoints/moving platforms/enemies/hazards/pickups, HUD).
- [x] **Stage 7 — Top-Down** — `topdown_main.tscn` (Ground+Walls TileMapLayer, NavigationRegion2D, Player+TopDownController+Interactable+Health, NPCs/Enemies/TransitionZones/Items, dialog overlay, HUD).
- [x] **Stage 8 — Shooter** — `shooter_main.tscn` + new `ShooterController` component (mouse-aim + fire cooldown, reads GameInfo tuning), Projectiles pool, EnemySpawner, WorldBounds, HUD.
- [x] **Stage 9 — Puzzle** — `puzzle_main.tscn` + new `Match3BoardComponent` (grid, swap→match→clear→gravity→refill cascade, scoring signals), HUD.
- [x] **Stage 10 — Genre dock tab + validate** — Genre Templates section in Project tab (4 buttons + game-name field → StampGenre); `dotnet build` 0 errors; README updated with Genre Templates section; tracker closed.
- [x] **Stage 11 — Genre-specific UI scenes** — researched real genre-leading games (Mario/Celeste, Zelda ALttP/Stardew, Gungeon/Vampire Survivors, Candy Crush) and built the genre-defining screens each genre actually ships:
  - **Platformer:** `level_select.tscn` (world-map tabs + level grid, stars, locked nodes), `level_results.tscn` (time/score/coins/deaths + stars + Next/Retry/Map).
  - **Top-Down:** `pause_subscreen.tscn` (the genre-defining tabbed subscreen — Inventory grid + equip slots, Map, Quest list, Status, Save).
  - **Shooter:** `character_select.tscn` (4 class cards: Marine/Pilot/Hunter/Bruiser), `level_up_choice.tscn` (survivors 3-card upgrade pick), `run_results.tscn` (time/floor/kills/score/gold/unlocks), `codex.tscn` (arsenal grid, locked items greyed).
  - **Puzzle:** `level_map.tscn` (vertical zig-zag node path, stars per node, lives/gold counter), `pre_level.tscn` (objective, star thresholds, moves, boosters, Play), `level_complete.tscn` (stars banner, score, high-score, Next/Retry/Map), `level_failed.tscn` (out-of-moves, retry-with-bonus, retry-costs-life, quit).
  - Wired into `BeepGenreGenerator.CopyGenreUiScenes` + `genre_templates.json`. Builds clean (0/0).
- [x] **Stage 12 — GameInfo centralization** — every UI scene now reads from the single `GameInfo` autoload at runtime (no baked literals):
  - New `GameInfoBinder` component (ecs/ui/) — reads GameInfo, pushes game_name/version/genre/theme into scene nodes via NodePath exports; also sets the OS window title.
  - Added to all **15 themed scenes** — each `ThemePresetComponent` is now driven by `GameInfo.DefaultThemePreset` (was baked as a literal enum int). Main menu title + version bound; game-over/level-select/character-select titles append the game name.
  - A dev edits `res://game_info.tres` ONCE (game name, version, theme, genre, resolution) and every menu reflects it — the "one place and environment" centralization. Builds clean (0/0).
- [x] **Stage 13 — One-click genre generation (Genres tab + flow fixes)** — the dev experience is now genuinely "click → play":
  - New **Genres tab** (partial class `BeepGameBuilderDock.Genres.cs`): 4 sections (Platformer/Top-Down/Shooter/Puzzle), each with a game-name field + theme picker + "Generate" button.
  - Fixed two flow-breaking bugs found by auditing the runtime chain:
    - **Autoload path mismatch** — generator registered `res://autoload/*.gd` but scripts are written to `res://scripts/managers/*.gd`. Corrected to matching paths (project would have failed to load).
    - **Dead signal wiring** — `MenuComponent.ActionTriggered` was never connected to `NavigationComponent.Dispatch`, so clicking buttons did nothing. Now `MenuComponent._Ready` auto-discovers a sibling `NavigationComponent` and connects `ActionTriggered → Dispatch` (no `[connection]` blocks or glue needed).
  - Verified end-to-end: PlayButton → action "play" → `Dispatch("play")` → `GoToGame()` → reads `GameInfo.GameScenePath` → `ChangeSceneToFile`. The genre scene is copied to exactly that path. Builds clean (0/0).
- [x] **Stage 14 — Optional UI/scene effects (cascade)** — effects now affect all child UI nodes from one parent component:
  - New `EffectComponent` base class (ecs/ui/) — adds `ApplyToChildren` + `ButtonsOnly` exports and a `Targets` list. One component under a container cascades to every descendant Button/Control.
  - Refactored 4 effects to inherit it: **Ripple** (click ripple on all buttons), **Pulse** (breathing scale), **Shake** (per-target original positions), **Slide** (per-target visible positions). All default to single-target (backwards compatible); set `ApplyToChildren = true` to cascade.
  - Scene-level transitions: `NavigationComponent` auto-discovers a sibling `SceneTransitionComponent` and gates scene changes behind the fade (optional — no transition component = instant change).
  - Genre scene templates ship with effects enabled (ripple on menus, transitions on navigators, animated entrance on hero screens). Builds clean (0/0).
- [x] **Stage 15 — GameApp global node/component** — the single referenceable game node:
  - New `GameApp` (ecs/, `[GlobalClass] : Node`) — registered as the "GameApp" autoload AND droppable into any scene via Add Node. Reference it C# `GameApp.Instance` / GDScript `get_node("/root/GameApp")` or `$GameApp`.
  - Holds TWO cleanly-separated kinds of data:
    • **Static config** via `Info` (the existing `GameInfo` resource — game name, version, genre, theme, resolution, scene paths, tuning). Loaded from `game_info.tres`.
    • **Runtime/session state** that didn't belong on the static resource: `CurrentLevel`, `SessionScore`, `SelectedCharacter`, `MaxLevelReached`, audio/display settings.
  - Convenience accessors (`GameName`, `Version`, `ThemePreset`, scene paths) so call sites stay short. Mutators (`AddSessionScore`, `SetLevel`, `ApplyAudioSettings`, `ApplyDisplaySettings`) emit signals (`SessionScoreChanged`, `LevelChanged`, `SettingsChanged`) so UI binds react.
  - Added `GameInfo.TresPath` constant. Registered as autoload in `BeepGenreGenerator`. Builds clean (0/0).
- [x] **Stage 16 — Multiple theme choices per genre** — each genre now offers a curated shortlist of suitable themes (not one forced preset):
  - New `GameInfo.RecommendedThemes(genre)` → `string[]` (5 vibes per genre). First entry is the default.
    - Platformer: Cartoon, Modern, Retro80s, Pixel8Bit, Nature
    - Top-Down: Fantasy, Classic, Nature, Japan, Military
    - Shooter: SciFi, Cyberpunk, Military, Space, Toxic
    - Puzzle: Candy, Cartoon, Modern, Sea, Japan
  - Dock Genres tab: theme picker now shows only the genre's shortlist (was all 22 presets). Dev picks the vibe, Generate stamps it.
  - `genre_templates.json` gained `"themes"` arrays per genre (manifest is source of truth). Builds clean (0/0).
- [x] **Stage 17 — Color palettes per theme (genre → theme → palette)** — each theme now has 7 swappable color palettes the user picks as a third dimension:
  - New `ColorPalette` resource (ecs/ui/, `[GlobalClass]`) — HSV-space tint (HueShift/SaturationMul/ValueMul) that retints any theme's ColorSchema AND every StyleBoxFlat color. 7 built-ins: Default, Warm, Cool, Pastel, Vibrant, Dark, Muted.
  - New `PaletteTintedPreset` decorator (IThemePreset wrapper) — applies the palette uniformly so the existing theme-assembly code runs unchanged on tinted output.
  - `ThemePresetComponent.PaletteName` export — "Default" leaves the theme unmodified; any other built-in retints the whole UI.
  - `GameInfo.PaletteName` + `GameInfoBinder` pushes it onto the theme component alongside the preset.
  - Dock Genres tab now has THREE pickers: **Genre → Theme → Palette**. Stamping writes all three into game_info.tres; scenes read it at runtime.
  - So e.g. Shooter + Cyberpunk + Pastel = a pastel cyberpunk game. Builds clean (0/0).
- [x] **Stage 18 — Geometry/shape profiles (4th dimension: genre → theme → palette → geometry)** — shape now varies independently of color, so the question "are you implementing geometry per genre/theme?" is finally YES:
  - New `GeometryProfile` resource (ecs/ui/, `[GlobalClass]`) — corner radius, border width, shadow size/offset, content padding, font size. Applied as an OVERRIDE layer after the preset builds its StyleBoxes (presets unchanged). 7 built-ins: As-Authored, Sharp, Rounded, Pill, Chunky, Flat, Beveled.
  - `GeometryProfile.ForGenre(genre)` suggests a default: Platformer→Chunky (16px corners, 4px border, big shadow), TopDown→Rounded, Shooter→Sharp (0 corners, thin border), Puzzle→Pill (24px corners, no border).
  - `ThemePresetComponent.GeometryProfileName` export — overrides geometry via `ExtractGeometry` (restamps the extracted fields) + `RegisterButtonType` (restamps each button-state StyleBox) + font-size loop. Preset `.cs` files untouched.
  - `GameInfo.GeometryProfileName` + `GameInfoBinder` push it through to scenes at runtime.
  - Dock Genres tab now has FOUR pickers: **Genre → Theme → Palette → Geometry** (geometry defaults to the genre suggestion, overridable). Stamping writes all four into game_info.tres.
  - Example: Platformer + Cartoon + Vibrant + Chunky = a chunky vivid cartoon platformer. 5 themes × 7 palettes × 7 geometries = 245 looks per genre. Builds clean (0/0).
  - **Fix (same stage):** geometry now applies to ALL UI nodes, not just buttons. Added an `Sb(name, type, box)` chokepoint that restamps every StyleBox through `StampGeometry` before assigning — so panels, line/text edits, spinboxes, progress bars, sliders (H+V), scrollbars (H+V), trees, item lists, popup menus, tabs, separators, and all selected/hover/cursor/focus states get the geometry profile. Verified zero raw `SetStylebox` calls bypass it (37 StyleBox assignments all routed through `Sb()`). `ApplyToSingleButton` path also stamps geometry + font size.
  - **Theming fix (same stage):** all colors now derive from the theme schema (palette-tinted). Replaced 3 hardcoded color literals: focus-glow now blends `AccentSecondary`+`TextOnDark` (was fixed white-brighten); ripple color now uses `AccentPrimary` (was fixed white). Final audit confirms zero hardcoded colors remain in StyleBox/animation code — every color reads from `c` (the tinted ColorSchema). The only `Color(1,1,1,1)` left is the focus-exit neutral reset, which is correct.
  - **Per-node-type complete theming fix (same stage):** rewrote `ApplyToSubtree` so each node type is themed as a COMPLETE UNIT — all its color properties + all its StyleBox background states + geometry, composed together in one block per type (was: colors in one generic loop, StyleBoxes in a separate flat list). Went from 3 → 40 color assignments across 23 distinct properties, covering every state: font_color / font_hover / font_pressed / font_disabled / font_focus / font_selected / font_outline / selection / caret / clear_button / tick / guide / drop_position / relationship_line / font_separator / font_accelerator / title / close (×3) / tab hovered+selected+disabled. Node types fully themed: Button (6 button-like), Label/RichTextLabel, LineEdit/TextEdit/SpinBox, ProgressBar, HSlider/VSlider, HScrollBar/VScrollBar, Tree/ItemList, PopupMenu, TabBar/TabContainer, Panel/PanelContainer, Separators, Window. Builds clean (0/0).
  - **Dedicated per-node method refactor (same stage):** split theming into a partial file `ThemePresetComponent.NodeTheming.cs` with ONE dedicated method per UI node type — `ThemeButton()`, `ThemeCheckButton()`, `ThemeCheckBox()`, `ThemeOptionButton()`, `ThemeMenuButton()`, `ThemeColorPickerButton()`, `ThemeLabel()`, `ThemeRichTextLabel()`, `ThemeLineEdit()`, `ThemeTextEdit()`, `ThemeSpinBox()`, `ThemeProgressBar()`, `ThemeSlider()`, `ThemeScrollBar()`, `ThemeTree()`, `ThemeItemList()`, `ThemePopupMenu()`, `ThemeTabBar()`, `ThemeTabContainer()`, `ThemePanel()`, `ThemePanelContainer()`, `ThemeSeparator()`, `ThemeWindow()`. `ApplyToSubtree` is now just a clean call list — no loops, no shared generic `RegisterButtonType`, no preset delegation. Each method owns its node's complete appearance. Shared low-level StyleBox primitives (`Box`, `InputBox`, `PanelBox`, `SurfaceBox`, `RoundBox`, `CircleBox`, `SelectedBox`, `SeparatorBox`) live at the bottom. Builds clean (0/0).
  - **Genre-tuned geometry (same stage):** researched reference games (Hollow Knight/Celeste, Stardew/Terraria, Gungeon/Nuclear Throne, Candy Crush/Bejeweled) and replaced placeholder geometry values with 4 genre-named profiles: `PlatformerStyle` (6px radius, thin 2px border, soft 10px shadow, 24px font), `TopDownStyle` (4px radius, chunky 4px border, 8px shadow, 20px font), `ShooterStyle` (2px radius, thin border, minimal 6px shadow, 28px bold HUD font), `PuzzleStyle` (24px pill radius, 3px border, 12px soft shadow, 32px friendly font). `ForGenre` maps each genre to its profile. Picker now shows genre-named options. Builds clean (0/0).
- [x] **Stage 19 — UISkin (texture / 9-patch support)** — optional texture-based UI skinning for ALL ui nodes, set globally via GameApp:
  - New `UISkin` resource (ecs/ui/, `[GlobalClass]`) — holds texture paths (res://) for button states (normal/hover/pressed/disabled/focus), panel, input (normal/focus), progressbar (bg/fill), slider grabber, scrollbar grabber, separator. Each slot optional — unset = procedural fallback. Builds `StyleBoxTexture` (9-patch) with configurable patch margin.
  - `GameApp.Skin` export — set the skin once globally; `GameInfoBinder` pushes it onto every scene's `ThemePresetComponent`.
  - `ThemePresetComponent.Skin` export + `SkinOr(texturePath, proceduralBox)` helper in the NodeTheming partial — each per-node method tries the skin texture first, falls back to the procedural `StyleBoxFlat`. Wired into: Button, LineEdit, ProgressBar, Panel, PanelContainer (the nodes with texture slots). Nodes without a texture slot (Label, Slider, ScrollBar, etc.) keep using procedural.
  - So: drop a UISkin resource on GameApp (or on the ThemePresetComponent directly), point the texture paths at your 9-patch PNGs, and buttons/panels/inputs/progressbars render from textures. No code. Builds clean (0/0).

---

## Status legend

- `[x]` done · `[~]` in progress · `[ ]` not started
- Update this file as each stage completes. Detailed task lists live in
  `plans/genre-templates/stage-N-*.md`.
