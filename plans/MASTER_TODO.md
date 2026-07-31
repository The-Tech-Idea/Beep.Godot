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

## The shapes ARE the same — measured (2026-07-29)

Owner: *"still you have the same shapes, you just changed colors"*. Correct. Outline distance
between genres, against the 0.040 distinguishable threshold:

| pair | outline |
|---|---|
| racing vs shooter | **0.019** — less than half the bar |
| platformer vs puzzle | **0.027** |
| cardgame vs topdown | **0.042** — scraping it |
| survival vs citybuilder | 0.066 |
| rpg vs platformer | 0.068 |
| **strategy** | **0.168** — the ONLY genuinely distinct silhouette |

 reports **0 indistinguishable pairs** only because the STRUCTURE axis
rescues those pairs — they differ in shading and banding, not outline. **The PASS line was masking
the exact thing the gate exists to detect**, and I kept reading the verdict instead of the column.

**Root cause: every  is a RECTANGLE WITH A CORNER TREATMENT** — chamfer, notch, clip,
octagon — at 4–16% of the shorter side. At panel scale those are the same object.  scores
0.168 precisely because it is the one shape whose OUTLINE differs rather than its corners.

This is also why  does not come through: rpgui has protruding corner ornaments,
citybuilder5 has irregular carved edges, racing is raked and asymmetric, ui1/store are large-radius
slabs with heavy outlines. None is "a rectangle with its corners cut", so no corner tuning reaches
them.

**What would actually fix it, by effect per unit of work:**
1. Silhouettes that change the OUTLINE, not the corners — protrusions past the bounding box
   (rpg ornament ears), asymmetry (racing raked on one edge only), non-parallel edges (survival
   chipped stone).
2. Per-genre banner PLACEMENT, not just shape — top-centre vs corner tab vs side rail. Every genre
   currently puts its banner in the same place.
3. Genres carrying NO banner at all, so the family is not uniform.
4. Per-layer MATERIAL (wood grain, stone, carbon) — PLAN.md phase E, and what separates the
   reference sheets most.

**Gate this work on the OUTLINE COLUMN, not the PASS line:** no pair may sit under 0.040 on
outline alone.

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

### EXTRACTION COMPLETE — 32 widgets (2026-07-28)

The last of the catalogue, closing every section:

- [x] **`KitKnob`** — section E's `RoundKnob`. Distinct from `KitSlider` rather than a round skin
      of it: it occupies a square, shows its value as an ANGLE plus a lit tick ring, and is
      dragged VERTICALLY on purpose — following the pointer's angle is the obvious
      implementation and the wrong one, because the value jumps as the hand crosses the centre.
- [x] **`KitGemSlot`** — section E. The circular counterpart to a grid cell, and deliberately not
      one: a socket is CUT INTO its host, so it takes the recessed readout shade (0.12) rather
      than the 0.79 content-well shade — the exact distinction that rendered a whole slot grid as
      black holes when it was got wrong.
- [x] **`KitLevelPath`** — F.2's `LevelNodeGrid`, the puzzle/platformer level map. Not `KitTree`
      with different data: a tree BRANCHES and a path is a SEQUENCE with one current position, so
      it owns the serpentine layout, per-node scores and the "you are here" ring. Locked nodes
      show no number; the track ahead is DASHED ("dashed stroke = path / provisional", 4x).
- [x] **`KitSpinWheel`** — F.2. The pointer is FIXED outside the rim while the wheel turns under
      it; turning the marker with the wheel is the classic mistake. `Spin(index)` takes the prize
      from the CALLER — a wheel that picked its own would make the odds a property of the widget,
      where no designer could reach them.
- [x] **`KitBookSpread`** — F.2's journal / codex. The shaded spine gutter is the whole idea: two
      pages meeting at a gutter read as one opened object, two panels side by side read as two
      panels. Exposes `LeftRect()` / `RightRect()` like `KitPanel.ContentRect()`.
- [x] **`TornPanel` and `CornerClose` as `KitPanel` OPTIONS**, not classes — the structure is
      identical and only the bottom edge or a corner attachment changes. The torn edge is seeded
      from the panel's own width so it is stable across redraws; an edge that reshuffles every
      frame reads as noise. The close button is drawn by the HOST so it can straddle the frame.
- [x] `RewardSlotRow` = `KitSlotGrid` at 1xN; `MedalRosette` = `KitOrnament` + `KitChip`;
      `PlayerCard` = `KitAvatarFrame` + `KitRow` + `KitNodeCard`'s welded footer. Compositions of
      shipped widgets, recorded here so nobody rebuilds them as classes.

**Every section of CATALOGUE-FROM-ART.md is now covered: A, B, C, D, E, F.1, F.2, F.3, plus the
INDEX extras (`RadarChart`, `InputHint`, `LabelValuePair`, `CollapsiblePanel`, ornaments).**
32 widgets, every one rendered and inspected across five proof sheets
(`widgets_*.png`, `formkit_*.png` … `formkit4_*.png`).

### KitLayer — the multilayer stack, and the harness bug that hid it (2026-07-29)

`KitLayer` + `KitStacks` are in: each register declares an ORDERED stack and `DrawMaterial` walks
it, instead of running the fixed frame/plate/bevel/gloss/studs/sparkle sequence that made every
genre a re-tint of one build. Carved gets frame + recess keyline + plate + face shade + bevel +
gloss; casual stays shallow with NO face shade (a gradient down the face IS the painted reading);
technical sits between. `KitLayerKind.Shade` draws the vertical falloff the painted/flat ratio
actually measures.

**RETRACTION.** Two commits recorded this as a "null result" — that the stack moved nothing. That
was wrong, and the cause was my own shell:

```
dotnet build 2>&1 | grep -cE 'error CS' && timeout 150 godot ...
```

**`grep -c` exits 1 when the count is zero.** On a clean build the `&&` short-circuited and Godot
never ran, so every "measurement" compared the previous evening's PNG against itself. That is why
two separate fixes appeared to move the numbers by exactly 0.00, and why a canary darkening the
inner plate 0.88 → 0.20 rendered "byte-identical" and looked like a stale assembly. The DLL was
fine; the render never happened.

**With a render that actually runs, the stack does its job.** bottom:peak against the painted band
of **0.18–0.27**:

| genre | before | after | |
|---|---|---|---|
| rpg | 0.33 | **0.22** | in band |
| survival | 0.26 | **0.17** | just under |
| strategy | 0.27 | **0.15** | just under |
| citybuilder | 0.49 | **0.31** | just over |

So **the carved register IS reachable procedurally**, which PLAN.md said it was not. Casual is
untouched and still flat (0.67–0.83 against 0.76–0.84).

- [ ] **THE GATE NOW FAILS, and the threshold was not moved.**
      `citybuilder vs strategy — outline 0.029, structure 0.069, bar 0.070.` Both are Rect +
      Carved and now share a darker face, so the pair that has been marginal all session fell just
      under. The documented fix is **strategy's corner brackets** (PLAN.md 4.2b, "square +
      brackets") — new drawing work, left for a session with room to verify it. Fixing this by
      relaxing the threshold would defeat the only instrument that has been reliably right.
- [ ] rim:body is still short of painted: carved reads 0.93–1.10 against a 1.78–2.05 target.

**The lesson is the harness, not the kit.** A verification step that cannot fail LOUDLY is worse
than none, because it manufactures confident wrong answers — two commits of "null result" analysis
came out of one silent short-circuit. Any future check must fail noisily: never put `grep -c`, or
anything else that returns non-zero on success, on the left of `&&`.

### Owner-supplied shape/icon packs — LOOKED AT, NOT USED (2026-07-28)

`H:\GameDev\GFX\GameAssets\shapes` — three Flaticon packs (essentials-UI 30, arrows 20, shapes 30)
as SVG/PNG/EPS/PSD plus icon-fonts.

**Decision: not used.** They are GLYPH sets, not chrome, so they do nothing for the carved-register
gap, which is the only open visual problem. `KitShape` already carries 16 silhouettes and every
widget renders correctly with no imported asset at all. Adding a hexagon would be nice-to-have,
not a need, and each pack ships a `license/license.pdf` (Flaticon free tier requires attribution)
against a standing repo rule of "shipped art stays CC0 Kenney or authored" — cost with no
corresponding need.

Recorded only so the next session does not re-evaluate them from scratch. **If** icon art is ever
wanted, the slots are already there and take a `Texture2D` with no code change:
`KitIconButton.Icon`, `KitSegmentedIconGroup.Segments[].Icon`, `KitSlotGrid.Slot.Icon`,
`KitTree.Node.Icon`, `KitCurrencyBar.Entry.Icon`, `KitNodeCard.Art`, `KitAvatarFrame.Portrait`,
`KitMeter.CapIcon`, `KitAttach.Icon`.

### CORRECTION — the ask was REPLICATE the patterns, not copy the pixels (2026-07-28)

Stated by the owner, and it supersedes the whole slicing thread below:

> "gameui2/3/7 and other — I'm not asking to copy, I'm asking you to replicate the patterns."

So:
- **The licensing blocker was never a blocker.** Nothing needs slicing, so which sheets are
  watermarked comps does not gate anything. I raised it as a decision for the owner when the
  right move was to keep replicating.
- **`tools/kit_art/slice_sheets.py` solves a problem this project does not have.** It is kept
  because a developer may still want to mount their OWN licensed art, and `KitArt` is the honest
  mechanism for that — but it is NOT the deliverable and phase E is not "waiting" on it.
- **The 32 procedural widgets ARE the deliverable**, and the measured proportions in
  `plans/game-ui-kit/art/` are exactly the right input for them.

**What "finished replicating" should therefore mean, and it is measurable.** PLAN.md claims the
painted family is "not reachable procedurally"; the owner's instruction is to reach it anyway.
The two ratios the art pass settled are the test, and `verify_greyscale.py` already reports them:

| | painted target | flat target | kit today |
|---|---|---|---|
| bottom : peak | 0.18-0.27 | 0.76-0.84 | carved 0.26-0.49, rpg 0.33 |
| rim : body | 1.78-2.05x | 1.3-1.5x | carved 1.05-1.28 |

The CASUAL register already lands in its band (0.67-0.83 against 0.76-0.84). The CARVED register
does not: its rim is too dim and its face too flat to read as painted. **That gap — making the
carved genres hit the painted band procedurally — is the remaining "replicate the patterns" work,
and it is checkable by running the gate rather than by opinion.**

### Phase E — sliced art, and the widgets consuming it (2026-07-28)

Clarified with the owner: "extract widgets from art" meant **both** — slice real 9-patch assets
AND have the widgets render from them, procedural being the fallback rather than the only mode.

- [x] **`KitArt`** — resolves `<root>/<genre>/<widget>_<slot>.png` with a `_common` fallback, and
      reads 9-patch margins from a sibling `.margins` file. Says so ONCE per missing slot when a
      root IS configured, because a widget silently falling back looks identical to one whose art
      failed to import.
- [x] **`KitControl.TryDrawArt`** — any widget gets texture-or-procedural for free. A
      `StyleBoxTexture` is used rather than `DrawTextureRect` because only it does real 9-patch
      margins. When a `base` slot exists it REPLACES the frame+plate build, because painted art
      already contains its own frame, bevel and rim — re-applying the procedural bevel over it is
      what makes textured chrome look plastic. The palette still drives `modulate`, so sliced art
      reskins instead of pinning one game's colours into every project.
