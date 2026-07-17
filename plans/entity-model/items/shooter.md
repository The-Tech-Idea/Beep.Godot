# shooter — GameItem tree

> Conforms to `README.md`. The rule: **a class earns existence only by adding a FIELD or
> BEHAVIOUR its parent cannot express.**

---

## 1. Does this genre need items at all?

**Yes — but only one kind: the gun.** And the gun is already `GameWeapon`.

The genre's own screens name the set. `codex.tscn` is an "Arsenal (12 / 40 unlocked)" grid
(`templates/scenes/shooter/codex.tscn:40`) listing **Pistol**, **Shotgun**, **Crossbow**
(`:67`, `:81`, `:95`); `character_select.tscn` advertises **Rifle (reliable)**, **Pistol +
lockpick**, **Crossbow (companion)**, **Shotgun (spread)** (`:69`, `:95`, `:121`, `:147`);
`run_results.tscn:89` unlocks a **Heavy Shotgun**. That is **five guns with the same fields
and different numbers** — the textbook `.tres` case from `README.md:19`.

Nothing else in the genre is an item. Grepped: `shooter_main.tscn` and
`levels/shooter/level_1.tscn` / `level_2.tscn` contain **no** `InventoryComponent`, no
`LevelingComponent`, no `AttackComponent`, no `StatusEffectComponent`. The `Pickups` node in
both levels (`levels/shooter/level_1.tscn:30`, `level_2.tscn:32`) is an **empty `Node2D`**.
There is **no ammo concept anywhere in the addon** — grepped `ammo|reload|magazine` across
`addons/beep_game_builder_cs/`: zero hits outside unrelated words (`ReloadCurrentScene`,
`CryptoStream`).

---

## 2. The tree

**Spine branches used:** `GameItem → GameEquipment → GameWeapon`. That is all.
No `GameShield`, no `GameArmor`, no `GameLiquid`, no `GameConsumable`.

```
GameWeapon  (Slot = MainHand, IsRanged = true, IsStatic = false, IsDestructible = false)
    weapon_pistol.tres
    weapon_rifle.tres
    weapon_shotgun.tres
    weapon_crossbow.tres
    weapon_heavy_shotgun.tres
```

`GameItem` (base, no subclass) for the coin the `Pickups` container implies:

```
GameItem   (IsStatic = false, IsDestructible = false, MaxStack = 999)
    coin.tres
```

**Traits and what they forbid.** Every weapon here is `IsDestructible = false` — a bullet-heaven
run has no weapon durability — so per `README.md:64` and `phase-1-item-resources.md:151`, none
of these `WieldScene`s may carry a `HealthComponent`. Same for `coin.tres`. `IsStatic = false`
on the coin implies `PickupComponent` when grounded (`README.md:63`).

### The FireRate constraint — state it, because it bites

`ShooterController` bakes the weapon's numbers as its own `[Export]`s —
`FireRate`, `ProjectileDamage`, `ProjectileSpeed`, `ProjectileScene`
(`ecs/ShooterController.cs:16-18,26`) — and then **overwrites two of them from `GameInfo` in
`_Ready`**:

```csharp
var info = GameBuilder.GameInfo.Instance;
if (info != null) { MoveSpeed = info.MoveSpeed; FireRate = info.FireRate; }
```
— `ecs/ShooterController.cs:44-45`

So a `GameWeapon` that sets `FireRate` **is clobbered on load** unless it writes *after*
`ShooterController._Ready()` has run. `GameInfo.FireRate` (`core/GameInfo.cs:145`) is fed by
`tuning.fire_rate: 0.2` (`catalogs/skins/shooter/genre.json:22`) — a **per-project** value that
by construction cannot differ per weapon. Equipping a shotgun and a pistol on the same project
would yield the same fire rate. This is a Phase-2 sequencing problem, not a data problem.

### The bigger constraint: the shooter's firing path does not go through `AttackComponent`

`MASTER_TODO.md:87-92` says equipment reaches combat "through the pattern the codebase already
uses" — `AttackComponent` resolving an optional sibling and asking it for a modifier
(`ecs/AttackComponent.cs:51-56`). **That hook does not exist in this genre.**
`ShooterController` spawns its own projectiles (`ecs/ShooterController.cs:76-105`), sets
`projComp.Damage`/`Speed` directly from its own exports (`:96-97`), and consults
`StatusEffectComponent` for **speed only** (`:55`) and stun (`:52`). It **never** asks for a
damage modifier the way `AttackComponent` does.

**Consequence:** an `EquipmentComponent` (Phase 2) modelled purely as "an optional sibling
`AttackComponent` queries" would be **inert in the shooter**, because the shooter has no
`AttackComponent`. Phase 2 must give `ShooterController` the same equipment query, or shooter
weapons are data nothing reads.

---

## 3. New framework classes this genre earns

**None.**

Each candidate, against the rule:

