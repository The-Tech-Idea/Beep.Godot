# Terrain Feature Scatter

The feature views decorate the existing grid; they do not decide which cells are
forest or create another world. `TerrainWorldComponent` coordinates views,
`GridCellDataComponent` owns live features and fine water patches, and each
renderer uses native cell corners to project decorative anchors.

## Placement

`TerrainFeatureScatter` is an internal helper, not a scene component. For each
requested stamp it tests twelve seeded positions and selects the one furthest
from earlier accepted positions in that cell. Work is bounded: at most sixteen
stamps per cell, no map-wide search, node-per-tree creation or unbounded retry.

`PositionJitter` sets the total width of the candidate area in cell units.
Flat default: 0.85; isometric default: 0.30. The feature count is clamped to the
Inspector's 1-8 base plus 0-8 forest extras. These are requested counts; shoreline
constraints can reject anchors. A rejected clump can retain one valid centre;
it no longer piles all rejected stamps onto the same point. Zero spread explicitly
allows a centred stack and adds no hidden vertical offset.

Seeded choices use absolute grid cell identity. Moving view bounds does not
change a retained cell's frames, scales or positions. Native projection geometry
maps offsets onto square or elevated isometric surfaces. With the same seed,
count, spread and source, both projections select matching logical anchors.
The world assigns its seed to both feature views before surface events can
rebuild them. Hidden isometric props receive reseeds too; they no longer retain
an independent default seed when the active projection changes.

The shared fine water sampler is authoritative for acceptance. No live terrain,
feature flags, navigation records or saved patches are changed by rendering.

## Artwork

`TerrainFeatureFrameBindings` shares the existing isometric binding format with
the flat renderer: `grass,dry_grass=0,1,4`. Repeated frame indices are weights.
Bindings apply to whichever feature selects the woods sheet, including woods
fallbacks. Dedicated jungle, oasis and marsh sheets use their own frames.

The lab and standalone painted demo retain the user's `forest_trees.png` art.
Temperate foliage frames exclude apples, oranges and blossoms. Both demos use
one base sprite, one forest extra, spread 0.85 and trunk anchor (0.5, 0.86).
Visible sprite dimensions now use the shared `TerrainPropSizing` resource in every
renderer. Art profiles no longer override sizes; see `TerrainPropSizing.md` for limits.
The new pixel/cartoon styles have separate artwork, but the original ground remains.

## Verification

- `terrain_feature_grid_probe.gd`: negative origins, transformed native grids,
  crop-stable anchors/sizes, seed restoration, visibility and clearing lifecycle.
- `terrain_lab_grid_probe.gd`: world seed applied to both views during projection
  switching and after regenerating with hidden isometric props. The seed assertion
  reproduced the prior independent-isometric-seed defect before the fix.
- `TerrainWaterSurfaceSmoke`: five-anchor average nearest spacing over 64 seeds
  (0.410 cell on this fixture), zero-spread and rejected-anchor behavior, and
  matching flat/isometric dry anchors over 32 seeds on a mixed shoreline cell.
- Existing four-seed fine-water, save/restore and terrain-edit checks remain.
- `terrain_water_surface_probe.gd` renders colored frame fixtures at 0.5x/1x/2x:
  terrain binding, live binding replacement, jungle-to-woods fallback and unchanged
  live grid bytes. Tests verify the actual selected sprite pixels, not only parsing.
- `terrain_painter_visual_probe.gd` renders the real addon lab at overview and
  close range. Latest captures: `tests/output/painter_visual/overview.png` and
  `close.png`. Captures are generated test output, not source assets.

## Limits

This is cell-local decorative spacing, not global Poisson-disc sampling. Adjacent
cells can overlap canopies; artwork may extend past dry ground even when its trunk
is dry. Fine-water acceptance does not include animated surf. Physical/selectable
trees and their footprints belong to grid objects, not this renderer. The retained
tree artwork is still more stylized than the ground; cross-view art consistency and
large-map cold-rebuild cost remain separate open terrain-quality issues.
