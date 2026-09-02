# Terrain Engine — Developer Guide

The connective guide to the `Terrain*` classes in `addons/beep_game_builder_cs/ecs/terrain/`. Every file also has its own reference page in this directory (`docs/terrain-engine/<FileName>.md`); this document explains how they fit together, in the order a map actually comes into being. Written against the source tree on 2026-09-02.

The engine is three layers with one rule between them: **generation decides, renderers draw, data layers publish**. Nothing downstream of the generator ever decides what terrain is — twelve renderers and the gameplay grid all read the same generated field, which is why four projections of one seed can never disagree about the world.

## The big picture

```mermaid
flowchart TB
    subgraph Axes["World axes (what a designer chooses)"]
        TWC[TerrainWorldComponent<br/>MapType, MapSize, WorldAge,<br/>Temperature, Rainfall, SeaLevel,<br/>ResourceLevel, Seed, Projection]
        SHAPE[TerrainShapePresets<br/>Continents, Pangaea, Archipelago,<br/>IslandChain, OceanWorld]
        SETUP[TerrainMapSetup<br/>axis multiplier tables]
    end

    subgraph Gen["Generation (pure, deterministic, cached)"]
        TGC[TerrainGeneratorComponent<br/>owns every setting + the cached field]
        SET[TerrainGenerationSettings<br/>immutable record = cache key]
        FB[TerrainFieldBuilder<br/>runs the stage pipeline]
        GTF[GeneratedTerrainField<br/>read-only result, two resolutions]
    end

    subgraph Views["Renderers (draw, never decide)"]
        P[TerrainPaintedRendererComponent]
        T[TerrainTileRendererComponent<br/>+ TerrainTransitionLayerComponent per biome]
        I[TerrainIsometricRendererComponent<br/>+ TerrainIsometricFeatureRendererComponent]
        A[TerrainIsometricAutotileRendererComponent]
        X[Feature / Relief / Resource /<br/>MapOverlay renderers + prop scatter]
    end

    subgraph Publish["Published map (no generator needed at runtime)"]
        DL[TerrainDataLayersComponent<br/>terrain, resource, feature, relief tile data<br/>+ native collision and navigation]
    end

    SHAPE --> TWC
    SETUP --> TWC
    TWC -->|ApplyMapSetup + Build| TGC
    TGC --> SET --> FB --> GTF
    GTF --> P & T & I & A & X
    GTF --> DL
```

## Generation is a pure function

`TerrainGenerationSettings` is an immutable record built from ~40 exported properties on `TerrainGeneratorComponent`. Two equal settings always produce an identical world, so the generator caches one `GeneratedTerrainField` and rebuilds it only when any setting changes. Renderers on a hot path call the internal `ResolveField()` once per rebuild and use the field's O(1) accessors, rather than paying the settings rebuild-and-compare per cell.

**The ownership contract** (guarded by `tests/addon_contract_scan.ps1`): when a scene drives the generator through `TerrainWorldComponent`, sixteen generator settings are derived from the axes and overwritten on every `Build()` — the eleven `ApplyMapSetup` documents plus `BoundsSize`, `Seed`, `ResourceSet`, `UseClimateBiomeMaps` and `UseScaleRules`. Set the **axes** on the world component, or drive the generator directly and set its exports — never both.

## The stage pipeline

`TerrainFieldBuilder.Build` runs the stages on a shared mutable `TerrainWorld` (struct-of-arrays, at sub-tile *sample* resolution), then reduces to gameplay tiles. Order is load-bearing; each stage reads only what earlier stages settled.

```mermaid
flowchart TB
    L[1 TerrainLandmassStage<br/>grow N separated masses to the coverage target] --> W[2 TerrainWaterStage<br/>carve lake basins, classify ocean vs lake by border reachability]
    W --> E[3 TerrainElevationStage.Apply<br/>coast distance + ridged fractal height]
    E --> ER[4 TerrainErosionStage<br/>stream-power incision + hillslope diffusion, 12 passes]
    ER --> EC[5 TerrainElevationStage.Classify<br/>hills/mountains as percentiles of the eroded field]
    EC --> C[6 TerrainClimateStage<br/>latitude temperature, moisture, rain shadow, dry belts]
    C --> R[7 TerrainRiverStage<br/>D8 drainage network via TerrainFlow, width from accumulation]
    R --> S[8 TerrainShadingStage<br/>hillshade from the elevation gradient]
    S --> B[9 TerrainBiomeStage<br/>Whittaker table -> terrain kind, beach and lake-shore bands]
    B --> CO[10 TerrainCoherenceStage<br/>majority smoothing + dissolve undersized biome regions]
    CO --> TR[11 TerrainTileReductionStage<br/>samples -> one value per gameplay tile]
    TR --> CT[12 TerrainContinentStage<br/>flood-fill landmass ids on the tile grid]
    CT --> SC1[13 TerrainScaleConstraintStage.ApplyTerrain<br/>drain oversized/small lakes, level lone relief, clear short rivers]
    SC1 --> RS[14 TerrainResourceStage<br/>catalog-weighted resources per tile]
    RS --> F[15 TerrainFeatureStage<br/>woods/forest/jungle/marsh/oasis from a vegetation field]
    F --> SC2[16 TerrainScaleConstraintStage.ApplyFeatures<br/>thin lone feature clumps]
    SC2 --> SP[17 TerrainStartPositionStage<br/>fair, separated, continent-spread starts]
```

