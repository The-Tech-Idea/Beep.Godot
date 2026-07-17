# Phase 4 — Pickups & drops

**Goal:** the sword on the ground *is* the sword. Picking it up puts that item in the
inventory; equipping it makes you hit harder. This closes the loop from world → inventory →
equipment → combat.

**Depends on:** Phase 1 (`GameItem`), and Phase 2 for the equip half.

---

## Why

`PickupComponent` carries a **string**: `[Export] string ItemId = "coin"`
(`ecs/PickupComponent.cs:13`) plus a `Quantity`. There is no way to say *which* sword. The
item's stats exist nowhere — `Collect()` emits `Collected(itemId, quantity)` and the id is all
the receiver gets.

So today a "sword pickup" and a "coin pickup" are the same object with different spelling.

**And nothing receives it anyway.** Three edges are missing, all verified:

| Edge | State |
|---|---|
| `PickupComponent.Collected` → `InventoryComponent.AddItem` | **0 connections.** The signatures are *identical* — `(string itemId, int quantity)` — and they were never wired. Picking anything up puts it nowhere. |
| `HealthComponent.Died` → `DropTableComponent.Roll()` | Never. Only `DestructibleComponent.Break` rolls, and that can't be damaged (Phase 6 §3). **Nothing ever drops loot.** |
| `CraftingComponent.Craft` → the output | **Deducts materials, grants nothing.** The comment reads `// Grant result.` above an `EmitSignal(Crafted, …)` with no `AddItem`. Crafting is a material shredder. |

That is the real state of the item loop: you cannot pick anything up, nothing drops anything,
and crafting eats your materials. **Building a beautiful item model on top of this would
produce data nothing can move** — so this phase is not optional polish, it is what makes
Phases 1–3 reachable.

## Work

**1. `PickupComponent` — carry the item.**

```
[Export] public GameItem? Item { get; set; }     // what this is
[Export] public int Quantity { get; set; } = 1;  // how many (stays)
```

**`ItemId` is deleted, not kept as a fallback.** An earlier draft proposed `Item?.Id ?? ItemId`
so existing scenes kept working — but the scenes are ours to edit, and no fallbacks or stubs
are wanted here. Two ways to say what a pickup is, one of which is a string that cannot carry
stats, is exactly the ambiguity this model exists to end. Update the shipped templates in the
same change (`pickup_template.tscn` and any level that places one) — `validate_scenes.sh`'s
property check will catch a missed one, since `ItemId` would no longer match any `[Export]`.

**2. `InventoryComponent.AddItem` — accept a `GameItem`, and be connected to.**
It currently rebuilds from a template lookup and falls back to `template?.X ?? default`
(`ecs/InventoryComponent.cs:143-149`), so an unregistered item restores with no icon, name,
or rarity. With the resource in hand there is nothing to look up and nothing to lose.

**Wire the edge.** `PickupComponent` should resolve the collector's `InventoryComponent` and
add to it — the collector is the body that triggered `BodyEntered`, so it is in hand. Warn
when an `Item` is set and the collector has no inventory (the points-go-nowhere warning added
for `ScoreValue` is the precedent). Prefer this over a separate bridge component: a pickup
that cannot be picked up is not a composition choice.

**3. `CraftingComponent.Craft` — actually grant the output.** One line. It already has the
`InventoryComponent` as a parameter.

**3. `DropTableComponent` — drop items, not strings.**
Same change: entries reference a `GameItem`. It composes with `HealthComponent.Died` — which
**currently has no listener anywhere** (an already-known gap), so the drop-on-death path is
worth wiring as the demonstration of the loop.

**4. `pickup_template.tscn`** — ship with an `Item` assigned, so a dropped-in pickup does
something. It already scores 100 via `ScoreValue` (added earlier); an item resource makes it
a *thing* rather than a score packet.

## The archetype this settles

A sword lying in the world:

| | |
|---|---|
| Node | `Area2D` — **required**: `PickupComponent` resolves `GetParent() as Area2D` (`ecs/PickupComponent.cs:34`) and connects `BodyEntered` |
| Components | `PickupComponent` (Item = `sword_iron.tres`) |
| **MUST NOT HAVE** | `HealthComponent` — it is not alive. `MovementComponent` — it does not walk. `AttackComponent` — it does not attack; **its wielder does** |

That last row is the crux of the whole model: the sword's damage is *data the wielder reads*,
not behaviour the sword performs. It is why this is a `Resource` hierarchy and not a
`SwordComponent`.

> Precedent for the MUST-NOT list being real: `projectile_template` shipped a
> `FlashComponent`, which resolves a sibling `HealthComponent` — a bullet has none, so it
> could never flash. Wrong component on the wrong archetype, silently.

## Verification

- `dotnet build` → 0 errors; `validate_scenes.sh` → PASS (pickup_template changes).
- Editor, the full loop: drop `pickup_template` with `Item = sword_iron.tres` into a level →
  walk the player over it → it is in the inventory → equip it → `AttackComponent` damage
  rises by the sword's `Damage`.
- Regression: a pickup with only `ItemId = "coin"` and no `Item` still collects and still
  scores.
</content>
