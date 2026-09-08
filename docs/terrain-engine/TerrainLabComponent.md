# TerrainLabComponent

Pipeline position: **game-facing component (editor/demo UI)** — a pure UI binder that drives a `TerrainWorldComponent` from panel controls; it sits above the generation/rendering pipeline and owns none of it.

`TerrainLabComponent` connects authored setup controls to `TerrainWorldComponent` and moves the preview node containing its renderers. Setup changes generate a new world; projection changes only redraw it. The main partial binds controls and status, while `TerrainLabComponent.Navigation.cs` handles viewport navigation. Generation and terrain rules remain in the world/engine components.

## Public API

- The first design-time selector now offers **Original, Game tiles, Isometric,
  Isometric tiles, Pixel Art, Cartoon**. There is no separate disabled art selector.
  Pixel Art and Cartoon select the painted renderer and their respective
  `PixelArtProfile` / `CartoonProfile` resources. Original clears the override.
  Entries 4 and 5 are presentation choices, not additional `TerrainProjection` values.
  All six choices redraw the same live grid; none regenerate it.

- `ZoomPreviewAt(screenPosition, factor)` keeps the preview-local point under a viewport position
  fixed using `GetGlobalTransformWithCanvas`. Nonfinite/nonpositive factors are ignored.
- `PanPreviewBy(screenDelta)` converts viewport-pixel displacement into the preview parent's space,
  so rotated/scaled parents do not distort the drag distance.
- Fit uses transformed screen bounds. World-built events refit changed extents; regenerating the
  same extent preserves the user's view. Projection selection calls `Redraw`, not `NewWorld`, so
  changing view does not regenerate live cell state. Setup-axis changes call `NewWorld`.

- `[Export] NodePath WorldPath` — the `TerrainWorldComponent` this panel drives.
- `[Export] NodePath PreviewPath` — the `Node2D` holding the renderers, panned/zoomed as one unit by the Navigation partial.
- `[Export] NodePath MapTypePath/MapSizePath/WorldAgePath/TemperaturePath/RainfallPath/SeaLevelPath/ResourceLevelPath/ResourceSetPath/SeedPath/ViewPath` — paths to the setup `OptionButton`s/`SpinBox` bound to the matching `TerrainWorldComponent` properties.
- `[Export] NodePath GenerateButtonPath/RandomSeedButtonPath/ResetViewButtonPath/StatusPath` — action buttons and the status `Label`.
- `MinimumZoom = 0.04f`, `MaximumZoom = 3.0f`, `ZoomStep = 1.15f`: preview navigation limits.
- `Generate()` copies bound setup controls to the world, preserving current values for unbound controls, then calls `NewWorld()`. World-built signals refresh status and extent-dependent framing.
- `_Ready()` resolves authored controls, populates choices, connects actions and viewport resize. An already-built world is displayed directly; a world with automatic startup disabled is generated deferred. Editor instances do not run this workflow.
- `override string[] _GetConfigurationWarnings()` — editor warning when `WorldPath` or `PreviewPath` is unset.

## Dependencies

- Reads/writes the world's setup axes and seed; calls `NewWorld()`, `Redraw()`, `PreviewExtent()` and `StatusLine()`.
- Setup choices use `TerrainShapePresets` and `TerrainMapSetup` names and enums.
  The combined view/style menu is mapped explicitly by `ApplyView`.
- The `.Navigation.cs` partial (same class) reads `_world.PreviewExtent()` (defined in `TerrainWorldComponent.Drawing.cs`) to frame the preview, and manipulates the `_preview` `Node2D` this file resolves.

## Notes

- A large comment block (lines 51-57) documents that relief, rivers, resource density, lake size, beach width, frequency, octaves, landform, and raw width/height exports were deliberately removed because they were "resolved and never read" — including three `CheckButton`s that looked wired but whose values were hardcoded elsewhere. This is a documented instance of the "accepted-but-ignored" pattern being fixed, not a live issue in this file.
- `Selected()` falls back to the world's *current* value (not a hardcoded default) when a control path is unresolved, so a partially-wired panel degrades to "leave that axis alone" rather than silently resetting it.
- `Fill()` guards on `option.ItemCount > 0`, so `PopulateOptions()` is idempotent — safe to call from `_Ready()` even if scene reload runs it more than once — but also means a chooser's option list is never refreshed if the underlying `TerrainMapSetup`/`TerrainShapePresets` name arrays change after the first population (not a concern within one session).
