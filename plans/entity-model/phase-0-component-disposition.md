# Phase 0 — Component disposition

**Goal:** decide what the existing ~146 components *are* before adding any. This phase comes
first because the plan was written the other way round, and that was backwards.

**Depends on:** nothing. **Blocks:** Phase 6 (which proposed 16 new components, 9 of which
turn out to already exist in some form).

---

## Why this phase exists

`phase-6-missing-components.md` proposed new components without first asking what we have.
Four parallel audits + hand-verification of every load-bearing claim found that **9 of 16
proposals are a fix or an extension to something that already ships**, and that roughly as
many existing components are inert, misnamed, or dead as were proposed to be added.

Adding to that pile without disposing of it makes the framework worse. With no legacy burden
(dev code, no shipped consumers), **delete is on the table for the first time.**

## The three facts that decide most verdicts

**1. There are zero `[connection]` lines in all 74 shipped scenes.** Every component here
communicates by signal; only **6** code-side subscriptions exist framework-wide, five of them
to `HealthComponent.Damaged`/`Died`. So "inert" is overwhelmingly *structural* — it is one
missing edge per component, not 60 bad components.

**2. Nothing in any shipped scene can deal damage.** All three `TakeDamage` call sites are
dead. `HealthComponent` ships in 6 scenes and can only ever be at full health. This is the
foundation Phases 1–4 were being built on.

**3. Scene-instantiated ≠ reached.** `FloatingTextComponent` ships in `pickup_template.tscn`
and its `ShowText()` has 0 callers. `WeatherAudioController` is mounted in `atmosphere.tscn`
and builds an entire audio bus at `VolumeDb = -80f` to mix permanent silence. Presence in a
`.tscn` proves nothing.

> **The cheapest win in the repo:** the score chain is complete and correct —
> `PickupComponent.cs:102` → `GameFlowComponent.AddScore` → `ScoreChanged` →
> `HudComponent.cs:45`. It is unreachable *solely* because no scene places a pickup.
> Verified: `pickup_template.tscn` is referenced **only** by the generator's copy list
> (`BeepGenreGenerator.cs:301`) and the docs — **no scene instances it.**

**4. Three of the four entity templates are orphans.** `player_template`, `pickup_template` and
`robot_npc_template` are instanced by **zero** scenes; only `enemy_template` is reached, as a
`PackedScene` export on `SpawnerComponent`. The genre mains each inline their own `Player`
`CharacterBody2D` carrying a Controller + Health and **no `AttackComponent`** — so even wiring
`Attack()` to input gives the player nothing to attack with. **"Ships in a template" ≠ reachable.**

## Decide before wiring anything: what "wire" means here

Fact 1 makes "wire A → B" **ambiguous in this codebase** — it can mean a `.tscn` `[connection]`
or a code-side `+=`. There are currently **zero of the first and six of the second**, so there is
no convention to follow. Roughly eight wiring fixes are queued across Phases 0–4; without a
deliberate choice they land in two styles.

**Recommendation: code-side `+=` in `_Ready`, resolved via `GetSiblingComponent`/`FindComponent`.**
It is what the six working subscriptions already do, it survives a developer rebuilding the
scene, and it keeps the component self-contained — which is the whole premise of a drop-in
component library. Reserve `[connection]` for edges the *developer* owns in *their* scene.

---

## Verified reuse — proposals that are already here

Hand-checked, not taken on the agents' word.

