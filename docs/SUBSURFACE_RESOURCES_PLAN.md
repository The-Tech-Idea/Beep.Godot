# Subsurface & Water Resources — Design and Implementation Plan

Multi-stratum resources for the terrain engine and resource manager: oil, gas
and ore **under the ground**, fish and other yields **in the water**, extracted
by buildings and boats instead of only walk-up gathering. Written 2026-09-02
against the post-enhancement-plan tree; companion to
[ENGINE_ENHANCEMENT_PLAN.md](ENGINE_ENHANCEMENT_PLAN.md).

## Why, and what exists today

Today a cell has **one** resource string (`TerrainWorld.Resource[cell]`),
placed point-wise by `TerrainResourceStage`: single-cell deposits, spacing
apart, weighted by `ResourceDefinition.TerrainKinds`/`Weight`/relief. That
model conflates three different facts:

- **On the ground** — trees, deer, an iron outcrop. Works today: scatter
  spawns a `GridResourceNodeComponent`, a worker walks up and gathers.
- **In the water** — the stage already places fish on water cells (at
  `WaterDensityScale`), but they spawn as walk-up nodes **no land worker can
  ever reach**. Sea resources are currently decoration.
- **Under the ground** — does not exist at all, despite the shipped
  `OilAndGas` catalogue being exactly this fantasy: a licence block is bought
  for what is UNDER it, invisible from the surface, extracted by a placed
  derrick over time.

The plan gives each fact its own stratum, generated deterministically from the
seed like everything else, published through the same data layers, and
extracted through the existing job/build/production machinery.

## The model: three strata per cell

| Stratum | Where | Examples | Deposit shape | Extraction |
| --- | --- | --- | --- | --- |
| **Surface** | land cells | wood, deer, iron outcrop, meteoric debris | single cell (as today) | walk-up gather (as today) |
| **Liquid** | water cells | fish, kelp, pearls; methane slush on a Titan skin | single cell, coast-biased | boat workers gather; wharf/fishery building optional |
| **Underground** | under land **or** seabed | ore veins, oil & gas fields, water ice, volatiles | multi-cell **fields** with per-cell richness | extractor building (derrick, mine, offshore platform, ice extractor) |

A cell can carry up to one resource per stratum — fish can swim over an
offshore oil field. `Surface` keeps the existing array and accessors untouched;
liquid-stratum placements **move out** of it into their own array, so each
array keeps one meaning (a map's sea dots become liquid-layer dots — same
seed, same places, different query).

The middle stratum is named **Liquid**, not water: the terrain engine's water
kinds are strings a skin can restyle (a methane lake is `shallow_water` wearing
different art), and the stratum holds whatever floats in that liquid. Naming it
after water would be wrong on every non-Earth map and painful to rename once it
is in catalogues and saves.

## Piece by piece

### 1. `ResourceDefinition` — new axes (back-compatible defaults)

```csharp
[ExportGroup("Where it occurs")]
[Export] public ResourceStratum Stratum { get; set; } = ResourceStratum.Surface;
// Underground only: how large a field grows, as a 0..1 dial mapped to the
// noise threshold (0 = rare pockets, 1 = broad basins).
[Export(PropertyHint.Range, "0,1,0.01")] public float DepositScale { get; set; } = 0.35f;

// Underground only: how deep the deposit lies. Depth is a fact of the
// RESOURCE (ore is shallow, oil is deeper, rare metals are deep), authored
// as a band so an extractor can declare how far it reaches. Liquid depth
// needs no field - the water terrain kinds already encode it (shallow
// versus deep water) - and surface "height" is the existing
// elevation/relief occurrence rules.
[Export] public ResourceDepth Depth { get; set; } = ResourceDepth.Shallow;

[ExportGroup("How it is gathered")]
[Export] public ResourceExtraction Extraction { get; set; } = ResourceExtraction.WalkUpGather;
// Extractor mode: which GridBuildDefinition works this deposit. Used by the
// extractor to validate, by the UI to hint, never to hard-code gameplay.
[Export] public string ExtractorBuildId { get; set; } = "";
```

`ResourceDepth { Shallow, Mid, Deep }`. In Phase C the extractor declares the
deepest band it reaches (`ReachDepth`), so a basic mine works shallow ore but
a deep deposit demands the drilling rig - the tech-ladder hook space colony
and oil games both want. The band is published with the deposit in the data
layer, so pure tile readers get it without a catalogue lookup.

`Stratum = Surface` + `Extraction = WalkUpGather` are the defaults, so every
existing catalogue behaves exactly as before until authored otherwise. For
underground definitions, `Amount` becomes the per-cell amount at full richness;
`AmountPerGather`/`GatherSeconds` become the extractor's cycle.

