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
    │       │
    │       ├── GameWeapon : GameEquipment
    │       │       Damage, DamageType (DamageTypeComponent.Type),
    │       │       AttackSpeedMultiplier, IsRanged, ProjectileScene
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
            HealAmount, StatusEffectId, Duration
```

`Rarity` moves from the nested `InventoryComponent.ItemRarity` onto `GameItem`. `ItemType`
(the string) is **deleted** — **the class is the type**. `Stats` (the `Variant` bag) is
**deleted** — the subclass fields are the stats, typed.

`DamageType` reuses the existing `DamageTypeComponent.Type` enum
(`ecs/DamageTypeComponent.cs:15`) rather than inventing a parallel one, so a weapon's type
already lines up with `ResistanceComponent`'s per-type multipliers. *(Note Phase 3a: that enum
is currently unreachable — every hit is Physical.)*

`Range` is deliberately **absent** from `GameWeapon` — see the note at the end of this doc.

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
4. **`InventoryComponent`** — `InventoryItem[] Slots` becomes `GameItem[]`; drop the nested
   class, `ItemType`, and `Stats`. Keep `Quantity`/`SlotIndex` — but they are *per-slot state*,
   not item identity, so they belong in the slot, not on the shared resource. **A `.tres` is
   shared by reference**: writing `Quantity` onto the resource would make every sword in the
   world share one count. Use a small `InventorySlot { GameItem Item; int Quantity; }`.
5. **Save/load** — `ISaveable` on `InventoryComponent` currently writes
   `Items[id] = quantity` (`ecs/InventoryComponent.cs:322`). Keep persisting **id + quantity**
   and re-resolve the resource on load; do **not** serialize the resource itself. Saves stay
   small and survive an item's stats being rebalanced.

## Gotchas

- **Resource sharing is the big one.** Two swords pointing at `sword_iron.tres` share it.
  Anything per-instance (quantity, durability, enchantments) must live in the slot/holder, or
  be an explicit `.Duplicate()`. This is exactly what the `Stats` bag was papering over.
- Filename must equal class name — registration is filename-driven.
- `[Export]` on a Resource subclass shows in the inspector only with `[GlobalClass]`.
- Existing scenes set `PickupComponent.ItemId` (a string). Phase 4 migrates them; Phase 1
  leaves `ItemId` alone so nothing breaks in between.

## Verification

- `dotnet build` → 0 errors.
- In the editor: **Create Resource → GameWeapon** appears; save `sword_iron.tres`; its
  `Damage`/`DamageType` show in the inspector; re-open and the values persist.
- `validate_scenes.sh` → PASS (no scene changes expected this phase).
</content>
