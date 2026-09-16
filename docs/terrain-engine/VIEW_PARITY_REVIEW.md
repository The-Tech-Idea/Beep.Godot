# Terrain view parity review (2026-09-15)

Date: 2026-09-15. Scope: the four `TerrainProjection` views — Painted (`TerrainPaintedRendererComponent`),
Tiles (`TerrainTileRendererComponent` + `TerrainTransitionLayerComponent`), Isometric
(`TerrainIsometricRendererComponent`, block stacks) and IsometricAutotile
(`TerrainIsometricAutotileRendererComponent`) — and every companion the world component drives:
features, isometric features, relief props, resource icons, map overlay, data layers, collision,
streaming and publication. Read as source, capability by capability; every claim below carries
the file:line it was read at. This is a review of what each view can say about the same world,
not a visual-quality judgement; the painter's visual items stay in
`plans/TERRAIN_VIEW_GRID_INTEGRATION.md`. The items it produced are VIEW-01 … VIEW-14 in
[the 2026-09-15 plan set](../../plans/terrain-grid/README.md#terrain-rendering-review-and-map-gameplay-features-2026-09-15).

Paths are under `addons/beep_game_builder_cs/ecs/terrain/` unless written otherwise.

## Foundations to retain (one owner, shared by every view)

`TerrainLayers` (the z-stack), `TerrainWaterMaterial` (the one uniform writer, DUP-02),
`TerrainCoastField` (the shared coast distance field), `TerrainShaderSurface` (blank tile sets for
shader surfaces), `TerrainTextures` (one loader, mip chains), `TerrainFeatureSheets`,
`TerrainFeatureScatter` + `TerrainGeometry.HashInt/VariantSalt`, `TerrainPropSizing`,
`TerrainLibraryPainter`/`TerrainLibraryPack`, `TerrainRendererComponent` (rebuild coalescer,
DUP-01), `TerrainPropResidency`, `TerrainKindCatalog` (DUP-13), `TerrainAuthoring`,
`TerrainWorldCameraComponent` (projection-agnostic framing through `PreviewExtent`).

## Parity matrix

F = full, P = partial, — = none. `Draw()` in `TerrainWorldComponent.Drawing.cs` shows exactly the
renderers a projection uses. As reviewed, `flat = Projection is not (Isometric or IsometricAutotile)`
(`:36`) gated every flat companion. Since VIEW-01 (implemented 2026-09-15) the gate is
`gridBound = Projection is not Isometric`, so the four companion rows below are F under
IsometricAutotile too. The rows keep the review-time evidence and are annotated.

| Capability | Painted | Tiles | Isometric | IsometricAutotile |
|---|---|---|---|---|
| Base ground per kind | F shader splat, one texel per cell (`TerrainPaintedRendererComponent.cs:431,486`) | F one 15-piece layer per biome present (`TerrainTileRendererComponent.cs:584,625`) | F one frame per kind (`TerrainIsometricRendererComponent.cs:1351`); unmapped kind = hole (`:472`) | P authored `TerrainBindings`; unbound kinds invalidate the build (`:485`) |
| Transitions | F shader blend (`:395-399`) | F 15-piece dual grid (`TerrainTransitionLayerComponent.cs:253,276`); pack path uses terrain sets (`TerrainLibraryPainter.cs:84`) | — hard block borders (by design) | P `SetCellsTerrainConnect` (`:469`); the shipped TileSet has no peering bits, so the lab runs `UseTerrainConnections = false` |
| Sea | F composited (`:405-421`) | P only with `WaterShaderPath` (`:458-504`); **cleared under a LibraryPack** (`:344`) | F overscanned `Polygon2D` + banded seabed (`:996-1044,717-724`) | **—** at review; P since VIEW-04 — same contract as Tiles (`WaterShaderPath`, cleared under a pack) |
| Lakes | F own lake map + per-cell shore width (`:533,377,497`) | P `lake_opacity` only (`:543`) | P `lake_opacity` only (`:1137`) | — |
| Rivers | P water cells | P water cells | F **only view with river geometry**: `IsoRivers` diamonds (`:439-450,1046`) | — |
| Water depth | P shore distance (`deep_tiles/shallow_tiles`) | P same | F private BFS at review (`:669-709`: "Depth is not something the generator records"); since VIEW-05 (2026-09-16) the depth comes from the same coast field as the other two, `ReadWaterDepth` reading `TerrainSeaSurface.CellDistances` | — |
| Foam / surf dials | F (`:98-142`) | P all dials, **defaults diverge** from Iso, admitted in code (`:126-134`) | F (`:187-214`) | — |
| Foam / surf dials *since VIEW-04* | F | F | F | F — one `TerrainWaterLook` on the world, pushed to all four; no renderer exports a dial |
| Beach | P composites its **own** sand band from the coast field (`:380-391`, a recorded drift incident) | F sand biome layer last so it meets the sea (`:589-591,609`) | F sand frame (`:88`) | P sand binding |
| Elevation / relief | — hillshade only (`:516-531`) | — z-raise only (`TerrainLayers.cs:144`) | F 5-tier stack (`TerrainLayers.cs:44-59`), summit tier = top 45 % (`:636-661`) | P LibraryPack profiles only, authoring-time (`TerrainLibraryPack.Elevation.cs:18`) |
| Cliffs / sides | — | — | F sides where a neighbour is lower (`:605-620`) + `terrain_block_sides.gdshader` | — |
| Ramps / roads / day-night / fog | — in every view: ramps FEAT-05 (`terrain_ramp_direction` read, never written); roads drawn by `GridRoadComponent`, `shaders/terrain_roads.gdshader` unbound; `ecs/atmosphere/DayNightCycleComponent.cs` unbound; fog FEAT-03 | | | |
| Hillshade | F `shade_map` (`:400,523`) | — | — | — |
| Vegetation | F `TerrainFeatureRendererComponent` (z `ZForProps(Ground)`) | F same | F `TerrainIsometricFeatureRendererComponent` per level (`:331`) | **—** at review (`Drawing.cs:36,49,106`); F since VIEW-01 |
| Relief props | F `TerrainReliefRendererComponent` | F | — (blocks are the relief, `Drawing.cs:164`) | — at review; F since VIEW-01 |
| Resource icons | F `TerrainResourceRendererComponent` (`:216`) | F | — (`Drawing.cs:171`) | — at review; F since VIEW-01 |
| Survey overlay / start rings | F `TerrainMapOverlayComponent` (`:39,206-218,236-245`) | F | — (`Drawing.cs:180`) | — at review; F since VIEW-01 |
| Structures | — | F `TerrainStructureLayerComponent` (`:36-90`) | — | F (`TerrainWorldComponent.Structures.cs:28-30`) |
| Streaming (> 65,536 cells) | F `TerrainSurfaceStreamingComponent` (`TerrainShaderSurface.cs:108-117`) | P water layer only (`:493`) | — full-grid loops (`:424-508`) | — |
| Async / time-sliced build | F off-thread snapshot + coast job (`:176-219`; `Generation.cs:154-167,208-239`) | — synchronous (`:268-312`) | — synchronous (`:371-512`, "compounds hardest") | F `IEnumerator` + `CellsPerFrame` (`:75,346-371`), publication handshake (`Generation.cs:241-272`) |
| Live cell edits | F chunk texel rewrite + coast window (`:340-361,460-484`) | F four dual cells per edit (`TerrainTransitionLayerComponent.cs:330-352`) | — **any in-bounds change = full rebuild** (`:312-316`) | P pack path incremental (`:167-197`); bindings path full rebuild (`:195`) |
| Save / load | recipe only (`TerrainWorldComponent.cs:429-448`); `Projection` is not saved | | | |
| Collision / navigation binding | F synthetic `LogicalGrid` (`:640-647`) | F synthetic `LogicalGrid` (`:50-57`) | F renderer as `ElevatedTerrainPath` (`Drawing.cs:246-250`) | F real `IsoTerrain` (`:133-140`) |
| Editor edit session | — | F | — | F (`addons/beep_game_builder_cs/TerrainLibraryInspector.cs:11-12`) |

## Second owners

| # | Duplicate | Evidence | Verdict |
|---|---|---|---|
| D1 | Two feature renderers with near-identical stamp math, two clump rules, two anchor conventions | `TerrainFeatureRendererComponent.cs:265-310` vs `TerrainIsometricFeatureRendererComponent.cs:338-369,66-73,367` | VIEW-03 |
| D2 | Three per-view sea bindings; DUP-02 unified the material, not the binding or the defaults | `TerrainTileRendererComponent.cs:458-568` vs `TerrainIsometricRendererComponent.cs:996-1162` vs `TerrainPaintedRendererComponent.cs:405-421`; divergence admitted at `TerrainTileRendererComponent.cs:126-134` | VIEW-04, **implemented 2026-09-16**: `TerrainWaterLook` owns the dials and textures, `TerrainSeaSurface` owns the coast/material/geometry lifecycle, IsometricAutotile gained a sea |
| D3 | Ten tile-size owners; `Drawing.cs:58-63` re-derives a flat size, writes it into the data layers (`:76,80-81`) and exposes it again as `FlatViewTileSize` (`:330-332`) | painted `TileSize` (`:56`), tiles `AtlasTileSize` (`:58`), iso `CellSize` (`:53`), IA layer `TileSet.TileSize` (`:155`), data layers `TileSize` (`:41-44`, an atlas-tile size), feature/relief/resource/overlay `TileSize` (`:37`, `:37`, `:69`, `:27`), grid `TileSize` export | VIEW-02 |
| D4 | Two water-depth notions: coast field distance vs the iso BFS + `SeabedDepth` bands | `TerrainIsometricRendererComponent.cs:669-709,717-724` | VIEW-05, **implemented 2026-09-16**: `TerrainCoastField.CellDistances` decodes the R channel per cell, `MeasureWaterDepth` is deleted, `SeabedFrameFor` and the bands are untouched |
| D5 | Two beach owners: the painter's coast-field band vs the sand cell kind the beach stage assigns | `TerrainPaintedRendererComponent.cs:380-391` | VIEW-07 |
| D6 | Two live-water reconstructions | `TerrainCoastField.cs:624` vs `:632` | DUP-15, **landed 2026-09-11** — not an item |
| D7 | Two resource drawers: icon sheets vs the overlay's coloured discs, both wired by the world (the lab wired only the overlay) | `TerrainResourceRendererComponent.cs:212-269` vs `TerrainMapOverlayComponent.cs:177-234,344-352` | VIEW-13, **resolved 2026-09-15**: the icon renderer is the one drawer |
| D8 | Two transition mechanisms in one component; the tile renderer hard-codes the manual one, the terrain-set half has one user | `TerrainTransitionLayerComponent.cs:223-247` vs `:301-312`; `TerrainTileRendererComponent.cs:768-771`; `terrain_generation_layers_demo.tscn:65` | VIEW-08 (the connect-and-verify core; the two art contracts stay) |
| D9 | Two elevation notions: generator `terrain_elevation` vs LibraryPack `terrain_library_elevation`, unmapped | `ITerrainSurfaceData.cs:36` vs `TerrainLibraryPack.Elevation.cs:10,18` | VIEW-09 |
| D10 | Two minimaps | `ecs/grid/ui/GridMinimapComponent.cs` (world overview with a terrain bake) vs `ecs/ui/MinimapComponent.cs` (player-centred blip radar) | different facts; ENH-13 owns the grid minimap — not an item |
| D11 | Two "is water" paths inside the painter | `TerrainPaintedRendererComponent.cs:506-513` | the two inputs `ReconstructWater` takes by design (cell kind, sub-cell patches) — not an item |

## Generation → render contract

**Published at cell resolution** (`GeneratedTerrainField`): `TerrainAtCell:99`, `InlandTerrainAtCell:100`,
`BeachWidth/LakeShoreWidth:35-36`, `WaterSourceAtCell:103` (ocean|lake|river), `ContinentAtCell:112`,
`ResourceAtCell:115`, `LiquidResourceAtCell:118`, `Underground*:121-127`, `ReliefAtCell:130`,
`ElevationAtCell:133`, `FeatureAtCell:136`, `StartPositions:95`; sub-tile `TerrainAtPosition:141`,
`IsWaterAtPosition:144`, `WaterPatchAtCell:147`, `LakePatchAtCell:152`, `WaterFractionAtPosition:172`,
`ShadeAtPosition:187`. **Live** (`ITerrainSurfaceData.cs:7-11`) exposes five members over the
metadata keys `terrain_relief`/`terrain_elevation`/`terrain_water_source`/`terrain_feature`
(`:26-43`); generated key set `GridCellDataComponent.cs:682-683`.

**Generated but drawn by no view:** river flow direction and accumulation (`TerrainFlow.Accumulate`,
`TerrainFlow.cs:30`; consumed from scratch buffers at `TerrainRiverStage.cs:60-66` and overwritten by
later stages); water depth (never stored — at review the iso view re-derived it; since VIEW-05 every
view reads it from the coast field, which is where it has always been measured); climate fields (collapse into
terrain kind); `terrain_shade` (a generated live key the painter ignores for live maps,
recomputing a coarser cell gradient at `TerrainPaintedRendererComponent.cs:522-524`, and the other
views never read); continent ids (data layers only); `BlendedBaseColour`, `WaterFractionAtPosition`
(no renderer call site).

**Needed by a view but not produced:** `terrain_ramp_direction` (FEAT-05); per-cell water depth
(VIEW-05, **implemented 2026-09-16**, answers it from the coast field rather than adding a generated
field); a generator→LibraryPack elevation mapping
(`TerrainLibraryPack.Elevation.cs:18` reads authored metadata only — VIEW-09); river direction and
width for directed water art (VIEW-06).

## Further findings verified for this review

- **Collision shapes never merge under any view.** `TerrainCollisionComponent.cs:242`
  `merge = _grid.TileMapLayerPath.IsEmpty && _grid.ElevatedTerrainPath.IsEmpty`, but
  `BindGameplayGrid` (`TerrainWorldComponent.Drawing.cs:245-262`) always fills one of the two under a
  terrain view (the "unavailable view" sentinel at `:261` still sets `TileMapLayerPath`), so every
  cell gets its own `ConvexPolygonShape2D` — 10,240 per class on Huge. Corner-substitution merging is
  exact for any affine cell run; the predicate already exists as
  `TerrainSurfaceStreamingComponent.HasExactChunkOutline` (`:229-232`). → VIEW-02.
- **The flat companions already stand on a diamond.** Feature (`TerrainFeatureRendererComponent.cs:283-294`),
  relief (`:224-236`), resources (`:254-260`) and the overlay (`CellOutline :315-321`) place through
  `GridProjectionComponent.CellToWorld/CellCorners`, which return `MapToLocal` centres and diamond
  corners for a bound isometric layer (`GridProjectionComponent.cs:249-258,351-354`); the world binds
  the grid to IA's `IsoTerrain` (`Drawing.cs:257`). Only `flat` (`Drawing.cs:36`) hides them. → VIEW-01,
  **implemented 2026-09-15** (180 trees on the lab's Tiny map, every anchor on a wooded diamond).
- **`GridProjectionComponent.EffectiveTileSize` (`:240-242`) reads only the manual export**, even with
  a native layer or the iso renderer bound; `TerrainPropResidency.CellPixels` (`:122-128`) reads it. → VIEW-02.
- **Two shader surfaces drop the item modulate.** `shaders/terrain_splat.gdshader:388` wrote
  `COLOR = vec4(col, 1.0)`; `shaders/iso_water.gdshader:115,172` kept only the input alpha;
  `terrain_tile_detail.gdshader:18` multiplies `COLOR.rgb`. → VIEW-14, **implemented 2026-09-15**.
  *Corrected on implementation:* this finding originally said these two shaders lost the scene's
  `CanvasModulate` (`ecs/atmosphere/AmbientController.cs`), "so at night the tile view's ground
  darkens while its sea stays daylight". It was the other way round. Godot multiplies
  `canvas_modulation` in after the user fragment code, so these two always darkened. What they lost
  was a tint set on the renderer node or its parents (`Modulate`/`SelfModulate`), plus opacity on
  the painted surface. Godot skips the CanvasModulate only for `render_mode unshaded`, and the
  unshaded `terrain_tile_detail.gdshader` (the Tiles view's ground) and `natural_terrain_green.gdshader`
  are what stayed daylight. Confirmed by rendered experiments on Compatibility and Forward+.
- **The half-sample distance rule has four writers**: `TerrainEuclideanDistance.Signed`
  (`TerrainEuclideanDistance.cs:146`), the shoreline stage deciding the cell kind
  (`TerrainShorelineStage.cs:29,38`) and the coast field, live and static
  (`TerrainCoastField.cs:541-543,795-797`), each write `max(0, √d² − 0.5) / samplesPerTile`;
  `terrain_splat.gdshader:325-328` decodes the field's value against the per-texel width. Nothing
  ties the shader's band to the cell kind at the cell centre. → VIEW-07.
- **`terrain_library_elevation` has one writer, the editor session**
  (`ecs/grid/GridCellDataComponent.TerrainEdits.cs:46`), and one reader (`TerrainLibraryPack.Elevation.cs:18-19`);
  a generated world renders every pack cell at the base profile. → VIEW-09.
- **The lab wires no resource-icon renderer and no collision**
  (`templates/scenes/terrain/terrain_generator_lab.tscn:2433-2622`; only `terrain_splat_demo.tscn:111`
  has an icon renderer); `tests/examples/stack_order.gd:74-81` guarded companion visibility for three
  of four views at review (all four since VIEW-01). → VIEW-13.

## Demo and probe coverage

| Scene | View(s) | Demonstrates |
|---|---|---|
| `terrain_generator_lab.tscn` | all four | the only scene wiring every view; features, iso features, overlay, relief prefab, data layers, grid, navigation; no collision, structures, packs or minimap |
| `terrain_splat_demo.tscn` | Painted | splat shader, 11 ground textures, material tiling, foam sheet, relief, features, resource icons |
| `terrain_tilemap_demo.tscn` | Tiles | 13 biome atlases + `iso_water` sea; no companions |
| `terrain_iso_demo.tscn` | Isometric | Kenney blocks/tops, `TerrainVariants`, seabed, iso water, iso features |
| `terrain_cartoon_demo.tscn`, `terrain_pixel_art_demo.tscn` | Painted | `MapArt` profiles over the lab |
| `terrain_generation_layers_demo.tscn` | component demo | the only `UseTileSetTerrains = true` user (`:65`) |
| `terrain_rock_objects.tscn` | flat | the relief prefab the lab and splat demo instance |
| `terrain_road_tiles_demo.tscn` | none | a labelled road-tile catalog, not a renderer |

No standalone IsometricAutotile demo; no scene wires `TerrainStructureLayerComponent`,
`TerrainLibraryPack`, `TerrainCollisionComponent`, `TerrainSurfaceStreamingComponent` or a minimap.

## Findings → plan

| Finding | Item |
|---|---|
| IsometricAutotile draws terrain and nothing else | VIEW-01 (companions, implemented), VIEW-04 (sea, implemented), VIEW-09 (elevation) |
| Eight cell-size owners; collision never merges | VIEW-02 |
| D1 two prop stamp rules | VIEW-03 |
| D2 three sea bindings, diverging defaults; no IA sea | VIEW-04, **implemented 2026-09-16** |
| D4 two depth notions | VIEW-05, **implemented 2026-09-16** |
| Flow generated and discarded; static rivers; lakes drawn as sea | VIEW-06 |
| D5 two beach owners; three copies of the distance rule | VIEW-07 |
| D8 three copies of connect-and-verify; no windowed edits on IA's bindings path | VIEW-08 |
| D9 two elevation facts; generated hills flat in the pack views | VIEW-09 |
| Tiles and Isometric build synchronously; DUP-01's remainder | VIEW-10 |
| Isometric rebuilds the whole map per edit | VIEW-11 |
| `terrain_shade` recomputed by the painter, unused elsewhere | VIEW-12 |
| D7 two resource drawers; lab and guard coverage | VIEW-13 |
| Shaders drop the item modulate; unshaded shaders skip the canvas modulate (corrected) | VIEW-14 |

## Deliberately not items

- Art for IsometricAutotile and packs, smooth isometric transitions, pack water tiles — the terrain
  art library's (`tools/terrain-library/specification.json`, `plans/TERRAIN_LIBRARY_*.md`); the owner
  confirmed on 2026-09-15 that the flat Kenney diamonds stay until the library delivers.
- Streaming for Tiles/Isometric/IA, streamed save, painted memory, prop residency — ENH-07/10/05/06
  and DUP-07; VIEW-08 and VIEW-10 are their foundations.
- Roads — `GridRoadComponent` is the owner (grid session); the road shader and tilesets are a style
  asset catalog; `plans/TERRAIN_VIEW_GRID_INTEGRATION.md:8` states roads are out of scope.
- Day/night as a terrain owner — the atmosphere session's; VIEW-14 is only the shader contract.
- D6 (landed), D10 (different facts), D11 (two inputs by design).
- **`Projection` is not saved — owner's call, recorded here.** It is a view preference; putting it in
  the recipe would make a view switch during generation discard the world
  (`TerrainWorldComponent.Generation.cs:123` compares `CaptureRecipe()` JSON). If persistence is
  wanted, it is a separate `terrain_world.view` key restored after the recipe without regenerating.
- Ramps/cliffs, fog, seasons, scatter limits, editor sessions limited to the two tile views —
  FEAT-05/03/07, ENH-13, and by design (BGB-13).
- The failing `terrain_water_surface_probe.gd` cell (21,3) — the open painter item; VIEW-07 adopts
  its cell-centre contract for sand without claiming the water fix.

## Industry reference

- **openage / Age of Empires II** blends terrains by priority: the higher-priority terrain's
  alpha-masked edge is drawn over the lower one, nine blend modes × 31 masks chosen from the
  8-neighbour bit pattern (`doc/media/blendomatic.md`); 100 texture variants per terrain selected by
  `(x % tc) + (y % tc) * tc` hide repetition; 17 elevation levels with slopes lit through an HSV lookup
  (`doc/media/terrain.md`). Our three transition methods (painted radial splat, 15-piece dual grid,
  47-configuration terrain sets) are the same idea at three fidelities; variants are governed by the
  library's `alternativePolicy: only_meaningful_variation`.
- **Godot 4 TileMapLayer** stalls on edits above roughly 128² cells; the accepted practice is chunked
  layers (25–41-cell windows) and a chunked minimap image (godotengine/godot#72458; Godot forum
  threads on tilemap chunking) — ENH-07's design.
