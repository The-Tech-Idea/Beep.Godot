# Isometric Demo Art

The original addon's `terrain_generator_lab.tscn` and `terrain_iso_demo.tscn`
use the plain `kenney_voxel_blocks.png` / `kenney_voxel_tops.png` pair. The user
requested restoring the plain green blocks after an earlier detailed-atlas switch.
Painted terrain materials do not replace this isometric art. No source bitmap was edited.

## Geometry Contract

Both sheets contain 8 columns and 7 rows. Block frames are 111x128 pixels;
top frames are 111x64. Consequently the authored cell footprint is 111x64,
BlockLift is 32, TopLift is 0, and LevelHeight is 32. The source sides are 64 pixels
tall; the block material fits only those faces to the 32-pixel step, preserving
the top diamond's geometry and texture detail. Swapping only the texture
path without these dimensions or the frame bindings gives incorrect art or geometry.

`terrain_block_sides.gdshader` samples below the sloping front edge of the diamond
at a different vertical rate. It leaves the top untouched and retains mipmaps.
It is assigned only to block-source TileData, not flat tops. The TileSet cache
includes LevelHeight, so changing the step rebuilds the matching material.
No source bitmap is edited. The single seabed plane uses flat tops where available,
avoiding rows of submerged cliff faces.

Mipmaps must be enabled on both sheets. The top sheet previously disabled them.
After changing the import setting, verify the imported Texture2D image actually
has mipmaps; checking only the `.import` text does not validate an old cache.

## Frame Bindings

Frame numbers are zero-based, left to right then top to bottom.

| Terrain | Primary | Variants |
| --- | ---: | --- |
| Grass | 54 | 54 only (plain green top) |
| Dry grassland | 2 | 2, 2, 4, 22 |
| Desert | 3 | primary only |
| Beach sand | 24 | 24, 24, 19 |
| Tundra | 48 | 48, 48, 47 |
| Snow | 20 | 20, 20, 1, 12 |
| Ice | 5 | 5, 5, 6 |
| Jungle | 53 | primary only |
| Swamp/mud | 10 | 10, 10, 52, 27 |
| Gravel | 28 | 28, 28, 8, 16 |
| Rock | 49 | 49, 49, 29 |

Repeated entries weight a variant. This is an art binding, not biome generation.
The atlas remains stylized, block-based art. Dry grassland uses the atlas's green
blocks, not the painted renderer's dry-grass material.
Natural cliff transitions and final cross-view palette consistency remain work.
Rivers now use the shared animated water shader on elevated native-grid diamonds,
not flat water atlas frames. Obsolete water-frame exports and demo bindings were
removed. Riverbank transitions and mouth drops still need art.

`tests/examples/iso_layers.gd` now passes the original stack-height limit unchanged:
the restored stack spans 2.16 cell heights (limit 2.5). All primary terrain frames are
distinct. Both this guard and the GPU cliff probe are included in the integration
runner. These are bounded checks, not complete isometric art acceptance.

## Verification

`tests/terrain_iso_art_probe.gd` loads the actual authored lab, checks atlas
dimensions/binding agreement with the standalone demo and imported mipmaps,
renders overview/close captures, and checks gameplay-grid positions against the
isometric surface under the authored geometry. Outputs are in
`tests/output/iso_art/`. A passing capture is not final art acceptance.

`tests/terrain_iso_cliff_probe.gd` renders a synthetic top/side/adjacent-frame atlas
through actual native TileMapLayer geometry at 0.5x, 1x and 2x. It checks unchanged
top detail, reduced face area, no neighbouring-frame colour and material refresh
when switching between 54- and 28-pixel steps. Its captures are in
`tests/output/iso_cliff/`. River and grid probes separately cover surface picking.

For a stale top-sheet import, run:

```powershell
godot --headless --editor --path . --script tools/reimport_terrain_art.gd
```

This uses Godot's targeted [EditorFileSystem.reimport_files](https://docs.godotengine.org/en/latest/classes/class_editorfilesystem.html#class-editorfilesystem-method-reimport-files)
API after the initial scan. The current host's unrelated Blender importer reports
an unconfigured executable during that scan. The PNG can still be reimported
explicitly; the runtime art probe verifies the resulting mip chain independently.
The custom headless editor session also reported resource shutdown warnings.
Do not treat its success marker as a clean editor-wide health check.
