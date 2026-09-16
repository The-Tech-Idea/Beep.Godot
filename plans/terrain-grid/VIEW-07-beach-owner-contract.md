# VIEW-07 — Beach: the stage owns width and inland kind; the painter's band agrees at cell centres

**Type:** fix (two beach owners and a half-sample formula written four times; the painted beach can disagree with the cell the game calls sand) · **Area:** `TerrainEuclideanDistance`, `TerrainShorelineStage`, `TerrainCoastField`, `shaders/terrain_splat.gdshader`, `TerrainPaintedRendererComponent`, `docs/terrain-engine/` · **Status:** Partly implemented 2026-09-16 — the one-owner refactor landed; **the cell-centre shader contract was built, rejected on sight by the owner, and reverted** · **Effort:** S (1–2 days) · **Risk:** low (behaviour-identical refactor proven by the recorded baseline)

## As landed (2026-09-16)

**Landed: one owner for the half-sample correction.**
`TerrainEuclideanDistance.ToTiles(squared, samplesPerTile)` (with a float overload for the stages'
shared scratch) is the one owner of `max(0, √d² − 0.5) / samplesPerTile`. All four sites call it:
`Signed`, the shoreline stage's ocean and lake bands, and the coast field's live and static
ocean-distance writes. `Signed` keeps its own saturation case for an entirely uniform mask, which is
not a boundary distance at all.

- `TerrainShorelineContourSmoke` (headless, inside the registered shoreline probe) checks `ToTiles`
  on a straight boundary at 1, 4 and 12 samples per tile, and that a sample inside the boundary
  never reports a negative distance. **Mutation:** dropping the `− 0.5` failed it
  (`ToTiles(1, 1) = 1, not 0.5 tiles`).
- Pin, both directions: no file in `ecs/terrain/` but `TerrainEuclideanDistance.cs` may write
  `Math.Sqrt(…) - 0.5`, and the stage and the coast field must both call `ToTiles(`.
- **`terrain_generation_baseline_probe`: all 96 hashes identical** - the proof the refactor changed
  no generated map.

**Reverted: the cell-centre contract (design point 3). It looks wrong, and the plan was wrong to
ask for it.**

