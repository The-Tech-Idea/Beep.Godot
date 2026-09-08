# Bedrock Material

`addons/beep_game_builder_cs/textures/terrain/bedrock_ground.png` is a new opaque
1254x1254 weathered-bedrock albedo generated with the built-in image tool.
Reference: the user's `Art/textures/stone_atlas.png`. It replaces loose gravel
as the rock binding in four painted addon demos, without overwriting `rock.png`
or any supplied artwork. The source is copied without resampling and imported
losslessly with mipmaps. It does not add height, cliffs, collision or another world.

## Accepted Rendering

The lab, splat demo, generation-layers demo and `grid_world_2d_iso.tscn` use the
shared `painted_ground_tiling.tres`: Rock=6 tiles/repeat and SeamBlend=0.04.
Other texture scales inherit the existing common scale. The authoring resource
is described in [TerrainMaterialTiling](TerrainMaterialTiling.md).

The raw bitmap is **not perfectly seamless**: opposite-edge mean RGB differences
are 0.0573/0.0617. A second image-tool seam-correction attempt still failed the
raw-edge gate (0.0573/0.0662) and was not shipped. Instead, the renderer's optional
narrow repeat-edge correction addresses the visible discontinuity without changing
texture interiors, alpha, terrain boundaries, grid state or terrain data textures.

Measured GPU repeat-boundary mean differences (X/Y):

| Zoom | Original sampling | Corrected sampling |
|---|---|---|
| 0.5 | 0.02115 / 0.02196 | 0.00043 / 0.00068 |
| 1.0 | 0.02169 / 0.02690 | 0.00013 / 0.00017 |
| 2.0 | 0.02547 / 0.02754 | 0.00006 / 0.00004 |

`terrain_bedrock_texture_probe.gd` checks imported opacity/mips, shared scene
bindings, actual rendered repeat boundaries, unchanged interiors and map-resource
identity. `terrain_painter_visual_probe.gd -- --bedrock` compares the old gravel
and new bedrock on one fixed map at overview/1x/2x without changing map bytes.
The comparison profile intentionally has no seam correction, isolating artwork;
the normal lab capture uses the shipped profile including its seam correction.

Other projections retain their own atlases. This asset is not a complete material
palette revision, a height renderer or proof of cross-view art consistency. The
editor import succeeded but still reported existing editor-shutdown RID leaks;
runtime regression results must not be presented as clean editor shutdown.

## Provenance

Built-in generation output:
`C:/Users/f_ald/.codex/generated_images/019fe727-b71e-7b30-99e6-c03137b8995b/exec-34145278-f19b-45c1-8808-896d47fc046d.png`.
The unsuccessful edit remains outside the addon as
`exec-14907fc5-8d99-4884-ada1-8aa105921fef.png` in the same generation directory.

Exact generation prompt:

> Create ONE new seamless game ground albedo texture, not a sprite sheet and not an edited atlas. Asset: weathered exposed BEDROCK seen straight down from orthographic top-down, for a high-quality 2D terrain painter. The provided stone_atlas.png is a material reference only: use its neutral weathered stone language, especially irregular fractured slab surfaces, not its grid layout or loose gravel panels. Fill the entire square with continuous irregular interlocking rock plates, branching narrow shallow cracks at mixed small and medium scales, very restrained sparse olive mineral staining in some crevices, finely pitted stone detail. Rock is neutral warm grey, middle value (roughly RGB 145,142,129), low-to-moderate local contrast; medium plates are visible but no dominant focal slab. Polished hand-painted natural game material, believable geology, not pixel art, not glossy photogrammetry and not soft cloudy noise. Uniform ambient diffuse lighting, no vignette, no broad bright/dark patches, no directional cast shadows. Seamlessly tileable on BOTH axes with matching edge colors and structure, uniform detail to all edges. Flat albedo only, NOT normal map, height map, decorative frame or 3D perspective scene. Fully opaque square texture, preferably 1254x1254 or higher. No text, no grid lines, no tile borders, no isolated boulders, no pebbly aggregate, no grass tufts, no objects, no alpha or transparent areas.

Exact unsuccessful seam-edit prompt:

> Edit the supplied bedrock ground texture to make it genuinely seamless tileable in BOTH X and Y. Preserve the central stone design, neutral grey palette, original detail scale, resolution, opacity and uniformly diffuse albedo. The current left-versus-right and top-versus-bottom edges have an obvious color/structure mismatch when placed adjacent. Correct the periodic boundaries: cracks, slab borders, tones and fine grain must join continuously across opposite edges. Think of painting on a torus with wraparound; opposite outermost pixel rows/columns should match closely (average normalized RGB error below 0.025). Do not add any border, frame, vignette, extra lighting, gradient, transparency, checkerboard or broad blurry seam band. Do not add new subject matter. Output one fully opaque square continuous seamless texture, not a montage or repeat preview.
