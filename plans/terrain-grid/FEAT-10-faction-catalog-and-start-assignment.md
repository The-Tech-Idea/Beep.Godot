# FEAT-10 — Faction catalog and start assignment: the same start for the same player everywhere

**Type:** feature (genre precedent: OpenRA `Players` definitions with `Spawn` index and `LockSpawn`; Civilization's one region per civ; Age of Empires' identical per-player kit) · **Area:** `ecs/grid/GridFactionCatalog.cs` + `GridFactionDefinition.cs` (new Resources), `ecs/grid/GridStartAreaComponent.cs` (FEAT-09), `GridWorkerSpawnerComponent.cs`, `ecs/grid/ui/GridMinimapComponent.cs`, `ecs/terrain/TerrainMapOverlayComponent.cs`, `ecs/actors/PlayerContextComponent.cs` (read only) · **Status:** Implemented 2026-09-16 · **Effort:** M (3–4 days) · **Risk:** medium (shared with FEAT-02 and FEAT-03 — the catalog must be introduced once, in whichever lands first)

## As landed (2026-09-16)

Built as designed, with these differences:

- **The overlay takes a start component, not a catalog.** The design said the overlay gets a
  `FactionCatalogPath` that overrides FEAT-09's fixed palette. A catalog alone cannot answer the
  question the overlay asks: it maps a faction to a colour, and only the assignment knows which
  faction holds start k. Reading `ColourOf(k + 1)` would have assumed catalog order IS start order,
  which is exactly what `LockedStart` breaks - and it would have been a second, divergent copy of
  the resolution the minimap does. The export is `StartAreaPath`, and both views ask
  `GridStartAreaComponent.ColourOfStart(k)`. A contract pin refuses either view calling `ColourOf(`
  at all.
- **The fixed palette stays, and is not a fallback.** `StartColour` returns the faction's colour when
  a faction holds the start and the palette otherwise. That is the answer on a map with no factions -
  most maps - and it is why an unassigned start still reads as a distinct start rather than as
  nothing. `ColourOfStart` answers transparent for a start nobody holds, so a caller tints nothing
  rather than inventing faction 1's colour.
- **The spawner resolves its OWN owner, not the local player.** `SpawnAtStartArea` read
  `ActiveStartIndex`, which is this machine's player. A map with a spawner per player would have put
  every player's units in the local player's area. It now resolves `OwnerId → FindPlayer().FactionId
  → StartIndexOf`, and falls through to `ActiveStartIndex` when there is no catalog, no registry or
  no owner - a single-player sandbox has one player and one start, and that is what it means. It
  refuses with `owner_has_no_start` only when the owner is a real player whose faction was assigned
  nothing, because any other area belongs to somebody else.
- **`AssignmentChanged` was needed.** Both views bake their colours (the minimap into a texture, the
  overlay into segments), so a start changing hands has to re-bake, not merely redraw. The signal is
  emitted by `Assign`, `AutoAssign` and `RestoreState`, and both views connect to it. `Assign`
  re-assigning a faction to the start it already holds emits nothing.
- **`AutoAssign` seats locked factions first.** The design said "maps catalog order to start order".
  Taken literally, a faction locked to start 3 would be displaced by whatever the catalog happened to
  list before it. Locked factions are seated first, then the rest in catalog order onto the starts
  still free; a lock this map cannot honour warns and leaves that faction unassigned rather than
  silently moving it.
- **`StartCount` was needed** so `Assign` can refuse a start the map does not have. It reads the
  spawn markers when `SpawnsRootPath` is wired (FEAT-12) and the data layers otherwise - the same
  two records `OriginOf` already chooses between, so there is no third opinion about how many starts
  a map has.
- **The save is keyed by faction id, and refuses rather than dropping.** A catalog edited between
  save and load moves every index, so an index-keyed table would hand a player someone else's start
  with nothing reporting it. A snapshot version mismatch throws, as `GridWorldStateComponent` does;
  a save holding assignments with no catalog wired to read them against throws rather than loading
  an empty table, which would have put every player on start 0.