Forcing `beach` to the id texel's verdict inside the central half of a cell makes the beach decision
piecewise constant per cell: the smooth band becomes cell-square cores with a thin transition ring,
and where a cell the stage did not call sand sat inside the band, the sand was pulled out from
between the grass and the waterline. The owner saw it in the lab immediately - "green is boxy and
blends with water" - and it was reverted the same session. It is the tile staircase the shader's own
comments record fighting off ("Mixing land to water on the tile-resolution neighbour fraction
instead put a second, coarser coastline on the map"), which should have been read as the answer
before the rule was written.

The defect the item names is therefore still open: the painted view composites its own band, so a
cell the game calls sand can be painted grass at its centre and the reverse. What a fix must not do
is quantise the band per cell. Two directions worth trying, both needing captures in front of the
owner before landing:

1. Nudge the band's threshold per cell so the centre falls on the correct side while the contour
   stays continuous - a bias, not a snap.
2. Make the width texel and the stage's decision come from one field at one resolution, so the band
   agrees without any shader special case. Bigger, and it touches generation.

`tests/terrain_beach_centre_probe.gd` reproduces the disagreement (authored cells, a deliberately
over-wide width texel, flat material colours, the count of cells whose centre disagrees with their
own kind). It is **not registered** and is not a guard: it reports the open defect rather than
asserting it away. With the rejected rule in place it read 0 disagreements; without it, 32.

## Gap

**Two owners.** The shoreline stage decides the beach: a land sample within `BeachWidth` of the ocean mask, or a flat sample within `LakeShoreWidth` of a lake, becomes `sand` (`TerrainShorelineStage.cs:24-40`), and the cell kind, `terrain_shore_inland` and both widths ride the handoff (`GridCellDataComponent.cs:650-652`, keys `:682-683`; `GeneratedTerrainField.InlandTerrainAtCell :100`). The tile and block views draw that kind. The painted view composites its *own* band per fragment: the width texel it wrote (`TerrainPaintedRendererComponent.cs:505`, `BeachWidth / 4` as a byte) against the coast field's ocean distance (`terrain_splat.gdshader:325-328`; lake banks `:329-333`; the mix `:334`), swapping in the inland id on bank texels (`id_at`, `:206-212`). The painter's own comment records what two owners cost: with `BeachWidth 0.028` the generator made no beach, two views showed none, and this one drew the 1.15 tiles its shader defaulted to (`:379-391`).

**One number, four writers.** The half-sample correction `max(0, √d² − 0.5) / samplesPerTile` — the rule that puts a straight boundary between sample centres — is written in `TerrainEuclideanDistance.Signed` (`TerrainEuclideanDistance.cs:146`), twice in the stage (`TerrainShorelineStage.cs:29,38`) and twice in the coast field (`TerrainCoastField.cs:541-543` live, `:795-797` static). The stage evaluates it at the generator's sample resolution (`SamplesPerCell`), the field at its own fine resolution (`detail`, default 12: `TerrainCoastField.cs:31`, `EffectiveDetail :753`), and the cell kind is the majority of samples (`TerrainTileReductionStage.cs:19-22`). So a cell the game calls `sand` can be painted grass at its centre and a grass cell can be painted sand — the same class of defect `docs/game-builder/TERRAIN_VIEW_GRID_INTEGRATION.md:47-55` records for water at seed 31415, cell (21,3), which has a probe; the sand case has none.

## Design

1. **`TerrainEuclideanDistance.ToTiles(double squared, int samplesPerTile) → float`** (float overload for the shared scratch): `max(0, √squared − 0.5) / samplesPerTile`. The four sites call it (`:146`, stage `:29,38`, field `:542,796`). Behaviour-identical: ENH-16's 96 baseline hashes prove it.
2. **The owner, stated.** The stage owns the beach — the cell kind, `terrain_shore_inland`, `terrain_beach_width`, `terrain_lake_shore_width`. The painter's band renders those; it decides nothing. Written on the stage's and painter's pages, and beside the width texel write (`:505`), whose comment already says "the beach is as wide as the GENERATOR says" (`:379-380`).
3. **Cell-centre contract in the shader.** Inside the central half-cell (both axes of `fract(tile)` within 0.25 of 0.5) `beach` is forced to the id texel's verdict — a `sand` id texel → 1, any other → 0 — for ocean and lake banks alike; outside it the width band blends as today, so sub-cell contours stay smooth. This is the rule the water-centre probe states for water, adopted for sand; it does not claim the water fix.
4. **No new dial.** The width texel stays the stage's number (`:505`); the shader's default band is never reached for a generated map.

## Guards (fail first)

- Rendered (`tests/run_terrain_integration.ps1`): seed 31415 Tiny, painted view, at three zooms — for every cell, the material decoded at the cell-centre pixel is sand exactly when `GridCellRules.TerrainKindAt(cell) == "sand"`. **Mutation:** force every width texel to 4.0 → inland grass cells paint sand at centre, the check fails at the first coast cell.
- Headless: `ToTiles` on a straight boundary between sample centres reads a whole number of tiles. **Mutation:** drop the `− 0.5` → reads n.5.
- Pin, both directions: `- 0.5` beside `Math.Sqrt` only in `TerrainEuclideanDistance.cs`; `ToTiles(` called from the stage and the coast field.
- `terrain_generation_baseline_probe` (96 hashes identical), `terrain_beach_footprint`, `terrain_lake_bank`, `terrain_shoreline_contour` green.

## Dependencies / collisions

ENH-16 (landed; the baseline is the proof), DUP-15 (live water reconstruction feeds the same field). Collision: `terrain_splat.gdshader` is also edited by VIEW-12 (hillshade) and VIEW-14 (canvas modulate) and by the art library's `art_style` paths — sequence the shader edits, one at a time.

## Out of scope

The water-centre defect at (21,3) (open painter item); beach art; tile and block sand transitions; changing what `LakeShoreWidth` means.
