# TerrainGeneratorComponent

Generation-stage entry point and game-facing component: the single `[Tool][GlobalClass]` node that owns every terrain-generation setting, builds (and caches) the generated world field, writes it into gameplay's `GridCellDataComponent`, and exposes per-cell/per-position queries that every renderer and gameplay system calls instead of touching the generation internals directly.

`TerrainGeneratorComponent` is the one place a level designer or another system configures a world: roughly forty `[Export]` fields (map bounds, mode/preset/seed, landform, noise, erosion/beach, lake/river, gameplay density dials, relief fractions, climate-map and scale-rule options) are assembled each call into a `TerrainGenerationSettings` record, handed to `TerrainFieldBuilder.Build`, and the resulting `GeneratedTerrainField` is cached by settings-equality so repeated per-cell queries (which every renderer makes, potentially per pixel) don't re-run generation. `GenerateTerrain()` additionally writes the whole field into a linked `GridCellDataComponent` as the gameplay-facing grid of terrain kinds.

## Public API

- `enum LandformMode { Mainland, Island, Archipelago }` — the three landform shapes; consumed by `TerrainGenerationSettings.RequestedLandmassCount`.
- `event TerrainGeneratedEventHandler(int cellCount)` (`[Signal]`) — emitted at the end of `GenerateTerrain()` with the number of cells written.
- `[Export] NodePath CellDataPath` — path to the `GridCellDataComponent` this generator writes into; resolved lazily by `ResolveReferences()`, falling back to `EntityComponent.FindComponent<GridCellDataComponent>` on the current scene if empty.
- `[Export] bool GenerateOnReady` — if true, `_Ready()` schedules `GenerateTerrain()` via `CallDeferred`.
- `[Export] bool GenerateInEditor` — gates whether `GenerateOnReady` also fires inside the editor (`Engine.IsEditorHint()`), not just at runtime.
- `[Export] bool ClearExistingCells` — passed through to `GridCellDataComponent.LoadCells` to decide whether prior cells are wiped before loading.
- `[Export] Vector2I BoundsOrigin`, `[Export] Vector2I BoundsSize` — the map rectangle in cell coordinates; `EffectiveBoundsSize` clamps both axes to at least 1.
- `[Export] string DefaultTerrainKind` — normalized (lowercased, spaces/dashes → underscore, blank → `"grass"`) via `NormalizeKind` and pushed onto the linked `GridCellDataComponent.DefaultTerrainKind` on generate.
- `[Export] TerrainMode Mode`, `[Export] TerrainPreset Preset`, `[Export] int Seed` — top-level generation mode/climate preset/RNG seed.
- `[Export] LandformMode Landform`, `[Export] float LandmassScale` (0.05–0.92), `[Export] int ArchipelagoIslandCount` (2–12), `[Export] int TopologySamplesPerCell` (2–24) — landmass shape controls.
- `[Export] FastNoiseLite.NoiseTypeEnum NoiseType`, `[Export] FastNoiseLite.FractalTypeEnum FractalType`, `[Export] float Frequency`, `[Export] int Octaves`, `[Export] float Lacunarity`, `[Export] float Gain` — raw noise parameters fed to the underlying `FastNoiseLite`.
- `[Export] float ErosionStrength` (0–4) — how hard simulated water cuts the height field; 0 leaves noise untouched.
- `[Export] float BeachWidth` (0–4, in tiles) — width of sand where land meets open sea.
- `[Export] float FeatureFrequencyMultiplier` (0.02–4) — scales the noise frequency used for feature-biome placement.
- `[Export] float LakeCoverage` (0–0.35), `[Export] float LakeFrequencyMultiplier` (0.02–1), `[Export] float LakeShoreWidth` (0–3, tiles) — lake generation controls; `LakeShoreWidth` defaults to 0 because it was newly implemented and turning it on by default would change previously-tuned maps.
- `[Export] float RiverDensity` (0–4) — river generation density.
- `[Export] int StartPositionCount` (0–24) — number of fair player-start tiles to compute; 0 disables the layer.
- `[Export] float ResourceDensity` (0–4) — resource-node density; 0 disables the layer.
- `[Export] ResourceSet ResourceSet` — which shipped resource catalog axis to draw from.
- `[Export] ResourceCatalog? Resources` — an authored catalog overriding `ResourceSet` when non-null; shared with gameplay so both the map and the economy read the same definitions.
- `[Export] float FeatureDensity` (0–4) — terrain-feature (woods/jungle/marsh/oasis) density.
- `[Export] float HillsFraction`, `[Export] float MountainsFraction` (each 0–0.9) — target land fractions for the hills/mountains relief bands.
- `[Export] float HillshadeStrength` (0–3) — relief-shading intensity for the painted base color.
- `[Export] bool UseClimateBiomeMaps` — (declared but its doc comment above it is empty/orphaned; see Notes).
- `[Export] int BiomeCoherencePasses` (0–6) — smoothing passes that merge lone-tile biome noise into coherent regions; 0 (default) is off.
- `[Export] bool UseScaleRules` — when true, derives `ClimateLatitudeSpan`/`MinBiomeRegionFraction` from map size via `TerrainScaleRules.For` instead of using the two exported values directly.
- `[Export] float MinBiomeRegionFraction` (0–0.5) — minimum biome region size as a fraction of land; ignored when `UseScaleRules` is on.
- `[Export] int BiomeCoherenceKeep` (1–8) — how many of a cell's 8 neighbours must share its kind for the smoothing pass to keep it.
- `[Export] float OceanMarginTiles` (0–16) — guaranteed ocean ring width at the map edge.
- `[Export] float CoastlineRaggedness` (0–4) — fractal-vs-radial-falloff weight controlling how jagged coastlines are.
- `[Export] float AltitudeCooling` (0–1) — how much elevation lowers temperature.
- `[Export] float ClimateLatitudeSpan` (0.05–1), `[Export] float ClimateLatitudeCentre` (0–1) — how much of the pole-to-equator range the map covers, and where that band is centered; ignored when `UseScaleRules` is on (span only).
- `[Export] float TemperatureFrequencyMultiplier`, `[Export] float MoistureFrequencyMultiplier` (each 0.1–4) — noise-frequency scalars for the climate maps.
- `Vector2I EffectiveBoundsSize { get; }` — `BoundsSize` with both axes floored to 1.
- `override void _Ready()` — resolves references, updates configuration warnings, and (if configured) defers a `GenerateTerrain()` call.
- `override string[] _GetConfigurationWarnings()` — editor warnings when `CellDataPath` is empty or `BoundsSize` has a non-positive axis.
- `int GenerateTerrain()` — builds the field for current settings, writes every cell (position + terrain kind, flags always 0) into the linked `GridCellDataComponent` via `LoadCells`, emits `TerrainGenerated`, and returns the cell count; returns 0 and logs a warning if no `GridCellDataComponent` is resolved.
- `string TerrainKindAt(Vector2I localCell)` — terrain kind string at a cell.
- `string TerrainKindAtPosition(Vector2 localPosition)` — terrain kind string at a continuous position.
- `string WaterSourceAt(Vector2I localCell)` — the water-source classification (e.g. ocean/lake) at a cell.
- `Godot.Collections.Array<string> TerrainKindsPresent()` — the distinct terrain kinds actually present on the map, ordered first by `TerrainTileSets.Kinds` canonical order then by any leftover kinds not in that canonical list — this is the single source of truth for "which layers does this map need," replacing three separate per-renderer computations.
- `Godot.Collections.Array<int> TerrainLevelsPresent()` — the distinct `TerrainLayers` levels used by the kinds in `TerrainKindsPresent()`, ascending.
- `bool IsWaterAtPosition(Vector2 localPosition)` — true if the position is ocean or lake, asked directly rather than inferred from terrain-kind string so new water kinds can't silently become plantable ground.
- `float WaterFractionAt(Vector2 localPosition)` — 0–1 fraction of the sampled neighborhood that is water, used to fade coastlines instead of drawing a hard step.
- `float ShadeAtPosition(Vector2 localPosition)` — relief-shading multiplier (1 = unlit) for the painted base color.
- `float ShadeAtCell(Vector2I localCell)` — same shading sampled at a cell's center (`+0.5` offset).
- `int ReliefAt(Vector2I localCell)` — relief band (flat/hills/mountains) as an int, per gameplay tile.
- `float ElevationAt(Vector2I localCell)` — continuous 0–1 land height (0 = water) within a relief band, so a renderer can shape a range's crest rather than drawing it flat-topped.
- `Color BlendedColourAt(Vector2 localPosition, Func<string, Color> colourFor)` — base color interpolated across neighbouring terrain samples using a caller-supplied kind→color function, so biome boundaries aren't drawn as hard blocks.
- `int ContinentAt(Vector2I localCell)` — landmass id at a cell (0 = water); two land cells sharing an id are reachable without crossing water.
- `string ResourceAt(Vector2I localCell)` — resource kind at a cell, or empty.
- `string FeatureAt(Vector2I localCell)` — terrain feature (woods/jungle/marsh/oasis/etc.) at a cell, or empty; a feature sits on top of terrain rather than replacing it.
- `Godot.Collections.Array<Vector2I> GetStartPositions()` — the computed fair player-start cells.
- `void ApplyMapSetup(int mapType, int worldAge, int temperature, int rainfall, int seaLevel, int resources)` — the single place that turns a chosen map "shape" plus five climate axes into concrete generator settings; overwrites `Landform`, `ArchipelagoIslandCount`, `StartPositionCount`, `LandmassScale`, `HillsFraction`, `MountainsFraction`, `ClimateLatitudeCentre`, `LakeCoverage`, `RiverDensity`, `FeatureDensity`, `ResourceDensity` on this component. Takes ints (not enums) specifically so GDScript can call it.
- `Godot.Collections.Dictionary GetGenerationDiagnostics()` — the current field's `TerrainGenerationDiagnostics` as a dictionary.
- `internal GeneratedTerrainField ResolveField()` — returns the cached/rebuilt field for current settings; internal fast-path for renderers that need to sample millions of positions without re-marshalling ~40 Godot properties per call.

