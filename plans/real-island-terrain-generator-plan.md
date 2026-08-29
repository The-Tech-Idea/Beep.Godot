# Real Island Terrain Generator Plan

Date: 2026-08-28

## Goal

Replace the current `LandformMode` implementation with a real, deterministic terrain pipeline. The result must generate believable islands and archipelagos from one elevation model, rather than combining unrelated sea, land, and lake masks.

The generated world must have these visible properties:

- Ocean surrounds an Island or Archipelago world.
- An Island has one connected landmass with an irregular, natural coast.
- An Archipelago has several separated, irregular landmasses with usable sea lanes.
- Beaches occur beside ocean water, not as random inland outlines.
- Lakes are enclosed inland basins, not arbitrary noise blobs or ocean-shaped holes.
- Lowlands, uplands, rock, and optional snow derive from the same elevation values.
- Props use the final terrain data and never appear in deep or shallow water.
- The same seed and settings reproduce the same map.
- A setting such as `Land coverage 70%` produces approximately that much land, within a documented tolerance.

This plan deliberately removes the current experimental `LandmassScale`/threshold behavior, fixed radial masks, and hash-based lake placement. This is a development-stage replacement, not a backward-compatible layer.

## Current Failure

`GridTerrainGeneratorComponent` currently has three competing sources of world shape:

1. `ApplyLandform(...)` creates a shaped value from separate noise and distance formulas.
2. `SeaTerrainAt(...)` independently turns a value into ocean and shore terrain.
3. `LakeTerrainAt(...)` independently turns a hash field into inland water.

That produces visually disconnected rules: an apparent island can contain large arbitrary water cuts, an archipelago can collapse into a continent, and an authored land-size value can produce only tiny fragments.

The grid bridge and painter already consume named terrain kinds, so they should remain consumers. The generator must become the single authority for elevation, water, terrain kind, and coast classification.

## Design Decisions

### One terrain field per generated world

Create an internal immutable `TerrainField` owned by `GridTerrainGeneratorComponent`. It holds, for every grid cell:

- Normalized elevation
- Moisture and temperature
- Land/ocean classification
- Water depth class
- Coast distance or shore class
- Lake identifier, if any
- Final terrain kind

The generator builds this field once for a settings/seed pair. `GenerateTerrain`, `TerrainKindAt(Vector2I)`, and continuous renderer sampling must read the same field. Do not recompute unrelated masks inside those paths.

### Clear setting semantics

Replace `Land size %` with `Land coverage %` in the Lab and component API.

- `Land coverage %`: target proportion of map cells above sea level for Island and Archipelago worlds. A value of 70 targets 70% land, subject to a stated +/- 3% tolerance after beach cells are excluded.
- `Sea level`: controls relative water depth bands and beach width, not a second hidden coverage multiplier.
- `Lake coverage %`: maximum proportion of land cells that may become enclosed freshwater.
- `Ruggedness`: controls the strength of high-frequency elevation variation.
- `Continent scale`: controls broad coast/landmass variation.
- `Archipelago density`: controls how many island groups are expected at a given map size.
- `Seed`, `Frequency`, `Octaves`, `Lacunarity`, and `Gain`: control deterministic noise generation and must visibly affect the result.

Do not expose settings that are not connected to the generated field.

### Elevation-first world construction

Use seeded FastNoiseLite Perlin fBm fields and deterministic domain warp, evaluated at a bounded low-resolution grid. The pipeline is:

`macro continental field -> domain warp -> fBm elevation -> landform constraint -> exact sea-level selection -> drainage/lake pass -> terrain classification -> props/rendering`

The macro field defines broad continents or island groups. Perlin fBm adds coastline variation and internal relief. Domain warp shifts sample coordinates so coastlines do not read as circles, ovals, or a rectangular frame.

### Topological constraints

Apply connected-component analysis after the raw elevation field is generated:

- Island: retain the largest non-ocean component; lower all other land fragments below sea level. This guarantees one connected island without using a circular mask.
- Archipelago: retain a seeded target range of the largest components and enforce a minimum sea-lane width between them. Remove tiny fragments below a configurable minimum area.
- Ocean: flood-fill water from all map edges. Any water not connected to the edges is a lake candidate, never ocean.

This is the key difference between terrain-looking noise and a valid island world.

### Lakes from basins, not a hash overlay

Remove `LakeTerrainAt(...)` and its standalone `SmoothHashField(...)` classification path.

After ocean flood-fill:

1. Find inland low-elevation basins enclosed by land.
2. Group contiguous basin candidates into lake components.
3. Reject lakes smaller than the minimum area and lakes that touch the ocean.
4. Keep components until `Lake coverage %` is reached, ordered deterministically by basin score and seed.
5. Classify retained interiors as shallow/deep freshwater and one-cell surrounding land as the preset-appropriate shore.

