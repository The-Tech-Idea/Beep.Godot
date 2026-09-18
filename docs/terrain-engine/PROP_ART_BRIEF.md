# Prop and ground art brief

What the terrain views need from the art, and why. Written 2026-09-18, when the owner reported that
trees and rocks blur as the camera zooms in and the ground reads as a flat wash.

## The rule

**A sprite is sharp only while the screen shows it no larger than the pixels it was drawn with.**
A tile is 64 screen pixels at zoom 1. The camera zooms past that, so art drawn at 64 pixels is already
magnified before anyone zooms.

## What is there now, and what it needs

Measured on the painted demo at zoom 1 (64-pixel tiles), cartoon art, seed 31415:

| Sheet | Now | Frame now | Drawn at (zoom 1) | Sharp to | Needed |
|---|---|---|---|---|---|
| `textures/map_art/cartoon_trees.png` | 256x256, 4x2 frames | **64x128** | 38x113 to 120x143 px, 206 stamps | zoom 1 at best, 0.5 for a wide tree | **1536x1536**, 4x2 frames of **384x768** |
| `textures/map_art/cartoon_bushes.png` | 128x64, 4x2 frames | **32x32** | 28x14 to 36x33 px, 394 stamps | about zoom 1 | **512x256**, 4x2 frames of **128x128** |
| `textures/map_art/cartoon_rocks.png` | 128x64, 4x2 frames | **32x32** | 19x9 to 39x32 px, 127 stamps | about zoom 1 | **512x256**, 4x2 frames of **128x128** |

A tree is the problem case: its frame is 64 pixels wide and the widest canopy is drawn at 120, so it is
magnified before the camera moves at all. At the needed sizes every prop stays sharp past zoom 3.

If 1536x1536 is more than you want to draw, 1024x1024 (frames of 256x512) keeps a typical tree sharp to
about zoom 2.7 and the widest to 2.1 — better than now by a factor of four either way.

### Frames

- Keep the grid exactly as it is: the renderer reads `WoodsColumns`/`WoodsRows` and
  `BushesColumns`/`BushesRows` (both 4x2 for these sheets), and the frame count decides which trees a
  terrain can draw.
- Every frame is one whole plant or rock, centred horizontally, standing on the frame's bottom edge:
  the renderer anchors a stamp at (0.5, 0.92) of its target rectangle.
- Draw at the new size. Do not upscale the current 64-pixel art — an upscale carries no more detail
  than the file it came from, which is the whole problem.

### Backing

- **Transparent background, straight alpha.** Not magenta, not white, not a coloured card: the stamps
  are drawn over the ground, so any backing colour shows as a halo around every tree.
- Keep a 2-pixel transparent margin inside each frame so neighbouring frames never bleed into one
  another when the sheet is mipmapped.

### Import

- `mipmaps/generate=true` (the trees sheet already has it; keep it) — the same art is drawn far
  smaller when the camera pulls back, and without mips it sparkles.
- No compression for these sheets; they are small and are sampled at every zoom.

## Ground textures

The painted views sample a ground texture at the pattern scale it is authored for — Cartoon repeats a
region every 1.6 tiles, which is 102 screen pixels from a 310-pixel region — so the source is minified
about three times and mipmapping averages its grain away. FIX-17 adds a grain pass that samples the same
texture again at one texel a pixel, which gives the ground a surface without changing the pattern scale.

Art that would need no second sample: a ground texture whose **pattern is fine enough to sit right at
1.6 tiles** while being drawn at the resolution that fills those 102 pixels — roughly a 128-pixel tile
with detail no coarser than a few pixels. The current cartoon atlas holds 310-pixel regions of a coarse
pattern, which is why it can be proportionally right or sharp, but not both.

## Delivering

Drop the new sheets over the existing paths, keeping the names. Nothing else needs editing: the scenes
and `cartoon.tres` bind those paths, and the renderers read the frame grid from their exports. Then
`tests/examples/demo_scenes.gd`, `tests/terrain_understory_probe.gd` and `terrain_prop_sizing_probe.gd`
check that the props still draw at their authored sizes, on the right tiles, with their bushes among the
trees; `tests/output/feature_sheets/` holds the captures to look at.
