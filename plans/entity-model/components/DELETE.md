# Delete list — 16 entries

Everything to remove, with the justification that earns it.

> **Status (signed off):** the **pure deletes are done** — #1–3, #4–8 (football + its doc residue,
> which was already clean), #9–12, **#15** (now a pure file-delete; enum extracted in Phase 0
> item 8), #16 (the 13 dead `ChangeScene` helpers), and the dead-member table (`_flashMaterial`,
> `hitNode` binding, `Spread` enum value, `SlideDuration`). Build + validator green.
> **Still open:** #13 `FlyComponent` and #14 the 3 UI effects — both need their **absorb-first**
> step before deletion — and the three **Borderline** calls below.

> **This is possible for the first time.** No back-compat, no shipped consumers, no fallbacks or
> stubs. **Do the deletions before writing anything new**, so new work isn't modelled on dead code.

## The test each entry had to pass

`grep` the symbol repo-wide → 0 references outside its own file and the docs, **plus** a reason it
should not simply be wired. **0 callers alone is not sufficient** — see the two saves at the bottom.

---

## Delete — dead residue

| # | File | Justification |
|---|---|---|
| 1 | `ecs/EntitySystem.cs` | Abstract (`:16`); **0 subclasses** repo-wide (`grep ": EntitySystem"`). `ProcessAll` (`:64`) appears **only in its own declaration and its own docstring** (`:12`). `TrackedGroup` reaches nothing. **The "S" of this ECS was never built** — every component self-drives via `_Process`. |
| 2 | `ecs/LightingComponent.cs` | **`_Process` (`:70`) is an empty body with a comment.** Uses `DirectionalLight2D` (`:38`) against a 2D shadow model that doesn't work that way; **`:124` tweens `"shadow_item_cull_margin"` — an `int` — with a float tween.** `_lastPhase` (`:41`) written at `:106`, never read. `DayNightCycleComponent` already tints the scene; this is a broken second path. |
| 3 | `ecs/GameInfoNode.cs` | **Its own comment (`:133`) calls it "the third way GenreId reaches GameInfo".** Copy guards (`:108-130`) compare against defaults so **the `.tres` always wins** → it can only configure a game with no `game_info.tres`, i.e. **never after first run** (it saves one at `:141`). 0 refs, 0 scenes. Winner: `game_info.tres` (`GameApp.cs:148`) + `BeepGenreScene`. |

## Delete — misplaced (football-manager residue)

**These four are one coherent slice, not four strays.** Delete together.

| # | File | Justification |
|---|---|---|
| 4 | `ecs/PlayerStatsComponent.cs` | `Shooting`/`Passing`/`Dribbling`/`Tackling`/`Keeping` (`:14-18`), `ShirtNumber` (`:33`), `Position = "CM"` (`:34`), `PotentialRating` "1-5 stars" (`:37`), `OverallRating` divides by a hardcoded **11** (`:46`). `SetStat` (`:49`) 0 callers. **Do not refactor** — ~15 lines of salvage in a 70-line file; every stat is a hardcoded property and `SetStat` a hardcoded switch, so refactoring means replacing the body and keeping a signal. Write `CharacterStatsComponent` fresh. **Watch the subtle half:** `Speed`/`Stamina`/`Strength` (`:21-23`) *look* neutral but are **0-99 soccer ratings**, not physics values — they do not connect to `MovementComponent.Speed` (px/sec). |
| 5 | `ecs/TrainingComponent.cs` | `DaysTrainedThisWeek` (`:14`), `WeeklyImprovementPoints` (`:16`), `TrainingFocus { Attacking, Defensive… }` (`:18`), `EndWeek()` (`:35`), depends on `InjuryComponent` (`:47`). `Train` (`:24`) 0 callers. **Not RTS production despite reading like it** — that is `WorkComponent`. |
| 6 | `ecs/ContractComponent.cs` | `WeeklyWage`/`ContractYears`/`ReleaseClause`/`IsLoanContract` (`:12-16`), `ContractExpiry = "2029-06-30"` (`:16`), `MarketValue => WeeklyWage*52*ContractYears` (`:22`). |
| 7 | `ecs/InjuryComponent.cs` | `DaysRemaining` (`:14`), `InjuryRisk` "5% base risk per match/training" (`:16`), `AdvanceDay()` (`:40`). `ApplyInjury` (`:25`) 0 callers. Only consumer is `TrainingComponent`. |
| 8 | **The residue in the base-class docs** | `EntityComponent.cs:18` documents `ComponentGroup` with the example **`"injured_players"`**; `EntitySystem.cs:21` uses **`"training_players"`** (both verified). Deleting the components while leaving their vocabulary in the base class teaches the next reader the wrong domain. |

