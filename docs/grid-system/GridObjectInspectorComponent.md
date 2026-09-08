# GridObjectInspectorComponent

Compact HUD inspector (a `Control`, `[Tool][GlobalClass]`) for the currently selected `GridObjectComponent`: it bridges `GridSelectionComponent`'s selected cells to a normal title/details label pair, so a game can show info about selected buildings, props, resource nodes, machines, and units without writing custom inspector glue.

It resolves "which object is selected" two ways: primarily by scanning the `GridObjectComponent.ComponentGroupName` Godot group for `Selectable` objects whose footprint covers any currently-selected cell (first match wins), and — only if that group scan finds nothing — by a recursive tree walk (`CollectObjects`) rooted at `ObjectsRootPath` or the current scene. Selection can also be driven directly through `SetSelectedObject`, bypassing `GridSelectionComponent` entirely, for callers (other UI, game code) that already know the object to inspect.

## Public API

- `[Signal] ObjectInspectedEventHandler(string objectId, int x, int y)` — fired when a non-null object becomes the inspected one, via either `RebuildInspector()` or `SetSelectedObject(...)`.
- `[Signal] InspectorClearedEventHandler()` — fired when the inspected object becomes null.
- `[Export] NodePath SelectionPath/ObjectsRootPath/PanelPath/TitleLabelPath/DetailsLabelPath` — component/control wiring; `ObjectsRootPath` scopes both the group scan and the tree-walk fallback, defaulting to the current scene.
- `[Export] bool BuildInEditor = true`, `bool GenerateControlsWhenPathsEmpty = false`.
- `[Export] bool HideWhenEmpty = false` — hide the whole panel when nothing is selected.
- `[Export] bool ShowCategory/ShowCell/ShowFootprint/ShowCompletion/ShowMetadata = true` — which detail lines `TextForObject` includes.
- `[Export] string EmptyText = "No object selected"`.
- `[Export] Vector2 PanelMinimumSize = new(220, 86)`.
- `public GridObjectComponent? SelectedObject { get; private set; }` — the currently inspected object.
- `public string InspectedObjectId => SelectedObject?.ObjectId ?? ""`.
- `public override void _Ready()` — resolves references, defers `RebuildInspector()` per `BuildInEditor`, subscribes to `SelectionChanged` at runtime.
- `public override void _ExitTree()` — unsubscribes `SelectionChanged`; frees the generated panel if this component created one.
- `public override string[] _GetConfigurationWarnings()` — warns on missing `SelectionPath` or (without generation enabled) no usable title/details labels.
- `public void RebuildInspector()` — resolves the currently-selected object via the group/tree-walk lookup, updates the panel, and emits `ObjectInspected`/`InspectorCleared` accordingly.
- `public void SetSelectedObject(GridObjectComponent? gridObject)` — directly sets the inspected object (bypassing `GridSelectionComponent`), updates the panel, and emits the same signals.
- `public string TextForObject(GridObjectComponent gridObject)` — builds the multi-line details text per the `Show*` flags, including every entry in `gridObject.Metadata` when `ShowMetadata` is on.
- `public int VisibleLineCount()` — number of non-empty `\n`-split lines currently shown in the details label.
- `public bool UsesSceneControls()` — true if both a title and a details label can be found (by path, by `Panel/Content/Title`+`Panel/Content/Details`, or by convention name).

## Dependencies

- Reads `GridObjectComponent.ObjectId/.DisplayName/.Description/.Cell/.Footprint/.Complete/.EffectiveCategory/.Metadata/.Selectable` and the static `GridObjectComponent.ComponentGroupName` (outside this batch).
- Reads `GridSelectionComponent.GetSelectedCells()` and its `SelectionChanged` signal (outside this batch).
- Uses `EntityComponent.FindComponent<GridSelectionComponent>(...)` as the scene-fallback lookup, same pattern as every other file in this batch.
- Uses `KitChrome.SetConstantOverrideIfChanged`/`SetColorOverrideIfChanged` (outside this batch) for generated-control styling.
- Not established from this batch alone whether anything calls into `GridObjectInspectorComponent` — none of the other 12 UI files reference it, but its own public `SetSelectedObject` is clearly meant to be called from outside this component (e.g. from a scene's own click-handling code), unlike most other panels in this batch whose public methods are mainly self-contained refresh/query surfaces.

## Notes

- `RebuildInspector()` and `SetSelectedObject(...)` duplicate the same empty-state/populated-state body (set title/details text, `Visible`, emit the matching signal) almost verbatim rather than `RebuildInspector()` simply calling `SetSelectedObject(FindSelectedObject())` — a real duplicate-looking pattern within this single file, not just across the batch.
- `FindSelectedObject()`'s group scan takes the *first* `Selectable` object found whose footprint covers a selected cell, with no explicit tie-break (z-order, most-recently-placed, etc.) documented when multiple objects could cover the same cell.
- `EnsureUi()`'s "already built" check is `_panel != null && GodotObject.IsInstanceValid(_panel)` — for the bind-existing-controls path this depends on `BindExistingControls()` having actually located and assigned a `_panel` (via `FindPanel()`), which is optional: a scene could have `Title`/`Details` labels without any enclosing `PanelContainer` named `Panel`, in which case `_panel` stays null and `EnsureUi()` re-runs `BindExistingControls()` on every call.
