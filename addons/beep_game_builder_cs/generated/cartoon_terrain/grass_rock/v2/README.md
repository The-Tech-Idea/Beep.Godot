# Cartoon Grass and Rock - Version 2

Status: artwork drafts, not a validated Godot TileSet.
Style: outlined hand-painted cartoon based on the supplied village reference.
Background: opaque green retained for later processing.

## Ground Sheet
File: grass_ground_sheet_green.png
Read each row left to right.
- Row 1: four borderless grass center variations.
- Row 2: north, east, south, west outer edges.
- Row 3: northwest, northeast, southeast, southwest outer corners.
- Row 4: northwest, northeast, southeast, southwest concave cutouts.

## Cliff Sheet
File: cliff_elevation_sheet_green.png
Intended roles, pending orientation and alignment correction:
- Row 1: front wall A, front wall B, side wall A, side wall B.
- Row 2: two exterior corner studies, two interior corner studies.
- Row 3: shorter wall, lowest wall, taller step plateau, lowest step plateau.

## Before Engine Use
- Normalize exact cell dimensions and connecting boundaries.
- Resolve left/right corner orientation; generated corner studies are not a verified complementary pair.
- Measure and normalize wall heights to the desired full/half/quarter ratio.
- Process backgrounds and define atlas regions.
- Check repeated joins in assembled terrain, then define Godot terrain and collision data.
No seamlessness, exact height ratio or grid alignment is claimed for these source sheets.

The earlier v1 sheet is retained unchanged. Prompts and source locations are in provenance.json.