The initial implementation does not need full hydraulic erosion. It must, however, use the same elevation field and enclosure test, so a lake cannot appear as a random ocean cut.

## Implementation Phases

### Phase 1 - Replace the generator data model

Files:

- `addons/beep_game_builder_cs/ecs/terrain/GridTerrainGeneratorComponent.cs`
- New internal file or nested types under `addons/beep_game_builder_cs/ecs/terrain/`

Work:

1. Introduce `TerrainGenerationSettings` with only meaningful, normalized settings.
2. Introduce `TerrainField` with arrays indexed by `Vector2I` for elevation, climate values, water state, coast distance, lake id, and terrain kind.
3. Add a settings/seed cache key and invalidate the field only when a generator setting changes.
4. Make `GenerateTerrain()` build the field first, then write final terrain kinds to `GridCellDataComponent` in one batch.
5. Make `TerrainKindAt(Vector2I)` return the stored cell result.
6. Make continuous `TerrainKindAt(Vector2)` sample the same elevation/coast field, not a separate procedural calculation.
7. Delete the experimental `LandformThresholdFor`, `ApplyLandform`, fixed edge sea rule, and hash-based `LakeTerrainAt` path.

Acceptance:

- No terrain kind is decided by an isolated secondary mask.
- A second generate call with unchanged settings reuses the cached field or has an equivalent bounded cost.
- Grid and continuous samples agree at cell centres.

### Phase 2 - Build coherent elevation for Mainland, Island, and Archipelago

Files:

- `addons/beep_game_builder_cs/ecs/terrain/GridTerrainGeneratorComponent.cs`
- New internal `TerrainFieldBuilder` if it materially reduces complexity

Work:

1. Create seeded macro, detail, and domain-warp Perlin samplers once per build.
2. Generate a normalized raw elevation value per cell from broad continental noise plus detail noise.
3. For Island mode, apply a non-circular continental falloff after domain warp, then retain only the largest connected land component.
4. For Archipelago mode, use a denser macro field plus domain warp, retain several components based on `Archipelago density`, and discard fragments below minimum island area.
5. Select the sea threshold from the sorted elevation distribution so `Land coverage %` is measured rather than guessed.
6. Re-run component analysis after threshold selection, adjust the threshold in a bounded iteration if component pruning moves coverage outside tolerance.
7. Record build diagnostics: target coverage, actual coverage, island component count, retained-cell count, and rejected fragment count.

Acceptance:

- Same seed/settings yield the same component count, coastline, and coverage.
- Different seed changes the terrain signature.
- Island mode has exactly one land component and ocean touches every map edge.
- Archipelago has at least two retained land components and ocean touches every map edge.
- At `Land coverage 70%`, actual non-shore land is within +/- 3% of 70% for supported map sizes.

### Phase 3 - Derive oceans, coasts, beaches, lakes, and elevation bands

Files:

- `addons/beep_game_builder_cs/ecs/terrain/GridTerrainGeneratorComponent.cs`
- `addons/beep_game_builder_cs/ecs/terrain/GridPainterlyTerrainBridgeComponent.cs`

Work:

1. Flood-fill below-sea-level cells from the map boundary to mark ocean water.
2. Compute distance-to-ocean or neighbor-based coast state for deep water, shallow water, and beach cells.
3. Detect enclosed basin components for lakes using the terrain field; enforce `Lake coverage %`, minimum lake area, and a deterministic order.
4. Classify land from elevation and preset:
   - ocean/deep water
   - shallow coast water
   - beach or gravel shore
   - lowland grass/desert/sand/mud
   - upland/dry grass/rock
   - optional snow/ice based on height and temperature
5. Expose water source as `Ocean` or `Lake` in the typed field for future gameplay and rendering decisions.
6. Update bridge sampling so the water layer receives the final water kind, base terrain receives the matching shore/land kind, and no water kind is inferred from final pixel color.

Acceptance:

- Lakes never touch an edge or ocean component.
- Beaches border ocean/lake water and do not appear as isolated rings.
- Rock/snow placement follows elevation rather than a random overlay.

### Phase 4 - Align terrain rendering and transitions

Files:

- `addons/beep_game_builder_cs/ecs/terrain/GridPainterlyTerrainBridgeComponent.cs`
- `addons/beep_game_builder_cs/ecs/terrain/PainterlyTerrainComponent.cs`
- `addons/beep_game_builder_cs/ecs/terrain/GridTerrainTransitionLayerComponent.cs`

Work:

