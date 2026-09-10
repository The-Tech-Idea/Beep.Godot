# DUP-13 — A terrain-kind registry: one owner for what "grass", "rock", "lava" mean

**Type:** duplication fix + feature (data-driven kinds) · **Area:** `TerrainTileSets`, `TerrainLayers`, `TerrainBiomeStage`, `TerrainCoherenceStage`, `TerrainScaleConstraintStage`, `TerrainStartPositionStage`, `TerrainFeatureStage`, `TerrainPaintedRendererComponent` (id map), `TerrainIsometricRendererComponent` (frame table), `TerrainTileRendererComponent`, `SeededTerrainPropScatterComponent.PaletteKeyFor`, `GridTerrainRules`, `ResourceCatalogs` · **Status:** **IN PROGRESS 2026-09-10** (registry + steps 1–4: start-position eligibility, coherence absorb sets, peak/lake-bed sets, per-kind level) · **Effort:** L (3–5 days) · **Risk:** medium–high (touches generation and every view; needs the determinism probes)

## Progress (2026-09-10)

`TerrainKind` + `TerrainKindCatalog` (with a code-built `Standard`) are landed, and the determinism baseline (`terrain_generation_baseline_probe`, every layer hashed for seeds {31415,4242,777} at Small+Huge) was confirmed green first, then kept byte-identical through **steps 1–4**:

