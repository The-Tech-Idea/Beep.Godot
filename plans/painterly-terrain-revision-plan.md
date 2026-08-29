# Painterly Terrain Revision Plan

Date: 2026-08-28

## Implementation Status

- Phase 0: complete. Bundled material textures remain opt-in; biome detail is disabled by default.
- Phase 1: complete. Base, ground-detail, water, and gameplay images are now separate explicit layers. RGB-delta alpha inference has been removed from the active render path.
- Phase 2: complete. `TerrainPaintSample` keeps terrain, water, ground-mask, cell flags, and road kind as distinct fields. The grid bridge now uses that typed path.
- Phase 2.5: partial. `GridTerrainTransitionLayerComponent` now uses Godot's batched `TileMapLayer.SetCellsTerrainConnect(...)` API when an authored TileSet terrain is supplied. The old numeric dual-grid fallback remains opt-in for a verified atlas mapping only. The supplied PNG sheets contain no TileSet/Tiled terrain metadata, so an authored `.tres` terrain resource is still required before this can be enabled visually.
- Phase 3: partial. Procedural hard-alpha ground marks are implemented. Asset-backed grass/flower stamp scatter remains pending asset licensing/source confirmation.
- Phase 4: complete. Water now has a dedicated sprite/layer and ripple material.
- Phase 5: pending. Do not copy the supplied plant or dual-grid images into distributable addon assets until their license/source terms are recorded.
- Phase 6: partial. Targeted painterly and dual-grid mask probes pass. The existing windowed render probe did not produce a completion result in this environment and needs an interactive run.

## Scope Read

This plan is based on a full read of:

- `addons/beep_game_builder_cs/ecs/terrain/PainterlyTerrainComponent.cs` lines 1-1162
- `addons/beep_game_builder_cs/ecs/terrain/GridPainterlyTerrainBridgeComponent.cs` lines 1-333
- `addons/beep_game_builder_cs/ecs/terrain/GridTerrainGeneratorComponent.cs` lines 1-267
- `tests/painterly_terrain_probe.gd` lines 1-310
- `tests/painterly_layer_capture.gd` lines 1-47
- Current painterly demo/template references found by `rg`
- Visual references supplied by the user, especially the grass terrain reference and plant assets under `C:\Users\f_ald\source\repos\The-Tech-Idea\Art\Plants`
- Gaea 2.0 architecture reference: `https://github.com/BenjaTK/gaea-fork`
- Godot terrain-generation data-flow reference: `https://github.com/SlothInTheHat/godot_terrain_generation`
- Dual-grid terrain-transition reference: `https://github.com/jess-hammer/dual-grid-tilemap-system-godot`
- Repeated-overlay shader reference: `https://godotshaders.com/shader/repeated-texture-overlay-for-tilemaps/`
- Godot `TileMapLayer` API and Godot TileSet terrain documentation, reviewed online on 2026-08-28
- Supplied transition assets under `C:\Users\f_ald\source\repos\The-Tech-Idea\Art\TileSets\Dual Grid`

Gaea is a useful architectural reference because it models procedural generation as a graph of small stages. It must not become a runtime dependency or a direct code transplant: the project needs a focused C# Godot component, not a second procedural-generation framework. Its MIT license permits reuse only if attribution and the license text accompany any substantial copied code.

The second reference demonstrates the right high-level separation of maps: altitude, temperature, moisture, biome classification, terrain choice, and object placement are distinct datasets. It is GPL-3.0, therefore it is reference-only: do not copy, adapt, or include its source in this addon.

The dual-grid reference is MIT-licensed and demonstrates an appropriate Godot structure: one logical world `TileMapLayer` plus one or more display `TileMapLayer` nodes. A 16-rule dual-grid lookup provides localized rounded borders while preserving the underlying grid. This is a fit for biome and water transitions. The supplied `Dual Grid` art already follows that visual layout: flat interiors with edge treatment around the affected region.

The repeated-overlay shader is CC0, but it is deliberately not a default visual path. A repeated overlay across every eligible tile would recreate the full-map texture and blur problem. It is suitable only as an explicitly masked, optional detail pass after correct filtering and alpha-edge validation.

## Godot 4.7 Integration Decision

The correct runtime integration is not a hand-written `mask % columns` atlas lookup. That assumes a particular 16-tile order that PNG art does not encode. It caused the visibly incorrect transition tiles in the sample.

