# FEAT-20 — A river has a bank, at its own width

**Type:** feature (generation + painted rendering) · **Area:** `TerrainGenerationBuffer`, `TerrainRiverStage`, `TerrainShorelineStage`, `GeneratedTerrainField`, `TerrainCoastField`, `shaders/terrain_splat.gdshader` · **Status:** generation LANDED 2026-09-19; the band that draws it is NOT done · **Effort:** M · **Risk:** medium — it adds a generated per-sample fact and a channel to a published field.

## Where this actually stands

**Done and measured:** the carve marks each river's bank in the same disc loop that cuts its
channel, recording THICKNESS in samples so each river's own width survives
(`TerrainGenerationBuffer.RiverBank`). `RiverBankScale` sizes it as a multiple of the river's radius
and zero turns it off. The shoreline stage reduces it to a per-cell shore width
(`CellShoreWidth` → `GeneratedTerrainField.ShoreWidthAtCell`), which replaced the map-wide constant
that used to fill that channel for every cell on the map.

**Not done:** the painted view does not draw it. Two attempts, both reverted the same day, and the
reason is worth recording because it is not obvious and it cost two rewrites.

## Why the beach's band cannot draw a riverbank

The band is `distance-to-water <= width`, with `width` read from a texture **per cell**. That is
exact for the ocean, whose width is one number for the whole map, and it cannot express a riverbank:

- **First attempt** gave every river the lake's 0.65-tile width. A river is 0.125 to 0.375 tiles
  wide, so the band was five times the width of the water it edged, grown from every thread of a
  dense network. Sand desert.
- **Second attempt** made the width per cell, taken from each river's own carve. Better, and the
  per-cell width is worth keeping - but still a desert, because a cell that edges BOTH a lake and a
  river carries one width. It carries the lake's, and then applies it to the river in the same cell.

A per-cell number cannot answer a question that varies inside the cell. No amount of choosing the
number better fixes that; it is the wrong shape of answer.

A third failure mode, also seen and also worth knowing: marking the bank as `"sand"` terrain paints
whole CELLS, because a cell holding a river has most of its samples under water and excluded from
the kind vote, so a bank a fraction of a tile wide wins the cell. The bank must not touch cell
terrain at this width.

## What will work

Carry the bank's SHAPE, not its width — which is what the design below always said, and what both
attempts cut a corner around.

The carve already marks the ring exactly. What is missing is a sub-cell signed distance field of
that mask, written into the lake field's spare green channel, which the shader draws with `>= 0`
and **no width compare at all**. No width, no propagation, no per-cell approximation, and the
variation comes free because the mask already varies. The steps are 4, 5 and 6 below.

## Ask

The owner, 2026-09-19: *"why not use the same algorithm you draw beach to draw rivers then add rocks
or grass to them. this will allow you to have different size and width of river easily."*

Right on both counts, and the second is the load-bearing one.

## Why the first attempt failed

Rivers were added to the lake bank field on 2026-09-18 and reverted within the hour: the map came
out a sand desert holding two ponds. The algorithm was not the problem. The WIDTH was.

`TerrainRiverStage.cs:87` already sizes every river from its own flow:

```csharp
// Width from flow, so a trunk is wider than the streams feeding it.
int radius = Mathf.Clamp(1 + Mathf.FloorToInt(Mathf.Log(flow[index] / threshold + 1.0f) * 1.6f), 1, 3);
Carve(world, index, radius);
```

Radius 1 to 3 SAMPLES. At the eight samples per cell this map builds at, a river is **0.125 to
0.375 tiles** wide. The reverted attempt grew a lake's **0.65-tile** bank from every one of those
threads — five times the width of the water itself, from a dense network — and the bands merged.

And that radius is thrown away. Only `WaterBody.River` survives `Carve`, so nothing downstream can
ask how wide a river is. `VIEW-06` proposed publishing it and has never been built.

## Design

### The bank is carved, not measured

A distance-field band needs a width at the point it is drawn, and the land beside a river does not
know which river it is beside. The usual answer is a feature transform — Felzenszwalb's algorithm,
which `TerrainEuclideanDistance` already implements, tracks the nearest site per pass and can carry
one, and that would work.

It is not needed, because of one property of dilation:

```
(A ∪ B) ⊕ D  =  (A ⊕ D) ∪ (B ⊕ D)
```

Dilation distributes over union. A river is carved as a union of discs of radius `r` along its
path, so painting a second disc of `r + bank(r)` in the same loop is EXACTLY the offset of that
river by `bank(r)` — not an approximation of it. The width varies per river because `r` does, the
variation costs nothing, and the shape is decided where the river is made.

So: no new transform, no width channel, no width propagation. `bank(r)` is a proportion of the
river's own radius, which is what makes a trunk's bank broader than a stream's without a second
rule.

### What each piece owns

1. **`TerrainGenerationBuffer`** gains `RiverBank` (bool per sample), allocated with the rest.
2. **`TerrainRiverStage.Carve`** paints the bank ring in the same disc loop: samples between `r` and
   `r + bank(r)` that are still land. It marks the mask only — it never moves water or terrain, so
   the carve's own contract is unchanged.
3. **`TerrainShorelineStage`** turns marked land into `"sand"`, beside the ocean and lake branches it
   already has. This is the gameplay and cell-level fact: it is what the tile and block views draw
   and what a builder stands on.
4. **`GeneratedTerrainField`** publishes `IsRiverBankAtPosition` at sample resolution.
5. **`TerrainCoastField`** writes a signed distance to the bank REGION into the lake field's spare
   green channel. That field's R is distance to lakes, G is unused (the open-sea flag is all false
   for a lake field), B is a meaningless ocean distance, A the patch flag — so this is a free
   channel in a texture that already exists, not a second texture.
6. **`terrain_splat.gdshader`** folds it into the band it already computes:
   `beach = max(beach, lake_bank)` becomes `max(..., river_bank)`, where `river_bank` is simply
   `bank_field >= 0` — no width compare, because the width is already in the mask.

A cell-resolution bank cannot express this: a bank 0.125 tiles wide never wins a cell's majority
vote, so it would round to nothing. The band has to be sub-cell, which is why it goes in the field
rather than only in the terrain ids.

### Then the rocks and the grass

With a bank to stand on, `TerrainFeatureRendererComponent` scatters reeds and pebbles along it, on
the clutter layer added the same week (`TerrainLayers.ZForClutter`), using the rock sheets the owner
supplied in all four styles. `TerrainPropSizing` already has `Reeds` at 0.2-0.35 tiles and
`SmallRocks` at 0.25-0.4 — sized for exactly this and currently only reachable through `marsh`.

## Guards (fail first)

- A generated map's widest river carries a wider bank than its narrowest: read `IsRiverBankAtPosition`
  across a trunk and a stream and compare the ring thickness. **Mutation:** make `bank(r)` constant →
  the two match and it fails.
- The bank never lands on water, and never on a cell the ocean beach already owns.
- Rendered: the band is visible beside a river at map zoom and is NOT a merged blob — the sand area
  stays under a bounded multiple of the river area. **Mutation:** restore the 0.65-tile lake width →
  the ratio explodes and it fails. This is the guard the reverted attempt did not have.

## Not in scope

Flow DIRECTION and animated current — that is the rest of `VIEW-06` and wants the flow map. Banks on
the live/grid path: the sub-cell mask would have to persist as cell metadata, and
`GridCellDataComponent` belongs to another session (`VIEW-06` flags the same collision). The
generated path lands first and the live path follows when that file is free.
