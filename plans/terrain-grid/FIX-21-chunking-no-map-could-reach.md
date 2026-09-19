# FIX-21 — Prop chunking no map could reach

**Type:** fix (rendering performance) · **Area:** `TerrainFeatureRendererComponent`, `TerrainReliefRendererComponent`, `TerrainMapSetup` · **Status:** FIXED 2026-09-19 · **Effort:** S · **Risk:** medium — it changes when every map streams its props, so it changes what several probes see.

## Evidence

The owner, 2026-09-19, looking for the feature in the lab and not finding it: *"i dont see the huge
and large maps that use chunking that we implemented"*.

## What was wrong

Both prop renderers streamed only above **65,536 cells**:

```csharp
if (StreamLargeMaps && !Engine.IsEditorHint() && IsInsideTree() && (long)size.X * size.Y > 65536)
```

The largest map the game could generate was Huge, 128x80 — **10,240 cells**. Six times short. So
`TerrainPropResidency`, `FeatureCellsPerFrame`, `FeaturePreloadChunks` and `StreamLargeMaps` were
configurable, guarded by three probes, and **unreachable from anything the game could make**.

Measured on Huge (wet, young — the heaviest the lab offers), streaming forced on and off:

| renderer | rebuild | stamps | vs a 16.7 ms frame |
|---|---|---|---|
| features | 48.3 ms | 2,083 | blocks 2.9 frames |
| relief | 18.5 ms | 443 | blocks 1.1 frames |

Turning `StreamLargeMaps` on changed nothing — same time, same stamps — which is the signature of a
switch that looks like it works. So the biggest map blocked the main thread for ~67 ms doing exactly
the work the streaming path exists to spread.

## The fix

A threshold per renderer, derived from measured cost rather than a round number:

- features **4.7 microseconds a cell** → one frame is about 3,500 cells → `StreamAboveCells = 3500`
- relief **1.8 microseconds a cell** → about 9,000 → `StreamAboveCells = 9000`

Two numbers, not one shared: relief is genuinely cheaper per cell, and a single constant would
either stream it earlier than it needs or let features block a frame.

On Huge that turns a 47 ms block into **0.1 ms**, with props filling over the following frames.

## And a map worth chunking: Massive, 256x256

Every existing size fits on screen at a sensible zoom, which is why nothing exercised residency by
being genuinely large. `TerrainMapSize.Massive` is 65,536 cells, six times Huge — and **appended**
to the enum, never inserted, because a world's recipe stores the value and inserting would
reinterpret every saved map above it.

It is not six times the generation cost. The field budget is in SAMPLES, and `EffectiveSamplesPerCell`
steps sub-cell detail down to fit, so Massive builds at 4 samples a cell against Huge's 11 and is the
smaller field — 1,048,576 samples against 1,239,040. Measured: 9.3 s to build, then props stream in
(1,260 → 7,076 trees over 60 frames, still filling). The honest cost is coarser sub-cell detail, so
coastlines are blockier than on Huge.

## What it broke, and why that was right

`terrain_feature_streaming_probe` and `terrain_relief_streaming_probe` both compare a STREAMED
renderer against a FULL one — and both obtained the "full" one by making it small enough to fall
under the threshold. Moving the threshold turned each control into a second streamed map, so the
comparison became a map against itself.

Both now state `StreamLargeMaps = false` on the reference instead of inferring it from size. With
that, both pass and establish something worth having: **streamed placement is identical to full
placement**. The chunking was always correct. It simply never ran.

## Guard limits, stated

The three streaming probes drive a 1024x1024 renderer directly, so they have always exercised the
streaming PATH. What none of them covered is whether the path ever ENGAGES for a real map — that is
what was broken, and no probe could have caught it, because none asks what any map size costs.
