# FIX-18 — A prop is never drawn larger than its art

**Type:** fix (rendering) · **Area:** `ecs/terrain/TerrainPropSizing.cs`, `TerrainFeatureRendererComponent`, `TerrainIsometricFeatureRendererComponent`, `TerrainReliefRendererComponent` · **Status:** implemented 2026-09-18, guarded · **Effort:** XS–S · **Risk:** low; it changes a shipped look (props are smaller where the art is small) and touches no generated data.

## Evidence

The owner, 2026-09-18, in the lab: "still when i zoom in in original and cartoon renders the tree's look blury", and on what to do about it: "your suppose to do best practice for any game engine like they do in any colony/rts game".

Both views draw trees from the owner's `cartoon_trees.png` — 256x256, a 4x2 grid, so 64x128 a frame, and the file is named for that size (`trees_cartoon_64x128.png` in `Art/Resources`). The renderers were drawing them at up to 120x143.

The cause is in how the size was computed:

```
fit   = tile / max(visible frame)      // normalise the frame to the tile
scale = SizeInCells(kind, jitter)      // trees 1.75-2.25 cells
drawn = frame * fit * scale            // the longest side becomes tile * cells
```

The size is stated in cells and normalised by each frame's longest side, so the frames holding the fewest pixels were stretched hardest. Measured on the shipped sheets at a 64-pixel cell:

| Sheet | Visible frame pixels | Drawn at | Magnified |
|---|---|---|---|
| Trees, frame 3 | 58x69 | 94x112 to 121x144 | 1.62x–2.09x |
| Trees, frame 0 | 58x77 | 84x112 to 108x144 | 1.45x–1.87x |
| Trees, frame 1 (the tall conifer) | 40x120 | 37x112 to 48x144 | 0.93x–1.20x |
| Bushes, every frame | 28x14 to 28x26 | up to 42x39 | 0.80x–1.49x |
| Rocks, every frame | 28x14 to 28x24 | up to 42x36 | 0.57x–1.49x |

Seven of the eight tree frames were magnified before the camera zoomed at all; zooming multiplied it.

## Design

The rule 2D strategy engines keep: **sprites are authored at the size they are drawn at, and the renderer only ever scales down.** Magnifying a sprite past its own resolution cannot add detail.

`TerrainPropSizing.DrawnPixels(visiblePixels, tilePixels, kind, jitter)` is now the one place a prop's drawn size is decided, for the flat feature, isometric feature and relief renderers alike:

```csharp
float fit = Mathf.Max(1f, tilePixels) / Mathf.Max(1f, Mathf.Max(visiblePixels.X, visiblePixels.Y));
return visiblePixels * Mathf.Min(fit * SizeInCells(kind, jitter), 1f);
```

Art with pixels to spare is unaffected: `forest_trees.png`'s 314x314 frames still take the full 1.75–2.25 cells, and the isometric sheet's 109x150 frames still fill their diamond. Only art too small for its category is affected, and it is drawn at its own size instead of being blown up.

The size in cells remains the category's own fact, and one edit to the shared resource still resizes every renderer and projection.

## Guards

`tests/terrain_prop_sizing_probe.gd`, rewritten around the two rules:
- On the resource alone: `DrawnPixels(314x314, 64, "woods")` takes the category size, `DrawnPixels(58x69, 64, "woods")` returns 58x69 unchanged.
- On the shipped cartoon art, in the flat, tile and isometric views: no stamp exceeds the largest visible frame its own sheet holds — each view measured against its own sheet, since the isometric view binds 109x150 art where the flat binds 58x120 — and the largest stamp must reach it, so nothing passes by drawing props tiny. Measured: flat trees largest 58x120 against art 58x120, bushes 28x26 against 28x26, isometric 109x144 against 109x150.
- On `forest_trees.png`: every stamp inside 1.75–2.25 cells, flat and isometric.
- One resource edit (Trees 1.25) still lands at exactly 1.25 cells in both.
- **Mutation:** the cap removed — "art smaller than the category was magnified to (107.6, 128.0)".

## Renders

`tmp/trees_cartoon_zoom1.png` and `_zoom2.png` (before) against `tmp/native_cartoon_zoom1.png`, `_zoom2.png`, `_zoom4.png` and the Original pair. Trees are smaller and hold their shape; at zoom 2 the art is magnified 2x rather than 3.8x.

## The other half: the filter

Capping the drawn size stopped the RENDERER magnifying; the camera still could, and did. With the
cap in place the trees were still blurred at zoom, because the prop renderers sampled
`LinearWithMipmaps`: at zoom 4 that interpolates between the artist's pixels. Rendered side by side
at zoom 4, `tmp/filter_linear_zoom4.png` is smeared and `tmp/filter_nearest_zoom4.png` shows the
art's own pixels.

Godot's class reference makes the choice explicit: both mipmap filters are "smooth from a distance",
and up close `NEAREST_WITH_MIPMAPS` is "pixelated" — the artist's texels — where `LINEAR_WITH_MIPMAPS`
is "smooth". So the three prop renderers now sample nearest above their mip chain, and the whole set
of per-view answers is written down in `docs/terrain-engine/TEXTURE_FILTERS.md`, which also records
two related defects found while auditing every renderer: the relief view was asking
`TerrainMapArt.PixelArt` (a **ground** art-style flag) to answer a sprite question, and a library
pack's tiles were sampled with no mip chain at all (`TerrainLibraryPainter`) while the isometric
autotile view kept a rival copy of the same answer beside it.

Guarded by `tests/terrain_texture_filter_probe.gd`, which pins every view's answer and only pins a
node that has actually drawn.

## Still open

- **The art is the ceiling.** 64x128 trees are sharp at zoom 1 and no better beyond it. For a camera that zooms to 3, the sheets have to carry the pixels: `docs/terrain-engine/PROP_ART_BRIEF.md` states the sizes (384x768 a tree frame, 128x128 for bushes and rocks).
- **Zoom range.** The other half of the practice — Factorio and its kin cap zoom-in at the resolution their sprites carry — is not applied here; the lab and the game still zoom past what the art supports.
