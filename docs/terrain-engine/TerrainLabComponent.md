# TerrainLabComponent

Pipeline position: **game-facing component (editor/demo UI)** — a pure UI binder that drives a `TerrainWorldComponent` from panel controls; it sits above the generation/rendering pipeline and owns none of it.

`TerrainLabComponent` is a `Node` (`[Tool]`, `[GlobalClass]`) that connects a set of `OptionButton`/`SpinBox`/`Button`/`Label` controls to a `TerrainWorldComponent`'s exported map-setup axes (map type, size, world age, temperature, rainfall, sea level, resource level/set, seed, projection) and to a preview `Node2D` that the world's renderers live under. On any control change it copies the selection onto the world, calls `world.Build()`, and updates a status label. It knows nothing about generation or rendering internals — that split is deliberate and stated in the class comment. The file is split by concern: this file resolves/populates controls and handles generate/status; `TerrainLabComponent.Navigation.cs` (a partial class) handles pan/zoom/fit-to-view of the preview.

## Public API

- `[Export] NodePath WorldPath` — the `TerrainWorldComponent` this panel drives.
- `[Export] NodePath PreviewPath` — the `Node2D` holding the renderers, panned/zoomed as one unit by the Navigation partial.
- `[Export] NodePath MapTypePath/MapSizePath/WorldAgePath/TemperaturePath/RainfallPath/SeaLevelPath/ResourceLevelPath/ResourceSetPath/SeedPath/ViewPath` — paths to the setup `OptionButton`s/`SpinBox` bound to the matching `TerrainWorldComponent` properties.
- `[Export] NodePath GenerateButtonPath/RandomSeedButtonPath/ResetViewButtonPath/StatusPath` — action buttons and the status `Label`.
- `[Export] float MinimumZoom = 0.04f`, `[Export] float MaximumZoom = 3.0f`, `[Export] float ZoomStep = 1.15f` — preview navigation limits, consumed by the Navigation partial's `ZoomAt`/`ResetPreviewView`.
- `void Generate()` — copies every bound control's current selection onto the corresponding `TerrainWorldComponent` property (falling back to the world's existing value if a control is unbound), calls `world.Build()`, sets the status label to `world.StatusLine()`, and calls `ResetPreviewView()` only on the very first build (detected via `_preview.Scale == Vector2.One`).
- `override void _Ready()` — no-ops in the editor; otherwise resolves nodes, populates chooser options, wires `GetViewport().SizeChanged` to `ResetPreviewView`, wires the Generate/RandomSeed/ResetView buttons and every setup `OptionButton`'s `ItemSelected` to `Generate()` (the view chooser additionally calls `ResetPreviewView()`, since projections have different footprints/origins), then calls `Generate()` deferred.
- `override string[] _GetConfigurationWarnings()` — editor warning when `WorldPath` or `PreviewPath` is unset.

## Dependencies

- Reads/writes `TerrainWorldComponent.MapType`, `.MapSize`, `.WorldAge`, `.Temperature`, `.Rainfall`, `.SeaLevel`, `.ResourceLevel`, `.Resources`, `.Projection`, `.Seed`; calls `.Build()` and `.StatusLine()`.
- Populates its choosers from `TerrainShapePresets.DisplayNames()` and `TerrainMapSetup.MapSizeNames/WorldAgeNames/TemperatureNames/RainfallNames/SeaLevelNames/ResourceLevelNames/ResourceSetNames/ProjectionNames`, and casts selections back to `TerrainShape`, `TerrainMapSize`, `TerrainWorldAge`, `TerrainTemperature`, `TerrainRainfall`, `TerrainSeaLevel`, `TerrainResourceLevel`, `ResourceSet`, `TerrainProjection` (all defined alongside `TerrainWorldComponent`/`TerrainMapSetup`).
- The `.Navigation.cs` partial (same class) reads `_world.PreviewExtent()` (defined in `TerrainWorldComponent.Drawing.cs`) to frame the preview, and manipulates the `_preview` `Node2D` this file resolves.

## Notes

- A large comment block (lines 51-57) documents that relief, rivers, resource density, lake size, beach width, frequency, octaves, landform, and raw width/height exports were deliberately removed because they were "resolved and never read" — including three `CheckButton`s that looked wired but whose values were hardcoded elsewhere. This is a documented instance of the "accepted-but-ignored" pattern being fixed, not a live issue in this file.
- `Selected()` falls back to the world's *current* value (not a hardcoded default) when a control path is unresolved, so a partially-wired panel degrades to "leave that axis alone" rather than silently resetting it.
- `Fill()` guards on `option.ItemCount > 0`, so `PopulateOptions()` is idempotent — safe to call from `_Ready()` even if scene reload runs it more than once — but also means a chooser's option list is never refreshed if the underlying `TerrainMapSetup`/`TerrainShapePresets` name arrays change after the first population (not a concern within one session).
