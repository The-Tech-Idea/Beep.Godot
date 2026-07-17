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

A sword is **both** a data problem and a composition problem, and the framework has no way to
express either half.

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
behaviour — and a sword is *both*, because it has two representations.**

- **Definition** — `GameWeapon : Resource` (`.tres`). What stacks, saves, and appears in a
  shop. Not in the scene tree, so it cannot carry components. 99 potions cannot be 99 nodes.
- **Instance** — a node in the world. **This one can carry components**, and often should:
  a *wielded* sword may legitimately have `AttackComponent` (it needs only a `Node2D` parent)
  and `HealthComponent` as durability (it is a blind component; `Died` = it breaks). The
  definition points at it via `[Export] PackedScene? WieldScene`.

This mirrors what the repo already does for bullets: `ProjectileScene` is a `PackedScene`
whose instance carries `ProjectileComponent`. A weapon is the same shape.

**The rule is therefore not "a sword is only data"** — an earlier draft said that and it was
wrong. It is: *give an archetype a component only if **that representation** of it does that
thing.* A sword on the floor doesn't swing; a wielded one does; a save-file row isn't a node.

### One base, with the traits that decide composition

```
GameItem : Resource        Id, Icon, Rarity, MaxStack, WorldScene
                           IsStatic        — stays put (anvil, chest, rock) vs carried
                           IsDestructible  — can be broken   (+ MaxDurability)
  ├── GameEquipment        Slot, WieldScene
  │     ├── GameWeapon     Damage, DamageType, IsRanged, ProjectileScene
  │     ├── GameShield     Defense, BlockChance, Resistances
  │     └── GameArmor      Defense, Resistances
  ├── GameLiquid           Volume, IsDrinkable, HealAmount   (potion, fuel, oil)
  └── GameConsumable       HealAmount, StatusEffectId, Duration
```

`IsStatic` / `IsDestructible` are the load-bearing part. They make the archetype rules
**derivable from the data** rather than remembered from a table:

| | | implies | forbids |
|---|---|---|---|
| anvil | static, indestructible | — | `MovementComponent`, `HealthComponent`, `PickupComponent` |
| rock | static, destructible | `HealthComponent` | `MovementComponent`, `PickupComponent` |
| sword | carried, destructible | `PickupComponent` (grounded), `HealthComponent` (durability) | — |
| potion | carried, indestructible | `PickupComponent` (grounded) | `HealthComponent` |

Four rows, four compositions, no folklore — and a validator can check a `WorldScene` against
its own `GameItem`'s traits (Phase 5).

**Equipment reaches combat through the pattern the codebase already uses.**
`AttackComponent` resolves an *optional* sibling and asks it for a modifier
(`ecs/AttackComponent.cs:51-56`, via `StatusEffectComponent.GetModifier`,
`ecs/StatusEffectComponent.cs:125`). `EquipmentComponent` answers the same shape, so an
entity without equipment is unaffected and nothing existing breaks.

---

## Progress

- [ ] **Phase 1 — Item resources** — `GameItem` hierarchy as `[Tool][GlobalClass] Resource`;
      `InventoryComponent` stores them. → `phase-1-item-resources.md`
- [ ] **Phase 2 — Equipment** — `EquipmentComponent`: slots, equip/unequip, modifier query.
      → `phase-2-equipment.md`
- [ ] **Phase 3 — Damage typing, then combat integration** — **3a blocks 3b.**
      → `phase-3-combat-integration.md`
- [ ] **Phase 4 — Pickups & drops** — carry a `GameItem`, not a string; wire the missing
      `Collected → AddItem` edge. → `phase-4-pickups-and-drops.md`
- [ ] **Phase 5 — Archetypes per genre** — required / optional / **must not have**, and make
      "must not" checkable. → `phase-5-archetypes-per-genre.md`
- [ ] **Phase 6 — Missing components** — ranked by leverage; `EntityTagComponent` first.
      → `phase-6-missing-components.md`

### Per-genre item trees → `items/`

`items/README.md` is the **shared spine** and the rule for what may become a class; one doc
per genre beside it. Read the README first — the genre docs conform to it.

