# Entity Archetypes

The place to look **before composing an entity**. For each archetype it lists the **node type**,
the components that are **required**, **optional**, and **must not** be present — and *why*, because
in this framework wrong-component-on-wrong-archetype fails **silently**.

`validate_scenes.sh` enforces the parent-type half of these rules (see [Checkable](#checkable)),
so a mistake here is usually caught before you run the game.

---

## The one rule

> **Give an archetype a component only if *that representation of it* does that thing.**

An item is not one object. It is a **definition** — a `GameItem` `.tres` that stacks, saves, and
appears in a shop (not in the scene tree, carries no components) — and an **instance**, a node in
the world (lying on the ground, or wielded) that *can* carry components. So the question is never
"can a sword have `AttackComponent`?" but **"does *this* sword, in *this* representation, attack?"**

- A **wielded** sword swinging at an orc — yes, `AttackComponent` belongs (it needs only a `Node2D`).
- A sword **on the ground** — no; its only verb is *be collected*.
- A sword **row in a save file** — not a node; the question doesn't apply.

Same for durability: a sword that **can break** carries `HealthComponent` (it's blind — no parent
cast — so HP = condition and `Died` = it breaks). One that can't, doesn't — *a component whose
behaviour never happens is a bug, not a feature.* Per-instance state (durability, quantity, sockets)
lives on the inventory **slot** or the world node, **never on the shared `.tres`**.

---

## Parent type is part of the contract

Many components hard-require a parent node type and most fail **silently** when it's wrong. The
archetype tables below name the node type for exactly this reason.

| Component(s) | Parent required | On mismatch |
|---|---|---|
| `ProjectileComponent` | `Area2D` | `PushError` (loud) |
| `PickupComponent`, `InteractableComponent`, `DoorSwitchComponent`, and any `AreaTriggerComponent` | `Area2D` | **warns** (`AreaTriggerComponent` resolves + warns) |
| Any `ControllerComponent`, `MovementComponent`, `HungerStaminaComponent` | `CharacterBody2D` | warns |
| `AttackComponent`, `DestructibleComponent`, `SpawnerComponent`, `DropTableComponent`, `CropGrowthComponent` | `Node2D` | silent no-op |
| `MovingPlatformComponent` | `AnimatableBody2D` | silent no-op |
| `DragComponent`, `TooltipComponent` | `Control` | silent |

### The split every mobile interactable needs

`InteractableComponent` needs `Area2D`; `AIController` needs `CharacterBody2D`. **Both cannot be on
one node.** A walking NPC is:

```
NPC (CharacterBody2D)
├── AIController              ← needs the body
└── InteractZone (Area2D)
    ├── InteractableComponent ← needs the Area2D
    └── DialogComponent       ← must be a SIBLING (it resolves a sibling)
```

---

## The tables

### Item lying in the world — `Area2D`
Its only verb is *be collected*.

| | |
|---|---|
| **Required** | `PickupComponent` (`Item` = a `GameItem` `.tres`) |
| **Optional** | `ParticleComponent`, `FloatingTextComponent`; `HealthComponent` **only if** ground loot can be destroyed (costs collision-layer care — any bullet finds it) |
| **Must not** | `AttackComponent` (it doesn't swing), any controller / `MovementComponent` (an `Area2D` isn't a `CharacterBody2D`), `InventoryComponent` (it's an item, not a container) |

### Item wielded — `Node2D` (the weapon's `WieldScene`, instanced into the hand)
Its verbs are *attack* and possibly *wear out*.

| | |
|---|---|
| **Required** | nothing intrinsic — it is the weapon's own scene |
| **Optional** | `AttackComponent` (the wielder delegates the swing); `HealthComponent` as durability (`Died` = it breaks); `ParticleComponent`, `TrailComponent` |
| **Must not** | `PickupComponent` (it's held, not lying there); any controller / `MovementComponent` (the wielder moves; the weapon follows the hand) |

### Player — `CharacterBody2D`

| | |
|---|---|
| **Required** | the genre controller (`PlatformerController` / `TopDownController` / `ShooterController`), `HealthComponent` |
| **Optional** | `StatsComponent` (base damage/armor/move_speed — read by combat), `InventoryComponent`, `EquipmentComponent`, `AttackComponent`, `StatusEffectComponent`, `ResistanceComponent`, `KnockbackComponent`, `DashComponent`, `LevelingComponent`, `HealthBarComponent`, `FlashComponent` |
| **Must not** | `MovementComponent` **with a controller** (both call `MoveAndSlide` and fight — warned), `AIController`, `PickupComponent` (goes on the *item*; on a player its `BodyEntered` would free the player) |

> When an entity uses `StatsComponent`, author its base combat values there — not *also* on
> `HealthComponent.Armor` / `AttackComponent.Damage`, which are the fallbacks for entities without
> a `StatsComponent`. Equipment adds modifiers on top of the stat's base.

### Enemy / hostile — `CharacterBody2D`

| | |
|---|---|
| **Required** | `HealthComponent`, **`AIController` XOR `MovementComponent`** |
| **Optional** | `AttackComponent`, `AggroComponent`, `DropTableComponent` (rolls loot on `Died`), `KnockbackComponent`, `FlashComponent`, `HealthBarComponent`, `ResistanceComponent`, `StatsComponent` |
| **Must not** | a player controller (they read `Input` and would mirror the player's keys), `AIController` **+** `MovementComponent` together, `InventoryComponent` (an enemy's loot is a drop table), `PickupComponent` |

### NPC / quest giver — `CharacterBody2D` + child `Area2D` (see the split above)

| | |
|---|---|
| **Required** | `InteractableComponent` **on the Area2D**, `DialogComponent` **as its sibling** |
| **Optional** | `QuestComponent`, `AIController` (on the body, for a walking NPC) |
| **Must not** | `HealthComponent` on a non-combat NPC, `AttackComponent`, `PickupComponent` |

### Projectile — `Area2D`

| | |
|---|---|
| **Required** | `ProjectileComponent` (`Damage`, `DamageType`; `Shooter`/owner set by the spawner) |
| **Optional** | `ParticleComponent`, `TrailComponent` |
| **Must not** | `HealthComponent` (a bullet isn't a target — and its presence makes *other* bullets consume themselves on it), `FlashComponent` / `HealthBarComponent` (they resolve a sibling `HealthComponent` and no-op), `MovementComponent` |

### Destructible (crate, tree, rock) — `Node2D` / `StaticBody2D`

| | |
|---|---|
| **Required** | `DestructibleComponent`, **`HealthComponent`** (destructibles unify behind it — every damage source reaches them for free; `Died` → break) |
| **Optional** | `DropTableComponent` (rolls loot on the same `Died`) |
| **Must not** | `MovementComponent` / any controller (static, and a `StaticBody2D` isn't a `CharacterBody2D`), `PickupComponent` (it's broken, not collected) |

### Container / chest / door / crafting station — `Area2D`

`InteractableComponent` required (`DoorSwitchComponent` for key-gated doors); `HealthComponent` /
`MovementComponent` / `PickupComponent` must not.

---

## Genres whose honest archetype count is ~zero

Resist inventing archetypes here — for these, "almost none" is the design being right.

- **puzzle** — the board is one logic node (`Match3BoardComponent`, an `int[,]`); a gem is a *view*
  with a type integer and a screen position. **Zero components belong on a gem.** `HealthComponent`
  must not (a gem is *cleared*, not damaged); `MovementComponent` must not (falling is the array
  rewriting, not physics).
- **citybuilder** — one archetype, Building. A grid cell is **data** (a `Vector2I` key), not a node;
  a citizen is a **scalar** (`"Population: 250"`), not 250 agents. Buildings must not have
  `MovementComponent` or `HealthComponent`.
- **cardgame** — no entity archetypes. A card is a `Control`; `HealthComponent` / `MovementComponent`
  / `AttackComponent` / `PickupComponent` are all must-not (a `Control` is neither `Area2D` nor
  `CharacterBody2D`). Its needs are a card `Resource` hierarchy and a hand/deck — data, not entities.

---

## <a name="checkable"></a>Making the "must not" checkable

Guidance nobody can enforce becomes folklore. Two mechanisms keep these rules honest:

1. **`validate_scenes.sh`** resolves each script-bearing node's **parent type** and fails the build
   when an Area2D-required component (`Pickup` / `Interactable` / `DoorSwitch` / `Checkpoint` /
   `Projectile`) sits under a non-`Area2D` parent. Run it after touching any `.tscn`.
2. **`_Ready` warnings** — every parent-type resolution that fails says so (`GD.PushWarning`) rather
   than doing nothing. `AreaTriggerComponent` and the controllers already do; extend this whenever
   you add a component with a parent-type requirement.
