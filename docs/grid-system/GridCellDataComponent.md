# GridCellDataComponent

Pure data model for per-cell land state — terrain kind, a bitmask of workflow flags, crop id/age/maturity/regrow, and arbitrary metadata — for top-down/isometric farming, builder, tactics, and settlement games. It is a plain `Node` (no `Node2D`, no TileMap), so a game can have grid "state" long before it has grid "art"; renderers such as `GridCellOverlayComponent` and `GridTileMapLayerBridgeComponent` read from it rather than the other way around.

It exists as the single owner of cell state specifically so nothing else has to duplicate it: the crop lifecycle (`PlantCrop`/`HarvestCrop`/`RemoveCrop`/`AdvanceDay`), the `[Flags] CellFlags` bitmask (`Blocked`/`Cleared`/`Tilled`/`Watered`/`Planted`/`HarvestReady`), and free-form `Metadata` all live here, and every other grid file in this batch that draws or serializes a cell reads through this one. Two design choices are called out in the code's own comments: bulk loads (`LoadCells`, `LoadGeneratedCells`) emit a single `CellsChanged` at the end rather than one `CellChanged` per cell, because a generated map is thousands of cells and a per-cell signal used to make `GridTileMapLayerBridgeComponent` run a full `TileMapLayer` internals update per cell before the batch-end rebuild repainted the same map again; and there is a second, `internal`, typed bulk-load path (`LoadGeneratedCells`) alongside the public `Variant`-based `LoadCells`, added because marshalling one `Godot.Collections.Dictionary` per cell was the single biggest allocation in a world build — the terrain generator uses the typed path, GDScript and saved data use the Variant path, and both honor the same one-signal-per-batch contract.

## Public API

Generated cells retain an immutable `GridTerrainWaterPatch` alongside terrain
metadata. `GetCell`/`GetCells` include its `water_surface` string, and `LoadCells`
restores it. Thus `GridWorldStateComponent` saves fine shorelines with the edited
grid; no renderer-owned or recipe-only copy is needed for restoration.

`SetTerrainKind` and `FillTerrain` are explicit whole-cell painting operations:
they replace fine shoreline detail, even when painting the existing terrain kind.
Flags, crops and metadata edits preserve it. A partial cell load replaces only the
provided records. Malformed water patches throw `FormatException` before any
records are cleared or changed; callers should report a failed snapshot restore.

An internal `TerrainRevision` tracks terrain painting, elevation metadata,
fine-patch replacement, cell creation/clearing and snapshot/generation loads.
Existing-cell flags, crops and non-elevation metadata retain the revision.
The painted renderer combines it with source identity and default terrain kind
to reuse unchanged map textures. General cell signals are unchanged, so workflow
and feature consumers still receive their normal notifications.

