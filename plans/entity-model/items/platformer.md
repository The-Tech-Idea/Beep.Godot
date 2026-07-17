# platformer — GameItem tree

> Conforms to `README.md`. The rule: **a class earns existence only by adding a FIELD or
> BEHAVIOUR its parent cannot express.**

---

## 1. Does this genre need items at all?

**Lightly, and honestly: barely.** Coins, keys, power-ups. Nothing else.

The level template names the whole intent in six sibling nodes —
`Checkpoints`, `MovingPlatforms`, `Enemies`, `Hazards`, `Pickups`, `LevelBoundary`
(`templates/scenes/levels/platformer/level_1.tscn:28-38`, identical in `level_2.tscn:28-38`).
**Exactly one of those six is an item concern: `Pickups`** — and it is an empty `Node2D`. The
other five are movement, level, and damage concerns that the item model must not colonise.

`pickup_template.tscn` shows what a platformer pickup is today:
`ItemId = "coin"`, `Quantity = 1`, `ScoreValue = 100` (`:18-20`). That is a coin. It works.

Grepped `platformer_main.tscn`: **no `InventoryComponent`, no `AttackComponent`, no
`StatusEffectComponent`, no `LevelingComponent`.** The player is a `CharacterBody2D` with
`PlatformerController` + `HealthComponent` + a camera (`:28-49`). There is no inventory UI, no
equipment, no shop, and no weapon in the genre. `tuning` is three movement numbers —
`gravity: 980`, `jump_velocity: -400`, `move_speed: 200` (`catalogs/skins/platformer/genre.json:19-21`).

---

## 2. The tree

**Spine branches used:** `GameItem` (base, unsubclassed) and `GameConsumable`. That is all.
No `GameEquipment`, no `GameWeapon`, no `GameShield`, no `GameArmor`, no `GameLiquid`.

```
GameItem       (IsStatic = false, IsDestructible = false)
    coin.tres           MaxStack = 999
    gem.tres            MaxStack = 999,  Rarity = Rare
    key_bronze.tres     MaxStack = 1
    key_silver.tres     MaxStack = 1

GameConsumable (IsStatic = false, IsDestructible = false)
    powerup_speed.tres        StatusEffectId = "speed_boost",  Duration = 8
    powerup_invincible.tres   StatusEffectId = "invincible",   Duration = 5
```

**Traits.** Every one of these is `carried, indestructible` — the fourth row of
`README.md:64`: implies `PickupComponent` when grounded, **forbids `HealthComponent`**. No
platformer item is destructible, so no item `WorldScene` in this genre may carry health.
`IsStatic = false` throughout: the genre has no anvil, no chest, no ore node.

**The keys are already half-real.** `DoorSwitchComponent.RequiredItem` is a string —
*"If non-empty, the player must have this item id to activate"* (`ecs/DoorSwitchComponent.cs:21`).
`key_bronze.tres`'s `Id` is that string. Phase 4 replaces the string with the resource
(`MASTER_TODO.md:103-104`); Phase 1 leaves it alone.

---

## 3. New framework classes this genre earns

**None.** This genre is the cheapest in the index and should stay that way.

The one candidate worth taking seriously is the power-up:

> *"A power-up that grants a temporary ability — is that `GameConsumable` with a
> `StatusEffectId`, or does it need more?"*

**It is `GameConsumable`, and it needs no new class — but it does expose one missing field on
the already-planned `GameConsumable`, and that is a field, not a class.**

`GameConsumable` as specified is `HealAmount, StatusEffectId, Duration`
(`phase-1-item-resources.md:125-126`). Those three cannot express the **magnitude** of a buff.
`PlatformerController` reads:

```csharp
float speedMod = _statusEffects?.GetModifier("speed_boost", "speed_multiplier", 1f) ?? 1f;
```
— `ecs/PlatformerController.cs:79`

Both the effect id (`"speed_boost"`) and the modifier key (`"speed_multiplier"`) are **hardcoded
string literals in the controller**. A `powerup_speed.tres` can supply `StatusEffectId =
"speed_boost"` and `Duration = 8` — but **1.5× vs 2.0× is not authorable anywhere**. The
receiving API already takes the shape:
`StatusEffectComponent.ApplyEffectWithModifiers(id, duration, Dictionary<string,float> modifiers, …)`
(`ecs/StatusEffectComponent.cs:92-99`).