## Delete — redundant (the winner already ships)

| # | File | Winner | Justification |
|---|---|---|---|
| 9 | `ecs/BobComponent.cs` | `PickupComponent` | `PickupComponent.cs:15-17,69-70` already exports `FloatAmplitude`/`FloatSpeed`/`AutoRotate` and sin-bobs itself — which is what `pickup_template.tscn:21-23` ships. Bob also **latches `_startPos` at `_Ready` (`:30`), teleporting a moving parent back.** 0 refs. |
| 10 | `ecs/RotateComponent.cs` | `PickupComponent.AutoRotate` (`:17`,`:70`) | Correct code, 0 scenes — but duplicated **in the one place it would be used** (the coin). |
| 11 | `ecs/ParallaxComponent.cs` | Godot's `ParallaxBackground`/`ParallaxLayer` | **Which is what the shipped levels already use** (`levels/shooter/level_1.tscn:10-11`). Also consumes a `FollowCameraGroup` (`:33`) nobody joins. |
| 12 | `ecs/LifetimeComponent.cs` | `ProjectileComponent.MaxLifetime` (`:14`) | Correct, 0 scenes. It belongs on `projectile_template` — which already uses the projectile's own lifetime. **Two timers freeing one node.** |
| 13 | `ecs/FlyComponent.cs` | `TopDownController` | **Its own doc (`:11-12`) says it "can replace TopDownController or ShooterController".** Same 8-dir input + `MoveToward` + `MoveAndSlide`; only deltas are banking (`:79-83`) and boost (`:60`). **Fold those in as `[Export]`s first**, then delete. |
| 14 | `ecs/ui/PulseComponent.cs`, `ecs/ui/ShakeComponent.cs`, `ecs/ui/SlideInOutComponent.cs` | `UIEffectComponent` | `UIEffectComponent.cs:7-8` has all three as `EffectType` + 4 scopes — a strict superset — **and is the version ported to GDScript** (`beep_ui/effects/ui_effect.gd:5`). `SlideInOutComponent.cs:98-110` also fakes multi-target completion off `_activeTweens[0]`. **Absorb `EffectComponent.ApplyToChildren`'s cascade into `UIEffectComponent` first.** |
| 15 | `ecs/DamageTypeComponent.cs` — **the node class only** | the enum + `[Export] DamageType` on the attacker | **0 resolvers.** `GetDamage()` (`:21`) 0 callers, `Multiplier` (`:18`) never read, `DamageDealt` (`:23`) never emitted. Its own doc says "attach to the same node as an AttackComponent or ProjectileComponent" — **neither ever looks for it.** ⚠ **KEEP THE ENUM** — `HealthComponent.cs:77,82` and `ResistanceComponent.cs:24-41` depend on it, and `GameWeapon.DamageType` will. Demote to a plain enum file. |
| 16 | The 13 private `ChangeScene` helpers | `SceneNav` | `DeckBuilder.cs:17`, `BuildMenu.cs:17`, `Districts.cs:17`, `Economy.cs:17`, `Character.cs:17`, `Inventory.cs:17`, `Quests.cs:17`, `Diplomacy.cs:17`, `Research.cs:17`, `UnitPanel.cs:17`, `Backpack.cs:17`, `Crafting.cs:17`, `WorldMap.cs:17`. **Zero call sites each** — residue of the `SceneNav` refactor. **C# emits no warning for an unused private method**, which is why they survived. The comment at `:16` is stale. |

## Also delete — dead members inside living files

