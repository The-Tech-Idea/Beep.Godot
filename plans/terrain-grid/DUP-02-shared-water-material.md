# DUP-02 — One water material builder for the three views

**Type:** duplication fix · **Area:** `TerrainTileRendererComponent`, `TerrainIsometricRendererComponent`, `TerrainPaintedRendererComponent`, `shaders/water_common.gdshaderinc`, `iso_water.gdshader` · **Status:** **IMPLEMENTED 2026-09-08** · **Effort:** S (took ~half a day) · **Risk:** low

## Outcome

Landed as `ecs/terrain/TerrainWaterMaterial.cs` plus `TerrainTextures.Bind`. Verified: `dotnet build` clean (0 warnings), the new `tests/terrain_water_material_probe.gd` green in the gate, and both guards mutation-tested — the scan pin trips on 4 of 4 mutations, the probe on 3 of 3.

Three things came out differently from the plan above, each for a reason found in the source:

1. **The uniform list is `water_common.gdshaderinc`'s, not an invented one.** The plan guessed at `FoamWidth`/`SwellAmplitude`; the include actually declares 32 uniforms, of which C# writes 22. `TerrainWaterMaterial.Settings` mirrors exactly the 16 scalar dials, and `ApplyTextures` the 4 textures plus the switch. The uniforms `iso_water.gdshader` declares for *itself* — `max_opacity`, `clarity_tiles`, `lake_opacity`, `shore_opacity`, `cell_size`, `tile_batch`, `flat_projection`, `tile_offset` — describe a transparent water *surface* and mean nothing to the painted view's opaque composite, so they stayed with the two views that draw one. `coast_map` also stayed per view: every view binds the shared field, but each resolves it through its own render cache and windowing, so the value is genuinely per view.

2. **The texture binder went to `TerrainTextures`, not to the water class.** The painted view binds nine *ground* slots through the same six lines, so a water-named owner would have been the wrong home. `TerrainTextures.Bind(material, parameter, path, owner)` now owns it and returns whether the art loaded, which is load-bearing: `use_foam_sheet` is set from that result rather than from the path being non-empty.

3. **The tile view's ranges were narrowed; its defaults were left alone.** Its exports allowed values the shader's `hint_range` does not (`DeepTiles` to 64 against a declared 0.5–12), so with clamping in the shared writer the Inspector would have offered numbers that are now silently clamped — the ranges are aligned. The *defaults* still differ from the isometric sea's (FoamStrength 1.0 vs 0.50, DeepTiles 6.0 vs 4.5, ShallowTiles 6.0 vs 1.8). That is a real divergence — one map with two seas — but changing it changes how `terrain_tilemap_demo.tscn` looks, which is an appearance decision rather than a consolidation. **Left for the owner**, and recorded in a comment beside the exports.

Checked before clamping: every water dial authored in `terrain_generator_lab.tscn` and `terrain_tilemap_demo.tscn` is already inside the shader's declared range, so no scene in the tree moves.

**A guard blind spot found and fixed while mutation-testing:** the export pin first read `public float $dial`, which is a prefix of `public float ${dial}Removed` — so a mutation that renamed the export out of existence passed. It now matches the whole declaration.

## Finding

Three renderers each build the water `ShaderMaterial` by hand and set the same uniform names:

| Site | Lines | What it sets |
|---|---|---|
| `TerrainTileRendererComponent.BuildWaterMaterial` | 369-420 | `water_color_*`, `wave_*`, `coast_map`, `tile_size` … |
| `TerrainIsometricRendererComponent.BuildWaterMaterial` | 1089-1162 | the same set **plus** `foam_*` and `swell_*` dials |
| `TerrainPaintedRendererComponent.Rebuild` (splat surface) | 392-449 | the same set plus `art_*` from `TerrainMapArt` |

`SetTexture(material, name, texture)` (null-safe, mip-generation) is copied verbatim at `TerrainTileRendererComponent:425-439` and `TerrainIsometricRendererComponent:1170-1181` (`SetTexture(` — 10 call sites across the two files), and the painted renderer has its own `Assign`.

The shader side is already shared: `water_common.gdshaderinc` defines the uniforms once. Only the C# side forgot.

**Consequence already visible:** the tile view has no foam or swell dials at all — a shoreline that surfs in the isometric view is flat in the tile view of the same map. That is the exact asymmetry Phase 1 fixed for `TerrainTextures` ("three copies were right and one was not").

## Design

`TerrainWaterMaterial` (static, `ecs/terrain/`):

```csharp
public static class TerrainWaterMaterial
{
    public sealed record Settings(Color Shallow, Color Deep, float WaveSpeed, float WaveScale,
        float FoamWidth, float FoamStrength, float SwellAmplitude, float SwellSpeed, Vector2 TileSize);

    public static ShaderMaterial Create(Shader shader, Settings settings);
    public static void Apply(ShaderMaterial material, Settings settings);          // dials only
    public static void BindCoast(ShaderMaterial material, Texture2D? coast, Texture2D? lake); // textures only
    public static void SetTexture(ShaderMaterial material, StringName name, Texture2D? texture);
}
```

Each renderer keeps its own exports (the tile view gains `FoamWidth`/`FoamStrength`/`SwellAmplitude`/`SwellSpeed` with the isometric defaults) and passes a `Settings` record. Uniform names appear in one file. `TerrainMapArt.ApplyGround` stays separate — it is the ground material, not water.

## Steps

1. Extract `TerrainWaterMaterial` from the isometric builder (the most complete copy).
2. Replace the tile and painted builders; add the four missing dials to the tile renderer.
3. Delete both `SetTexture` copies and `Assign`; route through `TerrainWaterMaterial.SetTexture`.
4. `terrain_tilemap_demo.tscn` / `terrain_iso_demo.tscn` / `terrain_generator_lab.tscn` reference `iso_water.gdshader` directly (Python scan: 3 scenes) — unchanged, the material instance is what changes.

## Guards

- Pin: `SetShaderParameter("water_` and `SetShaderParameter("foam_`/`"swell_` appear only in `TerrainWaterMaterial.cs`. Mutation: restore one inline `SetShaderParameter("water_color` in the tile renderer → pin fails.
- Probe: build the same 64×32 world in the tile and isometric views; read `foam_width` off both water materials; assert equal to the authored export. Mutation: drop the tile renderer's `FoamWidth` pass-through → assert fails.
- Existing `terrain_guards.ps1` `tile_layers`/`iso_layers` stay green.

## Dependencies / collisions

Independent of DUP-01 (can land before or after). No collision with the campaign session.

## Out of scope

Shader changes, new water art, lake/river visual differences.