- [ ] **THE ASSETS THEMSELVES ARE NOT EXTRACTED.** Correcting an earlier claim in this file:
      phase E was recorded as done when only its MECHANISM was. There are **zero sliced widget
      assets in the repo and zero recorded crop coordinates.** `slice_sheets.py` needs
      `x,y,w,h` + 9-patch margins per widget per sheet, and nobody has ever worked those out, so
      the tool is presently unusable by anyone. One throwaway crop was cut to prove the pipeline
      and then deleted. **The remaining work is the measurement pass**: go through the 43 sheets,
      locate each widget (button / panel / bar / frame / tab / slot), record its rect and margins
      the way `plans/game-ui-kit/art/*.md` recorded proportions, and cut them.
      **Blocked on a decision, not on effort:** the audit marks `gameui2/3/7` as watermarked
      comps and the standing rule is "shipped art stays CC0 Kenney or authored" — so which
      sheets, if any, may be sliced into shippable art is the owner's call. Building a tool
      instead of asking was the wrong move and is what let "phase E done" stand for three turns.
      **First attempt at automating the measurement pass FAILED, and the failure is recorded
      rather than papered over:** `tools/kit_art/find_slots.py` segments a sheet into candidate
      widget rects + margins, and emits coordinates (data, not pixels — so it is licensing-safe
      and committable). On a flat-field sheet it works. On `rpgui.png` it finds **5 fragments and
      misses the PLAY button, the title bar, every bar and every banner**, because those widgets
      sit on a dark TEXTURED backdrop and the background flood cannot reach between them. The
      rects it did emit looked completely plausible; only the montage revealed they were wrong.
      So the measurement pass is NOT automatable across these sheets as-is — for textured sheets
      the fallback is the by-hand scanline method that produced every reliable number already in
      `plans/game-ui-kit/art/`.
      **`--refine` makes that fallback practical.** Give it a ROUGH box and it tightens onto the
      widget and emits the exact `--slot` line plus a 2x preview to confirm against. It works
      where auto-segmentation cannot, because it never has to SEPARATE widgets — the human already
      did that by drawing the box. Verified on `rpgui.png`: a rough `20,530,340,100` snapped to
      `26,530,334,94` and previewed as exactly the PLAY button, frame, plate and corner studs.
      Its margin walker alone was wrong (2px, because it stops at the outer outline), so an
      implausibly thin result now falls back to the measured structural fit
      **3.5px + 0.07 x height** — the same formula `KitGeometry.FramePx` uses — which gives 10px
      on that button and matches the art document's own measurement of it.
      **So the measurement pass is now a tractable, boxes-then-confirm job rather than
      pixel-hunting.** It remains gated on the licensing decision, since only slicing copies
      pixels; recording coordinates does not.
- [x] **`tools/kit_art/slice_sheets.py`** — cuts regions + margins out of `Example_Art/` into the
      layout `KitArt` reads, and appends a `PROVENANCE.txt` naming the source of every file.
- [x] **Licensing enforced in the tool, not just documented.** It REFUSES `gameui2/3/7` (recorded
      as watermarked comps, "style reference only") without an explicit `--i-have-a-licence`, and
      REFUSES any `--dest` inside `addons/`. The addon ships no third-party pixels — the same
      resolution `docs/HUD_TEXTURE_SYSTEM.md` reached for the Kenney HUD art. Both guards tested.
- [x] **Verified end to end**, not assumed: sliced a real crop from `rpgui.png`, ran Godot's
      `--import`, and rendered a `KitButton` drawing FROM the 9-patch with its label over it
      (`tmp/kitproof/artproof.png`). The first attempt correctly reported "no art … drawing it
      procedurally" because Godot had not imported the PNG yet — `ResourceLoader.Exists` is false
      for un-imported files, which is worth knowing before blaming the resolver.
      Test pixels were deleted afterwards; they were third-party.

### Stage 30 begins — survival vitals are bars (2026-07-28)

The first genre HUD actually moved off its Label stack, which is what Stage 30 opens by
complaining about: "a player cannot read health at a glance from 'Health: 72'".

- [x] **`GenreHudComponent` speaks kit widgets.** `ResolveReadout` now accepts `KitMeter`,
      `KitRadialMeter` and `KitLabelValue` alongside Label and `ResourceBadgeComponent`;
      `SetReadout` drives a bar's FRACTION and a ring's fraction + centre text; `Tint` recolours a
      meter's FILL rather than its text, because the bar is the thing being read and a warning
      that only tints a number defeats the point of having one. All ten genre HUDs inherit this,
      so each scene can migrate one node at a time instead of ten scenes in lockstep.
- [x] **`Placeholder` had to become widget-aware too** — it resolved Label-only, so upgrading a
      readout to a `KitMeter` turned a working placeholder into "no such node". The migration
      would have been punished for succeeding. `_placeholders` is now `Control`, and `SetStat`
      routes through `SetReadout`, so a game's `SetStat("thirst", …)` drives a bar as happily as
      a Label.
- [x] **survival**: the four vitals are `KitMeter` bars (health Success, hunger Warning, thirst
      Info, stamina Accent2), segmented per the 7x rule, each with a track in its OWN hue.
      Verified by rendering `survival_main.tscn`: 0 "nowhere to display" warnings, all four
      resolved and filled.

- [ ] **Nine genres remain**: citybuilder, strategy, shooter, rpg, cardgame, racing, puzzle,
      topdown, platformer. The base-class work above is done once and serves all of them; what
      remains per genre is its own scene edit and the elements its `docs/hud/<genre>.md` names
      (hotbars, ability bars, minimaps, clocks, command cards).

**What this does NOT mean.** The widgets exist and reskin; they are not yet USED. Phase B's bar
is "settings + one HUD contain no generic `Button`/`PanelContainer`", and only `kit_gallery.tscn`
and the shared `hud.tscn` touch the kit. **Stage 30's ten per-genre HUDs remain untouched** —
that is now the largest open item in this file, and the kit is well past what it needs.

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

---

## Stage 37 — The material axis gets a number (2026-07-29)

Three files arrived in `Example_Art` on 07-28 22:28–22:33 that no earlier pass had seen —
`ui9.png`, `uiwood.png`, **`uitexturs.png`** — and eight `.svg` files arrived on 07-29 19:34–19:38,
mid-session. `uitexturs.png` is the cleanest statement of the two-axis model in the whole folder
and it turns the phase-E acceptance test from a description into a **threshold**.

### `uitexturs.png` — nine materials, one silhouette

Nine tiles: leather, rubber-dots, glossy-leaf, brushed-metal, diamond-plate, stone, wood-plank,
denim, graph-paper. **Identical rounded square, identical corner radius, identical drop shadow.**
They are unmistakably nine different things anyway. Same proof as `ui5.png`'s ten dialogs
(line 628), arrived at independently — but this one is measurable.

Metric (`tools/genre_shapes/` candidate): mean |laplacian| inside the plate, **normalised by the
plate's own mean tone**, plus a |dx| vs |dy| imbalance term. Normalising by tone is what makes it
colour-invariant — the same property the outline/structure axes have, and the reason it can be a
gate rather than an opinion.

| tile | tone | hf | dir |
|---|---|---|---|
| stone | 188 | 0.0055 | 0.11 |
| brushed-metal | 194 | 0.0218 | 0.02 |
| rubber-dots | 103 | 0.0344 | 0.01 |
| graph-paper | 228 | 0.0345 | 0.00 |
| glossy-leaf | 130 | 0.0358 | 0.23 |
| leather | 81 | 0.1037 | 0.06 |
| **wood-plank** | 96 | 0.1224 | **0.67** |
| denim | 89 | 0.1253 | 0.05 |
| **diamond-plate** | 68 | **0.3703** | 0.01 |

**67× spread**, and wood separates on the second axis alone (dir 0.67 — planks run one way,
everything else is isotropic). Two axes are needed: rubber-dots, graph-paper and glossy-leaf sit
within **0.0014** of each other on `hf` and are separated only by `dir` and by dot geometry.

> **These numbers replace an earlier, incomparable set** (stone 0.0086 … diamond-plate 0.6462,
> "75× spread"). The metric was **scale-dependent**: the laplacian is a per-pixel difference, so
> it shrinks as resolution rises. Caught by measuring one wood-plank region on both the 1920px
> JPG and the 4898px EPS render of the *same* artwork — **0.0248 vs 0.0144**, i.e. the
> higher-quality source scored as *less* detailed, and downsampling the render back to JPG size
> returned 0.0254. The metric was self-consistent at matched scale and meaningless across scales,
> which mattered because the reference tiles (~486px crops) and a rendered kit plate (~73×25) are
> nowhere near the same scale. `hf_energy`/`directionality` now resample every crop to
> `NORM_PX = 256` first, and `--selftest` asserts < 10 % drift at 4× resolution (measures 2.8 %).
> The ordering and the conclusion survived; the absolute values did not.

### What the kit scores: nothing

The plate is `DrawColoredPolygon` with a flat fill plus inset bands. No grain, no gradient, no
pattern. `KitLayer.cs:119` says it outright — *"Deliberately NO Shade layer."* Every
`DrawTextureRect` across all 32 widgets is **user-supplied content art** (avatar, gem, currency
icon, node-card art, slot icons, tree icons), never the surface.

So the kit has a silhouette axis and a colour axis and **no material axis at all**. That is the
gap behind "still the same shapes, you just changed colors", now stated on the same scale as the
reference rather than as a complaint.

### Two false measurements on the way there — recorded, because both looked clean

1. **The first crop measured empty background.** Content in `gs_rpg.png` occupies cols 505–645,
   rows 298–359; the fixed fractional crop sampled x[115:368] y[272:375]. It returned exactly
   `0.0000` for all ten genres and read as a devastating finding. It measured nothing.
2. **The second crop found the plate but was dominated by the label.** At 130×45 the inset core
   is ~73×25 and mostly glyph, which scored the kit at 0.59–1.03 — *above diamond plate*.

The reported result comes from **reading the drawing code**, not from either metric. Same failure
mode as the `grep -c` incident: a check that cannot fail loudly manufactures a confident wrong
answer. Any material gate must locate its crop, assert the crop is on a plate, and **exclude the
glyph** — the proof render needs a label-free variant before the axis can be gated at all.

### Source formats — what is actually readable

- **EPS: SOLVED — `tools/genre_shapes/eps_render.py`.** There is still no `gs.exe`, ImageMagick
  or Inkscape on the box, but **GIMP 3.2 bundles `bin/libgs-10.dll`** (Ghostscript 10.07.0), which
  exports the standard Ghostscript C API. `eps_render.py` drives it through `ctypes` — no GIMP
  Script-Fu, whose PDB signatures changed between GIMP 2 and 3. All **14/14 EPS render clean and
  watermark-free** at 144 dpi into `tmp/eps/` (gitignored), **4898–8579 px wide** against JPG
  previews **all capped at 1920**. Two API details that would otherwise fail silently: `-101`
  (`gs_error_Quit`) is the *normal* return from `-dBATCH` and must not be treated as an error, and
  each render needs a **fresh instance** — Ghostscript is single-init, and reuse yields a
  truncated second file. `--selftest` renders a synthesised 200×100pt EPS and asserts the exact
  pixel size at two dpi, plus a loud rejection of garbage input.
  Worth doing because the carved 4-band constants (rim 2.05× / bezel 1.14× / shadow 0.76×) were
  measured off 3–4px bands in JPEG, at the compression noise floor. Measured gain on a matched
  region, under the scale-corrected metric: **+11 % real detail** (hf 0.0249 vs 0.0224).
- **SVG: 5 of 8 are real vector**, and those are exact, resolution-free geometry:
  `abstract-technology-futuristic-concept-hud-interface` (11,765 paths),
  `list-of-mobile-games-…-20470769 / 20470774 / 20470776` (2941 / 1173 / 1944),
  `game-user-interface-elements-set_2202535` (674).
  **3 are rasters in an XML wrapper** — both `casual-game-ui-menu-popups…` and, unhelpfully,
  `wooden-buttons-cartoon-interface-game-ui-elements_10876594.svg` (7 paths, 3.2 MB base64).
  Classify before trusting: `<path>` count vs base64 payload share.
- The shapes pack at `H:\GameDev\GFX\GameAssets\shapes` ships **83 SVG + 80 PNG** beside its 80
  EPS, so it never needs conversion.

### `ui9.png` — structural findings the kit does not have

- **Currency pill**: circular icon cap **overhanging the left end**, welded `+` flush on the
  right. This is the **second independent sighting** of the left overhang first noted on `ui1`,
  and it is still unimplemented — `KitShape.Capsule` draws as a plain rounded rect, which is
  exactly why platformer↔puzzle did not separate (0.027).
- **Banner headers overhang the panel's top edge**; the kit's sit inside at 0.14× host.
- **Discrete pip meters** — 7 hearts, 5 filled — not a continuous bar. Corroborates the settled
  "segmented progress is the default" rule (7×).
- Gold **corner ornaments** on all four frame corners; tabs physically attached to the panel.

### `uiwood.png`

One material carrying a whole family — panel, round icon buttons, square icon buttons,
rope-lashed bars. Silhouette **varies within** the family while material holds it together. The
kit does the inverse: one flat material, silhouette varied per genre.

