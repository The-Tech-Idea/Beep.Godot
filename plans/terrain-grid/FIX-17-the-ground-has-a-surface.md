# FIX-17 — The painted ground has a surface: grain at one texel a pixel

**Type:** fix (rendering) · **Area:** `shaders/terrain_splat.gdshader`, `ecs/terrain/TerrainPaintedRendererComponent.cs`, `ecs/terrain/TerrainMapArt.cs`, the shipped art profiles · **Status:** implemented 2026-09-18, then turned OFF by default the same day — the grain carried the art's own painted objects onto the ground; see "Reversed later the same day" · **Effort:** S · **Risk:** low. It changes a shipped look, so it goes through the owner's before/after gate; it touches no generated data.

## Evidence

The owner, 2026-09-18: "original and cartoon renders is showing a blury terrain !!! they should look like sharp", and "the terrain looks very bad". The props are sharp in the same frame; the ground is a flat wash with soft boundaries.

Measured on the painted demo at 1:1 (64 px a cell), one map, one camera:

| Style | Ground source | Repeat | On screen | Minification |
|---|---|---|---|---|
| Original | `meadow_ground.png`, 1254x1254 | `GroundTextureTiles` 6 tiles | 384 px | 3.3x |
| Cartoon | `pixel_terrain_atlas.png` regions, ~310x310 | `TextureRepeatCells` 1.6 tiles | 102 px | 3.0x |

Every ground sampler is `filter_linear_mipmap`, so at three texels a pixel the mip chain averages the grain away before anything else in the shader runs. Cartoon then mixes the result over a flat colour at `GroundDetail` 0.16, leaving 16% texture.

Two dials were tried and rejected on the renders:
- **A mip bias of −1.0** (`tmp/sharp_original_bias.png`): indistinguishable from the shipped look.
- **Spreading the pattern to 20 tiles** (`tmp/sharp_original_repeat.png`): genuinely sharp, and exactly the change the owner had already rejected once — the cartoon ground repeat was moved 4.8 → 1.6 tiles on 2026-09-16 because its detail was "proportionally too big against buildings and cars".

## Design

Keep the base pattern where the artist put it; add the grain back separately. This is what terrain shaders do generally: a base colour at the authored scale and a detail sample at screen resolution.

- `ground_grain(id, ...)` samples the SAME material texture at `ground_detail_tiles` and returns a multiplier around one.
- It is measured against **the material's own average**, not mid-grey: the second sample takes gradients wide enough to land on the texture's smallest mip, which is that average. Mid-grey darkens every material brighter than it; sand lost a fifth of its brightness, and `terrain_painted_blend_probe` caught it as lost coverage.
- It is applied once per pixel, after the beach mix, from the material that pixel actually shows.
- The renderer owns the painted view's own pair (`GroundDetailTiles` 20, `GroundDetailStrength` 0.5 — a 1254-pixel source is one texel a pixel at 20 tiles of 64 px). A `TerrainMapArt` profile carries its own (`GroundGrainTiles`, `GroundGrainStrength`) and overwrites both, because the repeat follows the art's resolution: Cartoon 4.8 tiles at 0.35 for its 310-pixel regions, Pixel Art off — a second frequency fights its quantised look.

Strengths are the owner's pick from rendered comparisons (0.25 / 0.35 / 0.50 at zoom 1 and 2, both styles).

## Guards

`tests/terrain_painted_blend_probe.gd` gains `verify_ground_grain`: one material over the whole fixture, a 512-pixel one-pixel checker squeezed into a single tile so the base sample mips flat, and the grain sampling the same texture over eight tiles at one texel a pixel.
- The interior's variation must rise by more than three times: measured 0.0000 flat against 0.1255 grained.
- The mean must not move: 0.4980 either way.
- **Mutation:** the grain multiplied by zero — variation 0.0000 against 0.0000, and the probe reports it.

The probe's existing checks also guard the mean-relative form: with a solid red material, a mid-grey-relative grain failed "Zero-width desert has 61504 uncovered/grass pixels".

## Renders

In `tmp/`, all at 1:1 and 2x on one map and camera:
- `final_original_flat_zoom1.png` against `final_original_grain_zoom1.png`
- `final_cartoon_flat_zoom1.png` against `final_cartoon_grain_zoom1.png`
- `final_pixel_art_*` (unchanged by design), and the strength ladder `grain_<style>_{0.25,0.35,0.50}_zoom{1,2}.png`.

## Reversed later the same day: the grain is OFF by default

The grain samples the material's own texture near one texel a pixel, which is the whole idea — and
it is also why it cannot be shipped on with this art. At that scale it carries whatever the artist
drew at that size, and the cartoon atlas's sand has **a 22-texel starfish and 12-to-18-texel
shells**. They came through on the beach as soft, object-sized marks, in every view that lays a
ground texture over its terrain. The owner reported them three times and finally placed the fault
exactly: *"its in all renders except isometric"* — the isometric block view is the only one that
overlays no ground texture at all.

Two attempts to keep the feature and lose the objects both failed, on renders:

1. **Calibrating the repeat.** Pixel Art was still at 4.8 cells (1:1), so its colour layer drew the
   same objects at authored size; moving it to 1.6 like every other style was right on its own
   merits, and changed nothing about these marks.
2. **Making the grain a high pass** — reference sample a few mip levels up, so only detail finer
   than the reference survives. At 16 texels the shells survived; at 4 texels their *outlines*
   survived, which is what the magnified crop showed: rings, not discs.

So the default is now zero, in all three places that carry one: `TerrainMapArt.GroundGrainStrength`,
`TerrainPaintedRendererComponent.GroundDetailStrength`, and the tile view's own
`TerrainTileRendererComponent.GroundDetailStrength` — that last one had the same fault by a
different route, multiplying the tiles by the texture's absolute luminance.

The high-pass stays, because it is correct for what the feature is for, and the guard still measures
it unchanged (variation 0.0000 flat against 0.1255 grained, mean 0.4980 either way — the probe sets
the strength itself, so it never depended on the default). Turn it on per profile for ground art
that is only surface, with nothing in it to recognise. That art is the real fix, and the brief
already asks for it.

## Still open

- **The prop sheets are too small to zoom into.** `cartoon_trees.png` is 256x256 for a 4x2 grid, so a tree frame is 64x128 pixels and is drawn at 38x113 to 120x143 — already magnified at zoom 1, and about four times that at zoom 4. `cartoon_bushes.png` and `cartoon_rocks.png` are 128x64 with 32x32 frames, drawn at up to 36x33 and 39x32. No filter or shader change can restore detail the file does not carry; the owner is redrawing them (2026-09-18) against [`docs/terrain-engine/PROP_ART_BRIEF.md`](../../docs/terrain-engine/PROP_ART_BRIEF.md).
- **The ground art itself.** The grain gives the ground a surface at the authored pattern scale; ground art drawn fine enough to sit at the vehicles' scale AND hold up at one texel a pixel would need no second sample at all. The brief covers that too.