The rule, because it is the whole game: **a class earns existence only by adding a field or
behaviour its parent cannot express.** `sword_iron.tres`, `axe.tres` and `dagger.tres` are
`.tres` files of `GameWeapon` — a `SwordClass` would be a rename. A genre never gets a class
for being that genre; it earns one only by introducing a *kind of thing* the spine cannot
describe.

**Half the genres want no item tree at all.** They want a **spec** — a `.tres` describing a
vehicle, a building, a unit, a card — which is the same idea one level over: data by
inheritance, instanced through a `PackedScene`. Where a genre says "no items", its doc says
what it wants instead rather than forcing it into `GameItem`.

## What the analysis changed

Four parallel audits, hand-verified. Three findings reshaped the plan:

1. **`DamageTypeComponent` is dead** — 0 resolvers; every `TakeDamage` call in the addon uses
   the 1-arg overload, which hardcodes `Physical`. So `ResistanceComponent`'s Fire/Ice/Poison/
   Holy/Dark/Lightning **can never fire**, and an armour's resistances would be decorative.
   This became Phase **3a**, a prerequisite — the original plan assumed a pipeline that isn't
   there.
2. **`AttackComponent.Range` is never read** — melee is a point query at the cursor. So
   `GameWeapon.Range` must **not** be added in Phase 1 until melee hit detection is decided; a
   field that silently does nothing is this repo's signature defect.
3. **The item edges don't exist.** `Pickup.Collected → Inventory.AddItem`: **0 connections**.
   `Died → DropTable.Roll`: never. `Craft()` **deducts materials and grants nothing** — the
   comment reads `// Grant result.` above an `EmitSignal` with no `AddItem`. Building an item
   model on top of these would have produced beautiful data nothing could move.

**Highest leverage overall is not in this initiative:** nothing joins the `"players"` /
`"enemies"` groups, so `AIController`, `TurretComponent` and `ProjectileModifierComponent` are
inert in every genre (Phase 6, §1).

## Decisions

- **`GameItem`, not `BeepItem`.** The repo splits its namespaces: `Game*` is the model
  (`GameInfo`, `GameStateData` — both `Resource`s), `Beep*` is tooling (`BeepFileUtils`,
  `BeepGenreGenerator`). An item is model.
- **Variation by inheritance, never by a component per kind.** One `GameWeapon`, not a
  `SwordComponent` / `AxeComponent` / `BowComponent`. Kinds that differ in *fields* are
  subclasses (`GameShield` has `BlockChance`; `GameLiquid` has `Volume`); kinds that differ
  only in *values* are `.tres` files of the same class.
- **The base carries traits, and the traits drive composition.** `IsStatic` and
  `IsDestructible` on `GameItem` determine what the world instance may be built from — an
  anvil (`static, indestructible`), a rock (`static, destructible`), a sword
  (`carried, destructible`), a potion (`carried, indestructible`) each compose differently,
  and **the data says how**. This is what makes Phase 5 checkable instead of folklore.
- **Two representations.** Definition = `Resource` (stacks, saves, shops). Instance = a node
  (`WorldScene` / `WieldScene`) that **may** carry `AttackComponent`, durability, a hitbox.
  Same shape the repo already uses for bullets (`ProjectileScene` + `ProjectileComponent`).
- **Equipment is an optional sibling.** No entity is required to have it; combat components
  query it if present, exactly as they already query `StatusEffectComponent`.
- **Additive, not a rewrite.** Existing `[Export]`s on `AttackComponent` stay as the base
  values; equipment modifies them. A project with no equipment behaves identically.
- **`MUST NOT HAVE` is part of the contract** — but it is **conditional, not absolute**. A
  sword that cannot break must not have `HealthComponent`; one that can, should. The test is
  always *"does this representation of this thing do that?"*, never *"is it a sword?"*

## Verification

Every phase: `dotnet build` (0 errors) and `templates/scenes/validate_scenes.sh` (PASS).
Neither runs the game — see `CLAUDE.md` § *Testing*. Each phase names its own editor check.
</content>
