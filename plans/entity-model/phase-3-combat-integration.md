# Phase 3 — Damage typing, then combat integration

**Goal:** the sword hits harder and the shield absorbs. Two steps, and **3a blocks 3b** — an
armour's Fire resistance is meaningless while every hit is Physical.

**Depends on:** Phase 2 (`EquipmentComponent`'s modifier query).

---

## 3a — Damage typing (prerequisite)

### The finding

**`DamageTypeComponent` is dead.** Verified: `GetSiblingComponent<DamageTypeComponent>` /
`FindComponent<DamageTypeComponent>` → **0 resolvers** addon-wide. Its `GetDamage(float)`
(`ecs/DamageTypeComponent.cs:21`) has no callers. Its own doc says "attach to the same node
as an AttackComponent or ProjectileComponent" — and neither ever looks for it.

**So every hit in the framework is `Type.Physical`.** `HealthComponent` has a typed
`TakeDamage(float, Type)` (`ecs/HealthComponent.cs:82`), but every call site in the addon
uses the 1-arg overload (`:77`) which hardcodes Physical:

- `AttackComponent.cs:102` — `health.TakeDamage(damage)`
- `ProjectileComponent.cs:81` — `health.TakeDamage(Damage)`
- `TemperatureComponent.cs:180`, `HealthComponent.cs:74` (passive)

**Consequence:** `ResistanceComponent`'s `Fire`/`Ice`/`Poison`/`Holy`/`Dark`/`Lightning`/`True`
(`ecs/ResistanceComponent.cs:16-22`) can never fire. Only `Physical` is reachable. A
`GameArmor` with fire resistance would be decorative.

### The decision this forces

Damage currently crosses every hop as a **bare float** (`AttackComponent.cs:84`, `:102`;
`ProjectileComponent.cs:81`) — no type, no source, no crit, no on-hit data.

**Option A — minimal.** Attackers resolve an optional `DamageTypeComponent` sibling and call
the 2-arg `TakeDamage(amount, type)`. Small, uses what exists, no new concepts. But the
weapon's type must be pushed onto that component on equip, and the source is still lost.

**Option B — damage packet.** Introduce a `GameDamage` struct/resource (`Amount`, `Type`,
`Source`, `IsCrit`) threaded through `Attack → Projectile → TakeDamage`. Solves type, source
(fixes the projectile self-hit exclusion, `ProjectileComponent.cs:31`/`:70`), on-hit effects,
and damage numbers in one move. Larger, and touches every combat signature.

**Decided: B.** An earlier draft recommended "A now, B later" **because B touches every combat
signature**. That is not a cost we carry — this is dev code with no shipped consumers, and no
fallbacks or stubs are wanted. With the only objection void, A is just B with the useful parts
missing.

B fixes in one move what A leaves broken:

- **type** — A does this too;
- **`Source`** — repairs the projectile self-hit exclusion *properly*, replacing the `Shooter`
  property bolted on this session (`ProjectileComponent.cs:31`);
- **crit / on-hit data** — A has nowhere to put either;
- and it kills the **bare-float handoff** (`AttackComponent.cs:84`, `:102`;
  `ProjectileComponent.cs:81`) that loses everything except a number.

### Work (Option B)

1. **`GameDamage`** — `Amount`, `Type`, `Source`, `IsCrit`. Threaded through
   `Attack → Projectile → TakeDamage`.
2. **`HealthComponent.TakeDamage(GameDamage)`** replaces both overloads.
3. **Delete the 1-arg `TakeDamage(amount)` overload.** It exists to default to `Physical`, and
   it is *the reason* every hit in the framework is Physical — a convenience default that
   silently ate the entire type system. Make the type explicit at every call site; there are
   four (`AttackComponent.cs:102`, `ProjectileComponent.cs:81`, `TemperatureComponent.cs:180`,
   `HealthComponent.cs:74`).
4. **`DamageTypeComponent`** — with the weapon carrying `DamageType` (Phase 1) and the packet
   carrying it to the target, the component has no job left. Delete it rather than leave a
   third dead thing in `ecs/`. *(Its `Type` enum stays — `GameWeapon` and `ResistanceComponent`
   both reference it.)*

---

## 3b — Equipment reaches combat

### The pipeline, verified

1. `AttackComponent.cs:50` — `float finalDamage = Damage;` ← **weapon injects here**
2. `:51-56` — optional `StatusEffectComponent` multiplier
3. → `HealthComponent.TakeDamage(amount, type)` (`:82`)
4. `:86` — optional `ResistanceComponent` scales **per type** (0 = immune, 2 = weak)
5. `:89-90` — `Armor` reduces: `actual = amount * (1 - Clamp(Armor,0,MaxArmor)*0.01)`
   ← **armour injects here**
6. `:92-94` — `StatusEffectComponent` `damage_reduction`
7. `:96-99` — apply, `Damaged`, `Died`

The receiving half is **built and idle**: `HealthComponent.Armor` (`:16`) and
`ResistanceComponent`'s per-type floats. Nothing writes either.

### ⚠ There are TWO damage paths, not one — the shooter bypasses `AttackComponent` entirely

An earlier draft of this phase assumed all damage flows through `AttackComponent`. **It does
not.** `ShooterController` spawns projectiles itself (`SpawnProjectile`) and holds **no
reference to `AttackComponent`** — verified: its only sibling lookup is
`StatusEffectComponent` (`ecs/ShooterController.cs:43`), consulted for **`speed_boost` only**
(`:55`). Damage is never modified by anything.

So "`AttackComponent` queries `EquipmentComponent`" would leave the **entire shooter genre
inert** — the one genre whose central noun is a weapon.

**Both paths must query.** Put the lookup in one place (a small shared helper, or a protected
method on `GameplayComponent`) and call it from `AttackComponent.Attack` **and**
`ShooterController.SpawnProjectile`. Two copies of the same query will drift; this repo has
the receipts (`ApplyTuning` forked between the generator and `BeepGenreScene` and silently
dropped 14 of 21 keys).

**Second shooter constraint:** `ShooterController._Ready` does
`MoveSpeed = info.MoveSpeed; FireRate = info.FireRate` (`:45`) — **unconditionally, from
`GameInfo`**. That is a *per-project* value overwriting a *per-weapon* one, so a `GameWeapon`
setting `FireRate` is clobbered on every scene load. Fix it as part of this phase: make the
`GameInfo` values a **fallback** (apply only when the export is unset), not an override.
Otherwise weapon fire-rate silently cannot work, which is the exact defect class this plan
keeps finding.

### Work

**Consumers read the entity's `Stat`, not `EquipmentComponent`.** Phase 2 replaced the
three-accessor design; nothing queries equipment. This also **supersedes the shared-helper fix
above**: a `Stat` owned by the entity is read by whoever computes damage, so `AttackComponent`
and `ShooterController` need no common query and *cannot* fork.

**1. `AttackComponent`** — `finalDamage` comes from the `damage` `Stat`. The `[Export] Damage`
becomes that stat's `BaseValue` (the entity's own contribution — a punch). The
`StatusEffectComponent` block at `:51-56` is deleted: buffs are modifiers on the same stat now.

