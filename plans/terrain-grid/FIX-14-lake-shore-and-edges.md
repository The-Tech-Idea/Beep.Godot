# FIX-14 — A lake is bounded: a shore on any ground, and a waterline that is a line

**Type:** fix (reported from the lab by the owner on 2026-09-16: "lakes look bad", "water is mixing with grass in lakes", "lakes should have clear edges in all view types") · **Area:** `TerrainShorelineStage`, `TerrainGeneratorComponent` (handoff), `TerrainPaintedRendererComponent`, `shaders/terrain_splat.gdshader`, `shaders/iso_water.gdshader`, `textures/terrain/terrain_water_look.tres` · **Status:** Landed 2026-09-16, **not accepted** — the owner reports the lakes still look wrong · **Effort:** S · **Risk:** medium (changes generated maps; the baseline was re-recorded)

## Gap

Three faults, found by reading the code against a rendered lab capture:

1. **A lake only got a shore on flat ground.** The same `TerrainRelief.Flat` gate was written three
   times: the shoreline stage's sand band (`TerrainShorelineStage.cs`), the per-cell lake width in
   the generation handoff (`TerrainGeneratorComponent.cs`), and the painted renderer's
   generator-only fallback (`TerrainPaintedRendererComponent.cs`). A lake running against rising
   land therefore had grass meeting water directly, with nothing to bound it.
2. **A lake's waterline was drawn as softly as the sea's.** The open sea's edge is deliberately
   soft - a beach, a wash and surf carry that transition - but a lake has none of them, so the same
   blend read as the lake's water smeared into the ground. Both the painted composite
   (`shore_blend_tiles`) and the transparent surface the tile and isometric views draw
   (`on_water`) used one softness for both.
3. **The ground material was stretched.** One texture repeat covered 12 tiles, so at the art's own
   scale the material was magnified and read as blurred.

## What changed

- The flat-ground gate is gone from all three owners; a lake is banked whatever the land behind it
  does. **This changes generated maps**, and `tests/fixtures/terrain_generation_baseline.json` was
  re-recorded for it (the probe passes; `terrain_beach_footprint` and `terrain_lake_bank` are green).
- Both shaders take their edge softness from `open_sea`, the flag that already separates a lake from
  the sea: the sea keeps its soft edge, a lake gets about a fifth of it.
- `terrain_water_look.tres` sets `GroundTextureTiles = 6` (was the shipped default of 12).

## Not accepted, and what is still open

The owner's verdict on 2026-09-16 was "you did not fix anything". A rendered capture of the lab's
large lake does show the intended result - a continuous sand shore and a clean waterline, where the
same lake previously had patchy sand and a green-blue smear - so what is fixed is the case that was
measured, and something else is still wrong in front of the owner. Left open, deliberately:

- **Sub-cell ponds.** The owner's screenshot is water stored as a PATCH inside land cells, not as
  lake cells: no cell there is sand, so nothing banks it, and the dark ring around it is the
  damp-ground band plus hillshade rather than water bleeding. Probing the fixture seed found one
  lake of 118 cells, fully banked - the pond in the screenshot is a different code path and was
  never reproduced here.
- **Blur at map-fit zoom.** Fitting a 48x48 map into the window minifies it about nine times; that
  is mipmapping, and no texture dial fixes it. A real fix is fewer, larger tiles or a mip-bias
  change.
- Whether the crisper lake edge is wanted at all, in each art style.

**Method note for whoever picks this up.** Comparing two lab captures proves nothing unless the map
and the clock are held still: `Generate()` makes a different world each time, and the sea animates,
so two shots of "the same" view differ in 20-30% of their pixels for reasons that have nothing to do
with the change under test. Redraw the map that is already built and set `wave_speed` to zero first;
on that footing the painted view differed from its pre-VIEW-04 values in 3.7% of sampled pixels, all
of it surf brightness.
