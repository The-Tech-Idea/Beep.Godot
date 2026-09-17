# What RTS and colony games put on a map — research and design (2026-09-15)

**Why this exists.** The terrain engine generates a fair-looking world and chooses start tiles, but a
game built on it cannot say "this ground is yours", "build here, not there", "this is where player
two begins", or "nobody has seen this yet". The owner's example was the first of those: most RTS
and colony games reserve a distinct area of the map for each player. This document records what
the reference games actually do — from their code where it is public — what our engine lacks, and
the rules the FEAT-09 … FEAT-14 plans adopt. It is the research behind
[the 2026-09-15 plan set](../../plans/terrain-grid/README.md#terrain-rendering-review-and-map-gameplay-features-2026-09-15);
fog of war and territory were designed earlier (FEAT-02, FEAT-03) and are only cross-referenced.

**Method.** Web research restricted to RTS, colony/city-builder and 4X games, as the owner asked.
Sources are ranked: **[code]** — an open-source engine, a shipped script library, or a patent that
describes the shipped algorithm (Ensemble's random-map patent, 0 A.D.'s `player.js`, Civilization V's
`AssignStartingPlots.lua`, Widelands, OpenRA's map format, openage); **[doc]** — official or
community documentation of shipped behaviour (Factorio wiki, Northgard, Timberborn, Anno, RimWorld
wikis); **[design]** — design articles (StarCraft II level design, Wayward Strategy, Warcraft III
melee guidelines). Where a page could not be fetched directly (some wikis block automated readers)
the finding rests on search summaries and is marked. Our own code was read on 2026-09-15; every
file:line below was verified against that tree.

---

## 1. Player start areas are carved out first, then filled identically for every player

1. **Age of Empires — Ensemble Studios patent US7589742B2 [code].** The map "is divided into a
   number of areas, so that each player starts in a separate area"; each area is a fraction of the
   map (the example gives 60 of 240 tiles for four players) and must be a **contiguous blob** of
   tiles. Constraints: a **minimum distance between player areas**, an optional **shape** (rectangle,
   pie wedge of a circle) and the exclusion of "undesired or unusable areas (e.g., impassable
   mountains, water features)". Objects are then placed by a nested loop — *per player area, per
   object type* — where each object has **minimum and maximum distance from a point**, **types of
   other objects to avoid**, and a **critical** flag: a critical object that cannot be placed after
   N attempts has its constraints **relaxed** through predefined, sorted relaxed versions; a
   non-critical one falls back to a **spiral closest-point search** and is dropped after a scripted
   number of tiles. Equity is "the same object set for every player at matched distances", scaled
   by player count ("at least three mineral mine resources for every player"). Neutral objects go
   "between player areas or at a predetermined minimum distance away from each area".
2. **Age of Empires II random map scripts [doc].** `create_player_lands` takes `terrain_type`,
   `land_percent`, `base_size`, `number_of_tiles`, `other_zone_avoidance_distance`,
   `border_fuzziness` and `top/bottom/left/right_border`; "land origins are placed in order, and
   after all origins are placed, all lands **grow simultaneously** from their origins outwards …
   until they run into a border or another land". Per-player objects use
   `set_place_for_every_player`, `min_distance_to_players` / `max_distance_to_players` ("square
   distances, with the exception of `find_closest`"), `set_gaia_object_only`, `find_closest`,
   `set_scaling_to_map_size`, `avoid_forest_zone`, `set_circular_placement`.
3. **0 A.D. `binaries/data/mods/public/maps/random/rmgen-common/player.js` [code].** The
   concrete starting kit around every civic centre: base radius `defaultPlayerBaseRadius() =
   scaleByMapSize(15, 25)` tiles; placement order `["CityPatch", "Trees", "Mines", "Treasures",
   "Berries", "StartingAnimal", "Decoratives"]`; **trees at 11–13 tiles**, **stone and metal mines
   at 12** (angles π/6 to π/3 apart), **berries at 12**, **starting animals at 9** (two groups),
   **decoratives at 8–11**; base resources avoid the civic-centre disc
   (`avoidClasses(BaseResourceClass, 4)`, a 5-tile disc marked by `addCivicCenterAreaToClass`).
   Random placement enforces "minimum distance between initial bases must be a quarter of the map
   diameter" and a border distance of `fractionToTiles(0.08)`. Layouts: `playerPlacementCircle`,
   `placeLine`, `placeStronghold`, `playerPlacementRiver` (two parallel lines), `playerPlacementArcs`
   ("teammates placed on the same arc"); `groupPlayersByArea` and `partitionPlayers` keep allies
   together.
4. **Factorio [doc, wiki.factorio.com/Map_generator].** The starting area is "an almost circular
   radius around the spawn point that does not contain enemy bases"; the generator "makes sure that
   there is always at least one patch of coal, iron ore, copper ore, and stone each"; "uranium ore
   and crude oil do not spawn in the starting area"; "there is always a lake in the starting area,
   even when water is turned off, and there are never any cliffs there"; resource richness and enemy
   base size/frequency **increase with distance** from spawn. The size setting does one thing:
   "pushes the bases further out".
5. **Warcraft III melee guidelines [design, Hive Workshop].** The start gold mine "should not be
   open from all sides"; "creeps should be added to all start positions (except for 1v1 maps) so
   nobody gets a free expansion"; weak creeps near bases, strong ones in the middle.

## 2. Fair placement: partition by fertility, score, then normalise the weak starts

6. **Civilization V `AssignStartingPlots.lua` [code].** `MeasureStartPlacementFertilityOfLandmass`
   feeds a **recursive division of each landmass into regions of equal fertility**; each civilisation
   gets a region and a start plot inside it; **normalisation** "props up weaker locations" by adding
   food, production and bonus resources and removing bad features; luxuries and strategics are
   distributed afterwards with distance rules; starting biases (coast, river, hills, desert, …)
   steer the choice.
7. **Civilization VI [doc].** Major civilisations are spread first, city-states inserted in the
   margins; distance parameters **9 tiles civ–civ, 7 civ–city-state, 5 city-state–city-state**; the
   "Balanced" start option "narrows the range of starting location quality so everyone starts on
   more equal footing, though this usually brings down the ceiling".
8. **Advanced Civilization v0.98 (Civilization IV mod) [code, CivFanatics].** "Starting position
   iteration": each site is scored (found-city value, expansion space, rival distance, volatility),
   then civilisations are **swapped one or two at a time to reduce the average error** between
   start values (167 → 97 permille on the demonstration map). The author notes the limit: absolute
   space thresholds are not enforced, so a tight continent can still be unplayable.

## 3. Territory is a per-cell owner, set by buildings with a radius; you build only on your land

9. **The Settlers II / Widelands [code — Widelands is open source; page bot-gated, summarised].**
   "You can only build within your territory, and territory is expanded by building military
   buildings"; occupied military buildings **conquer land in a radius** that grows with the building;
   "as long as you are the first to have military influence over an area you'll own it"; losing
   influence loses the land (buildings on it burn); a lookout tower gives **vision without
   territory**.
10. **Northgard [doc, wiki summaries].** The map is pre-divided into **tiles (zones)** with a
    **building capacity of 2–4** and fixed resources; colonising requires the tile to be "adjacent to
    your territory, explored by you or an allied clan, and neutral", and costs food that **rises
    per tile owned**; the starting tile holds the town hall and is larger.
11. **Anno 1800 [doc].** An island belongs to whoever builds the trading post; a building's
    "range" is a **street distance** (paved streets +50 %), not a radius.
12. **Timberborn [doc, wiki.gg].** A district centre's range is **70 path tiles**; "buildings built
    beyond the district range … cannot be used by any beaver"; the range is drawn as a green→red line
    along the paths when the centre is selected.

## 4. Colony sims use player-drawn zones, and the home area grows itself

13. **RimWorld [doc, rimworldwiki.com].** *Zones* — stockpile, growing ("growing zones can only be
    placed on cells of fertile terrain") — and *areas* — home, allowed, roof, snow-clear. "By
    default, Home area is automatically added around any player-created structure, stockpile zone
    or growing zone"; it is where colonists clean, repair and fight fires. "An allowed area restricts
    where colonists can perform work and joy activities, but it does not restrict them from pathing
    outside an allowed area". Raids arrive from the map edges.
14. **Banished, They Are Billions, Frostpunk [doc].** One central start, expansion outward, danger
    from the edges; a "good seed" in Banished is a valley with a lake beside the start.

## 5. Competitive layouts are symmetric, legible, and stored as map data

15. **StarCraft II [design; Liquipedia terrain features].** A main base holds "two Vespene Geysers
    and eight Mineral Fields" on high ground behind one ramp; a natural just outside; a third; chokes
    and ramps limit how many units enter at once; **rotational symmetry for two-player maps, mirror
    symmetry for four**; a single watchtower at the centre; rich resources only in contested
    expansions; equal doodad density so movement speed matches; map sizes **120–140 for 1v1 …
    160–190 for 4v4**.
16. **Wayward Strategy [design].** Expansions "draw players closer together over game time";
    "too few attack paths" produce stalemates; contested resources sit between bases; "the third
    base is incredibly vital".
17. **OpenRA map format [code, GitHub wiki].** A map declares `Bounds` — "left, top, width, height
    in map-cell coordinates" — with "a 1-cell cordon … required on each side between the visible
    area and the edge of the map"; `Players` are `PlayerReference` nodes with `Name`, `Faction`,
    `Playable`, `AllowBots`, `Team`, `Allies`/`Enemies`, `Color`, `LockFaction`/`LockTeam`/`LockSpawn`;
    spawn points are `mpspawn` actors assigned through `Spawn` ("0: random, >= 1 for spawn >= A");
    starting cash and units are per-player traits (`PlayerResources`, `SpawnMPUnits`). Player
    definitions and spawn markers are **map data**, not engine constants.

## 6. Fog of war: three states, one visibility texture

18. Tile fog is universally **unexplored / explored / visible** per cell, rendered through **one
    visibility mask texture** sampled by the terrain shader or an overlay quad (Godot and Unity tile
    implementations; AoE II ships `blkedge.dat` fog-edge masks per elevation level). Already designed
    here as [FEAT-03](../../plans/terrain-grid/FEAT-03-fog-of-war-and-exploration.md) — not repeated.

---

## 7. What ours lacks (read 2026-09-15)

- **Start tiles, not start areas.** `TerrainStartPositionStage.cs` judges one cell (`:89-91`:
  not water, not Mountains, `Startable`) by a radius-3 score (`:142-200`: food by kind, production by
  relief, fresh water ×0.8, sea access ×0.35), takes greedily with
  `minimumSeparation = max(4, min(w,h)/max(2,n)*1.6)` (`:67`), one start per continent first
  (`:72-73`). Nothing checks a building footprint, exits, reachability or a resource within reach;
  `TerrainResourceStage.cs` scatters uniformly (`:39` density 0.085, `:49` spacing 4) with no
  start-relative guarantee. The workflow review's E01 records this gap
  (`plans/GAMEAPP_GRID_TERRAIN_WORKFLOW_REVIEW.md:359-362`).
- **Starts die at the field boundary.** The generation→cells handoff
  (`TerrainGeneratorComponent.cs:264-286`) carries terrain, feature, relief, shade, elevation, water
  source, water patch, inland terrain and shore widths — not starts or continent ids — so
  `CellRecord` (`GridCellDataComponent.cs:601-618`) has no start fact and `GridWorldStateComponent`
  cannot save one; only the seed is saved (`TerrainWorldComponent.cs:429-448`). Engine consumers read
  `starts[0]` only (`TerrainWorldComponent.Drawing.cs:279-297`, `TerrainWorldCameraComponent.cs:149`);
  the overlay draws one cream ring per start (`TerrainMapOverlayComponent.cs:236-244,354-358`);
  `TerrainDataLayersComponent` keeps starts in a `HashSet` (`:53`) and reads the materialised layer's
  `GetUsedCells()` (`:391-395`), neither index-ordered.
- **No owner, zone or player in the grid.** `CellFlags` are workflow bits (Blocked, Cleared,
  Tilled, Watered, Planted, HarvestReady); `TerrainChangeKind` has no territory kind;
  `GridPlacementComponent.WhyNot` (`:347-377`) knows six refusals and none about owners or areas;
  `GridObjectComponent` has no `OwnerId`. Players exist for actors only
  (`ecs/actors/PlayerContextComponent.cs:13-15`, `ActorRegistryComponent.cs`), reaching the grid at
  two points (`GridJobQueueComponent.OwnerId`, `GridWorkerSpawnerComponent.OwnerId`), with no faction
  catalog or colour. `GridWorkerSpawnerComponent` spawns everyone at one authored `SpawnCell + (i, 0)`
  (`:28,74`).
- **No symmetry, no cordon, no map-data spawns.** Nothing in `addons/` folds coordinates; the
  playable area is `GridNavigationComponent`'s bounds rectangle; the native map output contract
  (`docs/game-builder/TILEMAP_OUTPUT.md:19`) foresees a `Spawns` node of `Marker2D`s that nothing
  emits or reads.
- **Territory and fog are designed, not built** — FEAT-02 and FEAT-03 (proposed 2026-09-08).

## 8. Rules adopted (each names the precedent and the plan that carries it)

1. **A reserved start area is a generation stage, opt-in, deterministic** — one contiguous blob per
   start, all grown simultaneously from their origins, separated by a gap, excluding water,
   mountains and blocked kinds (AoE lands; AoE II simultaneous growth) → FEAT-09.
2. **The existing stage stays the only scorer**; the area stage never re-scores. Its one change is a
   footprint predicate in `Eligible` (the `not_level` rule placement already enforces) → FEAT-09.
3. **Every player gets the same kit at authored distances**, with the AoE critical-flag and
   relaxation-in-order rule, and a per-start report; **unusable seeds are reported, never rerolled**
   (E01) → FEAT-09. Civ-style normalisation beyond the kit and AdvCiv swapping are not adopted.
4. **Three facts, three owners:** `terrain_start_area` (generated, static, rides the handoff into the
   cells so it saves and regenerates), `Owner` (live, FEAT-02, seeded once from the reservation), the
   faction catalog / assignment (who a faction is and which start it was given) → FEAT-09/10.
5. **One start source for consumers** replaces the `starts[0]` copies; spawn markers (OpenRA
   `mpspawn`) and data layers feed it, cells give membership → FEAT-09/12.
6. **Consumers arrive with the feature**: placement refusal `outside_start_area`, per-player
   spawning, camera framing by index, overlay and minimap colour, status line → FEAT-09/10.
7. **Player definitions are data** (OpenRA `Players`): a `GridFactionCatalog` with index, colour,
   team and locked spawn, introduced once for FEAT-02, FEAT-03 and FEAT-10 → FEAT-10.
8. **Zones are named cell sets consumers query**, overlapping per kind (RimWorld), including a
   path-distance district (Timberborn) and a self-expanding home kind (RimWorld) → FEAT-11.
9. **A playable cordon is map data** (OpenRA `Bounds`) → FEAT-12.
10. **Symmetry is terrain symmetry** (SC2/WC3 fairness is the map, not the spawn), a generator-wide
    fold with its own probe; last, because every scan-order tie-break can break it → FEAT-13.
11. **The far country is richer and more dangerous** (Factorio) and **contested sites sit between
    areas** (AoE neutral objects): a distance-from-start field, a richness curve, neutral kit
    entries → FEAT-14. Enemy and raid spawning remain the game's, reading the field.
12. **Engine-owned vs game-owned:** the stage, the reservation and its save path, the kit mechanism,
    the catalog and assignment, placement refusals, spawn-cell search, camera, overlays, zones,
    markers and the distance field are engine; kit *content*, which build is the HQ, starting stock,
    lobby UI, AI, diplomacy, victory, raids and ladder maps are the game's.

## 9. Player lands first (2026-09-16)

**Why this section exists.** FEAT-09 adopted rule 2 above: starts are found in the finished terrain,
and areas are reserved around them afterwards. On an Oilfield Days capture the owner rejected that
order. The base pad sat where sand, a lake and grass met, and the terrain read as "too random". The
owner asked for a map where players are allocated first and everything else is generated around
them. This second pass read the games' own scripts and source to see how that is done. It is the
research behind [FEAT-15](../../plans/terrain-grid/FEAT-15-player-lands-first.md).

**Two families.**

- **Players first, terrain second: AoE2, AoM/AoE3, 0 A.D.** Place player origins, grow a territory
  per player, stamp a base core, and make every later generator avoid that core.
- **Terrain first, starts fitted afterwards: Civ V/VI, OpenRA, Widelands, Return To The Roots.**
  Build the world, then search it for fair starts. Unfairness is compensated with buffers,
  normalisation, or a thrown error when no room is left.
- **In between.** AoE4 places starts on a coarse grid and overwrites the ground around each with a
  buffer terrain. Factorio makes every noise field a function of distance to the start.

**The pipeline, in order, with what each step guarantees.** This is the synthesis; the games that
do each step are named.

0. **Size the map from the player count.** AoM: side = 2·√(players·7500/0.9) [community tutorial].
   AoE2 presets run from Tiny 120² for 2 players to Large 220² for 8. 0 A.D. uses
   `scaleByMapSize(128…512)`. *Guarantee:* roughly equal area per player.
1. **Origins before any terrain.**
   - Placement:
     - AoE2 `random_placement`, `circle_radius` ~40 % with variance ~20;
     - 0 A.D. `playerPlacementCircle(0.35)`;
     - AoM `rmPlacePlayersCircular`;
     - AoE4 `PlacePlayerStartsRing`.
   - Teams:
     - AoE2 `grouped_by_team`;
     - 0 A.D. `groupPlayersByArea`;
     - AoE4 `topSelectionThreshold`.
   - 0 A.D. random placement keeps players at least 0.25 × map size apart and 0.08 × map size from
     the border. It relaxes ×0.95 every 25 resets and gives up after 500.
   - *Guarantee:* spacing and team layout do not depend on terrain luck.
2. **A territory per player.**
   - AoE2 `create_player_lands`: `land_percent` split equally between players, all lands growing at
     once, `other_zone_avoidance_distance`, `set_zone_by_team`.
   - AoM: one area per player (`rmSetAreaSize` fraction, `rmSetAreaLocPlayer`).
   - Civ V's alternative is equal fertility rather than equal tiles.
3. **A base core, recorded as a mask.**
   - AoE2 `base_size` is a square of radius N: default 3 (7×7), Arabia 9 (19×19), Black Forest 13.
   - AoE4 `startBufferTerrain` `tt_plains` within `startBufferRadius` overwrites earlier terrain.
   - 0 A.D. CityPatch radius is `scaleByMapSize(15,25)/3`, plus `PlayerTileClass` (a radius-5 disk).
     The continent map guarantees a land blob of radius `scaleByMapSize(23,50)`.
   - Return To The Roots: buildable ground within radius 2 of the headquarters, flattened.
   - OpenRA: `MinimumSpawnRadius` 5 buildable, `SpawnBuildSize` 8 kept ore-free.
4. **Macro terrain around cores, never over them.**
   - AoE2: hills stay about 9 tiles from player origins and cliffs about 22 from land origins (both
     measured by the RMS guide's author, not stated officially). `create_connect_all_players_land`
     cuts connections.
   - 0 A.D.: hills, mountains and forests use `avoidClasses(clPlayer, 20)`.
   - Factorio: cliffs are suppressed near the start and elevation is raised so a flooded world still
     has land there. A starting lake is always made, and rocks are suppressed within ~35 tiles.
5. **Cosmetic terrain under filters. Beaches only at the shoreline.**
   - AoE2 `create_terrain` replaces only its `base_terrain`, and `beach_terrain` is drawn only where
     the new terrain touches water.
   - 0 A.D. paints shore by height band, and its dirt and grass patches avoid players by 12.
   - Widelands and OpenRA also keep coast terrain at the waterline.
6. **The same start kit for every player, most important object first.** AoE2
   `set_place_for_every_player` with `min/max_distance_to_players`. 0 A.D. places in fixed order:
   CityPatch → Trees → Mines → Treasures → Berries → StartingAnimal → Decoratives.
7. **Measure and top up** (Civ V `NormalizeStartLocation`; Civ VI adds food and production below a
   threshold).
8. **Neutral resources and hazards away from bases.**
   - AoE2: extra gold and stone at `min_distance_to_players 40`.
   - 0 A.D.: mines avoid players by `scaleByMapSize(20,35)`.
   - Factorio: richness grows with distance, and enemy bases are suppressed inside the starting area
     and scale out to 2400 tiles.
9. **Decoration last.** Return To The Roots keeps trees and stone piles out of radius 5 around the
   headquarters. 0 A.D. decoratives avoid players by 10.
10. **Validate and fail loudly.**
    - Do this: OpenRA throws "Not enough room for player spawns" and the lobby offers a reroll.
      Return To The Roots retries 10 times, then throws.
    - Anti-patterns: AoE2 silently skips an unplaceable object; Widelands only logs a warning.

**Visual scale.**

| Game | Texture repeat | Units and buildings |
|---|---|---|
| AoE2 HD | One 512 px texture spans 10×10 tiles (~51 px per tile) | Villager 0.4 tile wide; house 2×2, town centre 4×4 |
| 0 A.D. | 8 tiles per repeat by default, rotated 45° | Infantry radius 1.5 m; civic centre 32 m, one repeat |
| Factorio | Tile variants 1×1, 2×2 and 4×4 | Character 0.4 tile; car 1.4×2 |

In all three, tufts, rocks and bushes are separate decorative objects, not painted detail. The rule of
thumb that follows:

- A texture repeat is at least the largest common building footprint.
- Repetition is broken by rotation or by variants of several sizes.
- Painted detail stays a few texels across, relative to a unit 0.4 of a tile wide.
- Anything with a silhouette is a decorative object sized in world units.

**Could not be verified.**

- AoE4's official pages block automated readers; its template values come from an unofficial copy.
- AoM's map-size formula comes from a community tutorial.
- The 10-tile texture span for AoE2 DE comes from a wiki excerpt.
- Civ VI values come from community copies of the shipped file.
- The patent was read through a summary.
- Northgard, Stronghold Crusader and Rise of Nations are undocumented.

## Sources

- AoE2 Definitive RMS Guide (Zetnus) — https://docs.google.com/document/d/1jnhZXoeL9mkRUJxcGlKnO98fIwFKStP_OBozpr0CHXo/mobilebasic
- AoE2 HD Arabia.rms — https://github.com/Naramsim/AoE2-random-map-scripts/blob/master/The%20forgotten/Arabia.rms ; AoC Black_Forest.rms — https://github.com/Naramsim/AoE2-random-map-scripts/blob/master/The%20conquerors/Black_Forest.rms
- AoE4 unofficial API, generate maps — https://callfreak.github.io/aoeiv-unoffical-api/docs/generate-maps
- AoM random map API — https://mythicfreak.github.io/aomcodereference/aom/scripting/xs/rm/Player.html ; AoM Heaven RMS tutorial — https://aom.heavengames.com/cgi-bin/forums/display.cgi?action=st&fn=19&tn=28148
- 0 A.D. `mainland.js` — https://github.com/0ad/0ad/blob/master/binaries/data/mods/public/maps/random/mainland.js ; `continent.js` — https://github.com/0ad/0ad/blob/master/binaries/data/mods/public/maps/random/continent.js ; `rmgen2/setup.js` — https://github.com/0ad/0ad/blob/master/binaries/data/mods/public/maps/random/rmgen2/setup.js ; terrain texture code — https://github.com/0ad/0ad/blob/master/source/graphics/TerrainTextureEntry.cpp
- Civ VI `AssignStartingPlots` (community copies) — https://github.com/Gedemon/Civ6-YnAMP/blob/master/Override/AssignStartingPlots.lua
- Factorio data: `resource-autoplace.lua`, `noise-functions.lua`, `noise-programs.lua` — https://github.com/wube/factorio-data/tree/master/core ; FFF-258 — https://factorio.com/blog/post/fff-258
- OpenRA `ClassicMapGenerator.cs` — https://github.com/OpenRA/OpenRA/blob/bleed/OpenRA.Mods.Common/Traits/World/ClassicMapGenerator.cs ; RA generator defaults — https://github.com/OpenRA/OpenRA/blob/bleed/mods/ra/rules/map-generators.yaml
- Widelands `map_generator.cc` — https://github.com/widelands/widelands/blob/master/src/editor/map_generator.cc
- Return To The Roots map generator — https://github.com/Return-To-The-Roots/s25client/tree/master/libs/s25main/mapGenerator
- AoE2 HD texture span — https://github.com/e00E/Age-of-Empires-II-Grid-Generator/issues/2 ; AoE2 unit data — https://github.com/HSZemi/aoe2dat
- Ensemble Studios / Microsoft, US7589742B2 "Random map generation in a strategy video game" — https://patents.google.com/patent/US7589742B2/en
- Age of Empires II random map scripting, Steam guide — https://steamcommunity.com/sharedfiles/filedetails/?id=155256742
- 0 A.D., `rmgen-common/player.js` — https://github.com/0ad/0ad/blob/master/binaries/data/mods/public/maps/random/rmgen-common/player.js
- Civilization V `AssignStartingPlots.lua` (mirror) — https://github.com/Gedemon/Civ5-Culturally-Linked-Start-Location/blob/master/ASP_Vanilla.lua
- Civilization VI start distances — https://steamcommunity.com/app/289070/discussions/0/1700542332326124787/
- Advanced Civilization v0.98 start iteration — https://forums.civfanatics.com/threads/660888/
- Factorio map generator — https://wiki.factorio.com/Map_generator
- Widelands military and territory help — https://www.widelands.org/wiki/GameHelpmilitaryAndWarfare/ (bot-gated; via search summary)
- Northgard tiles and colonisation — https://northgard.fandom.com/wiki/Colonization (via search summary)
- Anno 1800 building distance — https://steamcommunity.com/app/916440/discussions/0/596288191849318573/
- Timberborn district centre — https://timberborn.wiki.gg/wiki/District_Center
- RimWorld zones and areas — https://rimworldwiki.com/wiki/Zone/Area, https://rimworldwiki.com/wiki/Home_area, https://rimworldwiki.com/wiki/Allowed_area
- StarCraft II level design — https://code.tutsplus.com/starcraft-ii-level-design-introduction-and-melee-maps--gamedev-3304t; Liquipedia terrain features — https://liquipedia.net/starcraft2/Terrain_Features
- Wayward Strategy, multiplayer map design — https://waywardstrategy.com/2015/06/07/time-as-a-resource-part-2-multiplayer-map-design/
- Warcraft III melee map making — https://www.hiveworkshop.com/threads/how-to-create-melee-maps.187652/
- OpenRA Map-Format — https://github.com/OpenRA/OpenRA/wiki/Map-Format
- Fog of war on tile maps — https://github.com/miquelqg99/fog-of-war; https://godotshaders.com/shader/hexagon-tilemap-fog-of-war/