| Proposed in Phase 6 | Verdict | Evidence |
|---|---|---|
| **`EntityTagComponent`** | **Half exists — a default change + ~3 lines** | `SpawnerComponent.cs:19` `SpawnGroup = "spawned"`, `:73` `inst.AddToGroup(SpawnGroup)` — groups the **body**, which is exactly what `AIController.cs:139` / `TurretComponent.cs:83` need. Change the default to `"enemies"` → homing and turrets work with **zero new code**. The missing half is *authored* bodies (the player); `EntityComponent.ComponentGroup` is the home. |
| **`LevelTransitionComponent`** | **Don't add — `LevelLoaderComponent` IS it** | `LevelLoaderComponent.cs:56` says so verbatim: *"this doubles as a runtime level transition."* It has **0 external callers**; the only entry is its own `_Ready` self-start (`:52`). Wire `GameFlowComponent.LevelComplete → LoadLevel(CurrentLevel+1)`. Zero new logic. |
| **`StatModifierComponent`** | **Don't add — 2-line fix to `StatusEffectComponent`** | `_Process:182` does `e.Duration -= delta` unconditionally; `:191 IsExpired => Duration <= 0`. No infinite sentinel — the codebase fakes permanence with `999f` (`HungerStaminaComponent.cs:148`). Guard both on `Duration < 0` and it *is* a permanent-modifier channel. |
| **`RespawnComponent`** | **Fix `CheckpointComponent`** | It already computes the respawn position (`:39`) and **throws it away**, persisting a *level index* (`:43`). Fix the storage + the `_activated` latch and it is a `RespawnComponent`. |
| **`ContainerComponent`** | **~15 lines on `InventoryComponent`** | Multiple inventories already coexist; `Resize` (`:297`) handles sizes. Only gap: no method takes another `InventoryComponent`. Add `TransferTo(...)` over the existing public `RemoveAt` + `AddItem`. |
| **`HarvestableComponent`** | **`CropGrowthComponent` is 70% of it** | Already has stages, `Harvest()` (`:114`), `_dropTable?.Roll()` (`:118`). Blocked on `DropTableComponent` regardless. |
| **`InteractionPromptComponent`** | **Exists and is complete — pure missing wire** | `Show()`/`Hide()` 0 callers; `InteractableComponent` emits `PlayerEnteredRange` and holds a `PromptText` nothing reads. **~4 lines.** Someone walked up to this wire and stopped. |
| **`CooldownComponent` (for weapons)** | **Exists — invert the redundancy** | `Trigger()`/`IsReady`/`CooldownReady` is exactly the weapon contract, and it has **0 references** — three hand-rolled duplicates won instead (`ShooterController.cs:34`, `TurretComponent.cs:31`, `AttackComponent.cs:23`). Refactor the three onto it; its `Progress` gives a free radial-cooldown hook they lack, and `GameWeapon.Cooldown` gets one place to land. |
| **`CharacterStatsComponent`** | **New — but delete `PlayerStatsComponent`, don't refactor** | ~15 lines of salvage in a 70-line file. Every stat is a hardcoded property and `SetStat` a hardcoded switch. `Speed`/`Stamina`/`Strength` *look* neutral but are 0-99 soccer ratings — they do not connect to `MovementComponent.Speed` (px/sec). |

**Genuinely new (5):** `HazardComponent`, `LapGate`/`LapTracker`, `EquipmentComponent`,
`VehicleController`, `SelectableComponent`, `GridPlacementComponent`,
`Match3Input`/`Match3View`. Each was checked against the nearest existing candidate and the
candidate fails structurally — e.g. `DragComponent.cs:33` does `GetParent() as Control` and
`:84` *moves the node it is attached to*, so it cannot serve selection at 0% overlap.

## The structural recommendation the plan missed

**`AreaTriggerComponent` — one primitive, five times.** Seven components already hand-roll
"Area2D-parented body trigger" (`CheckpointComponent:29`, `InteractableComponent:30`,
`PickupComponent:48`, `ProjectileComponent:41`, `WindFieldComponent:57`, `DoorSwitchComponent:40`,
`AmbientAudioComponent:61`) — **and two are BROKEN by exactly the parent-type failure the
pattern invites.** Hazard, LevelTransition and LapGate are all this same shape.

Extract the base once (safe resolve + warn on wrong parent) and derive four. That turns three
"new components" into three ~20-line subclasses **and fixes two BROKEN entries as a side
effect.** Do this before Phase 3's melee hitbox — it is the same primitive.

## Delete list

| Delete | Why |
|---|---|
| `EntitySystem.cs` | Abstract, **0 subclasses** (verified). `ProcessAll` appears only in its own declaration and docstring. The "S" of this ECS was never built. |
| `PlayerStatsComponent.cs` | Soccer stat block — `Shooting`, `ShirtNumber`, `Position = "CM"`, `OverallRating` divides by a hardcoded `11`. |
| `TrainingComponent.cs` | `DaysTrainedThisWeek`, `TrainingFocus { Attacking, Defensive }`. |
| `ContractComponent.cs` | `WeeklyWage`, `ReleaseClause`, `ContractExpiry = "2029-06-30"`. |
| `InjuryComponent.cs` | `InjuryRisk` "per match/training"; only consumer is `TrainingComponent`. |
| `LightingComponent.cs` | `_Process` is an **empty body with a comment**; wrong light type for 2D; `:124` tweens an **int** property with a float tween. `DayNightCycleComponent` already tints. |
| `ParallaxComponent.cs` | Godot's `ParallaxBackground` wins — and is already what the shipped levels use (`levels/shooter/level_1.tscn:10`). |
| `RotateComponent.cs`, `BobComponent.cs` | `PickupComponent.cs:15-17,69-70` already exports `FloatAmplitude`/`FloatSpeed`/`AutoRotate` and does both itself. Bob also teleports moving parents back (`:30`). |
| `FlyComponent.cs` | Its own doc (`:11`) says it replaces `TopDownController`. Fold banking + boost in as exports. |
| `GameInfoNode.cs` | Its own comment (`:133`) calls it "the **third way** GenreId reaches GameInfo"; its guards mean the `.tres` always wins → it can only configure a game with no `.tres`, i.e. never after first run. |
| `LifetimeComponent.cs` | `ProjectileComponent.MaxLifetime` covers bullets, the only use. |
| `ui/PulseComponent.cs`, `ui/ShakeComponent.cs`, `ui/SlideInOutComponent.cs` | `UIEffectComponent` is a strict superset **and** is the version ported to GDScript. |
| `DamageTypeComponent.cs` — **node only** | 0 resolvers, 0 callers. **Keep the enum** — `HealthComponent.cs:77,82` and `ResistanceComponent.cs:24-41` depend on it. (Phase 3a already decided this.) |
| 13 private `ChangeScene` helpers | Residue of the `SceneNav` refactor; zero call sites each. C# warns on none of them. |

