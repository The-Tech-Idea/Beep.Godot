# TerrainScaleRules

Support utility in the terrain pipeline — a pure-math, stateless class with no dependency on `TerrainWorld` or any generation stage; it only computes numbers other code (a generation-settings assembler and `TerrainScaleConstraintStage`) consumes.

`TerrainScaleRules` derives climate and biome-region-size constraints from a map's size rather than requiring them to be hand-tuned per map. It embodies two rules: a map only spans as much latitude/climate range as its height is a fraction of a full "planet" height (`WorldHeightTiles = 240`), so a small map sits in one climate band instead of sampling the whole range in a few dozen tiles; and every biome region must cover a fixed *absolute* tile count (`RegionTilesTarget = 80`) to survive, so the number of distinct biomes/features scales with map area rather than being a fixed count or a fixed share. It also holds the standalone minimum/maximum tile-count constants (`MinLakeTiles`, `MinReliefTiles`, `MinRiverTiles`, `MinFeatureTiles`, `MaxLakeShareOfLandmass`) that `TerrainScaleConstraintStage` reads directly.

## Public API

- `public const int WorldHeightTiles = 240` — the height, in tiles, that represents a map spanning an entire planet pole to pole; a map's latitude range is its own height measured as a fraction of this.
- `public const float MinLatitudeSpan = 0.12f` — the narrowest climate band a map may be given, regardless of how small its height is, so the biome table always has some range to work with.
- `public const int RegionTilesTarget = 80` — how many tiles a biome region must cover to survive, used (elsewhere, by whichever biome-coherence stage consumes `Rules.MinRegionFraction`) as an absolute target converted into a fraction of the map's expected land.
- `public const float MaxLakeShareOfLandmass = 0.30f` — the largest share of its own landmass's tile count a map's lakes (combined) may cover; read directly by `TerrainScaleConstraintStage.DrainOversizedLakes`.
- `public const int MinLakeTiles = 8`, `public const int MinReliefTiles = 6`, `public const int MinRiverTiles = 6`, `public const int MinFeatureTiles = 5` — minimum absolute tile counts below which a lake/raised-relief cluster/river/feature clump is removed rather than kept; read directly by the matching `Drain`/`Level`/`Clear`/`Thin` methods in `TerrainScaleConstraintStage`.
- `public readonly record struct Rules(float LatitudeSpan, float MinRegionFraction)` — the two derived values `For` produces for a given map.
- `public static Rules For(Vector2I bounds, float landCoverage)` — computes `LatitudeSpan` as `bounds.Y / WorldHeightTiles` clamped to `[MinLatitudeSpan, 1.0]`, and `MinRegionFraction` as `RegionTilesTarget / (bounds.X * bounds.Y * clamp(landCoverage, 0.05, 1.0))` clamped to `[0, 0.5]`. `landCoverage` is the coverage the generator was *configured* to produce, not measured output, because these rules must be known before the land itself exists.

## Dependencies

- None within `addons/beep_game_builder_cs/ecs/terrain/` — this file only uses `Godot.Vector2I`/`Godot.Mathf` and defines its own types; it does not read or write `TerrainWorld`, `TerrainGenerationSettings`, or any other terrain file.
- Consumed by: `TerrainGeneratorComponent` (calls `TerrainScaleRules.For(size, LandmassScale)` when assembling a `TerrainGenerationSettings`, only when `UseScaleRules` is on) and `TerrainScaleConstraintStage` (reads the five `Min*`/`Max*` constants directly, not through `Rules`/`For`).

## Notes

- `Rules.MinRegionFraction` (produced by `For`) is not read by `TerrainScaleConstraintStage` — that stage uses the raw `Min*Tiles`/`MaxLakeShareOfLandmass` constants directly instead of going through `Rules`. `MinRegionFraction` instead flows `TerrainScaleRules.For` → `TerrainGeneratorComponent.BuildSettings` (assigned to `settings.MinBiomeRegionFraction`) → `TerrainCoherenceStage.Apply` (reads `settings.MinBiomeRegionFraction` at line 100), a stage outside this batch. Not a dead/unread value.
- The two "rules" described at length in the class doc comment (latitude span scaling, region-size-must-earn-its-place) are only partially implemented *in this file*: `LatitudeSpan` is fully computed here, but "a biome must earn its place" is only handed off as a fraction (`MinRegionFraction`) — the actual region-grouping and dissolving logic lives in whichever stage consumes it (not `TerrainScaleConstraintStage`, which instead reads the flat `Min*Tiles` constants for a different, non-biome purpose: lakes/relief/rivers/features).
- No dead code, stubs, or TODOs found in this file.
