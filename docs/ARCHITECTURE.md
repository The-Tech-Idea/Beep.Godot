# Beep game-builder addon — architecture

How the pieces fit, and the three rules that decide where a new one goes.

## The three rules

1. **One owner per fact.** Every fact — what turn it is, what day it is, how long a build takes —
   is owned by exactly one thing. A second owner is a defect, not redundancy, because the two
   copies drift and nothing fails while they do.
2. **Composition, not inheritance.** Behaviour is a component you attach. A base class exists only
   where several components genuinely share the same machinery, and even then the shared piece is
   usually a helper held by composition (`GridWorkClockBinding`) rather than a base.
3. **Contracts, so anything can be replaced.** An extension point is an interface *plus* a
   duck-typed port that reads the same members by name. The interface serves C#; the port lets a
   GDScript node, or a plain authored Dictionary, answer the identical contract. Nothing in the
   engine may require a participant to be a C# type.

## The game master

```
GameApp : Node                      — the ONLY autoload (/root/GameApp)
├─ Info     : GameInfo (Resource)   — static config, incl. the declared TimeAxis
├─ Clock    : GameClock             — the one heartbeat, both time axes
├─ Settings : SettingsComponent     — audio, display, language
├─ Locale   : LocalizationComponent — translations
└─ Saves    : GameStateManagerComponent — save slots, ISaveable discovery
```

`GameApp` builds these as children in `_EnterTree`, in that order, before anything else's `_Ready`
runs, and hands them out by typed accessor (`GameApp.Instance.Clock`). This is the shape Widelands'
`Game` uses (`cmdqueue_`, `rng_`, `savehandler_`, reached through `game.cmdqueue()`) and Return to
the Roots' `Game` (`ggs_`, `em_`, `world_`).

They used to be five independent autoloads with five `static Instance` properties, two registered
conditionally. That made **the shape of the tree carry configuration** — and a component asked "is a
`TurnManager` in the tree?" to decide whether the game was turn-based. One owner, one construction
order, nothing inferred.

There is exactly one `static Instance` in the addon, and it is `GameApp`'s.

## Time: one clock, two axes

A real-time game and a turn-based game are not different kinds of time. They are the same discrete
step with different triggers — Age of Empires' lockstep advances its simulation in discrete *game
turns*, Return to the Roots advances one game frame per `RunGF()`, Widelands schedules work on a
command queue by due time. Real-time is a fast automatic driver over a discrete step.

So `GameClock` counts **beats**, and only the driver differs:

| Axis | What advances a beat | One beat is |
|---|---|---|
| `Realtime` | the clock's own `_Process` | one second of scaled game time |
| `Turns` | something calling `EndTurn()` | one turn |

Because a game has exactly one axis, a duration needs no unit tag: the same authored `5` is five
seconds or five turns depending on the game it is in.

**Consumers never branch on the axis.** `GameClock._Process` is the only place in the addon that
tests which axis it is on. Everything durational subscribes to `Advanced(beats)` and advances by
that amount — `StatsComponent` ticks modifier durations, `WorkComponent` burns work. That branching
used to live in every consumer, which is why a genre that declared turns and shipped no driver
(strategy did) froze every duration in the game, silently.

**Larger units cascade; they are never second clocks.** `Day` derives from beats through
`BeatsPerDay`, the way OpenTTD's date derives from `date_fract`, and the way Freeciv advances the
calendar year inside `end_turn`. A turn-based game sets `BeatsPerDay = 1`, so one end-turn is one
day; a real-time game sets it high, so a day passes smoothly.

