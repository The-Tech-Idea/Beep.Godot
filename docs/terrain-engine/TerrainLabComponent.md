# TerrainLabComponent

Pipeline position: **game-facing component (editor/demo UI)** — a pure UI binder that drives a `TerrainWorldComponent` from panel controls; it sits above the generation/rendering pipeline and owns none of it.

`TerrainLabComponent` connects authored setup controls to `TerrainWorldComponent` and moves the preview node containing its renderers. Setup changes generate a new world; projection changes only redraw it. The main partial binds controls and status, while `TerrainLabComponent.Navigation.cs` handles viewport navigation. Generation and terrain rules remain in the world/engine components.

## Public API

- The first design-time selector offers the four projections — **Original, Game tiles, Isometric,
  Isometric tiles** — and then one entry per art style, named by the style itself. There is no
  separate disabled art selector. A style entry selects the painted renderer and puts that style's
  resource on the world's `MapArt`; Original clears the override. Style entries are presentation
  choices, not additional `TerrainProjection` values. Every choice redraws the same live grid; none
  regenerate it.
- `[Export] Godot.Collections.Array<TerrainMapArt> StyleProfiles` — the art styles the menu lists,
  in order. One list rather than a property per style: adding a style is adding a resource to this
  array, and the menu, `SelectedView` and `ApplyView` all follow from it. The lab scene authors four
  (pixel art, cartoon, isometric, low poly). The menu length is therefore `4 + StyleProfiles.Count`,
  not a fixed six.

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
- `[Export] NodePath DiagnosticsButtonPath` / `DiagnosticsLayerPath` — the "Map diagnostics" toggle, and a CanvasItem holding the renderers that are diagnostics in this panel. In the lab that is `Preview/Diagnostics`, holding the resource icon renderer. The toggle shows or hides that layer, and turns the map overlay's start rings (`ShowStartPositions`) and survey (`ShowUndergroundResources`) on or off. The panel toggles the layer rather than the renderer because `TerrainWorldComponent` sets each renderer's own `Visible` per projection on every draw. The lab also wires `Preview/Collision` (`TerrainCollisionComponent`), so every projection builds native collision. `tests/terrain_view_parity_probe.gd` measures the lab per projection (VIEW-13).
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
- `Fill()` CLEARS a chooser and refills it, so the code that derives a list owns what is on screen. It used to add items only to an empty chooser, which made the scene a second owner of every menu's contents: `terrain_generator_lab.tscn` had six view entries typed into it, so styles added to `StyleProfiles` were simply not listed and nothing reported it — the menu looked authored and correct. `PopulateOptions()` stays idempotent, and now also picks up a changed name array.
