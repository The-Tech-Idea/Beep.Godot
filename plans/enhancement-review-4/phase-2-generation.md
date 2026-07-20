# Phase 2 — Generation & save/load correctness

The generation pipeline (`BeepGenreGenerator`) and the save format (`GameStateData`) were never reviewed
by the ecs/-focused rounds. Both have correctness bugs that a developer can hit on the *second* generate
or the *first* reload.

## Fixes

### 1. `BeepGenreGenerator.cs` — conditional autoloads never removed on re-generation
- **`:268-284`** — `EnsureAutoload` only *adds*. The two conditional autoloads —
  `GameStateManager` (gated `EnableGameStateManager`) and `TurnManager` (gated `TimeAxis == "turns"`) —
  stay registered when you regenerate a genre where the flag is now false.
  **Worst case: `TurnManager`'s mere presence is how durational components detect the turn axis.**
  Regenerate a real-time genre over a project that was previously turn-based → the real-time game
  silently runs on the turn axis. Fix in `StampProject`: `RemoveAutoload("TurnManager")` when
  `TimeAxis != "turns"`, `RemoveAutoload("GameStateManager")` when `!EnableGameStateManager`
  (`BeepProjectDefaults.RemoveAutoload` already exists, `:30`).

### 2. `GameStateData.cs` — world-entity positions lost on save (Decision B)
- **`:388,394-414`** — `EntityStateData.Position` (a `Vector2`) does **not** survive the JSON round-trip:
  `Json.Stringify` has no `Vector2` encoding → it writes the `"(x, y)"` string form → on load
  `pos.AsVector2()` on a `String` returns `Vector2.Zero`. **Every saved world-entity position reloads at
  the origin.** `PlayerMovementStateData` (`:235-253`) already does it right — `position_x`/`position_y`
  floats. Mirror that here, and in the deprecated `PlayerStateData.Position` (`:296/309/322`).
  **Decision B: decompose to `position_x`/`position_y`** — consistent with the class that already works.
  Verify the `AsVector2`-on-String behavior against 4.7 before/after (the fix is safe either way).
- **`:105-141` (polish)** — `GameStateData` is `[GlobalClass]` Resource but has no `[Export]`, so
  `ResourceSaver.Save` to `.tres` writes empty. Add a doc note (JSON is the real path).

### 3. `BeepGameBuilderDock.cs` — Save/Reload silently drop the palette & geometry
- **`:285-300`** — the "Save to game_info.tres" button drops **Palette + Geometry**:
  `ReadFormIntoGameInfo` never reads `_palettePicker → PaletteName` nor `GeometryProfileName`, but
  `GenerateFullProject` (`:224-236`) does. **Save and Generate disagree.** Set `PaletteName` from the
  picker and `GeometryProfileName` from the genre in the Save path.
- **`:262-283`** — Reload doesn't restore the Palette dropdown: `LoadGameInfoIntoForm` re-selects
  genre/theme but never the palette from `info.PaletteName`, and the `OnThemeChanged` cascade resets it
  to `"default"` (`:188`). **Save → Reload loses a non-default palette.** `IndexOf(info.PaletteName
  .ToLower())` into `_paletteIds` and `Select` it after the cascade.

### 4. `BeepGenreGenerator.cs:70` — `"Modern"` never matches the real default
- `info.DefaultThemePreset == "Modern"` (capital M) never matches the actual default `"modern"`
  (`GameInfo.cs:54`), so the "adopt the genre's default theme" branch is **dead for the default value.**
  Fix: `string.Equals(info.DefaultThemePreset, "modern", StringComparison.OrdinalIgnoreCase)`.

### 5. Polish tail
- **`BeepGenreGenerator.cs:74-75`** — `GameScenePath` from `genre.MainScene` with no `IsSafeSceneFileName`
  guard (its siblings `CopyGenreScene:391`/`CopyGenreUiScenes:409` have it). Validate for consistency.
- **`BeepGenreGenerator.cs:340`** — `StampProject` unconditionally calls `RefreshFilesystem()`;
  `EditorInterface.Singleton` is null at runtime and `CreateProject` is public + MCP-reachable → NRE.
  Guard `Engine.IsEditorHint()`.
- **`BeepGameBuilderDock.cs:122,246`** — the "manually set main_menu.tscn as run scene" instruction is
  stale (`ApplyFromGameInfo` already writes `application/run/main_scene` + `SaveAll` persists). Soften.

## Verify
- `dotnet build` → 0 errors; `validate_scenes.sh` → PASS.
- Editor: generate a turn genre, then regenerate a real-time genre over it → confirm `TurnManager` is gone
  from `project.godot` autoloads. Save a project with a non-default palette, Reload → palette persists.
