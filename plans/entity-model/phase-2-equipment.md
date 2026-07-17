# Phase 2 — Equipment

**Goal:** an entity can hold a `GameWeapon` in MainHand and a shield (`GameArmor`, OffHand),
and something can ask "what does that add to my damage?"

**Depends on:** Phase 1 (`GameEquipment`, `EquipSlot`).

---

## Why a component here, and not more data

Phase 1 says *what a sword is*. Equipment is **state that changes at runtime** — what is
currently held, per entity, restored on load. That is a component's job, not a resource's.

## The pattern to copy

`AttackComponent` already resolves an **optional sibling** and asks it for a number
(`ecs/AttackComponent.cs:51-56`):

```csharp
var statusEffects = GetSiblingComponent<StatusEffectComponent>();
if (statusEffects != null)
    finalDamage *= statusEffects.GetModifier("damage_boost", "damage_multiplier", 1f);
```

`EquipmentComponent` answers the same shape. Consequences worth stating: an entity with no
equipment is **unaffected**, nothing existing breaks, and Phase 3 is additive.

## `ecs/EquipmentComponent.cs`

```
EquipmentComponent : GameplayComponent      [Tool][GlobalClass]

  Equip(GameEquipment item) -> GameEquipment?   // returns what was displaced
  Unequip(EquipSlot slot)   -> GameEquipment?
  Get(EquipSlot slot)       -> GameEquipment?
  GameWeapon? MainWeapon    // convenience — the common query

  [Signal] Equipped(EquipSlot slot, GameEquipment item)
  [Signal] Unequipped(EquipSlot slot, GameEquipment item)

  // The query Phase 3 consumes:
  float DamageBonus                     // sum of equipped GameWeapon.Damage
  float DefenseBonus                    // sum of equipped GameArmor.Defense
  float ResistanceFor(DamageTypeComponent.Type)   // product of equipped multipliers
```

**Slots hold one item each**, keyed by `EquipSlot`. Backed by a
`Dictionary<EquipSlot, GameEquipment>`; expose an `[Export]` array of starting equipment so a
template can ship an entity pre-armed from the inspector.

## Design decisions

- **Pull, not push.** `EquipmentComponent` must *not* write `AttackComponent.Damage` on
  equip. Push creates two owners of one field and goes stale — the same class of bug as the
  `ApplyTheme` re-entrancy already fixed this session. Combat components *ask*, each time.
- **`ResistanceFor` multiplies, `DamageBonus` adds.** Matches the existing semantics:
  `ResistanceComponent`'s per-type values are multipliers where 0 = immune, 2 = weak
  (`ecs/ResistanceComponent.cs:15-20`); `HealthComponent.Armor` is a flat scalar
  (`ecs/HealthComponent.cs:16`).
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

- `dotnet build` → 0 errors.
- Editor: add `EquipmentComponent` to a player, drag `sword_iron.tres` into its starting
  equipment, run — `MainWeapon` is the sword and `DamageBonus` equals its `Damage`.
- Equip a second weapon into the same slot → the first is returned and the bonus does not
  double (the idempotence/cache check).
</content>