**2. `ShooterController`** — same, for `ProjectileDamage`. Reads the stat directly; there is no
`AttackComponent` in its path and now it doesn't need one.

**3. `HealthComponent`** — `Armor` becomes a `Stat`. The `damage_reduction` status lookup at
`:92-94` is deleted for the same reason.

**4. `ResistanceComponent`** — per-type values become `Stat`s (`resist_fire`, …), so armour
contributes `{resist_fire, Multiply, 0.5}` rather than mutating an `[Export]` float. This is
what lets **two armour pieces both contribute and cleanly withdraw** — impossible today.

### Melee has no reach — fix it, don't design around it

`AttackComponent.Range` is never read: `DealMeleeDamage` is an **`IntersectPoint` point query at
the cursor** (`:92-93`). No arc, no reach. An earlier draft said *"do not add `Range` to
`GameWeapon` until this is decided"* — that was avoidance, and it was premised on not touching
existing signatures, which we are free to do.

**Replace the point query with a real hitbox** (`Area2D` + reach), and `Range` means something.
This is the same missing primitive as `HazardComponent` (Phase 6 §2) — build it once. It is also
the model the user described: *a wielded sword carries `AttackComponent` and a hitbox, and hits
what it touches.* A weapon that cannot express reach is not a weapon.

Consequently **`GameWeapon` gets `Range`** (Phase 1), gated on this fix landing — not omitted.

`GameWeapon.IsRanged`/`ProjectileScene` **replace** the controller's exports rather than adding
to them: a bow *is* the range, it doesn't add reach to a fist. Same for `FireRate` —
`ShooterController._Ready` currently overwrites it from `GameInfo` unconditionally (`:45`), so
the weapon can never own it. The weapon wins when equipped; `GameInfo` is the project default.
No conditional-fallback dance.

### Rules

- **The entity owns the stats.** Every consumer reads them; none of them own them.
- **Warn, don't no-op.** A `GameWeapon` equipped with nothing able to use it → `PushWarning`
  (`CLAUDE.md` § *Never fail silently*).
- **An entity with no modifiers behaves exactly as today** — `Stat.Value` is `BaseValue`.

## Verification

- `dotnet build` → 0 errors.
- Editor, end-to-end: `AttackComponent(Damage=10)` + `EquipmentComponent` vs a dummy with
  `HealthComponent` → 10. Equip `GameWeapon(Damage=+15)` → 25. Give the dummy `GameArmor`
  with `Physical = 0.5` → ~12.5. Unequip → 10.
- **Typing check (3a):** give the attacker a `DamageTypeComponent(Fire)` and the dummy
  `ResistanceComponent(Fire = 0)` → **0 damage**. Today this test is impossible to pass.
- Regression: an entity with **no** `EquipmentComponent` takes exactly what it did before.
</content>
