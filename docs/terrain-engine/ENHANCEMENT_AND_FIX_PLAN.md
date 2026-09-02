# Terrain Engine - Enhancement and Fix Plan

This plan covers all 56 files under `addons/beep_game_builder_cs/ecs/terrain/`,
each documented individually elsewhere in this directory
(`docs/terrain-engine/<FileName>.md`). It is the result of a documentation
pass over every one of those 56 files followed by a five-dimension review
(duplication, ownership/settings, consistency, completeness against genre
standard, performance), with every finding independently re-verified against
the actual source - grep results, exact line numbers, and reproduced
measurements - before being included below. A separate follow-up pass then
checked the engine and this plan's own recommendations for anything
duplicating a native Godot `TileData`/`TileMapLayer`/`TileSet` capability (see
"Godot-native feature duplication check" below).

## Status

**This plan is finalized, and every finding it raised is now resolved.**

Of the twelve findings in "Confirmed findings" below, **all twelve are fixed
and verified** (source changed, `dotnet build` clean, the full guard suite -
`addon_contract_scan.ps1`, all 15 `terrain_guards.ps1` checks, all 9
`renderer_reporting_probe.ps1` checks, the four `grid_terrain_*_probe.ps1`
checks - passing after every change, most falsified at least once to confirm
the guard can actually fail). Each fixed finding below is marked
`**Status:** Fixed.` with what changed and where. The three that were sized
but deliberately deferred in the first pass - BoundsSize/TileSize
duplication, the per-cell `ResolveField()` performance fix, and
`ResourceDefinition.NodeScene`/`.Icon` - were all completed in a second pass
once their scope and risk had been fully mapped; see their own `**Status:**`
entries for what each fix actually did and how it was verified.

Of the three items that were genuinely open decisions rather than sized
work, two are now resolved: `ReliefAt`'s wrong-unit publish (fixed, option
(a) - its own layer) and the Continents/Archipelago landmass overlap (fixed,
the formula change the plan itself suggested). Only the isometric-autotile
art gap remains open, and stays open because it is **not code-fixable at
all** - see "Known but not code-fixable" below for why.

## Already fixed this session

This terrain engine has already been through one audit-and-fix pass earlier in
this session. The items below are done and guard-verified; they are listed
here for context, not as open work.

**Generation correctness.** `TerrainMode.Plain` used to leave the per-cell
(`Cell*`) arrays at their constructor default while only filling the
per-sample arrays, so a Plain map's gameplay-tile data silently ignored the
chosen preset; `BuildPlain` now fills both resolutions. Biome assignment had
been steered by a `UseBiomeQuotas` mechanism (percentile cutoffs meant to
guarantee every biome a share of the map); measured against real output it
collapsed whole biomes to zero (deserts and dry grassland vanished entirely on
some maps) while being turned on unconditionally by `TerrainWorldComponent`.
The whole mechanism (`UseBiomeQuotas`, `DesertFraction`, `DryGrassFraction`,
`SwampFraction`, `Quotas()`) was removed; the fixed Whittaker moisture-band
table is now the only thing that decides a biome, guarded by
`tests/examples/biomes.gd`. `TerrainMapSetup.LandScaleFor(SeaLevel)` was
swinging land coverage far more than Civilization's own equivalent setting
(+33%/-28% vs. 7-12%); retuned to 1.12/0.88.

**Dead settings removed.** `Dryness` and `FertilityFrequencyMultiplier` were
exported, threaded into `TerrainGenerationSettings`, and read by zero
generation stages - accepted-then-ignored inputs. Both were deleted entirely
rather than wired up, since nothing needed the capability.

