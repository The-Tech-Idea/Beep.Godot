# Combat — 22 components

Health, damage, attack, AI targeting, projectiles.

> **Read fact 2 first (`README.md`).** Nothing in any shipped scene can deal damage. Eight of the
> components below are gated behind that single fact, and fixing them in any other order is
> wasted work. **Damage first.**

---

## The damage pipeline, as built

`HealthComponent.TakeDamage` is the hub, and the pipeline behind it is complete and correct:

1. `HealthComponent.cs:82` — `TakeDamage(float, Type)`
2. `:86` — optional `ResistanceComponent` scales **per type** (0 = immune, 2 = weak)
3. `:89-90` — `Armor` reduces: `actual = amount * (1 - Clamp(Armor,0,MaxArmor)*0.01)`
4. `:92-94` — `StatusEffectComponent` `damage_reduction`
5. `:96-99` — apply, emit `Damaged`, emit `Died`

**Every stage past step 1 is unreachable.** All callers use the 1-arg overload (`:77`) which
hardcodes `Physical`, and all three callers are themselves dead. The receiving half is built and
idle.

---

## ALIVE

### `HealthComponent` — the hub, and it can only ever be full
**Evidence:** 6 scenes (`platformer_main`, `topdown_main`, `shooter_main`, `player_template`,
`enemy_template`, `robot_npc_template`); 18 xrefs. `_Process:51` applies passive temp/hunger
damage. `Died` (`:99`) has exactly **one** subscriber (`AutoHealComponent.cs:41`).

**Defects:**
- `_resistance` path (`:86`) is dead — no `DamageTypeComponent` resolver exists, so every hit is
  `Physical` (`:77`) and 7 of 8 resistance types are unreachable.
- The **1-arg `TakeDamage(amount)` overload (`:77`) is the root cause.** It exists to default to
  `Physical`, and that convenience default silently ate the entire type system.

**Fix (L):** Phase 3a — `GameDamage` packet (`Amount`, `Type`, `Source`, `IsCrit`) threaded
`Attack → Projectile → TakeDamage`. **Delete the 1-arg overload**; make type explicit at all four
call sites. `Armor` becomes a `Stat` (Phase 2); delete the `:92-94` status lookup.

### `ProjectileComponent`
**Evidence:** `projectile_template.tscn`. Called by `ShooterController.cs:93-102` (live),
`TurretComponent.cs:121-126` (inert), `AttackComponent.cs:85` (dead). Shooter self-hit exclusion
(`:68-72`) is solid. Parent contract: **`Area2D`**, and it `PushError`s on mismatch — the only
loud one in the addon.

**Defects:** dead binding at `:84` — `n is Node2D hitNode` binds `hitNode` and never uses it.
`Shooter` (`:31`) was bolted on this session and is superseded by `GameDamage.Source`.

**Fix (S):** drop the dead binding. **(L, Phase 3)** replace `Shooter` with `GameDamage.Source`.

### `SpawnerComponent` — the one live spawn path
**Evidence:** `levels/shooter/level_1.tscn:26`, `level_2.tscn`, with
`SpawnScene = enemy_template.tscn`. `_Process:35`. **`inst.AddToGroup(SpawnGroup)` (`:73`) groups
the spawned body** — a `Node2D`, exactly what the targeting components filter for.

**Defect:** `SpawnGroup` defaults to `"spawned"` (`:19`), not `"enemies"`, and `level_1.tscn:27`
doesn't override it. **This one word is why homing and turrets find nothing.**

**Fix (S):** default `"enemies"`. **Highest leverage-to-effort ratio in the addon.**

---

## ALIVE (crippled)

### `StatusEffectComponent` — every read of it returns a constant
**Evidence:** resolved at 5 sites (`HealthComponent.cs:47`, `PlatformerController.cs:41`,
`TopDownController.cs:30`, `ShooterController.cs:43`, `JumpComponent.cs:53`). Its only live
producer is `HungerStaminaComponent.cs:148,162` — itself inert.

**Defects — three, compounding:**
1. **`ApplyEffectWithModifiers` (`:92`) has 0 callers** → `Modifiers` is always empty →
   `GetModifier` (`:125`) always returns `defaultValue` → **every `speed_multiplier` /
   `armor_multiplier` read in the framework is a constant `1f`.** All four read sites are dead
   code that looks live.
