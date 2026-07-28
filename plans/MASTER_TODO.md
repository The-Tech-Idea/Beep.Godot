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
- [x] **Stage 20 — City Builder art set + the two bugs that made every texture inert** — the texture pipeline shipped in Stage 19 had never actually rendered a single pixel. Two defects, both in `ThemePresetComponent`:
  - **`ApplyButtonOverrides` painted over every button texture.** It rebuilt the five button-state boxes from `preset.GetButton*()`, which always return a procedural `StyleBoxFlat` — then stamped them as PER-NODE overrides, which outrank the Theme. So `ThemeButton()` resolved a `StyleBoxTexture` into the theme and the very next pass discarded it on every button in the subtree. The five button textures each theme shipped had never been visible. Now sources the boxes back out of `_generatedTheme`, so whatever `SkinOr()` resolved (theme.json → UISkin → procedural) is what lands on the button.
  - **`Duplicate()` silently downgraded 9-patches.** The `StyleBoxTexture` branch copied texture + margins + modulate but dropped `AxisStretchHorizontal/Vertical`, `DrawCenter` and all four `ExpandMargin*`, resetting each copy to Godot's defaults — so a slot's authored `axis_stretch_*`/`draw_center` survived into the Theme and was lost the instant the box was duplicated onto a node.
  - **Art:** 29 hand-drawn textures for the `citybuilder` genre, every colour taken from `urban/theme.json` so they match the procedural theme they replace. 14 chrome 9-patches (`textures/citybuilder/urban/`) — buttons ×5, panel, dialog, input ×2, progress bg/fill, slider + scroll grabbers, separator; 14 content icons (`textures/citybuilder/icons/`) — house, factory, park, population, budget, power, happiness, calendar, treasury, residential, commercial, industrial, income, expense; and a 256×256 seamless street-grid page background replacing the old 128px `backgrounds/city_grid.png`.
  - `urban/theme.json` went from **4 declared texture slots to 14**, each with its own 9-patch margins — the per-slot margins `UISkin`'s single uniform `PatchMargin` cannot express. The genre's four other themes are untouched and still use their original 4.
  - **`"baked": false`** — new `TextureSlotDef` flag (`SkinCatalog`), honoured by `BeepTextureBaker.Bake()`. The baker only knows how to draw a plain rounded box, and it is reachable from a dock button and an MCP command, so "bake everything" would have silently flattened all 14 hand-drawn slots. Authored art now opts out and the baker logs the skip.
  - City Builder templates (`build_menu`, `districts`, `economy`, `citybuilder_main`) wire the icons in; `citybuilder_main`'s five HUD `NodePath` exports were rewritten alongside, since each stat label moved into an icon+label row.
- [x] **Stage 21 — Save/Load menu UI revision (separation + sizing)** — the two screens sit one click apart and did not look related. Root cause: **four** copies of the layout numbers — each menu's `.tscn` plus each menu's `NormalizeLayout()`, which restamps sizing at runtime and so silently overrode the scene. They disagreed in every pair (load scene 700×550 vs its code's 640×520; save 560×500, 8px rows vs load's 10px and 58px).
  - New `SaveLoadMenuLayout` (internal static) is now the single source: panel 780×660, 28px padding, 20px sections, 10px row gap, 64px rows, 52px action buttons, 14px scroll gutter. Both `.tscn` files are authored to it and both `NormalizeLayout()` methods stamp it, so scene and code cannot drift.
  - **Slot rows were unreadable on every light theme.** The scene baked a near-black `StyleBoxFlat` (0.15, 0.15, 0.19) into each row while the theme supplied the text colour — dark text on a dark row. Selection then replaced it with a bare `new StyleBoxFlat()`, which is fully transparent, so clicking one row erased every other row's background. Row styling moved into `LoadGameMenuComponent.BuildRowStyle`, derived from the live theme and built from *translucent* overlays so it also works over a textured panel instead of punching a flat rectangle through the art.
  - **The `Level:`/`Time:` captions had never been visible.** Both carried `text_overrun_behavior = 1`, which lets a Label trim and so drops its minimum width to ~1px; inside an `HBoxContainer` (which hands each child its minimum) they rendered as a one-pixel column. Verified via a headless probe: `size=(1.0, 20.0)`. Trimming kept only on the name label, which is a VBox child (fills) and holds an arbitrary user-typed name.
  - **Save-slot selection was invisible** — `OnSlotSelected` calls `SetPressed()`, which paints nothing on a momentary button. Slots are now `toggle_mode = true`.
  - Scrollbar no longer sits on top of every row's Delete button (`SlotsMargin` gutter); the autosave row gained the Delete button the other five had, so the list edge is no longer ragged.
  - `BuildRowStyle` takes `Godot.Control`, not `Control` — a consuming game that defines its own `Control` type shadows the name and breaks the build (CS1503), the same trap already documented on `NormalizeLayout` (CS8121). Caught by building against the sample game, which has exactly such a type.
  - Verified by rendering both menus at 1280×720 and inspecting them, not by inspection of the scene files alone.