**Recommendation (a field on a planned class, not a new class):** `GameConsumable.Modifiers :
Godot.Collections.Dictionary<string, float>`, matching `ApplyEffectWithModifiers`'s parameter.
It would be **live on arrival** — `PlatformerController.cs:79` and `ShooterController.cs:55`
already call `GetModifier`, so it is not the dead-field defect that kept `Range` out
(`phase-1-item-resources.md:138`, `MASTER_TODO.md:123-125`). *(UNCERTAIN: whether this belongs
on `GameConsumable` or on a shared status-carrying interface also used by `GameLiquid`
(`README.md:48`, which likewise carries a bare `StatusEffectId`). Phase 1's call, not this
doc's.)*

**Rejected candidates:**

| Candidate | Verdict | Why |
|---|---|---|
| `GameCoin`, `GameGem` | **`.tres`** | Same fields, different `Rarity` and icon. `README.md:19`. |
| `GameKey : GameItem` | **`.tres`** | A key adds no field. Its whole behaviour is `Id` matching `DoorSwitchComponent.RequiredItem` (`:21`). `key_bronze.tres` is `GameItem` with `MaxStack = 1`. |
| `GamePowerUp : GameConsumable` | **`.tres`** | Its body would be default values — the exact smell in `README.md:23-24`. |
| `GamePlatformerItem` | **no** | "A genre never gets a class just for being that genre" (`README.md:26-28`). |

**The one power-up that genuinely does not fit — and still earns nothing.** A double-jump
power-up must write `JumpComponent.MaxJumps` (`ecs/JumpComponent.cs:25`, default 2). No status
effect can reach it: `StatusEffectComponent` offers only a modifier dictionary that
`JumpComponent` never queries (grepped — `JumpComponent` has no `GetModifier` call, unlike
`PlatformerController.cs:79`). That is a **component gap**, not a data gap. Adding
`GameConsumable.GrantsDoubleJump` would be a bool nothing reads. **Do not.**

---

## 4. Components this implies

**Existing, already serving it:**
- `PickupComponent` (`ecs/PickupComponent.cs`) — floats, rotates, respawns, emits
  `Collected(itemId, quantity)` (`:34`), and awards score via `GameFlow.AddScore` (`:99-104`).
  It is the genre's whole item surface today and it works.
- `CheckpointComponent` (`ecs/CheckpointComponent.cs`) — serves the `Checkpoints` node
  (`level_1.tscn:28`). Not an item.
- `DoorSwitchComponent.RequiredItem` (`:21`) — the key consumer. Already string-based.
- `StatusEffectComponent.ApplyEffectWithModifiers` (`:92-99`) — the power-up applier, ready.
- `MovingPlatformComponent`, `JumpComponent` / `DashComponent` / `GlideComponent` /
  `WallJumpComponent` — movement, correctly outside the item model.

**New ones the tree forces:** **none.**

**Gaps to cite, not solve here:**
- `Pickup.Collected → Inventory.AddItem` has **0 connections** (`README.md:88-90`). For a
  score-only coin (`ScoreValue = 100`, `pickup_template.tscn:20`) this does not matter — the
  edge that *does* exist is the one this genre uses. For `key_bronze.tres` it matters entirely:
  the key is collected and lands nowhere. Phase 4.
- `DropTableComponent`'s loot list has no `[Export]` (`README.md:91`) — so the `Enemies` node
  (`level_1.tscn:32`) can never drop a coin at authoring time. Phase 4.
- Nothing joins `"players"` / `"enemies"` (`README.md:93-94`).

---

## 5. Content vs framework

**We ship (framework):**
- Nothing new. `GameItem` and `GameConsumable` are already in the spine
  (`phase-1-item-resources.md:100-126`).
- One field recommendation to Phase 1: `GameConsumable.Modifiers` (§3).
- `pickup_template.tscn` stays as-is; Phase 4 swaps its `ItemId` string for a `GameItem` ref
  (`phase-1-item-resources.md:184-185`).

**The developer authors (content):**
- Every `.tres` above: `coin`, `gem`, `key_bronze`, `key_silver`, `powerup_speed`,
  `powerup_invincible` — plus icons.
- All balance: `ScoreValue = 100` (`pickup_template.tscn:20`) and
  `gravity: 980 / jump_velocity: -400 / move_speed: 200` (`genre.json:19-21`) are **defaults,
  not a designed game**.
- Level layout — the six containers in `level_1.tscn:28-38` ship **empty on purpose**
  (`CLAUDE.md` § *Scope*: no level layout, no assets).