## Dependencies

- Reads/writes `GridCellDataComponent`: resolves it via `CellDataPath` or `EntityComponent.FindComponent<GridCellDataComponent>`, sets its `DefaultTerrainKind`, and calls `GridCellDataComponent.LoadCells(...)` in `GenerateTerrain()`.
- Constructs `TerrainGenerationSettings` (in `CurrentSettings()`) from every exported field, and reads `TerrainGenerationSettings.RequestedLandmassCount`/`TargetLandCoverage` indirectly through the settings passed onward.
- Calls `TerrainFieldBuilder.Build(TerrainGenerationSettings)` to obtain a `GeneratedTerrainField`, and calls straight through to that field's methods (`TerrainAtCell`, `TerrainAtPosition`, `WaterSourceAtCell`, `IsWaterAtPosition`, `WaterFractionAtPosition`, `ShadeAtPosition`, `ReliefAtCell`, `ElevationAtCell`, `BlendedBaseColour`, `ContinentAtCell`, `ResourceAtCell`, `FeatureAtCell`, `StartPositions`, `Diagnostics`) for every public query method.
- Reads `TerrainTileSets.Kinds` (canonical kind ordering) in `TerrainKindsPresent()`.
- Reads `TerrainLayers.LevelForKind(kind)` in `TerrainLevelsPresent()`.
- Uses `TerrainScaleRules.For(size, LandmassScale)` / constructs `TerrainScaleRules.Rules` directly, depending on `UseScaleRules`.
- `ApplyMapSetup` reads `TerrainShapePresets.Get(TerrainShape)` and several `TerrainMapSetup` static helpers (`LandScaleFor`, `ReliefScaleFor`, `LatitudeCentreFor`, `WaterScaleFor`, `ResourceScaleFor`) keyed by the `TerrainWorldAge`/`TerrainTemperature`/`TerrainRainfall`/`TerrainSeaLevel`/`TerrainResourceLevel` enums (all defined in `TerrainMapSetup.cs`/`TerrainShapePresets.cs`).
- Is read by every renderer component in this directory (`TerrainIsometricAutotileRendererComponent`, `TerrainIsometricFeatureRendererComponent`, `TerrainIsometricRendererComponent`, `TerrainPaintedRendererComponent`, `TerrainTileRendererComponent`, `TerrainResourceRendererComponent`, `TerrainFeatureRendererComponent`, `TerrainDataLayersComponent`, etc. — outside this batch) via a `NodePath` and its public query API; this file is the sole owner of generation decisions per its own doc comments.

