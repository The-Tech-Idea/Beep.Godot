# Live Theming Picker — Design Spec

**Date:** 2026-07-11
**Status:** Draft → Approved (brainstorming complete)
**Owner:** beep_game_builder_cs
**Scope:** Sub-project 1 of 3 (live theming UX → animated transitions → themed widgets)

---

## 1. Context

The skin-system refactor shipped in 2026-07 (Phases A–F in `plans/again-effect-ok-but-nested-snowflake.md`) made every genre/theme/palette/geometry/texture/background JSON-driven. `ThemePresetComponent` already has a full `ApplyTheme()` method that rebuilds the theme tree and re-stamps every themed node.

What's missing is **a way to change the skin from inside a running scene or from inside the editor** and have the change actually take effect. Today the only way to swap a theme is to edit the inspector export on a `ThemePresetComponent` (and that's only at edit time).

This spec designs that piece: a picker UI that mounts in two places (an editor dock tab and a runtime settings scene), persists the user's last pick, and triggers the existing `ApplyTheme()` path on every `ThemePresetComponent` in the scene.

Out of scope: full StyleBox-morph transitions (next brainstorm), themed widget library (third brainstorm).

---

## 2. Goals

- **Live preview in the editor dock** — pick a genre/theme/palette/geometry from a side panel; see the open scene re-theme immediately.
- **Runtime settings scene** — same picker, mounted under a `theme_settings_menu.tscn` template scene, for player-facing skin choice.
- **Persistence** — the user's last pick is written to `user://skin_profile.cfg` and restored on next launch. Per-genre history is recorded (last-used theme per genre, last-used palette per theme).
- **Hooks** — `ThemeController.ThemeChanging` / `ThemeChanged` static events let game code wire its own transitions (e.g. our next-cycle spec will use these).
- **No C# scene file** — the picker UI is a `[Tool] [GlobalClass]` partial class so it works in both mount points without a `.tscn` round-trip.

## 3. Non-goals

- Animated stylebox morphing (cycle 2).
- New themed widgets like drop-down menus or accordions (cycle 3).
- Cloud / marketplace theme distribution.
- Importing/exporting themes as a single `.tres`.

---

## 4. Architecture

### 4.1 Three pieces, two mount points

| Piece | File | Role |
|-------|------|------|
| `ThemeController` | `addons/beep_game_builder_cs/ecs/ui/ThemeController.cs` (NEW) | Static manager. Holds current selection, persists it, propagates ApplyTheme calls. |
| `ThemePickerView` | `addons/beep_game_builder_cs/ecs/ui/ThemePickerView.cs` (NEW) | `[Tool] [GlobalClass] partial VBoxContainer`. The actual UI: 4 dropdowns + a "last changed" label. |
| `ThemePickerPlugin` | `addons/beep_game_builder_cs/mcp/ThemePickerPlugin.cs` (NEW) | `EditorPlugin`. Mounts the picker as a dock tab in the editor. |
| `theme_settings_menu.tscn` | `addons/beep_game_builder_cs/templates/theme_settings_menu.tscn` (NEW) | Tiny scene: `Control` root + `ThemePickerView` child. Shipped for game authors to wire into their settings menu. |

Registration: `ThemePickerPlugin` is registered in `BeepGameBuilderPlugin.cs` alongside the existing MCP plugins.

### 4.2 Data flow

```
[User clicks dropdown in ThemePickerView]
    │
    ▼
[ThemePickerView builds SkinSelection from current dropdown values]
    │
    ▼
ThemeController.SetSelection(genre, theme, palette, geometry)   // static
    │
    ├─ Fires ThemeController.ThemeChanging event        (subscribers: user animation libs, future-cycle tween plugin)
    ├─ ApplyToAllComponents()                          // walks current scene tree, calls ApplyTheme() on each ThemePresetComponent
    ├─ Fires ThemeController.ThemeChanged event
    └─ ConfigFile.SaveToFile("user://skin_profile.cfg")
```

### 4.3 Boot path

```
GameApp._Ready / Editor load
    │
    ▼
ThemeController.LoadFromDisk()
    │
    ├─ File missing → Current = defaults; no log
    ├─ File corrupt / version mismatch → GD.PushWarning; Current = defaults; file preserved
    └─ File valid → Current = parsed values
```

