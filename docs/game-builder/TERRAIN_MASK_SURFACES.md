# Masked terrain surfaces

Opt-in implementation inspired by Jess Hammer's repeated-texture approach:
https://godotshaders.com/shader/repeated-texture-overlay-for-tilemaps/
https://github.com/jess-hammer/repeated-texture-on-tilemap-demo-godot

No third-party art is included. Existing renderer defaults and detail-tint shaders
are unchanged. These materials require intentionally authored mask tiles; they
do not convert existing painted sheets automatically.

## Authoring

1. Author the required terrain shapes/peering bits as usual. Use pure red for
   replaceable interior pixels, actual colors for preserved borders, and real
   alpha outside the tile. Green source backgrounds must be removed in runtime
   assets. Keep decorations on their own layer.
2. Duplicate the square or isometric material from the addon's materials folder.
   Assign an opaque seamless surface texture, then enable `surface_enabled`.
3. Assign it to the intended TileMapLayer's Material. It works with native layers
   without C#; engine layers require the same explicit material assignment.
4. Use nearest filtering for the mask atlas and atlas padding to avoid color-mask
   interpolation/bleeding. The original shader samples the surface nearest.
   `terrain_mask_surface_cartoon.gdshader` instead samples surfaces linearly while
   retaining nearest atlas sampling. Assign it explicitly for cartoon candidates.
   Its optional blue secondary mask replaces dirt through `secondary_texture`
   when `secondary_enabled` is true. Borders and alpha remain unchanged.

The entire RGB mask is checked before item modulation. White/yellow highlights
are not masks. Original alpha and item modulation remain authoritative; surface
texture alpha is deliberately ignored. Use opaque surface textures, not grass
decoration sprites. No global color is reserved unless this material is assigned.

## Coordinates

The material repeats across world coordinates rather than per-tile UVs. Square
defaults repeat every four 64px cells. Isometric defaults repeat every four cells
along axes (32,16) and (-32,16), for an untransformed 64x32 diamond grid. This is
a material-plane basis, not a replacement for Godot's native cell placement.

For moved maps, set `surface_origin` to the shared world origin. For rotated/scaled
maps, set `surface_u` and `surface_v` to the rows of the inverse world-space
repeat basis. Camera panning/zooming does not change these world coordinates.
Maps moved without updating origin/basis slide through the texture. Adjacent
layers need the same origin/basis.

For automatic opt-in binding, add a Node2D with
`ecs/terrain/TerrainSurfacePlane.gd` under the map root. Set its projection,
cell size (64x64 square or 64x32 isometric), repeat count and `layer_paths`.
Keep this node at the shared material origin and bind all adjoining surface
layers to it. It follows the map's world transform, including rotation and
nonuniform scale, while the camera remains independent. It duplicates each
bound material once so unrelated users of the original resource are unchanged.
Missing/incompatible layers or a singular plane reject the whole update group
and expose a configuration warning. No renderer defaults are changed.

## Status

Plain grass/dirt source candidates and square/isometric native scenes are in
`generated/dev/cartoon/ground_masks/staging/`, described by
`surface_candidates_v1.json`. They contain no baked decoration. Full-bleed
opaque material textures do not need chroma-key padding; isolated source sheets
retain green backgrounds. Four-cell repeats now have 512px cartoon master and
256px runtime exports. Both projection scenes reference the same intrinsic
material-plane surface resources while retaining separate projection masks.

The GPU render probe passes camera-translation stability for diagnostic and
actual surface candidates in both projections. It captures native/2x images
under disposable test output. Edge statistics do not flag a large discontinuity
in the new sources, but do not establish visual approval or full seam coverage.

Actual surface candidates also pass GPU comparisons after splitting alternating
columns across two layers, and after map translation/rotation/nonuniform scale
with the camera compensated. Both comparisons have zero changed samples.
This does not replace testing every rendered mask join or elevated connectors.

Resource loading and square/isometric coordinate contracts are tested by
`tests/terrain_mask_surface_probe.gd`. Headless resource tests do not establish GPU
rendering, transparency, seam or visual acceptance. Actual 47-mask artwork,
matched surface textures and rendered showcases are still required. Do not
promote these presets or existing art as a completed production terrain pack.
# Painted Cliff Surfaces

`TerrainSurfacePlane.layer_paths` accepts explicit Sprite2D and AnimatedSprite2D
bindings as well as TileMapLayers. TileMapLayer bindings still require matching
projection and cell dimensions; sprite bindings use the declared plane basis.
Unsupported canvas nodes are rejected before any group material is changed.

`terrain_masked_art_surface.gdshader` samples a separate grayscale `surface_mask`.
White selects the shared `surface_texture`; black preserves original artwork.
The original alpha and item tint remain intact. Keep the mask registered to the
source texture. The granite prototype mask selects plateau grass, not rock or
the authored soil seam. Its original source PNG is retained unchanged.