2. **No permanent mode, and no turn mode.** `_Process:182` does `e.Duration -= delta`
   unconditionally and `:191 IsExpired => Duration <= 0`. No infinite sentinel — the codebase fakes
   it with `999f` (`HungerStaminaComponent.cs:148`), i.e. ~16 minutes. **And `delta` hardcodes real
   seconds**, so a cardgame/strategy effect lasting *3 turns* cannot be expressed at all — 2 of the
   10 genres. Subscribe to **`GameClock.Ticked`** instead of `_Process` (Phase 7); `Duration`
   becomes clock units and both work with no branch.
3. **No aggregate query.** `GetModifier(effectId, key)` requires knowing the *effect id*, so you
   cannot ask "total speed multiplier across all effects". That is *why* it is consulted at two
   hardcoded sites with magic strings (`AttackComponent.cs:54` — `"damage_boost"`,
   `"damage_multiplier"`).

**Fix (S then L):** guard `:182`/`:191` on `Duration < 0` → permanent (2 lines; this **is** the
proposed `StatModifierComponent`, and it fills the shooter's missing upgrade channel). Add
`GetTotalModifier(key)`. **Then (Phase 2 + Phase 7, one change)** refactor onto `Stat`/`StatModifier`
**and** move from `_Process` to `GameClock.Ticked` — its nested `ActiveEffect` (`:27`) with
`Dictionary<string,float> Modifiers` (`:37`) is the same not-inspector-authorable defect as
`InventoryItem`.

> **Do the `Stat` refactor and the clock switch in the same change.** Both touch the only two live
> effects in the framework; doing them separately puts that regression surface through twice.

> **Regression risk — the only two live effects in the framework are `speed_boost` and
> `damage_reduction`.** They are the regression test. Freedom to change is not freedom to regress
> silently.

---

## INERT — missing wire

### `AttackComponent` — nothing ever attacks
**Evidence:** `enemy_template.tscn:35`, `player_template.tscn:5`. **`Attack()` (`:45`) has 0
callers.** `_Process:36` only ticks a cooldown. Parent: `Node2D` (`:33`) — *not*
`CharacterBody2D`, so **it can live on a sword**.

**Defects:**
- **`Range` (`:14`) is never read.** `DealMeleeDamage` is an `IntersectPoint` **point query at a
  passed position** (`:92-93`) — no arc, no reach. It hits where you clicked.
- `:99` resolves **only** `HealthComponent` → `DestructibleComponent` can never be hit.
- Inline cooldown (`:15,:23,:36-43`) duplicates the unused `CooldownComponent` exactly.
- **Fact 4:** no player body in any genre main carries one.

**Fix (M/L):** wire `AIController.InAttackRange → Attack(target)`; add to the player + an input
edge. Replace the point query with an `AreaTriggerComponent`-derived hitbox → **`Range` becomes
real** and `GameWeapon.Range` lands with it. Resolve `DestructibleComponent` too (or unify it
behind Health — see below). Delete the inline cooldown; use the sibling.

### `AggroComponent` — threat table permanently empty
**Evidence:** `enemy_template.tscn:29`. `_Process:53` only decays. **`AddThreat` (`:27`) has 0
callers** → `ThreatTable` always empty → `CurrentTarget` always null → `TargetAcquired` never
fires. `AggroRange`/`DeaggroRange` are never read in the file — pure decoration.

**Fix (S, gated on damage):** `HealthComponent.Damaged → AddThreat`, plus a consumer
(`AIController.Mode = Chase`).

### `DestructibleComponent` — every destructible in every genre is invulnerable
**Evidence:** **`TakeDamage(int)` (`:31`) has 0 callers.** The exact edge:
`AttackComponent.cs:99` and `ProjectileComponent.cs:78` resolve **only** `HealthComponent`.
`Break()` (`:39`) also depends on the broken `DropTableComponent` (`:55`).

**The defect is its own HP.** `[Export] public int HP` (`:14`) + `_currentHP` (`:22`) is a
**second, parallel pool** with its own `Damaged` signal — and it is the one nothing can reach.

**Fix (M):** **unify behind `HealthComponent`.** Delete the private `int HP`; give it a sibling
`HealthComponent` and listen to `Died → Break()`. Every existing damage source then reaches
destructibles **for free**.

> **This resolves a contradiction inside the plan.** `phase-5` said an archetype MUST NOT carry
> `HealthComponent` *because* of the duplicate pool. That protected the broken pool and banned the
> working one. Delete the duplicate instead.

### `KnockbackComponent`
**Evidence:** 3 xrefs, **all from unreachable call sites** (`AttackComponent.cs:104`,
`ProjectileComponent.cs:85`); resolved by `PlatformerController.cs:42`. 0 scenes, so
`FindComponent<KnockbackComponent>` always returns null.

**Defect:** `:49` does `_body.Velocity += _knockbackVelocity; MoveAndSlide()` in the **same frame**
as a controller's own `MoveAndSlide` → **double-move**.

**Fix (S/M):** add to enemy/player templates; remove its `MoveAndSlide` and let the controller own
locomotion.

### `CooldownComponent` — correct, complete, and beaten by three copies of itself
**Evidence:** **0 references repo-wide.** `Trigger()` (`:44`), `IsReady` (`:21`),
`CooldownReady` (`:17`), self-ticking `_Process` (`:31`), stackable one-per-ability by design
(`:6-8`). Meanwhile three components rolled their own: `ShooterController.cs:34,64`,
`TurretComponent.cs:31,69-72`, `AttackComponent.cs:23,36-42`.

**Fix (M):** **invert the redundancy — keep this, delete the three inline timers.** Its
`Progress` (`:23`) gives a free radial-cooldown UI hook the copies lack, and `GameWeapon.Cooldown`
then has one place to land.

### `AutoHealComponent`, `FlashComponent`, `HitSparkComponent`
**Evidence:** all three self-wire correctly to a sibling `HealthComponent.Damaged`/`Died`
(`:40-41`, `:39`, `:29`). `FlashComponent:66` even holds the delegate so `_ExitTree` detaches
properly — genuinely correct code. **0 scenes, and the signal can never fire anyway (fact 2).**

**Defects:** `FlashComponent._flashMaterial` (`:21`) assigned at `:33`, never read — `Flash()`
tweens `modulate`. `HitSparkComponent` needs `SparkScene` set or `:37` returns immediately.

**Fix (S, gated on damage):** add to `enemy_template`/`player_template`; drop `_flashMaterial`;
ship a default spark scene (a null export that disables a shipped feature must warn or have a
default — `CLAUDE.md`).

---

## INERT — missing driver

### `AIController` — no brain ships in the one template that needs it
**Evidence:** `_PhysicsProcess:46`; 0 refs outside its own file. **`enemy_template.tscn:18-60`
ships Health/Aggro/Attack/Movement and no brain node.** `Chase`/`Flee` call
`FindNearestInGroup("players")` (`:88`, `:131`) — **nothing joins that group**; the filter at
`:139-145` requires `node is Node2D`.

**Fix (S + M):** add the node to `enemy_template`; join the player to `"players"`
(`EntityComponent` parent-group mode). Only `Wander` works without the group fix.

**Refactor (S — and it is ~2 lines, not "bigger"):** it self-drives (`_body.Velocity = …;
MoveAndSlide()`, `:62-63`), which is why it is mutually exclusive with `MovementComponent`. Have
it write `DesiredDirection` instead — **the receiving half already exists and is documented for
exactly this**: `MovementComponent.cs:45` is a public settable `Vector2 DesiredDirection`,
consumed at `:74`, and its doc at `:21` calls it *"the one an AI can steer by setting
DesiredDirection."* This dissolves the AI-vs-Movement conflict in every genre. **Schedule it.**

### `TurretComponent`
**Evidence:** `_PhysicsProcess:42`. `TargetGroup = "players"` (`:17`) — nothing joins;
`AcquireTarget` (`:77`) always finds nothing; filter at `:83-87` requires `Node2D`. Its `_pool`
path (`:104-109`) depends on the broken `ObjectPoolComponent`. 0 scenes.

**Fix (S):** the group fix. Then it works.

### `ProjectileModifierComponent`
**Evidence:** `_PhysicsProcess:38`. `TargetGroup = "enemies"` (`:22`) — nothing joins.

**Defect:** **`Spread` (`:18`) is in the enum with no case in the switch (`:42-76`)** → silently
falls through `default:` to `Straight`. A shipped option that does nothing, with no warning.

**Fix (S):** `SpawnGroup = "enemies"` makes homing work. Implement `Spread` or delete it from the
enum — **do not leave it selectable.**

### `ResistanceComponent`
**Evidence:** resolved by `HealthComponent.cs:48`, applied at `:86`. Per-type multipliers
(`:15-22`) keyed to `DamageTypeComponent.Type`. **0 resolvers for that type → every hit is
`Physical` → 7 of 8 types unreachable.** `ResistanceBroken` (`:24`) never emitted. 0 scenes.

**Fix (L, Phase 3a):** the `GameDamage` packet. Then (Phase 3b) per-type values become `Stat`s
(`resist_fire`…), so two armour pieces can both contribute and cleanly withdraw — impossible today.

### `LevelingComponent`
**Evidence:** `AddXp` (`:31`) and `SpendPoints` (`:51`) both 0 callers. `StatPoints` (`:27`)
accumulates with **no destination** — there is no general stat block (`PlayerStatsComponent` is
soccer). 0 scenes.

**Fix (M):** `HealthComponent.Died → AddXp` (gated on damage), and
`CharacterStatsComponent` as the destination.

### `ObjectPoolComponent` — BROKEN, see below
### `StateMachineComponent` — BROKEN, see below

---

## BROKEN

### `ObjectPoolComponent` — the cap never caps; allocates forever
**Evidence:** **`Release()` (`:59`) has 0 callers.** `_poolCount` is incoherent: `Expand`
increments (`:40`), `Get` **decrements** (`:52`), `Release` increments (`:65`) — so it tracks
*idle* instances, not *total allocated*, and the `_poolCount >= MaxSize` guard (`:34`) **never
caps anything**. With nothing calling `Release`, `Get` (`:45`) instantiates unbounded forever.
Only consumer is `TurretComponent.cs:106`, itself inert.

**Fix (M):** separate `_totalAllocated` from `_pool.Count`; wire `LifetimeComponent.Expired →
Release` — or, since `LifetimeComponent` is deleted, `ProjectileComponent`'s own lifetime expiry.

### `StateMachineComponent` — cannot be configured from a scene at all
**Evidence:** `AddState`/`AddTransition`/`Start` (`:69`, `:81`, `:88`) take **`Action` delegates**
→ **C#-only; a `.tscn` cannot configure it.** All 0 callers. `InitialState` (`:24`) is only read
in `Load()` (`:153`) — it never auto-starts. **`:158` does `(float)timeObj` on a `Variant` — an
invalid cast that will throw at runtime.**

**Fix (M):** fix `:158` → `timeObj.AsSingle()`; auto-`Start(InitialState)` in `_Ready`. Accept
that configuration is code-side, and **say so in the doc comment** — a `[GlobalClass]` component
that cannot be configured in the editor is a trap otherwise.

### `HitStopComponent` — stomps the global TimeScale
**Evidence:** `_Process:47`. `:51` does `_freezeTimer -= 0.016f` on a loop frozen by
`Engine.TimeScale = 0` (`:43`) — it does unfreeze, but **`Engine.TimeScale = 1f` (`:55`) is an
unconditional latch.** Anything else owning TimeScale (`PauseComponent`, slow-mo) gets stomped to
1. Two instances on screen fight over the global.

**Fix (S):** save the prior value on freeze, restore it on thaw. Never latch a global to a
literal.

---

## REDUNDANT / DELETE

### `DamageTypeComponent` — the node dies, the enum lives
**Evidence:** **0 resolvers.** `GetDamage()` (`:21`) 0 callers, `Multiplier` (`:18`) never read,
`DamageDealt` (`:23`) never emitted. Its own doc says "attach to the same node as an
AttackComponent or ProjectileComponent" — and **neither ever looks for it.**

**Fix (M):** **delete the node class.** **Keep the enum** — `HealthComponent.cs:77,82` and
`ResistanceComponent.cs:24-41` depend on it, and `GameWeapon.DamageType` will. Demote to a plain
enum file + `[Export] DamageType` on the attacker. → `DELETE.md`

### `EntitySystem` — the "S" of this ECS was never built
**Evidence:** abstract (`:16`); **0 subclasses** repo-wide (verified `grep ": EntitySystem"`).
`ProcessAll` (`:64`) appears only in its own declaration and its own docstring (`:12`).
`TrackedGroup`/`GetEntities`/`GetComponent(s)` reach nothing. Components all self-drive via
`_Process`.

**Fix:** delete. Also delete the football residue in its doc (`:21`, `"training_players"`).
→ `DELETE.md`

---

## Order

1. **`SpawnGroup = "enemies"`** (S) — un-inerts homing/turrets today.
2. **`EntityComponent` parent-group mode** (S) — un-inerts `AIController` targeting.
3. **Damage: `GameDamage` packet + delete the 1-arg overload** (L) — **gates 8 components**.
4. `DestructibleComponent` unify behind Health (M) — every damage source reaches it free.
5. `StatusEffectComponent` permanent guard (S), then `Stat` refactor (L).
6. `AttackComponent` hitbox (M) — makes `Range` and `GameWeapon.Range` real.
7. The wires: Aggro, AutoHeal, Flash, HitSpark, Knockback (S each, after 3).
8. `CooldownComponent` inversion (M); `ObjectPool`, `HitStop`, `StateMachine` fixes (S/M).