- **Step 1** — `TerrainStartPositionStage.Eligible` reads `Standard.Startable`. Mutation-proven against the baseline (flip a kind's `Startable` → start positions move → baseline fails).
- **Step 2** — `TerrainCoherenceStage` reads `Standard.Absorbable`/`AbsorbTarget`; its two HashSets deleted.
- **Step 3** — `TerrainCoherenceStage.PeakMaterials` and `TerrainScaleConstraintStage`'s `PeakKinds`/`NotLakeBedKinds` read `Standard.PeakMaterialKinds`/`NotLakeBedKinds`; three HashSets deleted. Mutation note: `rock.PeakMaterial=false` does NOT trip these seeds (no rock-peak cases in them) — mutate a **ubiquitous** kind (`grass.PeakMaterial=true`) for a reliable baseline trip.
- **Step 4** — `TerrainLayers.LevelForKind` reads `Standard.Level` (keeping only the bare-`"water"` legacy alias). **`Level` is NOT baseline-guarded**: its generation use (TerrainGeneratorComponent:369) feeds rendering z-order, not the hashed snapshot, so flipping a `Level` in `Standard` leaves the baseline green. It has its own **direct** guard, `terrain_kind_catalog_probe` (`TerrainKindCatalogSmoke`), asserting `LevelForKind` per canonical kind + water alias + unknown default; mutation-proven (gravel Hills→Ground → `LevelForKind('gravel') = 1, expected 2`). This probe is the home for every later migration that feeds a view/grid rather than generation.

Consumers read `Standard` statically for now (matching the static tables); the game-assignable catalog carried through generation settings is a **later step**, and the "no kind literals outside the catalog" pin only lands once every table is folded in.

The remaining tables fold in one step at a time, each verified against the baseline (generation) or the grid/render probes (views). The byte-exact data for all of them was extracted up front — this is the reference to populate `Standard` from:

- **Canonical order (= tile index, TerrainTileSets.Kinds):** deep_water, shallow_water, grass, dry_grass, desert, sand, tundra, snow, ice, jungle, swamp, mud, gravel, rock, lava (15). "water"/"sea"/"ocean"/"grassland"/"plains"/"beach"/"dirt"/"soil"/"stone" are non-canonical **aliases** several tables also handle — each migration must preserve its alias branch (generation never emits them, so the baseline will not catch an alias slip; the round-trip/grid tests must).
- **Class** (done — GroundOf/IsWaterKind/IsLandKind): Water = deep_water, shallow_water (+alias water); Steep = rock, lava; Land = all others. Folded onto `TerrainKind.Class` (reusing the `TerrainTileSets.Ground` enum). `IsWaterKind` feeds generation (baseline-guarded); `GroundOf`'s Steep branch is metadata-only (guarded by `terrain_kind_catalog_probe`). `IsLandKind` composes `IsWaterKind`, so it migrated for free. The bare-`"water"` alias and empty-string cases stay in `TerrainTileSets`.
- **Level** (done — TerrainLayers.LevelForKind; Sea0/Ground1/Hills2/Mountains3): Sea = deep_water, shallow_water (+water); Hills = gravel; Mountains = rock; Ground(default) = everything else incl. **lava**.
- **Coherence flags** (done — TerrainCoherenceStage): `Absorbable`, `AbsorbTarget`, `PeakMaterial` folded in (steps 2–3). Rainfall = {desert,dry_grass,grass,swamp,jungle} is the one set **still hardcoded** in the stage — its ordered voting index is entangled with the vote array, deferred to its own step.
- **Scale flags** (done — TerrainScaleConstraintStage): PeakKinds (= `PeakMaterialKinds`) and NotLakeBed (= `NotLakeBedKinds`) fold in via the catalog; `NeighbourLand`/`DominantLand` still take `HashSet<string>?` and are passed the cached sets.
- **Startable** (done): all except snow, ice, rock, lava.
- **FeatureEligibility** (TerrainFeatureStage `Choose`): jungle→jungle; swamp→marsh; desert→oasis; grass/dry_grass/tundra→woods (tundra keeps its -0.05 stand bias in the stage); all else→none. Forest-vs-Woods thresholds stay in the stage.
- **PropPalette** (done — PaletteKeyFor): folded onto `TerrainKind.PropPalette` (grass/dry_grass/jungle→grass; sand/desert→desert; mud/swamp→mud; rock/gravel/snow/ice/tundra→rock; shallow_water→water; deep_water/lava→""). The scatter keeps its own alias normalization (grassland/plains→grass, beach→sand, dirt/soil→mud, stone→rock) and the water opt-in, now gated on the resolved "water" key not the id. `PaletteKeyFor` is `internal`, driven by `terrain_kind_catalog_probe` (props aren't in the snapshot); mutation-proven on both the catalog data and the opt-in.
- **MaterialSlot** (done — painted TerrainIds): folded onto `TerrainKind.MaterialSlot` (grass0,dry_grass1,desert2,sand3,tundra4,snow5,ice6,jungle7,swamp8,mud8,gravel9,rock10,shallow_water11,deep_water12,lava13). `TerrainPaintedRendererComponent.TryMaterialSlot` maps the water/sea/ocean aliases to deep water's slot and returns false for unknown, so its two call sites keep their different fallbacks (grass's slot for a cell, the cell's own slot for shore inland). Render output, not baseline-guarded — the catalog probe covers all kinds, aliases and the unknown case; mutation-proven (grass 0→5).
- **IsoFrame** (TerrainIsometricRenderer) — **owner's call, NOT folded.** Triage: these are 11 per-scene `[Export] int XFrame` primary frames (grass0…rock10) PLUS a `TerrainVariants` string[] override; mud and lava have no export and are assembled in code (mud=swamp's frame, lava=rock's), deep/shallow_water have no block frame (surface shader). Unlike MaterialSlot (a private static table with no override), this is an authoring surface: a game rearranges its own atlas through those exports. Folding it means either (a) catalog default + export override with no "unset" sentinel — an int export always has a value, so the renderer cannot tell "use my default" from "use the catalog", which is a second owner, not a resolution; or (b) retiring the 11 exports so the catalog is the sole source — an authoring/removal change (shipped iso scenes may set them) that is **removal, i.e. Fahad's call, not the implementer's**. It is not a duplicate of the catalog (the iso atlas layout is genuinely independent) and not a missing implementation (it works). Left in place pending that decision.
- **BlockedByDefault** (done — GridTerrainRules): canonical deep_water/shallow_water/lava carry `TerrainKind.BlockedByDefault`; `DefaultBlockedTerrainKinds` reads them in catalog order and prepends the water/sea/ocean aliases. Grid-side, not baseline-guarded — the catalog probe asserts the per-kind flag and the exact assembled list. `GridNavigationComponent`'s own (shallow-water-permissive) blocked list is a movement policy, left untouched.

Suggested remaining order (least entangled first); **done: Startable, coherence+scale material flags, Level, Class, BlockedByDefault, PropPalette, MaterialSlot**. **Owner's call, deferred: IsoFrame** (per-scene `[Export]` frames — see its row). Remaining: FeatureEligibility (jungle/marsh/oasis/woods; Forest-vs-Woods thresholds and tundra's -0.05 bias stay in the stage) → the Rainfall ordered voting index (the entangled one) → the biome-preset stage (a biome→kind map, not a per-kind property — needs a catalog method or stays) → delete the literals + add the no-literals pin → game-assignable catalog through settings/recipe. View-only migrations (Level, MaterialSlot, PropPalette) extend `terrain_kind_catalog_probe`; generation migrations extend the baseline.

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
