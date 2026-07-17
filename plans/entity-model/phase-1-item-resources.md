# Phase 1 — Item resources

**Goal:** an item is authored data, not a string. A developer makes `sword_iron.tres` in the
inspector, drags it onto an `[Export]`, and the framework knows its damage, its type, and
which slot it occupies.

**Depends on:** nothing. This is the foundation for Phases 2–4.

---

## A sword has two representations — this is the crux

An earlier draft of this plan said a sword is *only* data and must never carry
`AttackComponent` or be damageable. **That was too absolute.** The code disagrees:

- `AttackComponent` requires only a **`Node2D`** parent (`ecs/AttackComponent.cs:33` —
  `_body = GetParent() as Node2D`), not a `CharacterBody2D`. **It can live on a sword.**
- `HealthComponent` is genuinely **blind** — it performs no parent cast at all. **A sword can
  have HP**, and durability-as-health is a real, working composition: `Died` = it breaks.

The distinction that actually matters is **where the sword is**:

| The sword is… | It is… | Can it have components? |
|---|---|---|
| Wielded, or lying in the world | a **node** in the scene tree | **Yes** — `AttackComponent`, durability, a hitbox |
| In an inventory, a save file, a shop list | **not in the tree** | **No** — components only exist on nodes |

So it is **both**, and neither half is optional:

- **`GameWeapon : Resource`** — the *definition*: id, icon, base damage, damage type, slot, and
  **`[Export] PackedScene? WieldScene`**. This is what stacks, serialises, and appears in a
  shop. 99 potions cannot be 99 nodes.
- **`WieldScene`** — the *instance*: a `Node2D` carrying `AttackComponent`, a hitbox, and
  optionally `HealthComponent` for durability. Instanced by `EquipmentComponent` (Phase 2)
  into the wielder's hand when equipped.

**This is the pattern the repo already uses.** `AttackComponent.ProjectileScene` is a
`PackedScene` whose instance carries `ProjectileComponent` — definition points at scene, scene
carries components. A weapon is the same shape as a bullet.

### Revised rule

Not *"a sword must not have `AttackComponent`"* but:

> **Give an archetype a component only if that representation of it does that thing.**
> A *wielded* sword attacks → `AttackComponent` belongs. A sword *entry in a save file* does
> not attack, cannot, and is not a node. A sword that **can break** should have durability; a
> sword that cannot, should not — a component whose behaviour never happens is this repo's
> signature defect.

### Two things to know before building it this way

1. **`AttackComponent` does not swing.** `DealMeleeDamage` is an `IntersectPoint` **point query
   at a target position** (`ecs/AttackComponent.cs:92-93`), and **`Range` is never read**. So a
   sword carrying `AttackComponent` hits *where you clicked*, not *what it touches*. A real
   weapon entity needs an `Area2D` hitbox component — which does not exist (Phase 6 §2, the
   same gap as `HazardComponent`). **Decide this before Phase 2**: either the wielder keeps
   `AttackComponent` and the weapon only supplies numbers, or the weapon gets a hitbox and
   `AttackComponent` stays on the wielder for unarmed.
