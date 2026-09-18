# Prop and ground art brief

What the terrain views need from the art, and why. Written 2026-09-18, when the owner reported that
trees and rocks blur as the camera zooms in and the ground reads as a flat wash.

## The rule

**A sprite is sharp only while the screen shows it no larger than the pixels it was drawn with.**
A tile is 64 screen pixels at zoom 1. The camera zooms past that, so art drawn at 64 pixels is already
magnified before anyone zooms.

Since FIX-18 the engine never magnifies prop art: a prop takes its category's size in cells unless that
would stretch the art, and then it is drawn at its own pixels instead. So art too small for its category
is not blurry any more — it is **small**. The sizes below are what each sheet needs to fill the size its
category asks for, and to hold up while the camera zooms in.

## What is there now, and what it needs

Measured on the painted demo at zoom 1 (64-pixel tiles), cartoon art, seed 31415:

| Sheet | Now | Frame now | Drawn at (zoom 1) | Sharp to | Needed |
|---|---|---|---|---|---|
| `textures/map_art/cartoon_trees.png` | 256x256, 4x2 frames | **64x128** | 39x69 to 60x124 px, 206 stamps | zoom 1 | **1536x1536**, 4x2 frames of **384x768** |
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

- **Transparent background, straight alpha** in the file the engine binds. Not magenta, not white, not
  a coloured card: the stamps are drawn over the ground, so any backing colour shows as a halo around
  every tree.
- The owner draws on a **pure green screen** and `tools/key_green_screen_sheet.py` makes the repo copy.
  Run it per sheet; it refuses a source that already has alpha, and a source whose most common colour
  is not the key.
- Keep a 2-pixel transparent margin inside each frame so neighbouring frames never bleed into one
  another when the sheet is mipmapped.

### Import

- `mipmaps/generate=true` (the trees sheet already has it; keep it) — the same art is drawn far
  smaller when the camera pulls back, and without mips it sparkles.
- No compression for these sheets; they are small and are sampled at every zoom.

## What was delivered, 2026-09-18

Four art **styles**, each a complete prop set, drawn on a green screen at the sizes this engine's
frame-size naming asks for (`Art/Resources/{trees_<style>_64x128, bushes_<style>_32x32_v2,
rocks_<style>_32x32}.png`): trees 256x256 of 4x2 frames of 64x128, bushes and rocks 128x64 of 4x2
frames of 32x32.

All four are wired, one `TerrainMapArt` profile each, and every profile carries its own eight tree
frames, eight bush frames and four plus four rocks:

| Style | Profile | Prop sheets |
|---|---|---|
| Cartoon | `cartoon.tres` | `cartoon_trees.png`, `cartoon_bushes.png`, `cartoon_rocks.png` |
| Pixel Art | `pixel_art.tres` | `pixel_art_trees.png`, `pixel_art_bushes.png`, `pixel_art_rocks.png` |
| Isometric Art | `isometric.tres` | `isometric_trees.png`, `isometric_bushes.png`, `isometric_rocks.png` |
| Low Poly | `low_poly.tres` | `low_poly_trees.png`, `low_poly_bushes.png`, `low_poly_rocks.png` |

The two new profiles take their GROUND from the same shared atlas the other two use, at the same
repeat, because the drop was prop art only. The style is in the props until ground art for them
exists. "Isometric Art" is named apart from the **Isometric** projection, which is a different axis
entirely - a style draws through the painted projection.

## Ground art must contain no objects

This is the other half of the brief, and it is what the ground art has to satisfy: **nothing in a
ground texture may be recognisable as a thing.** A ground texture is sampled at a few texels a pixel
and repeated across a whole biome, so anything the artist draws at twenty texels lands on screen at
about the size of a bush, in every tile, forever.

The shipped cartoon atlas's sand fails this: it has a 22-texel starfish and 12-to-18-texel shells.
They read as objects lying on the beach and the owner reported them three times on 2026-09-18. The
grain pass made them plainest, because it samples the material at one texel a pixel, so it is now
**off by default** (see `plans/terrain-grid/FIX-17-*.md`) - which costs the ground its surface until
the art can carry one.

What ground art needs, then: seamless, no motif wider than a few texels, all variation in tone and
speckle rather than in drawn objects. Shells, starfish, flowers and tufts belong in a prop sheet,
where the engine can place them, size them and let a player interact with them.

Adding a style is now adding a resource: `TerrainLabComponent.StyleProfiles` is an ordered list and
the view menu is built from it, each entry named by the profile's own `DisplayName`. It used to be a
property per style with a branch per index in three methods, plus the menu text typed into the lab
scene.

Two things this changed beyond the pixels:

- **The keying tool could not key it.** `key_green_screen_sheet.py` separated art from backing by green
  dominance, which worked only while the foliage was teal. On the new sheets it read leaves as backing:
  of the 3380 art pixels in `bushes_cartoon_32x32_v2.png` it left 127 opaque and dissolved 3154 into
  edge. The art's own greens reach (0, 187, 4) and the backing never falls below (3, 251, 3), so the
  tool now keys on nearness to the key itself and feathers only the ring that touches it.
- **Pixel art stopped repeating one tree.** That profile bound a single 61x80 region for every tree on
  the map; it now has the eight its sheet holds. `pixel_tree.png` was left unreferenced and removed.

The resolution ceiling in the table above is unchanged: these frames are still 64x128, so a tree is
drawn at about 120 pixels and is sharp at zoom 1 and coarse beyond it. Nothing is blurry - since FIX-18
and the nearest-above-mipmaps filter the art is never interpolated - but zooming in shows the art's own
pixels rather than more detail.

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
