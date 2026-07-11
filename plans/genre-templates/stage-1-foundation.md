# Stage 1 — Foundation (GameInfo + Orchestrator)

## Deliverables

### `core/GameInfo.cs` (new)
- `[Tool] [GlobalClass] public partial class GameInfo : Resource`
- Identity: `GameName` (string, default "My Game"), `Version` (string, "0.1.0"),
  `Developer` (string), `Genre` (enum `GameGenre { Platformer, TopDown, Shooter, Puzzle }`),
  `Description` (string).
- Display: `TargetResolution` (Vector2i, 1280×720), `PixelArt` (bool, true),
  `TargetFps` (int, 60).
- Theme: `DefaultThemePreset` (`ThemePresetType`, default Modern).
- Scene paths: `MainMenuPath`, `GameScenePath`, `SettingsScenePath`, `GameOverScenePath`
  (all strings, `res://` paths).
- Genre tuning: `Gravity` (float, 980), `JumpVelocity` (float, -400), `MoveSpeed`
  (float, 200), `FireRate` (float, 0.2), `GridSize` (Vector2i, 8×8),
  `TargetScore` (int, 1000).
- Static `Instance` accessor reading the autoload: `static GameInfo Instance =>
  Engine.GetMainLoop() is SceneTree t && t.Root.GetNodeOrNull<GameInfo>("/root/GameInfo")
  is { } gi ? gi : null;`
- `[Export]` on every field for inspector editing.

### `core/BeepGenreGenerator.cs` (new, C# orchestrator)
- `namespace Beep.GameBuilder` (matches core/ convention).
- Public API:
  - `static List<string> StampProject(GameInfo info, bool overwrite)` — full pipeline.
  - `static List<string> CreatePlatformerProject(GameInfo info, bool overwrite)`
  - `static List<string> CreateTopDownProject(GameInfo info, bool overwrite)`
  - `static List<string> CreateShooterProject(GameInfo info, bool overwrite)`
  - `static List<string> CreatePuzzleProject(GameInfo info, bool overwrite)`
- Shared private helpers:
  - `CopySceneTemplate(string templateName, string targetPath, bool overwrite)` —
    load from `templates/scenes/`, save via ResourceSaver (reuse BeepSceneGenerator pattern).
  - `CopyScriptTemplate(string templateName, string targetPath, bool overwrite)` —
    read `.gd.template`, strip `.template`, write `.gd` with token substitution.
  - `EnsureAutoload(string name, string path)` — delegate to BeepProjectDefaults.
  - `WriteGameInfoTres(GameInfo info)` — ResourceSaver.Save to `res://game_info.tres`.
  - `ApplySettingsFromInfo(GameInfo info)` — window size, title, main scene, stretch.

### `core/BeepProjectDefaults.cs` (extend)
- New `ApplyFromGameInfo(GameInfo info)`:
  - window size = info.TargetResolution
  - `application/config/name` = info.GameName
  - `application/config/version` = info.Version
  - main scene = info.MainMenuPath (menu is the boot scene)
  - texture filter = nearest if info.PixelArt
- Keep existing `ConfigureDefaults()` as the fallback.

### `catalogs/genre_templates.json` (new)
Manifest mapping each genre → its scene/script list + default theme + tuning defaults.
Consumed by `BeepGenreGenerator` so adding a genre is a manifest edit.

```json
{
  "platformer": { "theme": "Cartoon", "main_scene": "platformer_main.tscn",
    "scenes": [...], "scripts": [...], "tuning": { "gravity": 980, "jump": -400 } },
  "topdown":    { "theme": "Fantasy", "main_scene": "topdown_main.tscn", ... },
  "shooter":    { "theme": "SciFi",   "main_scene": "shooter_main.tscn", ... },
  "puzzle":     { "theme": "Candy",   "main_scene": "puzzle_main.tscn", ... }
}
```

## Acceptance criteria
- [ ] `GameInfo.cs` compiles and is visible in the Create-Node menu (GlobalClass).
- [ ] `BeepGenreGenerator` compiles; the 4 genre methods exist and call shared helpers.
- [ ] `BeepGenreGenerator` ensures `beep_ui` is enabled (required dependency for templates).
- [ ] `genre_templates.json` parses (validated via BeepFileUtils.LoadJson).
- [ ] `dotnet build` succeeds with 0 errors.

## Theming note (corrected)
Scene templates theme via the **`beep_ui` GDScript addon** (`BeepThemeApplier` +
`BeepWidgetFactory`), NOT the C# `ThemePresetComponent`. Rationale: the GDScript
applier has the container-drift bug fixed (4.7 offset transforms), 84 ready themed
widgets, and 11 UI effects — none of which the C# component offers. Templates
require `beep_ui` enabled. `GameInfo` stays C# (autoload) and GDScript reads it
transparently via `/root/GameInfo`.
