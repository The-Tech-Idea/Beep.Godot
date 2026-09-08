# Lava Material

`addons/beep_game_builder_cs/textures/terrain/lava_ground.png` is a new opaque
1254x1254 basalt/fissure albedo. It was generated with the built-in image tool
on 2026-09-06 and copied without resampling or overwriting supplied artwork.
The palette reference was the supplied `Art/TileSets/VolcanicTilesets/volcanocTileset3.png`.
Lossless import has `mipmaps/generate=true`; shader sampling repeats with explicit gradients.

## Binding And Scope

Assign `LavaTexturePath` on `TerrainPaintedRendererComponent`. Lava uses ID 13,
rock uses 10, and only IDs 11/12 are water. The generator lab, splat demo,
generation-layers demo and painted view in `grid_world_2d_iso.tscn` bind the asset.
It is a scene resource, not a hard-coded engine texture dependency.

This is a static material, not animated molten flow, heat emission or a new
lab world-recipe option. Other projection atlases still use their existing rock
fallback for lava. Desktop OpenGL is tested; low texture-unit/mobile targets
are not yet validated. Strong repeats and the pale ordinary-rock palette still
need visual refinement. No universal seamlessness or final visual-quality claim.

## Generator And Grid Checks

The real 32x32 procedural Lava preset, seed 31415, land coverage setting 0.65,
scale rules enabled, exposed a cleanup bug: flat basalt was treated as unwanted
mountain material. Before the fix: 591 lava, 15 grass, zero rock. After the fix:
378 lava, 228 rock, 359 deep-water and 59 shallow-water cells. Themed material
survives relief flattening; live-grid data remains the renderer's source.

The start selector also excluded too few materials and could choose blocked
lava. It now excludes lava; an entirely volcanic map need not have viable starts.

```powershell
$env:GODOT_MCP_BRIDGE_AUTO_CONNECT_RUNTIME="false"
godot --headless --path . --script tests/terrain_lava_material_probe.gd
godot --path . --rendering-method gl_compatibility --script tests/terrain_lava_material_probe.gd
```

The probe verifies imported opacity/mips, IDs, negative origins, navigation and
placement changes, unchanged coast bytes after lava-to-rock edits, and distinct
GPU material pixels at 0.5x/1x/2x. Actual preset captures are written under
`tests/output/lava_material/generated-zoom-{0.35,1.0}.png`, without a debug grid.
Synthetic pure-colour checks are separate from the real-texture captures.

## Exact Generation Prompt

> Use the attached volcanic tiles ONLY as a palette/material reference, not as a layout or an image to reproduce. Create ONE square seamless repeatable 1024x1024 fully opaque top-down LAVA GROUND ALBEDO material for a polished 2D settlement/RTS terrain renderer. Fill the whole image edge-to-edge with dark warm charcoal basalt crust plates broken by irregular narrow connected molten-orange and red fissures, a few tiny brighter orange centres, no large white-yellow highlights. Roughly 85 percent cooled dark rock crust, 15 percent hot fissures. Small-to-medium irregular shapes with subdued fine rock grain, natural varied fissure widths, no brick pattern, no uniform hexagons, no large central focal point, no circular volcano or lava lake. Quiet hand-painted non-pixel-art detail, camera perfectly perpendicular, flat material without thickness, cliff sides or perspective. Keep the base dark but detailed rather than pure black. No directional cast shadows, broad bloom, fog, global glow wash, smoke, objects, rocks protruding out of the plane, flowers, water, borders, gutters, tile grid, labels, text or transparent areas. This is a single continuous surface texture, not a sprite sheet. Features must continue through opposite edges seamlessly in both axes. Match the reference's dark basalt and vivid warm fissure colours, with finer more restrained painterly surface detail.

The tool returned 1254-square pixels, not the requested 1024; the imported asset
retains the returned dimensions. The prompt is recorded as intent, not evidence
that every requested visual property was achieved.
