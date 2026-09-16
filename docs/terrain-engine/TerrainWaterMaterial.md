# TerrainWaterMaterial

Renderer-support utility (`internal static class`): the C# half of
`shaders/water_common.gdshaderinc` — the one place C# writes the shared water uniforms (DUP-02).

The include exists because two shaders draw water and composite it differently — the painted view
mixes the sea into its own opaque ground pass, the tile and isometric views float a transparent
surface over real seabed geometry — while the water itself has to be identical, or one map grows
two oceans. That contract had been shared on the shader side since the include was written and
not on this side: three renderers set the same uniform names by hand and had drifted. Counted
before this file existed, of the 22 shared uniforms C# writes the tile view wrote 13 — it was
missing every dial that controls the authored foam sheet, both swell dials and both
texture-repeat dials, while still setting `use_foam_sheet` from its own foam path.

Since VIEW-04 (2026-09-16) the *values* come from one [`TerrainWaterLook`](TerrainWaterLook.md)
per world, so there is nowhere left for a view to keep its own number either.

## Public API

- `readonly record struct Settings(Vector2I Size, Vector2I Origin, float CoastRange, float GroundTextureTiles, float WaterTextureTiles, float WaveIntensity, float FoamStrength, float ShallowTiles, float DeepTiles, float FoamTilesAlong, float FoamTilesAcross, float FoamScroll, float FoamPulse, float FoamArrivalRate, float SwellDirectionDegrees, float SwellDirectionality)`
  — every shared dial, all required. Positional on purpose: the defect this file closes was a
  view that supplied half the dials and left the rest to the shader, and a record the compiler
  will not let you half-fill cannot do that again. `TerrainWaterLook.Settings(size, origin,
  coastRange)` is what builds it; call sites pass by name.
- `static void Apply(ShaderMaterial, in Settings)` — writes `map_size`, `map_origin`,
  `coast_range`, `ground_texture_tiles`, `water_texture_tiles`, `wave_intensity`,
  `foam_strength`, `shallow_tiles`, `deep_tiles`, `foam_tiles_along`, `foam_tiles_across`,
  `foam_scroll`, `foam_pulse`, `foam_arrival_rate`, `swell_direction_degrees` and
  `swell_directionality`, each held to the range the shader's own `hint_range` declares. The
  clamps are here rather than at each export because a `PropertyHint.Range` constrains the
  Inspector and nothing else. Two pass through untouched: `coast_range`, which must agree with
  the range its caller *built* the coast field with, and `swell_direction_degrees`, a direction
  where 370 is 10 rather than 360.
- `static bool BindFoamSheet(ShaderMaterial, string path, string owner)` — binds `foam_sheet` and
  the `use_foam_sheet` switch that selects it, together, so they cannot disagree. A path that is
  set and fails to load says so twice on purpose: `TerrainTextures` reports that the file did not
  load, this reports that the map will draw generated crests instead. Only the second tells an
  author their surf is gone rather than merely late.
- `static void ApplyTextures(ShaderMaterial, string owner, string shallowPath, string deepPath, string sandPath, string foamSheetPath)`
  — `tex_shallow`, `tex_deep`, `tex_sand` and the foam sheet in one call, so a view cannot bind
  two of them and forget the third. The paths are the look's `ShallowTexturePath`,
  `DeepTexturePath`, `SeabedSandTexturePath` and `FoamSheetPath`.

## Callers

- [`TerrainSeaSurface.BuildMaterial`](TerrainSeaSurface.md) — `Apply` + `ApplyTextures`, for the
  Tiles, IsometricAutotile and block-Isometric seas.
- `TerrainPaintedRendererComponent` — `Apply` plus `BindFoamSheet` only. That view composites its
  seabed from its own LAND materials, so the look's three water-bed textures are not read there.

## What stays with the caller, deliberately

- `coast_map`. Every view binds the shared field, but each resolves it through its own render
  cache and its own windowing, so the value is genuinely per view even though the uniform is
  shared.
- The uniforms `iso_water.gdshader` declares for itself — `max_opacity`, `clarity_tiles`,
  `lake_opacity`, `shore_opacity`, `cell_size`, `tile_batch`, `flat_projection`, `tile_offset`.
  They describe a transparent water *surface* and mean nothing to an opaque composite, so they
  belong to the views that draw one (`TerrainSeaSurface.Sheet` carries the first four).
- `sand_texture_tiles` and `ground_seam_blend`, which [`TerrainMaterialTiling`](TerrainMaterialTiling.md)
  owns.
- `tint_sand`, `tint_shallow`, `tint_deep`, `foam_tiles`, `wave_speed`, `wave_amount`,
  `shallow_sand` and `foam_lines`, which no renderer sets: they run on the values the shader
  author chose, and adding exports for them would be inventing dials nothing asked for.

## Tests

`tests/terrain_water_material_probe.gd` (`tests/terrain_water_material_probe.ps1` runs it) checks
that all four views carry the thirteen shared dials from one look, that they agree, that an empty
foam path leaves `use_foam_sheet` false, and that `Apply` still clamps a scripted out-of-range
value to the shader's own floor and ceiling.
