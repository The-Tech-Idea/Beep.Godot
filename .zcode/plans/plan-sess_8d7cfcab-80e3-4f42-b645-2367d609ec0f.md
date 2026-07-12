## Complete Scene Workflow Revision

### Issues found (6 critical)

1. **PauseComponent bug**: `pause_menu.tscn` root is `CanvasLayer`, but `PauseComponent._Ready()` does `GetParent() as Godot.Control` → returns null → pause never works
2. **Theme preset values are dead enum ints** (1, 2, 6, 15, 21) — the enum was deleted; these should be string preset names like `"cartoon"`, `"scifi"` etc.
3. **GameFlow signals not wired**: `GameOver` and `LevelComplete` signals fire but nothing listens → no scene transition when you win or lose
4. **Genre sub-scenes' NavigationComponent has no genre-scene paths**: `GameInfo` only exports 4 paths (MainMenu, Game, Settings, GameOver) — no paths for level_select, character_select, etc.
5. **`map` action is ambiguous**: means "World Map" in platformer but "Level Map" in puzzle — needs routing
6. **pause_subscreen tab buttons** (inventory/map/quest/status) have no action metadata — they're local tab-switchers, not navigation (this is actually correct, just needs MenuComponent to skip them)

### Fix plan

#### A. PauseComponent — accept CanvasLayer (Fix #1)
Change `GetParent() as Godot.Control` to `GetParent() as Node` so it works with both Control and CanvasLayer roots. The visibility toggle (`_overlay.Visible`) works on both.

#### B. ThemePresetComponent — change preset ints to PresetName strings (Fix #2)
Update ALL 21 scene templates: replace `preset = N` with `preset_name = "themename"` + `genre_name = "genreid"`. Map: 1→modern/platformer, 2→cartoon/platformer, 6→military/topdown, 15→scifi/shooter, 21→candy/puzzle.

#### C. GameFlow → NavigationComponent wiring (Fix #3)
Add a `BootComponent` or connection logic to each `*_main.tscn` that connects `GameFlow.GameOver → NavigationComponent.GoToGameOver` and `GameFlow.LevelComplete → genre-specific scene`. This is the missing link between "score reaches target" and "show results screen".

Actually simpler: add `GameFlowComponent` logic to call `GetTree().ChangeSceneToFile` directly when GameOver/LevelComplete fires, reading the target path from GameInfo.

#### D. GameInfo — add genre scene paths (Fix #4)
Add exported paths for genre-specific scenes so NavigationComponent can route to them:
- `LevelSelectPath`, `LevelResultsPath` (platformer)
- `CharacterSelectPath`, `LevelUpPath`, `RunResultsPath`, `CodexPath` (shooter)
- `LevelMapPath`, `PreLevelPath`, `LevelCompletePath`, `LevelFailedPath` (puzzle)

These get set by `BeepGenreGenerator` based on which genre is generated.

#### E. NavigationComponent — add genre routing (Fix #5)
Add cases for `map`, `next`, `level_*`, `char_*`, `pick_*`, `booster_*` that route to the right genre scene via the new GameInfo paths. Actions that aren't applicable to the current genre just no-op.

#### F. pause_subscreen — add known_actions filter (Fix #6)
Set `known_actions = ["resume", "menu", "save"]` so the tab-switcher buttons (which have no action metadata) are ignored by MenuComponent and work as local UI only.

### Implementation order
1. Fix PauseComponent (accept Node, not just Control)
2. Fix all theme preset values in scene templates
3. Add genre paths to GameInfo + BeepGenreGenerator
4. Add genre routing to NavigationComponent
5. Wire GameFlow signals to scene transitions
6. Fix pause_subscreen known_actions
7. Build + verify