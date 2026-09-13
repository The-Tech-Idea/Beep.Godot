# Lake depth candidate

Open `square/lake_depth_review.tscn` or `isometric/lake_depth_review.tscn`
in Godot. These standalone development scenes do not require the C# terrain
engine and do not change existing maps or renderer defaults.

## Painting

- Keep Water, Depth, SurfacePlane and CoastalDepthBinding together. Despite the
  retained node name, its script is the lake-specific `TerrainLakeDepthBinding`.
- Paint `shallow_lake` on Depth using native terrain Connect/Path tools. Erasing
  reveals deeper water; a shallow shoreline band remains against grass banks.
- Paint lake outlines on Water. Island holes, coves and bank corners use the
  prepared 47-configuration native set. The two layers share the same transform.
- Invalid land, missing-water or incompatible-resource edits remain available
  for correction. The binding disables its derived field and restores the
  original lake appearance rather than deleting paint.
- Depth is visual only. It does not infer navigation, collision, jumping or
  logical gameplay elevation. Apply-to-engine mapping remains a separate gate.

Depth tiles are authoring masks, not an opaque visual overlay. The shader samples
native cell-region fields for both depth and shore masks. Do not derive those
coordinates from Godot's padded runtime atlas UVs. Camera movement and grass
texture repeat settings must not move or rescale the lake field.

The existing 16-frame, 1.2-second local lake-ripple frames are preserved pixel for
pixel. Depth changes their shading; it adds no sea swells, surf or separate
animation clock. Shore and depth fields default to a 1024-cell side limit and
are rebuilt after native changes. Large-map snapshot cost and chunking have not
been validated. River/depth connectors and sand/rock banks are not supplied yet.

## Provenance and status

`manifest.json` records the original lake-ripple resources and the reused
geometric depth masks with hashes. Runtime resource copies are packaged locally;
no original artwork is replaced or deleted. The grass surface and shared scripts
still come from development dependencies, so this is not a production pack.

The source image remains in the earlier lake pack. No new painted sheet or
duplicate alternative art was generated. Appearance, geological expansion,
river contacts, production dependencies and engine integration remain pending.

## Rebuild and checks

From the project root, after the original lake and shared-depth resources exist:

```text
godot --headless --path . --script res://tools/terrain-library/package-lake-depth.gd
godot --headless --path . --script res://tests/terrain_coastal_depth_native_probe.gd -- --lake
godot --path . --rendering-method gl_compatibility --script res://tests/terrain_lake_depth_render_probe.gd
```

Reports and captures go to disposable `generated/test/library/output/`.
Native checks cover 650 bank/depth neighborhoods per projection, role rejection,
invalid paint, size limits and save/reopen. Render checks cover original frame
preservation, depth contrast, erasure, fixed banks/alpha, camera/repeat anchoring,
motion and deep-interior samples. Visual inspection is still required: motion
counts alone initially missed a block-shaped atlas-coordinate artifact.
