# Phase 6 — The missing components

**Goal:** name the framework-level gaps the archetype work exposed, ranked by leverage. Each
is general (`HazardComponent`), never content (`GoombaComponent`).

**Depends on:** Phase 5's archetype tables for the requirements, and **Phase 0**, which
disposes of what already exists.

> ## ⚠ Read Phase 0 first — this phase was written backwards
>
> It proposed 16 new components without first auditing the ~146 we have. A four-way audit,
> every load-bearing claim hand-verified, found **9 of the 16 already exist** in a form that
> needs a fix or a wire, not a new type. Those 9 have moved to
> `phase-0-component-disposition.md`. **What remains below is the genuinely-new residue**,
> plus the edges — which were always the real content of this phase.
>
> The corrections are recorded where they were wrong (below), not only in the tracker.

---

## Ranked by reach

### 1. ~~`EntityTagComponent`~~ — **WITHDRAWN: it already exists.** → Phase 0

Nothing joins `"players"` or `"enemies"`, so **every AI, every turret, and every homing
projectile is inert in every genre** (`AIController.cs:21`, `TurretComponent.cs:17`,
`ProjectileModifierComponent.cs:22`). That finding stands and is still the highest-leverage
item in the report. **The proposed fix was wrong.**

`SpawnerComponent.cs:73` already does exactly this — `inst.AddToGroup(SpawnGroup)` groups the
**body** (`Node2D`), which is precisely what those three lookups filter for. It only defaults
to `"spawned"` (`:19`) instead of `"enemies"`. **One default change makes homing and turrets
work with zero new code.**

The genuinely missing half is *authored* (non-spawned) bodies — the player.
`EntityComponent.ComponentGroup` is the home, and it is **free to redefine**: it groups `this`
(a `Node`, not a `Node2D`), **0 scenes set it**, and every component that really uses groups
bypasses the export and hardcodes `AddToGroup` in `_Ready` — `WindFieldComponent.cs:119`
documents the workaround. So it is a **~3-line change to `_EnterTree`**, not a component.

### 2. `HazardComponent` — no way to damage on contact
### — but build `AreaTriggerComponent` first

**Correction:** this is one instance of a primitive that already exists seven times by hand —
`CheckpointComponent:29`, `InteractableComponent:30`, `PickupComponent:48`,
`ProjectileComponent:41`, `WindFieldComponent:57`, `DoorSwitchComponent:40`,
`AmbientAudioComponent:61` — **and two of them are BROKEN by exactly the parent-type failure
the pattern invites.** Hazard, `LevelTransition` and `LapGate` are all this same shape.

Extract `AreaTriggerComponent` (safe resolve + warn on wrong parent) and derive four. Three
"new components" become ~20-line subclasses, and two BROKEN entries are fixed as a side effect.
It is also Phase 3's melee hitbox. **Build it once.** (Phase 0, item 2.)

No component damages a body on `Area2D` entry. Grep for `Hazard|KillZone|DamageZone` → nothing.
Consequences shipping today:

- `levels/platformer/level_1.tscn:38-42` — `LevelBoundary`, an `Area2D` + shape at y=1000,
  unmistakably a fall-out killzone. **No script, no connection.** Falling does nothing.
- `Hazards` is an empty `Node2D` in both platformer levels.
- `levels/shooter/*` — `WorldBounds`, an `Area2D` with **no shape and no script**.

Shape: Area2D-parented, on `BodyEntered` call `HealthComponent.TakeDamage(amount, type)`, with
`Continuous`/`OneShot`, `Cooldown`, `InstantKill`. Composes with `KnockbackComponent` the same
way `ProjectileComponent` already does (`:83-85`).

### 3. The inert verbs

Shipped, **zero callers**, verified by grep:

| Verb | Consequence |
|---|---|
| `AttackComponent.Attack()` (`:45`) | Nothing ever attacks. The whole melee/ranged pipeline is un-triggered. `AIController` emits `InAttackRange` (`:97`) and never calls it. |
| `AggroComponent.AddThreat()` (`:27`) | Threat table always empty, `CurrentTarget` always null. `HealthComponent.Damaged` is never routed to it. |
| `DestructibleComponent.TakeDamage(int)` (`:31`) | Every destructible is invulnerable — damage only lands on `HealthComponent`. |
| `WorkComponent.StartWork()` / `.Tick()` | **0 callers, and the component has no `_Process`** — so it never ticks itself either. An earlier draft of this plan called it "a usable production model today" and "the genre's one real asset". **That was wrong.** It is a *correct model that has never run*: the design needs no work, the wiring is entirely absent. Its value is that it needs designing, not that it functions. |

