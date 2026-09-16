# GridStartAreaComponent

The player start areas of a generated map, answered for gameplay (FEAT-09): which start this player has, where it begins, whether a cell is inside its reserved area, and where its first units can stand. Since FEAT-10 it also owns the **faction-to-start assignment** — which faction holds which start — and the save that carries it. A `[Tool][GlobalClass]` `Node`, and an `ISaveable`.

It owns no *terrain* fact. The reservation is the live cells' `terrain_start_area` (`GridCellDataComponent.GetStartArea`: 0 none, k+1 start k), which the generator hands over once and the grid save carries. The origin is a `Spawns` marker where the map has one (`TerrainSpawnMarkers`, FEAT-12 — an authored or a published map), otherwise `TerrainDataLayersComponent.StartCells()[k]`, which is in the generator's start order. The origin is also the headquarters anchor: the generator grew the area from `GridFootprint.Cells(origin, HqFootprint)` and checked that footprint was level, so a game places its headquarters at `OriginOf(ActiveStartIndex)`. The engine consumers that used to read start 0 ask this component instead: placement (`GridPlacementComponent.RestrictBuildToStartArea`), the worker spawner (`GridWorkerSpawnerComponent.SpawnAtStartArea`) and the camera (`TerrainWorldCameraComponent.StartAreaPath`). Which start a player has is decided in one place.

The one fact it *does* own is the assignment table — `faction index -> start index`, a private `Dictionary<int,int>`. Nothing else stores it, and the two views that draw a start in its holder's colour ([the minimap](GridMinimapComponent.md) and [the map overlay](../terrain-engine/TerrainMapOverlayComponent.md)) ask this component rather than reading [the catalog](GridFactionCatalog.md) by index, because catalog order is not start order the moment a faction locks a start.

## Public API

