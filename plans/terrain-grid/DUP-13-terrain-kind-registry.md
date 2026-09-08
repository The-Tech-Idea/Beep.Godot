# DUP-13 — A terrain-kind registry: one owner for what "grass", "rock", "lava" mean

**Type:** duplication fix + feature (data-driven kinds) · **Area:** `TerrainTileSets`, `TerrainLayers`, `TerrainBiomeStage`, `TerrainCoherenceStage`, `TerrainScaleConstraintStage`, `TerrainStartPositionStage`, `TerrainFeatureStage`, `TerrainPaintedRendererComponent` (id map), `TerrainIsometricRendererComponent` (frame table), `TerrainTileRendererComponent`, `SeededTerrainPropScatterComponent.PaletteKeyFor`, `GridTerrainRules`, `ResourceCatalogs` · **Status:** proposed 2026-09-08 · **Effort:** L (3–5 days) · **Risk:** medium–high (touches generation and every view; needs the determinism probes)

## Finding

A terrain kind is a string literal, and what it *means* is spelled out in at least eleven independent tables:

| Table | Where | Decides |
|---|---|---|
| `TerrainTileSets.Kinds` (ordered, 15 entries) | `TerrainTileSets.cs:249-258` | tile index per kind |
| `IsWaterKind` / `IsLandKind` / `GroundOf` | `TerrainTileSets.cs:213-242` | water, steep (`rock`, `lava`) |
| `TerrainLayers.LevelForKind` | `TerrainLayers.cs:420-426` | `gravel`→Hills, `rock`→Mountains |
| `TerrainBiomeStage.ThemedKind`/`PlainGround`/`EarlyKind` | 517-545 | preset ground kinds |
| `TerrainCoherenceStage.Rainfall`/`Absorbable`/`PeakMaterials`/`AbsorbTargets` | 35-76 | which kinds smooth/absorb |
| `TerrainScaleConstraintStage.PeakKinds`/`NotLakeBedKinds` | 247, 441 | peak vs bed |
| `TerrainStartPositionStage.Eligible` | 89-91 | `snow/ice/rock/lava` unstartable |
| `TerrainFeatureStage` eligibility | 130, 318-328 | which kinds carry woods/jungle/marsh/oasis |
| painted renderer shader id map | `TerrainPaintedRendererComponent` | kind → splat id / material slot |
| isometric frame table | `TerrainIsometricRendererComponent` | kind → block frame |
| `SeededTerrainPropScatterComponent.PaletteKeyFor` | 312-320 | kind → prop palette |
| `GridTerrainRules.DefaultBlockedTerrainKinds` | grid | kind → blocked by default |
| `ResourceCatalogs` terrain lists | 119-220 | which kinds host which resources |
| `TerrainMapArt` slots / `TerrainMaterialTiling` | 465, 496-504 | kind → texture slot |

Phase 1.10 of the master tracker is the proof of cost: adding `"lava"` required touching `Kinds`, the painted id map, the isometric frame table, `GroundOf` and `Describe` — and it was found because a lava field painted as grass. Any new kind (`"tundra_wet"`, `"volcanic_ash"`, a game's `"asphalt"`) repeats that hunt, and a game cannot add a kind without editing the addon.

## Design

`TerrainKind` resource + `TerrainKindCatalog` resource, authored once and shipped as `textures/terrain/terrain_kinds.tres`:

```csharp
[GlobalClass] public partial class TerrainKind : Resource
{
    [Export] public string Id;                      // "grass"
    [Export] public TerrainKindClass Class;         // Land, Water, Steep
    [Export] public int Level;                      // TerrainLayers level when relief is unknown
    [Export] public bool RainfallBiome;             // smoothed by coherence
    [Export] public bool PeakMaterial;              // levelled with relief
    [Export] public bool ShoreMaterial;             // "sand": never a lake bed / absorb target
    [Export] public bool Startable;                 // start-position eligible
    [Export] public string FeatureEligibility;      // "woods" | "jungle" | "marsh" | "oasis" | ""
    [Export] public string PropPalette;             // "grass" | "desert" | "mud" | "rock" | "water" | ""
    [Export] public string MaterialSlot;            // splat/shader texture slot
    [Export] public int IsoFrame;                   // block frame index
    [Export] public bool BlockedByDefault;          // grid placement/spawn default
    [Export] public float NavigationCost = 1f;
}
```

`TerrainKindCatalog.Standard` loads the shipped resource; `TerrainGenerationSettings` carries the catalog (like `ResourceCatalog` today) so generation is still a pure function of its input. Each table above becomes a lookup: `kinds[kind].PeakMaterial`, `kinds.WithFeature("woods")`, `kinds.Ordered` for tile indices (order = catalog order, appended-only rule documented on the resource).

The **string ids stay** — cells, saves and shaders still speak `"grass"`. The registry decides properties; it does not rename anything. A game authors its own `TerrainKindCatalog` (or extends the standard one) and assigns it on `TerrainGeneratorComponent`, exactly as `ResourceCatalog` works.

## Steps

1. Add the two resources and the standard `.tres` with today's 15 kinds and today's exact properties (read from the tables above).
2. Replace the tables one owner at a time, running the determinism probes after each: `TerrainTileSets` → `TerrainLayers` → stages → renderers → scatter → `GridTerrainRules` defaults.
3. Delete the hardcoded sets; the compiler and the scan sweep the literals.

## Guards

- **Determinism first:** record `CellTerrain`/`CellRelief`/`Feature` arrays for seeds {31415, 4242, 777} at Small/Huge before step 2; after every replacement they must be byte-identical. Mutation: flip `Startable` on `rock` in the `.tres` → start positions change → probe fails (proves the registry is read).
- Pin: kind string literals (`"deep_water"`, `"gravel"`, `"lava"`, …) appear under `ecs/terrain/` only in `terrain_kinds.tres` and the kind-id constants file. Mutation: restore `PeakKinds = new() { "rock", "snow", "gravel" }` → fails.
- Round-trip: a custom catalog adding `"ash"` (Steep, MaterialSlot rock) generates, paints (no grass fallback), stacks (no holes) and blocks placement — the lava incident as a test.

## Dependencies / collisions

Touches `TerrainGeneratorComponent` settings (owned by terrain; the campaign session edits `TerrainWorldComponent`, one level up — coordinate on `TerrainRecipe` carrying the catalog path). Best scheduled after DUP-01/DUP-02 so the renderers have one place to read the registry.

## Out of scope

New kinds or new art; changing any current classification.
