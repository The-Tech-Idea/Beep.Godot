# FIX-02 — Dead noise channels: Shape/ShapeWarpX/ShapeWarpY/Detail are built every generation but read by nothing

**Type:** fix (owner-call removal) · **Area:** `TerrainNoiseSet` (with a doc correction against `TerrainLandmassStage`) · **Status:** **PROPOSED 2026-09-11** · **Effort:** XS (~½ day) · **Risk:** low

## Finding

`TerrainNoiseSet` constructs ten `FastNoiseLite` channels on every non-plain generation run and disposes all ten at the end. Four of them — `Shape`, its two domain-warp channels `ShapeWarpX`/`ShapeWarpY`, and `Detail` — are allocated, held, and disposed without a single stage ever reading them. They are orphaned residue of the pre-rewrite thresholded-continental-noise approach to placing land.

1. **The four channels are never read.** The noise set's consumers are the four stages that take a `TerrainNoiseSet` parameter, and each reads only the live channels:
   - `TerrainWaterStage` reads `noise.Lake` (`TerrainWaterStage.cs:63`) and `noise.Ridge` (`:66`).
   - `TerrainElevationStage` reads `noise.Ridge` (`TerrainElevationStage.cs:49`) and `noise.Roughness` (`:50`).
   - `TerrainClimateStage` reads `noise.Temperature` (`TerrainClimateStage.cs:40`) and `noise.Moisture` (`:50`).
   - `TerrainFeatureStage` reads `noise.Vegetation` (`TerrainFeatureStage.cs:154`).

   That is the complete set of readers: `Lake`, `Ridge`, `Roughness`, `Temperature`, `Moisture`, `Vegetation` — six channels. `noise.Shape`, `noise.ShapeWarpX`, `noise.ShapeWarpY` and `noise.Detail` appear nowhere in the addon (the only `.Shape`/`.Detail` matches across the codebase are unrelated — `CollisionShape2D.Shape`, `KitChrome.Shape`, `CoastPreparation.Detail`).

2. **They are built and disposed regardless.** `TerrainNoiseSet.Create` constructs `Shape` at `shapeFrequency` (`TerrainNoiseSet.cs:82`), `ShapeWarpX`/`ShapeWarpY` at `shapeFrequency * 1.7f` (`:83-84`), and `Detail` at `settings.Frequency * 3.2f` (`:90`); `Dispose` frees all four (`:13-15`, `:21`). Each carries its own explicit seed offset (`91127`, `91159`, `91193`, `71069`), so removing them shifts no other channel's pattern — there is no shared RNG stream to disturb.

3. **The `Shape` doc is actively false, not merely dead.** `TerrainNoiseSet.cs:49` documents `Shape` as *"Continental fractal that decides where land is."* Land is no longer decided that way: `TerrainLandmassStage.Apply(world, settings)` takes **no** noise set at all (`TerrainFieldBuilder.cs:77`), grows land from separated seeds, and builds its **own** `edge` (`TerrainLandmassStage.cs:121`) and `coast` (`:379`) `FastNoiseLite`. The landmass stage's own class doc says so outright — *"It no longer decides WHERE land is - only what its edges look like."* (`TerrainLandmassStage.cs:42-43`). A reader trusting the `Shape` doc, and the presence of `ShapeWarpX`/`ShapeWarpY`, is wrong about how the map is built: they imply domain-warped continental shaping is applied when nothing of the kind runs.

4. **`shapeFrequency` is the only survivor of this cluster and stays.** `TerrainNoiseSet.cs:79` computes `shapeFrequency = 1.0f / TerrainLandmassStage.FeatureTiles(settings)` and feeds it into the six live channels' frequencies (Ridge `×3.0`, Roughness `×3.1`, Moisture `×1.25`, Temperature `×0.85`, Lake `×2.4`, Vegetation `×2.2`). `FeatureTiles` documents itself as *"Read by the noise set to choose its frequency"* (`TerrainLandmassStage.cs:53`). So the `FeatureTiles → shapeFrequency` input is a real, load-bearing dependency and is untouched by this change; only the four channels that no longer consume a decision are dead. (Note `Detail` does not even use `shapeFrequency` — it is scaled off `settings.Frequency` — so its removal is independent of the frequency computation entirely.)

## Design

This is a removal of dead code plus a doc correction. The two parts are separable and only the first is the owner's call.

**Doc correction (do regardless of the removal decision).** The `Shape` doc claim at `TerrainNoiseSet.cs:49` is false today and misleads every reader of the generation pipeline. Whether or not the channels are removed, that comment must be corrected to match `TerrainLandmassStage` — land is grown from seeds; the noise set no longer places it. This is a rule-7/rule-8 doc-drift fix independent of the channel lifetime.

