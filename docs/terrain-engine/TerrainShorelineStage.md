# TerrainShorelineStage

Internal generation stage, called after tile reduction and terrain scale cleanup,
before resources, vegetation and start positions are placed.

## Contract

For climate-driven presets, fine Euclidean distance to ocean samples classifies
dry samples within `BeachWidth` as sand. Widths are in cells, with no minimum
whole-cell ring. Zero disables the band. Explicit themed presets retain their
ground policy. Lake banks use their own fine Euclidean inset and
`LakeShoreWidth`, independently of ocean width, on any ground.
Rivers are not lake banks.

Both bands measure distance through
[`TerrainEuclideanDistance.ToTiles`](TerrainEuclideanDistance.md), the one owner
of the half-sample correction, so the band this stage cuts and the band the
painted view draws from the coast field apply the same rule.

The stage updates both the cell material and its dry samples. It does not change
water samples, water-body labels, elevation, relief, land coverage or seed. It
runs only during generation, never during redraw or player terrain editing.

## The stage owns the beach and the lake bank (VIEW-07, 2026-09-16)

Which cells are sand, what lies inland of them (`terrain_shore_inland`), and how
wide each band is (`terrain_beach_width`, `terrain_lake_shore_width`) are decided
here and nowhere else. The tile and block views draw the cell kind. The painted
view composites its own band per fragment, from the width texels the generation
handoff carries out of this stage against the coast field's distance — it renders
those numbers, it does not choose them, and there is no shader-side default width
left for it to fall back to.

That ownership is stated, not yet fully enforced at the pixel: see
[TerrainPaintedRendererComponent](TerrainPaintedRendererComponent.md) for the
cell-centre disagreement that remains open.

## A lake is banked on any ground (FIX-14, 2026-09-16)

The lake band used to apply only where `TerrainRelief.Flat` held. The same gate
was written three times — here, in the per-cell lake width of
`TerrainGeneratorComponent`'s handoff, and in the painted renderer's
generator-only fallback — so a lake running against rising land got no shore at
all: grass met water directly, and the lake read as a stain on the hillside
rather than as a body of water with an edge. All three gates are gone. A shore
is what bounds water; the ground behind it may do what it likes.

**This changes generated maps.** `tests/fixtures/terrain_generation_baseline.json`
was re-recorded for it on 2026-09-16;
`terrain_generation_baseline_probe`, `terrain_beach_footprint_probe` and
`terrain_lake_bank_probe` pass against the re-recorded fixture. The change
landed but was **not accepted** by the owner — see
`plans/terrain-grid/FIX-14-lake-shore-and-edges.md` for what is still open.

## Why It Exists

Sub-cell sand can lose a majority vote in gameplay cells. Generated live cells
therefore retain `terrain_shore_inland`, `terrain_beach_width`, and
`terrain_lake_shore_width` metadata. `lake_surface` stores bit-packed fine lake
membership separately from all-water geometry, including through save/load.
The painter uses these with the shared coast field to retain sub-cell beaches.
Its separate cached lake-distance field reconstructs lake banks without
reintroducing the cell-majority sand edge. `TerrainBiomeStage` no longer burns
lake sand into the underlying biome before this stage.
Explicit terrain painting clears this metadata in edited cells. The earlier
minimum whole-cell ring was rejected visually and has been replaced.

`terrain_coastal_grass_probe.gd` checks 36 seed/shape/width cases against an
independent distance oracle. `terrain_beach_footprint_probe.gd` checks fixed
land/water footprint. The display cache smooths distance before GPU sampling;
its output is not an exact metric offset. A generated-cell centre discrepancy
remains tracked in `plans/TERRAIN_VIEW_GRID_INTEGRATION.md`.

`terrain_lake_bank_probe.gd` checks an analytic circular lake at widths 0, 0.25,
1 and 2, separate ocean/river rules, saved patches, explicit edits, and 10,800
GPU radial samples across Original, Pixel Art and Cartoon.
