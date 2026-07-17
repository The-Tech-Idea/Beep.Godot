# Infrastructure — 24 components

Category bases, autoloads, the app spine, the skin layer.

> **This layer mostly works.** `ThemePresetComponent` + `GameInfoBinder` ship in 33 scenes each and
> drive the whole skin system; `SceneNav` is called by 33 screen scripts. The defects here are
> concentrated in two places: a base-class export that has never worked, and a third redundant
> path to `GameInfo`.

---

## The category bases

### `EntityComponent` — the base, and its one export has never worked
**Evidence:** base of all ~146 components. `FindComponent<T>` (`:69`) is the type-matching resolver
used by `AttackComponent.cs:80,99,103`, `CheckpointComponent.cs:47`, `DoorSwitchComponent.cs:60`,
`AnimalBehaviorComponent.cs:50-51`, `DropTableComponent.cs:46-47`, `CropGrowthComponent.cs:51`.
`GetSiblingComponent<T>` (`:46`). `IsActive` (`:28`) is honoured throughout.

**The `ComponentGroup` defect — verified three ways:**
- **It groups the wrong node.** `_EnterTree` (`:30-34`) calls `AddToGroup(ComponentGroup)` on
  `this` — **and `EntityComponent : Node`, not `Node2D`** (`:15`). Meanwhile
  `AIController.cs:139-145`, `TurretComponent.cs:83-87` and `ProjectileModifierComponent.cs:83`
  all filter `node is Node2D`. **A component added to `"players"` is filtered out.** The export is
  structurally incapable of serving its only consumers.
- **Zero scenes set it.** Verified: `ComponentGroup` appears in **no `.tscn`**, and `groups=`
  appears in no `.tscn`.
- **Everything that really uses groups bypasses it.** `DynamicFogLayer.cs:64` hardcodes
  `AddToGroup("fog_layer")`; `SeasonalComponent.cs:55` hardcodes `"seasonal"`;
  `WeatherSystemComponent.cs:225` hardcodes `"weather_system"`. **`WindFieldComponent.cs:119`
  documents the workaround**: *"joins its group in its `_Ready()` regardless of `ComponentGroup`;
  fall back to a tree scan."* Developers hit this export, found it didn't work, and routed around
  it.

**Fix (S — ~3 lines):** give it a **"group my parent" mode**. It is free to redefine: nothing sets
it and nothing depends on the current semantics. **This is the whole of the proposed
`EntityTagComponent`** — not a new component. It un-inerts AI, turrets and homing in every genre.

**Also (S):** the doc at `:18` cites `"injured_players"` — football-manager residue in the base
class. Fix with the deletions.

### `ControllerComponent` — the only base with behaviour
**Evidence:** `ResolveBody2D()` (`:28-33`) — a pattern-match, not a hard cast, and it **warns**
instead of silently nulling. 20 xrefs.

**Fix:** none. **This is the model** for the `Area2D` equivalent (`AreaTriggerComponent`), which
seven components currently hand-roll and two are broken by.

### `UIComponent` (52 xrefs), `GameplayComponent` (42), `WorldComponent` (24)
**Evidence:** empty bodies (`UIComponent.cs:15` is `: EntityComponent { }`).

**Fix:** keep. **They are editor taxonomy, not code** — a component's category is which shelf it
appears on in Add Node. Two are on the wrong shelf: `TrailComponent : UIComponent` (a world effect)
and `SquashAndStretchComponent : ControllerComponent` (a visual effect that controls nothing).

### `EffectComponent : UIComponent`
**Evidence:** abstract base; subclassed by `PulseComponent.cs:12`, `RippleComponent.cs:16`,
`ShakeComponent.cs:12`, `SlideInOutComponent.cs:13`. 5 xrefs.

**Fix:** keep, but three of its four subclasses are deleted (see `ui-widgets.md`). Its
`ApplyToChildren` cascade must be absorbed into `UIEffectComponent` first.

### `EntitySystem` — DELETE
**Evidence:** abstract (`:16`); **0 subclasses** repo-wide; `ProcessAll` (`:64`) referenced only by
its own declaration and docstring (`:12`). `TrackedGroup` (`:31`) reaches nothing. **The "S" of
this ECS was never built** — every component self-drives via `_Process`. Its doc (`:21`) cites
`"training_players"`. → `DELETE.md`

---

## The app spine — ALIVE