The dock and the runtime scene both implicitly inherit the loaded selection by reading `ThemeController.Current` when their picker constructs.

---

## 5. Components

### 5.1 `ThemeController` (static)

```csharp
public static class ThemeController
{
    public static SkinSelection Current { get; private set; } = SkinSelection.Default;

    public static event Action<SkinSelection>? ThemeChanging;
    public static event Action<SkinSelection>? ThemeChanged;

    public static void SetSelection(string genre, string theme,
                                    string palette = "Default",
                                    string geometry = "As-Authored");

    public static void LoadFromDisk();
    public static void SaveToDisk();
    public static int  ApplyToAllComponents();

#if DEBUG
    public static void ResetForTests();
#endif
}

public readonly record struct SkinSelection(
    string Genre,
    string Theme,
    string Palette,
    string GeometryProfile)
{
    public static SkinSelection Default =>
        new("platformer", "cartoon", "Default", "As-Authored");
}
```

**Persistence file:** `user://skin_profile.cfg`

```
[skin]
genre = "platformer"
theme = "cartoon"
palette = "warm"
geometry = "As-Authored"
version = 1
```

`version` allows future schema migrations; current spec writes `version = 1`.

**`ApplyToAllComponents` semantics:** walks `Engine.GetMainLoop()` cast as `SceneTree.Root`, then recursively finds every `ThemePresetComponent` and calls its existing `ApplyTheme()`. Returns count of components updated.

### 5.2 `ThemePickerView` (UI)

```csharp
[Tool]
[GlobalClass]
public partial class ThemePickerView : VBoxContainer
{
    /// <summary>Root scene to operate on. Null = walk Engine.GetMainLoop root.</summary>
    [Export] public NodePath? TargetScenePath { get; set; }

    /// <summary>Toggle the modulate flash on theme change. Default true.</summary>
    [Export] public bool EnableChangeFlash { get; set; } = true;

    public override void _Ready() { /* build 4 OptionButton + Label "Last changed" */ }

    private void OnSelectionChanged(int idx) { /* ThemeController.SetSelection(...) */ }
}
```

**Layout:** `VBoxContainer` with:

- `Label("Genre:")` + `OptionButton(Genres)`
- `Label("Theme:")` + `OptionButton(ThemesForGenre)`
- `Label("Palette:")` + `OptionButton(PalettesForTheme)`
- `Label("Geometry:")` + `OptionButton(GeometriesForGenre)`
- `HSeparator`
- `Label("Last changed: {timestamp}")`

When `Genre` changes, the `Theme`/`Palette`/`Geometry` dropdowns repopulate from `SkinCatalog.AllGenres[genre].Themes`, `.Palettes`, `.Geometry.DisplayName`.

When any dropdown changes, `ThemeController.SetSelection(...)` fires.

**Modulate flash:** when `ThemeChanged` fires (subscribed inside `_Ready`), every `Control` under the target scene gets `Modulate = Color(1,1,1,0)` and a `CreateTween().TweenProperty(ctrl, "modulate", Color(1,1,1,1), 0.25)`. This produces a brief fade-from-black (about 0.25s) immediately after the new theme is applied, signalling to the user that the swap happened. Cheap and visible. Skipped when `EnableChangeFlash == false`.

### 5.3 `ThemePickerPlugin` (EditorPlugin)

```csharp
[Tool]
public partial class ThemePickerPlugin : EditorPlugin
{
    private ThemePickerView? _view;
    public override void _EnterTree()
    {
        _view = new ThemePickerView { Name = "ThemePicker" };
        AddControlToDock(DockSlot.RightUl, _view);
    }
    public override void _ExitTree()
    {
        if (_view != null) RemoveControlFromDocks(_view);
        _view?.QueueFree();
        _view = null;
    }
}
```

Registered in `BeepGameBuilderPlugin.cs` as another `AddToolPlugin<ThemePickerPlugin>(...)`.

### 5.4 `theme_settings_menu.tscn`

