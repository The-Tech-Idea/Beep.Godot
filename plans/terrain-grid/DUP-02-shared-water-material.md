# DUP-02 — One water material builder for the three views

**Type:** duplication fix · **Area:** `TerrainTileRendererComponent`, `TerrainIsometricRendererComponent`, `TerrainPaintedRendererComponent`, `shaders/water_common.gdshaderinc`, `iso_water.gdshader` · **Status:** proposed 2026-09-08 · **Effort:** S (1 day) · **Risk:** low

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
