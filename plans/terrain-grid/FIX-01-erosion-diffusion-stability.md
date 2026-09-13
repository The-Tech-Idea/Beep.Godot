# FIX-01 — Erosion diffusion stability: cap the hillslope coefficient at its own documented limit

**Type:** fix · **Area:** `TerrainErosionStage` (`TerrainGeneratorComponent.ErosionStrength` doc) · **Status:** **IMPLEMENTED 2026-09-11** · **Effort:** XS (½ day) · **Risk:** low

## Outcome (2026-09-11)

`TerrainErosionStage.Diffuse` now computes its coefficient once as `Mathf.Min(Diffusion * strength, 1.0f)` and updates with it, so `ErosionStrength` above ~2.86 stops driving the weighted-average pass past the `D <= 1` limit its own doc states. The `ErosionStrength` export doc on `TerrainGeneratorComponent` gained a paragraph saying the hillslope smoothing saturates at its maximum stable rate while incision keeps growing. New `tests/TerrainErosionDiffusionStabilitySmoke.cs` plus `tests/terrain_erosion_diffusion_stability_probe.gd` (registered in `run_actor_checks.ps1`, with an explicit `Beep.Godot.csproj` entry) build an all-land, near-flat buffer carrying a ±0.01 checkerboard and run the stage at `ErosionStrength = 4`: the maximum deviation from 0.5 must stay at or below 0.05. Mutation-proven — with the cap removed the deviation reaches 0.3820 and the probe fails. `terrain_generation_baseline_probe` stays green, confirming the cap is a no-op at the default `ErosionStrength = 1.0` and the recorded fixtures do not move.