- [x] **Stage 22 — Save/Load from the in-game pause menu** — both entries were dead from pause (they worked only from the title screen). Two independent causes:
  - **The dialog was parented into WORLD space.** `SaveLoadManagerComponent.AddOverlay` added a Control-rooted menu straight to `GetTree().CurrentScene`. From the title screen that is a Control, so it landed in screen space and worked — which is why this looked fine in isolation. **During play `CurrentScene` is the Node2D game root**, so the dialog joined the world canvas: it rode the Camera2D and drew beneath every HUD layer, and far beneath the pause overlay itself, which `GameFlowComponent` hosts at `Layer = 100`. The menu was built, parented and left underneath the menu that opened it — indistinguishable from a button that does nothing. `AddOverlay` now hosts a Control-rooted menu in its own `CanvasLayer` at **110** (above pause's 100) and frees that host when the menu frees itself, so no empty layer is leaked per open. `GameFlowComponent` already documents this exact trap for its own overlay; this component had the identical defect.
  - **`FindUILayer()` never found anything.** It probed `/root/HUD` and then any `CanvasLayer` directly under `/root`, but a genre's HUD lives at `<GameScene>/HUD` and `/root` holds only autoloads — so it always returned null and fell through to the world-space parent above. Removed; `AddOverlay` owning its layer is correct in both contexts.
  - **Save was hidden in the one place it is useful.** `MainMenu._Ready` set `SaveGameButton.Visible = false` unconditionally, reasoning that the title screen has no run to capture. But this scene is BOTH the title menu and the pause overlay (`GameFlowComponent` instances it over the frozen game), so the entry was absent from the pause menu too. Now `Visible = GameApp.Instance?.IsGameRunning`, the same predicate `SaveGameMenuComponent` already uses to enable its Save button.
  - Verified end to end against a running City Builder: pause → Save → pick slot → Save produced `[SaveLoad] Game saved to slot 0` with the tree paused, and pause → Load rendered the dialog on layer 110 at (0,0), full screen.
  - Not fixed, and worth knowing: `NavigationComponent` declares and emits `LoadGameRequested`/`SaveGameRequested` for `Dispatch("load_game"/"save_game")`, and **nothing in the addon connects either signal**. That route is inert. It is documented as non-canonical (the shipped menus call `ShowSaveMenu()`/`ShowLoadMenu()` directly), so it is a trap for anyone wiring save/load through nav actions rather than a live bug.
- [x] **Stage 23 — Control sizing, type scale and scrollbars (the "unprofessional" pass)** — measured every control at runtime instead of eyeballing the scenes. Three systemic causes, all in the theme engine rather than in any one screen:
  - **A 9-patch margin is not padding.** A `StyleBoxTexture` whose content margins are unset (-1) falls back to its **texture margins** — which exist to stop corner artwork stretching and are routinely 14-28px. So a control's padding was decided by how its art was sliced: **64px buttons, 52px inputs**. **49 of the 50 shipped themes** leave content margins unset, so this was near-universal — and it only surfaced now because Stage 20 made textured buttons render at all, so the fat controls arrived *with* that fix. New `WithThemePadding()` in `SkinOr` stamps the geometry profile's padding onto any texture box that declares none; an explicit `content_margin_*` in theme.json still wins. Verified across citybuilder/rpg/puzzle/shooter/platformer/cardgame: padding is now 9-10px everywhere despite 9-patch margins ranging 6→20.
  - **Scrollbars inherited content padding.** `StampGeometry` applies the geometry's `ContentPadding` to every `StyleBoxFlat`, so a 14px padding produced a **28px-wide scrollbar**, and the 9-patch grabber texture forced 22px on top. Every list had a fat grey column down its side, and the track (`BgCanvas` at 0.7 alpha) read as a near-solid gutter on light themes. `ThemeScrollBar` now owns its thickness — a **10px** pill, hairline track, plus the previously-missing `grabber_pressed` state — restamped after `Sb()` so the geometry profile cannot re-inflate it. Grabber art re-cut 24×24 → **12×12**.
  - **Type scale.** Was 1.9 / 1.35 / 1.25 / 0.85 → 32/23/21/14 off a 17px base. A 1.9× title is poster-sized for a dialog, subtitle and value landed 2px apart (reads as an inconsistency, not a distinction), and the drop to caption was a cliff. Now **1.75 / 1.3 / 1.1 / 0.8** → 28/21/18/13, a conventional ~1.25 ratio scale. City Builder geometry also went font 17→16, padding 14→10, shadow 9→6 (the 9px shadow read as a halo on every control).
  - `SaveLoadMenuLayout` floors were sized against the old 64px controls and were silently inflating everything they touched; now panel 720×620, action buttons 44, slot selector 128×44, delete 88×36, outer margin 24. **Heights are floors, not targets** — documented, because setting one above the natural height is exactly how this went wrong.
  - Two defects that only appeared once a *real* save existed: `SaveMetadata.CurrentLevel` is a free-form string holding the gameplay scene's res:// path, so rows read `Level: res://scenes/main/citybuilder_main.tscn` — meaningless to a player, and long enough that the label's minimum width dragged the list wider than the dialog, adding a horizontal scrollbar and pushing every Delete button off the right edge. Added `PrettyLevel()` (basename, de-underscored) and set `horizontal_scroll_mode = 0` on both slot lists, since a vertical list must never scroll sideways. Meta captions got a width floor + ellipsis + expand, so they can neither collapse to 1px (the Stage 21 bug) nor force the row wider.

- [x] **Stage 24 — Text colour contrast + type-role consistency** — label colours were assigned without ever asking what surface the label sits on.
  - **Hover text was invisible on light themes.** `font_hover_color` and `font_pressed_color` were wired straight to `TextHover`, which nearly every light theme defines as pure white — while ALSO defining a near-white hover surface. urban put `#FFFFFF` on `#F5F8FA`: **contrast 1.07**, i.e. a button's label vanished the moment the pointer touched it. Pressed was worse, reusing the same colour against a lighter face. Audited all 50 themes: **68 of 200 button label states scored below 3.0**.
  - New `ReadableOn(schema, surface, preferred)` honours the author's colour whenever it actually reads (WCAG AA, 4.5) and otherwise falls back to whichever of the theme's OWN two text colours contrasts better — nothing is invented, so palette tints still carry through. The six duplicated three-line colour blocks (Button / CheckButton / CheckBox / OptionButton / MenuButton / ColorPickerButton) collapse into one `ButtonFontColors()`. **68 failing states -> 3.**
  - The 3 that remain are all `puzzle/candy`, which defines `text_primary`, `text_hover` AND `text_on_dark` as pure white over hot-pink surfaces — there is nothing to choose between. That is a palette authoring bug in one theme, and white-on-pink is also a deliberate match-3 look, so it is left alone and flagged rather than silently restyled.
  - **Disabled needs the opposite treatment.** Holding disabled labels to 4.5 repainted them in full-strength body colour, and a disabled Save button looked perfectly clickable. Muted IS the affordance. Split out `MinDisabledContrast = 1.6`, which only catches text that has vanished outright into its own surface. Caught by rendering the dialog and looking at it, not by the audit.
  - **Type roles were used for the wrong things.** The caption step is for secondary metadata (a save's Level/Time, a version string) — but it was also on form-field and table-row labels, so "Save Name:" rendered at 13px disabled-grey beside a 16px input, and every economy row label was a third smaller than its own value. Promoted 13 labels across save/build/districts/economy from caption to base size. `RoleFor()` also infers the role from a node name ending in "Caption", so each had to be renamed as well as have its variation dropped (no script referenced them — verified before renaming). Caption itself went 0.8 -> 0.85 (13 -> 14px). Resulting scale on the economy screen: title 28 -> heading 21 -> row label 16 -> value 18.

- [x] **Stage 25 — Sweep every scene (not just the ones on screen)** — Stages 23-24 fixed the theme engine, which reaches all scenes automatically, but the per-scene bypasses had only been cleaned up in the 6 screens being looked at. Audited all **66 template scenes** node-by-node and fixed what the audit found:
  - **Hardcoded font sizes (4).** `cardgame_main` EnergyLabel=32, `puzzle_main` ScoreLabel=40, `racing_main` SpeedLabel=56 and `level_summary` BannerLabel (a LabelSettings sub-resource) set px directly, which bypasses the theme completely — switching theme or geometry left them frozen while every other label rescaled. New **`BeepDisplay`** role (2.5x base = 40px, accent-coloured) keeps a score/speedo/energy readout big AND theme-driven. The orphaned LabelSettings sub-resource was removed with it.
  - **Inflated text buttons (29).** Plain text buttons pinned at 52/56/64px against a natural ~44. Normalised to 44. Deliberately NOT touched: the 22 tile/card buttons at 72-120px (level-select nodes, build-menu tiles, research cards) and save_game_menu's 60px list rows — those heights are the design, not inflation.
  - **Caption misused for primary text (15).** `settings_menu` had five more of the exact "Save Name:" defect (Master Volume / SFX / Music / Resolution / Language field labels at caption size), `shooter/codex` shrank item NAMES to caption, plus card_battle's HUD readouts, level_results' level name and level_complete's "New High Score!". Promoted to base size. Left alone where caption is correct: VersionLabel, HintLabel, item descriptions and the save-slot Level/Time meta.
  - **`load_steps` wrong in 58 scenes** — pre-existing hand-authoring drift (a scene with 3 ext_resources declaring `load_steps=3` instead of 4). Only a loader progress hint, so harmless, but normalised while sweeping. Formula confirmed against Godot-written scenes (3 ext + 2 sub -> 6).
  - Full validation pass over all **124 scenes** (66 template + 58 game-project): every ExtResource/SubResource id declared, every `res://` path resolves, every `load_steps` correct. Final scale verified at runtime: **display 40 / title 28 / subtitle 21 / value 18 / base 16 / caption 14**.

- [x] **Stage 26 — The chrome art had no material character** — the textures were wired correctly the whole time (`StyleBoxTexture` resolving, 29/29 imported), but they were drawn to match the urban palette so exactly — same fill, same radius, same 1px border as the procedural box — that a textured button differed from `StyleBoxFlat` by a **mean of 10.6/255 (~4%)**. Measured by rendering the same screen with `UseTextures` on and off and diffing. Correct plumbing, invisible art: it looked like nothing had been applied.
  - Re-baked as game UI rather than web-form boxes: hard 2px outline (the single biggest "this is a game" cue), a real edge bevel, a gloss sweep across the upper face, genuinely inset pressed and input states, and per-pixel grain — the one thing a StyleBoxFlat provably cannot reproduce.
  - **Two failed attempts on the way, both caught by looking at the render:** the first bevel used `ImageDraw.arc()` over an ELLIPSE bounding box, which paints circular rings straight across the middle of every plate instead of following the rounded-rect edge, and the "rivets" landed as smudges. Rebuilt as a rounded-rect ring masked by a vertical alpha ramp, so every light/dark cue stays ON the edge.
  - **A blueprint grid cannot live in a 9-patch centre.** The centre is STRETCHED (64px -> 720px here), so the grid intersections smeared into irregular blotches across the dialog. Switching to Tile would fix the centre but then the vertical gradient in the left/right edge regions repeats down the sides — 9-patch cannot do both. Panels therefore carry gradient + bevel + frame only; the repeating blueprint texture comes from the geometry's `background_image`, which `ApplyBackground` mounts as a `TextureRect` in Tile mode and which therefore tiles correctly. That mechanism was already working.

- [x] **Stage 27 — Full chrome set for all 50 themes** — Stage 26 gave citybuilder/urban proper game-UI chrome; every other theme still had 4 flat textures. Wrote one baker driven entirely by each `theme.json`'s OWN palette and geometry (corner radius, border width, content padding, ~20 distinct values per colour key), so nothing is hardcoded per theme and an edited theme re-bakes to match. **700 textures, 14 slots x 50 themes.**
  - **One recipe would have wrecked several palettes**, so the treatment is chosen from the theme's own data: `pixel` (pixel8bit / retro80s) gets NO antialiasing, no gloss, no grain and hard chunky borders — smooth bevels on a #000000 face with 1-bit art would be plainly wrong; `dark` (luminance(surface_primary) < 0.32 — cyberpunk, neon, darkfantasy, military…) gets restrained gloss and an accent-drawn outline so the plate still reads against a near-black canvas; `light` gets the full material treatment. Split: 17 light / 31 dark / 2 pixel.
  - Every theme.json now declares all **14 slots** with per-slot 9-patch margins derived from its own corner radius (margin must clear the radius or the patch slices the curve), content margins from its own `pad_left`/`pad_top`, and `"baked": false` so the texture baker cannot flatten them.
  - **Bug found while verifying: a stale background image was never cleared.** `ApplyBackground()` returned early when the geometry profile had no `background_image` (or was "As-Authored", where `_geometry` is null) but never removed the existing `TextureRect` — so switching profiles kept painting the PREVIOUS profile's tile forever. It showed up as the city-builder blueprint grid sitting on top of a cyberpunk theme. Now clears the rect on the empty path.
  - 490 new `.import` files generated (git-tracked here); addon repo and game project verified byte-identical across **744 textures, 744 imports, 50 theme.json and 258 C# sources** — zero content differences, zero files on only one side. Verified visually by rendering the load menu under urban / cyberpunk / royal / darkfantasy / pixel8bit.

- [x] **Stage 28 — Settings screen: made it work, then made it line up** — reported as "nothing in settings is doing anything".
  - **Root cause was a scene/code mismatch, not dead wiring.** The addon's `settings_menu.tscn` had grown to 56 nodes while both game projects still carried a 33-node version, missing `ResetButton` and `ControlsList` — 2 of the 13 nodes `SettingsMenu.cs` binds. Their `SettingsMenu.cs` (137 lines) and `SettingsComponent.cs` (312, no `ResetToDefaults`) were stale too. Pushed the current scene + both scripts to **both** projects; verified live: slider 51 -> 33 propagates to `MasterVolume`, Subtitles toggles, `ControlsList` populates with 19 keybind rows.
  - **`font_color` does not exist on TabBar/TabContainer.** Godot names the idle tab colour `font_unselected_color` (verified against `ThemeDB.get_default_theme().get_color_list()`), so every value the themer wrote to `font_color` was silently discarded and idle tabs kept Godot's own dim grey — "Display"/"Game"/"Controls" read as greyed-out/unavailable under every theme. Now sets the four real colour items plus the matching `icon_*_color`, and an idle tab is the normal text colour at 78% alpha rather than the *disabled* colour: an unselected tab is a place you can go, not one you cannot.
  - **The slider changed HUE on focus.** `grabber_area` used `AccentPrimary` but `grabber_area_highlight` used `AccentSecondary`, so the focused Master slider rendered green while the two beneath it stayed blue — three controls that looked unrelated. Highlight is now a lightened `AccentPrimary`.
  - **Alignment: the label column expanded on some rows and not others.** Fullscreen/Subtitles/ScreenShake/DamageNumbers carried `size_flags_horizontal = 3`; Master/SFX/Music/Resolution/Language did not — so the control column started at a different x on every tab and the dialog jumped sideways as you switched. Rows had no minimum height either, so slider/checkbox/option rows were three different heights. Every row is now the same three-column grid — fixed 190px label, expanding control, fixed 64px right-aligned read-out — at a uniform 44px. Verified: all controls land at **x=536 on every tab**, all rows 44px.
  - `SaveLoadMenuLayout` renamed **`BeepDialogLayout`** — it is no longer save/load-specific — and gained the settings form constants. `SettingsMenu.NormalizeLayout()` restamps the grid on every open, so a project still carrying an older settings scene is corrected without regenerating it.
  - **Tabs appeared to overlap.** Every tab stylebox was a generic `SurfaceBox`, so it inherited the geometry profile's DROP SHADOW (6px at +3y on citybuilder). A tab strip is a row of near-touching controls, so each tab cast its shadow across the gap onto its neighbours. Tabs now get their own `TabStyles`/`TabBox`: no shadow, rounded top, square bottom, no bottom border, plus `h_separation = 6` on TabBar. The selected tab is painted in the PANEL's colour so it welds to the content area, and unselected is `SurfacePressed` — previously the pair was SurfaceHover vs SurfacePrimary, two nearly identical light greys, so the active tab was barely distinguishable.
  - The shaping has to be applied AFTER `Sb()`, because `Sb -> StampGeometry` re-applies the geometry profile's shadow/radius/border over the box. Building a shadowless box and handing it to `Sb()` silently achieved nothing — verified by measuring the live stylebox (`shadow=6` came straight back), which is the same trap already documented on the scrollbar.
  - Applied to the addon and **both** game projects (`new-game-project`, `new-game-project-1`); all three build clean, C# byte-identical, settings scene identical.

- [x] **Stage 29 — The 11 most-looked-at scenes never followed GameInfo** — every genre main plus the shared HUD carried a `ThemePresetComponent` with **no `GameInfoBinder`**, so their theming came from whatever literal was typed into the scene and ignored `game_info.tres` entirely.
  - **`hud.tscn` was themed as the wrong genre outright** — `GenreName = "platformer"`, hardcoded, in a citybuilder project. Nothing would ever change it.
  - **All 10 `*_main.tscn` set only `GenreName` and no `PresetName`**, so `ThemePresetComponent` fell back to the genre's `default_theme` instead of the chosen preset/palette/geometry. These are the screens a player spends the most time on, and they were the only themed scenes in the project without a binder — the other 15 have had one since Stage 12.
  - Fixed by adding a `GameInfoBinder` to all 11 (x3 locations: addon templates, and both game projects = 33 scenes), targeting each scene's existing theme node (`HUD/Root/Theme` on the mains, `TopLeft/Theme` on the HUD). Verified at runtime: the main's component now reports `citybuilder/urban/default/City Builder`, exactly matching `game_info.tres`, and `hud.tscn` reports `citybuilder/urban` instead of platformer.
  - **`BeepGenreScene` is deliberately NOT adopted here, and that is a real decision rather than an oversight.** It exists and is used in 0 scenes, but it flows data the OPPOSITE way: `GameInfoBinder` READS `game_info.tres` and drives the theme component; `BeepGenreScene` WRITES into `GameApp.Info` (genre, theme, palette, geometry, tuning, nav wiring, `GameScenePath`) from the scene. Putting both on one scene makes them fight, order-dependent.
  - **DECISION (reviewed, keep): genre stays in `game_info.tres`.** `BeepGenreScene` remaining at 0 usages is deliberate, not an oversight — do not "fix" it without re-reading this. The decisive reason is boot order: `project.godot` launches `main_menu.tscn`, and `MainMenu` picks the gameplay scene from `GameInfo.NewGameScenePath`/`GameApp.GameScenePath`. `BeepGenreScene.RegisterAsMainScene` writes `GameScenePath` only once that main is ALREADY running, so it is circular and cannot deliver "launch a different main to switch genre" on its own — that needs `run/main_scene` pointed at the genre main, which costs the boot title screen (the menu would only be reachable as the pause overlay). Two further constraints found while evaluating it: `AutoInstantiateMainScene` defaults true and `genre.json#main_scene` for citybuilder IS `citybuilder_main.tscn`, so the node would instantiate its own scene as a child (infinite recursion); and `ApplyToSiblingTheme` scans `GetParent().GetChildren()`, so it must sit under `HUD/Root` beside `Theme`, not at the scene root. A hybrid (binder + genre scene on the same main) was rejected outright: the two flow opposite ways, so launching a main would rewrite `Info` and re-theme the pause menu mid-session against the title screen.
  - Dropping `BeepGenreScene` into the mains with its default exports would have *re-created* the bug it looks like it solves: `ThemePreset` empty means `Info.DefaultThemePreset = genre.DefaultTheme` (clobbering a chosen preset), `PaletteName` defaults to the non-empty `"Default"` so it always overwrites the project's palette, and `GeometryProfileName` defaults to `"As-Authored"` — which on this project would replace `"City Builder"` and take the tiled page background with it. It is the right mechanism for a "genre lives in the scene, launch a different main to switch" project; it is the wrong one for a project whose genre lives in `game_info.tres`, which is how this one is built.

- [ ] **Stage 30 — Genre HUD rebuild** — PLANNED, not started. Full per-genre design reference: **`docs/HUD_DESIGN_PER_GENRE.md`**.
  - **Problem.** Every one of the ten HUDs is a stack of text Labels in one corner. No bars, no meters, no hotbar, no build toolbar, no cooldowns, no alerts. A player cannot read health at a glance from `"Health: 72"`. City Builder is the clearest case — it shows a status strip and a minimap, and is missing both pieces that define the genre: the **build toolbar** and the **RCI demand meter**.
  - **Key finding: this is mostly a WIRING gap, not a build gap.** 45 of the addon's 70 UI components have never been placed in any scene — including `BuffBar`, `BossHealthBar`, `ComboCounter`, `MatchTimer`, `InteractionPrompt`, `ToastNotification`, `ProgressRing`, `Counter`, `WeatherHUD`, `Vignette`, `ScreenFlash`, `Tooltip`, `Table`, `TabGroup`, `ContextMenu`, `SafeArea`. 21 of them are directly reusable here with no new code.
  - **Design is documented in ONE DETAILED FILE PER GENRE** under `docs/hud/` — `citybuilder.md`, `strategy.md`, `shooter.md`, `rpg.md`, `survival.md`, `cardgame.md`, `racing.md`, `puzzle.md`, `topdown.md`, `platformer.md`, indexed by `docs/HUD_DESIGN_PER_GENRE.md`. Each carries: reference games and what each is known for, a canonical layout wireframe, a numbered element spec with P0-P3 priorities, genre best practices with rationale, audited current-vs-target, the `SetStat` data contract, component reuse/build lists, and implementation pitfalls.
  - Researched against the genre-defining games (Celeste/Hollow Knight, Zelda/Stardew, Doom/Halo/Vampire Survivors, Candy Crush/Tetris, Forza/Mario Kart, Skyrim/Diablo, Hearthstone/Slay the Spire, Cities: Skylines/Frostpunk, AoE II/StarCraft/Civ VI, Minecraft/Valheim/Don't Starve) — screen-region layout, what each region carries, current-vs-missing, and which component covers it.

  - **SECOND AUDIT (deeper pass) — the data is fake as well as the widgets.** `GenreHudComponent` exposes real binders (`BindScore/Lives/Level/Health`) plus a `Placeholder(...)` fallback that only warns and shows scene text. **31 of the 44 stat bindings across the ten genres are `Placeholder`**, and five genres — cardgame, citybuilder, racing, strategy, survival — have **zero** real data sources. Separately, **6 of 29 genre screens are 17-line Close-only mockups** with every figure hardcoded in the scene: `citybuilder/districts`, `citybuilder/economy`, `rpg/character`, `rpg/inventory`, `survival/backpack`, `survival/world_map`.
  - **Correction to an earlier claim in these notes:** rpg/survival/topdown were described as "already having correct modal screens". That was wrong — it was based on the files existing, not their contents. `Inventory.cs`/`Character.cs`/`Backpack.cs`/`WorldMap.cs` only wire a Close button. Each affected genre doc now records the correction. The 23 reusable components WERE verified this pass (all exist with real public APIs — `Table.SetData`, `Toast.ShowToast`, `Counter.CountTo`, `Modal.Open`, etc.), so the reuse plan holds.
  - **External research folded in** (sources listed at the foot of `HUD_DESIGN_PER_GENRE.md`): the **80/20 attention split** (~80% of visual attention is on gameplay, ~20% on HUD) as the argument for progressive disclosure; hierarchy by **positional stability**, not size alone; hybrid diegetic/non-diegetic as the norm rather than full diegesis; the RTS finding that **spreading data across many zones splits attention** (supports one dense bottom block for command card + selection panel) and that hotkeys belong on every command button; and the survival guidance that vitals are specified first, sit bottom/top-left, follow colour convention, and read better **themed than as plain rectangles** (so `MeterBarComponent` needs a themed fill mode). [Game UI Database](https://www.gameuidatabase.com/) and [Interface In Game](https://interfaceingame.com/) are the reference libraries to check layouts against during implementation.

  **Staged delivery** (each stage independently shippable):
  - [ ] **30.0 — Per-genre state layer (prerequisite), SAVEABLE via `ISaveable`.** Verified the existing contract rather than assuming: `Beep.ECS.ISaveable` (`ecs/ISaveable.cs`) declares `Save(GameStateData)` / `Load(GameStateData)`; a component only participates once it joins the `SaveableHelper.Group` (`"saveables"`) group, which the canonical implementations do in `_Ready` when their `[Export] ParticipatesInSave` is on (see `HealthComponent` lines 31/52/165/171). `GameStateManagerComponent.SyncAllSaveables()` runs before every save and `RestoreAllSaveables()` after every load. Current implementers: `HealthComponent`, `InventoryComponent`, `EquipmentComponent`, `StateMachineComponent`, `GameApp`, plus three UI hosts.
    - **Where genre state lives — checked, no format reshape needed.** `GameStateData` slots are player-centric by design (Movement / Combat / Inventory / Progression / Session / World) and the `ISaveable` doc-comment explicitly warns against widening them. But `GameStateData.GameData` is a free-form `Dictionary<string, Variant>` that **is serialised both ways** (`game_data` appears in `ToDict` line 159 AND `FromDict` line 192), and `SetGameData`/`GetGameData` already exist and are already used by nine screens (`build_selection`, `retry_bonus`, `research_selection`, …). Genre state therefore persists through `GameData` under a namespaced key per genre (`citybuilder.*`, `strategy.*`, …) with **no change to `SaveFormatVersion` (currently 1)** and no per-genre pollution of the shared schema. `WorldStateData.WorldData` is an equivalent second option if a value is world- rather than session-scoped.
    - **Every state component from this stage implements `ISaveable`**, exports `ParticipatesInSave` (defaulting **on** for these, unlike `HealthComponent` — genre state is global and single-slot, so the multi-writer hazard the interface warns about does not apply), joins the group in `_Ready`, and keeps its key names as `const string` next to symmetric `Save`/`Load` in the same file so an untyped-dictionary typo cannot desync the two.
    - **Round-trip is part of the definition of done**, not a follow-up: save → change state → load → assert every genre stat restored, verified at runtime for all ten genres.
  - [ ] **30.0a — Components to build.** Build the components that actually own and emit each genre's stats, so no HUD element is fed by `Placeholder(...)`. Five genres have zero real sources today and need one each: `CityEconomyComponent` (treasury, monthly delta, power/water, happiness, RCI demand, date+speed), `StrategyEconomyComponent` (resources + rates, population cap, turn/era), `SurvivalVitalsComponent` (health/hunger/thirst/stamina decay + threshold events; `TemperatureComponent` already exists), `RaceStateComponent` (lap, position, split/delta, speed, gear), `CardMatchStateComponent` (hand, piles, energy, intents). rpg/puzzle/shooter need their remaining placeholders replaced (`health`/`mana`/`quest`, `target`/`moves`, `ammo`/`wave`). Each emits signals; the `*HudComponent` binds them via the existing `SetStat` contract.
  - [ ] **30.1 — Shared HUD rules.** `SafeAreaComponent` wrapping every HUD root; re-anchor from one corner into the 7 screen regions; `mouse_filter = Ignore` on non-interactive HUD nodes so the HUD stops eating gameplay clicks; damage feedback via `ScreenFlash`/`Vignette`; events via `ToastNotification`. Touches all 10 mains, no new components.
  - [ ] **30.2 — The 6 SHARED components.** `MeterBarComponent` (4 genres), `HotbarComponent` (2), `AbilityBarComponent` (2), `SelectionPanelComponent` (2), `DayNightClockComponent` (2), `PipHealthComponent` (2). Deliberately first: these cover four genres before any genre-specific work begins.
  - [ ] **30.3 — City Builder** (flagged as the worst gap): `BuildToolbarComponent` (categories -> palette, reusing the existing `build_menu.tscn` as its content), `DemandMeterComponent` (RCI), `GameSpeedComponent` (pause/1x/2x/3x), `InfoViewComponent` (traffic/pollution/power overlays), selection panel, budget delta, alerts feed.
  - [ ] **30.4 — Strategy**: command card (3x4 ability grid), selection panel, production queue, population cap, resource income rates, idle-worker/alerts.
  - [ ] **30.5 — RPG + Shooter** (share `AbilityBar`): orbs/bars, cooldown sweeps, XP bar, buff row, target nameplate, quest tracker; ammo counter, reload ring, hitmarker, damage-direction, killfeed.
  - [ ] **30.6 — Survival + Top-Down** (share `Hotbar` + `DayNightClock`): meters as bars with critical warnings, status effects, clock/date, interaction prompt, compass.
  - [ ] **30.7 — Racing + Puzzle + Card + Platformer**: speedometer/gear/lap-delta/track map; objective panel + star progress + booster tray + combo popups; hand layout + pile counters + end-turn + relics; pip health + collectibles + run timer.
  - [ ] **30.8 — Verify**: render every genre main at 1280x720 and inspect, as with Stages 21-28. Numbers checked at runtime, not read off the scene files.
  - [ ] **30.9 — Replace the 6 Close-only mockups**: `citybuilder/districts`, `citybuilder/economy`, `rpg/character`, `rpg/inventory`, `survival/backpack`, `survival/world_map`. Their LAYOUTS are sound — the work is binding, not redesign — and under the production-ready rule these cannot remain as they are. Adds the two things real games have and these do not: **inventory carry-weight / slots-used**, and **global resource counts on the crafting screen** (currently only per-recipe `have/need`).

  **RULE — production-ready only (no placeholders, no mockups, no legacy).** This settles the question previously recorded here as "real signals vs demo values": it is **real signals**. A HUD element is not delivered until the component that owns and emits its data is delivered with it. `Placeholder(...)` is banned as an end state; no scene may carry a hardcoded figure; no screen ships Close-only; when a readout moves from a Label to a widget the Label is removed rather than left as a fallback. **Stated cost:** this makes Stage 30 more than a UI pass — it adds the per-genre state layer below. Accepted, because the alternative is ten screens that look finished and display invented numbers, which is exactly what the audit found.

---

## Status legend

- `[x]` done · `[~]` in progress · `[ ]` not started
- Update this file as each stage completes. Detailed task lists live in
  `plans/genre-templates/stage-N-*.md`.

## Stage 31 — Collapsible HUD panels (2026-07-26)

- [x] `CollapsiblePanelComponent` — header-above-panel, height tween, zero-space collapse,
      `ToggleAction` hotkey binding, `ISaveable` + `saveables` group, key `hud.collapsed.<panel>`
- [x] Cross-genre rule documented in `docs/HUD_DESIGN_PER_GENRE.md`
- [x] Section 9 added to all 10 per-genre docs, each naming its 4 panels, default state, and
      which panel must never default to collapsed
- [x] Wired + runtime-verified on the city builder `BuildBar` (fold, reflow, header survives,
      group membership)
- [x] Save/load round trip for collapsed state — **PASS** with a mutation guard:
      saved `IsCollapsed=true` -> mutated to `false` -> restored to `true` (visible=false).
- [ ] Wire the remaining panels named in each genre's section 9. **Blocked, not deferred:**
      an audit of the 10 genre scenes found only **7 of the 40 named panels exist**
      (citybuilder has all 4; strategy/rpg/topdown have `Minimap`; shooter, survival, cardgame,
      racing, puzzle, platformer have none). The other 33 are part of the per-genre HUD build in
      Stages 30.4-30.8 — they cannot be made collapsible before they are built.
- [x] Scene work for citybuilder: `DemandStack` / `MapStack` VBox wrappers added in-scene and
      `DemandMeterPath` updated in the same edit. **3/3 collapsible with header, verified**
      (`BuildBar`, `DemandMeter`, `Minimap` — fold, header survives, restore).
- [x] `Alerts` reclassified **not collapsible**: it is a Node-based `ToastNotificationComponent`,
      not a panel Control. Toasts are transient and self-dismissing — there is no persistent
      rect to fold. citybuilder.md corrected; cap the queue instead.
- [x] Fixed: `CollapsiblePanelComponent` used `GetParent<Control>()`, which THROWS on a
      mismatch — an unhandled `InvalidCastException` killed `Setup()` instead of taking the
      "parent is not a Control" warning path already written below it. Now a safe `as` cast.
- [ ] Same VBox-wrapper scene work for `Minimap` in strategy / rpg / topdown (3 panels)
- [x] Fixed en route: `DemandMeterPath` still read `"DemandMeter"` after the meter moved into
      `BottomDock/MinimapRow`, so the RCI meter had been drawing empty channels rather than
      real demand since that restructure.

## Stage 32 — Game-centric HUD, from the reference art (2026-07-26)

Reference: `Example_Art/citybuilder1..5.png`. Spec derived in `docs/hud/citybuilder.md` §10.
The verdict on the current HUD: it reads as an application toolbar because it is built from
full-width strips of Labels, where every reference builds from discrete, heavily-outlined,
icon-first objects clustered in the screen corners.

- [x] **Resolution model fixed** — `viewport_*` is now the fixed 1280x720 design canvas and the
      developer's resolution goes to `window_*_override`; `stretch/aspect` `keep` -> `expand`.
      Writing the chosen resolution into the viewport had been redefining the coordinate space
      the UI is authored in, shrinking panels and fonts at higher resolutions.
- [x] **Collapse affordance** — full-width header bar replaced by a 22x22 floating chevron
      pinned to the panel corner, positioned per-frame from the panel's rect. Works over any
      host (removed the VBoxContainer-only constraint) and never touches the panel's node path.
- [x] **`ResourceBadgeComponent`** — the defining element: circular icon frame overhanging a
      rounded capsule, chunky dark outline, drop shadow, optional capacity fill. Built on
      StyleBoxFlat so radius/border/shadow are native. Verified rendering with the real
      citybuilder icons and per-resource colour.
- [ ] Replace `TopBar` in citybuilder_main with a TR badge column (component is ready; the
      scene rewrite and `CityBuilderHudComponent` rebinding are not done)
- [ ] Build palette -> icon tiles (icon over caption) instead of text rows
- [ ] Category tabs -> icon tabs
- [ ] Chrome register: the sci-fi glass suits shooter/strategy but is wrong for citybuilder —
      needs thicker outline, saturated fill, real shadow
- [ ] Minimap into a round frame; add the L/R vertical icon rails
- [ ] Apply the same §10 audit to the other 9 genre docs

### Stage 32b — UI kit anatomy (from Example_Art/gameui1..7)

Seven full UI kits added. Spec: `docs/GAME_UI_KIT_SPEC.md`. The finding in one line: **every
element in every kit is made of overlapping parts; ours are one rectangle with text inside.**

- [x] Spec written, incl. which reference register maps to which genre
- [x] Collapse chip now **overhangs** the panel's top-right corner (dx/dy = 0.0 from the corner
      on all three citybuilder panels), matching gameui4/5/7
- [ ] Overhanging **title banners** on every panel/modal (currently inline Labels) — the single
      most repeated element across all 7 kits
- [ ] Panel **frame + recessed inner well** (two-tone); today panels are one flat rect
- [ ] `MeterBarComponent` icon cap on the bar end (gameui6)
- [ ] Icon-button family: square, rounded, thick outline, Normal/Over/Click/Disabled state set
      (gameui3 names these explicitly)
- [ ] Wire the per-theme semantic colours (success/danger/warning/info) to button intent — they
      are defined in all 50 themes and unused by the HUD

**Licensing note:** gameui2/3/7 are watermarked comps (Dreamstime, Game Art Partners, Envato).
Style reference only — not shippable art. Shipped art stays CC0 Kenney or authored.

### Stage 32c — per-genre UI pass (2026-07-26)

- [x] `PanelFrameComponent` — frame + recessed well + **overhanging title banner** in three
      shapes (Plaque / Ribbon / Ellipse), heavy outline, drop shadow. The most repeated element
      across all 7 kits and the one we got most consistently wrong.
- [x] Palette resolved **from the active theme**, not per scene — otherwise it is 10 genres x
      5 themes x every panel to hand-maintain, and wrong the moment a theme changes.
      `ShapeForGenre()` assigns the register: ribbon for wood/adventure genres
      (rpg/survival/topdown/cardgame), ellipse for candy (puzzle/platformer), plaque otherwise.
- [x] Applied to the 6 genres with a `TopLeft` stat cluster — cardgame, platformer, racing,
      rpg, shooter, topdown — across all 3 repos (18 scene files).
- [x] Two layout bugs fixed en route: `anchor_right=1.0` on a fixed-size box made the frame
      1508px wide (offset measured from the parent's right edge), and `grow_vertical=0` on
      `TopLeft` grew the stat cluster UPWARD off the top of the screen once it exceeded 40px.

- [x] **Frame is now content-driven** — `TargetPath` points the frame at the cluster it wraps
      and it sizes from that node's `CombinedMinimumSize` each frame. Verified per genre:
      rpg 166x139, shooter 106x166, cardgame 118x69 — each fitting its own stat count.
      Three sizing bugs found and fixed on the way:
      (a) sizing from `Size` instead of `CombinedMinimumSize` produced a 1348px frame, because
          the cluster is an anchored container that stretches full-width while its content is
          narrow;
      (b) deriving Position from an anchored target landed the frame at the parent origin, so
          the frame now sizes only and keeps the placement the scene gave it;
      (c) `grow_vertical = 0` on the frame — the same upward-growth defect already fixed on
          `TopLeft` — dragged it back to y=0 and clipped the overhanging banner.
- [ ] The other 4 genres (citybuilder, puzzle, strategy, survival) have no `TopLeft` cluster —
      they use a `TopBar` or bare labels and need structural work before they can be framed.
- [ ] Still outstanding from 32/32b: replace citybuilder `TopBar` with the badge column,
      icon tiles for the build palette, icon tabs, `MeterBarComponent` icon cap, semantic
      colourways, and the icon-button state-set family.

### Stage 32d — framing the remaining genres (2026-07-26)

- [x] **puzzle** (`TopCenter`, ellipse banner "Goal") and **survival** (`Vitals`, ribbon banner
      "Vitals") framed. 8 of 10 genres now carry a framed stat panel.
- [x] **Banner-aware sizing** — the frame was sized to its content alone, so the banner ate the
      top of the well and the last row of a cluster hung out of the bottom (survival's 4th vital,
      shooter's 4th stat). The WELL must be content-height, so the frame is content + padding +
      banner. Verified: survival 116->150, shooter 150->200, rpg 139->173, puzzle 85->119, each
      now containing its full cluster.
- [x] Clusters that are plain `VBoxContainer`s (puzzle, survival) have no margin constants to
      push content into the well, so their own offsets do it — unlike the six `MarginContainer`
      clusters where theme margins were used.

- [ ] **strategy** and **citybuilder** are the 2 remaining genres. Both use a full-width `TopBar`,
      which the reference art says should not exist at all — they need the badge-column treatment
      (Stage 32), not a frame around a bar. Deliberately not framed.
- [ ] Cosmetic: label text runs a few px past the well's right edge; the horizontal padding
      should scale with font size rather than being a flat 14px.
- [ ] Still open from 32/32b: citybuilder `TopBar` -> badges, icon tiles, icon tabs,
      `MeterBarComponent` icon cap, semantic colourways, icon-button state-set family.

### Stage 32e — citybuilder badge column (2026-07-26)

- [x] `TopBar` (the full-width strip of Label pairs) **deleted** and replaced with a right-aligned
      `Badges` column of 5 `ResourceBadgeComponent`s, colour-coded per resource — population blue,
      treasury gold, power orange, happiness green, date purple.
- [x] `CityBuilderHudComponent` drives **either a Label or a badge**. Binding to Label only would
      have meant rewriting every genre's scene in lockstep, and would resolve to null the instant
      a scene upgraded — the same failure mode as the DemandMeterPath move. `ResolveReadout()`
      accepts both and warns once if a path is neither.
- [x] Power and happiness now carry a **capacity fill** (0.43 / 0.53 verified), which the old
      Label pair could not express. `Tint()` sends the over-capacity colour to the badge's fill
      rather than its text, because the badge maintains its own contrast against its plate.
- [x] **Verified bound, not merely present** — a badge showing its default "0" is
      indistinguishable from one that failed to resolve, so the probe compares each badge's
      value against the live economy. 5/5.
- [x] Badge column moved below `SpeedBar`, which occupies the same top-right corner (the first
      badge was rendering underneath the speed controls).

- [ ] **strategy** is the last genre on a full-width `TopBar` — same badge treatment applies.
- [ ] Build palette still text rows; category tabs still words. Icon tiles + icon tabs remain
      the largest visible gap against `Example_Art`.

### Stage 32f — building icons + icon tiles (2026-07-26)

- [x] **7 building icons drawn** — apartment, shop, road, school, clinic, power_plant,
      water_tower. The catalogue has 10 buildings and shipped art for 3 (house, factory, park).
      Drawn to match that existing family: 64x64, flat two-tone, dark navy outline, one accent
      per subject. Generator `tools/.../gen_building_icons.py` **never overwrites** an existing
      icon, so hand-authored replacements survive a re-run. All 10 now have art.
- [x] **Build palette is icon tiles** — icon above caption above cost, replacing the
      `"House x3 / 1,200"` text rows. Composed from child controls, NOT `Button.Icon` +
      `Button.Text`: Godot lays those out in a single ROW, so the caption printed straight
      across the icon regardless of alignment settings. Children are mouse-transparent so the
      Button still takes the click — verified: clicking a tile moved treasury 50,000 -> 48,800
      and the caption updated to "House x1".
- [x] `RefreshAffordability` writes into the tile's child Labels; writing `Button.Text` would
      have printed a second caption over the icon.

- [ ] Category tabs are still words ("Zones", "Roads"...) — they want icons too, but there is
      no icon set for the four categories yet.
- [ ] strategy is still on a full-width `TopBar` — last genre needing the badge column.

### Stage 32g — game-art register at the COMPONENT layer (2026-07-26)

Correction to how 32c–32f were approached. Those patched scenes one at a time, which is the
wrong layer: the chrome for every control is generated by `ThemePresetComponent`, so a scene
pass can only ever fix the controls someone remembered to patch.

- [x] `ApplyGameArtRegister()` added at `StampGeometry` — the single choke point EVERY control
      type's stylebox passes through (Button, Panel, LineEdit, ProgressBar, TabBar, Tree,
      PopupMenu, Slider...). One change, whole UI, all 10 genres x 5 themes.
- [x] Register = heavy outline (>= `GameArtOutline`, default 3px) in an ink colour **derived
      from the active palette**, plus a drop shadow where the genre's geometry profile did not
      already specify one. Palette-derived rather than a fixed black so a parchment theme gets a
      brown outline and a sci-fi theme a blue-black one — a hardcoded colour would flatten all
      50 skins into the same border.
- [x] Genre still owns radius/padding/weight via `geometry.json`; theme still owns colour;
      the register only enforces the "outlined object" reading the kits share.
- [x] Exports `GameArtChrome` / `GameArtOutline` / `GameArtShadow` so a deliberately flat skin
      can opt out.
- [x] **Verified across control types**, not just the patched ones: TabContainer, PopupMenu,
      Tree, HSlider all report border=3 shadow=6; Button, PanelContainer, LineEdit, ProgressBar
      resolve to StyleBoxTexture, where the art carries its own outline and the register
      correctly stands aside. 8/8.

---

## Correction to Stage 32's closing claim

The line above — *"Button, PanelContainer, LineEdit, ProgressBar resolve to StyleBoxTexture,
where the art carries its own outline and the register correctly stands aside. 8/8"* — was
wrong. Those four were counted as passes without checking whether the art they resolve to
actually carries the register. It does not: they loaded the pale menu texture set, so the four
most visible control types were untouched. That is why the UI kept reading as "colours changed,
still looks like an app" after every pass. Corrected in Stage 33.

---

## Stage 33 — Register reaches every control (done)

- [x] `StampGeometry` handled `StyleBoxFlat` only; extended to `StyleBoxTexture` so Button,
      PanelContainer, LineEdit, ProgressBar, Slider, ScrollBar, Separator and Dialog receive it.
      Measured before/after: textured `modulate` was `#ffffff` in **all 50 skins**; now tracks the
      palette (`#bdc2c5` dark vs `#eff9ff` vibrant).
- [x] Theme layer authored: 50 themes collapsed to **21 distinct colour sets**, 6 genres had all
      5 themes byte-identical. All 50 now distinct in colour *and* geometry.
- [x] Per-genre silhouettes, menu + HUD tiers (740 nine-patches), with
      `tools/genre_shapes/verify_ninepatch.py` enforcing edge uniformity, tile seams and centre
      tone. TileFit edge ornament so genre identity survives on wide controls.
- [x] 39 colour literals across 15 components replaced by palette roles; 12 deliberate ones
      (fades, dims, multiply identities) documented and kept.
- [x] All font sizes and text-container metrics derived from the theme font. 350/350
      theme x palette pairs now clear WCAG AA (was 9 failures, worst 3.29).
- [x] 8 genre simulations built and wired; **0 placeholder HUD bindings**, 0 placeholder labels.

---

## Stage 34 — Game UI Kit (planned, not started)

> **Full plan: `plans/game-ui-kit/PLAN.md`.**
> **Widget catalogue derived from the reference pictures:
> `plans/game-ui-kit/CATALOGUE-FROM-ART.md`** — every widget traced to the image it came from.
>
> `Example_Art/` holds **TWO style families that must not be averaged**:
> - **casual/mobile** (`ui1` `ui2` `skilltree1` `store`) — thick uniform dark outline, flat
>   saturated fill + top band, large radius, hard drop shadow, overhanging icon caps.
>   **Reproducible procedurally — target this first.**
> - **painted fantasy** (`rpgui` `Upgrades`) — frame around a separate inner plate, carved
>   bevel, rivets, small radius, hand-painted material. **Needs 9-patch art sliced from the
>   sheets; not reachable procedurally.**
>
> The failed phase-A attempts measured the PAINTED family and fed those numbers to a PROCEDURAL
> renderer. That mismatch is why each parameter change made it worse.

Stages 31–33 all deepened the *theme generator*. That was the wrong ceiling: a generated
`StyleBox` can make a `Button` prettier, but a game button is an **assembly** — layers, a
non-rectangular silhouette, sub-elements that overhang their parent, and sculpted (not faded)
states. `Example_Art/Upgrades.png` contains nine distinct widgets and Godot's theming can
express exactly one of them.

**Decision: build a widget library under `ecs/ui/kit/`. Stop deepening the theme generator.**

- [ ] **A — Foundation.** `KitControl` / `KitLayer` / **`KitMaterial`** / `KitShape` /
      `KitAttach` / `KitState`. `KitMaterial` is the named layer stack
      (`Base → Bevel → Gloss → Rim → Sparkle`) a genre defines once and every widget inherits —
      confirmed by the owner-supplied golden-kit reference, where one gold material carries a
      dozen different silhouettes. Plus **`KitGeometry`** — per-genre corner/ratio/padding/rim/
      bevel, because the first proof rendered five genres as the same brown plate: geometry was
      constant on the base class and texture was never consulted.
      **Geometry must be MEASURED from `Example_Art/`, not invented.** Measuring `rpgui.png`
      showed the structure was wrong, not just the numbers: a game control is a **frame around a
      separate inner plate** (two nested shapes), where `KitControl` draws one plate with a
      bevel. That is why every genre still reads as generic. See PLAN.md 4.2a.
      **Gate: one button across 10 genres must be tellable apart in GREYSCALE.** If only colour
      separates them, phase A is not done. See PLAN.md §4.1–4.2.

      `KitAttach` is the piece with no Godot equivalent: a sub-element pinned to a host anchor
      and allowed to **overhang** it. Done when a 3-layer button with an overhanging badge
      renders and reskins across 3 genres.

      **The four known defects — status as of 2026-07-28 (all four closed):**
      1. ~~`DrawMaterial` draws inner layers as `Round` regardless of the host silhouette~~ —
         fixed; gloss is cut to the host shape.
      2. ~~`CornerFor` uses `min(w,h) x fraction`~~ — fixed; angular shapes take the cut from
         height, rounded ones keep the min-side radius rule.
      3. ~~Single plate, no frame~~ — fixed; frame + inner plate are two nested shapes.
      4. ~~`verify_greyscale.py` is not a valid gate~~ — **rewritten**, see below.
      (1-3 were already fixed in code before this session; only this tracker was stale.)
- [ ] **B — Core widgets.** Panel, Banner (ribbon/plaque/shield/ellipse), Button, IconButton,
      Tab, Badge, Meter. Done when settings + one HUD contain no generic `Button`/`PanelContainer`.
- [ ] **C — Motion.** Collapsible, Drawer, Accordion, Carousel — slide/clip reveals that open
      toward the anchored edge.
- [ ] **D — Structured.** Plus **ornaments** (crown/wings/star/laurel/trophy) as non-interactive
      overhanging attachments, and **status chips** (pentagon ✓/✗).
- [ ] **D — Structured.** SlotGrid, **Tree** (skill/upgrade nodes, tier columns, connector lines
      coloured by branch and unlock state, locked sculpt, corner cost badges), List, CardHand.
- [ ] **E — Art.** Per-genre **material** textures per layer per widget
      (`kit/<genre>/<widget>_<layer>.png`) — wood grain, brushed steel, carbon weave, candy gloss
      and so on. `gen_all_genres.py` currently emits flat greyscale sculpts, which is why no genre
      reads as a material. Nine-patch verifier **and** greyscale test must both pass.
- [ ] **F — Migration.** Port 10 genre HUDs and 25 screens onto the kit.
- [ ] **G — Retire.** Theme generator reduced to editor/inspector fallbacks.

Skinning is unchanged and already correct — **genre → silhouette, theme → colour, palette →
tint** — and the kit consumes it via `UiSurface`. Six rules carry over, each one a defect already
paid for: no colour literals, no pixel font sizes, no `AddThemeStyleboxOverride` on a kit widget,
no reparenting a tree others hold NodePaths into, ornament inside 9-patch margins, one global
skin source.

**Gate:** nothing after B starts until a rebuilt screen sits beside `Upgrades.png` and holds up.

**Open questions for the owner** (in the plan, §6): procedural vs purchased art; migrate all 10
genres or prove on two; and whether the kit retires the `beep_ui` GDScript addon's 84 widgets.

---

# Per-image art documentation — COMPLETE (44 files, 43 unique images)

`plans/game-ui-kit/art/` — **one measured document per image in `Example_Art/`**, index at
[art/INDEX.md](game-ui-kit/art/INDEX.md).

Standard applied, after a first pass was rejected for measuring one widget per image:
**every widget on the screen gets its own numbered entry with its own measurements**, taken
from scanlines through the real pixels (`tmp/m.py segr|segc`). Where a widget could not be
isolated cleanly, the entry says so rather than guessing.

`ui7.png` is a byte-identical duplicate of `gameui9.png` (same MD5) — 43 unique images.
`ui5.png` (1200×3579) is documented at the level of its material families, not per
instance; that one per-instance pass remains outstanding and is flagged in its document.

## The finding that explains every failed kit iteration

Two ratios distinguish **painted** from **flat**, both measurable automatically:

| test | painted | flat |
|---|---|---|
| bottom : peak lightness inside a plate | **0.18–0.27** | **0.76–0.84** |
| rim : body lightness | **1.78–2.05 ×** | **1.3–1.5 ×** |

Seven measurements across six unrelated sheets. Feeding `rpgui.png`'s painted proportions to
a flat procedural renderer was the root error of the earlier sessions. `verify_greyscale.py`
never caught it because it normalises size away and compares histograms — it scored 45/45
PASS while the silhouettes were near-identical rectangles. **These two ratios are what that
gate should check.**

## Corrections to earlier work, now in the documents

- **`PLAN.md` §4.2a's "frame ≈ 12 % of height" is wrong** — re-measured on `rpgui.png`'s
  PLAY button at **0.157** (wood) / **0.21** (including the inner keyline).
- **The frame formula does not generalise.** Two regimes: **structural**
  (`3.5px + 0.07 × height`, carved/wood families) and **hairline** (constant 1–3px
  regardless of size — `rpgui1`, `racing4`, `rpgui2`). Needs a mode flag, not tuned constants.
- **`CATALOGUE-FROM-ART.md` §D is wrong as a generalisation** — `gameui2`, `gameui4` and
  `gameui5` all contain checkboxes; `gameui2` contains a radio group. Dropdowns appear in
  none of the 43 images.
- **The "welded footer" is two widgets**, not one: a **status band at 0.19 × card height**
  (skilltree1, store1) and an **action button at 0.10 ×** (store). Modelling them as one
  would have produced a BUY button at twice its correct height.
- **`citybuilder4`'s lock plate is a desaturation, not a polarity flip** — my first reading
  of that image was wrong and is corrected in its document.

## Settled rules (3+ independent measurements each)

- **Disabled / unavailable = drain saturation** (S → 0.01–0.05); lightness may *rise*. **7×**
- **The palette goes on ONE element**; the other stays neutral. **5×**
- **Segmented progress is the default**, continuous is the exception. **7×**
- **HUD rail ≈ 3 % of screen height** (~30px at 1000px wide). **5×**
- **Top-right corner straddle = the attention anchor.** **8×**
- **Empty/track = a dark tint of the surface's own hue**, never grey. **4×**
- **Gloss band = 1.4 × body.** **4×**
- **Panels recede (0.67–0.87 ×) on dark saturated surfaces, raise (1.13–1.40 ×) on light
  ones** — derivable from the parent surface rather than set by the skin author. **3×**
- **Locked states state their requirement in words**, not just a padlock. **5×**

## New kit requirements the pass produced

- **`KitState.Selected` cannot be one renderer** — **17 distinct mechanisms** measured, and
  the choice follows widget class: card carousels use an outline (3 refs), tab strips use
  fill/elevation/underline, list rows use a fill. Selection cues also **stack**.
- **`KitState.Empty` has three meanings** — blank (bag), invite `+` (equip), locked with a
  requirement string. `ui3.png` shows all three on one screen.
- **`LabelValuePair`** (`rpgui3`): two welded plates of opposite polarity, **2 : 1** width,
  2px joint. The most reusable dense-information widget in the folder; the kit has none.
- **`CollapsiblePanel` now has a measured spec** (`ui8`): handle **outside** the panel on the
  moving edge, **33px**, dark plate, maximum-contrast white chevron. This is the widget the
  kit was originally asked for.
- **`RadarChart`** (`racing3`) — a missing primitive, fully procedural, useful to racing, rpg
  and strategy.
- **Hand-drawn outline mode** (`ui6`): position jitter **+ alpha modulation 0.46–0.76 ×
  the surface + width 1–3px**, seeded per control. The alpha modulation is what makes it
  read as pencil rather than a wobbly line.
- **`InputHint` widget with chord support** (`L2 + ✛`) — 3 refs.
- **Per-run text roles** inside a paragraph — 3 refs.
- **Number-formatting policy** (K/M/B abbreviation) — 2 refs.
- **Plate alpha** — `citybuilder2` proved translucency by measuring **one plate as two
  colours** over two backgrounds.

## Acceptance test for the material axis

`ui5.png` draws **one dialog geometry in ~10 materials** (wood, parchment, stone, vine-stone,
bone, book, taped card, chained metal, signpost, fabric) with no layout change. That is the
proof the two-axis model is right, and it is the gate for phase E: one layout must render
convincingly in all ten **without touching the layout**.

---

## Stage 35 — A greyscale gate that actually discriminates (2026-07-28)

The gate is now runnable, self-proving, and run. Phase A's acceptance test has a real number
against it for the first time.

- [x] **Render harness** — `tools/genre_shapes/KitProofProbe.cs` + `kit_proof.tscn` render one
      `KitButton` per genre to `gs_<genre>.png`. There were **no `gs_*.png` anywhere in the
      repo**, so the old "45/45 PASS" was not reproducible by anyone.
      **Colour is held constant** (no theme applied, one 14pt font), so any separation the gate
      measures is provably geometry and material. Letting each genre bring its own theme would
      let a lighter palette masquerade as a different material — passing the gate on exactly the
      thing it exists to reject.
      Must run **windowed, not `--headless`**: the dummy renderer never fires
      `RenderingServer.FramePostDraw`, so the probe hangs there forever (60s timeout, exit 124).
- [x] **`verify_greyscale.py` rewritten.** The old one had two defects that let colour alone
      carry a pass: it **resized every silhouette to 128x64** before comparing (erasing
      proportion, which `HeightRatio`/`PadRatio` make a genre tell), and its second axis was a
      raw greyscale **histogram**, which moves when nothing but the fill changes. With
      `shape OR texture`, any pair could pass on colour.
      Both axes are now colour-invariant by construction: **outline** (aspect at natural size,
      12 corner-occupancy samples at three radii, edge rake) and **structure** (lightness
      profiles standardised to zero mean and unit range, so a uniform recolour scores zero).
- [x] **The gate is self-proving** — `--selftest` synthesises the cases and asserts the verdicts,
      because a check that has only been seen to pass is not evidence:
      ten plates differing **only in fill colour** must FAIL (the old gate scored this 45/45
      PASS); genuinely different silhouettes must PASS; and a pure colour swap must score
      **0.0000 on both axes**, which it does exactly.
- [x] **Weighted outline.** A flat mean let one feature be diluted: a genre differing only in
      PROPORTION (racing 126x37 vs platformer 142x50 — a real tell) moved it by 0.033 and read as
      identical. Aspect now carries a third of the weight alone.
- [x] **Diagonal profiles added.** The centre cross **never crosses a corner**, so the gate was
      blind to corner ornament — it scored citybuilder vs strategy 0.069 against a 0.070 bar
      while they differ by four visible studs.
- [x] **Material ratios measured WITHIN the plate.** Scanning the full widget height counts the
      dark ink rim as "the bottom of the plate" and drove `bottom:peak` to 0.02-0.14 for every
      genre — below even the painted range, a measurement artefact rather than a reading.

### Result — PASS, 0 indistinguishable pairs, 1 marginal

Verified by running it, not by reading the scene files. `dotnet build` clean, `validate_scenes.sh`
PASS, montage inspected.

- **`citybuilder` vs `strategy` is the one unresolved pair** (outline 0.029 against a 0.040 bar,
  rescued by structure 0.071 against 0.070 — a **1%** margin, reported as MARGINAL rather than
  folded into the pass count). Both are `Rect`, both carved, and their corners differ by
  0.06 vs 0.04. Confirmed by eye on the montage, not just by the number. The documented fix is
  **strategy's corner brackets** (PLAN.md 4.2b: "square + brackets"), which is new drawing work.
  Thresholds were deliberately NOT moved to make this pass.

### Applied from the measurements

- [x] **`FrameRatio` -> `FramePx(height)` with a mode flag.** A single ratio cannot fit both ends
      of the measured range (citybuilder5: a 35px capsule carries 6px = 0.17, a 107px tile 11px =
      0.10). Now `Structural` = 3.5px floor + 0.07 x height, `Hairline` = a constant 1-3px that
      does not scale (rpgui1/racing4/rpgui2), `None` = a bare plate.
- [x] **`PlateShade` -> `PlateShadeFor(elevation)`** — see the correction folded into
      `art/INDEX.md`. 0.12 is the recessed readout; the raised tile beside it is 0.875.
- [x] **`RimBrightness` — rim POLARITY is now a genre axis.** The gate measured rim:body at
      **0.16 for all ten genres**: an identical dark ink line everywhere, carrying no genre
      identity, while the references use rim polarity as one of their loudest tells (carved
      families 1.78-2.05x BRIGHT, casual/mobile a thick DARK outline).
- [x] **Three registers, deliberately not averaged** (PLAN.md 34's rule): CARVED (structural
      frame + bright rim) rpg/survival/strategy/citybuilder · CASUAL (no frame, thick dark
      outline) platformer/puzzle/cardgame/topdown · TECHNICAL (hairline + thin light rim)
      shooter/racing. Visibly distinct on the montage.
- [x] Two bugs found by measuring the result rather than trusting the change: the bright rim
      **overshot 2.05x to 6.14x** (lerping toward white by "how far past 1.0" instead of hitting
      a luminance target), and racing asked for a 1.45x rim and rendered **0.17x** because the
      inner plate's dark ink rim sat on top of a 1.5px hairline frame and swallowed it.

### Still open (measured, not guessed)

- [ ] **`rim:body` lands ~0.6x of what each genre asks for** (carved 1.11-1.28 against a
      1.78-2.05 target; casual 0.07-0.09 against 0.18-0.22). The requested value is a multiple of
      the plate, but the measured "body" is the median of the plate interior, which the gloss
      lifts. Either the gloss belongs outside the body sample or `RimBrightness` should target
      measured body rather than nominal plate.
- [x] ~~**The casual register still renders painted** (`bottom:peak` 0.23-0.26 against a flat
      target of 0.76-0.84)~~ — **WITHDRAWN, this was a measurement artefact, not a defect.**
      The material scan ran down the widget's CENTRE COLUMN, which goes straight through the
      label: on platformer it took `peak` = 221 off the "PLAY" glyph against a 58 plate, so the
      ratio was measuring text contrast and no change to the material could move it. Each row is
      now represented by its **modal tone**, which a glyph (a minority of any row) cannot shift.
      Re-measured: casual reads **0.67-0.83** against the 0.76-0.84 flat target — three of the
      four classify `flat` outright. **The registers were already correct.**
      Proven rather than assumed: an A/B with the dark bevel restored moved `bottom:peak` by
      **0.00** on all four casual genres, because the bevel sits outside the measured plate
      window entirely. The lesson is the one this file keeps relearning — the instrument was
      wrong, and a plausible cause was accepted for it before the instrument was checked.
- [ ] `strategy` corner brackets, to separate the one marginal pair.

**Cleaned up en route (pre-existing, unrelated):** `citybuilder_main.tscn` carried five
`IconRingColor = Color(...)` lines against an export `ResourceBadgeComponent` deliberately
removed in Stage 32e, so Godot silently dropped all five. `validate_scenes.sh` was failing on
them; removed, and it is green again.

---

## Stage 36 — Phase B begins: the first widgets built FROM the art (2026-07-28)

Course correction. Stages 34-35 built measurement apparatus and tuned one `KitButton`'s numbers.
That is not the deliverable: the art pass exists to produce **widgets**, and the catalogue names
specific ones with measured specs that the kit did not have. Three built, each from its
measured document, each verified by rendering it and looking at it.

- [x] **`KitLabelValue`** — from `art/rpgui3.md` widget 1, the densest reference in the folder.
      Two plates of **opposite polarity welded by a 2px keyline**, **2:1** label-to-value
      (measured 92px : 46px), value plate driven to maximum lightness. The art pass called it
      "the single most reusable widget in the folder for dense information, and the kit does not
      have it" — `ATTACK/DEFENSE/COMBO/TYPE` and all six inventory stats are this one widget.
      The polarity inversion is why it is not a Label pair in an HBox: the value is the only
      maximum-contrast element, so the eye lands on the number with no size or colour cue needed.
- [x] **`KitMeter`** — **segmented by default**, honouring the settled 7x rule that "segmented
      progress is the default, continuous is the exception"; every meter this framework shipped
      before was the exception. Track is a **dark tint of the fill's own hue, never grey** (4x
      rule), and the optional overhanging end cap comes from `rpgui.md`'s finding that
      "variation lives in the END CAPS, not the body — six bars, one track".
- [x] **`KitCollapsiblePanel`** — from `art/ui8.md`, which calls it "the widget the kit was
      originally asked for": handle **outside** the panel on the edge it moves along, ~33px,
      dark plate (L=0.26), **pure-white chevron** — the highest-contrast glyph on the screen,
      because it is the only control whose state must be read at a glance. Distinct in kind from
      the existing `CollapsiblePanelComponent`, which drives a separate host Control and pins its
      chevron per-frame; this owns plate, well and handle as one silhouette, so they cannot drift.
- [x] **Reskin verified** across rpg / platformer / citybuilder: identical widget code renders
      with rpg's chamfer + studs + carved frame and platformer's pill + no frame, with **no
      per-genre branching in any widget**. `tmp/kitproof/widgets_<genre>.png`.

**Two real bugs found by rendering rather than by reading the code:**

- [x] **`UiSurface.Semantic` returned BLACK, silently.** With neither the role key nor `accent`
      registered under `BeepSemantic`, `GetThemeColor` hands back black — so `KitMeter` drew a
      black bar on a black track with nothing logged. Its doc comment claimed it "falls back to
      the accent rather than to a literal", which is only true when accent exists. Now derives a
      visible colour from the surface and `PushWarning`s once, naming the node. This is the
      repo's dominant defect class, in the one helper every drawn component routes through.
- [x] **`Invalid polygon data, triangulation failed`** — `Outline` clamped the corner cut to
      exactly half the shorter side, at which point a chamfer's two top vertices COINCIDE and the
      polygon degenerates; Godot logs it and draws nothing. Hit by `KitMeter`, whose segments are
      narrow enough for the genre's cut to reach half their width. Clamped to 0.45, plus a
      sub-pixel rect guard.

- [x] **`KitControl.DrawBanner`** — the overhanging title banner, which the art pass counts as
      "the single most repeated element across all 7 kits" and which this framework had shipped
      nowhere: every panel used an INLINE Label. Height **0.14 x the host** (rpgui2: 18px on a
      129px card), straddling the edge so half the plate sits outside the host. Shade defaults to
      **0.44 x the frame** (gameui2 — "a title plate reads recessed, not raised"), exposed as a
      parameter because polarity is per-family (gameui4's is white at L=0.97).
- [x] **`KitPanel`** — frame + recessed well + banner. The well takes the **0.79-0.80 x** subtle
      inset that citybuilder3's tiles and gameui1's parchment slots produced independently.
      `ContentRect()` is public so a screen lays children out inside the well instead of
      re-deriving the insets and drifting from them.
- [x] **`KitSlotGrid`** — interior : pitch **0.58** (gameui9: 49px on an ~85px pitch);
      **selection is a 3px white rectangle drawn OUTSIDE the slot** (gameui9) rather than a fill
      change, so it survives greyscale and works over any contents; empty slots **drain
      saturation** rather than darken (rpg3: available S=0.65-0.72 vs empty **S=0.05**); count
      badges straddle the bottom-right corner (gameui8); and locked slots **state their
      requirement in words** (5x rule), so `SlotKind` models blank / invite / locked separately
      rather than collapsing them into "not filled".

**Two more bugs, both found by looking at the render:**

- [x] **A grid of black holes.** `KitPanel`'s well and every slot used
      `PlateShadeFor(Recessed) = 0.12` — measured on citybuilder5's small StoneCapsule READOUT
      sunk into a pale frame. On a panel body it renders the whole panel black. Split out
      `WellShade = 0.79` for content wells. **This is the third time a measured ratio has been
      applied outside the widget class it was measured on** (after `PlateShade` and the frame
      ratio); the rule now written next to it is: check what was under the ruler before reusing
      a number.
- [x] **Twelve lozenges.** The angular corner cut is derived from HEIGHT so a wide button gets a
      real rake, but on a square slot 0.42 x height eats both corners and the square becomes a
      diamond. Capping it unconditionally fixed the slots and then shaved the rake off tall
      buttons, pushing **rpg vs survival to a marginal pass** on the greyscale gate — caught
      because the gate was re-run rather than assumed unaffected. The cap is now conditioned on
      how square the host is, which fixes the slots and leaves the buttons untouched: back to
      one marginal pair (citybuilder/strategy), 0 indistinguishable.

- [x] **`KitGeometry.GlyphRatio`** — glyph : button is a per-FAMILY ratio, not a constant:
      **0.40 carved / 0.55 flat** (citybuilder1 vs citybuilder2) and **0.60** on gameui3's kit.
      A carved plate spends its area on the frame, a flat one gives it to the icon. Derived from
      `Register` rather than restated per genre — which is the point of having a register.
- [x] **`KitIconButton`** — the unit a build toolbar, ability bar or icon rail is made of, with
      the state set gameui3 lays out and labels explicitly: **Normal / Over / Click / Disabled**.
      One size for every icon button, rail or docked (citybuilder2). Disabled **drains
      saturation** rather than fading. **A locked control shows NO hover and NO press** —
      gameui3's padlock button is drawn in normal and disabled only, and promising an
      interaction that will not happen is worse than showing none.
- [x] **`KitTree`** — skill / upgrade / research screens. Nodes on a tier grid at the measured
      ~12% gutter; connectors are **thin orthogonal lines drawn BEHIND the nodes** (never
      diagonal); a **locked node is a dark SILHOUETTE** — "art rendered near-black, no colour,
      **no number**" — rather than a dimmed owned node; cost badges straddle the corner.
      Its governing rule is taken verbatim from skilltree1.md and stated twice there:
      **"Spend colour on branch identity OR on node state, not both."** `ColourCarries` is
      therefore an either/or enum and deliberately not two independent toggles — doing both
      produces a tree in which neither reading survives.

**Next, still from the catalogue:** `KitCardHand`; ornaments (crown/wings/laurel) as
non-interactive overhanging attachments; pentagon status chips; `RadarChart` (racing3);
`InputHint` with chord support.

### Migration — `kit_gallery.tscn`, and the bug only a real scene could show

- [x] **`templates/scenes/kit_gallery.tscn` + `ecs/scenes/KitGallery.cs`** — all eight widgets in
      ONE real scene, inside real `VBox`/`HBox` Containers, under a real `ThemePresetComponent`.
      The kit's counterpart to `theme_gallery.tscn`, and the migration half of phase B: building
      a widget proves it can be drawn, only putting it in a scene proves it is usable. Passes
      `validate_scenes.sh` including the PascalCase-export check.
- [x] **Confirmed the palette actually reaches the kit.** `ThemePresetComponent.ThemeSemantics()`
      publishes all seven `BeepSemantic` roles, so kit widgets resolve real theme colours in a
      skinned scene rather than the probe's stub — checked before relying on it.
- [x] **An overhanging element must not draw outside its own rect.** `KitPanel` drew its banner
      at a negative y so it straddled the frame; in a Container that reserves no space for it, so
      the EQUIPMENT banner rendered **on top of the COMBO stat row in the HBox above**. The frame
      is now inset from the top by the banner's overhang and `_GetMinimumSize` includes it, so
      the banner still straddles the FRAME's edge (the measured behaviour) while the whole widget
      stays inside its own bounds.
      **This is the general hazard of the `KitAttach` model** — anything drawn outside the host
      rect is invisible to layout — and it is exactly the class of defect a probe harness with
      absolute positioning can never surface. Worth re-checking for every future attachment,
      ornament and badge.

### Following the catalogue's OWN build order (2026-07-28)

Correction to how widgets were being chosen. `CATALOGUE-FROM-ART.md` carries a **build order
derived from frequency, not preference**, and the first widgets were picked by hand instead —
so two of its top-tier items were still missing while lower-frequency ones had been built.

- [x] **`KitCurrencyBar`** — build-order item 1, "appears in nearly every picture". Capsule with
      its icon cap **overhanging the left end**, measured on citybuilder5's StoneCapsule row:
      35px capsule, **asymmetric frame** (7px top / 5px bottom), inner plate at **0.12** and a
      gloss band across the top. This is the widget class the 0.12 shade was actually measured
      on, so unlike a panel well it correctly uses the recessed plate shade.
      `IconOverhang` is exposed because it is per-skin (1.48x vs 1.0x).
- [x] **`KitTabStrip`** — build-order item 1. Selection uses the **tab-appropriate** mechanisms
      (weld / pill-behind / elevate) rather than a generic "selected" look, because the art pass
      measured 17 distinct selection mechanisms and found the choice follows widget CLASS. Keeps
      the Stage 28 lessons: no shadow across neighbours, and an unselected tab is normal text at
      78% alpha — a place you CAN go, not one that reads as unavailable.
- [x] **`KitNodeCard`** — build-order item 2, "the single most repeated compound element", with
      the welded footer counted **8x** across unrelated sheets. Honours INDEX.md's correction
      that **the footer is TWO widgets**: a status band at **0.19 x** card height and an action
      button at **0.10 x**. Verified visually — the BUY bar renders visibly shorter than the
      OWNED band, which is the whole point of the correction.
- [x] Removed a dead `Straddle` export from `KitIconButton` that did nothing at all. A silent
      no-op export is the same defect class as a snake_case one Godot drops. Straddling is the
      HOST's job — only it knows which edge is being crossed.

### Sections C / D / E / F.2 — the form, small-part and radial families (2026-07-28)

- [x] **`KitChip`** — section C's whole small-part family as ONE widget with variants:
      RarityChip / CountBubble / NotificationDot / **pentagon status chip** (tick or cross) /
      LockOverlay. One widget because they are one shape with different payloads; five classes
      would have five bevels drifting apart. Badge colour carries a ROLE (ui8: green = new
      content, red = action required), never a literal.
- [x] **`KitSlider`** — `settings1`'s **vertical bar knob**, not a desktop circular grabber.
      Track is a dark tint of the fill's own hue, and the knob does NOT change hue on press —
      the Stage 28 defect where grabber and highlight came from different palette roles and the
      focused slider rendered green among blue ones.
- [x] **`KitToggle`** — F.2's `OnOffSwitch`, noted there as "**this is the game checkbox**".
      Boxed style also offered, because CATALOGUE §D's "no checkboxes" claim is itself corrected
      (gameui2/4/5 all contain them). Off keeps full saturation — draining it is reserved for
      unavailable, and using it for "off" would make every unset option look broken.
- [x] **`KitArrowSelector`** — `< Option >`. Section D records that **dropdowns appear in NONE of
      the 43 reference images**; games page through options with arrows. This is what a settings
      screen wants for resolution, language and difficulty.
- [x] **`KitRadialMeter`** — section E's ring gauge (Don't Starve's vitals cluster, a rev
      counter). Separate from `KitMeter` because a bar and a ring do not share a layout problem —
      `docs/hud/survival.md` names rings for Don't Starve and bars for Valheim, both legitimate.
      Segmented by default, same 7x rule.
- [x] **`KitStarRating`** — F.2. Unearned stars **drain saturation rather than vanish**, so the
      player can see how many the level HAS, not only how many they earned.

### F.1 hangers, ornaments, and the comparison primitives (2026-07-28)

- [x] **`KitPanelHanger`** — section F.1's ENTIRE family in one widget: `ChainHang`, `RopeHang`,
      `NailPin`, `TapeCorner`, `ScrollRoll`, `VineFrame`. One widget because they are one idea —
      a fixing drawn above or across a panel's edge so the panel reads as a physical object hung
      in the world rather than a rectangle floating in screen space. `ui5.png` proves that axis
      by drawing one dialog geometry in ~10 materials with no layout change.
      **Fixed after looking at the render:** the first version tinted the surface by 1.15x for
      its neutral accent, which on a mid-tone background was invisible — a hanger you cannot see
      is not a hanger. It now pushes firmly away from the surface's luminance, and chain links
      alternate their long axis so a chain reads as a chain rather than a dotted line.
- [x] **`KitOrnament`** — crown / wings / laurel / trophy / **starburst** / ribbon-tail, the
      decorations PLAN.md phase D lists as overhanging attachments ("the golden-kit sheet uses
      them constantly"). Section E's `StarburstBadge` is the same idea at a different silhouette,
      so it is a variant rather than its own class. **Inert by construction** — `MouseFilter =
      Ignore` in `_Ready`, not left to the scene: these are always drawn over something the
      player is meant to be able to press.
- [x] **`KitTooltip`** — section C's `HintTooltip` **with a tail**. The tail is the point: it
      names which control the tip belongs to, which a floating rectangle cannot do when three
      controls sit close together. Drawn at the OPPOSITE polarity to its surface, per the 5x
      "one element class flips polarity" rule. Body is inset on the tail's edge so the tail stays
      inside the control's own rect — the containment rule `KitPanel`'s banner had to learn.
- [x] **`KitInputHint`** — `[E] Gather Wood`, **with chord support** (`L2 + X`), which INDEX.md
      calls out explicitly: a hint that can show only one glyph cannot express the modifier
      combinations controllers rely on. This is `docs/hud/survival.md` element 11.
- [x] **`KitRadarChart`** — INDEX.md's "missing primitive, fully procedural, useful to racing,
      rpg and strategy" (racing3). The folder's only COMPARISON widget: a stack of bars answers
      "how big is each", a radar answers "what shape is this thing", which is the actual question
      on a vehicle- or class-select screen.

### Closing the tail — rows, avatar, pager, segmented group, spinner (2026-07-28)

- [x] **`KitRow`** — section B's `MissionRow` and `PlayerRow`, one widget with different
      payloads: rank, title + subtitle, value, state chip. Selection is a **FILL**, per the
      convention-by-widget-class finding ("card carousels use an outline, tab strips use
      fill/elevation, **list rows use a fill**" — racing1). Rows band alternately so a long list
      stays readable without a separator per row.
- [x] **`KitAvatarFrame`** — section E, portrait with a ring in a palette role and a badge
      **straddling the bottom-right rim** (ui8's FriendCard star). The overhang is why this is a
      widget and not a TextureRect with a border.
- [x] **`KitPager`** — section C's `PagerArrow` **plus ui8.md's correction**: "add jump-to-end
      pagers alongside step pagers", and "step and jump paging can be separate control pairs".
      Dots up to 8 pages, a "13 / 40" readout beyond — what the references do rather than drawing
      forty dots.
- [x] **`KitSegmentedIconGroup`** — section D. The game form's radio group. **Welded**, because
      the join is what says "these are alternatives"; three spaced buttons say "three independent
      actions".
- [x] **`KitSpinner`** — F.2's `LoadingIndicator`, in three non-interchangeable forms: ring
      (unknown wait), dots (inline "working"), bar (known progress — using the ring there throws
      away information the player could have had). `ProcessMode = Always`, so it keeps moving
      while the tree is **paused** — the one moment a loading indicator must not freeze.

**Kit now: 27 widgets**, every one rendered and inspected across four proof sheets
(`widgets_*.png`, `formkit_*.png`, `formkit2_*.png`, `formkit3_*.png`).

**Remaining, and all of it is compositions or genre set pieces rather than primitives:**
`PlayerCard` (avatar + row + footer), `LevelNodeGrid` (tree + slot grid), `RewardSlotRow`
(slot grid 1xN), `MedalRosette` (ornament + chip), `TornPanel` / `CornerClose` (panel + shape
variants), `RoundKnob` / `GemSlot` (circular slot variants), and the two large set pieces
`BookSpread` and `SpinWheel`. **The primitive layer is covered.**

**Bug found while wiring the proof sheet:** an edit to the probe's positioning block silently
no-oped because its anchor text had already changed, so eight widgets were created and never
positioned — they rendered stacked at (0,0) under the panel and looked like a drawing bug. The
form families now get their OWN sheet; a proof sheet that overlaps itself proves nothing.

**Phase B's bar is still not fully met** — it is "settings + one HUD contain no generic
`Button`/`PanelContainer`", and the gallery is a demonstration screen rather than either of
those. The remaining work is migrating `settings_menu.tscn` and one genre HUD, which is a
behavioural change to shipped screens (focus, signals, `SettingsMenu.cs`'s 13 bound nodes, and
three copies across the addon and both game projects) rather than more drawing code.

**Known environment note:** rendering a themed scene outside the editor logs
`Failed loading resource: .../button_normal.png` for the Stage 27 art — only 539 of ~744
textures are in `.godot/imported`. The scene still renders; the baked art needs an editor import
pass. Pre-existing, unrelated to the kit.