For an authored Godot TileSet, configure a terrain set and paint each tile's terrain peering bits in the TileSet editor. The transition component then collects all cells of one logical terrain and calls `TileMapLayer.SetCellsTerrainConnect(cells, terrainSet, terrain, ignoreEmptyTerrains)` once per update. Godot selects edges and corners from that authored metadata.

Use FastNoiseLite Perlin fBm for deterministic world data, not to paint final terrain colors:

1. Sample seeded height, moisture, temperature, and fertility fields.
2. Classify the resulting values to stable logical terrain cells for gameplay.
3. Render a flat base plus masked detail stamps.
4. Send each terrain mask to its TileMapLayer in batched terrain-connect calls.
5. Update only changed chunks or an explicit bounded area, because TileMapLayer terrain updates are expensive.

This keeps visual fidelity in artist-authored tiles while keeping procedural generation deterministic and gameplay-friendly. The Grassland preset must remain grass by default; climate variants are an explicit world-style choice, not random visual color slabs.

## Current Findings

### P0 - The renderer has the wrong layer model

`PainterlyTerrainComponent.PaintSample` only carries one `Colour`, one effect flag, one edge amount, and a terrain kind. The target look needs multiple explicit layers:

- Plain biome base
- Ground detail patches
- Fine grass/flower/plant marks
- Water overlay
- Gameplay overlays such as roads, cleared land, tilled land, watered land, planted land, blocked cells
- Optional decorative prop scatter

The current code tries to recover those layers after the fact by comparing `baseColour` and `detailedColour` in `DetailOverlayPixel`. That is the root cause of the pale wash, whole-scene effects, and halo problems. A pixel color delta is not a reliable layer mask.

### P0 - The bridge mixes gameplay state into terrain color too early

`GridPainterlyTerrainBridgeComponent.SampleCell` blends cleared, tilled, watered, planted, harvest-ready, blocked, and road colors directly into the terrain color before the painter receives it.

Because of that, the painter cannot know whether a color difference means biome detail, road, water, farming state, or a base terrain change. This makes it impossible to keep the base layer plain while putting details and gameplay state in their correct visual layers.

### P0 - Alpha is being used as a side effect, not as authored layer data

`DetailOverlayPixel` sets alpha based on RGB difference from the base. This caused:

- Transparent pixels with incorrect RGB until recently patched
- Whole-scene pale or washed overlays when small color changes cross the threshold
- Detail disappearing when the delta threshold is too high
- Hard artifacts when the threshold is too low
- Water sharing the same overlay mechanism as grass/detail marks

Alpha should come only from explicit layer masks, texture/stamp alpha, or water settings. It should never be inferred from RGB difference.

### P0 - Biome detail applies as a global effect instead of region-owned detail

`EnableBiomeDetailLayers`, `BiomeDetailCoverage`, and `BiomeDetailPatchScale` are global renderer settings. The current mask limits coverage, but it still applies to every eligible terrain kind across the whole rendered map.

The desired behavior is different: a biome/detail region should be enabled for a specific part of the scene or map, then the painter should render detail only in that region.

### P1 - Grass rendering does not match the reference model

The reference image is not a uniformly textured grass image. It is:

1. A saturated, mostly flat green base.
2. Sparse broad darker/lighter organic patches.
3. Small grass tufts and flower clusters above the base.
4. No global alpha haze.
5. No repeated texture pasted across the entire map.

The current `ApplyGrassDetail` uses procedural color blending and `SpotMask`. It can create noise and blobs, but it cannot produce convincing grass tufts or flower clusters because there is no stamp or sprite layer.

### P1 - Material texture loading is still not a good default path

`MaterialTextureSet.Load` samples named textures like `grass.png`, `sand.png`, and `water_deep.png`. Missing files now fall back instead of crashing, but the whole feature remains risky as a default because it can still make the map blurry, noisy, or pale when enabled.

Material textures should remain opt-in. The default visual path should use procedural masks plus stamps/props, not full-surface sampled textures.

### P1 - `RenderFromPaintSampler` smears discrete grid states

`BlendPaintCells` bilinearly blends `PaintSample.Colour` between cells. That can be useful for terrain transitions, but it is wrong for roads, cleared cells, tilled cells, blocked cells, crops, and many gameplay overlays. These states should be rendered with explicit layer rules and masks, not blended through base terrain color.