1. Keep `PainterlyTerrainComponent` as a renderer; do not let its standalone `WaterLevel` generate conflicting ocean in grid-backed worlds.
2. Pass the field’s typed terrain sample to the painter for both cell and continuous sampling.
3. Render ocean and lake water in the dedicated water layer; use foam only for shoreline edges.
4. Keep land base opaque and plain; preserve local detail/props as separate layers.
5. Update transition layers from the final terrain-kind grid only. Use Godot `TileMapLayer.SetCellsTerrainConnect(...)` when a proper authored TileSet terrain resource is supplied.
6. Do not introduce global texture overlays or full-map alpha effects.

Acceptance:

- The painter cannot independently turn land into water in a grid-backed terrain world.
- Water foam appears only on detected coast/lake edges.
- Tile transition updates use final logical terrain kinds and do not alter gameplay data.

### Phase 5 - Rebuild the Terrain Lab as a real generator inspector

Files:

- `tests/examples/terrain_generator_lab.tscn`
- `tests/examples/TerrainGeneratorLabController.cs`

Work:

1. Replace the current `Land size` control with `Land coverage` and bind it to the field settings.
2. Add design-time controls for world mode, seed, map size, frequency, octaves, ruggedness, continent scale, sea level, lake coverage, and archipelago density.
3. Keep advanced controls in a design-time collapsed section; do not create HUD controls at runtime.
4. Show generated diagnostics in the existing status line: seed, target/actual land coverage, ocean coverage, lake coverage, retained island count, and generation time.
5. Add preset buttons or menu entries for `Large Island`, `Small Island`, `Sparse Archipelago`, `Dense Archipelago`, and `Mainland Coast`. Each preset sets explicit values; none are hidden code paths.
6. Retain pan and zoom so the generated coast and terrain detail can be inspected.

Acceptance:

- Changing one visible control changes the named generator property and changes the regenerated field predictably.
- The Lab opens quickly and renders only after the field is complete.
- A 70% land coverage setting visibly contains a substantial land area, not two tiny fragments.

### Phase 6 - Props, navigation, and gameplay constraints

Files:

- `addons/beep_game_builder_cs/ecs/terrain/SeededTerrainPropScatterComponent.cs`
- Any consumers of `GridCellDataComponent` terrain kinds

Work:

1. Use final field terrain kinds and water source to reject all water cells for land props.
2. Add height-aware prop palettes: rocks favor uplands/desert, trees/bushes favor grassland, reeds favor lake shore, and no terrestrial prop can appear in ocean.
3. Keep footprint validation against the final field around the placed prop, not only its center cell.
4. Confirm navigation/build placement uses the final ocean/lake/shore terrain kinds consistently.

Acceptance:

- No terrestrial prop or buildable cell is placed in deep/shallow water.
- Prop distributions remain deterministic for a seed.

### Phase 7 - Tests, captures, and cleanup

Files:

- `tests/grid_terrain_feature_probe.gd`
- `tests/grid_terrain_lake_scatter_probe.gd`
- New `tests/grid_terrain_island_topology_probe.gd`
- New `tests/grid_terrain_coverage_probe.gd`
- `tests/render_scene_capture.ps1`
- `tests/examples/terrain_generator_lab.tscn`

Work:

1. Replace tests that assert the current mask implementation with topology and coverage assertions.
2. Add deterministic tests for:
   - seed reproducibility and seed variation
   - Island: one component and edge-connected ocean
   - Archipelago: multiple components and edge-connected ocean
   - coverage tolerance at 25%, 50%, and 70%
   - lake enclosure and no lake/ocean overlap
   - coastline/beach adjacency
   - grid and continuous sampler agreement at every cell centre
   - no props on any water terrain kind
3. Add visual captures for Large Island and Sparse Archipelago at fixed seeds.
4. Run `dotnet build Beep.Godot.csproj --no-restore`, all terrain probes, and scene capture before completion.
5. Remove obsolete experimental fields, tests, screenshots, and status wording after the replacement is verified. Do not keep a legacy generator path.

## Delivery Order

1. Phase 1 and Phase 2 together: terrain field plus valid land/ocean topology.
2. Phase 3: real coasts, beaches, and basin-derived lakes.
3. Phase 4: renderer/transition alignment.
4. Phase 5: Lab controls and presets.
5. Phase 6 and Phase 7: gameplay integration, verification, and cleanup.

Do not start painter visual tuning until Phase 2 and Phase 3 produce valid logical terrain. A pretty renderer cannot compensate for invalid world topology.

## Completion Criteria

The work is complete only when the Lab can generate a 70% large island and a 70% sparse archipelago that visibly match their settings, lakes are enclosed and modest, water is coherent, props respect terrain, all generator controls influence the seeded field, and automated topology/coverage/render probes pass.
