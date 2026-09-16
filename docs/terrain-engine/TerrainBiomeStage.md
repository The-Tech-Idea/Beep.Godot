# TerrainBiomeStage

Generation stage: an internal pipeline step that turns per-sample elevation, relief, temperature and moisture into the final terrain-kind string ("desert", "grass", "swamp", "shallow_water", ...) for every sample on the map.

`TerrainBiomeStage` runs after elevation, relief and climate and writes `world.Terrain`.
Classification checks explicit themed presets, flat beach/lake-shore rims, the
climate-off plain ground, then temperature and moisture. Relief does not force
grassland hills to gravel or mountains to rock. Raised ground keeps its biome;
separate relief geometry and rock objects express elevation and exposed stones.
Climate cooling can still produce snow/tundra. Explicit Rock/Lava and other themed
presets retain their own elevation-based ground palettes. Water is shallow beside
land and deep farther out; lakes and rivers are always shallow.

## `shallow_water`/`deep_water` is a wading classification, not a depth

`WaterKind` writes one of two kinds per water sample, and the rule is deliberately not a distance:
lake and river water is **always** `shallow_water`, whatever its size or how far a sample sits from
the bank, and sea water is `shallow_water` exactly where it touches land (`TouchesLand`) and
`deep_water` everywhere else. So the two kinds answer *can something wade here* — which is how
gameplay reads them: `GridNavigationComponent` leaves `shallow_water` wadeable at a raised cost
while the build-side `BlockedTerrainKinds` defaults block it, and nothing in the engine may treat
`deep_water` as "further from shore than `shallow_water`". A one-cell pond is shallow; the middle
of a wide river is shallow; a sea cell one tile past the beach is already deep.

**How far a cell is from the waterline is a different fact with a different owner**:
[`TerrainCoastField`](TerrainCoastField.md), whose R channel carries signed distance in tiles. That
is what every sea shades its shallows by, and since VIEW-05 (2026-09-16) it is also what the
isometric block view shelves its seabed by. A renderer that wants depth reads the field; a rule
that wants "may a unit walk into this" reads the kind.

## Public API

- `static void Apply(TerrainGenerationBuffer world, TerrainGenerationSettings settings)` — the sole entry point. Computes ocean and lake distance fields via `TerrainGeometry.DistanceTo`, converts `settings.BeachWidth`/`settings.LakeShoreWidth` from tiles to samples using `world.SamplesPerCell`, then iterates every `(x, y)` in `world` and writes `world.Terrain[index]` by calling the internal `LandKind`/`WaterKind` classifiers.

(All other members — `MoistureBands`, `Bands`, `WaterKind`, `LandKind`, `EarlyKind`, `PlainGround`, `ThemedKind`, `TouchesLand` — are `private`/internal to the class and not part of its public surface.)

## Dependencies

`internal static string? ThemedKind(TerrainPreset preset, float elevation)` is
also used by relief cleanup to distinguish a themed ground palette from a
climate-driven peak material. It returns null for non-themed presets. Classification
thresholds are unchanged; this avoids maintaining a second list of themed presets.

- Reads `TerrainGenerationBuffer.Water`, `.Land`, `.Elevation`, `.Relief`, `.Moisture`, `.Temperature`, `.Width`, `.Height`, `.Count`, `.SamplesPerCell`, `.Index(x,y)` and **writes** `TerrainGenerationBuffer.Terrain[index]` (`TerrainGenerationBuffer.cs`).
- Reads `TerrainGenerationSettings.BeachWidth`, `.LakeShoreWidth`, `.Preset`, `.UseClimateBiomeMaps` (`TerrainGenerationSettings.cs`).
- Calls `TerrainGeometry.DistanceTo(bool[], width, height)` and `TerrainGeometry.Neighbours(x, y, width, height)` (`TerrainGeometry.cs`) for the ocean/lake distance fields and the land-adjacency check in `TouchesLand`.
- Assumes `TerrainElevationStage`/relief-assigning stages and `TerrainClimateStage` have already populated `world.Elevation`, `world.Relief`, `world.Temperature` and `world.Moisture` before it runs — it only reads those fields, never computes them.
- Does not depend on `TerrainAuthoring`, `ResourceDefinition`, or `SeededTerrainPropScatterComponent`.

## Notes

- The `Bands` moisture cutoffs (0.20 / 0.38 / 0.78) are fixed constants, not settings-driven; the extensive comment on `Bands` documents that this is intentional (a quota/percentile approach was tried, measured, and reverted after it collapsed desert and dry-grass biomes to zero on real maps) — this is design history worth knowing, not a bug.
- `ResourceCategory`/relief-kind concerns from `ResourceDefinition.cs` are unrelated to this file; there is no shared logic to flag there.
- No silent-failure paths: `Apply` has no early-return branches that skip work, and every land/water cell is always assigned exactly one terrain-kind string.