### P1 - Water belongs in a separate water layer

Water currently uses `TerrainPaintEffect.Water`, `ApplyWater`, and the same detail sprite path. It should instead be a dedicated water layer with:

- Explicit alpha
- Edge foam mask
- Optional ripple shader only on water pixels
- No interaction with grass/detail alpha logic
- No forced change to the plain land base

### P2 - The component has too many responsibilities

`PainterlyTerrainComponent` currently owns:

- Procedural terrain generation
- Biome classification
- Texture file loading
- Texture sampling
- Pixel image generation
- Detail masks
- Water effects
- Sprite creation
- Runtime debug state

This makes every visual change risky. The fix should split configuration, sampling, rendering, and layer composition into smaller internal types while preserving a simple public Godot component for developers.

### P2 - Current tests overfit the broken implementation

`tests/painterly_terrain_probe.gd` verifies current details such as `PainterlyTerrainDetailSprite` and coverage ranges. It does catch some useful failures, but it does not assert the key user-facing rules strongly enough:

- Base layer must remain plain.
- Biome details must be limited to a mask/region.
- Roads and gameplay states must not pollute the base layer.
- Transparent detail pixels must not create dark halos.
- Grass detail should be sparse and local.
- Water should be isolated to water layer pixels.

## Target Architecture

### Public developer model

Keep the developer-facing usage simple:

- Add one `PainterlyTerrainComponent`.
- Choose a preset such as grass, desert, sand, ice, sea, rock, swamp, snow.
- Optionally connect a `GridCellDataComponent` and `GridRoadComponent` through `GridPainterlyTerrainBridgeComponent`.
- Optionally enable procedural region masks and decorative scatter.
- Call `Rebuild` or let the bridge rebuild after map changes.

### Pipeline, informed by Gaea's graph model

Keep the public inspector workflow above, but implement it internally as a fixed, inspectable pipeline rather than one monolithic pixel routine:

`World/Grid source -> biome and region masks -> base terrain -> ground detail marks -> water -> gameplay overlay -> optional prop scatter`

Each stage consumes typed data and emits only its own layer. This is the relevant lesson from Gaea's node-graph approach: masks and outputs remain separate. It directly prevents a road, grass patch, or water edge from being reinterpreted as a change to the terrain base color.

For procedural world generation, the input portion must retain separate scalar maps for altitude, temperature, moisture, fertility, and designer/gameplay region masks. Biome selection may read those maps, but it must not overwrite them. Ground detail and prop scatter then read biome plus their dedicated region masks; they must not be inferred from final pixel colors.

For discrete biome boundaries, a dual-grid transition layer reads the terrain-kind grid after classification. It writes only the 16 required edge/corner variants around a changed region. The center of a grass, desert, or water region remains the chosen plain base tile or base image.

### Internal layer stack

Replace the current base/delta approach with an explicit layer stack:

1. `TerrainBaseLayer`
   - Always opaque.
   - One plain biome base color per terrain kind unless explicitly overridden.
   - Grass base should be green and not pale.

2. `TerrainGroundDetailLayer`
   - Transparent except where an explicit mask emits marks.
   - Contains broad local patches, dune marks, pebbles, dirt patches, snow scratches, etc.
   - Never covers the whole scene by default.

3. `TerrainWaterLayer`
   - Transparent except water pixels.
   - Handles alpha, edge foam, and ripple material.
   - Does not share grass/detail threshold logic.

4. `TerrainGameplayOverlayLayer`
   - Roads, cleared land, tilled land, watered land, blocked overlays.
   - Uses cell-shaped masks or authored tile/sprite overlays depending on the use case.
   - Does not alter the base image.

5. `TerrainPropScatterLayer`
   - Optional sprite/node layer for grasses, flowers, bushes, rocks, cacti, reeds, etc.
   - Uses curated transparent PNGs or atlas regions.
   - Seeded and deterministic.
   - Density controlled per biome and per region.

6. `TerrainTransitionLayer`
   - A design-time `TileMapLayer` generated or updated from the logical terrain grid using dual-grid rules.
   - Draws only boundaries between terrain regions: grass-to-sand, sand-to-water, grass-to-dirt, and similar transitions.
   - Uses a 16-tile dual-grid atlas for rounded local corners and edges.
   - Has no full-map texture and does not modify the terrain base image.

