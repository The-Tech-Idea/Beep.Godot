# TerrainFlow

Support utility shared by two generation stages (`TerrainRiverStage` and `TerrainErosionStage`).

`TerrainFlow` computes a single D8 drainage network over the world's sample grid: for every land cell it finds the steepest downhill neighbour among its eight neighbours (falling back to the neighbour nearest the coast if the cell is a local pit, so flow never dead-ends inland), then accumulates flow by processing cells highest-elevation-first so each cell's own upstream total is finished before it hands water to its downstream neighbour. It exists specifically so the river stage and the erosion stage compute drainage identically — the file's own doc comment states the point is "one answer rather than two implementations drifting apart", since rivers are drawn where the water is and erosion cuts land by how much water passes.

## Public API

- `public static int Accumulate(TerrainWorld world, int[] flowsTo, int[] order, float[] flow)` — fills the three caller-provided arrays (`flowsTo`: each land cell's downhill neighbour index, or -1; `order`: land cell indices sorted highest-elevation-first; `flow`: accumulated drainage count, starting at 1.0 per cell and summing downstream) and returns the number of land cells. Non-land cells are left at `flowsTo = -1`, `flow = 0`. Returns 0 immediately if there is no land.
- `public static int Downhill(TerrainWorld world, int current)` — for one cell, returns the neighbour index it drains to: the neighbouring water cell if adjacent to open water (flow exits the land there), else the lowest-elevation land neighbour, else (if no lower neighbour exists — a pit) the neighbour nearest the coast by `CoastDistance`. Returns -1 only if there is no water neighbour, no lower neighbour, and no closer-to-coast neighbour at all (i.e., an isolated single-cell island with no eligible neighbours).

`HighestFirst` (an `IComparer<int>` sorting by descending elevation) is a private nested class, not public API.

## Dependencies

- Reads `TerrainWorld.Land`, `TerrainWorld.Elevation`, `TerrainWorld.CoastDistance`, `TerrainWorld.Width`, `TerrainWorld.Index`, `TerrainWorld.InBounds` (from `TerrainWorld.cs`). Writes nothing to `TerrainWorld` itself — all output goes into the caller-supplied `flowsTo`/`order`/`flow` arrays.
- Consumed by `TerrainErosionStage.Apply` (from `TerrainErosionStage.cs`), which calls `Accumulate` once per `Apply` call and reuses the resulting network across all erosion passes.
- Consumed by `TerrainRiverStage.Apply` (from `TerrainRiverStage.cs`, not in this batch but referenced by the class doc comment) to decide where river channels are placed.
- Called by `TerrainFieldBuilder.Build` only indirectly, through `TerrainErosionStage` and `TerrainRiverStage` — `TerrainFieldBuilder` never calls `TerrainFlow` directly.

## Notes

- `count` in `Accumulate` is `world.Count` (every sample, land and water), but `order`/`flowsTo`/`flow` are sized and indexed by the caller to `world.Count` as well in both current call sites (`TerrainErosionStage` allocates `new int[count]` for all three) — only the first `land` entries of `order` are meaningful; callers must know to iterate `order[0..land)`, not the full array. Both current callers (`TerrainErosionStage`) do this correctly.
- The single-pass accumulation relies on `order` being strictly highest-first and land cells never draining to a higher land cell; `Downhill` only ever returns a strictly-lower land neighbour or a coast-ward one, so this invariant holds by construction — no separate cycle-guard exists or is needed given that guarantee.
- No exported/configurable parameters, no dead code; the file is a tight, self-contained algorithm with no accepted-but-unused settings.
