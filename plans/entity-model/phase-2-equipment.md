# Phase 2 — Equipment

**Goal:** an entity can hold a `GameWeapon` in MainHand and a shield (`GameArmor`, OffHand),
and something can ask "what does that add to my damage?"

**Depends on:** Phase 1 (`GameEquipment`, `EquipSlot`).

---

## Why a component here, and not more data

Phase 1 says *what a sword is*. Equipment is **state that changes at runtime** — what is
currently held, per entity, restored on load. That is a component's job, not a resource's.

## The pattern to copy

## 2a — `Stat` + `StatModifier` (do this first)

An earlier draft had `EquipmentComponent` expose three hardcoded aggregates — `DamageBonus`,
`DefenseBonus`, `ResistanceFor(type)` — which Phase 3 would read at three hardcoded sites.
**Rejected.** Every new stat would need a new accessor *and* an edit to every consumer — the
same shape as `StatusEffectComponent`, which the audit found is "consulted at exactly two
hardcoded sites with two hardcoded key pairs". Adding "crit chance" later would touch five
files.

The [community pattern](https://minoqi.medium.com/modular-stat-attribute-system-tutorial-for-godot-4-0bac1c5062ce)
is a generic stat with a modifier list:

```
Stat : Resource                         [Tool][GlobalClass]
    Id            : StringName          — "damage", "armor", "move_speed"
    BaseValue     : float
    Modifiers     : StatModifier[]
    Value         : float               — recalculated, cached, not stored per-consumer
    [Signal] Changed(float value)

StatModifier : Resource                 [Tool][GlobalClass]
    Stat     : StringName
    Op       : Add | Multiply
    Amount   : float
    Duration : float                    — 0 = permanent
    Source   : Variant                  — WHO added it (see withdrawal, below)
```

**Duration is what unifies the systems.** A sword's `+10` is `duration 0`; a rage buff's `×1.5`
is `duration 5`. One list, one recalculation, one signal — so equipment, timed buffs and
permanent upgrades stop being three mechanisms.

This resolves two gaps the audits found and the accessor design could not:

1. **The shooter's missing permanent-modifier channel.** `LevelUpChoice` records a pick and
   says applying it "is the game's job", because `StatusEffectComponent` is timed-buff-shaped
   and `ShooterController` consults it for **speed only**. A `duration 0` modifier *is* a
   permanent upgrade.
2. **The two-damage-paths blocker.** `ShooterController` bypasses `AttackComponent` entirely,
   so a query bolted onto `AttackComponent` would leave the whole shooter genre inert. A `Stat`
   owned by the **entity** is read by whoever computes damage — both paths, no shared helper,
   nothing to fork. (`ApplyTuning` forked once already and silently dropped 14 of 21 keys.)

### Withdrawal is the hard part

The accessor design got removal for free (recompute the sum). A modifier list does not: on
unequip you must remove **exactly** the modifiers that item added. Hence `Source` — remove by
identity, never by value-matching (`Amount == 10` would strip an unrelated +10 from a buff).

### 2b — refactor `StatusEffectComponent` onto `Stat`

**Replace its API, don't shim it.** `GetModifier("damage_boost", "damage_multiplier", 1f)` is a
bag of magic strings and the weaker half of what `Stat` does; keeping it would leave two
modifier channels forever, and a consumer that forgets one is a silent bug — this repo's
signature defect. Its three call sites move in the same change:
`AttackComponent.cs:51-56`, `ShooterController.cs:55`, `HealthComponent.cs:92-94`.

`StatusEffectComponent` becomes a thin **producer** of timed modifiers: applying an effect adds
`StatModifier`s with a duration and a `Source` of that effect; expiry removes them by source.

> This is the one step that touches code which currently works — `speed_boost` and
> `damage_reduction` are the only two live effects in the framework. They are the regression
> test (see Verification). Freedom to change is not freedom to regress silently.

## 2c — `ecs/EquipmentComponent.cs`

```
EquipmentComponent : GameplayComponent      [Tool][GlobalClass]

  Equip(GameEquipment item) -> GameEquipment?   // returns what was displaced
  Unequip(EquipSlot slot)   -> GameEquipment?
  Get(EquipSlot slot)       -> GameEquipment?
  GameWeapon? MainWeapon    // convenience — the common query

  [Signal] Equipped(EquipSlot slot, GameEquipment item)
  [Signal] Unequipped(EquipSlot slot, GameEquipment item)
```

**It contributes modifiers; it does not answer queries.** On equip it adds the item's
`StatModifier`s (`Source` = the item) to the entity's `Stat`s; on unequip it removes them by
source. It exposes no `DamageBonus` — nothing asks it anything.

**Slots hold one item each**, keyed by `EquipSlot`. Backed by a
`Dictionary<EquipSlot, GameEquipment>`; expose an `[Export]` array of starting equipment so a
template can ship an entity pre-armed from the inspector.

## Design decisions

- **The entity owns its stats.** Not `EquipmentComponent`, not `AttackComponent` — otherwise
  the shooter (which has no `AttackComponent`) has nowhere to look.
- **Add vs multiply is on the modifier, not the call site.** A sword is `{damage, Add, 10}`; a
  rage buff is `{damage, Multiply, 1.5}`. That preserves the existing semantics —
  `ResistanceComponent`'s per-type values are multipliers where 0 = immune, 2 = weak
  (`ecs/ResistanceComponent.cs:15-20`), `HealthComponent.Armor` is a flat scalar (`:16`) —
  without hardcoding either into a consumer.
- **Recalculate on change, not per read.** `Stat.Value` is cached and invalidated by
  add/remove/expiry. Must be idempotent: recomputing twice yields the same number
  (`ApplyTheme` was not, and that cost a regression this session).
- **Cache, invalidate on change.** Recompute the three aggregates on Equip/Unequip only —
  not per attack. Guard the recompute so it is safe to call repeatedly (idempotent, per
  `CLAUDE.md`).
- **No parent-type requirement.** Equipment is data-on-an-entity; it works on any node. Do
  **not** add a silent `GetParent() as X` — that is the repo's dominant defect class.

## `ISaveable`

Implement it (`ecs/IGameStateable.cs`). Persist **slot → item id**, and re-resolve on load —
same rule as Phase 1's inventory. Join the `saveables` group only when
`ParticipatesInSave` is on, matching `HealthComponent`/`InventoryComponent`
(`ecs/HealthComponent.cs`, the group is `SaveableHelper.Group`).

> Note the existing collision hazard: `GameStateData` is player-centric with single slots,
> so only the player's components should opt in. Equipment for many entities needs the
> per-entity save keying that is still an open question.

## Verification

`dotnet build` → 0 errors, `validate_scenes.sh` → PASS. **Neither runs the game** — the
editor checks below are the real ones.

**Regression — 2b touches the only two live effects in the framework.** Freedom to change is
not freedom to regress silently:

- an entity with a `speed_boost` status moves **exactly** as it does today
  (`ShooterController.cs:55`);
- an entity with `damage_reduction` takes **exactly** the damage it does today
  (`HealthComponent.cs:92-94`).

If either changes, the refactor broke something that worked.

**New capability — prove it, or the churn wasn't worth it:**

- equip `sword_iron.tres` (`{damage, Add, 10}`) → the `damage` stat rises by 10;
- **unequip → it returns to exactly `BaseValue`.** This is the one the design can leak:
  removal is by `Source`, so a modifier removed by value-matching would strip an unrelated +10.
  Test with a rage buff *and* a sword both contributing +10, then unequip only the sword.
- equip a second weapon into the same slot → the first is returned and the bonus **does not
  double** (idempotence/cache).
- a `duration 0` modifier survives; a `duration 5` one expires and its stat returns to base —
  the same list, two lifetimes. That is the whole point of 2a.
</content>
