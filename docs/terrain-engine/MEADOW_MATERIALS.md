# Meadow Materials

The painted addon demos use two new opaque 1254x1254 albedo textures:

- `addons/beep_game_builder_cs/textures/terrain/meadow_ground.png`: green meadow.
- `addons/beep_game_builder_cs/textures/terrain/dry_meadow_ground.png`: straw-green dry meadow.

Both were generated with the built-in image-generation tool on 2026-09-06.
They were copied into the addon without resampling, recolouring or overwriting
the supplied `grass.png`, `dry_grass.png` or external Art directory originals.
The dry version was generated using the new green version as its edit reference.

## Bindings

Assign the green image to `GrassTexturePath` and the dry image to
`DryGrassTexturePath` on `TerrainPaintedRendererComponent`. Both remain optional
scene assets, not hard-coded engine dependencies. The shader's existing biome
tints still distinguish grass, dry grass and jungle. No biome or water data is
changed to force a greener picture.

Updated scenes: terrain generator lab, standalone splat demo, generation-layers
demo and the painted surface in `grid_world_2d_iso.tscn`. These assets do not
replace terrain atlases in the other projections. Cross-view art remains work
in progress. Other terrain materials retain their existing bindings.

The root project's Run action now opens the terrain generator lab; it previously
referenced the removed `tests/examples/grid_world_painterly_demo.tscn`.

## Import And Verification

Lossless imports retain their full dimensions, with `mipmaps/generate=true`.
The painted shader uses repeat sampling and explicit texture gradients. Do not
disable mipmaps to make a distant view appear sharper: the close detail is in
the artwork and ground repeat scale, not a nearest-neighbour filter.

The raw-image probe checks full alpha opacity, square dimensions, imported mip
chains, broad brightness variation and average opposite-edge colour mismatch.
Across an 8x8 partition, block-mean luminance standard deviation fell from
0.00608 to 0.00143 for grass and 0.00992 to 0.00229 for dry grass. Average RGB
opposite-edge mismatch is 0.0212/0.0223 and 0.0219/0.0290 respectively (X/Y).
These are approximate repeatable painted textures, not mathematically identical
edge pixels. The measurements support reduced mottling, not universal visual quality.

Run the checks from the addon root:

```powershell
godot --headless --path . --script tests/terrain_meadow_texture_probe.gd
godot --path . --rendering-method gl_compatibility --script tests/terrain_painter_visual_probe.gd -- --meadow-art
```

The second command renders the same seed, ID/shade/coast maps and feature positions
with old/new textures at 0.36x, 1x and 2x zoom. Animated water time is fixed.
Output: `tests/output/painter_visual/art-{original,meadow}-zoom-*.png`.
The default seed's pictured grassland is dry grass, not a green-biome preview.
`--materials` is a separate diagnostic that swaps materials and hides features;
it must not be presented as a generated biome distribution.

For targeted imports, pass resource paths to the existing helper:

```powershell
godot --headless --path . --editor --script tools/reimport_terrain_art.gd -- res://addons/beep_game_builder_cs/textures/terrain/meadow_ground.png res://addons/beep_game_builder_cs/textures/terrain/dry_meadow_ground.png
```

This machine's bulk editor import encountered an unrelated missing Blender path;
targeted PNG imports succeeded and runtime resource/mipmap checks passed. The
headless editor also reported shutdown RID leaks, so a successful PNG import is
not a claim that all editor import/shutdown issues are resolved.

## Generation Prompts

Green meadow, built-in generation:

> Create ONE seamless tileable square 1024x1024 opaque ground ALBEDO TEXTURE for a high quality 2D top-down settlement building game. This is a material texture, not a scene, illustration, sprite sheet or terrain tile with borders. Subject: quiet natural short meadow grass, mildly dry in summer but predominantly lively medium leaf green (#719633 approximate), with occasional tiny straw-green blade flecks. Camera perfectly perpendicular to ground. An even solid green base should dominate 85 percent of the surface, with very small, crisp yet softly painted irregular grass blade strokes grouped loosely in restrained patches at approximately 6 to 20 pixels size. Moderate realism in individual grass strokes, elegant hand-painted RTS ground treatment, not pixel art, not cartoon outlines, not a photo of tangled grass. Low overall brightness variation; local blade detail only slightly darker or lighter than base. Avoid all large pale/dark cloudy patches, diagonal bands, fractal marbling, broad gradients, haze, blur, excessive contrast, central highlight, vignette, directional lighting, cast shadows, tall tufts, flowers, bushes, trees, stones, dirt holes, roads, islands, water. Flat even ambient albedo. No large recognizable repeating motifs. Texture continues uniformly through all FOUR edges and opposite edges must wrap seamlessly both horizontally and vertically. No transparent areas, gutters, frame, borders, text, grid, labels, UI. Fill the whole square with this quiet green ground material.

Dry meadow, built-in edit using the green output as reference:

> Create the DRY GRASS seasonal counterpart of this seamless game ground albedo texture. Preserve its exact top-down short grass scale, sparse small stroke clusters, very even base brightness, fine painterly detail, overall restrained contrast, opaque full-square coverage and seamless wrap on ALL four edges. Replace the lively green base with a moderately warmer muted straw-green base, approximately #9a9e46, with sparse light ochre and olive grass blades. It should look like slightly drought-dried meadow grass, not bare sand, beige fog, brown dirt or a dead yellow field. Keep the grass distinct and readable but quiet under game objects. Do not add large pale/dark mottled regions, cloud noise, shadows, bright lighting, gradients, borders, gutters, text, tiles, rocks, flowers, objects, water, transparency, pixel art, or tall tufts. Keep everything else unchanged. One square seamless material texture.

The generator returned 1254x1254 images despite the requested 1024x1024 size.
The returned dimensions were preserved and verified rather than silently claiming
the requested size or resampling away fine detail.