```
[gd_scene format=3 uid="uid://b1thp1ck3r"]

[node name="ThemeSettingsMenu" type="Control"]
anchor_right = 1.0
anchor_bottom = 1.0

[node name="ThemePickerView" parent="." type="VBoxContainer"]
anchor_right = 1.0
anchor_bottom = 1.0
```

Shipped as a template; game code can instantiate it directly, or wire its children into an existing settings menu.

---

## 6. Data flow details

### 6.1 Selection change

`ThemeController.SetSelection(...)` is idempotent: same selection twice → no-op (no re-ApplyTheme, no re-save, no re-fire).

When the selection differs from `Current`:

1. Save the old `Current` to `oldSelection`.
2. Fire `ThemeChanging(oldSelection)` — subscribers may inspect old values.
3. Set `Current = new`.
4. Call `ApplyToAllComponents()`.
5. Fire `ThemeChanged(new)` — subscribers (e.g. animate-flash) act on new values.
6. `SaveToDisk()`.

### 6.2 Scene walk

`ApplyToAllComponents()`:

```csharp
var tree = Engine.GetMainLoop() as SceneTree;
if (tree == null) return 0;
var root = tree.Root;
int count = 0;
CollectAndApply(root, ref count);
return count;

static void CollectAndApply(Node n, ref int count)
{
    if (n is ThemePresetComponent tp)
    {
        tp.PresetName    = Current.Theme;
        tp.GenreName     = Current.Genre;
        tp.PaletteName   = Current.Palette;
        tp.GeometryProfileName = Current.GeometryProfile;
        // (These setters already call ApplyTheme() internally per Phase A in the
        //  existing component design.)
        count++;
    }
    foreach (var child in n.GetChildren())
        CollectAndApply(child, ref count);
}
```

### 6.3 Persistence

`SaveToDisk()`:

```csharp
var cfg = new ConfigFile();
cfg.SetValue("skin", "genre", Current.Genre);
cfg.SetValue("skin", "theme", Current.Theme);
cfg.SetValue("skin", "palette", Current.Palette);
cfg.SetValue("skin", "geometry", Current.GeometryProfile);
cfg.SetValue("skin", "version", 1);
cfg.Save("user://skin_profile.cfg");
```

`LoadFromDisk()`:

```csharp
var cfg = new ConfigFile();
if (cfg.Load("user://skin_profile.cfg") != Error.Ok) return;
int version = (int)cfg.GetValue("skin", "version", 0);
if (version != 1) { GD.PushWarning(...); return; }
Current = new SkinSelection(
    (string)cfg.GetValue("skin", "genre", "platformer"),
    (string)cfg.GetValue("skin", "theme", "cartoon"),
    (string)cfg.GetValue("skin", "palette", "Default"),
    (string)cfg.GetValue("skin", "geometry", "As-Authored"));
```

---

## 7. Error handling

