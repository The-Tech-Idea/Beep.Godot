# Isometric Granite Faces: Development Candidate

Open `native_faces_review.tscn` for the native Godot example. No C# terrain engine
is required. This is not a production pack or a complete cliff collection.

## Layers and Patterns

- `PlateauSurface`: existing grass mask TileSet with a separate shared surface.
- `LightFace`: `runtime/face_tiles.tres`, pattern 0, along increasing cell X.
- `ShadeFace`: the same TileSet, pattern 1, along increasing cell Y.

The two saved patterns are also available as `runtime/light_span_pattern.tres`
and `runtime/shade_span_pattern.tres`. The example places pattern 0 at cell (0,3)
and pattern 1 at (3,0), underneath a 4x4 top. Paint the face patterns on their
named layers; faces can coexist on a corner cell because the layers are separate.

These are structural patterns, not 47-mask boundary terrain. Only the authored
four-piece sequence for each face has been validated. Do not rotate, mirror,
shuffle or cyclically repeat it and assume the lighting or end seams will match.
Arbitrary-length walls still require authored end connectors and repeat sections.

## Coordinates and Metadata

The native cell footprint is 64x32. Each face region is 32x80 and has 64px visual
rise. Light/shade texture origins are (16,24) and (-16,24). The texture bounds
include the projected slope; they are not the logical footprint or rise.

Custom data records `art_face`, `art_sequence_index`, `visual_rise_pixels` and
`art_material`. These describe artwork only. No collision, navigation, terrain
height, jumping behavior or engine logical terrain is inferred from them.

Eight AtlasTexture modules share two span textures. Green-backed source artwork
is retained in `sources/`; runtime textures have calibrated alpha. No bitmap is
duplicated for each atlas region.

## Rebuild and Validation

From the Godot project root, run in this order:

1. `node tools/terrain-library/build-isometric-granite-faces.mjs`
2. Godot headless with `--script res://tools/terrain-library/package-isometric-granite-faces.gd`.
3. Godot headless with `--script res://tools/terrain-library/package-isometric-face-tiles.gd`.
4. Godot with a rendering backend and `--script res://tests/terrain_isometric_face_tiles_probe.gd`.

The Node exporter requires `sharp`, directly installed or located through
`TERRAIN_NODE_MODULES`. The final packaging step writes native-authoring metadata
into the manifest. Render reports and captures go to disposable
`generated/test/library/output/`.

Pending: visual approval, natural bottom/corner contacts, repeatable end seams,
arbitrary lengths, terrain-engine pack bindings and other materials/projections.
