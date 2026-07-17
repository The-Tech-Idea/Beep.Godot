# GameItem trees — the shared spine

One document per genre lives beside this file. **Read this first** — it defines what may
become a class, and every genre doc conforms to it.

---

## The one rule: class, or `.tres`?

The whole model collapses if this is got wrong, so it is a test, not a taste:

> **A new class earns its existence only by adding a FIELD or a BEHAVIOUR that its parent
> cannot express. Everything else is a `.tres` of an existing class.**

| | Verdict | Why |
|---|---|---|
| `GameWeapon : GameEquipment` | **class** | Adds `Damage`, `DamageType`, `IsRanged` — fields `GameEquipment` has no business carrying |
| `GameShield : GameEquipment` | **class** | Adds `BlockChance` — a real field, not a value |
| `sword_iron.tres`, `sword_steel.tres`, `axe.tres`, `dagger.tres` | **`.tres`** | Same fields, different **numbers**. A `SwordClass` would be a rename of `GameWeapon`. |
| `potion_health.tres`, `potion_mana.tres` | **`.tres`** | Same fields (`HealAmount`, `StatusEffectId`), different values |
| `GameLiquid : GameItem` | **class** | Adds `Volume`, `IsDrinkable` — fuel and lamp oil are liquids you never swallow |

**The smell:** if a proposed class's body would be *only* different default values, it is a
`.tres`. If it would be an empty class with a nicer name, it is a `.tres`.

**The corollary:** a genre never gets a class just for being that genre. There is no
`RpgSword`. A genre earns a class only when it introduces a *kind of thing* the spine cannot
describe — a **card**, a **vehicle spec**, a **building footprint**.

## The spine

Every genre draws from this. Nothing here is genre-specific.

```
GameItem : Resource                     [Tool][GlobalClass]
    Id, DisplayName, Description, Icon, Rarity, MaxStack
    IsStatic        : bool              — stays put (anvil, chest, rock) vs carried
    IsDestructible  : bool              — can be broken
    MaxDurability   : float             — meaningful only when IsDestructible
    WorldScene      : PackedScene?      — how it exists as a node, when it does
    │
    ├── GameEquipment : GameItem        Slot : EquipSlot, WieldScene : PackedScene?
    │       ├── GameWeapon              Damage, DamageType, AttackSpeedMultiplier,
    │       │                           IsRanged, ProjectileScene
    │       ├── GameShield              Defense, BlockChance, Resistances
    │       └── GameArmor               Defense, Resistances
    │
    ├── GameLiquid : GameItem           Volume, IsDrinkable, HealAmount, StatusEffectId
    └── GameConsumable : GameItem       HealAmount, StatusEffectId, Duration
```

## Traits decide composition — not the class name

`IsStatic` and `IsDestructible` are the load-bearing fields. They say what the **world
instance** may be built from, which is why the archetype rules are derivable rather than
remembered:

| | | implies | forbids |
|---|---|---|---|
| anvil | static, indestructible | — | `MovementComponent`, `HealthComponent`, `PickupComponent` |
| rock | static, destructible | `HealthComponent` (as durability) | `MovementComponent`, `PickupComponent` |
| sword | carried, destructible | `PickupComponent` (grounded), `HealthComponent` (durability) | — |
| potion | carried, indestructible | `PickupComponent` (grounded) | `HealthComponent` |

Note it is the **trait**, not the class, that decides. A `GameWeapon` with
`IsDestructible = false` must not carry `HealthComponent`; the same class with
`IsDestructible = true` should.

## The template every genre doc follows

1. **Does this genre need items at all?** An honest, short answer. Three genres do not, and
   saying so is worth more than a padded tree. Resist inventing kinds to fill a table.
2. **The tree** — which spine branches it uses, and its `.tres` set. Concrete: name the
   actual files a developer would author.
3. **New framework classes this genre earns** — each must pass the rule above. If none, say
   none.
4. **Components this implies** — existing ones that already serve it, and new ones the tree
   forces. Cite what exists; do not re-derive.
5. **Content vs framework** — what the developer authors, what we ship. The boundary from
   `CLAUDE.md` § *Scope*.

## What is already known and must not be re-litigated

The audits established these; genre docs should cite, not rediscover:

- **`DamageTypeComponent` is dead** — 0 resolvers; every hit is `Physical`, so per-type
  resistances are currently unreachable (Phase 3a).
- **`AttackComponent.Range` is never read** — melee is a point query at the cursor, so no
  weapon can express reach yet.
- **The item edges do not exist** — `Pickup.Collected → Inventory.AddItem`: 0 connections;
  `Died → DropTable.Roll`: never; `Craft()` deducts materials and **grants nothing**.
- **`DropTableComponent`'s loot list has no `[Export]`** — drops are unauthorable.
- **Nothing joins `"players"` / `"enemies"`** — `AIController`, `TurretComponent`,
  `ProjectileModifierComponent` are inert in every genre.
- **Three false friends:** `PlayerStatsComponent` is a **soccer** stat block;
  `NavigationComponent` is a scene-transition button wirer, not pathfinding;
  `TrainingComponent`/`ContractComponent` are football-manager residue.

## Index

| Genre | Needs items? | Doc |
|---|---|---|
| rpg | heavily — the reference case | `rpg.md` |
| survival | heavily — tools + materials | `survival.md` |
| shooter | yes — weapons | `shooter.md` |
| platformer | lightly — pickups | `platformer.md` |
| topdown | yes — keys, consumables | `topdown.md` |
| racing | no items; needs a **spec** | `racing.md` |
| strategy | no items; needs **specs** | `strategy.md` |
| citybuilder | no items; needs a **spec** | `citybuilder.md` |
| puzzle | **no** — board-driven | `puzzle.md` |
| cardgame | no items; needs a **card** | `cardgame.md` |

> Half of these genres do not want an item tree at all — they want a **spec** (a `.tres`
> describing a vehicle, a building, a unit, a card). That is the same idea one level over:
> data by inheritance, instanced through a `PackedScene`. Where a genre says "no items", the
> doc says what it wants **instead**, and does not force it into `GameItem`.
</content>