**Needs a call:** `AnimalBehaviorComponent` (survival ships no animals), `AudioComponent`
(no duplicate, but every audio need in the addon is met by a component rolling its own
`AudioStreamPlayer` — never adopted, for a reason not recoverable from the code).

**Do NOT delete `BeepPathfindingGrid`** (`core/BeepEncryptionPathfinding.cs:68`) despite 0
callers. It is a real A* over `bool[,] _walkable` + `SetObstacle(x,y,blocked)` (`:80`) — an
**occupancy model**, which is exactly what `GridPlacementComponent` needs, and it hands
citybuilder *and* strategy pathfinding for free. **Move it out of that file**, where it sits
incongruously beside SHA256 helpers. (This is why `Match3BoardComponent` is the *wrong* base:
its grid uses `0` to mean "cleared, refill me", so `Refill()` would auto-fill empty lots with
random buildings.)

## Also fix while here — cheap, found by the audit

- **Football residue reaches the base-class docs.** `EntityComponent.cs:18` documents
  `ComponentGroup` with the example `"injured_players"`; `EntitySystem.cs:21` uses
  `"training_players"`. Verified. Deleting the four components leaves their vocabulary
  teaching the next reader.
- **Two category errors.** `TrailComponent : UIComponent` is a world effect requiring `Node2D`
  (`:31`); `SquashAndStretchComponent : ControllerComponent` is a visual effect that controls
  nothing. Both should be `WorldComponent`. Category is the editor's taxonomy — a wrong one is
  a wrong shelf.
- **`AttackComponent`'s inline cooldown** (`:15,:23,:36-43`) is an exact duplicate of the unused
  `CooldownComponent`. Delete the inline copy; resolve the sibling. Same for `ShooterController`
  and `TurretComponent`.

## BROKEN — fix, in leverage order

1. **`DropTableComponent`** — `_entries` is a `private readonly List` with **no `[Export]`**
   (verified: 8 unrelated knobs above it *are* exported). Both callers are guaranteed no-ops.
   **One fix un-deadens `DestructibleComponent`, `CropGrowthComponent`, and all loot at once.**
   This blocks Phase 4.
2. **`CraftingComponent.Craft()`** — deducts inputs, never grants the output. One line.
3. **`InteractableComponent`** — in `topdown_main.tscn:48` it sits on the **Player** and
   `IsPlayer` filters entering bodies to those named `"Player"`, so it could only fire for
   *another player*. The semantics are inverted: the player is the *interactor*.
4. **`InventoryComponent.Interact.cs`** — `Display.cs:42-54` builds the slots and never
   connects the signals. Drag-drop, right-click split, and the whole tooltip path are dead.
5. **`CheckpointComponent`**, **`DoorSwitchComponent`** (`SetDeferred("monitoring", …)` on a
   `CollisionObject2D` — an Area2D-only property; doors go invisible while staying solid),
   **`ObjectPoolComponent`** (`MaxSize` never caps), **`HitStopComponent`**
   (`Engine.TimeScale = 1f` is an unconditional latch that stomps pause), **`ui/BossHealthBarComponent`**
   (screen-space anchors under a Node2D → scrolls off-camera).

## Sequencing this changes

Phase 0 lands **before Phase 1**. Two ~1-line wins come first, because each turns on a chain
that is *already fully coded*:

1. **Place a pickup in a level.** `Pickup:102 → AddScore:72 → ScoreChanged → HudComponent:45`
   is complete and correct, and unreachable only because no scene instances the template.
   Scoring works end-to-end after one node.
2. **`SpawnGroup = "enemies"` in `levels/shooter/level_1.tscn`.** `SpawnerComponent:73` already
   groups the body; this alone un-inerts `ProjectileModifierComponent`.
3. `EntityComponent.ComponentGroup` "group my parent" mode — covers authored players; the rest
   of what `EntityTagComponent` was for.
4. **`DropTableComponent` `[Export]`** — unblocks Phase 4 and three components at once.
5. **`AreaTriggerComponent`** base + port the two broken triggers — unblocks Phase 3's hitbox.
6. **Damage** (Phase 3a) — fact 2 is upstream of `Aggro`, `DropTable`, `Flash`, `HitSpark`,
   `HitStop`, `AutoHeal`, `Resistance` and `DamageType`. **Eight components are gated behind it.**
7. The delete list — do it before writing anything new, so new work isn't modelled on dead code.

## Verification

`dotnet build` → 0 errors; `validate_scenes.sh` → PASS. Neither runs the game.

Per deletion, the check is *the same one that justified it*: `grep` the symbol repo-wide and
confirm 0 references outside its own file and the docs. Every entry above was verified that
way; `CLAUDE.md` § *Testing* is the standing caveat on all of it.
