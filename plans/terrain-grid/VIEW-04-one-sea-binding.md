# VIEW-04 — One sea binding and one water look (`TerrainWaterLook`, `TerrainSeaSurface`; IsometricAutotile gets a sea)

**Type:** duplication fix + feature (three dial sets with diverging defaults, three surface lifecycles, and a fourth view with no sea) · **Area:** new `ecs/terrain/TerrainWaterLook.cs` + `textures/terrain/terrain_water_look.tres`, new `ecs/terrain/TerrainSeaSurface.cs`, `TerrainTileRendererComponent`, `TerrainIsometricRendererComponent`, `TerrainIsometricAutotileRendererComponent`, `TerrainPaintedRendererComponent`, `TerrainWorldComponent` (+ `.Drawing`), `TerrainWaterMaterial`, `templates/scenes/terrain/terrain_tilemap_demo.tscn` · **Status:** Implemented 2026-09-16 · **Effort:** M (3 days) · **Risk:** medium — **a look change: the tile view's sea takes the shared defaults. Fahad ran the lab on 2026-09-16, saw all four views draw the shared sea, and approved it.**

## As landed (2026-09-16)

Built as designed, with these differences, each found while reading the code:

- **The look's seabed sand is `SeabedSandTexturePath`, not `SandTexturePath`.** The painted renderer
  has its own `SandTexturePath` - the LAND material, `textures/terrain/sand.png`, authored in four
  shipped scenes - while the tile and block views' `SandTexturePath` is the seabed,
  `textures/water/seamless_golden_desert_sand_texture.png`. Both bind the same shader uniform from
  different views. One name for both would have quietly swapped a demo's beach for an ocean floor.
  The painted view keeps its land slot; the look owns the seabed one.
- **`TerrainWaterLook.Shared` is the class defaults, not the shipped .tres.** A scene that assigns
  nothing still draws one sea rather than three; the shipped `terrain_water_look.tres` is a scene
  preference, assigned on the world, exactly as `painted_ground_tiling.tres` is. It carries what the
  demos authored: `FoamStrength 0.4`, the three seabed textures and the surf sheet.
- **The scenes assign the look on the WORLD, not the renderer.** `Draw()` pushes `WaterLook` to every
  renderer the way it pushes `PropSizing`, so a renderer-level assignment would be overwritten on the
  next build. `terrain_tilemap_demo`, `terrain_iso_demo`, `terrain_splat_demo` and the lab all assign
  the shipped resource on their `TerrainWorldComponent`.
- **The block view keeps `BuildWaterMaterial`** as a three-line call into the surface: its quad is
  neither flat nor batched, and `RebuildRivers` duplicates the sea material, so the ordering
  (sea material first, rivers after) had to stay where it was.
- **The autotile sea is positioned by asking both layers where a cell is** - `terrain.MapToLocal
  (BoundsOrigin) - water.MapToLocal(Vector2I.Zero)` - rather than re-deriving the isometric
  projection, so it stays aligned whatever an authored TileSet's layout is.
- **The plan's rendered pixel guard became a headless one.** Comparing "the same cell's deep-sea
  pixel" across two projections needs camera arithmetic to find that cell on screen in each view.
  `terrain_view_parity_probe` instead checks, for all four views at once, that every sea carries a
  coast map and the same eight shared uniform values - which is the property the pixel comparison
  was standing in for, measured directly.