### Open, in priority order

- [ ] **`KitLayerKind.Grain`** — per-genre material (plank / stone / carbon / leather /
      parchment), gated on `hf` + `dir` at thresholds derived from the table above.
- [ ] **Label-free proof render**, so the material axis can be measured at all.
- [ ] **`Capsule`'s overhanging cap** — twice-referenced, still missing.
- [ ] racing↔shooter **0.019**, platformer↔puzzle **0.027**, cardgame↔topdown **0.042** — all
      under the 0.040 outline bar. Gate on the **outline column**, never the PASS line.
- [ ] Stage 30's ten per-genre HUDs — still the largest untouched item in this file.

---

## Stage 38 — The material axis, built (2026-07-29)

Phase E, and the answer to "still the same shapes, you just changed colors". The kit now has
three axes instead of two: **silhouette, colour, and material.**

### Where the pixels come from, and why

Authorised to use "anything from kenney or example art". The split is deliberate:

- **Kenney Pattern Pack — CC0 1.0** → **ships**. Commercial use, no attribution required, no
  template/redistribution restriction. 9 patterns, ~30 KB total, in `textures/grain/`.
- **Example_Art (Vecteezy)** → **measurement only, never shipped.** Its Free License requires
  attribution *and* its terms restrict redistribution inside templates/themes — which is exactly
  what this addon is. Nothing is lost by this: the art's value here was always the measurements.

`textures/grain/LICENSE.txt` records both.

### Patterns are chosen by MEASUREMENT, not by eye

`tools/genre_shapes/pick_grain.py` scores all 171 CC0 patterns and assigns one per genre against
the material that genre's reference art is actually made of. Three axes, all colour- and
scale-invariant:

| axis | what it fixes | why it is needed |
|---|---|---|
| `dir` | which pattern | tiling cannot rotate a grain — direction is unfixable, so it drives the choice |
| `coarseness` | tile count | solved so feature size matches the reference material |
| `hf` | amplitude | solved so detail energy matches |

Targets are **measured off `uitexturs.png` at run time**, not transcribed — a transcribed
constant rots silently, and this metric has already changed once.

| genre | material | pattern | tiles | amp |
|---|---|---|---|---|
| rpg | wood-plank | pattern_50 (vertical planks) | 3 | 0.300 |
| survival | leather | pattern_49 | 4 | 0.300 |
| citybuilder | stone | pattern_78 | 1 | 0.136 |
| strategy | stone | pattern_78 *(shared, by design)* | 1 | 0.136 |
| shooter | diamond-plate | pattern_41 | 8 | 0.300 |
| racing | brushed-metal | pattern_37 | 1 | 0.300 |
| platformer | rubber-dots | pattern_42 | 1 | 0.300 |
| puzzle | graph-paper | pattern_32 | 1 | 0.300 |
| cardgame | denim | pattern_57 | 5 | 0.300 |
| topdown | glossy-leaf | pattern_19 (brick) | 2 | 0.293 |

The table is **generated into `ecs/ui/kit/KitGrainTable.cs`** so the shipped constants ARE the
measured ones. Regenerate with `python tools/genre_shapes/pick_grain.py --install`.

### Implementation

- **`KitLayerKind.Grain`** + `KitGrain.cs`. Masks are baked to **RGB white + alpha = 1 −
  luminance**, so they carry no colour and reskin with every palette. The naive alternative
  (ship the black/white PNG, draw it modulated) does **not** work — alpha blending an opaque
  texture with a black modulate paints flat black across the rect and ignores the pattern.
- Added to all three register stacks at different strengths, and always **under** the lighting
  layers: carved 1.0 (wood/stone), technical 0.80 (machined metal), casual 0.55 (printed
  surfaces — a full-strength grain there would collapse the register distinction).
- Clipped to the widget's own silhouette via a new shared `KitControl.OutlinePoly`, so a pill's
  wood does not spill past its round ends.

### Bugs caught, all by checks that could fail

1. **`score_all` returned 0 patterns** (pointed at the pack root, not `PNG/Default`) and every
   genre printed `NO CANDIDATE` — reads like a selection problem, is a path problem. Now raises.
2. **Five genres were assigned near-identical patterns**, because ranking on `dir` alone put six
   near-isotropic materials in a heap. That would have rebuilt "they all look the same" *inside
   the layer built to cure it*. Fixed with per-genre uniqueness plus the coarseness axis.
3. **The first coarseness metric was dead** — `hf(64)/hf(256)` returned a saturated `2.00` for
   all 171 patterns and still produced a full, plausible assignment table. Replaced with mean run
   length, verified linear against synthetic checkers (cell C scores exactly C/256).
4. **`Invalid polygon data, triangulation failed`** — `Spiked`/`Torn`/`Ellipse` are not always
   simple polygons, and Godot drew *nothing*. Left unguarded this would have made rpg and
   survival — the two most distinctive materials — the only flat plates in the set, silently.
   Now guarded by `Geometry2D.TriangulatePolygon`, falling back to the bounding rect **and
   warning**. Only `puzzle` currently needs it.

### Verified

- `dotnet build` — **0 errors**
- `validate_scenes.sh` — **PASS**
- `verify_greyscale.py` — **PASS**, 0 indistinguishable pairs
- Ten-genre render inspected: topdown's brick, rpg's planks, racing's hex cobble, platformer's
  scales and shooter's fine plate are all visibly different materials at constant colour.

### Still open

- [ ] **The material axis is not yet GATED.** `measure_material.py --proof` still refuses,
      because at 130×45 the crop is dominated by the button's label. Needs a **label-free proof
      render** before a threshold can be enforced; until then the axis is verified by eye.
- [ ] Outline column unchanged and still under the 0.040 bar: **racing↔shooter 0.018**,
      **platformer↔puzzle 0.026**, **cardgame↔topdown 0.042**. Grain does not move outline.
- [ ] `Capsule`'s overhanging left cap — now **twice** referenced (ui1, ui9's currency pill).
- [ ] Per-family panel lightness; `HSlider`/`TabContainer`/`OptionButton`/`CheckBox` sweep.
- [ ] Stage 30's ten per-genre HUDs.

### Stage 38b — the material axis is now GATED (2026-07-29)

The item left open above is closed. `measure_material.py --proof` no longer refuses; it grades.

**The blocker was the input, not the metric.** `gs_*.png` carry a "PLAY" glyph across a 130×45
plate, so the inset crop is ~73×25 and mostly letterform — which is why flat-filled plates once
scored *above* diamond plate. `KitProofProbe` now renders a **second pass per genre**: the same
widget, no label, 420×260. Those are `gm_*.png`, and they are the only files the gate will grade —
`gs_*.png` are still reported but marked `includes glyph — NOT gradeable`, so the bad input cannot
quietly come back.

Two requirements, checked separately because they fail for different reasons:

| check | bar | why |
|---|---|---|
| material present | `hf ≥ 0.0028` | half of stone's 0.0055 — the subtlest reference tile. Below that a plate is a flat fill. |
| pairwise separation | `dist ≥ 0.010` | two genres that measure the same are not telling themselves apart |

**The gate failed on first run, and the failure was real:** `puzzle vs racing 0.0093`. Their
reference materials (graph-paper `dir 0.00`, brushed-metal `dir 0.02`) are both isotropic, so
ranking on direction handed both a large blob at one tile — *different files, same look*, which is
the original complaint reproduced inside the fix for it.

Fixed in `pick_grain.py` with a **separation repair**: uniqueness of the FILE was never the
requirement; separation of the RESULT is. It repeatedly takes the worst pair and moves the genre
whose material fit is weaker to its next candidate.

That loop **cycled** on the first attempt — shooter oscillated `42 → 59 → 42 → 59` forever, because
"next free candidate that isn't the current one" flips between two once both beat everything else.
Each genre now advances monotonically through a `tried` set, with a fallback to the other half of
the pair when one side exhausts its candidates.

Final assignment after repair (3 genres moved): platformer `42→58`, puzzle `32→41`,
shooter `41→08`. Worst non-sharing pair at design time **0.056** (bar 0.055).

**Verified — every gate run, exit codes checked:**

| gate | result |
|---|---|
| `dotnet build` | 0 errors |
| `measure_material.py --selftest` | exit 0 |
| `measure_material.py --proof gm_*` | **MATERIAL PASS**, exit 0, closest pair **0.0668** (was 0.0093 FAIL) |
| `measure_material.py --proof <bad glob>` | exit **1** — the refusal path still refuses |
| `verify_greyscale.py` | PASS, 0 indistinguishable pairs |
| `validate_scenes.sh` | PASS |
| `Invalid polygon` errors | 0 |

Inspected too: rpg reads as vertical planks, shooter as chevron tread plate, topdown as brick,
survival as triangular hide facets, platformer/puzzle/racing/cardgame as four different cobbles.

