# SeededTerrainPropScatterComponent

Renderer / decoration component: a `Node2D` that deterministically stamps individual transparent prop sprites (grass tufts, rocks, cacti, ...) on top of an already-generated terrain, keyed by each tile's terrain kind.

This component reads terrain classification from either a live `TerrainGeneratorComponent` or a `GridCellDataComponent` (or both, in which case they must agree) and, for each tile in a rectangular area, deterministically decides whether to place a prop and which sprite/scale/rotation to use, using a hash of the tile coordinates and a seed rather than `Random`. It groups terrain kinds into five palettes (grass, desert, mud, rock, water) and only scatters into a palette if at least one sprite path is configured for it — a swamp cell never gets a cactus and a desert cell never gets grass because each palette is independently populated and matched. It deliberately takes individual sprite file paths, not sprite sheets, and refuses to place a prop unless the tile itself and its four cardinal-adjacent sample points all agree on the same palette, keeping props off kind boundaries.

## Public API

- `[Export] NodePath TerrainGeneratorPath { get; set; }` — path to a `TerrainGeneratorComponent` used as the terrain-kind/water source.
- `[Export] NodePath CellDataPath { get; set; }` — path to a `GridCellDataComponent` used as the terrain-kind source; if both this and `TerrainGeneratorPath` are set, both are consulted and must return the same palette or the tile is skipped.
- `[Export] Vector2I SizeInTiles { get; set; } = (20, 12)` — width/height of the area scanned for scatter candidates.
- `[Export(Range 1,256,1)] int TileSize { get; set; } = 64` — pixel size of one tile, used to convert tile-space positions to pixel positions.
- `[Export] int Seed { get; set; } = 31415` — deterministic seed fed into the hash function driving every random-looking decision.
- `[Export] bool GenerateOnReady { get; set; } = false` — if true, `_Ready` schedules `Rebuild()` via `CallDeferred`.
- `[Export] bool GenerateInEditor { get; set; } = false` — gates whether `GenerateOnReady` also fires when running inside the editor (`Engine.IsEditorHint()`).
- `[Export(Range 0,256,1)] int MaxProps { get; set; } = 28` — hard cap on the number of stamps placed in one `Rebuild()`.
- `[Export(Range 0,1,0.01)] float GrassCoverage/DesertCoverage/MudCoverage/RockCoverage/WaterEdgeCoverage { get; set; }` — per-palette probability (0–1) that a candidate tile in that palette receives a prop.
- `[Export(Range 0,3,0.05)] float MinimumDistanceTiles { get; set; } = 0.85f` — minimum tile-space distance enforced between any two placed stamps (0 disables the check).
- `[Export(Range 0,1,0.01)] float ScatterJitter { get; set; } = 0.70f` — how far a stamp's position is randomly offset from its tile's center, as a fraction of a tile.
- `[Export(Range 0.05,2,0.01)] float MinScale/MaxScale { get; set; }` — the range a stamp's uniform scale is randomly interpolated within.
- `[Export(File)] string GrassPrimaryPath / GrassSecondaryPath / GrassAccentPath` — up to three sprite file paths for the "grass" palette (also covers grassland, dry_grass, plains, jungle terrain kinds).
- `[Export(File)] string DesertPrimaryPath / DesertSecondaryPath / DesertAccentPath` — sprite paths for the "desert" palette (sand, desert, beach).
- `[Export(File)] string MudPrimaryPath / MudSecondaryPath / MudAccentPath` — sprite paths for the "mud" palette (mud, swamp, dirt, soil).
- `[Export(File)] string RockPrimaryPath / RockSecondaryPath / SnowAccentPath` — sprite paths for the "rock" palette (rock, stone, gravel, snow, ice, tundra).
- `[Export] bool AllowShallowWaterProps { get; set; } = false` — when true, `shallow_water` cells are eligible for the "water" palette; deep water is never eligible regardless of this flag.
- `[Export(File)] string WaterEdgePrimaryPath / WaterEdgeSecondaryPath` — sprite paths for the "water" palette.
- `void Rebuild()` — clears previously generated stamps and re-scatters props across the configured area using the current settings; public entry point called by editor tooling or gameplay code, and internally deferred from `_Ready`.
- `override void _Ready()` — resolves the generator/cell-data node references and, if `GenerateOnReady` is set (and either not in editor, or `GenerateInEditor` is also set), defers a call to `Rebuild()`.
- `override string[] _GetConfigurationWarnings()` — editor warnings: neither source path assigned, `SizeInTiles` non-positive, or no prop sprite path configured at all.

## Dependencies

- Reads `TerrainGeneratorComponent.TerrainKindAtPosition(Vector2)` and `TerrainGeneratorComponent.IsWaterAtPosition(Vector2)` (defined in `TerrainGeneratorComponent.cs`) when `TerrainGeneratorPath` is assigned.
- Reads `GridCellDataComponent.GetTerrainKind(Vector2I)` (defined in `GridCellDataComponent.cs`) when `CellDataPath` is assigned.
- Reads `TerrainLayers.ZForProps(TerrainLayers.Ground)` (`TerrainLayers.cs`) to set each stamp's `ZIndex`.
- Reads `TerrainTextures.Load(path, owner, what)` (`TerrainTextures.cs`) as the shared texture-loading helper for every configured sprite path.
- Does not touch `TerrainAuthoring`, `TerrainWorld`, `TerrainGenerationSettings`, `TerrainBiomeStage`, or `TerrainClimateStage` — it works entirely off already-generated terrain-kind queries, not the raw generation fields.

## Notes

- Generated stamps are identified purely by name prefix (`"GeneratedTerrainStamp_"`); a user-renamed stamp would survive `RemoveGeneratedStamps()` and silently accumulate on repeated `Rebuild()` calls.
- The comment on `AddTexture`/`TerrainTextures.Load` documents a real historical bug (raw `Image.LoadFromFile` failing on `res://` paths and missing mipmaps) that is now fixed by routing through `TerrainTextures.Load` — the comment is accurate to current code, not stale.
- When both `TerrainGeneratorPath` and `CellDataPath` are assigned, `PaletteAt` requires their palette keys to match exactly or the tile is skipped entirely; this is a strict "both must agree" policy with no fallback or logging when they disagree, which could silently suppress scatter over large areas if the two sources are out of sync (e.g. cell data not yet painted to match the generator).
- No z-index `[Export]`; the class comment explicitly notes this is deliberate — `TerrainLayers` owns the stack.
