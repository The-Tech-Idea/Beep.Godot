# Master TODO Tracker — Entity & Item Model

> Tracks the entity/item/equipment model for `addons/beep_game_builder_cs/`.
> Per-phase detail lives beside this file: `plans/entity-model/phase-N-*.md`.
>
> **Goal:** a Godot developer can model an entity — a player, an enemy, a **sword**, a
> **shield**, a potion — by composing existing components plus **data**, with the framework
> stating which components an archetype needs and which are wrong for it.
>
> **Scope:** framework only. Components, data model, composition guidance, validation.
> Not game content — no balance, no level layout, no assets. See `CLAUDE.md` § *Scope*.

---

## The problem

A sword is not a component problem, it is a **data** problem, and the framework has no way to
say so.

- **There is no weapon, equipment, armor, or shield component.** Verified: nothing in
  `ecs/` matches `weapon|equip|item|gear|armor|shield`.
- **`AttackComponent` bakes the numbers into the wielder** — `Damage`, `Range`, `Cooldown`,
  `IsRanged`, `ProjectileScene` are all `[Export]`s on the attacker
  (`ecs/AttackComponent.cs:13-18`). Equipping a sword cannot change any of them.
- **`PickupComponent` carries a string, not an item** — `[Export] string ItemId = "coin"`
  (`ecs/PickupComponent.cs:13`). A sword on the ground is the *word* "sword"; its damage
  exists nowhere.
- **Items are the only unloved data in the repo.** `InventoryComponent.InventoryItem`
  (`ecs/InventoryComponent.cs:30`) is a **plain nested class** with a stringly-typed
  `Dictionary<string, Variant> Stats` bag — so it cannot be authored in the inspector, saved
  as `.tres`, dragged onto an `[Export]`, or subclassed. Everything else already uses the
  idiomatic pattern: `GameInfo`, `UISkin`, `ColorPalette`, `GeometryProfile`,
  `CraftingRecipe`, `CraftingIngredient`, `QuestObjective` are all
  `[Tool][GlobalClass] : Resource`.

**The receiving half already exists and is idle.** `HealthComponent.Armor`
(`ecs/HealthComponent.cs:16`) and `ResistanceComponent`'s per-type multipliers
(`ecs/ResistanceComponent.cs:15-20`, matching `DamageTypeComponent.Type`) are ready for
armor to drive them. Nothing does.

## The approach

**Classes + inheritance for item data (Godot `Resource` subclasses), components for
behaviour.** A sword is a `BeepWeapon` resource; the thing on the ground is an `Area2D` +
`PickupComponent` holding that resource. It has no `HealthComponent` — it is not alive.

**Equipment reaches combat through the pattern the codebase already uses.**
`AttackComponent` resolves an *optional* sibling and asks it for a modifier
(`ecs/AttackComponent.cs:51-56`, via `StatusEffectComponent.GetModifier`,
`ecs/StatusEffectComponent.cs:125`). `EquipmentComponent` answers the same shape, so an
entity without equipment is unaffected and nothing existing breaks.

---

## Progress

- [ ] **Phase 1 — Item resources** — `BeepItem` hierarchy as `[Tool][GlobalClass] Resource`;
      `InventoryComponent` stores them. → `phase-1-item-resources.md`
- [ ] **Phase 2 — Equipment** — `EquipmentComponent`: slots, equip/unequip, modifier query.
      → `phase-2-equipment.md`
- [ ] **Phase 3 — Damage typing, then combat integration** — **3a blocks 3b.**
      → `phase-3-combat-integration.md`
- [ ] **Phase 4 — Pickups & drops** — carry a `BeepItem`, not a string; wire the missing
      `Collected → AddItem` edge. → `phase-4-pickups-and-drops.md`
- [ ] **Phase 5 — Archetypes per genre** — required / optional / **must not have**, and make
      "must not" checkable. → `phase-5-archetypes-per-genre.md`
- [ ] **Phase 6 — Missing components** — ranked by leverage; `EntityTagComponent` first.
      → `phase-6-missing-components.md`

## What the analysis changed

Four parallel audits, hand-verified. Three findings reshaped the plan:

1. **`DamageTypeComponent` is dead** — 0 resolvers; every `TakeDamage` call in the addon uses
   the 1-arg overload, which hardcodes `Physical`. So `ResistanceComponent`'s Fire/Ice/Poison/
   Holy/Dark/Lightning **can never fire**, and an armour's resistances would be decorative.
   This became Phase **3a**, a prerequisite — the original plan assumed a pipeline that isn't
   there.
2. **`AttackComponent.Range` is never read** — melee is a point query at the cursor. So
   `BeepWeapon.Range` must **not** be added in Phase 1 until melee hit detection is decided; a
   field that silently does nothing is this repo's signature defect.
3. **The item edges don't exist.** `Pickup.Collected → Inventory.AddItem`: **0 connections**.
   `Died → DropTable.Roll`: never. `Craft()` **deducts materials and grants nothing** — the
   comment reads `// Grant result.` above an `EmitSignal` with no `AddItem`. Building an item
   model on top of these would have produced beautiful data nothing could move.

**Highest leverage overall is not in this initiative:** nothing joins the `"players"` /
`"enemies"` groups, so `AIController`, `TurretComponent` and `ProjectileModifierComponent` are
inert in every genre (Phase 6, §1).

## Decisions

- **Data = Resource subclasses, behaviour = components.** One `BeepWeapon` resource, not a
  `SwordComponent`, `AxeComponent`, `BowComponent`.
- **Equipment is an optional sibling.** No entity is required to have it; combat components
  query it if present, exactly as they already query `StatusEffectComponent`.
- **Additive, not a rewrite.** Existing `[Export]`s on `AttackComponent` stay as the base
  values; equipment modifies them. A project with no equipment behaves identically.
- **`MUST NOT HAVE` is part of the contract.** Saying a sword must not have
  `HealthComponent` is as much framework guidance as saying it needs `PickupComponent` — and
  it is checkable (see Phase 5).

## Verification

Every phase: `dotnet build` (0 errors) and `templates/scenes/validate_scenes.sh` (PASS).
Neither runs the game — see `CLAUDE.md` § *Testing*. Each phase names its own editor check.
</content>