These are **edges**, not components: `Damaged → AddThreat`, `AIController.InAttackRange →
Attack`, and either unifying `DestructibleComponent` behind `HealthComponent` or teaching
damage to find it. The last is the cleanest: give `DestructibleComponent` a sibling
`HealthComponent` and listen to `Died` — then every existing damage source reaches it for free,
and the duplicate `int HP` disappears.

### 4. The broken item edges

| Edge | State |
|---|---|
| `PickupComponent.Collected` → `InventoryComponent.AddItem` | **0 connections.** Identical signatures, never wired. Picking anything up puts it nowhere. |
| `HealthComponent.Died` → `DropTableComponent.Roll()` | Never. Only `DestructibleComponent.Break` rolls — and it can't be damaged (§3). **Nothing ever drops loot.** |
| `CraftingComponent.Craft` → the output item | **Deducts materials, grants nothing.** `ecs/CraftingComponent.cs` — the comment literally reads `// Grant result.` above an `EmitSignal(Crafted, recipe.OutputItem)` with no `AddItem`. Crafting is a material shredder. |
| `DropTableComponent`'s loot list | `private readonly List<DropEntry> _entries`, **no `[Export]`**, filled only by `AddEntry()` — which has no callers. A designer cannot author drops without C#. Fix with `DropTableEntry : Resource`, mirroring `CraftingIngredient`. |
| `QuestComponent.ProgressObjective` | Public, **no callers**. The shipped `quests.tscn` objective *"Defeat the Dark Lord (0/1)"* can never tick. |

Phase 4 covers the first; the rest belong here.

### 5. Genre-shaped gaps

| Missing | Genre | Note |
|---|---|---|
| ~~`LevelTransitionComponent`~~ **WITHDRAWN** | topdown | **`LevelLoaderComponent` already is it** — `:56` says so verbatim: *"this doubles as a runtime level transition."* It frees the old instance (`:77`), instances the new (`:80`), repositions to `PlayerSpawn` (`:84`), and has **0 external callers**. Wire `GameFlowComponent.LevelComplete → LoadLevel(CurrentLevel+1)`, plus an `AreaTriggerComponent` zone for `TransitionZones`. **Zero new logic.** |
| `DialogUIComponent` binding | topdown | `DialogComponent` emits `DialogStarted`; `topdown_main`'s `DialogLayer` is inert markup. *(A `DialogUIComponent` exists but builds its own UI and is parent-type-broken — see Phase 5.)* |
| ~~`StatModifierComponent`~~ **WITHDRAWN** | shooter | The gap is real — `LevelUpChoice` records a pick and says applying it "is the game's job", with **no permanent-modifier channel**. But the fix is **2 lines on `StatusEffectComponent`**: `_Process:182` decrements `Duration` unconditionally and `:191 IsExpired => Duration <= 0`, so there is no infinite sentinel — the codebase fakes permanence with `999f` (`HungerStaminaComponent.cs:148`). Guard both on `Duration < 0` and it **is** the channel. (The deeper defect is orthogonal: `ApplyEffectWithModifiers` (`:92`) has **0 callers**, so `GetModifier` is a constant-default function and all 4 read sites are dead. Phase 2 covers it.) |
| `CharacterStatsComponent` | rpg | **NEW — but delete `PlayerStatsComponent`, don't refactor it.** ~15 lines of salvage in a 70-line file: every stat is a hardcoded property and `SetStat` a hardcoded switch, so "refactoring" means replacing the body with a dictionary and keeping a signal. `Speed`/`Stamina`/`Strength` *look* neutral but are 0-99 soccer ratings — they do not connect to `MovementComponent.Speed` (px/sec). Gives `LevelingComponent.StatPoints` a destination. |
| `HarvestableComponent` → **extend `CropGrowthComponent`** | survival | It is already 70% of it — stages (`:15`), `Harvest()` (`:114`), `_dropTable?.Roll()` (`:118`). Missing only a tool-class gate and a non-crop (rock/tree) mode. **Blocked on `DropTableComponent` regardless** (§4). |
| `ContainerComponent` → **~15 lines on `InventoryComponent`** | rpg/survival | Multiple inventories already coexist (nothing is static) and `Resize` (`:297`) handles sizes. The only gap: **no method takes another `InventoryComponent`** — `MoveItem` (`:201`) indexes `Slots[]` on `this` for both ends. Add `TransferTo(...)` over the existing public `RemoveAt` + `AddItem`. **Two real blockers:** `ParticipatesInSave` (`:58`) is documented "player's inventory only — `GameStateData` keeps a single Inventory slot", so a chest **cannot persist**; and drag-across-grids is moot because drag-drop is dead (`Interact.cs:23` never connected). |
| `CardDef : Resource` + deck/hand | cardgame | The genre is data-shaped; `hand_limit`/`card_fan_angle`/`card_hover_scale` are all inert. |
| `Match3InputComponent` + `Match3ViewComponent` | puzzle | **The board is headless.** `Swap()` has 0 callers and nothing subscribes to `CellChanged` — no input path, no renderer. The sim is complete and correct; it is simply not connected to anything. Also: `ScoreChanged` → `GameFlow.AddScore` is a **signal connection, not a component** — that one edge makes `target_score` real and `level_complete.tscn` reachable. |
| `VehicleController` + `VehicleSpec : Resource` | racing | **Confirmed: none exists.** No `ControllerComponent` subclass models a vehicle; grep for `throttle` → 0 hits. `MovementComponent` is the *wrong model*, not just redundant — it accelerates omnidirectionally with no heading, turn rate, or lateral grip. A car that can strafe sideways is not a car. `GameApp.SelectedVehicle` is written by `VehicleSelect` and **read by nothing**. |
| `LapGateComponent` + `LapTrackerComponent` | racing | **`CheckpointComponent` cannot count laps** — three independent blockers: it latches `_activated` permanently (so lap 2 through the same gate is ignored, and `SingleUse` does *not* gate the latch); it stores a **level index**, not order or position, so reverse/skip crossings are indistinguishable; and `HealOnActivate` defaults true — respawn semantics, not a lap gate. |
| `SelectableComponent` + `SelectionManagerComponent` + `CommandComponent` | strategy | No unit selection, orders, or formation exist at all. |
| `GridPlacementComponent` + `EconomyTickComponent` + `BuildingSpec` | citybuilder | Nothing exists. Keep the grid as **data**, per Phase 5 — a Node per cell would be a regression from what `Match3BoardComponent` already demonstrates. |