The optional contract-scan pin was not added: the scan currently throws earlier in the file (see FIX-03's Outcome) and the behavioural guard is mutation-proven.

**Correction — a second copy of the formula was missed when this first landed.** `TerrainWaterSurfaceSmoke.ReferenceErosion` is the parity oracle that reimplements the erosion maths independently and compares it against the stage bit-for-bit. It still applied `0.35f * dial` unclamped, so on its `ErosionStrength = 4f` case the oracle and the fixed stage diverged and the probe failed with `Optimized erosion changed a height bit`. The oracle now caps at 1 as well, with a comment stating it encodes the intended formula — a parity oracle has to follow an intentional change to the thing it oracles. This was not caught at the time because only the new FIX-01 smoke and the generation baseline were run, not `terrain_water_surface_probe`; all three are green now. Anyone touching `TerrainErosionStage` should run that probe too.

## Finding

The hillslope-diffusion half of the erosion stage runs its weighted-average update with a coefficient that the top of the exposed `ErosionStrength` dial pushes past the stability limit the code itself asserts, turning the smoothing pass into an amplifier of checkerboard speckle.

The data flow:

1. **The diffusion coefficient is `0.35 * dial`, up to `1.4`.** `Diffuse` is invoked with the dial, not the incision scalar: `Diffuse(world, settled, dial, ...)` at `TerrainErosionStage.cs:178`, where `dial = Mathf.Clamp(settings.ErosionStrength, 0.0f, 4.0f)` (`:144`). The incision path uses a *separate* scalar `strength = Strength * dial` (`:145`) — the `Diffuse` call deliberately bypasses it, per the comment at `:140-143` ("the two processes are not the same size"). Inside `Diffuse` the per-cell update is `elevation + (Diffusion * strength * ((total / counted) - elevation))` (`:220-223`) with `Diffusion = 0.35` (`:83`) and its `strength` parameter bound to `dial`. Effective coefficient `D = 0.35 * dial`, reaching `1.4` at `dial = 4`.

2. **`D > 1` violates the stage's own stability claim.** The `Diffusion` doc (`:78-82`) states the weighted-average form "is stable for any D up to 1 and simply relaxes faster as D rises." The update is `new = (1 - D)·old + D·mean`. For an interior land cell carrying the checkerboard eigenmode (centre `+a`, four land neighbours `-a`, so `mean = -a`), one pass yields `new = (1 - 2D)·a`. The per-pass factor is `|1 - 2D|`: below one for `0 < D < 1`, exactly `-1` (neutral) at `D = 1`, and **greater than one once `D > 1`**, i.e. once `dial > 1/0.7 ≈ 2.857`. Above that dial the smoothing pass amplifies the highest-frequency mode instead of relaxing it.

3. **This runs 12 times and then feeds relief classification.** The `pass` loop applies incision-then-`Diffuse` `Passes = 12` times (`:99`, `:148`). The per-pass copy-back clamps land to `[0, 1]` (`:229`), so the field does not diverge numerically — but a `|1-2D| > 1` mode saturates neighbouring samples to alternating `0/1`. Because the stage runs **before** relief is classified (its own doc, `:48-50`: "Hills and mountains are cut as percentiles of the height field") and `TerrainGeneratorComponent.ErosionStrength` is an `[Export(Range "0,4,0.05")]` (`:55`, default `1.0`) that flows only `Mathf.Clamp(ErosionStrength, 0, 4)` into settings (`:616`, never otherwise overridden), an inspector value in the top ~29% of the exposed range computes hill/mountain percentile bands from an artifact-laden surface rather than the intended smoothed one.

The dial's *lower* range is fine: at the default `1.0`, `D = 0.35`; the problem only exists for `dial > 2.857`, which is reachable and unguarded.

## Design

Clamp the effective diffusion coefficient to the limit the comment already asserts. In `Diffuse`, bound the coefficient the update uses so `Diffusion * strength ≤ 1`:

```
float coefficient = Mathf.Min(Diffusion * strength, 1.0f);
...
settled[index] = counted == 0
    ? elevation[index]
    : elevation[index] + (coefficient * ((total / counted) - elevation[index]));
```

This is the minimal correct form: it leaves incision entirely alone (that path is unchanged and its coefficient `Strength * dial = 0.48` at `dial = 4` is nowhere near the diffusion bound), preserves current behaviour for every `dial ≤ 2.857` (where `0.35 * dial ≤ 1` already), and only bites where the coefficient would otherwise cross one. At `dial = 4` the diffusion pass now relaxes at the fastest stable rate (`D = 1`, neutral checkerboard) instead of amplifying.

- **One owner.** The coefficient is computed in exactly one place, the update site in `Diffuse`. No parallel clamp elsewhere; the `dial`-vs-`strength` split at `:144-145` stays as the deliberate design the comment documents.
- **No new dial, no compat shim.** `ErosionStrength` keeps its `0..4` range and meaning; the top of the range simply stops going unstable. Update the `ErosionStrength` doc (`TerrainGeneratorComponent.cs:51-54`) to note that beyond the mid-range the hillslope smoothing saturates at its maximum stable rate rather than cutting harder, so the export's contract matches its behaviour.
- The `Diffusion` doc block already states the `D ≤ 1` limit; no wording change is needed there beyond it now being enforced rather than assumed.

## Guards (fail first)

**Primary — a headless C# smoke on the stage directly** (tests share the `Beep.ECS` assembly and already reach internal stage types and `TerrainGenerationBuffer.Elevation`/`.Land`, e.g. `TerrainScratchLifetimeSmoke.cs:12`). New `TerrainErosionDiffusionStabilitySmoke`:

- Build a small all-land `TerrainGenerationBuffer` with a nearly-flat elevation field carrying a tiny high-frequency perturbation: `elevation[index] = 0.5f + (checkerboard(x, y) ? +epsilon : -epsilon)` with `epsilon = 0.01f`. Flat base means incision slopes are ~`2·epsilon`, so `lowering ≤ Strength·dial·MaxDrainageFactor·2·epsilon ≈ 0.029` — diffusion, not incision, dominates the outcome.
- Call `TerrainErosionStage.Apply(world, settings)` with `settings.ErosionStrength = 4.0f`.
- **Assertion:** the maximum deviation of any land sample from `0.5` stays bounded — `max|elevation[i] - 0.5| ≤ 0.05f`. After the fix (`D = 1`, per-pass factor `|1-2D| = 1`, neutral) the checkerboard perturbation never grows, so deviation stays near `epsilon` plus the small incision term, well under the bound.
- **Mutation that trips it:** revert the cap (restore `Diffusion * strength` = `1.4` at `dial = 4`). The `|1-2D| = 1.8` per-pass factor amplifies the perturbation across 12 passes until the `[0,1]` clamp saturates it, driving `max|elevation[i] - 0.5|` to ≈`0.5` — the assertion fails. (Confirm it can fail before trusting the pass: run the mutated stage once and see the deviation spike.)

**Secondary — contract-scan pin** in `tests/addon_contract_scan.ps1`: assert `TerrainErosionStage.cs` contains a `Mathf.Min(` bounding the diffusion coefficient on the `Diffuse` update line, so the cap cannot be silently dropped in a future edit. Mutation: delete the `Min` → scan fails.

**Determinism note (must NOT fail):** `tests/terrain_generation_baseline_probe.gd` records fixtures at default settings, where `ErosionStrength = 1.0` gives `D = 0.35 < 1` — the cap is a no-op there, so the fix moves no baseline hash. If the probe is ever extended to cover a high-`ErosionStrength` case, that fixture must be (re-)recorded *after* the fix, since the corrected output is the intended one.

## Dependencies / collisions

- **ENH-17** (coherence rainfall index) also touches terrain generation-stage CPU but is a disjoint stage (`TerrainCoherenceStage`); no overlap in files or fields.
- **FIX-02** (dead noise channels) is in `TerrainNoiseSet`/`TerrainLandmassStage`, upstream of erosion; independent.
- No collision with the concurrent `TerrainWorldComponent`/streaming/archive session — this change is confined to the offline generation stage `TerrainErosionStage` and one export doc on `TerrainGeneratorComponent`, neither of which the streaming/archive group touches.
- No relation to the resource/grid plans (DUP-05, DUP-13, DUP-14, ENH-01, ENH-02, ENH-12).

## Out of scope

- Retuning the incision constants (`Strength`, `DrainageExponent`, `MaxDrainageFactor`, `Passes`) or the diffusion base `0.35` — the values below the instability are the authored design and are not being changed.
- Changing the `ErosionStrength` export range, default, or its clamp in `TerrainGeneratorComponent`.
- The `dial`-vs-`strength` two-scalar split (`:140-145`) — it is deliberate and correct; only the unbounded product in the diffusion update is at fault.
- Any change to relief percentile classification itself; this fix only ensures it reads a stable surface at high dial.