| File | Member | Why |
|---|---|---|
| `ecs/FlashComponent.cs` | `_flashMaterial` (`:21`) | Assigned at `:33`, **never read** — `Flash()` tweens `modulate`. |
| `ecs/ProjectileComponent.cs` | `hitNode` binding (`:84`) | `n is Node2D hitNode` binds and never uses it. |
| `ecs/LightingComponent.cs` | *(whole file — see #2)* | |
| `ecs/ui/SlideInOutComponent.cs` | `_finishCount` (`:30`,`:75`) | *(whole file — see #14)* |
| `ecs/ProjectileModifierComponent.cs` | `Spread` (`:18`) | **In the enum with no case in the switch** (`:42-76`) → silently falls to `Straight`. **Implement it or remove it from the enum — do not leave it selectable.** |
| `ecs/ui/BossHealthBarComponent.cs` | `SlideDuration` (`:17`) | Declared, **never referenced**; the advertised slide does not exist. |
| `ecs/StateMachineComponent.cs` | — | `:158` `(float)timeObj` on a `Variant` is an **invalid cast** and will throw. Fix, don't delete. |

---

## Borderline — needs a call

| File | The case for | The case against |
|---|---|---|
| `ecs/AnimalBehaviorComponent.cs` | Legitimate survival domain; code is correct (`_Process:55`, safe cast `:52`) | 0 scenes, no animal entity ships, `survival_main.tscn` has none, `TryHunt()` (`:119`) 0 callers. Needs a scene **and** an AI driver. |
| `ecs/AudioComponent.cs` | No duplicate exists; `Play`/`PlayOneShot` (`:47`,`:60`) are fine | **Every audio need in the addon is met by a component rolling its own `AudioStreamPlayer`** — it was never adopted, for a reason not recoverable from the code. Either adopt it across those, or delete. |
| `ecs/CooldownComponent.cs` | **Recommend KEEP.** `Trigger()`/`IsReady`/`CooldownReady`/self-tick is exactly the weapon contract; its `Progress` (`:23`) gives a free radial-cooldown hook the copies lack; `GameWeapon.Cooldown` needs one home | 0 refs; three inline duplicates already won (`ShooterController.cs:34`, `TurretComponent.cs:31`, `AttackComponent.cs:23`). **Delete this OR the three copies — not neither.** |

---

## ⚠ Do NOT delete — 0 callers, still valuable

**These two failed the "0 callers ⇒ dead" heuristic and would have been deleted by it.**

| File | Why it survives |
|---|---|
| `core/BeepEncryptionPathfinding.cs:68` — `BeepPathfindingGrid` | 0 callers, not a `Node`, not `[GlobalClass]`, marooned beside SHA256 helpers. **But it is a real A\* over `bool[,] _walkable` + `SetObstacle(x,y,blocked)` (`:80`) — an occupancy model**, which is exactly what `GridPlacementComponent` needs, and it hands citybuilder *and* strategy pathfinding for free. **Keep it; move it out of that file.** (Contrast `Match3BoardComponent`, the tempting alternative: its grid uses `0` to mean *"cleared, refill me"* (`:144`,`:174-178`), so `Refill()` would auto-fill empty lots with random buildings.) |
| `ecs/atmosphere/LightningBoltComponent.cs` | In **no `.tscn`** — **correctly.** It is constructed and driven at runtime: `WeatherSystemComponent.cs:605-609` (`new`, `AddChild`, `Strike`), and self-frees (`:71-74`). **The one file where "not in any scene" is not a death signal.** |

**The lesson, worth keeping:** a 0-caller class is not automatically residue. `BeepPathfindingGrid`
has 0 callers and is valuable; `EntitySystem` has 0 callers **and** 0 subclasses **and** no
behaviour, and is genuinely dead. The heuristic that finds one finds the other — **look at what it
is before pricing it.**

---

## Verification

Per deletion, run the check that justified it: `grep` the symbol repo-wide and confirm 0 references
outside its own file and the docs. Then `dotnet build` → 0 errors and `validate_scenes.sh` → PASS.

**Order:** deletions **before** new components (#1–16), except #13 and #14, which need their
absorb-first step. **Never delete #15's enum.**