| Candidate | Verdict | Why |
|---|---|---|
| `GameGun : GameWeapon` | **`.tres`** | Its body would be default values. A rename of `GameWeapon` (`README.md:19`). |
| `GameAmmo : GameItem` | **`.tres`** | `ammo_9mm.tres` is `GameItem` with `MaxStack = 99`. It adds no field. And **no ammo concept exists** to consume it — see §1. |
| `GameWeapon.MagSize` / `.ReloadTime` | **no** | Not a class question, but the same defect: nothing reads them, and there is no reload input action. `phase-1-item-resources.md:138` deliberately omitted `Range` for exactly this reason — "a field that silently does nothing is this repo's signature defect" (`MASTER_TODO.md:123-125`). |
| `GameWeapon.Spread` | **no** | Spread already lives one level down: `ProjectileModifierComponent.ModifierMode.Spread` (`ecs/ProjectileModifierComponent.cs:18`), whose own doc says it is "used by the spawner, not the projectile" (`:11`). `weapon_shotgun.tres` differs from `weapon_pistol.tres` by its **`ProjectileScene`**, not by a new field. *(UNCERTAIN: no spawner implements `Spread` today — `ShooterController.SpawnProjectile` never reads the mode. That is a component bug, not an item-model gap.)* |

**Ammo, concretely:** it is a counter. `InventoryComponent` already has quantity-per-slot, and
Phase 1 makes that an `InventorySlot { GameItem Item; int Quantity; }`
(`phase-1-item-resources.md:171`). A magazine is `ammo_9mm.tres` + that int. Zero classes.

---

## 4. Components this implies

**Existing, already serving it:**
- `PickupComponent` (`ecs/PickupComponent.cs`) — `ItemId = "coin"`, `ScoreValue`, and the
  `Collected` signal (`:34`). Its `ScoreValue → GameFlow.AddScore` edge is live (`:99-104`).
- `ProjectileComponent` + `ProjectileScene` — the pattern `GameWeapon.ProjectileScene` copies
  (`phase-1-item-resources.md:37-39`).
- `LevelingComponent.LevelUp(newLevel, statPoints)` (`ecs/LevelingComponent.cs:22`) — the
  natural trigger for the level-up overlay. **It is in no shooter scene** (grepped
  `shooter_main.tscn`, both levels: no hits). The `level_up_choice.tscn` screen therefore has
  **no trigger** today.

**Gaps the tree exposes** (naming them, not designing them — `CLAUDE.md` § *Scope*):

1. **`ShooterController` needs the equipment query**, mirroring `AttackComponent.cs:51-56`.
   Without it §2's whole tree is decorative. This is the single highest-leverage item in the
   genre.
2. **There is no permanent-modifier channel.** `LevelUpChoice` records the pick to
   `SetGameData(PickKey, action)` and its own comment says applying it "is the game's job"
   (`ecs/scenes/shooter/LevelUpChoice.cs:10-11,42`). `StatusEffectComponent` cannot serve:
   its `_Process` decrements `Duration` every frame and expires the effect at `<= 0`
   (`ecs/StatusEffectComponent.cs:182,191-195`) — it is timed-buff-shaped by construction. A
   permanent "+10% move speed" would need an infinite-duration hack. **This is not an item
   problem and must not be solved with an item class** — it is a Phase-6 component gap.
3. Weapon `WieldScene`s hitting *what they touch* needs an `Area2D` hitbox component that does
   not exist (`phase-1-item-resources.md:53-59`). **Less urgent here than in melee genres**:
   these weapons are all `IsRanged = true`, so they hit via `ProjectileComponent` and never
   touch the dead melee path (`README.md:88`, `AttackComponent.Range` never read).

**Cited, not re-derived:** `DamageTypeComponent` is dead — every hit is `Physical`
(`README.md:86-87`), so `GameWeapon.DamageType` is authorable but unreachable until Phase 3a.

---

## 5. Content vs framework

**We ship (framework):**
- `GameWeapon` — already in the spine (`phase-1-item-resources.md:111-113`). Nothing new.
- The **wire**: `ShooterController` querying equipment, and the `FireRate` write ordering fixed
  so a weapon is not clobbered by `GameInfo` (`ShooterController.cs:44-45`).
- `weapon_pistol.tres` as a **single worked example** — the shooter's `ProjectileScene` default
  already sets this precedent (`shooter_main.tscn:34`).

**The developer authors (content):**
- Every other `.tres`: `weapon_rifle`, `weapon_shotgun`, `weapon_crossbow`,
  `weapon_heavy_shotgun`, `coin`, and their `ProjectileScene`s and icons.
- All balance. `tuning.fire_rate: 0.2` (`genre.json:22`), `ProjectileDamage = 10`
  (`ShooterController.cs:17`), and the codex/character-select copy ("Rifle (reliable)",
  "Shotgun (spread)") are **placeholder text**, not a shipped arsenal.
- The level-up upgrade table — `LevelUpChoice` records the pick and says so (`:10-11`).