- **`WorldBuilt` does not re-deal a table that already fits the world.** The design says the save is
  "restored lazily so load order is free"; it is not, and an unconditional deal on `WorldBuilt` made
  that worse in three silent ways. Saveables load in group order, so this component's `Load` can run
  **before** the world is restored - and the `RestoreWorld` that follows would then throw the saved
  assignment away and deal a fresh one. `Redraw()` re-emits `WorldBuilt` too, so a lobby's manual
  `Assign` calls would not survive a view switch. The rule is now: deal when the table is empty, or
  when it holds a start this world does not have (with a warning, because that discards somebody's
  choices); otherwise leave it, whoever made it. Load order is genuinely free under that rule, in
  both directions. **Found by reading the landed code against the plan, after the first guards were
  already green** - none of them exercised a `WorldBuilt` arriving after a restore.
- **`WorldPath` re-points its subscription.** `BindWorld` ran only in `_Ready`, so a path edited
  afterwards kept listening to the old world while reading the new one's starts. It is a property
  with a setter now, as `TerrainWorldCameraComponent` does it.
- **`TerrainGeometry.MostCommon` became generic.** The minimap needed the same majority vote over
  start indices that it already ran over terrain kinds. A second vote written beside it would be a
  downsampled block picking its terrain by one rule and its owner by another. Existing call sites are
  unchanged (`TKey` infers to `string`); the contract pin was updated to the generic signature.

**Guards:** `tests/terrain_faction_assignment_probe.gd` (headless, registered) and a new section in
`tests/GridMinimapSmoke.cs` (run by `tests/grid_minimap_probe.ps1`).

- Every `Assign` refusal has its own reason: `no_faction_catalog`, `unknown_faction`,
  `start_out_of_range` (both ends), `start_taken:<id>` naming the holder, `start_locked:<n>` naming
  the lock. `AutoAssign` seats the locked faction on its own start, skips the unplayable one, and
  gives the same table twice.
- The assignment survives `CaptureState`/`RestoreState`, **and** survives a catalog whose order
  changed between the two.
- Two spawners owned by two registered players, with a two-faction catalog, land in their own
  factions' areas (`StartAreaAt(cell) == StartIndexOf(faction) + 1`) on different cells.
- The overlay draws each start's border in the colour of the faction holding it, and still colours
  starts from its own palette with no start component wired.
- A minimap texel inside a start area equals the plain ground tinted 20 % toward that start's
  faction; unreserved ground is untouched; a start nobody holds is not tinted.
- A `WorldBuilt` leaves an assignment that fits the map alone, and re-deals one holding a start the
  map does not have.