The axis is **declared**, in `GameInfo.TimeAxis`, and read in exactly one place — `GameApp.ReconfigureClock`,
called when the master builds its subsystems and again by whatever rewrites `GameInfo` after that (a
`BeepGenreScene` applying its genre's tuning at `_Ready`). The clock follows the declaration, always;
it used to keep the axis it was built with while `GameInfo` said something else. A turn-based game
needs a `TurnDriverComponent` (or anything else calling `EndTurn`) or its clock never advances; the
clock reports a refused `EndTurn` rather than pretending.

**Pause is the tree's.** `SceneTree.Paused` is the one pause fact and `GameApp.SetPaused` the one
door that writes it (and announces `GamePaused`/`GameResumed`). `GameApp.IsPaused` is a view of that
flag, never a stored copy. The real-time clock stops for free — it is a pausable node — and `EndTurn`
reads the same flag, so a turn cannot be ended from under the pause menu. The clock carried a pause
flag of its own that nothing set, and turns went straight through.

**The clock is saved by its owner.** `GameApp.Save` writes `Elapsed`, `Turn` and `DayFraction` into
`SessionStateData`; `Day` is re-derived on restore, never stored twice.

## One owner per fact — where each fact lives

| Fact | Owner | What everything else is |
|---|---|---|
| the time axis | `GameInfo.TimeAxis` | `GameClock.Axis` is configured from it, by `GameApp.ReconfigureClock` only |
| beats, turns, the day | `GameClock` | every calendar and duration derives from `Advanced`/`DayAdvanced` |
| paused | `SceneTree.Paused` | `GameApp.SetPaused` writes it; `GameApp.IsPaused` reads it |
| a cell's **live** terrain kind | `GridCellDataComponent` | read through `GridCellRules.TerrainKindAt`; saved by `GridWorldStateComponent` |
| the **generated** world | the recipe: `TerrainWorldComponent`'s axes + `Seed` | `TerrainDataLayersComponent` and every renderer are projections of it, rebuilt from it on load |
| what lies underground, and how rich | the generated world (via the data layers) | `GridSubsurfaceStoreComponent` owns only the drawdown |

The live map and the generated world are **two facts with two owners**, and they part ways the moment
a player edits a cell. The generator fills the cells once — it is the map loader — and from then on
the cells are the map; a saved world is regenerated from its recipe by `TerrainWorldComponent.RestoreWorld`,
which never writes a cell. OpenTTD and Widelands each keep one tile array that *is* the saved map;
the cells are that array. The data layers used to be preferred over cells for kind, and won: a rebuilt
map overrode every restored or edited cell wherever it had a tile.

## World roles and boundaries

There is **one terrain-world controller** in this addon: `TerrainWorldComponent`.
The eight world-named classes found in the addon before the rename were not eight terrain engines:

| Type | Responsibility |
|---|---|
| `TerrainWorldComponent` (two partial files) | Configures generation, binds the live cell store, selects renderers, saves the recipe |
| `TerrainGenerationBuffer` (formerly `TerrainWorld`) | Internal temporary arrays for one generation run; not a node or a second live world |
| `GridWorldStateComponent` | Snapshot coordinator for existing cells, objects, roads, jobs and other grid state |
| `TerrainWorldCameraComponent` | Frames the controller's extent or start position |
| `TerrainWorldStatusComponent` | Displays the controller's report |
| `WorldComponent` | Empty abstract environment-component category in Add Node |
| `Beep.ECS.Scenes.WorldMap` | Survival map-screen Control with a Back button; no terrain generation |
| `GameBuilder.WorldStateData` | Serialized level entity/switch/custom-data DTO in GameStateData; not a generator |

The pipeline is `TerrainWorldComponent -> TerrainGeneratorComponent -> TerrainFieldBuilder
-> TerrainGenerationBuffer -> GeneratedTerrainField`. Generation initializes `GridCellDataComponent`;
after that, live edits belong to the cell store, not the generation buffer. Renderers consume the
shared source; switching projections is not another world or a reason to generate again.

Current count: seven world-named addon classes, with the controller split across two files.
`TerrainGenerationBuffer` is an additional internal generation helper, no longer world-named.
Class count is not instance count: a preview and a gameplay scene can each instantiate the same
controller implementation with separate cell stores.

The lab delegates startup to `BuildOnReady` when enabled, otherwise requests one initial build.
Opening the lab on a built world reuses it. Camera/status helpers subscribe to the configured world,
initialize from an existing build, and detach when their world path changes or they leave the tree.
An explicit build before the deferred startup callback also suppresses that callback's generation;
later explicit `NewWorld()` calls still regenerate. `BuiltSize` is published after drawing finishes.
The controller holds one typed reference per renderer. Painted and overlay renderers are already
`Node2D` classes, so separate visibility references are unnecessary. Flat resource, overlay and
relief views use one shared relative-grid-path rule and one resolved grid per draw.

Grid snapshots resolve current paths on every capture/restore. Unwired collaborators are searched
within `ObjectsRootPath` (the current scene when empty). A missing explicit root captures no objects;
saved object paths cannot restore objects outside that root. Explicit collaborator paths may point
outside the root deliberately. Separate simultaneously saved worlds also require distinct save keys.

### Oilfield Days is a separate integration

The inspected external `OilfieldDays.World.BasinWorld` is a game-specific host, not an addon class.
It builds `TerrainMap`, a road/pad `WorldMap`, the old `PainterlyTerrainLayer`, seven hidden atlas
layers, scenery, structures and traffic. Gameplay and WorldPreview still call it in that checkout.
The old painter is absent from the current addon; it was not restored or modified in this refactor.

That checkout has a separate, old terrain pipeline. Consolidating it requires replacing its terrain
generation/rendering with this addon and keeping only oil-game presentation and simulation mapping
in the host. Merely renaming BasinWorld or merging it into TerrainWorldComponent would retain the
duplicate pipeline and couple a reusable addon to OGSim. This migration is **not complete**.

The remaining migration is tracked in `plans/OILFIELD_WORLD_CONSOLIDATION.md`. Do not delete the
game's host until its callers have been migrated. No old painter was changed in this review.

Review scope: all eight classes above (including both controller partials), TerrainFieldBuilder,
GeneratedTerrainField, both lab partials, the external BasinWorld and its WorldMap, plus reference
searches and targeted collaborator reads. This is not a claim that every addon class was audited.

## Contracts and ports

The pattern, everywhere an extension point exists:

| Contract | Duck-typed port | What it lets you replace |
|---|---|---|
| `IGridSite` | `GridSitePorts` | anything commissioned onto the grid: area, materials, turns |
| `IConstructionVisual` | `GridConstructionVisualPorts` | how a site under construction looks |
| `ILoadPort` / `IUnloadPort` / `IStorage` / `ITransporter` | `GridPorts` | anything material moves through |
| `IWorker` | `GridWorkerPorts` | anything that claims and works a job |
| `IExtractor` | — | anything that produces material out of the world |
| (the clock's shape) | `GridClockPorts` | the clock the grid measures work against |

The port reads members **by name** (`Get`/`HasMethod`/`Call`), so a GDScript node with matching
member names participates exactly like the shipped C# component. `GridSitePorts` additionally reads
a plain `Dictionary`, so a site can be authored in JSON.

The grid toolkit deliberately references **no global at all** — no `GameApp`, no autoload path, no
singleton. It finds the clock by shape through `GridClockPorts`, and when there is none it runs
itself in real time. That is what lets a grid template scene be opened on its own and every headless
probe run without standing up a game.

## Wiring: how components find each other

One rule, in one place — `EntityComponent.Resolve<T>(owner, path, ref cached)`:

> the authored `NodePath` when there is one, otherwise the first matching component in the scene;
> cached, and re-resolved when that reference goes stale.

Explicit path first, scene search second, is deliberate: a scene that wires a collaborator
explicitly always wins over one that merely happens to be found, so adding a second component of a
type cannot silently re-point everything searching for it.

Reach for globals only for the game master (`GameApp.Instance` and its accessors). Everything else
is scene-local and composed.

## Where time is measured, and in what

| Subsystem | Unit | Driven by |
|---|---|---|
| `GameClock` | beats | its own `_Process` (Realtime) or `EndTurn()` (Turns) |
| `StatsComponent`, `WorkComponent` | beats | `GameClock.Advanced` |
| grid jobs, builds, production | **turns** (a turn is a day) | `GridWorkClockComponent.WorkTick` |
| `GridCalendarComponent` | days | `AdvanceDay`, called on the clock's day cascade |
| crops | days | the calendar |
| UI refresh intervals, animation, retry backoff | **real seconds, deliberately** | their own `_Process` |

That last row matters: a progress bar's refresh rate and a delivery retry backoff are real-time
concerns even in a turn-based game, and converting them would be wrong. Gameplay duration is turns;
presentation cadence is seconds.

## Verification

`tests/run_addon_checks.ps1` is the gate. Three parts are load-bearing for this architecture:

- `tests/addon_contract_scan.ps1` — pins facts at their single home, and forbids the shapes that
  caused the defects above (no component may read the time axis; the calendar may not run a clock of
  its own; a site's duration may not be seconds; the clock may not carry a pause flag; nothing but
  `GameApp` may write `SceneTree.Paused`; no grid file may read a terrain kind from the data layers;
  `RestoreWorld` may not call `GenerateTerrain`).
- `tests/game_clock_axes_probe.gd` — the same component reaching the same end state on both axes, a
  turn-based producer actually finishing, the clock following a re-declared axis, the tree pause
  refusing `EndTurn`, and the clock surviving a save.
- `tests/terrain_world_recipe_probe.gd` — the recipe round trip: an edited cell survives a restored
  world, the layers publish the saved world again, the subsurface drawdown realigns with its deposit,
  and a pending `BuildOnReady` yields to the restore.
- `tests/runtime_smoke.ps1` → `GridPlacementSmoke` — the grid behaviours, including the site
  contract answered by both a typed definition and a duck-typed one.

A new guard is only trusted after it has been made to fail once against the unfixed behaviour.
