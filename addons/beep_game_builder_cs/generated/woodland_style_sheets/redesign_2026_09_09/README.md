# Woodland Redesign

Six new source-art sheets. Originals are preserved. This batch does not redesign the complete 764-image generated library.

Open index.html to compare the cartoon and pixel versions.

## Contents

- Ground: 24 source regions per style, including fills, terrain transitions, paths and banks.
- Elevation: 12 pieces per style, including four wall patterns, directional ends, corners, three platform heights and a hill.
- Nature: 16 items per style, including trees, plants, wood and rocks.

## Production Status

These are generated source illustrations, NOT validated TileSet atlases. Do not assign a uniform cell size to the source PNGs and assume they will connect.

Before production: calibrate sprite scale and ground cells; complete missing directional corner and junction variants; verify matching connection points; check repeated texture seams; normalize pixel clusters; export alpha with clean foliage edges; add named atlas regions, pivots and collisions in Godot.

The cartoon ground sheet currently duplicates the crossroads instead of providing the requested T-junction. Both ground sheets have incomplete or ambiguous corner coverage. These remain source drafts until corrected and tested.

Suggested export targets: 32x32 pixel ground cells, 128x128 cartoon ground cells, bottom-center object pivots. These targets have not been applied to these source images.

Full prompts and original generated paths are in provenance.json.