| Situation | Behavior |
|-----------|----------|
| `user://skin_profile.cfg` missing at boot | Silent — use `SkinSelection.Default`. |
| `skin_profile.cfg` corrupt / unreadable / wrong version | `GD.PushWarning("[ThemeController] Discarding unreadable skin_profile.cfg"); Current = Default; file preserved. |
| `SetSelection("nonexistent-genre", …)` | Push warning; do nothing. |
| `ApplyToAllComponents()` finds zero `ThemePresetComponent` | Silent no-op, returns 0. |
| `SetSelection` fires while previous ApplyTheme still in flight (shouldn't happen — synchronous) | Not possible; ApplyTheme is synchronous. |
| `ApplyTheme()` throws inside a component (existing behavior) | Existing component swallows / logs; we don't add new wrapping. |

---

## 8. Testing strategy

### 8.1 Unit tests (no scene tree)

- `ResetForTests()` → assert `Current == SkinSelection.Default`.
- Round-trip: `SetSelection("puzzle", "sea", "cool", "As-Authored")` → `SaveToDisk()` (mock user dir) → `ResetForTests()` → `LoadFromDisk()` → assert `Current` round-trips exactly.
- Corrupt-file: write garbage to `skin_profile.cfg` → `LoadFromDisk()` → assert `Current == Default`, assert warning was pushed.
- Missing-file: `LoadFromDisk()` (no prior save) → assert `Current == Default`, no warnings.
- Unknown genre in valid file: write a `skin_profile.cfg` with `genre = "does-not-exist"` → `LoadFromDisk()` succeeds (it just loads the strings; resolve happens later when ApplyTheme walks the tree).

### 8.2 Integration test (Godot scene harness)

- Build a minimal scene with two `ThemePresetComponent` nodes set to different themes.
- Boot, call `ThemeController.SetSelection("puzzle", "sea")`.
- Assert both components' `PresetName` and `GenreName` exports reflect the new selection.
- Assert `ThemeChanged` event fired exactly once with the expected `SkinSelection`.

### 8.3 Manual editor verification

- Open `platformer_main.tscn` → enable ThemePicker dock → switch to `puzzle/sea` → scene visually re-themes.
- Restart editor → confirm the puzzle/sea choice persists via `skin_profile.cfg`.
- Switch back → confirm modulate flash plays.

### 8.4 Test scope limits

- No mocking of `SceneTree` walk — covered by the integration test instead.
- No test for the modulate flash itself (visual; manual).

---

## 9. Files & changes

### 9.1 New files

| File | Lines (est.) | Purpose |
|------|--------------|---------|
| `addons/beep_game_builder_cs/ecs/ui/ThemeController.cs` | ~80 | Static manager. |
| `addons/beep_game_builder_cs/ecs/ui/ThemePickerView.cs` | ~180 | Picker UI Control. |
| `addons/beep_game_builder_cs/mcp/ThemePickerPlugin.cs` | ~30 | EditorPlugin dock host. |
| `addons/beep_game_builder_cs/templates/theme_settings_menu.tscn` | ~10 | Runtime template scene. |

### 9.2 Modified files

| File | Change |
|------|--------|
| `addons/beep_game_builder_cs/BeepGameBuilderPlugin.cs` | Add `AddToolPlugin<ThemePickerPlugin>(...)` (or equivalent registration in whichever pattern it already uses for MCP plugins). |

### 9.3 No changes to

- `ThemePresetComponent.cs` — its `ApplyTheme()` method already does everything we need; we only read/write its exports.
- `SkinCatalog.cs` — picker reads from it as-is.
- Any existing skin JSON files.

---

## 10. Open risks

1. **`ThemePresetComponent` property setters are guarded by `IsInsideTree()`** — if a `ThemePresetComponent` exists but isn't inside the tree when `ApplyToAllComponents` runs, its setter's `ApplyTheme()` call is skipped. The component re-themes on `_Ready()` next time it enters the tree, so this is acceptable but worth noting.
2. **EditorPlugin lifecycle** — `ThemePickerPlugin._EnterTree` runs every time the editor opens the project. If the plugin was already attached in the previous session, the dock may briefly flicker. Standard Godot editor plugin behavior; nothing custom needed.
3. **Static state in tests** — `ResetForTests` is `#if DEBUG`-gated. Release builds have no test reset path, which is correct.
4. **`TargetScenePath` semantics in the dock** — when the dock is up and the user has a different scene open in the editor, the walk hits the currently edited scene. We don't freeze-frame a specific scene; the dock always operates on "the current one." This matches Figma/Unity/Godot dock-tab conventions.

---

## 11. Verification (end-to-end)

1. `dotnet build Beep.Godot.sln` → 0 errors, 0 new warnings.
2. Godot → Build → Build Project → 0 errors.
3. Open project → editor gets a "Theme" dock on the right → switch genre/theme/palette/geometry → open scene visually changes.
4. Restart Godot → the last pick is restored.
5. `PackedScene.Instantiate("res://addons/beep_game_builder_cs/templates/theme_settings_menu.tscn")` in a test scene → run → picker appears and works the same way as the dock.
6. Unit + integration tests pass.

---

## 12. Deferred to next brainstorm cycles

- **Cycle 2 — Animated theme transitions.** `ThemeController.ThemeChanging` / `ThemeChanged` hooks are designed to support this. The next spec would ship a `ThemeTransitionComponent` that subscribes to those events and morphs old/new StyleBoxes per node.
- **Cycle 3 — Themed widgets library.** Higher-level components (themed drop-down, themed accordion, themed tabs with sliding indicator) composed from existing primitives + the motion hooks from cycle 2.