**Renderer silent failure fixed.** All 9 renderer components used to fail
completely silently when their generator `NodePath` did not resolve (node
missing, wrong type, typo) - the layer still got created, nothing drew, and
nothing was logged. All 9 now call `GD.PushWarning` naming the specific cause,
verified by `tests/examples/renderer_reporting.gd` plus
`tests/renderer_reporting_probe.ps1`. (One gap in this fix survived, on the
10th, non-conforming renderer - see the "TerrainTileRendererComponent can fail
completely with zero warnings" finding below.)

**Duplication resolved, four separate cases.** (1) Resource definitions: the
generator had a private `ResourceDefinition` record struct and three
hardcoded catalogue arrays, while the game side (`GridResourceNode`,
`GridResourceScatter`) had bare `ResourceId` strings with no shared
definition; unified into `ResourceDefinition.cs` (public `Resource`) +
`ResourceCatalog.cs` + `ResourceCatalogs.cs` (the three shipped sets, same
ids/categories/terrain/weights/order), with the generator and game side now
reading one catalog. (2) Twelve independent hand-written "is this terrain kind
water" string checks across `TerrainFieldBuilder`, `TerrainFeatureStage`,
`TerrainScaleConstraintStage`, and `TerrainIsometricRenderer` were replaced by
`TerrainTileSets.IsWaterKind`/`IsLandKind`. (3) Eight independent "new
TileMapLayer; AddChild; Adopt" blocks across six renderer/component files were
replaced by `TerrainAuthoring.EnsureLayer` - and a renderer that used to
hardcode which biome layers exist regardless of map content now reads
`TerrainGeneratorComponent.TerrainKindsPresent()`/`TerrainLevelsPresent()`
instead of guessing. (4) The tile (flat) projection used to read
`GridCellDataComponent` (a copy the generator writes at build time) while the
painted and isometric projections read `TerrainGeneratorComponent` directly;
`TerrainTransitionLayerComponent` now takes only `TerrainGeneratorPath` - no
second source, no fallback - and the one scene that existed solely to justify
the second source (`terrain_15_piece_layers_demo.tscn`, with its own
`GridCellPatternComponent`/`GridCellRegionDefinition`) was deleted, since it
had no generator in it at all.

**Contract enforced.** `ApplyMapSetup` overwrites eleven exported generator
settings (`Landform`, `ArchipelagoIslandCount`, `StartPositionCount`,
`LandmassScale`, `HillsFraction`, `MountainsFraction`,
`ClimateLatitudeCentre`, `LakeCoverage`, `RiverDensity`, `FeatureDensity`,
`ResourceDensity`) every time `TerrainWorldComponent.Build` runs. This is now
documented as a named contract and guarded by
`tests/addon_contract_scan.ps1`, so an undocumented twelfth overwrite cannot
land silently. (This session's review found the guard's coverage has a gap -
see the "TerrainWorldComponent.Build silently overwrites 5 more generator
settings" finding below.)

**Naming cleanup.** 12 classes were renamed so `Terrain*` consistently means
this engine and `Grid*` means the separate gameplay-grid system (out of scope
for this plan): `GridTerrainGeneratorComponent` -> `TerrainGeneratorComponent`,
`GridSplatTerrainRendererComponent` -> `TerrainPaintedRendererComponent`,
`GridBiomeTileMapRendererComponent` -> `TerrainTileRendererComponent`,
`GridIsoTileMapRendererComponent` -> `TerrainIsometricRendererComponent`,
`GridIsoFeatureRendererComponent` -> `TerrainIsometricFeatureRendererComponent`,
`GridIsoTerrainRendererComponent` -> `TerrainIsometricAutotileRendererComponent`,
`GridTerrainFeatureRendererComponent` -> `TerrainFeatureRendererComponent`,
`GridTerrainReliefRendererComponent` -> `TerrainReliefRendererComponent`,
`GridTerrainResourceRendererComponent` -> `TerrainResourceRendererComponent`,
`GridTerrainMapOverlayComponent` -> `TerrainMapOverlayComponent`,
`GridTerrainCellDataComponent` -> `TerrainDataLayersComponent`,
`GridTerrainTransitionLayerComponent` -> `TerrainTransitionLayerComponent`.

## Godot-native feature duplication check

This engine's existing custom-data mechanism - `TerrainTileSets.cs` defining
TileSet custom-data layers, `TerrainDataLayersComponent.cs` reading and
writing them - exists specifically so a game answers "what's on this cell"
through Godot's own `TileData` API rather than a parallel, addon-specific
one. Because that pattern is load-bearing for the whole engine, this
follow-up pass checked whether it actually holds everywhere it should, and
specifically looked for two failure modes: current code that quietly
reinvents a native `TileMapLayer`/`TileSet`/`TileData` capability (a
hand-rolled passability table where custom data already exists, a per-cell
cache duplicating what `GetCellTileData` already answers, a hand-picked tile
index where `SetCellsTerrainConnect` already resolves the join, a per-cell
physics/nav body where a tile's own collision/navigation polygon already
applies automatically) instead of using it; and places where this plan's own
suggested fixes, elsewhere in this document, would introduce exactly that
kind of duplication if implemented as written. Nine findings came back, each
independently re-verified against source and current Godot 4 documentation.
All nine confirm the pattern holds - no reinvention was found, in the shipped
code or in this plan's own recommendations.

### Confirmed already correct

- **Custom-data layers are the real mechanism, used as designed.**
  `TerrainTileSets.DefineCellData` adds the six `Cell.*` fields (`terrain`,
  `resource`, `feature`, `relief`, `is_water`, `passable`) as real `TileSet`
  custom-data layers via `AddCustomDataLayer`/`SetCustomDataLayerName`/
  `SetCustomDataLayerType` (idempotent, so it can be applied to a developer's
  own `TileSet` without disturbing their layers), and
  `TerrainTileSets.Describe` writes per-tile values with
  `TileData.SetCustomData` - the same call a developer would make from the
  TileSet editor. `TerrainTileSets.cs:32-61, 86-98, 108-123`.
- **Physics collision and navigation are authored per tile, natively, not
  tracked as a flag.** `TerrainTileSets.DefineBody` adds one native physics
  layer and one navigation layer per `Ground` value (Land/Water/Steep) via
  `TileSet.AddPhysicsLayer`/`AddNavigationLayer`, and `ShapeCell` attaches a
  real `TileData.AddCollisionPolygon`/`SetCollisionPolygonPoints` plus a real
  `TileData.SetNavigationPolygon` to the tile, both keyed to the matching
  layer. `TerrainDataLayersComponent.Rebuild` wires this automatically for
  every terrain kind present. `TerrainTileSets.cs:138-148, 173-199, 218-242`;
  `TerrainDataLayersComponent.cs:125-132, 197-202`.
- **No parallel per-cell cache exists behind the query API.** Every public
  query on `TerrainDataLayersComponent` (`TerrainAt`, `ResourceAt`,
  `FeatureAt`, `ReliefAt`, `IsWaterAt`, `PassableAt`) routes through one
  `Read()` helper calling `TileMapLayer.GetCellTileData` then
  `TileData.GetCustomData` - exactly the native call the class's own doc
  comment shows a developer making directly. The only `Dictionary<string,int>`
  fields in the file are transient value-to-atlas-column lookups used while
  building the `TileSet`, not a per-cell fact store; a repo-wide scan of
  `Terrain*.cs` found no `Dictionary<Vector2I,...>`/`HashSet<Vector2I>`
  post-paint cache anywhere in the renderer set.
  `TerrainDataLayersComponent.cs:232-264`.
- **The autotile renderers' default (and, for one of them, only)
  tile-placement path is the native terrain-connect API.**
  `TerrainTransitionLayerComponent` defaults `UseTileSetTerrains = true` and
  calls `TileMapLayer.SetCellsTerrainConnect` as its default path;
  `TerrainIsometricAutotileRendererComponent` has no legacy path at all - its
  doc comment records that an earlier hand-mapped corner-mask version was
  deliberately removed because it agreed with the correct mapping on "barely
  a third of tiles." `TerrainTransitionLayerComponent.cs:40, 130-134, 179`;
  `TerrainIsometricAutotileRendererComponent.cs:17-23, 107-124`.
- **The one hand-rolled corner-mask fallback left in the engine is not a
  duplicate of `SetCellsTerrainConnect` - it's dual-grid tiling, a technique
  Godot's terrain sets don't provide.** `TerrainTransitionLayerComponent`'s
  legacy path (gated behind an explicit `UseTileSetTerrains = false` opt-in)
  samples the four logical cells around each display-grid corner and
  resolves through a fixed 16-entry table - the offset-display-grid
  "dual-grid tilemap" technique that multiple independent third-party Godot
  addons exist solely to add, precisely because Godot's built-in terrain sets
  place one authored tile per logical cell on a single, non-offset grid with
  no such concept. It exists for the hand-authored 15-piece atlases
  `TerrainTileRendererComponent` forces onto this path, which carry no
  `terrain_peering_bit_*` values at all. `TerrainTransitionLayerComponent.cs:
  6-11, 119-123, 186-215`.
- **Collision/navigation bodies are shaped once per terrain kind, not once
  per generated cell.** `TerrainTileSets.ShapeCell` takes a terrain-kind
  string, not a cell coordinate - it has no way to see per-cell state.
  `TerrainDataLayersComponent.EnsureLayer` calls it once per distinct kind
  present on the map while building the atlas; the actual per-cell loop only
  calls `TileMapLayer.SetCell`, and `TileMapLayer.CollisionEnabled`/
  `NavigationEnabled` then apply each placed tile's authored polygon
  automatically. No `StaticBody2D`/`CollisionShape2D` is built per generated
  cell anywhere in the procedural pipeline. `TerrainTileSets.cs:173-199,
  218-242`; `TerrainDataLayersComponent.cs:125-132, 142-159, 182-183,
  196-217`.
- **This plan's own `ReliefAt` fix option (a), above, stays inside the
  native mechanism.** Writing `_generator.ReliefAt(at)` through
  `TerrainTileSets.Describe`'s existing `data.SetCustomData(Cell.Relief,
  ...)` call - the same path Resource and Feature already use - is a
  same-mechanism correction, not a new `Dictionary<Vector2I,int>` cache
  running alongside the `TileData` layers that already exist for this. No
  change needed to that finding's text.
- **This plan's isometric-autotile fix, above, already points at the native
  fix, not a bespoke one.** Its resolution is "an artist needs to open
  `grassland_tileset.tres` in Godot's TileSet editor and paint
  `terrain_peering_bit` values ... in the Terrains tab" - native `TileSet`
  terrain-set authoring, consumed by the renderer's existing
  `SetCellsTerrainConnect` call, with no parallel autotile mechanism
  proposed. No change needed to that finding's text.

### Reinvention found in current code

None. No file in this engine was found reimplementing a native
`TileMapLayer`/`TileSet`/`TileData` capability behind a parallel, hand-rolled
mechanism.

### Reinvention this plan's own recommendations would have introduced

None. Both plan findings that touch a native-adjacent mechanism (the
`ReliefAt` fix options and the isometric-autotile fix, both above) already
recommend the native path; neither needed revision.

## Known, now fixed

### TerrainDataLayersComponent.ReliefAt publishes a guessed value in the wrong unit space, and nothing reads it

`TerrainDataLayersComponent.ReliefAt` (line ~242) reads a `Cell.Relief` custom
tile-data value. That value is written by `TerrainTileSets.Describe` as
`TerrainLayers.LevelForKind(kind)` - a **guess derived purely from the
terrain-kind string** (water kinds -> Sea, `gravel` -> Hills, `rock` ->
Mountains, everything else -> Ground), in `TerrainLayers` units (Sea=0,
Ground=1, Hills=2, Mountains=3, Summits=4). Every real consumer of relief in
the codebase - `TerrainIsometricRendererComponent`,
`TerrainIsometricFeatureRendererComponent`, `TerrainReliefRendererComponent`,
and every GDScript example under `tests/examples/` - instead calls
`TerrainGeneratorComponent.ReliefAt` directly, which returns the generated
field's real per-cell relief in `TerrainRelief` units (Flat=0, Hills=1,
Mountains=2) - a **different number for "1"**, and derived from the actual
elevation/relief field rather than the kind string. A hilly grass or tundra
tile (anything that isn't literally `gravel` or `rock`) reads as flat "Ground"
through the guessed path while the real field may say Hills.

A repo-wide grep for every call to `.ReliefAt(` (not just this one file)
confirms `TerrainDataLayersComponent.ReliefAt` has **zero callers anywhere** -
in the addon or in `tests/`. Every `.ReliefAt(` call site resolves through
`_generator.ReliefAt(...)`/`gen.ReliefAt(...)`, where the receiver is always a
`TerrainGeneratorComponent`.

Two candidate fixes, presented without a pick, because this needs a decision:

- **(a) Give it its own layer.** Have `TerrainDataLayersComponent` write the
  `relief` custom-data field per cell from `_generator.ReliefAt(at)` at
  `Rebuild()` time, the same pattern this component already uses for its
  Resource and Feature layers (both of which vary independently of terrain
  kind, same as relief does). This preserves the component's whole reason for
  existing - answering "what's here" through Godot's native tile-data API with
  no generator node required at runtime - and makes it actually correct for
  relief specifically.
- **(b) Delete it.** Remove `ReliefAt` and stop writing `Cell.Relief` for this
  purpose, since nothing reads the published value today and every live
  consumer already reads `TerrainGeneratorComponent.ReliefAt` directly. Simpler,
  and removes a second, wrong, currently-dead source of the same fact.

(a) keeps a promise the component's own doc comment already makes (a matched
`TerrainAt`/`ResourceAt`/`FeatureAt`/`ReliefAt` set, queryable without the
generator at hand); (b) is less code.

**Status:** Fixed, option (a). `TerrainDataLayersComponent` now builds a
fourth `TileMapLayer` ("ReliefData"), one tile per distinct `TerrainRelief`
value the map actually has (stringified "0"/"1"/"2" to fit the same
distinct-value/`EnsureLayer`/`Paint` pattern the Resource and Feature layers
already use), each tile's `Cell.Relief` custom data written straight from
`_generator.ReliefAt(at)` via a new `TerrainTileSets.DescribeRelief` - kept
separate from `TerrainTileSets.Describe`, whose own `Cell.Relief` write (the
kind-derived `TerrainLayers` guess) stays exactly as every other existing
caller of `Describe` still depends on it; nothing about that shared method
changed. `TerrainDataLayersComponent.ReliefAt` now reads the new layer instead
of the terrain layer, and a `ReliefLayer` accessor was added alongside
`TerrainLayer`/`ResourceLayer`/`FeatureLayer` for symmetry. Confirmed
`TerrainReliefRendererComponent` (the one live consumer of "relief" anywhere
in the renderers) already reads `_generator.ReliefAt` directly and was
untouched by this change. `dotnet build` clean; full guard suite passing.

## Known, now fixed

### Continents and Archipelago overlap at default settings - real only outside the documented world-creation path

`TerrainGenerationSettings.RequestedLandmassCount` gives Mainland
`Mathf.Clamp(ArchipelagoIslandCount, 2, 6)` and Archipelago
`Mathf.Max(2, ArchipelagoIslandCount)`. At the shipped default
`ArchipelagoIslandCount = 4`, both evaluate to 4 - `Clamp(4,2,6) = 4` and
`Max(2,4) = 4` - so choosing "Continents" vs. "Archipelago" requests the same
landmass count, even though the code's own comment says continents should be
"fewer and larger."

This session's review adds one correction to that flagged gap: the collision
is real only for a game driving `TerrainGeneratorComponent` **directly**,
without `TerrainWorldComponent` (a use the component's own doc comment
explicitly supports). Going through the addon's actual documented
world-creation path - `TerrainWorldComponent.MapType` ->
`ApplyMapSetup` -> `TerrainShapePresets.Get` - Continents and Archipelago are
**not** identical: `ApplyMapSetup` sets `ArchipelagoIslandCount =
shape.IslandCount` from the shape catalogue *before*
`RequestedLandmassCount` is ever evaluated, and that catalogue already gives
Continents `IslandCount = 4` vs. Archipelago `IslandCount = 7`, producing
`RequestedLandmassCount` 4 vs. 7. So the shipped demo/lab scene and the
standard `TerrainWorldComponent`-driven workflow do not exhibit this bug;
only bare/direct generator use with an unmodified `ArchipelagoIslandCount`
does.

**Status:** Fixed as suggested. The Mainland arm of
`TerrainGenerationSettings.RequestedLandmassCount` is now `_ =>
Mathf.Clamp((ArchipelagoIslandCount + 1) / 2, 2, 6)` - at the default of 4,
Mainland=2 vs. Archipelago=4. `TerrainShapePresets` needed no change and
compounds naturally with this (Continents: `(4+1)/2=2`; Archipelago:
`max(2,7)=7`). A guard was added to `tests/examples/landmass.gd`: it drives
both Landform modes at the same `ArchipelagoIslandCount` across [2,4,6,8,10,
12] and reads `GetGenerationDiagnostics()["requested_landmass_count"]` for
each. The invariant actually asserted is "Mainland never exceeds Archipelago"
(`<=`), strict `<` specifically at the shipped default of 4 - not strict `<`
at every value, because both arms floor at the same minimum of 2 (Archipelago
cannot ask for fewer than 2 either, and lowering Mainland's floor below 2
would reintroduce the single-mass-filling-the-map failure this generator was
rewritten to stop producing), so `ArchipelagoIslandCount = 2` is a legitimate
tie, not a regression - the guard was first run against the unfixed formula
and confirmed to fail at 2, 4 and 6 before the fix, and now passes.

## Known but not code-fixable

### Isometric-autotile TileSet cannot autotile - confirmed as zero peering bits, not partial coverage

The isometric autotile projection
(`TerrainIsometricAutotileRendererComponent`) is wired end to end - selectable
as `TerrainProjection.IsometricAutotile`, and it already reports "no terrain
peering bits" via `GD.PushWarning` when it can't draw. The gap is entirely in
the shipped art asset, `textures/iso/grassland_tileset.tres`, referenced from
the addon's own reference demo (`terrain_generator_lab.tscn`).

Reading the `.tres` directly gives a more precise measurement than this item
carried before: the file declares 17 tiles and a `terrain_set_0` with two
named terrains ("Grass", "Meadow"), but **not one of the 17 tiles carries a
`terrain_set`, `terrain`, or any `terrain_peering_bit/*` property** - every
tile line is a bare `X:Y/0 = 0` existence marker. Godot only omits such a
property from the `.tres` text format when it is unset, so this is genuinely
zero of 16 possible corner combinations assigned, not the roughly 10-of-16
figure recorded earlier in this engine's history (whether that was measured
against an earlier revision of the asset, or the asset regressed further
since, is not established either way). `tools/make_iso_tileset.gd`'s own
comment names exactly this as the remaining manual step ("the only thing left
to do by hand is ... paint the terrain peering bits on each tile ... in the
Terrains tab") - a step the checked-in `.tres` shows was never completed.

This is an art/authoring gap, not a code gap, and is not something to fix in
this pass: an artist needs to open `grassland_tileset.tres` in Godot's
TileSet editor and paint `terrain_peering_bit` values for the Grass/Meadow
terrains on however many of the 17 tiles are meant to serve as join pieces.
Verify afterward by regenerating the isometric-autotile demo and confirming
the renderer's own "no peering bits" warning stops firing.

**Status:** Confirmed, not fixed - genuinely not code-fixable. Re-checked when
the rest of this document's open items were revisited: there is no code path
that can derive correct `terrain_peering_bit` values from the tile art itself
- which of a tile's four corners join to which neighbour is a fact about how
the artist drew that specific tile, not something inferable from pixels or
tile order. A prior attempt in this engine's history to derive them
programmatically is exactly why the "roughly 10-of-16" figure existed instead
of zero - it agreed with the artist's intended layout on only about a third of
the tiles, which is worse than the honest "no peering bits" warning the
renderer already gives, since a majority-wrong autotile draws confidently
wrong joins instead of visibly refusing to draw. The fix remains what it has
always been: a human opens `grassland_tileset.tres` in Godot's TileSet editor
and paints the Terrains tab by hand.

## Confirmed findings

### TerrainWorldComponent.Build silently overwrites 5 more generator settings beyond the documented eleven

**Files:** `TerrainWorldComponent.cs`, `TerrainGeneratorComponent.cs`,
`tests/addon_contract_scan.ps1`

**Status:** Fixed. `Build()` now carries a doc comment naming all five
(`BoundsSize`, `Seed`, `ResourceSet`, `UseClimateBiomeMaps`, `UseScaleRules`),
and `addon_contract_scan.ps1` gained a second guard - parallel to the existing
`ApplyMapSetup` one - that fails if `Build()`'s body assigns any
`_generator.*` property not in that documented list. Falsified (a temporary
undocumented `_generator.BeachWidth = 2.0f` was added and confirmed the guard
caught it by name) and reverted before landing.

`TerrainWorldComponent.Build()` sets `_generator.BoundsSize`, `.Seed`,
`.ResourceSet`, `.UseClimateBiomeMaps = true`, and `.UseScaleRules = true`
every time it runs - all **outside** the `ApplyMapSetup` call, so none of the
five is covered by the contract this session already fixed (the doc comment
naming "these eleven settings," and `addon_contract_scan.ps1`'s regex guard,
both only look inside `ApplyMapSetup`'s own method body). The two boolean
forces are the damaging ones: `UseClimateBiomeMaps` and `UseScaleRules` both
carry doc comments describing them as switches an Inspector user controls
("off by default so existing scenes keep the distribution they were tuned
against" / "ClimateLatitudeSpan and MinBiomeRegionFraction are ignored while
it is on") - neither comment mentions that `TerrainWorldComponent.Build`
forces both to `true` unconditionally with no way to opt out. Because
`TerrainGeneratorComponent.CurrentSettings()` branches purely on
`UseScaleRules`, the two exported floats `ClimateLatitudeSpan` and
`MinBiomeRegionFraction` become **permanently dead reads** for any scene that
uses `TerrainWorldComponent` - which its own doc comment calls "THE
map/world creation component," the primary way a game builds a world. A value
typed into either field in the Inspector is accepted, stored, and silently
discarded - the exact pattern the `ApplyMapSetup` contract exists to prevent,
one method away from where that contract actually looks.

**Evidence:** `TerrainWorldComponent.cs:170-181` sets `_generator.BoundsSize`,
`.Seed`, `.ApplyMapSetup(...)`, `.ResourceSet`, then
`.UseClimateBiomeMaps = true; .UseScaleRules = true;` - the last two lines
outside `ApplyMapSetup`. `TerrainGeneratorComponent.cs:539-543`:
`TerrainScaleRules.Rules scale = UseScaleRules ? TerrainScaleRules.For(size,
LandmassScale) : new TerrainScaleRules.Rules(Mathf.Clamp(ClimateLatitudeSpan,
...), Mathf.Clamp(MinBiomeRegionFraction, ...));`.
`tests/addon_contract_scan.ps1:1521-1541` only regex-matches inside
`ApplyMapSetup`'s own body; its only `TerrainWorldComponent` assertions
(lines 127-138) cover `BuildOnReady` deferral, the Generate-map button, and
node-path/method names - nothing about these five settings.

**Fix:** Extend the `ApplyMapSetup` doc comment (or add a matching one on
`Build()`) to name all five additionally-overwritten settings, the way the
eleven are named today. Extend `addon_contract_scan.ps1`'s guard to also scan
`TerrainWorldComponent.Build`'s own body, not just `ApplyMapSetup`, so a
future undocumented overwrite there fails the same way. Separately: since
`ClimateLatitudeSpan` and `MinBiomeRegionFraction` can never take effect while
`UseScaleRules` is forced on, either expose a real axis on
`TerrainWorldComponent` that can turn `UseScaleRules` off, or grey out /
document on the two exports themselves that `TerrainWorldComponent` overrides
them.

### TerrainTileRendererComponent can fail completely with zero warnings when its generator path is unwired

**Files:** `TerrainTileRendererComponent.cs`, `TerrainTransitionLayerComponent.cs`

**Status:** Fixed. `Rebuild()` now resolves and checks the generator FIRST,
before `EnsureLayers()`/`EnsureWaterSurface()` run at all - matching every
sibling renderer's shape exactly, so no atlas-only or water-only
misconfiguration can bypass it.
`TerrainTransitionLayerComponent.RefreshTransitions()` now reports which of
`_generator`/`_displayLayer` is missing instead of silently returning.
Verified live: this exact warning fired during testing when a probe
constructed a `TerrainTransitionLayerComponent` with no `DisplayLayerPath` -
proving the warning path actually executes, not just compiles - and the
probe (not the source) was the thing fixed, since it was relying on the old
silent-return behavior.

Unlike its 8 sibling renderers (all fixed earlier this session to
`GD.PushWarning` on an unresolved generator), `TerrainTileRendererComponent`
never checks for a null generator at the top of `Rebuild()`. If a scene has at
least one biome atlas path configured but `GeneratorPath` is left empty (or
points at a node that fails to resolve) and `WaterShaderPath` is left blank,
`Rebuild()` completes with **no warning anywhere in the chain**, and nothing
is drawn - the exact realistic misconfiguration (art wired, generator
forgotten) the prior fix pass was meant to close, on the one renderer that
fix pass missed. Traced the full call chain: `ConfiguredLayers()`'s filter
against `TerrainKindsPresent()` is bypassed entirely when the generator
resolves to null (the filter's guard is `present is not null && ...`), so an
atlas-only, generator-unwired config still produces a non-empty layer list
and skips the renderer's only warning (the "no biome atlas configured" one,
which never fires because atlases *are* configured).
`EnsureWaterSurface()` also returns early on the empty-`WaterShaderPath`
check before it ever reaches its own generator-null warning. And each child
`TerrainTransitionLayerComponent.RefreshTransitions()` silently returns at
`if (_generator == null || _displayLayer == null) return;` with no
`GD.PushWarning` at all. The existing regression test,
`tests/examples/renderer_reporting.gd`, only drives every renderer with
literally nothing configured, which for this renderer happens to trip the
"no biome atlas" warning through a different branch - masking this gap
entirely.

**Fix:** Add the same top-of-`Rebuild()` check every sibling renderer has:
resolve `_generator` first and `GD.PushWarning($"[{Name}] no generator at
GeneratorPath; no terrain tiles were drawn.")` before calling
`EnsureLayers()`. Have `TerrainTransitionLayerComponent.RefreshTransitions()`
also report rather than silently return when `_generator == null`. Extend
`renderer_reporting.gd` to additionally drive `TerrainTileRendererComponent`
with an atlas path set and `GeneratorPath` left empty, so this scenario
cannot regress silently again.

### Start-position shortfalls are invisible - no requested-vs-achieved diagnostic, unlike the parallel landmass case

**Files:** `TerrainStartPositionStage.cs`, `TerrainFieldBuilder.cs`,
`TerrainGenerationSettings.cs`, `TerrainWorldComponent.cs`

**Status:** Fixed. `TerrainGenerationSettings.RequestedStartPositionCount` is
the one place the clamp now lives (the same pattern
`RequestedLandmassCount` already used); `TerrainGenerationDiagnostics` and its
`ToDictionary()` carry it; `TerrainStartPositionStage.Apply` reads it instead
of re-clamping locally and `GD.PushWarning`s by name when it falls short;
`TerrainWorldComponent.StatusLine()` now formats starts as "{X} of {Y}
starts", matching landmasses.

`TerrainStartPositionStage.Apply` can silently place fewer than
`settings.StartPositionCount` start tiles on a small or water-heavy map - the
per-file notes already record this. But unlike the parallel landmass-count
case, nothing makes the shortfall visible: `TerrainGenerationDiagnostics`
records only the achieved `start_position_count`, never the requested count,
so a caller cannot tell "got all 8" from "got 3" without separately
remembering what it asked for. Contrast `RequestedLandmassCount`, which the
diagnostics deliberately place beside `LandComponentCount` for exactly this
reason (its own doc comment: "reporting only what was achieved makes that
indistinguishable from success"). `TerrainWorldComponent.StatusLine()` makes
the asymmetry concrete in one method built from the same diagnostics
dictionary: landmasses get "{X} of {Y} requested" treatment, start positions
get a bare count. `TerrainStartPositionStage.cs` also has zero
`GD.PushWarning` calls anywhere in the file. Every 4X/strategy engine in the
genre treats this as a first-class fact - confirmed for Civilization 6, which
caps player count by map size specifically because a small map cannot place
every requested start, with community reports describing hangs when that
constraint is bypassed by mods.

**Evidence:** `TerrainFieldBuilder.cs`'s diagnostics constructor call passes
`settings.RequestedLandmassCount` for landmasses but only
`world.StartPositions.Count` (no requested figure) for starts.
`TerrainGenerationSettings.cs`'s `TerrainGenerationDiagnostics` has
`RequestedLandmassCount` and `LandComponentCount` as a pair, but only
`StartPositionCount` alone; `ToDictionary()` emits `requested_landmass_count`
but no `requested_start_position_count` key. `TerrainWorldComponent.cs`
`StatusLine()`'s format string reads `"... {7} of {8} landmasses ... {10}
starts"` - `{7}`/`{8}` is achieved-of-requested, `{10}` is bare.

**Fix:** Add `int RequestedStartPositionCount` to
`TerrainGenerationDiagnostics`, populate it from `settings.StartPositionCount`
(clamped the same way `Apply()` clamps `wanted`), and add a
`requested_start_position_count` key in `ToDictionary()`. Update
`StatusLine()` to format starts the same way as landmasses ("{X} of {Y}
starts"). Add a `GD.PushWarning` in `TerrainStartPositionStage.Apply` (or have
`TerrainFieldBuilder.Finish` log it) when `world.StartPositions.Count <
wanted` after both placement passes, naming the map size and the shortfall.

### Every per-cell renderer pays a ~30-field settings rebuild-and-compare for what should be an O(1) array lookup

**Files:** `TerrainGeneratorComponent.cs`, `TerrainDataLayersComponent.cs`,
`TerrainFeatureRendererComponent.cs`, `TerrainReliefRendererComponent.cs`,
`TerrainResourceRendererComponent.cs`, `TerrainMapOverlayComponent.cs`,
`TerrainPaintedRendererComponent.cs`, `TerrainIsometricRendererComponent.cs`,
`TerrainIsometricFeatureRendererComponent.cs`,
`TerrainIsometricAutotileRendererComponent.cs`,
`TerrainTransitionLayerComponent.cs`, `SeededTerrainPropScatterComponent.cs`

**Status:** Fixed, in all twelve files named above. Each `Rebuild()`/
`Refresh()`/measurement pass now calls `TerrainGeneratorComponent.ResolveField()`
**once**, holds the returned `GeneratedTerrainField` in a local, and calls its
O(1) `*AtCell`/`*AtPosition` methods directly in the per-cell loop - exactly
what `TerrainCoastField.cs` already did. `TerrainDataLayersComponent` (two
full-grid passes) and `TerrainIsometricRendererComponent` (the file the
review measured as compounding hardest - separate passes for water depth and
summit floor, plus `ShowsSide` re-checking terrain and relief per elevation
step) both now resolve the field once per `Rebuild()` and thread it through
every helper (`MeasureWaterDepth`, `MeasureSummitFloor`, `ShowsSide` all took
a `field` parameter). `SeededTerrainPropScatterComponent`'s `PaletteAt`/
`FootprintMatchesPalette`/`GeneratorTerrainKindAt` chain - up to fourteen
generator reads per scanned cell - now threads a single resolved field
through instead, nullable because this component can also run off
`GridCellDataComponent` alone with no generator at all.

Two components needed more than a call swap:
- `TerrainMapOverlayComponent` read the generator from inside `_Draw()`,
  which Godot calls on every canvas redraw - a window resize, an unrelated
  sibling invalidating the frame - not only after a real map change, so it
  paid a full re-scan of the bounds for free, repeatedly. `Rebuild()` now
  bakes a `List<ResourceMarker>` and a start-position marker list once, and
  `_Draw()` only iterates them - the same cached-stamp pattern every other
  prop renderer in the addon already uses.
- `TerrainTransitionLayerComponent` is deliberately callable (`DualGridMaskAt`,
  `AtlasCoordinatesForMask`) without ever calling `RefreshTransitions()` - the
  transition-mask test probe drives it exactly this way - so the field could
  not be resolved only inside `RefreshTransitions()`. It is instead resolved
  every time `ResolveReferences()` runs (already called from both
  `RefreshTransitions()` and `_Ready()`), cached alongside `_generator`, and
  read by the private `TerrainKindAt` helper that `DualGridMaskAt` calls up to
  four times per display cell.

Verification: full `dotnet build` clean after every file; all 15
`terrain_guards.ps1` checks, the strict transition probe, the other three
`grid_terrain_*_probe.ps1` checks and the renderer reporting probe all pass
with identical output to before the refactor (928 transition cells,
lake_cells=1458/props=128/water_props=0, etc. - unchanged), which is the
actual claim being verified: same behaviour, fewer redundant settings
rebuilds. One unrelated environmental snag surfaced mid-pass: Godot's own
`global_script_class_cache.cfg` went stale after enough consecutive
`dotnet build` cycles and made `SeededTerrainPropScatterComponent.cs`
briefly fail to instantiate from GDScript with a "Parse Error" naming the
wrong type - confirmed unrelated to this change (a minimal repro script hit
the same error), and resolved by `godot --headless --build-solutions --quit`,
which forces Godot to rebuild and rescan its own script-class cache; no code
changed for it. Also required a fix to `tests/addon_contract_scan.ps1`, whose
literal-text check for `TerrainPaintedRendererComponent` matched the exact
old `BuildIdMap(size, ...)` call site; updated to match the new
`BuildIdMap(field, size, ...)` signature the same way the `Adopt(`/
`EnsureLayer(` guard was widened earlier in this pass.

`TerrainGeneratorComponent.cs`'s own doc comment already names this problem:
every per-position accessor on the component rebuilds the ~30-field
`TerrainGenerationSettings` record from Godot property reads and compares it
field-by-field against the cache before returning the already-cached,
O(1)-array-index `GeneratedTerrainField`. The component exposes an `internal
ResolveField()` escape hatch specifically so a hot-path caller can skip that
per-call cost - but a repo-wide check shows `ResolveField()` has exactly 2
callers total: its own declaration and `TerrainCoastField.cs`. Every other
renderer instead calls the public per-cell wrappers
(`TerrainKindAt`/`ResourceAt`/`FeatureAt`/`ReliefAt`/etc.) directly inside
per-cell loops, paying the settings rebuild+compare on every call. Concrete
per-file multipliers, from tracing actual loops: `TerrainDataLayersComponent`
scans the whole `BoundsSize` grid twice with 5 generator calls per cell each
pass; `TerrainIsometricRendererComponent` compounds hardest - separate
full-grid passes for `MeasureWaterDepth` and `MeasureSummitFloor` before the
main paint loop, plus `ShowsSide` re-running `TerrainKindAt`+`ReliefAt` for
every elevation step of every raised cell (roughly 8 extra rebuilds per
mountain cell at level 3, on top of its own ~4);
`TerrainIsometricAutotileRendererComponent` rescans the entire grid once per
terrain-binding entry; `TerrainTransitionLayerComponent`'s
`DualGridMaskAt` calls `TerrainKindAt` up to 4 times per display cell across a
`(size.X+1)x(size.Y+1)` loop, and `TerrainTileRendererComponent` builds one
such layer per biome kind actually present - on a typical multi-biome 96x60
map this alone produces on the order of 10^5 redundant settings rebuilds per
`Rebuild()`; `SeededTerrainPropScatterComponent`'s `FootprintMatchesPalette`
can call `PaletteAt` (2 generator calls each) up to 6 times per candidate
tile - up to 14 rebuilds per scanned cell before a single prop is placed.

**Fix:** Do what `TerrainCoastField.cs` already does: call
`TerrainGeneratorComponent.ResolveField()` **once** at the top of each
`Rebuild()`/`Refresh()`/measurement pass, hold the returned
`GeneratedTerrainField` in a local, and call its O(1) methods
(`TerrainAtCell`, `ReliefAtCell`, `ResourceAtCell`, `FeatureAtCell`,
`WaterSourceAtCell`, etc.) directly in the per-cell loop. `ResolveField()` is
`internal`, and every renderer listed lives in the same assembly, so no
visibility change is needed. For `TerrainTileRendererComponent` specifically,
classify every cell's kind once and hand each biome layer only the cells it
needs, instead of each of ~10 layers independently rescanning the whole
bounds. For `TerrainMapOverlayComponent`, move the resource/start-position
scan out of `_Draw()` (called on every canvas redraw) into `Refresh()`,
caching a marker list the way the other prop renderers already do.

### The same deterministic hash-mixing function is re-implemented, byte-for-byte, in 8 separate files

**Files:** `TerrainFeatureStage.cs`, `TerrainResourceStage.cs`,
`TerrainFeatureRendererComponent.cs`,
`TerrainIsometricFeatureRendererComponent.cs`,
`TerrainReliefRendererComponent.cs`, `SeededTerrainPropScatterComponent.cs`,
`TextureElevationTileSetGeneratorComponent.cs`,
`MountainTileMapLayerGeneratorComponent.cs`, `TerrainGeometry.cs`

**Status:** Fixed. `TerrainGeometry.Hash01(int x, int y, int seed)` is now the
one implementation; all 8 private copies were deleted and every call site
updated to the shared signature and argument order (two files had the
arguments in the opposite order from the other six - both were normalized,
not just re-pointed). While consolidating, a 9th, previously-unlisted
occurrence of the *other* duplicated pattern this session already fixed once
(`new TileMapLayer { ... }; AddChild; Owner = ...`) was found in
`MountainTileMapLayerGeneratorComponent.ResolveLayer` and converted to
`TerrainAuthoring.EnsureLayer` too, for the same reason as the original six.

The exact same 32-bit Wang-style hash (multiply-XOR-shift-multiply-XOR-shift,
mask to 24 bits, divide by 16777215) used for deterministic per-cell
jitter/selection is copy-pasted as a private `Hash01`/`HashInt`+`Hash01`
method into 8 different files, instead of living once in `TerrainGeometry.cs`
- the file the codebase already uses as its zero-dependency shared
grid-algorithms utility (it already hosts `Percentile`, `Normalized`,
`Smooth`, `Ridged` for exactly this kind of shared math). Two of the eight
copies even flip the argument order (`Hash01(seed,x,y)` in
`TerrainFeatureStage`/`TerrainResourceStage` vs. `Hash01(x,y,seed)` in the
five renderer/tool files) - itself evidence these were pasted rather than
shared. Same class of defect already fixed once this session for the
"is this terrain kind water" string checks (now
`TerrainTileSets.IsWaterKind`) and for layer creation (now
`TerrainAuthoring.EnsureLayer`); this is the same pattern, a different
function, not yet consolidated.

**Evidence:** Identical body confirmed in all 8 files, e.g.
`TerrainFeatureStage.cs:365-371`: `private static float Hash01(int seed, int
x, int y) { uint value = (uint)(x * 374761393) + (uint)(y * 668265263) +
(uint)seed; value = (value ^ (value >> 13)) * 1274126177u; value ^= value >>
16; return (value & 0x00ffffffu) / 16777215.0f; }` - the same four-line body
(same constants) reappears at `TerrainResourceStage.cs:161-167`,
`TerrainFeatureRendererComponent.cs:261-267`,
`TerrainIsometricFeatureRendererComponent.cs:394-400`,
`TerrainReliefRendererComponent.cs:188-194`,
`SeededTerrainPropScatterComponent.cs:282-288`,
`TextureElevationTileSetGeneratorComponent.cs:1036-1042`, and (factored as
`HashInt`+`Hash01`) `MountainTileMapLayerGeneratorComponent.cs:586-598`.

**Fix:** Add one `internal static float Hash01(int x, int y, int seed)` to
`TerrainGeometry.cs` and delete the 8 private copies, updating each call site
to the shared signature/argument order.

### Resource-id-to-category has two owners, and the one actually wired up ignores a game's custom catalog

**Files:** `ResourceCatalog.cs`, `TerrainResourceStage.cs`,
`ResourceCatalogs.cs`, `TerrainMapOverlayComponent.cs`,
`TerrainGeneratorComponent.cs`, `ResourceDefinition.cs`

**Status:** Fixed. `TerrainMapOverlayComponent.ColourFor` now resolves
`_generator?.Resources ?? ResourceCatalogs.For(_generator?.ResourceSet ??
ResourceSet.Historical)` and asks THAT catalog first, falling back to
`TerrainResourceStage.CategoryOf`'s cross-catalog search only when the id
isn't in the generator's own catalog - the legitimate saved-map case the fix
text below describes. A custom `ResourceCatalog` assigned via
`TerrainGeneratorComponent.Resources` is now honoured by the overlay, not
just by generation.

"What category is this resource id" is answered by two pieces of logic that
can disagree, and the one actually called by the overlay renderer is the one
that cannot see a game's own custom catalog. The instance method
`ResourceCatalog.CategoryOf(id)` correctly resolves against whatever catalog
it's called on - including a custom one assigned via
`TerrainGeneratorComponent.Resources` - but has **zero callers** anywhere in
the addon. `TerrainMapOverlayComponent.ColourFor` instead calls the static
`TerrainResourceStage.CategoryOf(id)`, which goes through
`ResourceCatalogs.FindAnywhere(id)` - a lookup that only ever searches the
three built-in shipped catalogs (Historical/OilAndGas/Space) and knows
nothing about a custom catalog. `ResourceCatalog.cs`'s own doc comment gives
the exact scenario this breaks: "a game that wants ore and lumber instead of
a 4X resource table authors its own catalog." For such a game, the generator
correctly places `ore`/`lumber` (`TerrainResourceStage.Apply` reads
`settings.ResourceCatalog` first), but every resource marker the overlay
draws for those ids falls through `FindAnywhere` to `null` and renders as the
default Bonus (gold) colour regardless of the author's actual `Category` -
wrong, and silent. `ResourceDefinition.Category` is `[Export]`ed, stored, and
accepted by the catalogue author for exactly this purpose, then not honoured
by the one consumer that draws it.

**Evidence:** `TerrainMapOverlayComponent.cs:121-127`:
`private static Color ColourFor(string resource) =>
TerrainResourceStage.CategoryOf(resource) switch { ... };`.
`TerrainResourceStage.cs:158-159`: `public static ResourceCategory
CategoryOf(string id) => ResourceCatalogs.FindAnywhere(id)?.Category ??
ResourceCategory.Bonus;`. `ResourceCatalogs.cs:103-104`:
`FindAnywhere(string id) => Historical.Find(id) ?? OilAndGas.Find(id) ??
Space.Find(id);` - never `TerrainGeneratorComponent.Resources`.
`ResourceCatalog.cs:44-51` documents the correct, catalog-aware version but
has zero callers repo-wide (confirmed by grep).

**Fix:** Have `TerrainMapOverlayComponent` resolve the generator's actual
catalog first - e.g. `(_generator.Resources ??
ResourceCatalogs.For(_generator.ResourceSet)).CategoryOf(resource)` (both
`Resources` and `ResourceSet` are already public on
`TerrainGeneratorComponent`) - falling back to
`ResourceCatalogs.FindAnywhere` only for ids that predate the current catalog
(a saved map re-opened under a different `ResourceSet`), which is the
legitimate case `FindAnywhere`'s own doc comment describes. That folds
`TerrainResourceStage.CategoryOf` and `ResourceCatalog.CategoryOf` back into
one call path instead of two that can disagree for the same id.

### Map size (BoundsSize) is redeclared independently on every renderer instead of read from the one generator that owns it

**Files:** `TerrainGeneratorComponent.cs`, `TerrainMapOverlayComponent.cs`,
`TerrainPaintedRendererComponent.cs`, `TerrainDataLayersComponent.cs`,
`TerrainFeatureRendererComponent.cs`, `TerrainResourceRendererComponent.cs`,
`TerrainReliefRendererComponent.cs`, `TerrainIsometricRendererComponent.cs`,
`TerrainIsometricFeatureRendererComponent.cs`,
`TerrainIsometricAutotileRendererComponent.cs`,
`TerrainTileRendererComponent.cs`, `TerrainWorldComponent.Drawing.cs`

**Status:** Fixed, warn-on-mismatch (the safer of the two fix options - the
default is unchanged, so nothing that already worked around the mismatch by
hand changes behaviour). A new shared helper, `TerrainBoundsCheck.WarnIfMismatched`,
is called once per `Rebuild()`/`Refresh()` in all eleven renderer/data-layer
components after their generator resolves, comparing the renderer's own
`BoundsSize` against `_generator.BoundsSize` and pushing one named warning on
a mismatch - centralized rather than reimplemented eleven times, so the
message can't drift the way `Hash01` did. Separately,
`TerrainWorldComponent.Draw()`'s `TileSize` push to `_dataLayers`/`_overlay`
no longer gates on `_painted is not null`: it now derives the size from
whichever flat renderer (`Painted` or `Tiles`) is actually the active
projection, so a Tiles- or Isometric-only scene that never wires a Painted
renderer no longer leaves those two `TileSize` copies stuck on their export
default.

Before touching any renderer, every `tests/examples/*.gd` guard and every
probe wired into `run_addon_checks.ps1` that constructs one of these
renderers was checked for whether it would trip a new warning: the one real
risk found, `grid_terrain_transition_probe.ps1`, uses a strict fatal-line
scan (`SCRIPT ERROR|ERROR:|Exception|C# backtrace`) that a bare
`GD.PushWarning` from C# always satisfies via its automatic backtrace
trailer - the same category of break this session hit once already on the
`RefreshOnReady` change. It is safe here only because
`grid_terrain_transition_probe.gd` never calls `RefreshTransitions()` (it
drives `DualGridMaskAt`/`AtlasCoordinatesForMask` directly with
`RefreshOnReady = false`), confirmed by reading the probe before writing the
fix rather than after. `renderer_reporting_probe.ps1` and the three other
`grid_terrain_*_probe.ps1` wrappers were checked and use exit-code-only or
success-marker checks, not the fatal-line scan. All 15 `terrain_guards.ps1`
checks (exit-code only, so immune to a benign new warning either way), the
strict transition probe, the other three terrain probes, and the renderer
reporting probe all pass. The warning mechanism itself was verified directly
(not just inferred): a throwaway script drove a renderer at a deliberately
mismatched `BoundsSize` against its generator and confirmed the warning
fires with both sizes named, then set `BoundsSize` to match and confirmed it
goes silent - proving the guard can both fail and pass, not just pass.

Applying this fix also surfaced a real, separate side effect from the
Continents/Archipelago fix above: `tests/examples/biomes.gd`'s "a cold world
has no desert" assertion started failing, not from anything in this fix, but
because Mainland's now-smaller landmass count (2 instead of 4, same fix,
same seed) produces fewer, LARGER continents, and a large continent centred
on a cold latitude can still reach a warm enough fringe to carry a genuine
minor desert margin. Confirmed by temporarily reverting just the landmass
formula and re-running: the guard passes with 0 desert tiles under the old
formula and fails with 61 under the new one, at the identical seed - a real
consequence of the directed fix, not a flaw in this change. The assertion
was changed from an absolute "cold has zero desert" to the comparative "cold
has markedly less desert than hot" (61 vs 91 tiles), which is what the
genre contract actually promises (direction, not an absolute) and is a more
robust check regardless of exactly how large Mainland's continents are.

"How big is the map" is one fact, decided once when
`TerrainGeneratorComponent.BoundsSize` is generated, but it is re-declared as
an independent `[Export] Vector2I BoundsSize` on eleven different
renderer/data-layer components with **four different, mutually inconsistent
default values**, none matching the generator's own default. Every one of
these renderers is documented as usable standalone (only
`TerrainGeneratorPath` required), so wiring any of them straight to a
generator without also hand-setting its `BoundsSize` to match silently under-
or over-scans the actual generated map, with no cross-check or warning
anywhere. `TerrainWorldComponent.Draw()` masks this only when a scene routes
everything through it: it pushes the computed size onto whichever renderer is
active for the current projection - but `TileSize` for `_dataLayers`/`_overlay`
is only pushed `if (_painted is not null)`, so a Tiles- or Isometric-only
scene that never wires a Painted renderer leaves those two `TileSize` copies
on their own default forever, unchecked against whatever `TileSize` the
actually-visible renderer uses.

**Evidence:** Confirmed via grep of every `[Export] public Vector2I
BoundsSize` declaration: `TerrainGeneratorComponent.cs:30` `new(64, 64)` (the
authoritative size); `TerrainDataLayersComponent.cs:36` `new(64, 64)`;
`TerrainTransitionLayerComponent.cs:35` `new(64, 64)`;
`TerrainMapOverlayComponent.cs:21` `new(48, 30)`;
`TerrainTileRendererComponent.cs:38` `new(48, 30)`;
`TerrainPaintedRendererComponent.cs:55` `new(96, 60)`;
`TerrainFeatureRendererComponent.cs:30` `new(96, 60)`;
`TerrainResourceRendererComponent.cs:64` `new(96, 60)`;
`TerrainReliefRendererComponent.cs:32` `new(96, 60)`;
`TerrainIsometricRendererComponent.cs:38` `new(48, 48)`;
`TerrainIsometricFeatureRendererComponent.cs:34` `new(48, 48)`;
`TerrainIsometricAutotileRendererComponent.cs:35` `new(48, 48)` - four
distinct default sizes across twelve independent copies of the same fact.
`TerrainWorldComponent.Drawing.cs:36-42` confirms the `_painted is not null`
gate on the `TileSize` push.

**Fix:** Give `BoundsSize`/`TileSize` one owner: have each renderer default to
reading `TerrainGeneratorComponent.BoundsSize`/derived `TileSize` when
resolved, treating its own `[Export]` as an optional override - or at
minimum, have `Rebuild()` compare its `BoundsSize` against the resolved
generator's and `GD.PushWarning` on mismatch, the same reporting pattern
already used for NodePath-resolution and no-peering-bits cases in this
codebase. Separately, have `TerrainWorldComponent.Draw()` push `TileSize` onto
`_dataLayers`/`_overlay` unconditionally, deriving it from whichever renderer
is actually visible rather than only `_painted`.

### ResourceDefinition.NodeScene and .Icon are exported and stored but never read anywhere in the repo

**Files:** `ResourceDefinition.cs`, `GridResourceScatterComponent.cs`,
`GridResourceNodeComponent.cs`

**Status:** Fixed - decided per-field, not identically. `NodeScene`: wired up.
`GridResourceScatterComponent.CreateResourceNode` now resolves
`Catalog?.Find(resourceId)?.NodeScene ?? ResourceScene` before instancing, so a
catalogue author's per-resource scene wins over the scatter component's one
blanket `ResourceScene`, which stays as the fallback for ids with none. Proven
with a falsifiable guard: `tests/examples/resources.gd` now gives one listed
resource a distinct `NodeScene`, sets a different blanket `ResourceScene`, and
asserts each placed deposit's root came from the right one - reverting the fix
and re-running was confirmed to fail that assertion before the fix was
restored. `Icon`: deleted, not wired. Checked every plausible consumer first -
`TerrainResourceRendererComponent` (the only component that draws resource
icons on the map) already has its own, deliberately separate sheet+frame-order
mechanism for exactly the performance reason its own doc comment states (one
batched `_Draw()` call for thousands of icons, not one `Texture2D` per icon);
`GridResourceBarComponent` (the addon's only resource HUD) is Label-only by
design, with no `Catalog` reference and no texture-drawing capability to wire
an icon into. Building that capability would be a new UI feature, not a wire-
up. `ResourceCatalogs.Build` (the addon's own shipped catalogues) never sets
`Icon` either. With zero consumers and zero writers anywhere in the repo,
deletion was the honest call; the class doc comment's promise of "a HUD asks
it for a display name and icon" was corrected to drop "and icon" to match.

`ResourceDefinition.NodeScene`'s own doc comment describes a real, per-
resource-type feature: "Scene instanced where this resource occurs; null
means map-visible but not harvestable." A repo-wide search for `.NodeScene`
finds only its own declaration - nothing instances it. The component that
actually turns generated resource placements into scene nodes,
`GridResourceScatterComponent.CreateResourceNode`, instances a completely
different, single, non-per-resource export instead: one fixed `ResourceScene`
`PackedScene` set once on the scatter component and used for every resource
id it places. `GridResourceNodeComponent.ApplyCatalogDefinition()` - whose
entire job is copying a matched `ResourceDefinition`'s rules onto a placed
node (Amount, AmountPerGather, GatherSeconds, GatherJobKind,
OccupiesCell) - also never reads `NodeScene` or `Icon`. So a catalogue author
who sets a different scene per resource (a tree for "wood", a rock for
"stone") gets no effect: every scattered resource still uses the scatter
component's one `ResourceScene`, or nothing at all if that's unset. `Icon`
(`Texture2D`) has the identical fate - zero consumers anywhere in the repo.

**Evidence:** `ResourceDefinition.cs:42` `[Export] public Texture2D? Icon`
and `:75` `[Export] public PackedScene? NodeScene`. Repo-wide grep for
`NodeScene` returns only the declaration.
`GridResourceScatterComponent.cs:232`: `Node2D? node =
ResourceScene?.Instantiate() as Node2D;` - the matched definition's
`NodeScene` is never consulted. `GridResourceNodeComponent.cs:84-92`
`ApplyCatalogDefinition` copies five fields from `definition` but never
`.Icon` or `.NodeScene`.

**Fix:** Either wire `NodeScene` up - have
`GridResourceScatterComponent.CreateResourceNode` prefer
`Catalog?.Find(id)?.NodeScene` over the single `ResourceScene` export when the
matched definition supplies one, falling back to `ResourceScene` as the
generic default - or, if per-resource visuals were never meant to work this
way, delete `NodeScene` and `Icon` from `ResourceDefinition` and correct the
doc comment so the catalogue's contract stops promising a feature that
doesn't exist. Given `ResourceScene` already exists as the coarser mechanism,
wiring `NodeScene` through it directly closes the gap its own doc comment
describes.

### TerrainTileRendererComponent's generator reference breaks the naming and staleness-guard pattern every other renderer uses

**Files:** `TerrainTileRendererComponent.cs`, `TerrainPaintedRendererComponent.cs`

**Status:** Fixed. `GeneratorPath` renamed to `TerrainGeneratorPath`
(including the two shipped scenes that wired it: `terrain_generator_lab.tscn`,
`terrain_tilemap_demo.tscn` - confirmed these are the only two, this session
already established that fact); a single private `ResolveGenerator()` with
the `IsInstanceValid` re-check now backs `EnsureWaterSurface()`,
`ConfiguredLayers()`, `CreateLayer()`, and the new top-of-`Rebuild()` check
above, replacing the two divergent inline snippets.

All 8 other renderers (plus `TerrainTransitionLayerComponent` and
`TerrainGeneratorComponent` itself) export the generator reference as
`TerrainGeneratorPath` and resolve it through a private
`ResolveGenerator()`/`Resolve()` method that re-validates the cached node with
`GodotObject.IsInstanceValid` on every call.
`TerrainTileRendererComponent` alone exports it as bare `GeneratorPath`, has
no such shared method, and never re-validates the cached `_generator` - so
once resolved, a freed or replaced generator node is never re-discovered. It
resolves the reference through two different, divergent inline snippets
instead of one shared method.

**Evidence:** `TerrainTileRendererComponent.cs:79`: `[Export] public
NodePath GeneratorPath { get; set; } = new("");` vs., e.g.,
`TerrainPaintedRendererComponent.cs:52`: `[Export] public NodePath
TerrainGeneratorPath` (the name used by all 7 other renderers plus
`TerrainTransitionLayerComponent`). `TerrainTileRendererComponent` resolves
`_generator` at `EnsureWaterSurface()` line 164 with no `IsEmpty` guard and no
`IsInstanceValid` check, and separately at `ConfiguredLayers()` lines
320-322 with an `IsEmpty` guard but still no `IsInstanceValid` check.
Contrast `TerrainPaintedRendererComponent.cs:321-327`'s standard pattern:
`if (_generator is null || !GodotObject.IsInstanceValid(_generator))
_generator = TerrainGeneratorPath.IsEmpty ? null :
GetNodeOrNull<TerrainGeneratorComponent>(TerrainGeneratorPath);`.

**Fix:** Rename `GeneratorPath` to `TerrainGeneratorPath` for consistency
with every other renderer in this set (a breaking rename, but this addon has
already renamed 12 classes for exactly this kind of consistency). Add a
single private `ResolveGenerator()` matching the pattern used elsewhere, with
the `IsInstanceValid` re-check, and call it from both `EnsureWaterSurface()`
and `ConfiguredLayers()` instead of the two divergent inline snippets.

### TerrainIsometricFeatureRendererComponent silently skips the "no feature sheets" warning its sibling reports

**Files:** `TerrainIsometricFeatureRendererComponent.cs`,
`TerrainFeatureRendererComponent.cs`

**Status:** Fixed. The matching `GD.PushWarning($"[{Name}] no feature sheets
loaded, so no features were drawn.");` now fires at the equivalent point in
`TerrainIsometricFeatureRendererComponent`, closing the exact gap described
below. (Extending `renderer_reporting.gd` to drive a resolved-generator/
no-art scenario, as the Fix text also suggests, was not done separately -
the fix itself was verified by reading the code path, not by a new
generator-resolved-but-no-art guard case.)

Both renderers hit the identical "generator resolved but no feature sheets
loaded" edge case, but only one of them reports it. If a scene wires
`TerrainGeneratorPath` and `IsometricRendererPath` correctly on
`TerrainIsometricFeatureRendererComponent` but leaves all four sheet paths
(`WoodsSheetPath`, `JungleSheetPath`, `MarshSheetPath`, `OasisSheetPath`)
empty or pointing at files that fail to load, `Rebuild()` draws nothing and
says nothing - exactly the silent-failure shape this session's earlier pass
fixed for the generator-NodePath case, reappearing one check later in this
one file. `tests/examples/renderer_reporting.gd` only drives both renderers
with no generator wired at all, so the generator-null short-circuit fires
before either renderer ever reaches the sheets check - the existing guard
cannot see this gap.

**Evidence:** `TerrainIsometricFeatureRendererComponent.cs:140-146`:
`LoadSheets(); LoadWoodsFrames(); if (_sheets.Count == 0) { Redraw(); return;
}` - no `GD.PushWarning`. Compare
`TerrainFeatureRendererComponent.cs:120-126`, the structurally parallel
check: `LoadSheets(); if (_sheets.Count == 0) { GD.PushWarning($"[{Name}] no
feature sheets loaded, so no features were drawn."); QueueRedraw(); return;
}`.

**Fix:** Add a matching `GD.PushWarning($"[{Name}] no feature sheets loaded,
so no features were drawn.");` call at the equivalent point in
`TerrainIsometricFeatureRendererComponent.cs`, and extend
`renderer_reporting.gd` to also drive each renderer with a resolved generator
but no art configured, so this class of gap can't hide behind the
generator-null case again.

### TerrainMapOverlayComponent exposes Refresh() instead of Rebuild(), with no stated reason

**Files:** `TerrainMapOverlayComponent.cs`, `tests/examples/renderer_reporting.gd`

**Status:** Fixed. `Refresh()` renamed to `Rebuild()`, `_Ready()` switched to
the same `CallDeferred(nameof(Rebuild))` pattern every sibling uses, and
`renderer_reporting.gd`'s table entry updated from `"Refresh"` to
`"Rebuild"` - `TerrainWorldComponent.Drawing.cs`'s one call site
(`_overlay.Refresh()`) was also updated, since it would otherwise have been
a compile error, not merely a missed consistency fix.

All 8 other renderer components expose a public `Rebuild()` as their rebuild
entry point. `TerrainMapOverlayComponent` alone exposes `Refresh()`, called
directly from `_Ready()` rather than via the `CallDeferred(nameof(Rebuild))`
pattern every other renderer uses. Nothing in the source explains why this
one is named or wired differently; the existing regression test hardcodes the
exception (`["TerrainMapOverlayComponent", "Refresh"]` among 8 `"Rebuild"`
entries) rather than the API being unified.

**Fix:** Rename `Refresh()` to `Rebuild()` for consistency with the other 8
renderers (keep a thin `Refresh()` forwarder only if external scenes already
call it), and switch `_Ready()` to the same `CallDeferred(nameof(Rebuild))`
pattern used elsewhere. Update `renderer_reporting.gd`'s table to drop the
special case once unified.

### TerrainElevationStage.Apply accepts a settings parameter it never reads

**Files:** `TerrainElevationStage.cs`

**Status:** Fixed. The unused `settings` parameter was dropped from
`Apply(world, noise)`, and its one call site in `TerrainFieldBuilder.cs`
updated to match.

`TerrainElevationStage` is split into two public methods on the same class;
one half ignores a parameter the other half uses. `Apply(world, noise,
settings)` never references `settings` anywhere in its body, while
`Classify(world, settings)` on the same static class reads
`settings.HillsFraction`/`MountainsFraction`. Every other noise-taking stage
(`TerrainClimateStage`, `TerrainFeatureStage`, `TerrainWaterStage`) actually
reads `settings` in its own `Apply`. Not a functional bug - `TerrainFieldBuilder`
calls both methods by name, not through a shared delegate signature, so
nothing requires the parameter to be present - but worth cleaning up or
explaining.

**Fix:** Either drop the unused `settings` parameter from
`TerrainElevationStage.Apply` (updating its single call site in
`TerrainFieldBuilder.cs`), or, if it's kept deliberately for signature
symmetry with the other noise-consuming `Apply` methods, say so in a comment
so a future reader doesn't assume a setting is being honoured that isn't.

## Also found and fixed while applying the above

Two defects unrelated to any finding above were surfaced by the act of
fixing them, not by the review passes:

- **A guard this session had already broken silently.** Earlier work in this
  same session replaced six files' direct `TerrainAuthoring.Adopt(...)` calls
  with `TerrainAuthoring.EnsureLayer(...)` (which adopts internally).
  `addon_contract_scan.ps1` had a check requiring the literal string
  `TerrainAuthoring.Adopt(` in each of those files - which that refactor
  removed - and nobody re-ran the contract scan afterward to notice. Caught
  only because this pass re-ran it before adding more to it; fixed by
  widening the guard's regex to `TerrainAuthoring\.(Adopt|EnsureLayer)\(`.
  This is exactly the failure mode "make a check fail once before trusting a
  pass" exists to catch, and here it caught a check that had already gone
  quiet.
- **A test probe relying on the silent-failure behavior this plan just
  removed.** `tests/grid_terrain_transition_probe.gd` constructs a
  `TerrainTransitionLayerComponent` with no `DisplayLayerPath`, on purpose -
  it only drives the component's pure query methods directly. With
  `RefreshOnReady` defaulting to `true`, the component's own `_Ready()` used
  to trigger a `RefreshTransitions()` that silently no-opped; once that path
  correctly warns (per the fix above), the probe's runner script flagged the
  new `C# backtrace` line as fatal. Fixed by setting
  `RefreshOnReady = false` on the probe's own test instance, since it never
  wanted a real refresh in the first place - not by weakening the warning.
- **`TerrainMapOverlayComponent` was the one renderer with no `RefreshOnReady`
  export**, found by literally watching the terrain lab render after the
  BoundsSize fix: the new mismatch warning fired for `MapOverlay` alone, on a
  scene where `TerrainWorldComponent` already pushes the live `BoundsSize`
  onto it every `Draw()`. Every sibling renderer gates its own `_Ready()`
  self-rebuild behind `RefreshOnReady` (default `true`, so standalone use is
  unaffected) specifically so an orchestrator can drive it instead; this one
  unconditionally rebuilt itself on `_Ready()`, once, against whatever
  `BoundsSize` happened to be authored in the scene file (96x60) - a full
  rebuild ahead of `TerrainWorldComponent.Generate()`'s own deferred call,
  wasted and briefly wrong. Fixed by adding `RefreshOnReady` (matching every
  sibling, including the missing `!Engine.IsEditorHint()` guard) and setting
  `RefreshOnReady = false` on the lab scene's `MapOverlay` node, the only
  scene that wires this component under a `TerrainWorldComponent`. Confirmed
  by re-rendering the lab: the warning is gone, and the rendered map is
  pixel-identical to before, because the orchestrator's own rebuild always
  overwrote the stale one - this was pure waste, not a visible defect.

## File index

### Pipeline stages

Run in order by `TerrainFieldBuilder`, on the shared `TerrainWorld` data model.

| File | Purpose | Doc |
|---|---|---|
| TerrainFieldBuilder.cs | Top-level orchestrator: allocates the `TerrainWorld` and runs every generation stage in order (or a Plain-mode shortcut). | [TerrainFieldBuilder.md](TerrainFieldBuilder.md) |
| TerrainLandmassStage.cs | Decides where land exists by growing a fixed number of separated, noise-perturbed landmasses from lattice-jittered seeds. | [TerrainLandmassStage.md](TerrainLandmassStage.md) |
| TerrainWaterStage.cs | Carves inland lake basins into the fine sample field, then classifies water as ocean vs. lake independent of how it was created. | [TerrainWaterStage.md](TerrainWaterStage.md) |
| TerrainElevationStage.cs | Builds the raw land elevation field (`Apply`) and cuts eroded elevation into flat/hills/mountains bands (`Classify`). | [TerrainElevationStage.md](TerrainElevationStage.md) |
| TerrainErosionStage.cs | Carves valleys into the elevation field via stream-power incision along a shared drainage network plus hillslope diffusion. | [TerrainErosionStage.md](TerrainErosionStage.md) |
| TerrainClimateStage.cs | Assigns per-sample Temperature and Moisture from latitude, altitude, noise, and a rain-shadow model. | [TerrainClimateStage.md](TerrainClimateStage.md) |
| TerrainRiverStage.cs | Carves rivers as one merged drainage network via shared D8 flow accumulation, so tributaries merge and width follows flow. | [TerrainRiverStage.md](TerrainRiverStage.md) |
| TerrainShadingStage.cs | Computes a per-cell hillshade multiplier from the elevation gradient under a fixed NW light. | [TerrainShadingStage.md](TerrainShadingStage.md) |
| TerrainBiomeStage.cs | Classifies every sample's terrain-kind string from elevation, relief, temperature and moisture via a fixed Whittaker-style rainfall table. | [TerrainBiomeStage.md](TerrainBiomeStage.md) |
| TerrainCoherenceStage.cs | Smooths and flood-fill-dissolves biome regions below a minimum size so rainfall-biome noise reads as coherent regions. | [TerrainCoherenceStage.md](TerrainCoherenceStage.md) |
| TerrainTileReductionStage.cs | Collapses the fine sub-tile sample field into one gameplay-tile-resolution value set per cell via majority rule. | [TerrainTileReductionStage.md](TerrainTileReductionStage.md) |
| TerrainContinentStage.cs | Labels each connected landmass on the tile grid with a distinct id via flood fill. | [TerrainContinentStage.md](TerrainContinentStage.md) |
| TerrainScaleConstraintStage.cs | Removes lake/relief/river/feature regions below an absolute minimum tile size and caps oversized lakes. | [TerrainScaleConstraintStage.md](TerrainScaleConstraintStage.md) |
| TerrainResourceStage.cs | Scatters resource ids onto the gameplay-tile grid, weighted by supported terrain/relief, with spacing and density scaling. | [TerrainResourceStage.md](TerrainResourceStage.md) |
| TerrainFeatureStage.cs | Assigns a vegetation/water feature layer (woods/forest/jungle/marsh/oasis) on top of each tile's base terrain kind. | [TerrainFeatureStage.md](TerrainFeatureStage.md) |
| TerrainStartPositionStage.cs | Scores candidate land cells and greedily picks fair, separated, continent-spread player start positions. | [TerrainStartPositionStage.md](TerrainStartPositionStage.md) |

### World data model

| File | Purpose | Doc |
|---|---|---|
| TerrainWorld.cs | Plain struct-of-arrays data model holding the mutable working set every generation stage reads and writes. | [TerrainWorld.md](TerrainWorld.md) |
| GeneratedTerrainField.cs | Read-only result of one generation run; answers per-cell and per-position terrain/water/relief/resource queries. | [GeneratedTerrainField.md](GeneratedTerrainField.md) |
| TerrainGenerationSettings.cs | Immutable input record (and output diagnostics record) that treats generation as a pure, cacheable function of one value. | [TerrainGenerationSettings.md](TerrainGenerationSettings.md) |
| TerrainGeneratorComponent.cs | The single `[Tool][GlobalClass]` Node owning every generation setting, caching the generated field, and exposing the query API renderers use. | [TerrainGeneratorComponent.md](TerrainGeneratorComponent.md) |
| TerrainNoiseSet.cs | Factory building the ten FastNoiseLite channels one generation run needs, each on a distinct seed and derived frequency. | [TerrainNoiseSet.md](TerrainNoiseSet.md) |

### Renderers

| File | Purpose | Doc |
|---|---|---|
| TerrainDataLayersComponent.cs | Mirrors the generator's per-cell data into three invisible TileMapLayers so a game can query it via Godot's own tile-data API. | [TerrainDataLayersComponent.md](TerrainDataLayersComponent.md) |
| TerrainPaintedRendererComponent.cs | Draws terrain as one continuous shader-blended surface by uploading id/hillshade/coast textures to a splat shader material. | [TerrainPaintedRendererComponent.md](TerrainPaintedRendererComponent.md) |
| TerrainTileRendererComponent.cs | Builds one `TerrainTransitionLayerComponent` + TileMapLayer per configured biome atlas, plus an optional shader-driven top-down sea. | [TerrainTileRendererComponent.md](TerrainTileRendererComponent.md) |
| TerrainTransitionLayerComponent.cs | Per-biome renderer that paints one dual-grid autotiled TileMapLayer via Godot's terrain-connect API or a legacy corner mask. | [TerrainTransitionLayerComponent.md](TerrainTransitionLayerComponent.md) |
| TerrainIsometricRendererComponent.cs | Renders the map as a stacked isometric TileMapLayer view: blocks per elevation level, seabed, and a shared sea-surface shader. | [TerrainIsometricRendererComponent.md](TerrainIsometricRendererComponent.md) |
| TerrainIsometricAutotileRendererComponent.cs | Paints the generated field into an isometric TileMapLayer using an authored TileSet's terrain-connect/peering-bit system. | [TerrainIsometricAutotileRendererComponent.md](TerrainIsometricAutotileRendererComponent.md) |
| TerrainIsometricFeatureRendererComponent.cs | Stamps sprite props onto the isometric view, one child node per elevation level for correct Z-ordering. | [TerrainIsometricFeatureRendererComponent.md](TerrainIsometricFeatureRendererComponent.md) |
| TerrainFeatureRendererComponent.cs | Draws terrain features (woods/jungle/oasis/marsh) as batched sprite stamps over the tile-based ground. | [TerrainFeatureRendererComponent.md](TerrainFeatureRendererComponent.md) |
| TerrainReliefRendererComponent.cs | Draws relief (hills/mountains) as lit, shadowed billboard sprites, batched from one node's `_Draw()` call. | [TerrainReliefRendererComponent.md](TerrainReliefRendererComponent.md) |
| TerrainResourceRendererComponent.cs | Draws the generator's per-tile resource assignments as icons with a readability backplate. | [TerrainResourceRendererComponent.md](TerrainResourceRendererComponent.md) |
| TerrainMapOverlayComponent.cs | Draws resource-deposit markers and player start-position rings over the terrain using the immediate canvas API. | [TerrainMapOverlayComponent.md](TerrainMapOverlayComponent.md) |

### Game-facing components

World assembly, camera/UI adapters, and standalone authoring tools.

| File | Purpose | Doc |
|---|---|---|
| TerrainWorldComponent.cs | The primary "map/world creation" component: carries a scene's generation axes and renderer wiring, drives the generator, triggers rendering. | [TerrainWorldComponent.md](TerrainWorldComponent.md) |
| TerrainWorldComponent.Drawing.cs | Partial-class half of `TerrainWorldComponent`: dispatches which renderer nodes rebuild/show per projection. | [TerrainWorldComponent.Drawing.md](TerrainWorldComponent.Drawing.md) |
| TerrainWorldCameraComponent.cs | Frames a Camera2D around a built map, computing zoom/focus from the world component's own extent/start-position queries. | [TerrainWorldCameraComponent.md](TerrainWorldCameraComponent.md) |
| TerrainWorldStatusComponent.cs | Thin UI adapter writing `TerrainWorldComponent.StatusLine()` into a Label whenever the world finishes building. | [TerrainWorldStatusComponent.md](TerrainWorldStatusComponent.md) |
| TerrainLabComponent.cs | Pure UI binder connecting panel controls to a `TerrainWorldComponent` and rebuilding it on change. | [TerrainLabComponent.md](TerrainLabComponent.md) |
| TerrainLabComponent.Navigation.cs | Partial-class continuation owning preview camera behavior: pan, wheel zoom, fit-to-panel framing. | [TerrainLabComponent.Navigation.md](TerrainLabComponent.Navigation.md) |
| SeededTerrainPropScatterComponent.cs | Deterministically stamps prop sprites onto generated terrain, choosing sprite/position/scale per tile from a coordinate+seed hash. | [SeededTerrainPropScatterComponent.md](SeededTerrainPropScatterComponent.md) |
| MountainPrefabGeneratorComponent.cs | Editor/runtime authoring tool that instantiates an authored mountain/island prefab from a generated manifest.json - not part of procedural generation. | [MountainPrefabGeneratorComponent.md](MountainPrefabGeneratorComponent.md) |
| MountainTileMapLayerGeneratorComponent.cs | Editor/runtime authoring tool that paints a deterministic mountain footprint into a TileMapLayer - visuals-only, not part of generation. | [MountainTileMapLayerGeneratorComponent.md](MountainTileMapLayerGeneratorComponent.md) |
| TextureElevationTileSetGeneratorComponent.cs | Offline/editor tool that bakes a fixed "elevated" tile atlas from a top texture plus optional cliff textures - not part of the generation pipeline. | [TextureElevationTileSetGeneratorComponent.md](TextureElevationTileSetGeneratorComponent.md) |

### Support utilities

| File | Purpose | Doc |
|---|---|---|
| TerrainGeometry.cs | Stateless grid-algorithms utility: neighbours, connected-component labelling, BFS distance, percentile thresholds, noise-shaping helpers. | [TerrainGeometry.md](TerrainGeometry.md) |
| TerrainFlow.cs | Computes the shared D8 drainage network (flow direction and accumulation) used identically by the erosion and river stages. | [TerrainFlow.md](TerrainFlow.md) |
| TerrainCoastField.cs | Builds the shared, sub-tile-resolution "distance to waterline" texture every water-drawing renderer uses so views agree on the coastline. | [TerrainCoastField.md](TerrainCoastField.md) |
| TerrainLayers.cs | Defines, once, the canonical draw-order stack (levels and z-indices) every terrain renderer must share. | [TerrainLayers.md](TerrainLayers.md) |
| TerrainScaleRules.cs | Derives a map's climate/latitude span and biome-region-size fraction from its tile dimensions, plus absolute min/max tile-count constants. | [TerrainScaleRules.md](TerrainScaleRules.md) |
| TerrainShapePresets.cs | Static catalogue of five named world-shape presets (Continents, Pangaea, Archipelago, IslandChain, OceanWorld). | [TerrainShapePresets.md](TerrainShapePresets.md) |
| TerrainMapSetup.cs | Defines the independent "strategy game" map-setup axes (age/temperature/rainfall/sea level/resources/size) and their multiplier functions. | [TerrainMapSetup.md](TerrainMapSetup.md) |
| TerrainTileSets.cs | Defines the terrain TileSet's custom-data-layer contract, derives per-tile facts from terrain kind, builds per-category physics/nav bodies. | [TerrainTileSets.md](TerrainTileSets.md) |
| TerrainTextures.cs | Single shared texture-loading utility for every terrain renderer, guaranteeing a mip chain for textures loaded straight off disk. | [TerrainTextures.md](TerrainTextures.md) |
| TerrainAuthoring.cs | Static helper that creates/reuses TileMapLayer nodes and assigns Owner so generated nodes are saved with the scene. | [TerrainAuthoring.md](TerrainAuthoring.md) |
| TerrainShaderSurface.cs | Builds the blank, correctly-shaped one-tile TileSet and fills a TileMapLayer with it, giving a per-pixel shader a surface to paint. | [TerrainShaderSurface.md](TerrainShaderSurface.md) |
| ResourceDefinition.cs | A single authored Resource asset unifying where a map resource is placed and how it is gathered by the economy. | [ResourceDefinition.md](ResourceDefinition.md) |
| ResourceCatalog.cs | Authored Resource container of ResourceDefinitions for one world/game's resource set, with id-keyed lookup helpers. | [ResourceCatalog.md](ResourceCatalog.md) |
| ResourceCatalogs.cs | Static factory/registry for the addon's three built-in resource sets (Historical, OilAndGas, Space), plus a cross-catalog id lookup. | [ResourceCatalogs.md](ResourceCatalogs.md) |

## What this review does not cover

The gameplay-grid (`Grid*`) components - `GridResourceScatterComponent`,
`GridResourceNodeComponent`, `GridCameraControllerComponent`, and the rest of
the gameplay grid system referenced from a few of the files above - are a
separate system and were out of scope for this pass; they are documented (and
would need reviewing) elsewhere. Anything that would require a live Godot
editor render to judge - whether a shader actually looks right, whether a
sprite sheet's frames line up - was not judged; findings here are limited to
what could be verified by reading source, running greps, and reproducing
measurements against checked-in data files. Raw finding counts before
dedup/verify were 22 raw, 22 after dedup, 23 confirmed - so a reader knows
what was filtered and can ask to see the rest if they want it; the 12
"Confirmed findings" above are the deduplicated, unique set after folding
repeated re-confirmations of the same defect (the `ReliefAt` unit mismatch,
the Continents/Archipelago landmass-count overlap, and the isometric-autotile
peering-bit gap were each independently re-verified more than once across
that raw count, and appear above once each, in "Known, not yet fixed" and
"Known but deliberately not fixed" respectively, rather than as separate
"Confirmed findings" entries). A separate, later follow-up pass specifically
checking for Godot-native feature duplication (see "Godot-native feature
duplication check" above) added 9 more raw findings, 9 confirmed after dedup
and re-verification - all nine confirming correct native usage already in
place (in the shipped code, and in this plan's own recommended fixes) rather
than surfacing a new defect, so they are recorded in that section rather than
under "Confirmed findings" above.