### 2. Generation — `TerrainSubsurfaceStage` (new pipeline stage)

Runs after `TerrainResourceStage`. Two passes, both purely hash/noise-driven
from the seed:

- **Underground fields**: for each `Stratum = Underground` definition, a
  low-frequency seeded noise (unique seed offset per resource id) thresholded
  by `DepositScale`, gated by the definition's `TerrainKinds`/relief rules
  applied to the SURFACE above (oil under desert and shallow sea, ore under
  hills and mountains). Cells above threshold get the id plus a **richness**
  (0..1 from noise height above threshold). Overlapping candidates resolve by
  weight-then-hash, one id per cell.
- **Liquid**: the water-cell placement moves here from
  `TerrainResourceStage` (which becomes surface-land-only), now using
  `Stratum = Liquid` definitions and biased by the coast: fish
  concentrate within a few tiles of shore on open sea (the coast distance and
  fetch data already exist), thin out over deep water, skip lakes unless the
  definition allows them.

`TerrainWorld` gains `CellUndergroundResource[]`, `CellUndergroundRichness[]`,
`CellWaterResource[]`. `GeneratedTerrainField` gains `UndergroundResourceAtCell`,
`UndergroundRichnessAtCell`, `WaterResourceAtCell`. Diagnostics gain per-stratum
counts (the lab's status line shows them).

### 3. Publication — data layers

`TerrainDataLayersComponent` publishes two more invisible layers, exactly like
the six it has:

- `UndergroundData` — custom data `underground_resource` (string) and
  `richness` (float), one tile per (id, richness-band).
- `WaterResourceData` — custom data `water_resource` (string).

Plus `TerrainTileSets.Cell.UndergroundResource/Richness/WaterResource` names,
`DescribeUnderground`/`DescribeWaterResource` helpers, and accessors
`UndergroundResourceAt`, `UndergroundRichnessAt`, `WaterResourceAt`. The
"no generator needed at runtime" promise covers the new strata. Contract-scan
guards extended to pin all of it.

### 4. Extraction gameplay (grid side)

- **`GridSubsurfaceStoreComponent` (new)** — the ONE owner of "how much is
  left underground", per the ownership-matrix discipline. Remaining amounts
  per cell, lazily seeded on first touch from
  `richness x definition.Amount` (read through `DataLayersPath`), drawn down
  by extractors, `ISaveable`, `DepositChanged`/`DepositDepleted` signals.
- **`GridExtractorComponent` (new)** — attach under an extractor building
  scene. When its building is complete (plays with `GridBuildSiteComponent`),
  it binds to the deposit under its footprint via the data layers, validates
  the id (and `ExtractorBuildId` when authored), then cycles: every
  `GatherSeconds` it draws `AmountPerGather` from the subsurface store into
  the wallet, until the deposit depletes (signal + optional modulate/stop).
  Ticking mirrors `GridProductionComponent`'s bounded-delta pattern.
- **Offshore placement** — `GridBuildDefinition.AllowedTerrainKinds` (empty =
  scene policy, as everywhere else): an offshore platform authorizes
  `shallow_water` for itself without opening water placement globally.
  `GridPlacementComponent` consults it during `CanPlaceOnCellData`.
- **Boats and fishing** — no new movement system needed: a second
  `GridNavigationComponent` authored inverse (blocked kinds = land, allowed =
  water) already gives water-only pathing; a boat is a worker whose
  `GridPathFollowerComponent` points at it. The scatter spawns
  `Liquid` walk-up nodes as today — now reachable. Ships as a
  `grid_worker_boat.tscn` template plus a fishing wiring example in the HUD
  example scene.
- **Scatter split** — `GridResourceScatterComponent` spawns nodes for surface
  and water strata only; underground deposits get NO walk-up nodes (they are
  invisible and building-extracted).

### 5. Discovery (optional mechanic, off by default)

`GridProspectingComponent` (new): a `survey` job kind reveals the underground
stratum per cell into a discovered set (`ISaveable`). `RevealAll = true` by
default — games that do not want prospecting see everything, nothing changes.
The overlay and inspector consult it when it is enabled.

### 6. Visualization

- `TerrainMapOverlayComponent`: `ShowUndergroundResources` (hatched field
  patches, per-id colour, gated by discovery) and `ShowWaterResources`
  toggles.
- `GridObjectInspectorComponent`: selecting a cell shows its strata (surface
  node, water resource, underground deposit + remaining amount).
- The painted/tile/iso views change NOTHING — the subsurface is invisible on
  the map by design; the overlay is the survey map.

### 7. Catalogue authoring

The three shipped sets get their strata: OilAndGas moves crude oil, gas and
condensate underground (fields under desert, scrub and shallow sea) with
derrick/platform extractor builds; Historical gains fish/whales in the liquid
stratum and moves iron/coal/gems underground under hills; SpaceExploration
becomes the full colony loop. Templates gain a derrick + fishing example.

### The space colony case

A space colony game is the model's best stress test, and it passes without new
machinery:

- **Water ice and volatiles** are underground fields like oil — the
  generator's temperature axis already exists, so ice biases toward cold
  latitudes exactly as oil biases toward desert. Rare metals go deep under
  mountains (relief rule). An ice extractor is a derrick wearing different
  art.
- **Regolith and meteoric debris** are surface walk-up gathers — today's
  system.
- **Refinement chains** (ice -> water -> oxygen + hydrogen) are the part such
  games lean on hardest, and they already ship: the extractor pulls raw from
  a finite deposit into the wallet, and `GridProductionComponent` recipes
  refine wallet-to-wallet down the chain. The store/extractor split lands
  exactly on this seam.
- **Exotic liquids** (methane seas) are the Liquid stratum under a re-skinned
  water palette; Europa-style oceans under ice are, correctly, an underground
  stratum.
- **Noted for later, not in scope**: per-cell *site qualities* (solar
  exposure, wind, geothermal gradient) that drive generator placement — those
  are scalar fields, not deposits, and belong as a future data layer beside
  relief, not as a stratum.

## Implementation phases

- **Phase A — model & generation. DONE (2026-09-02).** ResourceDefinition
  gained `Stratum`/`Depth`/`DepositScale`/`Extraction`/`ExtractorBuildId`;
  TerrainWorld carries the three new cell arrays; `TerrainSubsurfaceStage`
  lays contiguous richness fields per underground definition;
  `TerrainResourceStage` is stratum-aware with the same hashes (a seed lays
  the surface out unchanged); field + generator accessors; diagnostics count
  the strata; the shipped catalogues are stratified (OilAndGas underground
  with a depth ladder, Historical fish/whale liquid + metals underground,
  Space colony ices/metals underground + deuterium liquid).
  `tests/grid_terrain_subsurface_probe.gd` asserts existence, ground rules,
  richness, depth bands, field contiguity, stratum placement and determinism.
- **Phase B — publication. DONE (2026-09-02).** `LiquidData` and
  `UndergroundData` layers (id + banded richness + depth as custom tile
  data), `TerrainTileSets.Cell` names + `DescribeLiquid`/`DescribeUnderground`,
  accessors `LiquidResourceAt`/`UndergroundResourceAt`/`UndergroundRichnessAt`/
  `UndergroundDepthAt`, contract-scan guards, and probe checks that the
  published layers answer identically to the generator. Lab status-line
  counts deferred to Phase D.
- **Phase C — extraction. CORE DONE (2026-09-02); boat template pending.**
  `GridSubsurfaceStoreComponent` owns the drawdown (lazy-seeded from
  richness x catalogue Amount, `ISaveable`, deposit signals);
  `GridExtractorComponent` binds to the deposit under its building's
  footprint, validates depth reach (`ReachDepth`) and `ExtractorBuildId`,
  waits for build completion, and pumps `AmountPerGather` per
  `GatherSeconds` into the wallet until depletion.
  `GridBuildDefinition.AllowedTerrainKinds` lets an offshore platform
  authorize shallow water for itself (exclusive allow beats the scene's
  blocked list). The scatter spawns LIQUID nodes on their water cells
  (skipping the land-terrain veto exactly there) so fish are gatherable the
  moment a water-authored navigation exists; underground deposits get no
  walk-up nodes. Probe covers store seeding, full pump-to-depletion and
  stop; the placement smoke covers offshore-allow/land-refuse.
  **Still to do in C:** `grid_worker_boat.tscn` + water-navigation wiring in
  the kit HUD example, so fishing ships working out of the box.
- **Phase D — discovery & polish.** Prospecting, overlay/minimap/inspector,
  saves round-trip, catalogue authoring for the three sets, docs
  (2D_ISO_TOOLKIT matrix + terrain guide + this doc's status updates).

Every phase lands with the standing gate: `dotnet build` clean, contract scan,
15 terrain guards, probes, runtime smoke against Godot 4.7.1 mono.

## Decisions to approve (recommendations first)

1. **Extractor as a dedicated component** (recommended) rather than forcing
   deposits through `GridProductionComponent` recipes — a deposit is finite
   world state, not a wallet-to-wallet recipe; the store/extractor split keeps
   one owner for the remaining amount.
2. **Water resources move out of the surface array** (recommended) — one
   meaning per array; the visible effect is only that sea dots become
   water-layer dots queried by `WaterResourceAt`.
3. **Prospecting ships off by default** (recommended) — the mechanic is a
   dial, not a tax on games that do not want it.
