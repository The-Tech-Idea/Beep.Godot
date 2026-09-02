# TerrainMapSetup

Support utility: a static, stateless helper consumed by the generation stage and the map-setup UI to translate named "strategy game" map-setup axes into the raw multipliers the generation pipeline actually uses.

`TerrainMapSetup` defines the enums and display-name arrays for the independent map-setup axes (map type/size, world age, temperature, rainfall, sea level, resource level, resource set, projection) and the pure functions that turn a chosen enum value into a scale multiplier or a `Vector2I` bounds. It exists so the six axes can be chosen independently (a Civilization-V-style "advanced setup" factorisation) rather than as combined presets that couple geography to climate. It holds no state and touches no other terrain file — it is pure enum-to-number/string mapping.

## Public API

- `enum TerrainWorldAge { Young, Mature, Old }` — relief axis; young = little erosion (mountainous), old = worn down.
- `enum TerrainTemperature { Cold, Temperate, Hot }` — latitude-window axis.
- `enum TerrainRainfall { Arid, Normal, Wet }` — water/vegetation axis, independent of temperature.
- `enum TerrainSeaLevel { Low, Normal, High }` — land/water ratio trim.
- `enum TerrainResourceLevel { Sparse, Normal, Abundant }` — resource density axis.
- `enum TerrainMapSize { Tiny, Small, Standard, Large, Huge }` — named map dimensions.
- `static readonly string[] WorldAgeNames`, `TemperatureNames`, `RainfallNames`, `SeaLevelNames`, `ResourceLevelNames`, `MapSizeNames` — display labels for each axis's enum, indexed by the enum's own integer value so a UI dropdown's selected index doubles as the enum value with no separate lookup table.
- `static readonly string[] ResourceSetNames` — display labels for `ResourceSet` (defined in `TerrainResourceStage.cs`), in that enum's own declaration order (`Historical`, `Oil and gas`, `Space exploration`).
- `static readonly string[] ProjectionNames` — display labels for `TerrainProjection` (defined in `TerrainWorldComponent.cs`), in that enum's own declaration order (`Painted`, `Game tiles`, `Isometric`, `Isometric tiles`).
- `static Vector2I BoundsFor(TerrainMapSize size)` — Tiny→32x32, Small→48x48, Standard/default→64x64, Large→96x60, Huge→128x80.
- `static float ReliefScaleFor(TerrainWorldAge age)` — Young→2.10, Old→0.35, Mature/default→1.0; multiplies relief (hills/mountains) intensity.
- `static float LatitudeCentreFor(TerrainTemperature temperature)` — Cold→0.78, Hot→0.22, Temperate/default→0.52; a latitude-window position (0=pole, presumably 1=equator or vice versa per the comment) fed to climate generation.
- `static float LandScaleFor(TerrainSeaLevel level)` — Low→1.12, High→0.88, Normal/default→1.0; small trim (±12%) on land coverage, deliberately narrow per the code comment (a wider swing was found to overpower the map-type shape it's meant to modify).
- `static float WaterScaleFor(TerrainRainfall rainfall)` — Arid→0.30, Wet→1.90, Normal/default→1.0; scales lakes/rivers/vegetation together.
- `static float ResourceScaleFor(TerrainResourceLevel level)` — Sparse→0.45, Abundant→1.90, Normal/default→1.0.

## Dependencies

None within `addons/beep_game_builder_cs/ecs/terrain/` — the file only uses `Godot.Vector2I`/`Mathf` and its own enums. It is consumed by, but does not itself read from, `TerrainGeneratorComponent.cs` (calls `LandScaleFor`, `ReliefScaleFor`, `LatitudeCentreFor`, `WaterScaleFor`, `ResourceScaleFor`), `TerrainWorldComponent.cs` and `TerrainWorldComponent.Drawing.cs` (`BoundsFor`), and `TerrainLabComponent.cs` (all the `*Names` arrays, to populate dropdowns).

## Notes

- `ResourceSetNames` and `ProjectionNames` document that their order must track `ResourceSet` (in `TerrainResourceStage.cs`) and `TerrainProjection` (in `TerrainWorldComponent.cs`) respectively "in that enum's own declaration order" — verified: both enums' declared order (`Historical, OilAndGas, SpaceExploration` and `Painted, Tiles, Isometric, IsometricAutotile`) matches the string arrays here. This is an implicit cross-file contract (index-as-value) with no compile-time enforcement — if either enum gains, reorders, or removes a member without updating the matching array here, a UI dropdown will silently mislabel or drop a value.
- All public members are exercised by other files in this batch's directory (`TerrainGeneratorComponent`, `TerrainWorldComponent`, `TerrainWorldComponent.Drawing`, `TerrainLabComponent`) — nothing here is dead code.
- The extensive doc comment at the top explaining the Civilization V axis rationale is design rationale, not a description of code behaviour that could go stale; it reads as accurate to the enums/functions beneath it.