### Structured sample contract

Replace or supplement `PaintSample` with a structured record similar to:

```csharp
public readonly record struct TerrainPaintSample(
    string TerrainKind,
    Color BaseColour,
    TerrainPaintEffect Effect,
    float WaterAmount,
    float WaterEdgeAmount,
    float GroundDetailMask,
    float PropScatterMask,
    GridCellDataComponent.CellFlags CellFlags,
    string RoadKind);
```

Compatibility can be preserved by keeping the old `PaintSample` overload, but the grid bridge should use the new structured contract.

### Region masks instead of global biome effects

Biome details should be controlled by explicit masks:

- Procedural mask from seed/noise for simple use
- Grid cell region mask from `GridCellDataComponent`
- Optional authored mask from a TileMap/NodePath later

The important rule: detail exists only where the selected mask says it exists. A global boolean should not mean "paint every matching terrain kind everywhere."

### Grass target

For grass, implement exactly this layering:

1. Base: solid green from `GrassBaseColour`.
2. Broad patches: low-density organic darker green marks with hard enough alpha to avoid haze.
3. Fine tufts: sparse small deterministic shapes or sprite stamps.
4. Flowers: very sparse white/blue/yellow clusters from stamp shapes or curated sprites.
5. Props: optional `MultiMeshInstance2D` or pooled `Sprite2D` stamps from the plant asset set.

Use the supplied plant assets as visual reference and, if allowed by project licensing, copy a small curated set into the addon/sample instead of sampling a full texture over the terrain.

## Implementation Plan

### Phase 0 - Stabilize current behavior before refactor

- Set `EnableBiomeDetailLayers` to false in default examples/templates until the layer rewrite is complete.
- Keep `UseBundledMaterialTextures` false by default.
- Remove or bypass RGB-delta-based `DetailOverlayPixel` from the visual path.
- Keep `dotnet build Beep.Godot.csproj` green.
- Keep existing old overloads compiling.

### Phase 1 - Introduce explicit render layers

- Add internal buffers for base, ground detail, water, gameplay overlay, and prop hints.
- Create layer write methods with explicit alpha inputs.
- Add `TerrainLayerStats` debug values for each layer: coverage, pixel count, alpha min/max.
- Ensure transparent pixels use safe RGB and no filtering halo.

Files:

- `addons/beep_game_builder_cs/ecs/terrain/PainterlyTerrainComponent.cs`
- `tests/painterly_terrain_probe.gd`
- `tests/painterly_layer_capture.gd`

### Phase 2 - Replace the sample contract used by the grid bridge

- Add `TerrainPaintSample` or an equivalent structured sample.
- Update `GridPainterlyTerrainBridgeComponent.SampleCell` so terrain kind, cell flags, and road kind are separate fields.
- Stop blending gameplay state into `Colour`.
- Render roads and farming states through `TerrainGameplayOverlayLayer`.
- Keep legacy `RenderFromSampler` and `RenderFromPaintSampler` compatibility for existing projects.

### Phase 2.5 - Add an optional dual-grid transition TileMap layer

- Add a `GridTerrainTransitionLayerComponent` or equivalent focused helper.
- Maintain a logical terrain-kind grid separately from the display `TileMapLayer`.
- For each terrain boundary, calculate its four-corner dual-grid mask and choose one of 16 atlas variants.
- Keep the `TileMapLayer` in the scene so developers can inspect the display layer at design time.
- Start with the supplied grass, desert, and water dual-grid sheets; do not copy them into the addon until their licensing/source is confirmed.
- Use nearest-neighbor filtering for pixel-art assets and keep authored resolution; do not upscale raster textures through the painter.
- Add a sample scene with grass, desert, and water regions proving that only their shared borders receive edge art.

Files:

- `addons/beep_game_builder_cs/ecs/terrain/GridTerrainTransitionLayerComponent.cs` (new)
- `addons/beep_game_builder_cs/ecs/terrain/GridPainterlyTerrainBridgeComponent.cs`
- `tests/examples/grid_world_dual_grid_transition_demo.tscn` (new)

Files:

- `addons/beep_game_builder_cs/ecs/terrain/PainterlyTerrainComponent.cs`
- `addons/beep_game_builder_cs/ecs/terrain/GridPainterlyTerrainBridgeComponent.cs`
- `tests/GridPlacementSmoke.cs`

