# Mountain and Hill Template Collection

Seven themes, nine separate sprites per theme, 63 sprites total. Every theme
contains transparent PNGs, Godot AtlasTexture `.tres` resources, a manifest,
and a preview. `all_themes_preview.png` displays the complete collection.

## References

- `Art/TileSets/ForestTileSet/Tilemap_color5.png`: top, surrounding edge,
  front wall and side cliff relationships. Its exact 17-piece grid and bushy
  pixel texture were not copied into these complete prefab plates.
- `Art/TileSets/isometric Cliff and Mountain Tileset Atlas.jfif`: primary
  painted cliff style, rock formations, material transitions and volume.
- `Art/TileSets/Mountain Cliff Terrain Tile Atlas.jfif`: additional material
  examples for the different terrain themes.

The source references were not modified. Earlier approved prefab packs
were not replaced. This collection is a new image-generated interpretation
of the supplied templates, rather than crops of their original sprites.

## Contents

Themes: grass/granite, grey rock, sandstone, volcanic basalt, meadow hill,
red rock mesa, alpine snow.

Each theme has:

| Asset | Purpose |
| --- | --- |
| `prefab_1_plate_large` | Rounded plateau, nominal full cliff height |
| `prefab_1_plate_medium` | Rounded plateau, nominal half cliff height |
| `prefab_1_plate_small` | Rounded plateau, nominal quarter cliff height |
| `prefab_2_plate_large` | Wide plateau, nominal full cliff height |
| `prefab_2_plate_medium` | Wide plateau, nominal half cliff height |
| `prefab_2_plate_small` | Wide plateau, nominal quarter cliff height |
| `hill_round` | Broad hill with continuous sloping sides |
| `hill_square_sloped` | Squarish hill with sloping sides |
| `hill_small` | Small rounded hill |

There are no embedded ramps, paths, castles or assembled floating tiers.
These plates are separate placement assets, not automatically assembled
mountains or seamless terrain tiles.

## Godot Use

Assign the desired `.tres` to a Sprite2D's Texture property, or use its
standalone PNG. AtlasTexture resources share the corresponding PNG in
`source_sheets/`. Keep that folder when copying the collection.

The manifest records original atlas rectangles and approximate bottom-center
placement pivots. The nominal full/half/quarter ratios are requested visual
targets, not measured elevation data. Artist-generated footprints and top
depths vary slightly between materials. Do not use these values directly
for collision, jump calculations, automatic stacking or tile terrain matching.
Collision, navigation, calibrated attachment points and seamless tile edges
are not provided by this art collection.

## Packaging and Validation

Run `python tools/package_template_terrain.py` from the project root to
rebuild cropped PNGs, resources, manifests and previews from the local sheets.
The script does not repaint, key backgrounds or stretch the artwork.

Validated: all seven sources have real alpha transparency; each contains
three distinct rows with three sprite regions; all 63 regions fit their
source sheets. Preview images use an opaque dark background for inspection.
Godot runtime import and gameplay placement have not been tested here.