## Notes

- `UseClimateBiomeMaps` (line 112) itself has no XML doc comment directly above it. Two `/// <summary>` blocks follow it instead (lines 114-123, describing quota-based rainfall-biome assignment, and 125-133, describing smoothing passes) with nothing but blank lines between them and `BiomeCoherencePasses` (line 134) — C# attaches contiguous doc-comment trivia to the next declaration, so both blocks become `BiomeCoherencePasses`'s doc comment, and the quota-assignment text (clearly meant for `UseClimateBiomeMaps`) is misattached. `UseClimateBiomeMaps` reads as undocumented in the emitted API; its actual behavior must be checked in `TerrainBiomeStage`/`TerrainClimateStage`.
- Same pattern, more severe, at lines 169-174: three `/// <summary>` blocks in a row ("Driest fraction of the land that becomes desert", "The next-driest fraction, which becomes dry grassland", "Wettest fraction of the land that becomes swamp") precede no property declaration at all — the exports they once documented are gone from this file, and the three comments are dead documentation floating ahead of `OceanMarginTiles`'s own doc block.
- The class-level comment on `ApplyMapSetup`/its second summary explicitly documents an "accepted-then-ignored" trap: eleven properties (`Landform`, `ArchipelagoIslandCount`, `StartPositionCount`, `LandmassScale`, `HillsFraction`, `MountainsFraction`, `ClimateLatitudeCentre`, `LakeCoverage`, `RiverDensity`, `FeatureDensity`, `ResourceDensity`) are silently overwritten by `ApplyMapSetup` every time `TerrainWorldComponent.Build` calls it — a value typed into the Inspector for any of them is discarded whenever that pairing is in use. This is intentional per the comment (single-owner-per-fact design) but is a real footgun for anyone editing the Inspector without knowing which owner is active.
- `TerrainKindsPresent()`/`TerrainLevelsPresent()` replace what the doc comment says used to be three separate, independently-drifting implementations (tile view, isometric autotile view, data layers) — worth checking that no renderer in this addon still keeps its own hard-coded kind list instead of calling these.
- `CurrentSettings()` rebuilds a ~40-field settings record (with full Mathf clamps) on every single public query call; `ResolveField()` exists specifically so hot-path callers (renderers) can hold one field reference instead of paying that cost per pixel — callers that instead call the public per-cell/per-position methods repeatedly (rather than caching `ResolveField()`) still pay the settings-rebuild cost each time, since `FieldFor` only skips rebuilding the `GeneratedTerrainField` itself, not the settings record used to look it up.
