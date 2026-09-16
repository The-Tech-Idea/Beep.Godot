# TerrainMaterialTiling

A native Godot `[GlobalClass] Resource`, assigned to
`TerrainPaintedRendererComponent.MaterialTiling`. This is render configuration,
not a world recipe or a second terrain-data source.

## Settings

`Grass`, `DryGrass`, `Sand`, `Dirt`, `Snow`, `Mud`, `Gravel`, `Rock` and `Lava`
set **tiles per texture repeat**. Smaller values make features in that texture
smaller. Zero inherits `GroundTextureTiles`, which since VIEW-04 (2026-09-16) is a dial on the
world's [`TerrainWaterLook`](TerrainWaterLook.md) rather than an export on the painted renderer;
positive values are
clamped to 0.25-32, nonpositive/nonfinite values inherit. A null resource leaves
all materials at the common ground scale.

The settings follow texture slots, not biome IDs: jungle shares Grass, desert
shares Sand, tundra shares Dirt, ice shares Snow, and swamp shares Mud. Sand's
scale also applies to the visible seabed. Animated water still uses the separate
`WaterTextureTiles` dial, which is the water look's too.

Repeat phase uses absolute grid coordinates, including the renderer's
`BoundsOrigin`. Cropping a live map does not restart the textures at local zero.
Scene transforms do not change their size in grid units.

`SeamBlend` optionally corrects albedo repeat-edge mismatches, from 0 to 0.125 of
a repeat per edge. It defaults to zero. The shader blends a mirrored counterpart
only inside that narrow boundary band; at corners it combines both axes. Texture
interiors and alpha remain unchanged. This applies to ground albedos, including
submerged sand, not animated water, coast geometry, navigation or feature placement.

The correction adds up to three texture samples within corner bands, one near
a single-axis boundary, none in interiors. It is not cost-free, not stochastic
tiling, and cannot remove a recognizable repeating motif. Prefer good source art;
use zero when its edges already match. This does not make the source bitmap seamless.

## Use

Create a TerrainMaterialTiling resource in the Inspector, set the needed overrides,
and assign it to the painted renderer. Call `Rebuild()` after changing settings
from code. The renderer reads the resource without modifying it, and updates only
material uniforms when terrain data are unchanged. Removing the resource or
clearing a slot resets the corresponding uniforms; stale overrides do not persist.

The four painted addon demos share
`textures/terrain/painted_ground_tiling.tres`: Rock=6, SeamBlend=0.04, other slots=0.
Its bedrock artwork and limits are documented in [Bedrock Material](BEDROCK_MATERIAL.md).

## Tests

`terrain_material_scale_probe.gd` checks all nine slots and terrain aliases with
actual GPU pixels at 0.5x/1x/2x, unchanged unrelated materials/open sea, visible
seabed scaling, full opacity, resource removal and slot reset, and unchanged
ID/shade/coast resource identities and live grid bytes. The bedrock GPU probe
checks repeat-edge correction and byte-identical interiors at the same zooms.