2. **Durability is per-instance, and a `.tres` is shared by reference.** Two swords pointing at
   `sword_iron.tres` share it — writing durability onto the resource would degrade *every* iron
   sword at once. It lives on the instance (the `WieldScene`'s `HealthComponent`) or in the
   inventory slot, never on the definition.

---

## Why

`InventoryComponent.InventoryItem` (`ecs/InventoryComponent.cs:30-42`) is a plain nested
class:

```csharp
public class InventoryItem {
    public string Id = "";  public string DisplayName = "";  public Texture2D? Icon;
    public int Quantity = 1;  public ItemRarity Rarity = ItemRarity.Common;
    public string ItemType = "misc";
    public Godot.Collections.Dictionary<string, Variant> Stats = new();   // ← everything else
}
```

`ItemType` is a **string** and the variation lives in a `Stats` **bag**. That cannot be
authored in the inspector, saved as `.tres`, dragged onto an export, or subclassed — and
nothing type-checks `Stats["damage"]`.

Every other data shape in this repo already does it properly:
`GameInfo` (`core/GameInfo.cs:15`), `UISkin`, `ColorPalette`, `GeometryProfile`,
`CraftingRecipe` + `CraftingIngredient` (`ecs/CraftingComponent.cs:53,65`), `QuestObjective`
(`ecs/QuestComponent.cs:71`) — all `[Tool][GlobalClass] : Resource`. Items are the outlier.

## The hierarchy

New files under `ecs/items/` — one per class, filename matching the class name (registration
is filename-driven; `CLAUDE.md` § *[GlobalClass] Components*).

**Named `Game*`, not `Beep*`.** The repo already splits these: `Game*` is the game model —
`GameInfo`, `GameStateData` (both `Resource`s), `GameApp`, `GameFlowComponent` — while `Beep*`
is tooling (`BeepFileUtils`, `BeepGenreGenerator`, `BeepCommandHistory`). An item is model.

```
GameItem : Resource                     [Tool][GlobalClass]
    Id, DisplayName, Description, Icon, Rarity, MaxStack
    IsStatic        : bool              — stays put (anvil, chest, rock) vs carried
    IsDestructible  : bool              — can be broken
    MaxDurability   : float             — meaningful only when IsDestructible
    WorldScene      : PackedScene?      — how it exists as a node, when it does
    │
    ├── GameEquipment : GameItem        [Tool][GlobalClass]
    │       Slot : EquipSlot            (MainHand, OffHand, Head, Body, Accessory)
    │       WieldScene : PackedScene?   — the instance held in the hand
    │       SocketCount : int           — how many holes.  Phase 7 (composition).
    │       │                             ⚠ NOT `Socketed[]` — see below
    │       ├── GameWeapon : GameEquipment
    │       │       Damage, DamageType (DamageTypeComponent.Type),
    │       │       Range, IsRanged, ProjectileScene
    │       │       Cooldown    : float      — CLOCK UNITS, not seconds.  Phase 7
    │       │       AmmoItem    : GameItem?  — null = needs none (a sword).  Phase 7
    │       │       AmmoPerUse  : int = 1                                    Phase 7
    │       │
    │       ├── GameShield : GameEquipment
    │       │       Defense, BlockChance, Resistances
    │       │
    │       └── GameArmor : GameEquipment
    │               Defense, Resistances (per DamageTypeComponent.Type)
    │
    ├── GameLiquid : GameItem           [Tool][GlobalClass]
    │       Volume, IsDrinkable, HealAmount, StatusEffectId
    │       (potion, fuel, lamp oil, water — a liquid is not always drinkable)
    │
    └── GameConsumable : GameItem       [Tool][GlobalClass]
            HealAmount, StatusEffectId
            Duration : float            — CLOCK UNITS, not seconds.  Phase 7
```

### Every duration on a `GameItem` is in **clock units** — Phase 7

`GameConsumable.Duration` and `GameWeapon.Cooldown` are **not seconds**. A potion authored
`Duration = 3` lasts 3 seconds in a real-time genre and 3 **turns** in cardgame/strategy — the same
`.tres`, no branch in the consumer, because the genre owns the clock (`GameClock`, mode from
`genre.json` tuning). **An earlier draft called these seconds. That silently excluded 2 of the 10
genres**, which is the same defect class as a `[GlobalClass]` that only works on one parent type.

**`AttackSpeedMultiplier` is deleted from `GameWeapon`.** It was a multiplier over an unstated unit
— exactly the ambiguity `GameClock` exists to end. A weapon states its `Cooldown` in clock units;
speed buffs are `StatModifier`s on the same axis (Phase 2). Two mechanisms for one number is how
`StatusEffectComponent` ended up consulted at two hardcoded sites with two magic strings.

### Dependencies: what goes on the item, and what does not — Phase 7

| Dependency | Lives on | Why |
|---|---|---|
| **Construction** — sword ← 2 iron + 1 leather | **`CraftingRecipe`** (already exists, `CraftingComponent.cs:53-60`) | The same sword may be craftable three ways. `RequiredItems[]` on `GameItem` hardcodes one path and makes the item know how it was made. |
| **Consumption** — gun ← ammo, lamp ← oil | **`GameWeapon.AmmoItem`** ✅ on the item | A gun *is* a thing that eats ammo, regardless of how it was made. |
| **Composition** — sword ← 3 gems | **split**: `SocketCount` on the definition, `Socketed[]` on the **slot** | Per-instance. See below. |
| **Unlock** — barracks ← "Bronze Working" | **`ResearchNode`**, not `GameItem` | A technology is not a thing you can hold. |

**`GameItem` gains no `RequiredItems[]`.** `CraftingRecipe` already models item-depends-on-items and
already models it correctly. Phase 1's job is to retire its strings:
`CraftingIngredient.ItemId` → `GameItem`, `CraftingRecipe.OutputItem` → `GameItem`.

`Rarity` moves from the nested `InventoryComponent.ItemRarity` onto `GameItem`. `ItemType`
(the string) is **deleted** — **the class is the type**. `Stats` (the `Variant` bag) is
**deleted** — the subclass fields are the stats, typed.

`DamageType` reuses the existing `DamageTypeComponent.Type` enum
(`ecs/DamageTypeComponent.cs:15`) rather than inventing a parallel one, so a weapon's type
already lines up with `ResistanceComponent`'s per-type multipliers. *(Note Phase 3a: that enum
is currently unreachable — every hit is Physical.)*

**`GameWeapon` gets `Range`** — gated on Phase 3's melee fix, not omitted. An earlier draft left
it out because `AttackComponent.Range` is never read (melee is a point query at the cursor), so
the field would silently do nothing. That was avoidance premised on not touching existing
signatures — a constraint we do not have. Phase 3 replaces the point query with a real hitbox;
`Range` lands with it. A weapon that cannot express reach is not a weapon.

