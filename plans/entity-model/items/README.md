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

## Validated against Godot community practice

This model was designed from first principles plus evidence inside this repo — neither of which
tells you whether it is what a **Godot developer** expects to find. It was then checked against
external practice. **The spine is confirmed; don't re-litigate it:**

| Our decision | External practice |
|---|---|
| Components are **Nodes**, composed on an entity | Idiomatic. [`HealthComponent`/`HitboxComponent` are the canonical examples](https://www.gotut.net/composition-in-godot-4/) — the names this repo already uses. A true ECS is [essentially impossible in vanilla Godot](https://forum.godotengine.org/t/question-about-composition-inheritence/98065); pseudo-composition is the norm. |
| Items are **`Resource` subclasses**, authored as `.tres` | ["The best way to define items"](https://uhiyama-lab.com/en/notes/godot/inventory-system/) — a node-less template usable anywhere. |
| `GameItem` → `GameWeapon` **by inheritance** | The standard shape is a base `ItemData` with [`WeaponData extends ItemData`](https://codingquests.io/blog/godot-4-custom-resources-tutorial) adding `attack_bonus`/`weapon_type`. Ours is a rename of theirs. |
| Save **id + quantity**, re-resolve on load | Near-verbatim: ["use Resources to author game data, but save player progress as plain JSON containing only the values you need, like ids and quantities"](https://www.strayspark.studio/blog/godot-4-inventory-crafting-system-complete-guide). Phase 1 reached this from first principles. |
| Variation by `.tres`, not by class | Confirmed — one `.tres` per item, the path itself acting as identity. |

**The sharing trap is worse than it looks, and the obvious fix is broken.** Per-instance state
(durability, enchantments) cannot live on a shared `.tres`. Godot's built-in answer,
`resource_local_to_scene`, has a [known engine bug (godot#45350)](https://github.com/godotengine/godot/issues/45350):
duplicating an already-instantiated scene makes the copies **share** the resource anyway, and
setting the flag at runtime does nothing to resources already created. It is exactly what a
developer will reach for, and it will not hold. Per-instance state lives on the **instance**.

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
- **`WorkComponent` has never run.** `StartWork`/`Tick` have **0 callers** and it has no
  `_Process`, so it does not tick itself. A correct production model (furnaces, workbenches,
  labs) that has never executed — needs wiring, not design.
- **A real A\* already exists**, unused: `BeepPathfindingGrid`
  (`core/BeepEncryptionPathfinding.cs:68`) — 0 callers, not a Node, not `[GlobalClass]`, so
  not usable as-is. Know it is there before writing another.

## Index — and what each genre actually earned

| Genre | Items? | New classes earned | Doc |
|---|---|---|---|
| **rpg** | heavily — the reference case | **none** | `rpg.md` |
| **survival** | heavily — the loop *is* the item loop | `GameTool : GameWeapon`, `GameFood : GameConsumable` | `survival.md` |
| **shooter** | yes — weapons | **none** | `shooter.md` |
| **platformer** | lightly — pickups | **none** | `platformer.md` |
| **topdown** | yes — a strict subset of rpg | **none** | `topdown.md` |
| **racing** | no | `VehicleSpec` (sibling) + `VehicleController` | `racing.md` |
| **strategy** | no | `UnitSpec`, `TechNode` (+ **shares** `BuildingSpec`) | `strategy.md` |
| **citybuilder** | no | `BuildingSpec` (sibling) | `citybuilder.md` |
| **cardgame** | no | `GameCard` — a **sibling**, not a subclass | `cardgame.md` |
| **puzzle** | **no** | **none — no items, no specs** | `puzzle.md` |

### What the ten docs settled

**The spine survived its hardest test unchanged.** rpg — weapons, armour, shields, potions,
scrolls, keys, quest items, materials, currency, chests, anvils — earned **zero** new
classes. `GameKey` and `GameQuestItem` were proposed and rejected on a clean argument: the
**door** names the key (`DoorSwitchComponent.RequiredItem`) and the **objective** names the
item (`QuestObjective.TargetId`), so neither would add a field. Shooter, platformer and
topdown likewise earned nothing. **Four of the five item genres needed no new class** — which
is the outcome that says the model is right.

**Survival earned two**, both by the rule: `GameTool` adds `ToolClass` + `HarvestPower` — a
tier gate `Damage` cannot express, and "Axe" is not a `DamageType`; `GameFood` adds
`HungerRestore`, which `HungerStaminaComponent.ConsumeFood` already demands and the spine
cannot supply. `GameMaterial`, `GameStation` and a `WarmthRating` were all rejected —
the last because `TemperatureComponent` reads no resistances, so it would be a dead field.

**Half the genres wanted a *sibling*, not a subclass** — and argued it field-by-field rather
than by taste. A card is not carried, not destructible, not in the world:
`IsStatic`/`IsDestructible`/`MaxDurability`/`WorldScene` would all be permanently null, so
subclassing would inherit what the child must suppress — **the inverse of the rule**. Same
for a vehicle: 1 of 10 spine fields fits. `BuildingSpec` is **shared** between strategy and
citybuilder rather than forked.

**Puzzle earned nothing at all, and that is the sharpest result.** `GemSpec` was considered
and rejected — decisively because `Match3BoardComponent` **grows `GemTypeCount` at runtime**
for the difficulty ramp (`Mathf.Min(GemTypeCount + (level-1)/2, 8)`). An `int` scales; an
authored array does not. **The bare int is correct, not lazy.**

> Recurring finding across every doc: the genre `tuning` keys are inert.
> `inventory_columns`, `quick_slots`, `dialogue_speed`, `tooltip_delay`,
> `status_decay_seconds`, `hand_limit`, `card_fan_angle`, `lap_count`, `speedometer_max`,
> `resource_slots`, `build_grid_size` are read by nothing, and several are duplicated as
> literals in the scenes. A `GameItem`/spec tree is where those values should live.
</content>