| Component | Evidence | Note |
|---|---|---|
| `GameApp` | Autoload (`BeepGenreGenerator.cs:261`); 53 xrefs; `_Process:191` (FPS + playtime). Joins `saveables` **only when it is the autoload** (`:157`) — correctly guards a scene-dropped copy | Keep |
| `GameStateManagerComponent` | Autoload (`:271`); 14 xrefs; called from `GameFlowComponent.cs:59`. `SyncAllSaveables` inside `Save`, deferred restore via `BeginSession:274`, `LoadForSceneChange:253` | Keep — save/load path is coherent |
| `GameFlowComponent` | **10 shipped mains**; 10 xrefs. Drives `BeginSession` (`:59`), `AddScore` (`:72`, one caller), the pause overlay (`:178-228`). `_UnhandledInput` | Keep — **the spine** |
| `LevelLoaderComponent` | 6 mains; `_Ready:44-50` reads `GameApp.CurrentLevel`, `CallDeferred(LoadLevel):52` | Keep — see below |
| `SettingsComponent` | Autoload (`:262`); `settings_menu.tscn` | Keep |
| `LocalizationComponent` | Autoload (`:263`); read by `SettingsComponent.cs:259` | Keep (0 scenes, but autoloaded — not inert) |
| `IGameStateable.cs` | Contains `ISaveable` (`:38`) + `SaveableHelper` (`:52`). Implemented by `GameApp.cs:26`; `SaveableHelper.Group` used at `GameApp.cs:157`; `FindAllSaveables` from `GameStateManagerComponent.cs:332` | Keep — **but no type named `IGameStateable` exists in it.** Rename the file to `ISaveable.cs` (S) |

### `LevelLoaderComponent` — it already *is* `LevelTransitionComponent`
**Evidence:** **`:55-56` says so verbatim: _"this doubles as a runtime level transition."_**
`LoadLevel(int)` (`:57`) frees the current instance (`:77-78`), instances the new one (`:80-81`),
repositions the player to `PlayerSpawn` (`:84`, `:89-95`).

**Defect:** **`LoadLevel` has 0 external callers.** The only entry is the `_Ready` self-start
(`:52`). Level *loading* works; level *changing* has no driver.

**Fix (S):** `GameFlowComponent.LevelComplete → LoadLevel(CurrentLevel + 1)`, plus an
`AreaTriggerComponent`-derived zone for topdown's empty `TransitionZones` (`level_1.tscn:18`).
**Zero new logic. Do not write `LevelTransitionComponent`.**

### `BeepGenreScene` — the advertised entry point, in no scene
**Evidence:** 0 shipped `.tscn`, 0 code callers — **but it IS the entry point the docs advertise**
(`README.md:35`, `INDEX.md:11`). Calls `BeepGenreGenerator.ApplyTuning`/`ApplyNavWiring`
(`:115`,`:122`). Its sibling-theme push (`:140-145`) is safe — `ThemePresetComponent.cs:23,35,49,61`
setters re-`ApplyTheme()` when `IsInsideTree()`.

**Fix (S):** ship one `genre_root.tscn` containing it, so the entry point is reachable without
reading the README.

---

## The skin layer — ALIVE

| Component | Evidence |
|---|---|
| `ThemePresetComponent` (+`.NodeTheming`) | **33 shipped scenes**; `GetParent() as Control` (`:130`); driven by `GameInfoBinder.cs:68-77`; 12 xrefs |
| `GameInfoBinder` | **33 shipped scenes** |
| `SkinCatalog` | 16 xrefs — dock (`BeepGameBuilderDock.cs:142,156,177`), `GameInfo`, `GameApp`, `BeepGenreScene`, `BeepMcpCommands`. **The hub** |
| `UISkin`, `ColorPalette`, `GeometryProfile` | Resources; `SkinCatalog.cs:303-307,645`; `ThemePresetComponent.cs:181`, `.NodeTheming.cs:18` |
| `FileThemePreset`, `PaletteTintedPreset`, `IThemePreset` | `ThemePresetComponent.cs:119,166,181` |
| `ShapeOverrides`, `SkinPropertyHints` | `SkinCatalog.cs:343-381,634`; `ThemePresetComponent.cs:790-799` |

**Fix:** none. This layer works and is the addon's strongest.

> **`ApplyTheme()` idempotency was fixed this session** via meta flags — it ran 5× per scene load
> and wasn't idempotent (`AddChild(new RippleComponent)` per pass). `CLAUDE.md` § *Public API must
> be idempotent* records why.

---

## REDUNDANT

### `GameInfoNode` — "the third way GenreId reaches GameInfo", by its own admission
**Evidence:** 0 refs, 0 scenes; the only mention outside itself is a docstring
(`ui/SkinPropertyHints.cs:10`). **Its own comment (`:133`) calls it "the third way GenreId reaches
GameInfo".** Its copy guards (`:108-130`) compare against defaults, so **the `.tres` always wins**
→ it can only configure a game with **no** `game_info.tres` — i.e. never after first run, since it
saves one at `:141`.

**Fix:** delete. Winner: `game_info.tres` (loaded by `GameApp.cs:148`) + `BeepGenreScene`.
→ `DELETE.md`

---

## Order

1. **`EntityComponent` parent-group mode** (S) — ~3 lines; un-inerts AI/turrets/homing everywhere.
2. `LevelLoaderComponent` — wire `LevelComplete → LoadLevel` (S).
3. Rename `IGameStateable.cs` → `ISaveable.cs` (S).
4. Ship `genre_root.tscn` for `BeepGenreScene` (S).
5. Delete `EntitySystem`, `GameInfoNode` (S).
6. Reclassify `TrailComponent` → `WorldComponent`, `SquashAndStretchComponent` → `WorldComponent`.
7. Strip football residue from `EntityComponent.cs:18` (with the deletions).