### Phase 3 - Implement reference-style grass terrain

- Base layer remains flat `GrassBaseColour`.
- Add seeded broad grass patches as explicit alpha marks, not global color blending.
- Add a small procedural grass-tuft stamp set for projects without external assets.
- Add sparse flower stamps with separate density controls.
- Add controls:
  - `GrassPatchCoverage`
  - `GrassPatchScale`
  - `GrassTuftDensity`
  - `FlowerDensity`
  - `DetailRegionMode`
  - `DetailRegionSeed`

Files:

- `addons/beep_game_builder_cs/ecs/terrain/PainterlyTerrainComponent.cs`
- `tests/examples/painterly_terrain_component_example.tscn`
- `tests/examples/grid_world_painterly_demo.tscn`

### Phase 4 - Water isolation

- Render water to its own sprite/layer.
- Apply ripple shader only to the water sprite.
- Use explicit water alpha, edge foam, and water mask.
- Confirm water no longer changes grass or land overlays.

Files:

- `addons/beep_game_builder_cs/ecs/terrain/PainterlyTerrainComponent.cs`
- `tests/painterly_terrain_probe.gd`

### Phase 5 - Asset-backed optional prop scatter

- Review licensing/source metadata for `C:\Users\f_ald\source\repos\The-Tech-Idea\Art\Plants`.
- Curate small grass/flower/bush sprites, likely from:
  - `Art\Plants\plantpack\isometric tiles\grasses01.png`
  - `Art\Plants\plantpack\isometric tiles\grasses02.png`
  - `Art\Plants\plants_flowers.png`
- Import with the correct filter settings so they do not blur or pixelate.
- Add a sample scene that shows:
  - Plain base only
  - Base + local patches
  - Base + patches + plant scatter
  - Water next to grass
  - Gameplay road/cleared overlays

Files:

- `addons/beep_game_builder_cs/textures` or `tests/examples/assets`, depending on license decision
- `tests/examples/painterly_terrain_biome_gallery.tscn`
- `tests/examples/grid_world_painterly_demo.tscn`

### Phase 6 - Tests and visual acceptance

- Update `tests/painterly_terrain_probe.gd` to verify the new contract rather than current implementation details.
- Add a screenshot/layer capture test for grass reference rules:
  - Base layer color variance stays very low.
  - Detail layer coverage is bounded.
  - Detail marks are absent outside a mask region.
  - No translucent full-scene haze.
  - No black/green halo in transparent detail pixels.
  - Roads and tilled/cleared cells appear only in gameplay overlay.
- Run:
  - `dotnet build Beep.Godot.csproj`
  - `tests/painterly_terrain_probe.ps1`
  - `tests/render_scene_probe.ps1`
  - relevant `GridPlacementSmoke` checks

## Acceptance Criteria

- Grass base is a saturated flat green layer, not a pale texture.
- Enabling biome detail affects only its mask/region, not the entire scene.
- Grass details look like local terrain marks and plant/flower scatter, not a repeated full-map texture.
- No alpha haze, no black halos, and no whole-map whitening/darkening.
- Roads, cleared land, tilled land, watered land, planted land, and blocked state render as gameplay overlays and do not modify terrain base pixels.
- Water is transparent only where water exists and has its own isolated ripple/foam behavior.
- Biome boundaries are local dual-grid tiles; plain biome interiors remain plain and untextured.
- A repeated texture overlay, when enabled explicitly, is restricted by an authored/region mask and never applies to the whole map by terrain type alone.
- The component remains easy for a Godot developer to use from the inspector.
- Existing public API calls keep compiling or receive clear compatibility overloads.

## Decisions Needed Before Implementation

- Whether the addon may copy selected plant PNGs from `C:\Users\f_ald\source\repos\The-Tech-Idea\Art\Plants` into addon assets, or whether examples should reference them only locally.
- Whether the first region mask implementation should be procedural only or should also read a designer-authored TileMap/mask node.
- Whether gameplay overlays should stay as generated image layers or move to separate Godot nodes/TileMap overlays for easier designer control.
- The source/license terms for the supplied `Art\\TileSets\\Dual Grid` image sheets before any addon redistribution; until confirmed, use them only in local demos.