- `static readonly Vector2I NoCell` — `(int.MinValue, int.MinValue)`, returned by `OriginOf` for an index that is not a start.
- `[Signal] AssignmentChanged()` — the faction-to-start table changed. Emitted by `Assign`, `AutoAssign` and `RestoreState`; no arguments, because both listeners re-bake everything.
- `[Export] NodePath CellDataPath` — the `GridCellDataComponent` holding the generated start areas.
- `[Export] NodePath DataLayersPath` — the `TerrainDataLayersComponent` that publishes the start order.
- `[Export] NodePath NavigationPath` — the `GridNavigationComponent` spawn cells are filtered against.
- `[Export] NodePath SpawnsRootPath` (FEAT-12) — the map's `Spawns` node of `Start_<k>` markers (`TerrainSpawnMarkers`). Set, it is where the origins come from and the data layers are not consulted for them: a marker is the one thing a native map, saved as a plain `.tscn` without a generator, still carries, and a designer's authored map is read exactly the same way.
- `[Export] NodePath GridPath` (FEAT-12) — the `GridProjectionComponent` a marker's position is read as a cell through. Needed with `SpawnsRootPath`.
- `[Export(Range 0,254,1)] int LocalStartIndex` (default 0) — the start this machine's player was given, as its index in the generator's start order. Used when no catalog is wired, or when the local faction holds nothing.
- **`[ExportGroup("Factions")]`** — `FactionCatalog`, `LocalFaction`, `AutoAssignOnBuild` and `WorldPath` below.
- `[Export] GridFactionCatalog? FactionCatalog` — who the factions are. Without one this component answers for one local player and every assignment call refuses.
- `[Export] string LocalFaction` (default `""`) — the faction the player at this machine plays, as its id in the catalog.
- `[Export] bool AutoAssignOnBuild` (default true) — whether the catalog is dealt onto the map's starts as soon as a world is built. Meaningful only once a catalog is wired, so a sandbox with none is unchanged.
- `[Export] NodePath WorldPath` — the `TerrainWorldComponent` whose `WorldBuilt` triggers `AutoAssignOnBuild`.
- **`[ExportGroup("Save")]`** — `ParticipatesInSave` (default true) and `SaveKey` (default `"grid_world.start_assignment"`).
- `int ActiveStartIndex { get; }` — the start the player acting here has: `StartIndexOf(LocalFaction)` when a catalog is wired, `LocalFaction` is non-empty **and** that faction was actually assigned a start; otherwise `LocalStartIndex` clamped to 0–254. A catalog wired but no start held therefore falls back to the authored index rather than answering -1.
- `int StartCount { get; }` — how many starts the map has, from whichever record of them this component reads: with `SpawnsRootPath` set, it counts `TerrainSpawnMarkers.Find(spawns, k)` upward from 0 until one is missing; otherwise `TerrainDataLayersComponent.StartCells().Count`. The same two records `OriginOf` already chooses between, so there is no third opinion about how many starts a map has. 0 when the chosen record is not wired.
- `string Assign(string factionId, int startIndex)` — gives a faction a start, or returns why it cannot have one (see below). Empty string on success.
- `int AutoAssign()` — deals the catalog's playable factions onto the map's starts and returns how many were seated.
- `int StartIndexOf(string factionId)` — the start a faction was assigned, or `-1` when it has none or no catalog is wired.
- `int FactionAtStart(int startIndex)` — the faction holding a start, as its catalog index, or `0` when nobody does.
- `Color ColourOfStart(int startIndex)` — the colour start k is drawn in: its faction's when one holds it and a catalog is wired, transparent (`0,0,0,0`) otherwise, so a caller tints nothing rather than inventing faction 1's colour. **This is the only door to a start's colour**; a contract pin refuses either view calling the catalog's `ColourOf(` at all.
- `Godot.Collections.Dictionary GetAssignments()` — the assignment as a game reads it: normalised faction id to start index. Empty without a catalog.
- `Godot.Collections.Dictionary CaptureState()` / `void RestoreState(Godot.Collections.Dictionary)` / `void Save(GameStateData)` / `void Load(GameStateData)` — `ISaveable`, see the save section below.
- `_GetConfigurationWarnings()` — accumulates: `CellDataPath` empty; both `DataLayersPath` and `SpawnsRootPath` empty (without one there are no start origins); `SpawnsRootPath` set with `GridPath` empty; `NavigationPath` empty; `SaveKey` blank while `ParticipatesInSave` is on (the assignment would be saved under no key and silently lost); `LocalFaction` set to an id the wired catalog does not hold (so this machine's player falls back to `LocalStartIndex`).
- `bool IsInArea(Vector2I cell, int index)` — true when the cell store is wired, `index` is not negative, and `GetStartArea(cell) == index + 1`.
- `bool HasAreas { get; }` (FEAT-12) — whether this map reserves ground for its starts at all: `HasCells` and `GridCellDataComponent.HasStartAreas`. False on a map generated without start areas and on a native map published with markers but no gameplay baseline — maps that have starts and nothing to be inside or outside of. `GridPlacementComponent` asks it before refusing anything, because on such a map every cell would be "outside".
- `Vector2I OriginOf(int index)` — start `index`'s origin in absolute cells, or `NoCell` when the index is negative or the map has no such start. With `SpawnsRootPath` set it is the marker's position read back through `GridPath`; otherwise it is the data layers' `StartCells()[index]`, and `NoCell` when they are not wired.
- `Vector2I MarkerOrigin(int index)` *(private)* — `TerrainSpawnMarkers.Find(spawns, index)`'s `GlobalPosition` through `GridProjectionComponent.WorldToCell`. Markers **win** over the data layers rather than filling in for them: a map that carries markers is authored or published, and its own record of where a start stands is the answer, whatever a regenerated field would say. A `SpawnsRootPath` or `GridPath` that does not resolve pushes a warning and answers `NoCell` — it does not quietly fall back to the data layers, which is how a published map and its regenerated field come to disagree about where a player begins.
- `Godot.Collections.Array<Vector2I> SpawnCellsFor(int index, int count)` — up to `count` walkable cells of start `index`'s area, nearest the origin first, ordered by (distance², y, x) so every machine gets the same answer. It floods over 4-neighbours from the origin through cells `IsInArea` accepts, then drops cells outside the navigation's bounds or blocked in it, so a building placed since generation takes its cells out. Empty when `count` is 0 or less, the navigation is not wired, the start does not exist, or the origin is not inside its own area (a map generated without start areas, or unwired cells).
- `internal bool HasCells { get; }` — whether the cell store resolves. `GridPlacementComponent` asks it so an unwired store refuses with `not_ready` instead of a guessed `outside_start_area`.

## The assignment (FEAT-10)

### `Assign` — reasons, not exceptions

The reasons are *returned*, not pushed: an assignment refused in a lobby is a thing the caller has to show someone. Empty string means it took. The checks run in this order, and the first one to fire is the answer:

1. `no_faction_catalog` — no `FactionCatalog` is wired. It refuses rather than pretending to assign.
2. `unknown_faction` — `FactionCatalog.IndexOf(factionId)` is 0; the catalog does not hold that id (compared normalised, so case and spacing do not matter).
3. `start_out_of_range` — a negative `startIndex`, or one at/past `StartCount`. Note the upper bound is only enforced **while `StartCount > 0`**: a component with neither record of the map's starts wired has no idea how many there are, and refuses only the negative case.
4. `start_locked:<n>` — the catalog pins that faction to start `n` (`LockedStart`) and this is a different start. Checked *before* double-booking, so a locked faction asking for somebody else's start is told about its lock, not about the holder.
5. `start_taken:<id>` — another faction already holds that start; the reason **names the holder**, so a lobby can say who.

Re-assigning a faction to the start it already holds succeeds and emits **nothing** — the table did not change. Any real change emits `AssignmentChanged`.

### `AutoAssign` — locked factions first

The design said "maps catalog order to start order". Taken literally, a faction locked to start 3 would be displaced by whatever the catalog happened to list before it, so the rule that landed is two passes over the catalog:

1. **Locked first.** Every playable faction with `LockedStart >= 0` takes exactly that start. A lock this map cannot honour — past the last start, or onto one another locked faction already claimed — pushes a warning naming the faction and the start, and leaves that faction **unassigned**; it is never quietly moved somewhere else.
2. **Then the rest, in catalog order,** onto the starts still free, lowest free index first. Running out of starts pushes a warning naming the map's start count and the first faction left out, and stops.

Unplayable factions (`Playable` false — scenery, neutral sides) are skipped in both passes. The result is deterministic: the same catalog and the same map give the same table every run, which is what lets two machines agree without talking. `AutoAssign` clears the table first, returns the number of factions seated, and emits `AssignmentChanged` (on the no-catalog and no-starts paths, only if it actually cleared something).

`AutoAssign` seats starts in index order **without consulting FEAT-09's `Usable` report**. A scenario that must avoid an unusable start locks its factions there instead. Whether an unusable start should be skipped is deliberately left open rather than guessed at.

It runs on `WorldBuilt` when `AutoAssignOnBuild` is on and a catalog is wired: a new world has new starts, so an assignment made against the last one means nothing. `_Ready` also catches up on a world that was already built before this node was ready (`_world.BuiltSize.X > 0`), because `WorldBuilt` has already passed by then. A restored assignment is not lost to this: `RestoreState` re-applies over the top.

### The save — keyed by id, and it refuses rather than drops

`ISaveable` under `grid_world.start_assignment` (`SaveKey`), joining `SaveableHelper.Group` in `_Ready` when `ParticipatesInSave` is on and leaving it in `_ExitTree`. The snapshot is `{ "version": 1, "assignments": { <faction id>: <start index> } }`.

- **Ids, not indices.** A catalog edited between save and load moves every index, so an index-keyed table would hand a player someone else's start with nothing reporting it. Keys are normalised faction ids, so a reordered catalog still gives each faction its own start.
- **A version mismatch throws** `FormatException`, as `GridWorldStateComponent` does.
- **A save holding assignments with no catalog wired throws** rather than loading an empty table, which would have put every player on start 0. An *empty* assignment block with no catalog is not an error and simply returns.
- **An id the catalog no longer holds** pushes a warning naming it and that one assignment is dropped; the rest load.
- `RestoreState` emits `AssignmentChanged` on every successful restore, so both views re-bake.

### `AssignmentChanged` — who listens

Both consumers **bake** their colours, so a start changing hands has to re-bake, not merely redraw:

- `GridMinimapComponent` (when `StartAreaPath` is wired) marks its terrain texture dirty and queues a redraw — the tint is a faction's colour, and the bake otherwise runs only on cell changes.
- `TerrainMapOverlayComponent` (when `StartAreaPath` is wired) queues a rebuild — its area segments are baked *with* their colours.

## Dependencies

- Resolves `GridCellDataComponent` (`GetStartArea`, `HasStartAreas`), `TerrainDataLayersComponent` (`StartCells`), `GridNavigationComponent` (`IsInBounds`, `IsBlocked`), and — for the marker path — the `Spawns` node and `GridProjectionComponent` (`WorldToCell`) through `EntityComponent.ResolveLive` on every query. The two marker wires resolve with `fallbackWhenEmpty: false`, so an unset path never adopts a scene-wide node.
- Calls `TerrainSpawnMarkers.Find` (`ecs/terrain/TerrainSpawnMarkers.cs`) to locate a start's marker by its `start_index` metadata, for `OriginOf` and for counting in `StartCount`.
- Reads [`GridFactionCatalog`](GridFactionCatalog.md) — `IndexOf`, `IdOf`, `ColourOf`, `PlayableAt`, `LockedStartOf`, `Count` — as a `Resource` on the export, not through a path. There is no node to resolve and no signal to connect: a catalog is authored map data.
- Binds `TerrainWorldComponent.WorldBuilt` through `WorldPath`, and unsubscribes in `_ExitTree`.
- Parses saved state through `GridVariantReader.Int` / `TryDictionary`, like every other grid saveable.
- Consumed by `GridPlacementComponent` (`HasCells`, `HasAreas`, `ActiveStartIndex`, `IsInArea`), `GridWorkerSpawnerComponent` (`FactionCatalog`, `ActiveStartIndex`, `StartIndexOf`, `SpawnCellsFor`), `TerrainWorldCameraComponent` (`ActiveStartIndex`), `GridMinimapComponent` (`ColourOfStart`, `AssignmentChanged`) and `TerrainMapOverlayComponent` (`ColourOfStart`, `AssignmentChanged`).

## Notes

- `SpawnCellsFor` returns the area's cells in order without considering units already standing on them. `GridWorkerSpawnerComponent` asks for every cell and skips the ones its live units occupy, which is what keeps successive workers on successive cells.
- The reservation is the generated reservation, not ownership. Nothing here changes when land is claimed during play — and the assignment is not ownership either: it says who *starts* where, and FEAT-02's territory layer takes over from there.
- Markers carry the origin; membership still comes from the cells. A native-profile map therefore answers `OriginOf` and reports `HasAreas` false at the same time — that is the expected shape, not a broken wiring. The published marker contract (`start_index`, `hq_footprint`, `unusable`) is specified in [the TileMapLayer output contract](../game-builder/TILEMAP_OUTPUT.md#spawns-implemented-feat-09feat-12).
- The marker's `hq_footprint` is not re-exposed here: `OriginOf` is the anchor, and a second accessor for the footprint would have no engine consumer. It is part of the published contract for whoever reads the scene.
- The collaborator paths all resolve live on every query, but `WorldPath` does not: `BindWorld()` runs once, from `_Ready`. Re-pointing `WorldPath` at runtime therefore does not move the `WorldBuilt` subscription (the method itself is written to be idempotent, so a future caller can).
- `_assignments` is keyed by **catalog index**, not by id — ids are the save format and the public vocabulary, indices are the in-memory key, and `GetAssignments` is the translation. That is why `RestoreState` against a reordered catalog works: it re-resolves every id through `IndexOf` on the way in.
- `tests/terrain_start_area_play_probe.gd` covers `OriginOf` against the generator's starts and past the last start (`NoCell`), the `SpawnCellsFor` order and blocked-cell skipping, and the placement and spawner consumers.
- `tests/terrain_spawn_markers_probe.gd` (FEAT-12) covers the marker path: a generated world's markers read back as the generator's own start cells with **no** generator and **no** data layers wired, an authored map of two hand-built markers answering `OriginOf(1)` while reporting `HasAreas` false, and `NoCell` past the last marker.
- `tests/terrain_faction_assignment_probe.gd` (FEAT-10, registered as `[terrain-faction-assignment] OK`) covers every `Assign` refusal, `AutoAssign`'s locked-first rule and its determinism, `ActiveStartIndex` with and without a catalog, the save round-trip **and** a catalog reordered between save and load, the two-spawner case, and the overlay's colours. Its locked fixture pins `bravo` — second in the catalog — to start **3** rather than start 1 on purpose: locked to 1, catalog order would have given it start 1 anyway, and removing the locked pass produced an identical table, so the check passed against mutated code.
- `tests/addon_contract_scan.ps1` pins the eight declarations this component must keep (`public string Assign(`, `public int AutoAssign(`, `public int StartIndexOf(`, `public int FactionAtStart(`, `public Color ColourOfStart(`, `public int StartCount`, `AssignmentChangedEventHandler`, `ISaveable`) under its `# FEAT-10:` block. The pins are substring matches on the file text, so they catch a member being deleted or renamed, not a signature quietly changing shape. Nothing pins the `SaveKey` literal or the `StartAreaPath` wiring of the consumers — those are covered by the probe, not by the scan.