- `[Flags] public enum CellFlags { None, Blocked, Cleared, Tilled, Watered, Planted, HarvestReady }` — per-cell workflow state, bitwise-combinable.
- `[Signal] CellChangedEventHandler(int x, int y)` / `CellsChangedEventHandler()` / `CropMaturedEventHandler(int x, int y, string cropId)` / `DayAdvancedEventHandler(int days)`.
- `[Export] public string DefaultTerrainKind { get; set; } = "grass"` — terrain kind returned for any cell with no stored record.
- `[Export] public bool ClearWaterOnNewDay { get; set; } = true` — whether `AdvanceDay` strips the `Watered` flag from every cell each call.
- `public void ClearCells()` — drops all stored cells and resets `HasStartAreas`; emits `CellsChanged` only if something was actually cleared.
- `public bool HasCell(Vector2I cell)` / `public int CellCount` — presence/count of stored (non-default) cells.
- `public string GetTerrainKind(Vector2I cell)` / `public void SetTerrainKind(Vector2I cell, string terrainKind)` — falls back to `DefaultTerrainKind` when empty/whitespace; the setter trims and emits `CellChanged`.
- `public int GetStartArea(Vector2I cell)` — which generated player start area the cell is reserved for: 0 for none (or no stored record), k+1 for start k (FEAT-09). Reads the generated `terrain_start_area` metadata. It is a static generated fact, like `terrain_relief`: who the map set the land aside for, not who owns it now. `GridStartAreaComponent` reads it for `IsInArea`, placement restriction and spawn cells.
- `public bool HasStartAreas { get; private set; }` (FEAT-12) — whether this map carries start-area reservations at all. False on a map generated with no start areas and on a native map published without the gameplay baseline: both have starts a game can read from spawn markers, but no reserved ground, and a build restriction asking "is this cell in my area" there would refuse every cell on the map. Set wherever a reservation can enter the store — a `LoadCells` bulk load (`Generated.StartArea > 0`), `LoadGeneratedCells` (`StartArea > 0`), a `GeneratedPublication.Commit` (which replaces the flag, because it replaces the whole store), and a `SetMetadata` write of `terrain_start_area` above 0 — and cleared where the store is emptied or replaced wholesale: `ClearCells`, a `clearExisting` bulk load through either path, and the publication commit. Evicting a chunk does **not** clear it: the map still has areas, this component just is not holding those cells in memory. `GridStartAreaComponent.HasAreas` is the one reader.
- `public int GetFlags(Vector2I cell)` / `public void SetFlags(Vector2I cell, int flags)` / `public void AddFlag(...)` / `public void RemoveFlag(...)` / `public bool HasFlag(...)` — raw bitmask read/write, all mutators emit `CellChanged` (`RemoveFlag` is a no-op, no signal, on a cell that was never created).
- `public void ClearLand(Vector2I cell)` — sets `Cleared`, unsets `Blocked`.
- `public void Till(Vector2I cell)` — sets `Cleared | Tilled`.
- `public void Water(Vector2I cell)` — sets `Watered`.
- `public bool PlantCrop(Vector2I cell, string cropId, int daysToMature, int regrowDays = -1)` — fails (returns `false`) unless the cell is `Tilled`; otherwise resets crop age, sets `Planted`, clears `HarvestReady`.
- `public bool HarvestCrop(Vector2I cell, bool clearTilled = false)` — if the crop regrows (`CropRegrowDays >= 0`), resets its growth clock in place and ignores `clearTilled` (the plant still occupies the tile); otherwise clears the crop entirely and optionally un-tills.
- `public bool RemoveCrop(Vector2I cell, bool clearTilled = false)` — forces removal even of a regrowing crop (sets `CropRegrowDays = -1` then delegates to `HarvestCrop`); the scythe/undo path, as opposed to the regrow-honoring `HarvestCrop`.
- `public string GetCropId(Vector2I cell)` / `public int GetCropAgeDays(Vector2I cell)` / `public int GetCropRegrowDays(Vector2I cell)`.
- `public void SetMetadata(Vector2I cell, string key, Variant value)` / `public Variant GetMetadata(Vector2I cell, string key)` — arbitrary per-cell key/value storage; a missing key returns a default (null) `Variant`, indistinguishable from an explicitly stored null.
- `public void AdvanceDay(int days = 1)` — for every stored cell: optionally clears `Watered`, ages any planted crop, sets `HarvestReady` (and emits `CropMatured`) the tick it first reaches maturity; emits `DayAdvanced` once at the end.
- `internal IEnumerable<(Vector2I Cell, CellFlags Flags)> EnumerateFlags()` — lean typed enumeration for same-assembly drawers, avoiding the per-cell `Dictionary` marshalling `GetCells()` would cost in a per-frame draw.
- `public Godot.Collections.Dictionary GetCell(Vector2I cell)` / `public Godot.Collections.Array<Godot.Collections.Dictionary> GetCells()` — full marshalled snapshot(s) for GDScript/save consumers.
- `public void LoadCells(Godot.Collections.Array cells, bool clearExisting = true)` and the `Array<Dictionary>` overload (which just wraps into the untyped form) — bulk replace/merge from Variant data; single `CellsChanged` at the end.
- `internal LoadGeneratedCells(...)` receives cell, terrain, feature, relief, shade,
  elevation, water source, fine water patch, inland terrain, beach width, lake patch,
  lake width and start area through the typed generation handoff;
  it retains the single `CellsChanged` bulk-notification contract.
- Generated metadata key `terrain_start_area` (int, k+1 for start k) is present only on
  cells inside a start area, so cells outside every area save nothing extra. It is
  written into `GetCell`/`GetCells` metadata with the other generated `terrain_*` keys,
  so `GridWorldStateComponent` saves it with the cells, and `LoadCells` folds it back
  into the typed generated record (clamped to 0..255).

## Dependencies

- Reads `GridVariantReader.Int`/`.Vector2I` (from `GridVariantReader.cs`) to parse incoming dictionaries in `LoadCells`.
- No other outbound calls into this batch — it is a leaf data store.
- `GridStartAreaComponent` reads `GetStartArea`.
- Called into by three other files read in this same batch: `GridCellOverlayComponent` (`EnumerateFlags`, `GetFlags`), `GridTileMapLayerBridgeComponent` (`GetCells`, `GetFlags`, and the `CellChanged`/`CellsChanged` signals), and `GridWorldStateComponent` (`GetCells`/`LoadCells` for save/restore) and `GridPlacementComponent` (`GetTerrainKind`, `HasFlag(Blocked)`).

## Notes

- `GridCellOverlayComponent.ColorForFlags` and `GridTileMapLayerBridgeComponent.AtlasForCell` both independently re-implement the exact same `CellFlags` priority order (`Blocked` > `HarvestReady` > `Planted` > `Watered` > `Tilled` > `Cleared`) to pick one visual per cell — the same decision table exists twice, once per renderer, rather than once on this component or a shared helper.
- `GetMetadata` on a missing key returns a default `Variant` (null), which is indistinguishable from a key explicitly set to a null `Variant` — callers cannot tell "never set" from "set to null."
- `HarvestCrop`'s regrow branch silently ignores its own `clearTilled` parameter (documented in an inline comment: the plant still occupies the tilled cell), which is a real, intentional divergence from the non-regrowing branch, not an oversight.
- `ClearCells()` resets `HasStartAreas` with the cells it drops. It did not at first, and a component whose cells were cleared and not reloaded reported that the map had areas while `GridStartAreaComponent.IsInArea` answered false for every cell — the "refuses the whole map" shape the flag exists to avoid. `tests/terrain_spawn_markers_probe.gd` clears the store, checks the flag follows, and regenerates.