**Channel removal (owner's call — Fahad's, not the implementer's).** Per the standing rule, "nothing reads it" opens the question, it does not settle it. Both triage questions are answered here:

- *Is it a duplicate?* No. The active land-placement mechanism (grown seeds + the landmass stage's own `edge`/`coast` noise) is a **different capability** from a thresholded continental `Shape` field — it is the replacement for it, not a second copy of it.
- *Is it part of a real workflow the product must have?* No longer. The landmass stage was **deliberately rewritten away** from thresholded continental noise; its class doc (`TerrainLandmassStage.cs:10-31`) records exactly why (single-blob falloff, percolation into spidery masses). Re-wiring `Shape`/`ShapeWarp`/`Detail` would mean reverting that decision, which is not wanted.

So the honest options are two, and the choice between them is Fahad's:

- **(A) Remove** the four channels: drop the `Shape`/`ShapeWarpX`/`ShapeWarpY`/`Detail` properties, their four constructor parameters, their four `Create(...)` calls (`:82-84`, `:90`), and their four `Dispose()` calls (`:13-15`, `:21`). Let the compiler sweep the private constructor's arity — no legacy shim, no `[Obsolete]`. `shapeFrequency` and the six live channels stay exactly as they are; generated output must be byte-identical (Guard 2). This reclaims four native `FastNoiseLite` allocations per build and removes the misleading surface.
- **(B) Keep** the channels and only correct the `Shape` doc — accepting the per-build native allocation as the cost of leaving the option in place.

Deletion is not presented as decided. The implementer performs the doc correction unconditionally and applies (A) only on Fahad's say-so; absent that, (B) is the fallback and no channel is deleted.

## Guards (fail first)

- **Guard 1 — doc-lie pin (fail-first, `tests/addon_contract_scan.ps1`).** Assert the literal claim that the shape fractal *"decides where land is"* appears nowhere under `addons/`, and — if option (A) is taken — that the property declarations `ShapeWarpX`/`ShapeWarpY` do not appear in `TerrainNoiseSet.cs`. This **fails against the current tree** (the false doc string is present at `TerrainNoiseSet.cs:49`) and passes once the doc is corrected/removed. Mutation to re-trip: restore the `Shape` doc line → the pin fails.
- **Guard 2 — determinism safety (`tests/terrain_generation_baseline_probe.gd`).** Generate a fixed-seed map before and after option (A) and assert the reduced-field hash is **identical** — proving the four channels fed no decision and their removal (with their seed offsets) shifts nothing. This is the repo's standard verification for a determinism-sensitive terrain change. To prove the probe can actually fail on this surface (not a green that cannot go red): in a throwaway mutation also drop `shapeFrequency`'s use on one surviving channel (e.g. change Ridge's `× 3.0f`) or remove a live channel's seed offset → the baseline hash shifts and the probe fails. Restore, and the removal-only diff leaves the hash untouched.

Guard 1 is the fail-first guard that drives the change; Guard 2 is the safety proof that the removal is inert, with a demonstrated failure mode so it is not a guard that cannot fail.

## Dependencies / collisions

- **No plan collision.** `TerrainNoiseSet` is generation-only and is not shared with any other open item. DUP-13 (`TerrainKindCatalog`) touches `TerrainFeatureStage`, one of the *readers* of the noise set, but only its `FeatureEligibility` lookup, not the `Vegetation` channel or any dead channel — no overlap.
- **No concurrent-session collision.** This does not touch `TerrainWorldComponent`, streaming, or the archive owned by the separate concurrent session — the noise set lives entirely inside the offline `TerrainFieldBuilder` generation pipeline.
- **Shares the determinism discipline** with the terrain-generation items (e.g. FIX-01): any change under the generation stages is verified through `tests/terrain_generation_baseline_probe.gd`, which is why Guard 2 is mandatory even for a removal expected to be output-neutral.

## Out of scope

- The six live channels (`Lake`, `Ridge`, `Roughness`, `Temperature`, `Moisture`, `Vegetation`) and their frequency multipliers — untouched.
- `shapeFrequency` and `TerrainLandmassStage.FeatureTiles` — a real surviving input, not part of the dead cluster.
- Reverting the landmass-stage rewrite or reintroducing thresholded continental / domain-warped land placement — explicitly not proposed; that is the design decision the dead channels are a leftover of.
- Any change to generated map output: option (A) must be byte-identical; if a diff appears, the removal is wrong and stops.
- The removal decision itself, which is Fahad's; the implementer performs only the doc correction unconditionally.
