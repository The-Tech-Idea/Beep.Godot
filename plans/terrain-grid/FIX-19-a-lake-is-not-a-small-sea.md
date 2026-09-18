# FIX-19 — Narrow inland water is not a small sea

*(Filed as "a lake is not a small sea". The bodies turned out to be mostly RIVERS — see the
correction below — and the fix is the same one either way: water without a fetch is not a small sea.
The filename is kept so links to it still work.)*

**Type:** fix (rendering) · **Area:** `shaders/water_common.gdshaderinc`, `shaders/terrain_splat.gdshader` · **Status:** FIXED and guarded 2026-09-18 · **Effort:** M · **Risk:** medium — it changes how water is drawn on every map, so it went through the owner's before/after gate.

## Evidence

The owner, 2026-09-18, with a screenshot of what he called a lake, drawn as a dark green stain
holding a wisp of teal: *"still lakes looks bad in some renderers like original and other"*, then the
diagnosis that turned out to be right: *"i suggest we make lake render's seperate from sea"*. Then
three clues that located it: *"i think pixelart lake looks ok"*, *"not all lakes are bad"*, *"only
the ones that do the same as origin"*.

Pixel Art was the control all along. It steps its waterline at zero and takes flat art colours, so
it never asked the distance field how deep the water was. Every view that asked got the wrong answer.

## What was wrong, measured

*(Read "lake" below as "narrow inland water". The bodies measured are rivers and one half-lake; the
numbers and the fault are unchanged by that, and the correction is recorded further down.)*

Decoded exactly as `water_common.gdshaderinc` does, `sd = (coast.r - 0.5) * 2 * coast_range`, on the
painted demo, seed 31415, `coast_range` 5, reading the field at its own resolution (24 texels per
tile) rather than one sample per cell:

| body | deepest sd | through the middle |
|---|---|---|
| the ocean, 3331 cells | 5.000 tiles | 3.854 |
| lake, 14 cells, widest row | **0.085** | slightly NEGATIVE, -0.02 to -0.23 |
| lake, 13 cells, widest row | **0.077** | slightly NEGATIVE |

Every term that shades water measures in tiles from the waterline: `shore` blends across
`shore_blend_tiles` (0.28, and 0.056 for a lake under FIX-14), `deep_tiles` is 4.5 and
`shallow_tiles` 1.8. Against a lake whose whole field is under a tenth of a tile:

1. **The waterline was a coin toss.** Every pixel of the lake fell inside the blend, so it came out
   half land — the pale mottled wash. Sampled in the render, a lake pixel was (95, 127, 108) against
   (25, 122, 163) where the shader's own contour debug said "this is water".
2. **The water was scored as ankle deep everywhere**, so it took the palest end of the depth ramp and
   kept a third of the sand bed showing through it. Measured over the 44,820 pixels the shader itself
   calls water: mean (99, 131, 109) — **greener than it is blue**, and all but indistinguishable from
   the grass at (112, 125, 43). None of those pixels, 0.0%, were blue-dominant.

## What was fixed

**A lake ends in a line** (`terrain_splat.gdshader`). `shore` steps at zero for water without a
fetch and keeps the sea's soft edge for water with one. The sea's edge is soft on purpose — the sand,
the wash and the surf carry that transition — and a lake has none of those to carry it.

**A lake is not ankle deep everywhere** (`water_common.gdshaderinc`). Water without a fetch gets a
floor on its colour (`LAKE_DEPTH`) and a ceiling on how much bed shows through it (`LAKE_BED`). Not a
separate ramp: the sea's own terms still apply wherever they say something stronger, so a large
inland body keeps its real gradient and a pond stops being a puddle. Both are fractions of the ramps
that already exist, so neither adds a dial.

`open_sea` is the flag the coast field already carries, and it does not ramp at a sea's waterline —
`OceanMask` grows the mask by a cell precisely so it does not. Proven rather than assumed: the same
coast rendered with the rule on and off matches to within **0.34/255** on open-sea rows and **0.00**
on land; the worst row, 4.5/255, is the animated surf line. The sandy shallows over the beach are
untouched.