**Mutations**, each failing its own check: the spawner reading `ActiveStartIndex` (both players
landed on one cell); the overlay ignoring the assignment; the save keyed by index (the reordered
catalog handed factions each other's starts); `AutoAssign` not seating locked factions first;
`Assign` dropping the lock check; and the unconditional re-deal on `WorldBuilt`. Renaming
`ColourOfStart` and pointing the minimap at the catalog raised three of the new contract pins
(19 against the 16 pre-existing).

**One guard could not fail at first.** The locked-start fixture originally locked `bravo` - second in
the catalog - to start 1, which is the start catalog order would have given it anyway, so removing
the locked pass produced an identical table and the check passed against mutated code. The fixture
now locks it to start 3. This is the failure mode the mutation discipline exists for: the check was
green, correct-looking, and proving nothing.

**Blind spot found in a new pin.** The pin refusing a view to read the catalog's colour directly was
written as the literal `FactionCatalog.ColourOf(` and slid straight past
`_startArea!.FactionCatalog!.ColourOf(` - a null-forgiving operator was enough to defeat it. It is
now pinned on the bare `ColourOf(` call, which `ColourOfStart(` does not contain.

**Not built here:** AI factions, lobby UI, per-player starting stock, and `Team` behaviour - `Team`
is data the engine reads nowhere, as designed. `AutoAssign` seats starts in index order without
consulting FEAT-09's `Usable` report; a scenario that must avoid an unusable start locks its
factions. Whether an unusable start should be skipped is left open rather than guessed at.

## Gap

Players exist only for actors. `PlayerContextComponent` carries `PlayerId` and `FactionId` as strings (`ecs/actors/PlayerContextComponent.cs:13-15`); `ActorRegistryComponent` keeps string-keyed hostility (`_hostilities`, `:40`; `SetHostile`/`AreHostile`, `:111-119`), owned actors (`GetOwnedActors`, `:95-97`) and rejects commands from a non-owner (`:134, 141`). The grid touches this model at exactly two points, and neither knows a colour, an index or a start: `GridJobQueueComponent.OwnerId` (`:44`) filters which worker may claim (`CanWorkerClaim`, `:634-636`), and `GridWorkerSpawnerComponent.OwnerId` (`:20`) stamps spawned actors (`:123-130`). `GridWorkerSpawnerComponent.SpawnCell` (`:28`) is one authored cell for every owner. No faction colour table exists; FEAT-02 needs a byte per cell for `Owner`, FEAT-03 a faction index for its bit planes, the minimap a colour — and both plans defer a `GridFactionCatalog` to whichever lands first. OpenRA keeps player definitions (`Name/Faction/Playable/AllowBots/Team/Allies/Enemies/Color/LockFaction/LockTeam/LockSpawn`) and the spawn assignment as map and session data; AoE gives every player an identical kit; Civilization gives every civ its own region.

## Design

1. **`GridFactionCatalog : Resource`** — `Factions : Array<GridFactionDefinition>`; a definition has `FactionId` (normalised through `GridIds`), `DisplayName`, `Colour`, `Playable`, `Team`, `LockedStart` (-1 = free). Index = position + 1, a byte — the same value FEAT-02 writes to `Owner` and FEAT-03 uses as its plane index. `IndexOf(id)`, `IdOf(index)`, `ColourOf(index)`. `PlayerContextComponent.FactionId` stays a string and resolves through the catalog; the registry's hostility model is untouched.
2. **Assignment** lives on `GridStartAreaComponent`: exports `FactionCatalog`, `LocalFaction`; `Assign(factionId, startIndex)` rejects out-of-range, double-booking and `LockedStart` violations with a reason; `AutoAssign()` maps catalog order to start order, `Playable` only, deterministically; `StartIndexOf(factionId)`; `ActiveStartIndex` becomes `StartIndexOf(LocalFaction)` when a catalog is wired, else `LocalStartIndex`. `ISaveable` under `grid_world.start_assignment` (`{version, assignments: {id: index}}`), restored lazily so load order is free. `AutoAssign` runs on `WorldBuilt` when `AutoAssignOnBuild` (default true — meaningful only once a catalog is wired, so sandboxes are unchanged).
3. **Per-faction consumers.** Spawner: with `SpawnAtStartArea` (FEAT-09) and an `ActorRegistryPath`, resolve `OwnerId → FindPlayer(OwnerId).FactionId → StartIndexOf` (`ActorRegistryComponent.cs:93`), so a spawner per player spawns in its own area. Minimap `ShowStartAreas` (default false) + `StartAreaPath`: the terrain bake (`BakeTerrain`, `GridMinimapComponent.cs:317`, which already reads `_cells`) tints texels with `terrain_start_area > 0` 20 % toward `ColourOf` — one `TintToward` helper that FEAT-02's owner tint reuses. Overlay `FactionCatalogPath` overrides FEAT-09's fixed palette. Objectives: none new — FEAT-02's `own_cells` is fed by the initial claim; "reach your start" is true at spawn.
4. **Bridge to FEAT-02, stated here so it is built once:** `GridTerritoryComponent.ClaimStartAreasOnStart` (in FEAT-02's change) fills `Owner` from `terrain_start_area` through `StartIndexOf`; afterwards the two facts diverge legitimately as land is claimed and lost.

## Guards (fail first)

- `Assign` rejects double-booking and a `LockedStart` violation with reasons; `AutoAssign` yields the same table on two runs; the assignment survives a save round-trip.
- Two spawners (`OwnerId = player_1 / player_2`) with two registered players and a two-faction catalog: `StartAreaAt(spawnCell) == StartIndexOf(faction) + 1` for each. **Mutation:** ignore the assignment → both spawn in area 1.
- Minimap texel inside area 2 equals the terrain colour lerped 20 % toward faction 2's colour; off → the plain colour. **Mutation:** skip the tint → equals the plain colour.
- Overlay edge colour for index 2 equals `ColourOf(2)` when the catalog is wired.

## Dependencies / collisions

FEAT-09 (the component and the cell key). FEAT-02 (`Owner` byte semantics, the tint helper, `ClaimStartAreasOnStart`) and FEAT-03 (plane index): the catalog is introduced here or in whichever of them lands first, never twice. ENH-13 (the minimap bake). The actors layer is unchanged.

## Out of scope

Engine-owned here: the catalog resource, the assignment table and its save, the per-faction spawn, tint and palette hooks. Game-owned: diplomacy and team behaviour (`Team` is data only; hostility stays in the registry), lobby UI, AI factions, per-player starting stock.