Support pieces the stages share: `TerrainNoiseSet` (ten seeded FastNoiseLite channels), `TerrainFlow` (the one D8 drainage network erosion and rivers both use), `TerrainGeometry` (components, BFS distance, percentiles, the shared `Hash01`), and `TerrainScaleRules` (climate span and minimum biome region derived from map size).

Two resolutions matter throughout. The sample field (`TopologySamplesPerCell`² samples per tile, capped at ~1.25M samples) is why coastlines curve inside a tile; `TerrainTileReductionStage` collapses it to the per-tile values a game paths and builds on, taking terrain from the samples that agree with the tile's winning relief band so a tile can never be "snowfield on level ground".

`TerrainMode.Plain` bypasses the stages entirely and fills both resolutions with one preset kind.

## Renderers

All views share `TerrainLayers` — the one stack (seabed, sea, ground, hills, mountains, summits, props, markers) with its z-index scheme — plus `TerrainTextures` (one loader, mip chains guaranteed), `TerrainAuthoring` (`EnsureLayer` creates/reuses/adopts TileMapLayers so generated maps are saved with the scene), `TerrainCoastField` (the shared signed-distance-to-waterline texture every water shader reads), and `TerrainShaderSurface` (the blank one-tile TileSet a per-pixel shader paints on).

| Projection | Renderer | How it draws |
|---|---|---|
| Painted | `TerrainPaintedRendererComponent` | One shader-blended surface (Factorio-style splat): terrain ids + hillshade + coast field uploaded as textures to `terrain_splat.gdshader`. |
| Tiles | `TerrainTileRendererComponent` | One dual-grid autotiled `TerrainTransitionLayerComponent` + TileMapLayer per biome the map actually contains, stacked by `TerrainLayers`; optional shader sea over the water tiles. |
| Isometric | `TerrainIsometricRendererComponent` | Stacked block layers per elevation level, seabed by BFS water depth, summits above a measured height floor, and the same water shader on an isometric surface. `TerrainIsometricFeatureRendererComponent` stamps vegetation per level so cliffs occlude correctly. |
| IsometricAutotile | `TerrainIsometricAutotileRendererComponent` | Hands runs of cells to Godot's `SetCellsTerrainConnect` against an authored isometric TileSet with painted peering bits. |

Flat-view companions: `TerrainFeatureRendererComponent` (batched tree stamps), `TerrainReliefRendererComponent` (hill/mountain sprites), `TerrainResourceRendererComponent` (icon sheets per resource set), `TerrainMapOverlayComponent` (resource markers + start rings), `SeededTerrainPropScatterComponent` (deterministic prop stamps). `TerrainWorldComponent.Draw` shows exactly the renderers a projection uses and rebuilds them with the built size — a renderer left out of that dispatch is not "left alone", it keeps whatever the last projection did to it.

Standalone authoring tools, not part of the pipeline: `MountainPrefabGeneratorComponent` (instantiates an authored mountain prefab from a manifest), `MountainTileMapLayerGeneratorComponent` (paints a deterministic mountain footprint), `TextureElevationTileSetGeneratorComponent` (bakes an elevated-terrain atlas from textures).

## The published map

`TerrainDataLayersComponent` mirrors the generated field into four invisible TileMapLayers — terrain, resource, feature, relief — whose tiles carry custom data (`terrain`, `resource`, `feature`, `relief`, `is_water`, `passable`, defined once in `TerrainTileSets.Cell`). A game asks cells about themselves through Godot's own `GetCellTileData`/`GetCustomData`, with no generator node required at runtime. The terrain layer also carries **native** per-ground physics and navigation polygons (`TerrainTileSets.DefineBody`/`ShapeCell`): land, water and steep each collide and navigate on their own layer, so whether water stops a character is the game's collision-mask decision, never the map's.

```mermaid
flowchart LR
    GTF[GeneratedTerrainField] --> DL[TerrainDataLayersComponent]
    DL --> TL["TerrainData layer<br/>kind + is_water + passable + body"]
    DL --> RL["ResourceData layer"]
    DL --> FL["FeatureData layer"]
    DL --> RE["ReliefData layer"]
    TL & RL & FL & RE -->|GetCellTileData| GAME[gameplay code / GridResourceScatterComponent]
```

## Editor authoring

Every component is `[Tool]`. `TerrainWorldComponent`'s **Generate map** button builds in the editor; `TerrainAuthoring.Adopt` gives every generated node the edited scene root as owner, so the map is saved with the scene, hand-editable, and shipped. `BuildOnReady` is deliberately ignored in the editor so opening a scene never overwrites an authored map.

## In-scene wiring (the lab pattern)

`terrain_generator_lab.tscn` is the reference: `TerrainLabComponent` (pure UI binder) → `TerrainWorldComponent` → `TerrainGeneratorComponent` → renderers, with `TerrainWorldCameraComponent` framing the result through `PreviewExtent()`/`StartPositionView()` and `TerrainWorldStatusComponent` writing `StatusLine()` to a label. All three of those read the world component, never the renderers, so a game can build its own creation screen on the same component.

## Verification

```powershell
dotnet build Beep.Godot.csproj --no-restore
powershell -ExecutionPolicy Bypass -File tests/addon_contract_scan.ps1
powershell -ExecutionPolicy Bypass -File tests/terrain_guards.ps1 -GodotCommand '<godot-mono-4.7>'
powershell -ExecutionPolicy Bypass -File tests/renderer_reporting_probe.ps1 -GodotCommand '<godot-mono-4.7>'
```

`tests/examples/*.gd` hold the falsifiable per-behavior guards (biomes, landmass counts, resources, renderer reporting); the four `grid_terrain_*_probe.ps1` scripts cover topology, features, lake scatter, and the transition mask table.