**Guards:**
- `tests/terrain_water_material_probe.gd`, rewritten: one `TerrainWaterLook` with thirteen
  non-default values, pushed to all four views (the painted composite included, and the autotile view
  on the lab's authored TileSet); every water material carries all thirteen; every view agrees; the
  shared writer still clamps to the shader's own hint_range; and no view exports a dial of its own.
  **Mutation:** re-adding a private `FoamStrength` export to the tile view failed both the probe and
  the contract scan (17 against the 16 pre-existing).
- `tests/terrain_view_parity_probe.gd` gained the fourth sea row and the one-world-one-sea check.
  **Mutation:** dropping the autotile view's coast resolve failed "the sea has no coast map, so it
  draws without shallows".
- Contract pin, both directions: each dial is declared on `TerrainWaterLook.cs` and on none of the
  four renderers, and every renderer takes a `WaterLook`.
- Seven probes that set water dials on a renderer would have become silent no-ops (Godot's `set()` on
  a missing property does nothing): `terrain_material_scale`, `terrain_lava_material`,
  `terrain_water_surface`, `terrain_bedrock_texture`, `terrain_live_coast_shape`,
  `terrain_painted_blend` and `terrain_iso_river` now author a look. All green.
- Still green: runtime smoke, and the headless integration run - only the three recorded failures
  (`ground_cover`, `prop_sizing :40`, `lab_grid :14`).

**Captures:** `tests/terrain_water_look_capture.gd` renders the lab in all four views plus the tile
view before and after, into `tests/output/water_look/`. The owner reviewed the change live in the lab
instead, which is the same evidence.

## Gap

DUP-02 (landed 2026-09-08) made `TerrainWaterMaterial.Apply` the one *writer* of the shared water uniforms and left two things it named but did not resolve.

**The dials still have three owners with two sets of defaults.** Painted exports them at `TerrainPaintedRendererComponent.cs:60-62,98-142`, Tiles at `TerrainTileRendererComponent.cs:131-168`, the block view at `TerrainIsometricRendererComponent.cs:187-214` — each with a comment saying the other view exposes "the same dials" (`TerrainPaintedRendererComponent.cs:129-132`, `TerrainIsometricRendererComponent.cs:196-200`). The tile view's defaults differ: `FoamStrength 1.0 / DeepTiles 6.0 / ShallowTiles 6.0` (`TerrainTileRendererComponent.cs:132-134`) against `0.50 / 4.5 / 1.8` in the other two (`TerrainPaintedRendererComponent.cs:134-138`, `TerrainIsometricRendererComponent.cs:188-190`); the tile view's own comment calls it "a real divergence — one map drawn twice with two different seas" and leaves it to the owner (`:126-130`), as does DUP-02's outcome. `TerrainWaterMaterial.Settings` is positional so no view can half-fill it (`TerrainWaterMaterial.cs:43-51`), but each view still fills it from its own copy of the numbers.

**The surface lifecycle is written twice and a half.** Tiles: `EnsureWaterSurface` (`:458-504` — the `TileWater` layer `:482`, its TileSet `:491`, the fill `:493`) and `BuildWaterMaterial` (`:506-568`, adopt-or-create `:508-520`, coast resolve `:525-533`, own uniforms `:539-544`, `Apply :547-563`). Block view: `EnsureWaterSurface` (`:996-1044`, an overscanned `Polygon2D`) and `BuildWaterMaterial` (`:1087-1162`, the same adopt-or-create, own uniform block `:1131-1138`, `Apply :1141-1157`, textures `:1159-1160`). Painted keeps only the `Apply` on its composite (`:405-421`) — correct, it has no surface. The coast resolve through a per-view `RenderCache` is repeated at `TerrainTileRendererComponent.cs:532`, `TerrainIsometricRendererComponent.cs:1122` and `TerrainPaintedRendererComponent.cs:373-376`.

**IsometricAutotile has no sea.** It draws the ground from its TileSet or LibraryPack and nothing else; there is no `WaterShaderPath`, no water node, no coast bind. The tile view already states the contract a sea should have — "leave the path empty and the tiles are all that draws" (`TerrainTileRendererComponent.cs:96-99`) — and clears its sea under a LibraryPack (`:344`) because packs bring their own shoreline sets (`plans/TERRAIN_LIBRARY_ENGINE_UPGRADE.md:240-246`).

`terrain_tilemap_demo.tscn` authors eight water values on its renderer (`:74-83`), and `tests/terrain_water_material_probe.gd` authors thirteen dials per view (`:23-37`) and checks the two views that draw a surface (`:3-15`).

## Design

1. **`TerrainWaterLook : Resource`** (`[GlobalClass]`): the thirteen shared scalar dials the probe already lists — `WaveIntensity, FoamStrength, ShallowTiles, DeepTiles, GroundTextureTiles, WaterTextureTiles, FoamTilesAlong, FoamTilesAcross, FoamScroll, FoamPulse, FoamArrivalRate, SwellDirectionDegrees, SwellDirectionality` — plus the four texture paths (`Shallow, Deep, Sand, FoamSheet`), with the shader's own values as defaults and one shipped `textures/terrain/terrain_water_look.tres` beside `terrain_prop_sizing.tres`. `TerrainWorldComponent.WaterLook` is pushed to every renderer in `Draw()` exactly as `PropSizing` is (`TerrainWorldComponent.Drawing.cs:37-39`). The per-renderer dial and texture exports are deleted. What stays per view is what is genuinely per surface: `CoastRangeTiles/CoastDetail` (each view resolves its own coast window — DUP-02's outcome 1), the transparent-surface uniforms `MaxOpacity/ClarityTiles/LakeOpacity/ShoreOpacity` (`TerrainWaterMaterial.cs:30-33`), and the block view's `SeabedDepth/SeabedStep/WaterOverscan`.
2. **The look change, stated.** The look's defaults are the painted/block values, so the tile view's sea gets shallower and less foamy than today. Before landing: captures of `terrain_tilemap_demo.tscn` and the lab's Tiles view, before and after, for Fahad. If he wants the demo's present sea, its authored values (`:82-83`) become `terrain_tilemap_demo_water.tres` assigned on that scene's world — a scene preference, not a renderer default.
3. **`TerrainSeaSurface`** (one class, owned by each surface-drawing view): the coast resolve (`LiveCache` + `RenderCache`, today `TerrainTileRendererComponent.cs:176-177` and the block view's equivalents), material adopt-or-create, the surface uniform block, and two geometries — `TileBatched(layer, cellSize, isometric)` (Tiles; IsometricAutotile with `isometric: true`) and `Polygon(overscan)` (the block view, `:1022-1040`). Views call `Ensure`/`Clear`; the block view's `RebuildRivers` (`:1046-1081`) keeps duplicating the surface's material for its opaque river sheet until VIEW-06 gives the shader a flow class.
4. **IsometricAutotile gains `WaterShaderPath`** with the tile view's contract: empty → tiles only; set → a `TileWater` sea through `TileBatched(isometric: true)`; under a LibraryPack the sea is cleared as `:344` does. The lab's autotile entry gets the shader path so the lab shows it.
5. **`TerrainWaterMaterial.Apply`** stays the one writer; it takes the look, and `ApplyTextures` reads the look's four paths. `TerrainWaterMaterial`'s header, the four renderer pages and `docs/game-builder/TERRAIN_LIBRARY_PACKS.md` (autotile sea under a pack) are updated.

## Guards (fail first)

- `tests/terrain_water_material_probe.gd` extended: one `TerrainWaterLook` authored with the probe's thirteen non-default values (`:23-37`) pushed through the world → every water material in all four views (`Flat/TileWater`, `Iso/IsoWater`, `IsoAutotile/TileWater`, the painted composite) carries the same thirteen uniform values. **Mutation:** keep a private `FoamStrength` export on the tile view and pass it → the tile material's `foam_strength` ≠ the look.
- Rendered (`tests/run_terrain_integration.ps1`): lab in IsometricAutotile — a deep-sea pixel of `IsoAutotile/TileWater` equals the Tiles view's deep-sea pixel at the same cell within one byte per channel. **Mutation:** skip the coast-map bind in the autotile surface → shallows vanish, the pixel differs.
- Pin, both directions: each dial's `[Export]` declaration exists in `TerrainWaterLook.cs` and in none of the four renderers (the pin matches the whole declaration, per DUP-02's blind-spot note).
- Existing: `terrain_water_surface_probe`, `terrain_iso_river_probe`, `iso_water_origin`, `lab_tile_views` green; the before/after captures are attached to the outcome.

## Dependencies / collisions

DUP-02 and DUP-15 (landed). VIEW-05 (depth) and VIEW-06 (flow) bind through `TerrainSeaSurface`, so this lands before them. ENH-07 windows the `TileBatched` geometry later — the surface is the seam it needs. Collision: the library session edits `TerrainIsometricAutotileRendererComponent` (`TerrainLibraryInspector`); the streaming session streams the tile view's water layer — coordinate both.

## Out of scope

Water art and textures; pack shoreline art; the painted composite's structure; lake depth; canvas modulate (VIEW-14); the block view's seabed bands (VIEW-05).
