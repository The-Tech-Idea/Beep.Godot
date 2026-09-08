# GridSubsurfaceStoreComponent

The one owner of "how much is left underground," per cell. The doc comment draws an explicit split: the map's data layers (`TerrainDataLayersComponent`) say what lies beneath each cell and how rich it is — immutable facts of the generated world, reproducible from the recipe — while this component owns the *mutable* half, the remaining amount, drawn down by extractors and carried through saves. That split keeps the published map pure while the drawdown lives with the rest of gameplay state. The regeneration the split relies on is real: `TerrainWorldComponent` saves its recipe (axes and seed) and `RestoreWorld` rebuilds the layers from it on load, so a restored remaining amount reads against the same deposit it was drawn from. The two halves meet lazily — `SeedAmountAt` reads the layers on first touch, never inside `Load` — so the order the saveables restore in is irrelevant; `tests/terrain_world_recipe_probe.gd` proves the drawdown realigns. It implements `ISaveable`.

A cell's remaining amount is seeded lazily on first touch — `richness × the catalog definition's per-cell Amount` (or `DefaultCellAmount` if no catalog is wired) — rather than pre-computed for the whole map up front. Cells never touched are never stored in `_remaining`, so a save stays the size of what the player actually worked rather than the size of the map.

## Public API
The snapshot includes `underground_identity`, a stable hash of published deposit IDs,
richness bands, depths and absolute bounds. Cached amounts apply only to that identity.
Missing/unbuilt sources report zero; a different map reads its own seeded stock.
Reads do not erase pending restored state, including when an old map is still present
during load. The first draw on a different map replaces the old depletion ledger.
Capture excludes mismatched depletion; snapshots without identity restore no depletion.

- `string ResourceIdAt(Vector2I cell)` — the underground resource id beneath a cell, or empty; delegates to `TerrainDataLayersComponent.UndergroundResourceAt`.
- `int RemainingAt(Vector2I cell)` — units left beneath a cell; returns the stored drawdown if the cell has been touched, else lazily seeds and returns it via `SeedAmountAt` (without persisting the seed to `_remaining` until a `Draw` actually happens — a read-only `RemainingAt` call does not itself allocate a dictionary entry).
- `int Draw(Vector2I cell, int amount)` — draws up to `amount` from beneath a cell, returns what actually came up (0 if no deposit or already worked out). Emits `DepositChanged` on every successful draw, and `DepositDepleted` additionally when the last unit leaves.
- `Godot.Collections.Dictionary CaptureState()` / `void RestoreState(Godot.Collections.Dictionary state)` — public snapshot/restore of `_remaining` under a `"cells"` array of `{cell, remaining}` dictionaries.
- `void Save(GameBuilder.GameStateData state)` / `void Load(GameBuilder.GameStateData state)` — ordinary public `ISaveable` implementation writing/reading `CaptureState()`/`RestoreState()` under `SaveKey`.
- Exports: `ParticipatesInSave`, `SaveKey`, `DataLayersPath`, `Catalog` (`ResourceCatalog?`), `DefaultCellAmount`.
- Signals: `DepositChanged(int x, int y, string resourceId, int remaining)`, `DepositDepleted(int x, int y, string resourceId)`.

## Dependencies
- Resolves `TerrainDataLayersComponent` at `DataLayersPath`, explicit-only (its own code comment: "like every other `DataLayersPath`... a scene with two data-layer nodes must not silently pick one" — no scene-wide `FindComponent` fallback, unlike most other `NodePath` resolutions in this batch).
- Calls `TerrainDataLayersComponent.UndergroundResourceAt(Vector2I)`, `.UndergroundRichnessAt(Vector2I)` (outside this batch).
- Its three methods (`ResourceIdAt`, `RemainingAt`, `Draw`) are the contract `GridExtractorComponent.TryBind`/`RunCycle`/`ExpandToReservoir` (this batch) call into directly — per that file's own doc comment, this is "the real contract the system depends on," callable by any script independent of `GridExtractorComponent`.
- `GridProspectingComponent.Survey` (this batch) reads `TerrainDataLayersComponent.UndergroundResourceAt` directly rather than through this component — the two components read the same underlying data layer independently rather than one depending on the other (see that file's Notes).
- Joins `SaveableHelper.Group` on `_Ready` (outside this batch), matching `GridStorageComponent` and `GridProspectingComponent` in this same batch.

## Notes
- `Save`/`Load` are ordinary public methods here (not explicit `ISaveable.Save`/`.Load` interface implementations, unlike `GridStorageComponent` in this batch) — correct and consistent, since this component has no `Load(string, int)` port method for a public `Load(GameStateData)` to collide with in Godot's name-based `Call` dispatch. See `GridStorageComponent.md` for the contrasting case.
- `RemainingAt` and `SeedAmountAt` both call `ResolveReferences()` on every invocation (as does `Draw` via `ResourceIdAt`) rather than resolving once and trusting it — consistent with the `IsInstanceValid`-guarded re-resolution pattern used throughout this batch, cheap since the guard short-circuits once resolved.
- `_remaining.TryGetValue` is checked before falling back to `SeedAmountAt` in `RemainingAt`, but `Draw` re-derives `remaining` via a fresh `RemainingAt(cell)` call rather than reusing a value — no correctness issue (it's the same lazy-seed-then-read path either way), just two call sites doing the same lookup rather than one passing a value to the other.
