# Blender Terrain Starter

Status: rejected visual experiment. The user rejected the procedural walls
and grass transition. Do not use these renders as the approved asset style.
The active style revision is `generated/terrain_reference/v2/` under the addon.
The Blender source remains an experiment, not a source for that painted sheet.

An editable front-facing 2.5D terrain study using the project's existing
texture library. This is a new pack; Mountain Prefab 1 and 2 remain separate.

Outputs: `addons/beep_game_builder_cs/generated/terrain_blender/v1/`.
Blender sources: `art_sources/terrain_blender/v1/<theme>/terrain_source.blend`.
Texture images are packed into each Blender file. The source directory is
excluded from Godot import; Godot uses the rendered PNGs.

Three themes: grass/granite, grey rock, alpine snow. Each has 16 transparent
sprites, an atlas, a manifest, a contact sheet, and `mountain_layers.tscn`:

- Three independent plateau sizes, each with a 1-unit cliff height.
- Two small steps with identical footprints and 0.5 / 0.25-unit heights.
- Rounded and square hills with continuous sloping sides.
- Nine separate ramps: front, left and right at 0.25 / 0.5 / 1-unit rise.

The camera faces straight along the Y axis, elevated 38 degrees, with no
isometric yaw or perspective distortion. All original sprites use 64 pixels
per world unit and an 800x720 transparent canvas. Their ground origin is
(400,360). One unit of vertical elevation moves a sprite up 50.4327 pixels.
The atlas crops retain that same origin in each region's `pivot` field.

Ramp width is 0.85 units and ground run is 1.65 units at all heights.
Heights change the actual mesh rise, not a resized image. Ramps have no rails,
plants or ground skirt. A separate `ramp_fit.png` illustrates the full-height
front ramp meeting a platform. The mountain scenes contain only three plates.

Drag a theme's `mountain_layers.tscn` into a Godot 4 scene to use independent
Sprite2D tiers. Step and ramp sprites are placed manually. These are visual
assets; collision, navigation and character elevation are not included.
The Blender assembled preview includes contact shading between tiers;
individually rendered Sprite2D layers do not include that inter-tier shading.

Regenerate from the project root:

```powershell
& 'H:\Program Files\Blender Foundation\Blender 3.1\blender.exe' --background --factory-startup --python-exit-code 1 --python tools/build_blender_terrain.py
python tools/package_blender_terrain.py
```

Add `-- --theme grass_granite` to the Blender command for one theme.
In `build_blender_terrain.py`, the three `plate(...)` calls control plateau
radii and heights. The lower footprint must remain larger in both axes than
the upper footprint, including the small cliff flare. `ramp()` controls width
and run, `hill()` controls the slope, and `THEMES` selects texture files and
atlas swatches. Rerun packaging after rendering to update atlas rectangles.

The appearance is a textured Blender render, rather than the painterly style
of the earlier authored prefabs. Use the preview to evaluate that direction
before expanding to further materials or shapes.
