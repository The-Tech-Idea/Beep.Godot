# Reference Style Revision

Three grass/rock plateau shapes based directly on the user's supplied
`codex-clipboard-3bfedc80-f511-4e37-9ba3-7878c31d3e0a.png` style reference.
This direction replaces the rejected Blender wall appearance.

`grass_rock_plates.png` is RGBA with actual background transparency.
Assign `plate_low.tres`, `plate_medium.tres`, or `plate_tall.tres` to a
Godot Sprite2D's Texture property to use one isolated region.

The low, medium and tall names describe this visual style sample. Exact
quarter/half/full gameplay height ratios have not been calibrated. The
footprints are similar, but not mathematically identical. No ramps, collision
or navigation are included. Original approved Prefab 1/2 assets are untouched.

Generation preserves the supplied textured, irregular stone art direction.
The grass meets an uneven stone rim. This is not an export of the procedural
Blender meshes. Very faint alpha noise outside the sprites is excluded by
the atlas regions. Some bright grass-edge pixels remain for visual review.
