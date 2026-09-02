# TerrainWorldComponent

Game-facing component — "THE map/world creation component": the single node a scene drops in to generate a world (via a `TerrainGeneratorComponent`) and draw it (via the renderer components handled in its `.Drawing.cs` partial).

Per its header comment, this replaces logic that used to live welded into the lab's scene controller; every demo re-implemented the same three steps (generate, rebuild renderers, report) as its own hundred-line controller, and the copies drifted (one reported continents, another landmasses; one framed the camera by scaling the world, another by moving a camera). This component owns none of what the generation axes *mean* (that's `TerrainMapSetup`) or how they reach the generator (`TerrainGeneratorComponent.ApplyMapSetup`) — it only carries the scene's choice of axes and renderers and drives `Build()`.

## Public API

- `[Signal] WorldBuiltEventHandler WorldBuilt(Vector2I size)` — emitted at the end of `Build()`.
- `NodePath GeneratorPath` `[Export]` — the `TerrainGeneratorComponent` driven by `Build()`.
- `NodePath PaintedRendererPath`, `TileRendererPath`, `IsometricRendererPath`, `IsometricAutotileRendererPath`, `FeaturesPath`, `IsometricFeaturesPath`, `MapOverlayPath`, `ReliefRendererPath`, `ResourceRendererPath`, `DataLayersPath` `[Export]` — optional paths to each renderer/data layer; an unset one is simply skipped by `Draw()`, not treated as an error.
- `TerrainShape MapType`, `TerrainMapSize MapSize`, `TerrainWorldAge WorldAge`, `TerrainTemperature Temperature`, `TerrainRainfall Rainfall`, `TerrainSeaLevel SeaLevel`, `TerrainResourceLevel ResourceLevel`, `ResourceSet Resources`, `int Seed` (default `31415`) `[Export]` — the world's generation axes, forwarded to the generator verbatim in `Build()`.
- `TerrainProjection Projection` `[Export]` (default `Painted`) — which renderer set `Draw()` shows/rebuilds.
- `bool BuildOnReady` `[Export]` (default `true`) — when true and not running in the editor, `_Ready()` defers a call to `Build()`.
- `Callable GenerateMap` `[ExportToolButton("Generate map")]` — editor-only button wrapping `Callable.From(Build)`; per its doc comment this is deliberately the *only* way a map gets (re)built in the editor — `BuildOnReady` is intentionally not honoured there, so opening a scene never silently overwrites a hand-authored map.
- `Vector2I BuiltSize { get; private set; }` — size (tiles) of the last world `Build()` produced; used by `PreviewExtent()` (Drawing.cs) as a fallback before any build has run.
- `_Ready()` — defers `Build()` when `BuildOnReady` is set and this is not the editor.
- `_GetConfigurationWarnings()` — warns only when `GeneratorPath` is empty; none of the ten renderer/data-layer paths are checked.
- `void Build()` — resolves nodes; `GD.PushWarning`s and returns if there's no generator; otherwise computes `size` from `TerrainMapSetup.BoundsFor(MapSize)`, stores it as `BuiltSize`, pushes `BoundsSize`/`Seed`/`ApplyMapSetup(MapType, WorldAge, Temperature, Rainfall, SeaLevel, ResourceLevel)`/`ResourceSet` onto the generator, forces `UseClimateBiomeMaps`/`UseScaleRules` on, calls `_generator.GenerateTerrain()`, calls the `.Drawing.cs` partial's `Draw(size)`, then emits `WorldBuilt`.
- `Godot.Collections.Dictionary Diagnostics()` — resolves and returns `_generator.GetGenerationDiagnostics()`, or an empty `Dictionary` if there is no generator.
- `string StatusLine()` — returns `""` if there's no generator; otherwise formats one line (built size, landform name, land/ocean/lake coverage, river coverage, landmass count vs. requested, resource count, start count, generation time) sourced entirely from `_generator.GetGenerationDiagnostics()` and `_generator.Landform`.
- `LandformName(TerrainGeneratorComponent.LandformMode)` *(private, static)* — maps `Island`/`Archipelago`/anything else to `"island"`/`"archipelago"`/`"mainland"`.
- `Resolve()` *(private)* — lazily resolves every renderer/generator field from its NodePath; `_generator` re-resolves if the cached reference becomes invalid, every renderer field uses a bare `??=` and is never re-validated afterward.

## Dependencies

- Drives `TerrainGeneratorComponent` — `BoundsSize`, `Seed`, `ApplyMapSetup(...)`, `ResourceSet`, `UseClimateBiomeMaps`, `UseScaleRules`, `GenerateTerrain()`, `GetGenerationDiagnostics()`, `GetStartPositions()` (used from the Drawing.cs partial), `Landform`/`LandformMode` — all defined in `TerrainGeneratorComponent.cs`.
- Reads `TerrainMapSetup.BoundsFor(MapSize)` from `TerrainMapSetup.cs`, which is also where `TerrainShape`, `TerrainMapSize`, `TerrainWorldAge`, `TerrainTemperature`, `TerrainRainfall`, `TerrainSeaLevel`, `TerrainResourceLevel` are declared.
- `ResourceSet` (the `Resources` export's type) is declared in `TerrainResourceStage.cs`.
- Resolves node references that `TerrainWorldComponent.Drawing.cs` then reads/writes: `TerrainPaintedRendererComponent`, `TerrainTileRendererComponent`, `TerrainIsometricRendererComponent`, `TerrainIsometricAutotileRendererComponent`, `TerrainFeatureRendererComponent`, `TerrainIsometricFeatureRendererComponent`, `TerrainMapOverlayComponent`, `TerrainReliefRendererComponent`, `TerrainResourceRendererComponent`, `TerrainDataLayersComponent`.
- Is itself read by `TerrainWorldCameraComponent.cs` (`PreviewExtent`/`StartPositionView`/`WorldBuilt`) and `TerrainWorldStatusComponent.cs` (`StatusLine`/`WorldBuilt`).
- Split across two files: this file plus `TerrainWorldComponent.Drawing.cs` (the `Draw`/`StartPositionView`/`PreviewExtent` partial).

## Notes

- `_GetConfigurationWarnings()` checks only `GeneratorPath`; none of the ten renderer/data-layer `NodePath`s are validated. This matches the documented "each renderer is optional" design for the *drawing* projections, but it also means a scene that forgot to wire `DataLayersPath` gets no editor warning even though `Draw()` rebuilds the data layers unconditionally for gameplay cell queries to work.
- Every field `Resolve()` sets via `??=` (`_painted`, `_tiles`, `_iso`, `_isometricAutotile`, `_features`, `_isometricFeatures`, `_overlay`, `_relief`, `_resources`, `_dataLayers`, `_paintedNode`, `_overlayNode`) is never re-checked with `GodotObject.IsInstanceValid` on later calls, unlike `_generator` — mirrors the same asymmetry flagged in `TerrainWorldCameraComponent`'s `_camera` field.
- `BuildOnReady`'s "skipped in the editor" behaviour is both documented (in two separate doc comments) and actually implemented that way (`_Ready()` checks `!Engine.IsEditorHint()`) — comment and code agree; noted only because it's the kind of setting that could easily have drifted into "accepted but ignored" and didn't.
- `StatusLine()`'s doc comment cites the historical "one demo reported continents, another landmasses" bug as its motivation, and the code genuinely centralises the fix (a single `string.Format` sourced only from generator diagnostics) — comment matches current behaviour.