### 6. The refactor worth considering — **and it is ~2 lines, not "bigger"**

**`AIController` self-drives** (`_body.Velocity = …; MoveAndSlide()`, `:62-63`), which is why it
is mutually exclusive with `MovementComponent`. If it instead wrote `DesiredDirection` into a
sibling `MovementComponent`, the AI-vs-Movement conflict class **dissolves in every genre**, and
`enemy_template` works as already composed.

**Correction — this said "Bigger change; flagged, not scheduled." That was wrong.** The
receiving half is already built and documented for exactly this:
`MovementComponent.cs:45` is a public settable `Vector2 DesiredDirection`, consumed at
`:74 Move(DesiredDirection, delta)`, and its own doc at `:21` says it is *"the one an AI can
steer by setting DesiredDirection."* The change is ~2 lines on the `AIController` side.
**Schedule it.**

## Sequencing

**Phase 0 lands first** — the disposition, the deletes, and the four reuse fixes that
everything here rests on. Then:

1. **The inert edges** (§3, §4) — mostly wiring existing signals; each is a few lines. This is
   the bulk of the real value in this phase, and always was.
2. **`AreaTriggerComponent`** → then `HazardComponent` falls out as a subclass, filling three
   shipped-but-scriptless nodes.
3. Genre-shaped components (§5) — **only the ones still marked NEW**, as the genres are taken
   seriously.
4. The `AIController` refactor (§6), if at all.

## What survives as genuinely new

`HazardComponent`, `LapGate`/`LapTracker`, `EquipmentComponent`, `VehicleController`,
`SelectableComponent`, `GridPlacementComponent`, `Match3Input`/`Match3View`,
`CharacterStatsComponent`, `CardDef`. Each was checked against its nearest existing candidate
and the candidate fails **structurally**, not by taste — e.g. `DragComponent.cs:33` does
`GetParent() as Control` and `:84` *moves the node it is attached to*, so it cannot serve unit
selection at 0% overlap; and `Match3BoardComponent`'s grid uses `0` to mean "cleared, refill
me", so `Refill()` would auto-fill a citybuilder's empty lots with random buildings.

## Verification

Per component: `dotnet build` 0 errors, `validate_scenes.sh` PASS, and an editor check that
the thing it unblocks now **actually happens** — e.g. for the group fix: an `AIController`
enemy chases the player, which today it provably cannot.

**For every proposal, one prior check:** grep the nearest existing candidate and state why it
fails. Nine of the original sixteen did not survive that question. It is cheap, and skipping it
is what produced this phase's first draft.
</content>