### The traits on the base are the point

`IsStatic` and `IsDestructible` are not decoration. **They determine what the world instance
may be composed of**, which turns Phase 5's archetype tables from folklore into something
derived from data:

| Trait | The instance must be | Components it implies | Components it forbids |
|---|---|---|---|
| `IsStatic = true` | `StaticBody2D` / `Node2D` | — | `MovementComponent`, any `ControllerComponent`, `PickupComponent` (it isn't collected) |
| `IsStatic = false` | `Area2D` (collectible) or `Node2D` (wielded) | `PickupComponent` when on the ground | — |
| `IsDestructible = true` | — | `HealthComponent` as durability (`MaxHealth = MaxDurability`; `Died` = it breaks) | — |
| `IsDestructible = false` | — | — | **`HealthComponent`** — it cannot be broken, so HP is behaviour that never happens |

An anvil is `IsStatic = true, IsDestructible = false`. A rock is `true, true`. A sword is
`false, true`. A potion is `false, false`. **Each of those four rows composes differently, and
the data says how** — the developer doesn't have to remember a table.

This is also what makes the "must not" list checkable (Phase 5): a validator can compare a
`WorldScene` against its `GameItem`'s traits and flag a `HealthComponent` on an
`IsDestructible = false` item, or a `MovementComponent` on an `IsStatic = true` one.

## Work

1. **`ecs/items/GameItem.cs`** — base. `Id` is the stacking/save key.
2. **`ecs/items/GameEquipment.cs`** — adds `EquipSlot`. Declare `EquipSlot` here; Phase 2's
   `EquipmentComponent` uses it.
3. **`ecs/items/GameWeapon.cs`**, **`ecs/items/GameArmor.cs`**, **`ecs/items/GameConsumable.cs`**.
4. **`InventoryComponent`** — `InventoryItem[] Slots` becomes `InventorySlot[]`; drop the nested
   class, `ItemType`, and `Stats`. **`Quantity` is per-slot state, not item identity** — a `.tres`
   is shared by reference, so writing it onto the resource would make every sword in the world
   share one count.

   **The slot is where all per-instance state lives — this is the load-bearing line of Phase 1:**

   ```csharp
   InventorySlot {
       GameItem  Item;          // the shared definition (a .tres)
       int       Quantity;      // per-instance
       float     Durability;    // per-instance — NOT GameItem.MaxDurability, which is the cap
       GameItem[] Socketed;     // per-instance — Phase 7 (composition)
   }
   ```

   **Every field below `Item` would be a bug on the resource.** Two swords pointing at
   `sword_iron.tres` must wear out independently and hold different gems. `MaxDurability` and
   `SocketCount` are on the **definition** (the cap, the hole count); `Durability` and `Socketed`
   are on the **instance**. Conflating them is the single most likely way to get this wrong,
   because both pairs read naturally as "the sword's durability" and "the sword's gems".

5. **Save/load** — `ISaveable` on `InventoryComponent` currently writes
   `Items[id] = quantity` (`ecs/InventoryComponent.cs:322`). Keep persisting **id + quantity**
   and re-resolve the resource on load; do **not** serialize the resource itself. Saves stay
   small and survive an item's stats being rebalanced.

   **But per-instance state must persist too**, or a saved sword returns at full durability with
   its gems gone. Persist the **slot**, not the item: `{ id, quantity, durability, socketed[ids] }`.
   Still ids, still small, still rebalance-proof — this is the reason the slot exists rather than
   being a convenience.

## Gotchas

- **Resource sharing is the big one, and the obvious fix is broken.** Two swords pointing at
  `sword_iron.tres` share it, so anything per-instance (quantity, durability, enchantments)
  must live on the instance/slot, or be an explicit `.Duplicate()`. This is exactly what the
  `Stats` bag was papering over.

  Godot's built-in answer is **`resource_local_to_scene`** — and it is a trap. It has a [known
  engine bug (godot#45350)](https://github.com/godotengine/godot/issues/45350): duplicating an
  already-instantiated scene makes the copies **share** the resource anyway, and setting the
  flag at runtime does nothing to resources already created. It is precisely what a developer
  will reach for, and it will not hold. **Say so in the component docs**, or this gets
  rediscovered the hard way.
- Filename must equal class name — registration is filename-driven.
- `[Export]` on a Resource subclass shows in the inspector only with `[GlobalClass]`.
- Existing scenes set `PickupComponent.ItemId` (a string). Phase 4 **replaces** it — no
  fallback, no dual path. Phase 1 can leave it alone; Phase 4 deletes it and updates the
  templates.

## Verification

- `dotnet build` → 0 errors.
- In the editor: **Create Resource → GameWeapon** appears; save `sword_iron.tres`; its
  `Damage`/`DamageType` show in the inspector; re-open and the values persist.
- `validate_scenes.sh` → PASS (no scene changes expected this phase).
</content>