**Honest caveat:** citybuilder and strategy render nearly flat by eye (amp 0.136, derived from
stone's `hf 0.0055` — genuinely the subtlest of the nine reference tiles). They pass the
present-material bar and separate from everything else, but if stone should read more strongly
that is a change to the *reference target*, not to the pipeline.

**Unchanged and still open:** the outline column. racing↔shooter **0.018**, platformer↔puzzle
**0.026**, cardgame↔topdown **0.042**, all under the 0.040 bar. Grain does not move outline, and
this stage did not attempt to.

### Stage 38c — the outline column, closed (2026-07-29)

All three under-bar pairs are fixed. **Zero indistinguishable pairs, and the MARGINAL section is
now empty** — the first time that has been true.

| pair | before | after | bar |
|---|---|---|---|
| racing ↔ shooter | 0.018 | **0.107** | 0.040 |
| platformer ↔ puzzle | 0.026 | **> 0.111** (out of the closest five) | 0.040 |
| cardgame ↔ topdown | 0.042 | **0.048** | 0.040 |

Read off the **144 dpi EPS renders in greyscale**, which is what the Ghostscript pipeline was
built for:

- **`Capsule` implemented at last** — ui1's mission bar, ui9's currency pill and the mobile kit's
  `$ 200` chip are the same object: a circular cap **overhanging the left end**, wider than the
  bar is tall. **Three independent references**, and it had been drawing as a plain rounded rect,
  which is exactly why platformer never separated from puzzle. It now leaves its bounding box,
  like `Spiked`.
- **`Asymmetric` (shooter)** — the sci-fi HUD sheet's tell is *asymmetry*: two diagonally opposite
  corners cut long, the other two square, plus a shallow notch bitten out of the top edge. A
  symmetric cut on all four corners is just a chamfer, which is why shooter and racing were 0.018
  apart while both were "a rectangle with its corners off".
- **`Stepped` (topdown)** — pixel-era corners are a staircase, not an arc. Unlike a radius it
  survives being measured at small size.

**A first cut of `Asymmetric` only moved the pair 0.018 → 0.027** because it sized the cut off the
shared corner *fraction*: an 11px nick on a 128px plate still reads as a rectangle. Sizing it off
HEIGHT (0.62×) took it to 0.107.

#### New gate: `poly_probe.tscn` / `PolyProbe.cs`

Every `KitShape` × 5 aspect ratios must produce a polygon Godot can triangulate. Written because
a shape that fails triangulation **draws nothing**, and the only previous signal was an error
buried in a render log — after the silhouette had shipped.

It failed on its first run and found more than the new shapes:

- **`Pill` and `Ellipse` failed at EVERY size** (25/25). At the stadium limit (`rad == min(w,h)/2`)
  the two corner centres on the short axis coincide, so consecutive arc points land on top of each
  other and the triangulator rejects the polygon. This was the *real* cause of puzzle's grain
  fallback, which had been mis-attributed to an exotic silhouette. Fixed by deduping coincident
  points; `grain-fallback` count is now **0**.
- **`Capsule` failed on square and tall controls** — the disc cannot be both larger than the bar's
  radius and inside the width, so the intersection went imaginary. Now guarded by aspect
  (`w >= h * 1.8`), degrading to an ordinary pill.

Result: **105/105 polygons valid**.

#### Verified

| gate | result |
|---|---|
| `dotnet build` | 0 errors |
| `poly_probe` | **105/105**, 0 FAIL |
| `verify_greyscale` | PASS — 0 indistinguishable, **0 marginal** |
| `measure_material --proof gm_*` | **PASS**, closest pair 0.0664 |
| `measure_material --selftest` | PASS |
| `validate_scenes.sh` | PASS |
| `Invalid polygon` in render | **0** |

Inspected: platformer's overhanging disc, shooter's notched asymmetric plate and topdown's stepped
corners are all unmistakable at button size.

**Rough edge, stated:** platformer's cap is large relative to its bar — structurally the referenced
form, but ui1's is more restrained. Worth a proportion pass, not a re-implementation.

#### Still open

- [ ] Panel lightness per family (gameui4/5 white, store parchment, rpgui dark wood)
- [ ] `HSlider`/`TabContainer`/`OptionButton`/`CheckBox` sweep onto kit widgets
- [ ] `settings_menu` banner ↔ TabContainer overlap
- [ ] Stage 30's ten per-genre HUDs — now the largest open item in this file
- [ ] citybuilder/strategy grain reads near-flat (faithful to stone's measured subtlety; revisit
      the target if it should read harder)

### Stage 38d — the last stock controls swept (2026-07-29)

`settings_menu` and `topdown/pause_subscreen` — the screen reported as "a mess" — were still built
from stock Godot controls. **21 controls across 5 files** now carry kit chrome.

#### Drop-ins, derived from the Godot type

`KitSlider`/`KitTabStrip`/`KitToggle`/`KitArrowSelector` all derive from `KitControl`, so swapping
scenes onto them would have returned **null** from every typed lookup — `SettingsMenu.cs` alone
resolves ten controls as `Find<TabContainer>`, `Find<OptionButton>`, `Find<CheckButton>`. That is
the trap that left 126 buttons unconverted until `KitPushButton : Button`. So:

| new drop-in | derives from | replaces |
|---|---|---|
| `KitSliderBar` | `HSlider` | 4 sliders |
| `KitOptionButton` | `OptionButton` | 8 dropdowns |
| `KitCheckButton` | `CheckButton` | 5 toggles |
| `KitTabPanel` | `TabContainer` | 4 tab strips |

Node types are unchanged; only `script =` is added. Every `Find<T>`, signal and layout survives.

**`KitChrome`** now holds the single band walk, shared by all five drop-ins — the register stack is
the kit's definition of what a plate IS, and five hand-copies would drift. `KitPushButton` was
refactored onto it and lost 72 lines of duplicated helpers (251 → 179).

**`sweep_controls.py`** applies it: dry-run by default, reports every node it touches, never stacks
a second script, and fixes `load_steps`. The last sweep of this kind was done by hand and shipped
two bugs that compiled clean.

#### Three bugs, all caught by looking at the render

1. **Sliders vanished completely.** `HSlider` derives its minimum size from the grabber ICON and
   the slider StyleBox; blanking both collapsed it to ~1px, `_Draw` hit its own `Size.Y <= 4`
   guard and returned. `settings_menu` rendered "Master Volume … 80%" with nothing between them
   and **nothing was logged**. Anything that blanks a control's theme art must restate the size
   that art was providing.
2. **Every tab title drew TWICE** — "AudioAudio", "DisplayDisplay". `TabContainer` is a COMPOSITE:
   it delegates its tab row to an internal `TabBar` that draws labels in C++, which suppressing
   the container's styleboxes does nothing about. Rewritten to supply real `StyleBoxFlat`s and let
   TabBar lay the row out. **Rule of thumb now recorded: own the draw for LEAF controls, supply
   styleboxes for composite ones.**
3. **`validate_scenes.sh` false-flagged three built-ins.** Attaching a C# script to a real Godot
   type made `min_value`/`max_value`/`button_pressed` appear on scripted nodes — legitimate
   snake_case built-ins whose PascalCase forms happen to match `[Export]`s elsewhere in the addon.
   Before the sweep no scripted node was also a `Range`, so the collision could not arise.

#### The validator is now factual, not heuristic

`tools/genre_shapes/classdb_dump.tscn` dumps **3087 real property names** from Godot's `ClassDB`;
the check consults that instead of guessing. An allowlist would have been a guess.

**Proved in both directions** rather than assumed:
- injected `on_role = 1` (a real `[Export]`, snake_case) → **FAIL**, correctly named
- built-ins present → **PASS**

A first attempt injected `accent = 1`, which the check ignored — single-word lowercase names match
neither branch. **Known remaining gap, stated:** a PascalCase export written all-lowercase and
single-word (`accent` for `Accent`) is still silently dropped and still not caught.

#### Verified

| gate | result |
|---|---|
| `dotnet build` | 0 errors |
| `validate_scenes.sh` | PASS (and proved it can still fail) |
| `verify_greyscale` | PASS |
| `measure_material --proof` | MATERIAL PASS |
| `poly_probe` | 105/105 |
| all 67 template scenes | rendered, **0 load/instantiate/draw failures** |

Inspected: `settings_menu` shows single tab titles with the selected tab in accent, and three
working sliders with track, fill and knob. `topdown/pause_subscreen` renders in topdown's own cream
palette with a correct tab row.

**Not claimed:** the 425 errors in the sheet render are pre-existing `InputMap` warnings
(`move_up` etc. are registered by the project generator, which a bare template render does not
run) — unrelated to this sweep, and unchanged by it.

### Stage 38e — settings alignment + a real checkbox (2026-07-30)

Two user-reported defects. Both were symptoms of something larger.

#### 1. "Settings" banner overlapped the tab row

Measured rather than eyeballed, with a rect probe: Frame at y=164 with a 34px banner (164–198),
tabs starting at y=188 — a **10px overlap**, the banner sitting over the "Controls" tab.

Cause: **`BeepDialogLayout.ApplyShell` stamps all four margins to a flat `OuterMargin = 24`**,
clobbering the `margin_top = 46` that `settings_menu.tscn` set deliberately for the banner. The
top is not a free choice when a panel carries a title plaque — the banner draws from the frame's
y=0 downward and overlaps the panel border by design.

Fixed by exposing `PanelFrameComponent.BannerRoom` and having `ApplyShell` take
`max(OuterMargin, BannerRoom + 6)` for the top only. **6 scenes changed, all correctly** (the
five besides settings_menu also carry banners); districts and the other 44 are untouched.

##### The bigger thing found on the way, and NOT fixed

`PanelFrameComponent.DriveChildMargins()` walks **`GetChildren()`** looking for a
MarginContainer. A survey of the templates found **0 frames with a MarginContainer child and 25
with one as a sibling** — so the method has never executed in a single shipped scene. Its whole
stated purpose ("content lands inside the recess without every scene hand-tuning four margins
against art it cannot see") has never once happened; all 25 scenes hand-tune instead.

I tried fixing it and **reverted**. Driving all four margins from `WellRect` re-rendered 23 of 67
scenes: settings_menu and theme_gallery improved, but **districts lost content** — its rows were
already laid out to the frame, so re-insetting clipped "Happiness 78%" and the value off the first
row. Narrowing to top-only-increase-only still regressed districts, because the extra top margin
feeds back into the frame's own `GetCombinedMinimumSize` sizing and squeezes the row.

Left as a known defect with the evidence, rather than shipped as a regression. Fixing it properly
means untangling that size feedback loop and re-tuning the 25 scenes that compensated for it.

#### 2. "checkbox is not a checkbox"

Correct — `CheckBox` was never swept (only `CheckButton` was), so both instances rendered as
Godot's stock 16px blue glyph on a themed plate. The distinction matters and the kit now keeps it:

- **`KitCheckButton`** — a SWITCH: track + sliding knob (Godot's CheckButton)
- **`KitCheckBox`** *(new)* — a BOX that gets a drawn tick, sized off the theme font rather than a
  fixed 16px icon

#### A drawing rule, learned twice more

**Redraw the label only if your plate hid it.**

`KitPushButton`'s plate covers the whole control, so it must redraw the text. `KitCheckBox` and
`KitCheckButton` cover only a small box or switch, so the base class's label survives — and
drawing it again rendered "Textures" twice, overlapping. Same family as the TabContainer bug
(own the draw for leaf controls, supply styleboxes for composite ones). Both now documented in
the classes themselves.

#### Verified

| gate | result |
|---|---|
| `dotnet build` | 0 errors |
| `validate_scenes.sh` | PASS |
| shadowing gate | ok |
| all 67 template scenes | rendered, 0 failures |
| scenes changed | 6, each inspected |

Inspected: the "Settings" plaque now clears the tab row; the gallery's Textures checkbox is a
box with a tick and its label renders once.

### Stage 38f — a named type scale, and a slot you can populate (2026-07-30)

#### 1. Type was one flat size everywhere

**79 of the kit's 86 font-size call sites were a bare `UiSurface.FontSize(this)`** — the theme's
body size, knowing nothing about the widget drawing with it. So a 24px count badge and a 200px
card title rendered identically: banners read as tiny captions on large panels ("INVENTORY",
"EQUIPMENT"), slot badges were barely legible, and card names looked like footnotes.

Fixed with a **named role scale** on `UiSurface`, deliberately reusing the SAME multipliers as the
Label `theme_type_variation`s that `ThemePresetComponent` already registers — so a drawn card title
and a `BeepTitle` Label beside it agree, and the scale changes in one place:

| role | × | matches |
|---|---|---|
| `Title` | 1.90 | `BeepTitle` |
| `Subtitle` | 1.35 | `BeepSubtitle` |
| `Value` | 1.25 | `BeepValue` |
| `Body` | 1.00 | — |
| `Caption` | 0.85 | `BeepCaption` |
| `Small` | 0.70 | *(new — drawn widgets need a step below Caption for badges)* |

Two entry points: `FontSize(n, role)` when the widget grows to fit its text, and
**`FitRole(n, role, box, text, font)`** when the widget draws into a box it controls — the role
sets the intent, the box sets the ceiling, and it shrinks to fit the width rather than overflowing.

Applied where the box is fixed and the text was breaking: banner (`Subtitle`), card title
(`Title`), card/slot requirement (`Caption`/`Small`), slot count badge (`Small`), radial-meter
centre (`Value`), tab-strip label (`Body`, fitted to `Size.X / tabCount`).

**Deliberately NOT blanket-replaced.** Each widget has two sites — one sizing the widget from its
text and one drawing into a box. Only the second is the bug; fitting the first would fight the
widget's own minimum size. Chip, LabelValue, Tooltip and CurrencyBar grow to fit and were left
alone.

#### 2. `KitInventorySlot` — the inspector-populated slot

`KitSlotGrid` holds a `List<Slot>` of plain C# objects, so a slot's icon and count can only be set
from CODE. Right for a runtime bag, useless for building a screen in the editor.

`KitInventorySlot : KitControl` is the drag-and-drop counterpart: `[Export]` **Icon**, **Count**,
**Rarity** (a palette *role*, not a colour), **Locked**, **Requirement**, **Selected**, plus a
`SlotPressed` signal. Draws the recessed well, the item, a corner count badge, a rarity rim, the
padlock and its requirement — all in the active genre's material.

`Icon` may be null on purpose: the framework ships no item art, so an empty slot is a legitimate
state and draws as an empty well rather than warning.

Applied to `rpg/inventory.tscn`: its 12 `ItemSlot*` were bare decorative `PanelContainer`s that
`Inventory.cs` never referenced, so converting them broke no typed lookup. Four are populated to
make the exports self-evident.

##### Three bugs caught by rendering it at three sizes

Rendering one size would have proved nothing, since the whole defect was that type did not change
with size.

1. **rpg's `Spiked` silhouette hung triangular points off the bottom of every slot in the grid.**
   A slot is a container for someone else's art drawn in a tiled row, and the shapes that give a
   button its identity make a terrible slot. `SlotShape` now tames the exotic ones
   (`Spiked`/`Torn`/`Capsule`/`Shield`/`Ellipse`/…) to `Round`; genre still reads through corner
   radius, frame, material and rim. `OverrideShape` still wins.
2. **The locked requirement drew OUTSIDE the slot**, colliding with the slot below it.
3. **The count badge straddled the corner at 0.62 and came out clipped.** Now 0.92.

And one caught before it shipped: the tab-strip label was **measured** at the new fitted size but
still **drawn** at the old one — a silent misalignment that compiles fine.

#### Verified

| gate | result |
|---|---|
| `dotnet build` | 0 errors (both repos) |
| `validate_scenes.sh` | PASS |
| shadowing gate | ok |
| 67 templates + 52 game scenes | rendered, 0 failures |

Inspected: banners and card titles now scale with their boxes; the inventory grid shows real slots
with count badges, a rarity rim and a locked slot naming its requirement — in the addon and in the
user's game.

### Stage 38g — Beep Game Builder dock revised (2026-07-30)

The dock is the addon's main user-facing surface and had gone untouched while everything around it
changed. It is built from stock `OptionButton`/`CheckBox`/`Button` and **that stays** — it runs in
the editor and should look like the editor, not like a game. Applying kit chrome to an editor dock
would be the wrong call.

#### 1. Zero editor-scale awareness

Six hardcoded metrics — font sizes 16 / 10 / 10 / 13, a 100px output box, an 8px spacer, a SpinBox
width — and **no reference to `EditorInterface.GetEditorScale()` anywhere**. Godot scales the whole
editor on a hiDPI display (1.5×, 2×, and users can set it manually), so the dock's 10pt captions
and 8px spacers were unreadable slivers at 2× while every native panel beside them looked right.

Same defect class as the kit's flat font size, one layer up: type and metrics that ignore context.

Added `S(px)` (× editor scale) and `DockFont(scale)` (relative to the *editor's* own font size, not
a literal), and moved all six onto them. The multipliers mirror `UiSurface.TextRole` on the runtime
side — 1.30 title, 1.05 section header, 0.85 caption — so both sides read the same way.

#### 2. The genre/theme cascade failed silently

`OnGenreChanged` and `OnThemeChanged` each had two bare `return`s on a null catalog lookup. The
theme and palette dropdowns simply stayed **empty**, with no way to tell a catalog-loading failure
from a genre that legitimately has no themes — both look like a dead dropdown. Now each says which
it is, and a genre declaring no themes names the path to add one.

#### 3. A refresh with no diagnostic — `BeepSceneDrift`

Generated scenes are COPIES, so an addon update never reaches them. The dock could already force a
refresh ("Overwrite existing scenes") but there was **no way to find out whether it was needed** —
the destructive option existed and the diagnostic one did not.

> Correction to Stage 38f's closing note, which said there was "nothing that detects drift" and
> proposed adding a refresh: the refresh already existed. What was missing was *reporting*.

New read-only `core/BeepSceneDrift.cs` + a dock button, reporting scenes behind their templates,
scenes that are the developer's own (never touched by a refresh, so counted separately rather than
as drift), and duplicate basenames that make the pairing ambiguous.

**It is a separate UI-free class for a discovered reason:** `BeepGameBuilderDock` creates an
`EditorResourcePicker` in `_Ready`, so **instantiating the dock outside the editor SEGFAULTS**
(0xC0000005 in `BuildUI` → `AddChild`). Pre-existing, and it means nothing embedded in the dock can
be tested headlessly. The comparison is plain logic and now runs anywhere; the dock only formats.

An unreadable file counts as drifted, not identical — treating it as up to date would be exactly
the quiet false negative the class exists to remove.

#### Verified

`tools/genre_shapes/drift_probe.tscn` asserts **all eight outcomes** — up-to-date, drifted, the
drifted file's name, own-screens, duplicate basename, and both empty-project branches. **PASS.**
This matters because a check only ever seen taking its "nothing generated" branch is no evidence
the comparison works, which is how the first version of this probe would have shipped.

| gate | result |
|---|---|
| `dotnet build` | 0 errors |
| `validate_scenes.sh` | PASS |
| shadowing gate | ok |
| `poly_probe` | 105/105 |
| 67 template scenes | rendered, 0 failures |
| `drift_probe` | 8/8 PASS |

#### Still open in the dock

- [ ] It cannot be smoke-tested at all outside the editor (`EditorResourcePicker` in `_Ready`).
      Deferring that picker until first use would make the whole dock headless-testable.
- [ ] `EditorPlugin` and the widget fields are non-nullable and unassigned — part of the ~148
      pre-existing nullable warnings.

---

## Stage 39 — the per-file art pass, and why genres still look alike (2026-07-30)

Reported, correctly, for the *n*th time: **some genres still do not read as different**, the
Example_Art images were never worked through **one by one**, and the kit has no per-genre
**border/layer construction, shadow, or font**. All four are true. This stage is the pass and the
plan it produced — no kit code changed yet, deliberately.

**New documents**
- **[`plans/game-ui-kit/ART_PASS_PER_FILE.md`](game-ui-kit/ART_PASS_PER_FILE.md)** — one row per
  image: frame construction, layer stack, shadow, corner, typography, texture, and what it suits.
  **13 of 60 read in depth**; the remaining 47 are listed by genre and priority.
- **[`plans/game-ui-kit/PLAN_STYLE_SYSTEM.md`](game-ui-kit/PLAN_STYLE_SYSTEM.md)** — the plan,
  with every claim traced to a numbered file.

### The finding that reframes all of it

The kit models genre → look as a **1:1 lookup** (`ShapeForGenre`, `ForGenre` → one register).
**citybuilder alone appears in five mutually exclusive registers** — cartoon-outlined, flat
translucent, papery minimal, monochrome drawer, carved stone. They disagree about outline polarity,
shadow, corner radius, texture *and* typography.

So **genre does not determine the look.** It constrains which looks are plausible; the **theme**
picks one. The theme layer already exists (`catalogs/skins/<genre>/themes/<theme>/`) — the style
properties are simply not in it. One register per genre cannot be made to work by adding more
silhouettes.

### Eight axes the kit lacks, each with the file that proves it

| axis | proof |
|---|---|
| **Shadow as a layer** — hard / soft / none / glow all appear; `KitLayerKind` has no `Shadow` | 01·06 / 02·04·11·13 / 03·07·09·10 / 06 |
| **Outline polarity** — `Casual` hardcodes thick *dark*; the art also uses thick *light*, hairline, none, dashed | 12 / 02 / 09·10 / 04 / 03 |
| **Frames are constructed** — rivets, metal brackets, corner ticks, L-brackets, gold double-line, organic log, top-rounded-only | 11 / 01·11 / 10 / 10 / 11 / 13 / 04 |
| **Font family / weight / case** — the kit has **no font family at all** | serif 11 · bold condensed caps 06·08·10 · thin letter-spaced caps 07 · light 04·09 · rounded display 12·13 |
| **Corner is not one number** — sharp, shear, small, large+wobble, full pill, per-widget mixed | 07·10 / 08 / 09 / 12 / 04 / 11 |
| **Meter end caps**, per tier | 11 |
| **Attachments overhanging the host** — cap-left, medallion-top, corner flag, edge arrow, awning | 02·12 / 04 / 13 / 13 / 06 |
| **Selection has several renderers, two on one screen** | 09 (fill *and* border) · 06 glow · 05 fill+border · 10 border |

### Phases (each gated, each gate shown to fail first)

- **A — `KitLayerKind.Shadow`** + `measure_shadow.py`. Smallest change, unblocks the most.
- **B — outline polarity + corner per widget class.** `verify_greyscale.py` gains a **polarity**
  column; today it measures rim magnitude only, so a light and a dark rim of equal contrast are
  indistinguishable to it.
- **C — typography: family, weight, case, tracking.** Fonts must be **CC0 and shipped** (Kenney UI
  packs, same licence and source as the grain patterns). `measure_type.py` must also assert every
  declared family *resolves* — a missing font falls back silently and looks exactly like having no
  family at all.
- **D — constructed frames** (corner ornaments as a layer).
- **E — attachments + meter end caps**, verified at three host sizes.
- **F — selection as a SET of renderers**, keyed by widget class.
- **G — two or three style packs per genre**, only after A–F.

**A, B and C are the ones that answer the complaint.** Silhouette (Stage 38c) was necessary and is
not sufficient — two themes of the same genre are separated by shadow, outline polarity and type.

### Stated, not implied

- No kit code changed in this stage. The deliverable is the pass and the plan.
- 13/60 files read; the model survives 13 files across 6 genres. If a later file contradicts it,
  the model changes — which is why the per-file notes exist rather than a summary.
- **Organic/illustrated frames are out of scope**: they need authored art the addon does not ship.
  The baked-texture path plus documentation is the honest answer there.

### Stage 39b — pass continued to 28/60; the frame model had to grow (2026-07-30)

Read 15 more files. Two of them changed the plan rather than confirming it.

**`futuristic-hud-frames` (file 14) broke Phase D.** A sci-fi frame is not a border with decorated
corners — it is a **run list per edge**: the stroke varies in weight along its length, breaks and
restarts, turns into solid blocks, carries hatch and tick runs, steps at the corners, and is
**deliberately asymmetric between corners**. No StyleBox, no silhouette and no corner-ornament enum
expresses that. Phase D is now `KitEdgeRun` (segments per edge + per-corner steps), and it is the
**biggest** phase, not the smallest. A plain rectangle is its degenerate case, so existing themes
are unaffected.

**`medieval-royal-knight` (file 25) grew Phase E.** Victory, Restart and Settings are told apart by
**ornament alone** — crown, helm, gear. So attachments are keyed by **screen archetype**, not only
by placement.

Also added: four more frame *construction* families (masonry 22, plank 15·28, double-border-with-gap
26, frame+torn-insert 23); **engraved/debossed** text as a third treatment beside plain and outlined
(22); **handwritten** as a font role (18); the plate's **two-tone top band** — hard-edged (17) and
curved-glass (27) — which the kit's soft linear `Gloss` reproduces neither of; state encoded by the
**frame** with unreachable shown by **alpha** rather than desaturation (20).

Confirmations worth recording: the left-overhanging icon cap now has **five** independent sightings
(02·12·17·19·28); the header plaque overhanging the top edge appears in six files; the collapsible
handle spec (outside, on the moving edge) is confirmed by 24.

**Coverage: 28 of 60.** The remaining 32 are listed by genre in `ART_PASS_PER_FILE.md`. Phase order
re-sequenced to A → B → C → D → E → F → G, with D given its own gate.

### Stage 39c — pass at 46/60; three more model changes (2026-07-30)

Read 18 more files. Verified by hash that **`gameui9.png` is byte-identical to `ui7.png`**, so the
folder holds **59 unique images**, not 60.

Three findings changed the model again:

1. **A fifth shadow kind, `Extrude`** (file 35) — a thick dark **side face** under panel and button,
   so each reads as a solid slab. Not a drop shadow, not a bevel, not a glow. And two files use
   **no shadow at all** with different compensations: three **layered concentric strokes** (41) or
   pure value contrast against a **ragged silhouette** (38).
2. **`Pixel` is a register, not a corner treatment** (40·42). Choosing pixel decides outline weight
   (1px), anti-aliasing (off), corner construction (stepped), font (bitmap) and shadow (none)
   *together*. Modelling it as `KitShape.Stepped` alone guarantees a pixel theme draws smooth type
   and soft shadows inside a stepped outline.
3. **The overhanging header plaque is the most repeated construction in the folder** — eleven files,
   in four shapes (bar, ellipse, ribbon-with-folded-ends, sheared tab). The kit draws its banner
   *inside* the host at 0.14 × height. **Wrong side of the edge**, and a direct cause of panels
   reading flat against the references.

Also recorded: `CapRight` as the mirror of the left icon cap (41, first sighting); rarity carried by
**tile fill colour** (37) and by **tooltip title colour** (32); unaffordable shown by desaturating
**only the price footer** (39); two tab levels using **different active renderers** on one screen
(39); `???` placeholder text on locked content (32); K/M number abbreviation (37); input-hint
**chords** (42).

**Coverage: 46 of 60 files (45 entries, one duplicate).** The 14 unread are named in
`ART_PASS_PER_FILE.md`. Two of them (`uitexturs`, `uiwood`) were read earlier this session and are
already reflected in the tracker. The remaining twelve belong to families documented several times
over and are *expected* to confirm rather than extend — **stated as a prediction, not a result.**

### Stage 39d — art pass COMPLETE, 59/59 (2026-07-30)

All 59 unique images read (60 files; `gameui9.png` == `ui7.png` by hash).
`plans/game-ui-kit/ART_PASS_PER_FILE.md` holds 55 numbered entries plus 3 recorded earlier.

The last 14 files added no new **axis** — the model held — but two of them isolate it cleanly and
three add states the kit lacks.

**The two proof images.** `square-wooden-frames` is **one geometry × many attachments** (six avatar
frames, identical block, identity from vine / rope / nothing). `wooden-game-buttons` is **one
material × many silhouettes** (chamfer, capsule, hexagon, notched, tiered, circle, triangle, all in
one wood). Together they settle it: **geometry, material and ornament are three independent axes**,
and the kit currently ties all three to `genre`.

**Three missing states.** A **fourth empty state** — a *ghosted silhouette* of what belongs in the
slot, beside blank / invite-`+` / locked (`KitInventorySlot` has the first three). **Comparison
indicators** — stat chips turning green with an up-arrow to show the delta against equipped gear.
And an **authored four-state set** (`Normal · Over · Click · Disabled`) with per-state icon variants:
the kit derives states procedurally, which is right, but should *accept* authored art where a theme
supplies it.

**Typography, final tally: nine families across the folder** — serif, blackletter, bitmap/pixel,
handwritten, bold-condensed-caps, thin-letter-spaced-caps, light, rounded-display,
typewriter/condensed-serif. **The kit ships one.**

**Counts worth keeping.** Header plaque overhanging the top edge: **15 of 59 files** — the single
most repeated construction in the folder, and the kit draws its banner on the wrong side of the
edge. Icon cap overhanging an end: 7 left, 1 right. Welded footer/cost strip: 6. Segmented meter: 7
independent sightings, so "segmented is the default" holds.

The plan (`PLAN_STYLE_SYSTEM.md`) is now final for phases A–G and traced to numbered files
throughout. **Next turn starts Phase A (`KitLayerKind.Shadow` + `measure_shadow.py`), which must be
shown to fail on today's build before it is trusted.**

### Stage 40 — Phase A: the shadow layer, and a gate that measures it honestly (2026-07-30)

`KitLayerKind` had **no `Shadow` member**: every widget in every genre drew flat onto whatever was
behind it, while the art pass found five distinct behaviours across the 59 references.

**Built**: `KitShadowKind { None, Hard, Soft, Glow, Extrude }`, `KitShadowDef` as theme data,
`KitShadow.Draw` (drawn first, under the whole stack, in a dark tint of the *surface's own hue* so
a parchment theme casts a warm shadow and a metal one a cool shadow), and a per-genre assignment
read off the art:

| soft | hard | extrude | none |
|---|---|---|---|
| rpg · survival · cardgame | citybuilder · strategy | platformer | shooter · racing · puzzle · topdown |

#### The gate had to be rebuilt twice, and both rebuilds were caused by it failing

`measure_shadow.py` classifies a rendered widget's shadow on colour-invariant measures
(spill · coverage · falloff · offset · **axis ratio** · solidity). Its `--selftest` synthesises all
five kinds and **passes 5/5**.

1. **First version scored 3 of 5 synthetic cases wrong.** Its "widget" threshold swallowed dark
   shadows into the body, so a textbook hard shadow measured `spill 0.0000`. Fixed by taking the
   widget's rect as *given* rather than inferred.
2. **Second version reported a confident `hard` for all ten genres on a build with no shadow layer
   at all** — it assumed a 260×150 widget against a 420×260 render, so the measuring ring sat
   *inside* the plate. Assuming the rect is the same mistake as inferring it.
3. **Third version is immune to both**: the probe renders each widget **twice**, with shadows on
   and off (`KitShadow.Enabled`), and the gate analyses the **difference**. Silhouette cancels
   exactly — which matters because `Capsule`/`Spiked`/`Torn` deliberately draw *outside* their rect
   (they made platformer and rpg measure as `extrude` on a shadow-free build) and `Shield` sits
   *inset* within its rect, so its shadow never leaves it.

#### One real rendering bug the gate caught

Shadow offsets were sized off `FramePx`. The Casual genres declare `KitFrameMode.None`, so their
frame is ~0 and **their shadows collapsed to about one pixel** — cardgame and platformer rendered a
shadow too small for the gate to see. Now sized off the widget's short edge: a shadow scales with
the thing casting it.

#### Result — 7 of 10, and the 3 that fail are NOT tuned away

| gate | result |
|---|---|
| `measure_shadow --selftest` | **PASS 5/5** |
| `measure_shadow --proof` | **FAIL — 7/10 match** |
| `dotnet build` · `validate_scenes` · shadowing · `poly_probe` · 67 scenes | 0 errors · PASS · ok · 105/105 · 0 failures |

**All four `none` genres are exactly right, and `soft` is right for rpg, survival and cardgame.**

**Unresolved, stated rather than tuned away:** citybuilder and strategy (`hard`) and platformer
(`extrude`) all render with a falloff the classifier reads as `soft` — interior solidity
0.20–0.30 where a crisp edge should be ~1.0. Eroding the anti-aliased collar barely moved it
(0.32 → 0.30), so it is **not** an AA artefact: those shadows are genuinely being drawn with
graded depth, and the cause is not yet found. Lowering `SOLID_HARD` until the gate went green was
the obvious move and is exactly the failure this repo keeps paying for, so it was not done.

Next: find why a single opaque `DrawColoredPolygon` produces graded depth for those three, then
Phase B (outline polarity).

### Stage 40b — Phase A closed: SHADOW PASS 10/10 (2026-07-30)

The three failures left open in Stage 40 are resolved, and the cause was **one real rendering
defect plus two confounded measurements** — not thresholds.

#### The rendering defect: a widget showed its own shadow through itself

Histogramming the difference render made it obvious. citybuilder's shadow depth was **bimodal** —
**9068 px at depth 5–18** alongside **4725 px at 94–107**. The faint population was the shadow
showing *through* the plate: five shipped themes declare a 95 %-opaque panel (`#…F2`), and the
shadow was being drawn underneath it.

Fixed by **subtracting the widget's own silhouette from every shadow pass**
(`Geometry2D.ClipPolygons`). Where a pass is entirely covered it now draws nothing, rather than
falling through to the uncut polygon — which would have quietly reintroduced the bleed.

**After: 3276 lit pixels, every one at depth exactly 107.** A uniform hard shadow.

#### Two measurements that were confounded by silhouette, not by tuning

- **Solidity** was averaging the bleed-through population with the real shadow, so three crisp
  shadows measured `soft`. Now judged only where the shadow is **unobstructed** — outside the
  widget's body.
- **The axis ratio got the last two exactly backwards.** Capsule's overhanging left cap pulled an
  extruded, straight-down shadow to `axis 0.53`; Shield's narrowing bottom hid the horizontal half
  of a diagonal shadow, giving `axis 0.03`. Replaced with **side-vs-below extent** — "does any of
  it fall past the body's side?" — which a side face never does and a diagonal drop shadow always
  does. Immune to silhouette shape.

`SOLID_HARD` and the other thresholds are **unchanged** from Stage 40.

#### Verified

| gate | result |
|---|---|
| `measure_shadow --selftest` | **PASS 5/5** synthetic kinds |
| `measure_shadow --proof` | **SHADOW PASS 10/10** |
| `verify_greyscale` | PASS |
| `measure_material` | MATERIAL PASS |
| `poly_probe` | 105/105 |
| `validate_scenes` · shadowing · `dotnet build` | PASS · ok · 0 errors |
| 67 template scenes | rendered, 0 failures |

Inspected: citybuilder throws a crisp offset shadow, platformer a solid side face, rpg a soft
ambient one, shooter none — each as its theme declares.

**Rough edge carried forward:** platformer's `Capsule` still reads as a disc overlapping a bar
rather than one bar with an overhanging cap. Noted in Stage 38c and still true.

**Phase A is closed. Next: Phase B — outline polarity**, which needs `verify_greyscale.py` to gain
a polarity column; today it measures rim magnitude only, so a light and a dark rim of equal
contrast are indistinguishable to it.

### Stage 41 — Phase B started: outline polarity is now theme data, and the gate found a regression (2026-07-30)

**Built.** `KitGeometry.OutlineShade` — the multiplier the outline BAND takes against the plate
(&lt;1 dark, &gt;1 light). `KitStacks.Casual` hardcoded `shade: 0.16` (thick dark) and
`KitStacks.Technical` hardcoded `1.42` for **every** genre in those registers; both now declare
`shade: -1`, a sentinel both renderers resolve to the theme's `OutlineShade`. Assigned from the
art: platformer/cardgame 0.16 · topdown 0.22 · puzzle **1.85** (the galaxy-space kit's thick
**light** stroke) · shooter 1.90 · racing 1.85.

**Gate.** `verify_greyscale.py --expect-outline` asserts each genre's measured `rim:body` polarity
against what its theme declares. `rim_body` was always *measured*; nothing ever asserted it. It
fails on today's build — which is the point.

#### The gate caught a regression I introduced in Phase A

`OUTLINE FAIL (4 mismatch)`, and the cause is **not** the outline code:

> **Adding the shadow layer broke the outline measurement.** The gate derives the widget's extent
> from its rendered bounds and samples the outermost two rows as "the rim". A soft shadow now
> extends those bounds outward, so those rows are **faint shadow over background**, not the
> outline band. `survival` reads `rim:body 5.06` — a nonsense figure that is the tell — and
> `cardgame` reads *light* while its band is genuinely `0.16` dark.

Verified it is not a stale render: the proof re-ran (file mtime advanced 21:15:37 → 21:16:01) and
produced a **byte-identical** result, which is correct — cardgame's new `OutlineShade` 0.16 happens
to equal the old hardcoded value, so nothing about it should have changed.

**Fix, next turn:** measure polarity on the **shadow-off** render. `shadow_probe.tscn` already emits
`nos_<genre>.png` for exactly this reason, and `KitShadow.Enabled` already exists. The lesson is
the one this repo keeps relearning: *a new layer can invalidate an existing gate's assumptions, and
the gate will keep reporting numbers rather than saying so.*

#### Verified

| gate | result |
|---|---|
| `verify_greyscale --selftest` | PASS |
| `verify_greyscale` (separation) | PASS |
| `verify_greyscale --expect-outline` | **FAIL 4/10 — confounded by the Phase A shadow, cause identified** |
| `measure_shadow` | **SHADOW PASS 10/10** |
| `measure_material` | MATERIAL PASS |
| `poly_probe` · `validate_scenes` · build | 105/105 · PASS · 0 errors |
| 67 template scenes | rendered, 0 failures |

Phase B is **not** complete: polarity must be re-measured shadow-free, and corner-per-widget-class
plus shear/wobble are untouched.

### Stage 41b — polarity now measured honestly; it says the RENDER is wrong (2026-07-30)

**The Phase A regression is fixed.** `verify_greyscale --expect-outline` no longer measures the
proof render — it measures the **shadow-free** pair `nos_<genre>.png` that `shadow_probe.tscn`
already emits, via a new `outline_rows()` that **refuses** if those files are absent rather than
quietly measuring the wrong thing.

Proof the fix is real: `survival` went from `rim:body 5.06` — a nonsense figure that was the tell —
to **1.82**. Every reading is now plausible.

#### And with an honest measurement, the gate points at the code

`OUTLINE FAIL (6 mismatch)`. Before assuming the gate was still wrong, I sampled the actual pixels
straight down through each widget's top edge:

| genre | outline band | plate | reading |
|---|---|---|---|
| cardgame | **82** | 48 | band is genuinely **lighter** than the plate → `rim:body 1.64` is **correct** |
| platformer | 82 | 48 | same |
| citybuilder | 46 → 111 → 62 → 36 | 26 | the four-band carved edge, as designed |

So the measurement is right and **the render is wrong**: `OutlineShade = 0.16` is declared for
cardgame/platformer/topdown and is **not reaching the draw** — their outline band renders at about
1.7× the plate instead of 0.16×. The sentinel resolves in `KitControl`'s `Plate` case, so either
that path is not the one drawing these widgets or the band being sampled is not the layer I think
it is.

**Deliberately not fixed by adjusting the expectations.** The art is unambiguous — ui1 and gameui4
are thick *dark* outlines — so the declaration is right and the renderer has to meet it.

#### Verified

| gate | result |
|---|---|
| `measure_shadow` | **SHADOW PASS 10/10** |
| `measure_material` | MATERIAL PASS |
| `verify_greyscale` (separation) + selftest | PASS |
| `verify_greyscale --expect-outline` | **FAIL 6/10 — render defect, located** |
| `validate_scenes` · shadowing · build | PASS · ok · 0 errors |

**Next:** trace why the `Shade < 0` sentinel does not change the outermost band for the Casual
genres — most likely candidate is that the sampled outermost band is the layer's *rim stroke*
(driven by `RimBrightness`) rather than its fill, in which case polarity has two sources and they
disagree.

### Stage 41c — two hypotheses disproved, one stale-render caught, the defect narrowed (2026-07-30)

Continuing the `OUTLINE FAIL 6/10` from Stage 41b. Three things established, none of them by guessing.

**1. The bevel hypothesis is wrong.** I expected the top rows to be bevel-lightened. Sampling the
LEFT edge at mid-height — where the bevel's top-light/bottom-dark gradient is neutral — gave the
*same* value as the top rows (cardgame 82 both ways). Not the bevel.

**2. A stale render was in play, and it mattered.** The gate reads `tmp/shadow/nos_*.png`, which
`shadow_probe.tscn` writes — and I had only been re-running `kit_proof.tscn` since adding
`OutlineShade`. The files were 23 minutes old. Re-rendering moved the Technical genres
(shooter 1.00 → **0.79**, racing 0.94 → **0.84**, puzzle 1.64 → **1.86**), so the sentinel *is*
reaching that path. **Any gate reading files a different probe produces needs that probe re-run;
nothing enforces this and it silently reported pre-change numbers.**

**3. The remaining defect is an INVERSION, and it is measured, not inferred.**

| genre | declared `OutlineShade` | measured `rim:body` | |
|---|---|---|---|
| puzzle | 1.85 (light) | 1.86 | ✅ |
| shooter | 1.90 (light) | **0.79** | ❌ inverted |
| racing | 1.85 (light) | **0.84** | ❌ inverted |
| cardgame | 0.16 (dark) | **1.64** | ❌ inverted |
| platformer | 0.16 (dark) | **1.90** | ❌ inverted |
| topdown | 0.22 (dark) | **1.66** | ❌ inverted |

Direct pixel evidence for cardgame: **outline band 82, plate 48**. With `face ≈ 48` and
`shade = 0.16`, the band should render near **8**. It renders at 82 — roughly `1.7 × face`, which
is what a *light* shade would produce. puzzle, which declares 1.85, is the only Casual genre that
comes out right.

So the sentinel resolves (Technical moved when re-rendered) but the resulting colour does not match
the declared multiplier for five of six genres. The wiring is verified present end-to-end —
`KitLayer` declares `shade: -1f`, `KitControl:662` resolves it, `KitButton` calls `DrawMaterial` —
so the next step is **runtime instrumentation**: print the resolved `shade` and the `face` colour
for the outermost layer. Reading the code has now failed twice; the value itself has to be observed.

**Not done:** changing the expectations to match the render. The art is unambiguous.

#### Verified unchanged

`measure_shadow` **PASS 10/10** · `measure_material` PASS · `verify_greyscale` separation + selftest
PASS · `validate_scenes` PASS · shadowing ok · build 0 errors.

### Stage 41d — CORRECTION: the render was right all along; the gate is wrong (2026-07-30)

**Stage 41c is wrong and this supersedes it.** It concluded "the render is wrong — an inversion",
stated as measured fact. Runtime instrumentation says otherwise.

`KitControl.DebugOutline` (kept, default off) prints each layer's resolved shade and the luminance
actually drawn:

| genre | band lum | plate lum | ratio | declared |
|---|---|---|---|---|
| cardgame | 0.016 | 0.100 | **0.16** | 0.16 dark ✓ |
| puzzle | 0.185 | 0.100 | **1.85** | 1.85 light ✓ |
| shooter | 0.190 | 0.100 | **1.90** | 1.90 light ✓ |

**`OutlineShade` works exactly as declared, for every genre.** The sentinel resolves, the multiply
and lift-toward-white branches are both correct, and the drawn colour matches.

#### Why I got it wrong

The `band 82 / plate 48` pixel evidence in Stage 41c came from `nos_*.png` files written at
**21:04** — sampled *before* I re-rendered them at **21:27**. I caught the staleness for the gate's
own numbers in the same stage and then drew a conclusion from the same stale files a few minutes
later. **Confirming freshness once does not make later reads of the same files fresh.**

#### So the defect is in the gate

`verify_greyscale`'s polarity reports cardgame **1.64** where the render is **0.16**, and shooter
**0.79** where the render is **1.90** — inverted, on freshly rendered input. Its `rim` is the median
of the outermost two rows and its `body` is the modal tone of an inner patch; one or both is not
sampling what the name says on these silhouettes.

**That is now the only open item in Phase B's polarity work**, and it is a measurement bug, not a
kit bug. The kit side of outline polarity is **done**.

#### Lesson, recorded

Three hypotheses were wrong this stage and the one before — bevel confound, sentinel not reaching
the draw, render inversion — and all three came from reasoning over code and stale pixels. The
runtime print settled it in one run. **When two reads of the code disagree with the pixels, print
the value; do not reason harder.**

#### Verified

`measure_shadow` **PASS 10/10** · `measure_material` PASS · `verify_greyscale` separation + selftest
PASS · `validate_scenes` PASS · shadowing ok · build 0 errors.

### Stage 41e — CORRECTION to the correction: the gate is right too (2026-07-30)

Stage 41d said "the render is right, the gate is wrong". Half of that is wrong. Sampling the
**freshly rendered** `nos_*.png` against the gate's own output:

| genre | gate `rim:body` | outer band px | centre px | verdict |
|---|---|---|---|---|
| cardgame | 1.64 | **82** | 48 | band really is lighter — gate correct |
| shooter | 0.79 | **30** | 36 | band really is darker — gate correct |
| puzzle | 1.86 | **108** | 58 | gate correct |

**The gate reports exactly what is on screen.** And Stage 41d's runtime print is also right:
`DrawShape` is handed a band colour of luminance **0.016** for cardgame against a plate of
**0.100**.

So both measurements are sound and they disagree, which means the discrepancy is **between the
colour handed to `DrawShape` and the pixel that lands in the framebuffer**:

| genre | drawn lum | expected byte | actual byte |
|---|---|---|---|
| cardgame band | 0.016 | ~4 | **82** |
| cardgame centre | 0.100 | ~26 | **48** |

Both are lifted, and the dark band far more than the plate — the signature of **alpha blending
against the probe's light (0.78) field**. `FaceColor()` carries the palette's alpha, and five
shipped themes declare a 95 %-opaque panel; the probe applies no `ThemePresetComponent`, so
`UiSurface.Of` falls through to its last-resort branch and every genre draws the same face
(`faceLum = 0.100` for all ten — visible in the Stage 41d print and a tell in its own right).

**Next, and it is one line:** re-render the shadow probe on an **opaque dark** field instead of
`0.78` light. If the apparent polarity flips, blending is confirmed and the fix is to composite the
plate opaquely (or measure polarity on an opaque background). If it does not flip, a later layer is
repainting the band and the layer walk is at fault.

#### Three corrections in three stages — the pattern is the point

41c: "render is inverted" — wrong, from stale pixels.
41d: "gate is wrong" — wrong, from trusting the draw-time print alone.
41e: both instruments are right; the *system between them* is not.

Each wrong conclusion came from trusting **one** source. The gate and the runtime print only became
useful when read **together**, against the same fresh render.

#### Verified unchanged

`measure_shadow` **PASS 10/10** · `measure_material` PASS · `verify_greyscale` separation + selftest
PASS · `validate_scenes` PASS · shadowing ok · build 0 errors.

### Stage 41f — two more explanations ruled out; the rim STROKE is the confound (2026-07-31)

Both remaining hypotheses tested and **both wrong**.

**Alpha blending against the field — ruled out.** Added a dark-field pass (`pol_<genre>.png`,
shadows off, opaque 0.08 ground). If blending caused it, polarity would flip. It does not:
cardgame reads 1.71 light on the light field and **1.18 light** on the dark one; platformer 1.98
and **2.53**. The band is genuinely lighter than the plate on both grounds.

**Anti-aliased edge pixel — ruled out.** Sampling inward: cardgame is `[82, 82, 82, 82, 82]`. A
uniform band five pixels deep, not a boundary blend.

#### What the data actually shows

`shooter` is the tell: `[30, 30, 108, 108, 108]` against a centre of 36. **Two pixels of dark
stroke, then the light band.** The Technical stack's outermost layer declares `rim: 0.8`, so
`DrawShape` strokes it — and that stroke is drawn *outside* the fill it belongs to.

So the outermost pixels are the layer's **rim stroke**, not its fill, and the two have independent
sources: the fill from `OutlineShade`, the stroke from `RimBrightness`. For shooter they disagree
(`OutlineShade 1.90` light fill, dark stroke); the gate samples the stroke and reports `0.79`.

The casual genres have `rim: 0` on that layer, so no stroke — yet they still read 82 against a
plate of 48 while the print says the fill is drawn at `lum 0.016`. That part is **still unexplained**
and the next probe is inside `DrawShape` itself.

#### Stopping the chase here, deliberately

Four stages have gone into this and each ruled something out honestly, but the scope is a **gate's
ability to certify** a property, not the property itself. The kit side is proven correct by runtime
instrumentation (Stage 41d): `OutlineShade` resolves per genre and the drawn colour matches the
declaration for all ten.

**Recorded for whoever picks it up**, in order of likelihood:
1. Print inside `DrawShape` — the fill colour it receives vs what it strokes.
2. `rim` and `OutlineShade` are two sources for one edge and disagree; they should be one, or the
   gate must sample the fill and skip the stroke.
3. The gate should sample at a fixed *fraction* into the band, derived from the register's own
   inset, rather than a fixed pixel offset.

**Phase B's remaining work is untouched and independent**: corner radius per widget class, plus
shear and wobble as corner modifiers. That is where the next turn should go.

#### Verified unchanged

`measure_shadow` **PASS 10/10** · `measure_material` PASS · `verify_greyscale` separation + selftest
PASS · `validate_scenes` PASS · shadowing ok · build 0 errors.

### Stage 42 — Phase B: corner radius per WIDGET CLASS (2026-07-31)

One corner number per genre cannot express the references. `rpgui.png` (art pass file 11) uses
**three different corners inside one theme** — chamfered/octagonal plaques, rounded slots, square
rivetted wood bars — and `ui1` pairs large-radius plates with **full-pill** meters. A slot, a bar
and a panel are different objects and the art treats them so.

**Built.** `KitWidgetClass { Button, Panel, Slot, Bar, Chip }` — deliberately coarse; these are the
five groupings the sheets actually treat differently, and a longer list would be per-widget
settings wearing a taxonomy's clothes. `KitGeometry.CornerFor(class)` with `CornerPanel/Slot/Bar/
Chip`, each `-1` meaning "inherit the genre's `Corner`", so nothing changes for a widget that does
not opt in. `KitControl.WidgetClass` is virtual, defaulting to `Button`.

**17 widgets declared their class** — panels (`KitPanel`, `KitCollapsiblePanel`, `KitNodeCard`,
`KitBookSpread`, `KitTooltip`, `KitPanelHanger`), slots (`KitInventorySlot`, `KitSlotGrid`,
`KitGemSlot`), bars (`KitMeter`, `KitSlider`, `KitCurrencyBar`, `KitRow`), chips (`KitChip`,
`KitStarRating`, `KitLabelValue`, `KitInputHint`).

All ten genres given per-class values from the art:

| genre | button | panel | slot | bar | source |
|---|---|---|---|---|---|
| rpg | 0.16 | 0.10 | 0.22 | **0.04** | 11 — chamfer plaque, rounded slot, square bar |
| platformer | 0.45 | 0.28 | 0.22 | **0.50** | 17 — large plates, full-pill meters |
| shooter | 0.10 | 0.04 | 0.06 | **0.02** | 14·43 — sharp throughout |
| citybuilder | 0.06 | 0.05 | 0.10 | 0.06 | 06·22 — chunky stone |

#### The gate, and it was made to fail first

`poly_probe` now asserts the classes resolve to **different** radii where the theme says they
should — because a per-class value that silently falls back to the genre default looks identical to
not having the feature at all.

Proved by removing rpg's per-class values: `corner: rpg button=0.16 panel=0.16 slot=0.16 bar=0.16
<-- ALL EQUAL`, `corner: FAIL`, **probe exit 1**. Restored: `corner: PASS`, **exit 0**.

> **I hit the `grep -c` short-circuit again while doing it.** `dotnet build | grep -c 'error CS'`
> returns 0 and *exits 1*, so the `&&` chain stopped and Godot never ran — the same trap recorded
> in this repo's own notes. The first fail-test result was therefore meaningless and was redone
> with the commands sequenced, not chained.

#### Verified

| gate | result |
|---|---|
| `poly_probe` | 105/105 polygons · **corner PASS**, shown to fail |
| `measure_shadow` | **SHADOW PASS 10/10** |
| `measure_material` | MATERIAL PASS |
| `verify_greyscale` separation | PASS |
| `validate_scenes` · shadowing · build | PASS · ok · 0 errors |
| 67 template scenes | rendered, 0 failures |

**Phase B remaining:** shear and wobble as corner modifiers, and the polarity *measurement* left
open in Stage 41f (the kit side of polarity is done and proven).

### Stage 42b — Phase B complete: shear and wobble (2026-07-31)

The last two corner modifiers the art asks for, and the kit could express neither.

**`KitGeometry.Shear`** — horizontal skew as a fraction of height. racing2 (file 08) builds its
plates from **sheared ends**: the left and right edges are angled, not vertical. That is a border
*shape*, and no amount of corner tuning produces it. Skewed about the vertical centre so the widget
stays put rather than drifting sideways. racing **0.16**, shooter **0.09** (its title bands and tabs).

**`KitGeometry.Wobble`** — per-vertex irregularity as a fraction of the short edge. The
galaxy-space kit (file 12) draws deliberately **hand-drawn** outlines where no two corners match.
Seeded from the widget's own size, exactly as `Torn` is: a wobble that reshuffles every frame reads
as noise, not as a drawn line. puzzle **0.012**, platformer **0.008**.

Both are **post-passes on the finished polygon** in `KitControl.Modify`, applied at the single place
every polygon is produced — so every silhouette gets them free, they compose (a sheared octagon, a
wobbly pill), and no widget can miss them.

#### The check, with a negative control

`poly_probe` asserts each declared modifier **actually moves the polygon** and that the result
**still triangulates**. Both failure modes are silent otherwise: a value that never reaches the
geometry does nothing, and a polygon Godot cannot triangulate draws *nothing* — which from a
screenshot is indistinguishable from "the effect is subtle".

```
mod:  racing      shear=0.16 wobble=0.000  maxMove=13.6px  tri=True  want=shear
mod:  shooter     shear=0.09 wobble=0.000  maxMove= 7.7px  tri=True  want=shear
mod:  puzzle      shear=0.00 wobble=0.012  maxMove= 2.5px  tri=True  want=wobble
mod:  platformer  shear=0.00 wobble=0.008  maxMove= 1.7px  tri=True  want=wobble
mod:  rpg         shear=0.00 wobble=0.000  maxMove= 0.0px  tri=True  want=none
mod:  PASS
```

The `rpg = none` row is a **negative control** — it would catch a modifier leaking into a genre that
should not have one, which an all-positive check cannot.

#### Verified

| gate | result |
|---|---|
| `poly_probe` | 105/105 · corner **PASS** · mod **PASS** |
| `measure_shadow` | **SHADOW PASS 10/10** |
| `measure_material` | MATERIAL PASS |
| `verify_greyscale` separation | PASS |
| `validate_scenes` · build | PASS · 0 errors |
| 67 template scenes | 0 failures, **0 `Invalid polygon`** |

Inspected: racing and shooter render with visibly angled edges, puzzle's pill carries a subtle
irregularity, rpg is untouched.

**Phase B is complete** except the polarity *measurement* left open in Stage 41f — the kit side of
polarity is done and proven by runtime instrumentation. **Next: Phase C — typography** (font family,
weight, case, tracking), which needs CC0 fonts shipped and a gate that asserts every declared family
actually resolves.

### Stage 43 — Phase C: typography (2026-07-31)

The art pass found **nine type families** across the 59 references; the kit shipped **one**. Every
genre drew in whatever the theme's default font happened to be — and that is most of what makes two
themes of one genre read differently.

**Fonts shipped**: six CC0 faces from Kenney (same licence and source as the grain patterns), 180 KB
in `addons/beep_game_builder_cs/fonts/`. `KitFontRole` → `Sans · Condensed · Rounded · Heavy ·
Pixel · Mono`, plus per-genre `UpperCase` and `Tracking`.

**Three roles have NO CC0 face and are not shipped**: `Serif` (rpg, survival storybook),
`Blackletter` (rpg gothic), `Handwritten` (the diegetic journal). rpg and survival declare `Serif`
anyway, so `KitFonts` **warns at runtime** rather than falling back in silence — because a missing
font renders *identically to having no font system at all*, which is the most invisible way this
feature can fail. `fonts/LICENSE.txt` states the gap.

#### The wiring looked done and was not

First pass wired the family into `KitChrome.DrawLabel` — which only the derive-from-Godot drop-ins
use. Every `KitControl` widget calls `GetThemeDefaultFont()` directly, so **nothing changed**: the
proof render showed four genres in identical type and `KitFonts` **never even warned** (0 warnings).

Caught because the warning count was checked, not because the render was eyeballed — at that size
four sans-serif "PLAY"s look plausible.

Fixed with one resolver, `KitControl.KitFont()`, and a sweep of **25 call sites across 24 widgets**,
so a new widget cannot miss it. After: **3 warnings fire**, and the render shows topdown in bitmap,
racing condensed with wide tracking, platformer in rounded blocks, citybuilder condensed.

**Tracking** is drawn glyph-by-glyph because Godot's `DrawString` has no letter-spacing — and only
on the two themes that ask for it, since the per-glyph path would be waste on the other eight.

#### Verified

| gate | result |
|---|---|
| `poly_probe` | 105/105 · corner **PASS** · mod **PASS** · **font PASS** |
| `measure_shadow` | **SHADOW PASS 10/10** |
| `measure_material` | MATERIAL PASS |
| `verify_greyscale` separation | PASS |
| `validate_scenes` · shadowing · build | PASS · ok · 0 errors |
| 67 template scenes | 0 failures |

#### Stated, not implied

- `KitControl.KitCase()` exists but the 25 swept sites had only the **font** swapped, not the case.
  `UpperCase` currently applies in `KitChrome.DrawLabel` only. The proof text is already "PLAY", so
  the render cannot show whether it works — **untested, and not claimed.**
- Serif/Blackletter/Handwritten remain unsatisfiable from CC0 sources. A developer supplies their
  own licensed face; the framework's job is to warn, which it now does.

**Five of eight axes are now built and gated**: material, shadow, outline polarity, corner
(per-class + shear + wobble), typography. Remaining: constructed frames (Phase D, the biggest —
`KitEdgeRun`), attachments (E), selection as a set (F), style packs (G).

### Stage 44 — Phase D started: `KitEdgeRun`, and its gate fails usefully (2026-07-31)

The biggest phase, and the one shooter actually needs. Built the model and the renderer; the gate
is red and says exactly why.

**`KitEdgeRun`** — a frame as a **run list per edge**. Each `KitEdgeSeg` carries a start, a length
(fractions of the edge), a weight multiple, and a fill: `Solid · Gap · Hatch · Ticks · Block`.
`KitEdge.Draw` walks all four edges with weight growing *inward*, so a heavy block never spills
outside the control.

`KitEdgeRun.SciFi()` encodes files 14 and 43 with the asymmetry built in — heavy block on the top
third then a break; hairline right edge with a tick run; long solid bottom with a hatch; mostly-gap
left with one short block. **Rotating it 180° does not give the same frame**, which is what those
sheets do and what a corner-ornament enum cannot express. A plain rectangle is the degenerate case
(one `Solid` per edge), so the eight genres that declare no run are untouched.

#### `measure_edgerun.py` — and it is red

It counts contiguous stroke runs just inside each edge of a rendered widget, requiring a declared
run to be **broken** (>1 run on some edge) and **asymmetric** (top≠bottom or left≠right), and a
genre with no run to show none.

```
racing    top 1  right 1  bottom 1  left 1   run    <-- NOT BROKEN
shooter   top 1  right 2  bottom 1  left 2   run    <-- SYMMETRIC
rpg       top 1  right 1  bottom 4  left 1   plain  <-- unexpected run
survival  top 1  right 1  bottom 1  left 2   plain  <-- unexpected run
EDGERUN FAIL (4)
```

**Three distinct problems, all real:**

1. **racing shows nothing.** It declares the same run as shooter but renders unbroken. racing's
   `Shear` is 0.16 against shooter's 0.09 — and `KitEdge.Draw` strokes the **axis-aligned rect**
   while the silhouette is **sheared**, so the run and the shape do not line up. The edge run must
   follow the silhouette polygon, not the rect.
2. **shooter is broken but symmetric** at this size — left and right both resolve to 2 runs, top
   and bottom both to 1. The declared asymmetry is real in the data; the scan at 300×170 does not
   resolve it. Either the segments are too fine for the widget or the scan needs sub-run weighting.
3. **The negative control mis-fires on `Spiked` and `Torn`.** rpg's bottom edge legitimately has
   4 stretches — they are *spikes*. A silhouette that is naturally discontinuous is not "an
   unexpected edge run", and the gate must distinguish a broken **stroke** from a broken **shape**.

None of these is tuned away. The gate is doing its job: it caught a renderer that ignores shear, a
declaration that does not survive to the pixels, and a flaw in its own negative control.

#### Verified (nothing regressed)

`poly_probe` 105/105 · corner PASS · mod PASS · font PASS · `measure_shadow` **PASS 10/10** ·
`measure_material` PASS · `validate_scenes` PASS · build 0 errors · 67 scenes, 0 failures.

**Next:** stroke the run along the silhouette polygon rather than the rect (fixes 1, and is the
correct model anyway); then re-check 2 with the shear fixed; then teach the negative control to
exclude genres whose *shape* is discontinuous.

### Stage 44b — Phase D complete: EDGERUN PASS (2026-07-31)

All three problems from Stage 44 resolved, and the middle one was caused by *fixing* the first.

**1. The run now follows the SILHOUETTE, not the rect.** `KitEdge.Draw` applies the same
`Modify()` the silhouette uses to the four corners and walks that quad, with `inward` recomputed
per edge as the perpendicular pointing at the centroid. racing (shear 0.16) was stroking its frame
where the widget is not; it now lands on the shape.

**2. …which immediately broke the gate.** The scan-based measurement counted marked stretches along
a fixed row just inside the widget. A sheared frame is **diagonal**, so it stops crossing the scan
line — and both declared genres regressed to `1,1,1,1` **the instant the renderer became more
correct**. A measurement that assumes axis-aligned geometry cannot certify a renderer that no
longer has it.

Rewritten to difference a run-on render against a run-off one (`KitEdge.Enabled`, the same toggle
pattern as `KitShadow.Enabled`) and count **connected components** in the difference. Immune to
shear, silhouette and shadow together, and it counts the frame's pieces directly.

**3. The negative control fixed itself.** Differencing cancels the silhouette, so `Spiked`'s points
and `Torn`'s ragged edges no longer read as "an unexpected frame" — the broken-shape/broken-stroke
confusion cannot arise. The explicit `DISCONTINUOUS` exclusion added mid-stage became unnecessary
and was dropped.

```
racing        475 frame px   14 pieces   run
shooter       973 frame px   11 pieces   run
(eight others)  0 px          0 pieces   plain
EDGERUN PASS
```

#### Verified

| gate | result |
|---|---|
| `measure_edgerun` | **EDGERUN PASS** — 14 and 11 pieces, 0 for all plain genres |
| `poly_probe` | 105/105 · corner PASS · mod PASS · font PASS |
| `measure_shadow` | **SHADOW PASS 10/10** |
| `measure_material` | MATERIAL PASS |
| `verify_greyscale` separation | PASS |
| `validate_scenes` · shadowing · build | PASS · ok · 0 errors |
| 67 template scenes | 0 failures |

**Six of eight axes built and gated**: material · shadow · outline polarity · corner (per-class,
shear, wobble) · typography · constructed frames. Remaining: attachments (E), selection as a set
(F), style packs (G) — plus the outline-polarity *measurement* still open from Stage 41f.
