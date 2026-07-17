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

**Recommendation: A now, B when a second consumer appears.** A unblocks the whole equipment
model with a few lines. B is the right end state but is a combat-pipeline rewrite and should
not ride along with the item model. **Decide before writing code.**

### Work (Option A)

1. `AttackComponent.Attack` — resolve optional `DamageTypeComponent` sibling; pass its `Type`
   to the 2-arg `TakeDamage`. Default `Physical` when absent (identical to today).
2. `ProjectileComponent.OnCollision` — same.
3. Keep the 1-arg overload — it is the correct default, not a bug.

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

**1. `AttackComponent`** — beside the existing `StatusEffectComponent` block (`:51-56`),
resolve an optional `EquipmentComponent` and add its `DamageBonus` to the base:

```
finalDamage = (Damage + equipment?.DamageBonus ?? 0) * statusMultiplier
```

Additive base, multiplicative buffs — a +10 sword and a ×1.5 rage buff compose as expected.
`Damage` stays the entity's own contribution (a punch).

**1b. `ShooterController`** — same query, same helper, applied to `ProjectileDamage` before it
is handed to the projectile. Without this the shooter has no equipment at all.

**2. `HealthComponent`** — add `equipment?.DefenseBonus` to `Armor` **at use** (`:89`), not by
writing the field. Writing it would fight the inspector value and go stale.

**3. `ResistanceComponent.ApplyResistance`** (`:41`) — multiply in
`equipment.ResistanceFor(type)`. Product, not sum, matching the existing semantics.

### Rules

- **Optional throughout.** Every lookup is a null-checked sibling. An entity with no
  equipment must behave *exactly* as before — that is what makes this safe.
- **Query, never cache on the consumer.** `AttackComponent` must not copy `DamageBonus` at
  `_Ready`; equipment changes at runtime. Phase 2 caches on the owner, where invalidation is
  possible.
- **Warn, don't no-op.** A `GameWeapon` equipped with no `AttackComponent` to use it →
  `PushWarning` (`CLAUDE.md` § *Never fail silently*).

### Out of scope — record, don't fix

- **`AttackComponent.Range` is never read.** `DealMeleeDamage` is a **point query at the
  cursor** (`:92-93`) — no arc, no reach. So `GameWeapon.Range` cannot work without
  reworking melee hit detection. **Do not add `Range` to `GameWeapon` in Phase 1** until this
  is decided; a field that silently does nothing is this repo's signature defect.
- `GameWeapon.IsRanged`/`ProjectileScene` **replace** rather than add to
  `AttackComponent`'s exports (a bow *is* the range; it doesn't add to a fist). Needs the
  same decision. Damage is the only cleanly additive stat — ship that first.

## Verification

- `dotnet build` → 0 errors.
- Editor, end-to-end: `AttackComponent(Damage=10)` + `EquipmentComponent` vs a dummy with
  `HealthComponent` → 10. Equip `GameWeapon(Damage=+15)` → 25. Give the dummy `GameArmor`
  with `Physical = 0.5` → ~12.5. Unequip → 10.
- **Typing check (3a):** give the attacker a `DamageTypeComponent(Fire)` and the dummy
  `ResistanceComponent(Fire = 0)` → **0 damage**. Today this test is impossible to pass.
- Regression: an entity with **no** `EquipmentComponent` takes exactly what it did before.
</content>