Result on the same 44,820 pixels: mean (37, 93, 108), **100.0%** blue-dominant.

**Guard:** `tests/terrain_lake_colour_probe.gd` (GPU, in `run_terrain_integration.ps1` as
`lake_colour`). It frames the largest enclosed lake, takes the normal frame and the contour-debug
frame as an exact mask, and requires 95% of the pixels the shader calls water to render blue over
green. Confirmed to fail: with `LAKE_DEPTH` and `LAKE_BED` set to their no-op values the probe exits
1 with "only 0.0% of the lake reads as water".

## They were RIVERS. Corrected 2026-09-18, after a wrong diagnosis.

The bodies in every measurement above are **rivers**, not lakes, and a river is supposed to be a thin
winding ribbon. `WaterSourceAt` over the whole painted demo, seed 31415:

```
whole map: { "ocean": 3330, "": 2363, "river": 59, "lake": 8 }
body 0: 16 cells -> { "lake": 8, "river": 8 }      body 4:  8 cells -> { "river": 8 }
body 1: 13 cells -> { "river": 13 }                body 5:  7 cells -> { "river": 7 }
body 2:  9 cells -> { "river": 9 }                 body 6:  6 cells -> { "river": 6 }
body 3:  8 cells -> { "river": 8 }
```

Six of the seven enclosed bodies are pure river; the seventh is half. The entire map holds **eight**
lake cells. Everything that looked like a defect follows from that and is correct behaviour:

- **The 0.085-tile distance is right.** A river is well under a tile wide, so its middle really is a
  fraction of a tile from its bank. There is no missing interior — a river has none by definition.
- **The "dashed ribbon" was a river sampled across.** Eight samples per cell stepped along a row
  through a body about 0.6 tiles wide, catching it where it wove through that row and missing it
  where it wove out. The picture was of a river, read as if it were a broken lake.
- **`IsLakeAtPosition` answering "no" was correct**, not the empty-field bug the first draft claimed.

**A change was written and reverted.** `TerrainWaterStage.CarveLakeBasins` was rewritten to fill each
basin outward from its seed with a per-lake size cap, on the theory that the flood was spreading
across saddles. It built and ran, and the measurement that was supposed to confirm it instead showed
the same ribbon — which is what led to asking `WaterSourceAt` and finding rivers. The carve is back
to exactly what it was. Nothing was wrong with it.

**What this leaves open, as a question rather than a defect:** this map generates almost no lakes at
all — eight cells against 59 of river and 3330 of ocean. Whether `LakeCoverage` should produce more
than that is a tuning question for the owner, and it is NOT what was reported: what the owner was
looking at and calling a lake was, in six cases out of seven, a river.

## Corrected from the first draft of this plan

The first diagnosis said the coast field "is built from ocean cells only" and that lakes are
"excluded by construction". That is wrong and worth recording so it is not repeated:
`TerrainCoastField.BuildPixels` takes `field.IsWaterAtPosition` for the R channel — **all** water,
lakes included — and uses `OceanCells` only for the G channel (the open-sea flag) and the B channel
(distance to ocean). Lakes are measured. There is just almost nothing there to measure.

## Tried and NOT kept

- **`LAKE_DEPTH_SCALE`**, shelving a lake to full colour over a quarter of the sea's reach. Written
  twice and reverted twice. It cannot work: scaling a ramp still multiplies a distance, and the
  distance inside a lake is 0.085 tiles. A quarter of 4.5 tiles is still twelve times more than the
  deepest point of any lake on the map. The floor replaced it because a floor does not ask the
  distance anything.

## Kept

- **`TerrainGeneratorComponent.LakeShoreWidth` now defaults to 0.65** instead of 0. The rim has been
  implemented and guarded (`terrain_lake_bank_probe` drives 0, 0.25, 1, 2) since it was written, but
  the default stayed at zero from when it had no implementation, so every generated map had lakes
  with no shore at all. `terrain_generator_lab.tscn` already stored 1.0, so the lab was never the
  case that proved it.
