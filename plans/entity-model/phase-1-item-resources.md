# Phase 1 — Item resources

**Goal:** an item is authored data, not a string and not a component. A developer makes
`sword_iron.tres` in the inspector, drags it onto an `[Export]`, and the framework knows its
damage, its type, and which slot it occupies.

**Depends on:** nothing. This is the foundation for Phases 2–4.

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

New file `ecs/items/BeepItem.cs` (+ one file per subclass — filename must match the class,
see `CLAUDE.md` § *[GlobalClass] Components*).

```
BeepItem : Resource                  [Tool][GlobalClass]
    Id, DisplayName, Description, Icon, MaxStack, Rarity
    │
    ├── BeepEquipment : BeepItem     [Tool][GlobalClass]
    │       Slot : EquipSlot         (MainHand, OffHand, Head, Body, Accessory)
    │       │
    │       ├── BeepWeapon : BeepEquipment
    │       │       Damage, DamageType (DamageTypeComponent.Type), Range,
    │       │       AttackSpeedMultiplier, IsRanged, ProjectileScene
    │       │
    │       └── BeepArmor : BeepEquipment
    │               Defense, Resistances (per DamageTypeComponent.Type)
    │
    └── BeepConsumable : BeepItem    [Tool][GlobalClass]
            HealAmount, StatusEffectId, Duration
```

**A shield is `BeepArmor` with `Slot = OffHand`** — not its own class. It differs by data,
not by behaviour, which is the whole point of the steer.

`Rarity` moves from the nested `InventoryComponent.ItemRarity` to `BeepItem`. `ItemType` (the
string) is **deleted** — the class *is* the type. `Stats` (the bag) is **deleted** — the
subclass fields are the stats.

`DamageType` reuses the existing `DamageTypeComponent.Type` enum
(`ecs/DamageTypeComponent.cs:15`) rather than inventing a parallel one, so a weapon's type
already lines up with `ResistanceComponent`'s per-type multipliers.

## Work

1. **`ecs/items/BeepItem.cs`** — base. `Id` is the stacking/save key.
2. **`ecs/items/BeepEquipment.cs`** — adds `EquipSlot`. Declare `EquipSlot` here; Phase 2's
   `EquipmentComponent` uses it.
3. **`ecs/items/BeepWeapon.cs`**, **`ecs/items/BeepArmor.cs`**, **`ecs/items/BeepConsumable.cs`**.
4. **`InventoryComponent`** — `InventoryItem[] Slots` becomes `BeepItem[]`; drop the nested
   class, `ItemType`, and `Stats`. Keep `Quantity`/`SlotIndex` — but they are *per-slot state*,
   not item identity, so they belong in the slot, not on the shared resource. **A `.tres` is
   shared by reference**: writing `Quantity` onto the resource would make every sword in the
   world share one count. Use a small `InventorySlot { BeepItem Item; int Quantity; }`.
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
- In the editor: **Create Resource → BeepWeapon** appears; save `sword_iron.tres`; its
  `Damage`/`DamageType` show in the inspector; re-open and the values persist.
- `validate_scenes.sh` → PASS (no scene changes expected this phase).
</content>
