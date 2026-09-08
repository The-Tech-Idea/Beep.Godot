# GridInteractionModeBarComponent

HUD button bar (a `Control`, `[Tool][GlobalClass]`) that lets a player switch `GridInteractionModeComponent` between its `Select`/`Inspect`/`Tool`/`Build`/`Disabled` modes — the single UI surface for changing how map clicks are interpreted.

It supports three ways of getting its buttons, tried in order in `RebuildBar()`/`BindExistingButtons()`: an explicit parallel binding via `BoundModeNames`/`BoundButtonPaths` (string mode name + `NodePath` pairs, validated for matching array length and parsed with `TryParseMode`'s tolerant enum matching — exact `Enum.TryParse` first, then a normalized comparison stripping spaces/dashes/underscores), scene-authored buttons found by the `Mode_{mode}` naming convention, or (if `GenerateControlsWhenPathsEmpty`) a generated `HBoxContainer` of toggle buttons for whichever modes are enabled via the `Show*` exports. This is the same three-tier "authored > path-bound > generated" shape used by `GridToolPaletteComponent` for tool actions — the two files are structurally near-identical, just keyed by a different enum.

## Public API

- `[Signal] ModeButtonPressedEventHandler(int mode)` — fired after `SelectMode` successfully sets the mode; carries the mode as its underlying `int`, not the enum.
- `[Export] NodePath InteractionModePath` — the `GridInteractionModeComponent` to drive.
- `[Export] string[] BoundModeNames` / `[Export] NodePath[] BoundButtonPaths` — explicit parallel binding of mode names to button paths; must be the same length.
- `[Export] bool BuildInEditor = true`, `bool GenerateControlsWhenPathsEmpty = false`.
- `[Export] bool ShowSelect/ShowInspect/ShowTool/ShowBuild = true`, `bool ShowDisabled = false` — which modes get a button in the default (non-`BoundModeNames`) case.
- `[Export] Vector2 ButtonMinimumSize = new(88, 34)`.
- `public override void _Ready()` — resolves references, connects to `ModeChanged`, defers `RebuildBar()` per `BuildInEditor`.
- `public override void _ExitTree()` — disconnects all button handlers and the mode-changed subscription.
- `public override string[] _GetConfigurationWarnings()` — warns on missing `InteractionModePath`, mismatched `BoundModeNames`/`BoundButtonPaths` lengths, or (when generation is disabled) no authored buttons and no bound names.
- `public void RebuildBar()` — binds existing buttons if any resolve, otherwise generates a row (if allowed).
- `public bool SelectMode(GridInteractionModeComponent.InteractionMode mode)` — calls `GridInteractionModeComponent.SetMode(mode)`, refreshes toggle state, emits `ModeButtonPressed`; returns false if no interaction component resolved.
- `public string SelectedModeName()` — `_interaction?.CurrentMode.ToString() ?? ""`.
- `public int VisibleModeButtonCount()` — count of currently tracked mode buttons.
- `public void RefreshSelection()` — syncs every tracked button's pressed state (`SetPressedNoSignal`) to `_interaction.CurrentMode`.
- `public bool UsesSceneButtons()` — true if `BoundModeNames`/`BoundButtonPaths` are set or any conventionally-named mode button exists.

## Dependencies

- Reads/writes `GridInteractionModeComponent.CurrentMode`, `.SetMode(...)`, its `InteractionMode` enum, and subscribes to its `ModeChanged` signal (outside this batch) — the entire source of truth this bar reflects and drives.
- Uses `EntityComponent.FindComponent<GridInteractionModeComponent>(...)` as the scene-fallback lookup, same as every other file in this batch.
- Uses `KitChrome.SetConstantOverrideIfChanged` (outside this batch) for the generated row's spacing.
- Not established from this batch alone whether anything calls into `GridInteractionModeBarComponent` — none of the other 12 UI files reference it, though `GridBuildToolbarComponent` and `GridToolPaletteComponent` both independently call `GridInteractionModeComponent.BuildMode()`/`.ToolMode()` directly rather than through this bar, so a game using both toolbars and this mode bar has two components changing the same mode from different places.

## Notes

- `LabelFor`/`TooltipFor` hardcode short display strings per mode (`"Select"`, `"Tools"`, `"Lock"` for `Disabled`, etc.) — there is no export to override these; a scene-authored button can override the label/tooltip by pre-setting its own `Text`/`TooltipText` before `BindModeButton` runs (it only fills them in when blank).
- Architecturally this file and `GridToolPaletteComponent` are the same template (parallel-array binding + convention-name lookup + tolerant `TryParse` + `Dictionary<enum, Button>` + `ToggleMode` + `SetPressedNoSignal` sync) instantiated for two different enums (`InteractionMode` vs. `ToolAction`) rather than sharing a common generic base — the clearest duplicate-looking pair in this batch.
- `ModeButtonPressedEventHandler` and the internal `ModeChanged` subscription both carry the mode as a raw `int`; Godot signals in this codebase consistently avoid exposing enum types directly on `[Signal]` delegates.
