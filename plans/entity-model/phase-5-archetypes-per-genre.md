# Phase 5 — Archetypes per genre

**Goal:** state, per archetype, which components are **required**, **optional**, and **must not
be present** — and make the "must not" checkable rather than folklore.

**Depends on:** Phases 1–4 for the archetypes that need items.

---

## The rule that generates every table

**A component belongs on an entity only if the entity *does* that thing.** A sword does not
attack — **its wielder does**. A sword's damage is *data the wielder reads*. That single
sentence is why this is a `Resource` hierarchy (Phase 1) and not a `SwordComponent`.

This is not pedantry; wrong-component-on-wrong-archetype ships today and fails **silently**:

- `projectile_template` shipped a `FlashComponent`, which resolves a sibling
  `HealthComponent` — a bullet has none, so it could never flash. *(fixed)*
- `topdown_main.tscn:48` and `robot_npc_template.tscn:38` put `InteractableComponent` under a
  `CharacterBody2D`. It needs an **`Area2D`** parent (`ecs/InteractableComponent.cs:34`) →
  `_playerInRange` never becomes true → **the topdown player cannot interact with anything**,
  and the NPC's "Talk" prompt leads nowhere. Still live.

## Parent type is part of the contract

Several components hard-require a parent and most fail **silently** when it's wrong:

| Component | Parent required | On mismatch |
|---|---|---|
| `ProjectileComponent` | `Area2D` | `PushError` — the only loud one |
| `PickupComponent`, `InteractableComponent`, `DoorSwitchComponent`, `CheckpointComponent` | `Area2D` | silent no-op |
| `MovementComponent`, `HungerStaminaComponent`, `AnimalBehaviorComponent`, any `ControllerComponent` | `CharacterBody2D` | warns *(MovementComponent since this session)* |
| `MovingPlatformComponent` | `AnimatableBody2D` | silent no-op |
| `AttackComponent`, `DestructibleComponent`, `CropGrowthComponent`, `SpawnerComponent`, `DropTableComponent` | `Node2D` | silent no-op |
| `DragComponent`, `TooltipComponent` | `Control` | silent |
| `FlipCardComponent` | `Container` with ≥2 Control children | silent |

**The archetype tables must carry the node type**, or "required components" is meaningless.

### The split every mobile-interactable needs

`InteractableComponent` needs `Area2D`; `AIController` needs `CharacterBody2D`. **Both cannot
be on one node.** A walking NPC is:

```
NPC (CharacterBody2D)
├── AIController            ← needs the body
└── InteractZone (Area2D)
    ├── InteractableComponent   ← needs the Area2D
    └── DialogComponent         ← must be a SIBLING of Interactable (it resolves a sibling)
```

Nothing in the repo documents or demonstrates this, and both shipped templates get it wrong.

---

## The archetype tables

### Item in the world (sword, shield, potion, coin) — `Area2D`

| | |
|---|---|
| **REQUIRED** | `PickupComponent` (+ `Item` = a `BeepItem` `.tres`, per Phase 4) |
| **OPTIONAL** | `BobComponent`, `RotateComponent`, `LifetimeComponent`, `ParticleComponent`, `AudioComponent` |
| **MUST NOT** | `HealthComponent` — not alive, and a bullet would "hit" it and be consumed (`ProjectileComponent.cs:78` finds any `HealthComponent`). `MovementComponent` / any controller — doesn't move, and an `Area2D` isn't a `CharacterBody2D`. `AttackComponent` — **the sword does not attack**. `InventoryComponent` — it is an item, not a container. `FlashComponent` / `HealthBarComponent` — both resolve a sibling `HealthComponent` and silently no-op. |

**This row is the answer to "what does a sword need?"** — one component and one resource.

### Player — `CharacterBody2D`

| | |
|---|---|
| **REQUIRED** | the genre's controller (`PlatformerController` / `TopDownController` / `ShooterController`), `HealthComponent` |
| **OPTIONAL** | `InventoryComponent`, `EquipmentComponent` (Phase 2), `AttackComponent`, `StatusEffectComponent`, `ResistanceComponent`, `KnockbackComponent`, `DashComponent`, `LevelingComponent`, `HealthBarComponent`, `FlashComponent` |
| **MUST NOT** | `MovementComponent` **with a controller** — both call `MoveAndSlide` and fight (warned since this session). `AIController` — same. `PickupComponent` — it goes on the *item*; on a player its `BodyEntered` would hide/free the player. `PlayerStatsComponent` — **see below**. |

> **`PlayerStatsComponent` is a trap.** Despite the name it is a **football/soccer** stat block —
> `Shooting`, `Passing`, `Dribbling`, `Tackling`, `Keeping`, `ShirtNumber`, `Position = "CM"`
> (`ecs/PlayerStatsComponent.cs:14-38`). Nothing reads it. It is wrong for RPG and shooter
> despite `rpg/character.tscn` displaying a Strength stat. `LevelingComponent.StatPointsPerLevel`
> has **nowhere to spend** — there is no general stat block.

### Enemy / hostile — `CharacterBody2D`

| | |
|---|---|
| **REQUIRED** | `HealthComponent` (the only thing damage can land on), **`AIController` XOR `MovementComponent`** |
| **OPTIONAL** | `AttackComponent`, `AggroComponent`, `DropTableComponent`, `KnockbackComponent`, `FlashComponent`, `HealthBarComponent`, `ResistanceComponent` |
| **MUST NOT** | a player controller — they read `Input` directly, so the enemy would mirror the player's keys. `AIController` **+** `MovementComponent` together. `InventoryComponent` — an enemy's loot is a drop table. `PickupComponent`. |

### NPC / quest giver — `CharacterBody2D` + child `Area2D` (see the split above)

| | |
|---|---|
| **REQUIRED** | `InteractableComponent` **on the Area2D**, `DialogComponent` **as its sibling** |
| **OPTIONAL** | `QuestComponent`, `AIController` (on the body, for a walking NPC) |
| **MUST NOT** | `HealthComponent` on a non-combat NPC. `AttackComponent`. `PickupComponent`. |

### Projectile — `Area2D`

| | |
|---|---|
| **REQUIRED** | `ProjectileComponent` (+ `Shooter` set by the spawner) |
| **OPTIONAL** | `TrailComponent`, `ParticleComponent` |
| **MUST NOT** | `HealthComponent` — a bullet isn't damageable, and its presence makes it a *target* other bullets consume themselves on. `LifetimeComponent` — `ProjectileComponent` owns lifetime; two timers freeing one node. `FlashComponent` / `HealthBarComponent` — sibling-health no-ops. `MovementComponent`. |

### Destructible (crate, tree, rock) — `Node2D` / `StaticBody2D`

| | |
|---|---|
| **REQUIRED** | `DestructibleComponent` |
| **OPTIONAL** | `DropTableComponent` (auto-rolled on break) |
| **MUST NOT** | `HealthComponent` — `DestructibleComponent` has its own `HP` (`int`); two HP pools on one node, and **only one is reachable**. |

> **`DestructibleComponent` cannot be hit.** `AttackComponent.cs:99` and
> `ProjectileComponent.cs:78` look **only** for `HealthComponent`. `TakeDamage(int)`
> (`ecs/DestructibleComponent.cs:31`) has zero callers. So every destructible in every genre
> is invulnerable. See Phase 6.

### Container / chest — `Area2D`, Door — `Area2D`, Crafting station — `Area2D`

`InteractableComponent` required; `HealthComponent` / `MovementComponent` / `PickupComponent`
must not. `DoorSwitchComponent` for gates — note its `RequiredItem` check finds the player via
`FindChild("Player", false, false)`, a **hardcoded, non-recursive node name**
(`ecs/DoorSwitchComponent.cs:55`): rename or nest the player and every gated door silently
stops working.

### The genres whose honest archetype count is ~zero

**Resist the pressure to invent archetypes here.** For three genres the right answer is "almost
none", and in two of them that is a sign the design is *right*.

**puzzle — the board is one logic Node; a gem is a *view*.** `Match3BoardComponent` holds the
whole game in an `int[,]` and implements swap, match, clear, gravity, refill and cascade with
**no Node per cell**. That is correct and must not be entity-ized. A gem has a type integer and
a screen position — **zero components belong on it**. `HealthComponent` **must not**: a gem is
not damaged, it is *cleared*, and only the board decides that; HP would be a second, conflicting
source of truth for "is this cell empty". `MovementComponent` **must not**: gem falling is
`ApplyGravity()` rewriting the array, not physics — a physically-moving gem would drift out of
sync with its own cell. Fall animation is a tween of a view toward a board-dictated cell.

> Puzzle's gap is **wiring, not archetypes**, and it is stark: `Swap()` has **0 callers** and
> nothing subscribes to `CellChanged` — the board shuffles itself at `_Ready` and then sits
> inert forever, headless, with no input path and no renderer. Its `ScoreChanged` reaches
> neither the HUD nor `GameFlow`, so `target_score` can never be hit and
> `level_complete.tscn` is unreachable from play. Verified: 0 references outside the
> component's own file.

**citybuilder — one archetype: Building.** A grid cell is **data** (`Vector2I` keys in a
placement component), not a Node — at `build_grid_size: 32` a Node per cell is thousands of
Nodes for zero benefit, and `Match3BoardComponent` already demonstrates the right pattern
(grid-as-array + a `CellChanged` signal, rendering left to the game). A citizen is a **scalar**,
not an agent: `"Population: 250"` is an aggregate, and spawning 250 Nodes to make a label say
250 would be a design error. Buildings **must not** have `MovementComponent` (static, and it
needs `CharacterBody2D`) or `HealthComponent`/`HealthBarComponent` (no combat; demolition is an
action, not HP depletion — contrast strategy, where building HP *is* correct).

**cardgame — no entity archetypes.** It disables every world system in its `genre.json` and
ships no `LevelContainer` or levels. A card is a `Control`; `HealthComponent` /
`MovementComponent` / `AttackComponent` / `PickupComponent` are all **must not** (a Control can
never be an `Area2D` or `CharacterBody2D`). Its needs are a `CardDef : Resource` hierarchy and
a hand/deck — data, not entities.

### Two false friends — do not plan around the name

- **`NavigationComponent` is not pathfinding.** It is a scene-transition button auto-wirer.
  Nothing in the addon touches `NavigationAgent2D` or A*, so the `NavigationRegion2D` in the
  topdown levels is decorative. Putting it on a unit silently hijacks Buttons in the parent tree.
- **`MarqueeComponent` is not an RTS drag-select marquee.** It is a scrolling text ticker on a
  `Label`.
- Also: **`TrainingComponent` and `ContractComponent` are football-manager residue**
  (`WeeklyWage`, `ReleaseClause`, `ContractExpiry`, `TrainingFocus`) — not RTS production, despite
  reading like it. Same trap as `PlayerStatsComponent`.

---

## Make the "must not" checkable

Guidance nobody can enforce becomes folklore. Two mechanisms, cheap:

1. **Extend `validate_scenes.sh`.** It already parses every `.tscn`, knows each node's `type=`,
   and knows which script each node carries. Adding *"`PickupComponent` on a non-`Area2D`
   parent"* and *"`HealthComponent` on the same node as `ProjectileComponent`"* is the same
   shape as the `[Export]`-name check already there — and it would catch the live
   `InteractableComponent` bug above.
2. **Warn at `_Ready`.** Every silent parent-type cast should say so (`CLAUDE.md` §
   *Never fail silently*). Several already do after this session; the `Area2D` group does not.

## Verification

- `validate_scenes.sh` → PASS, and **fails** when a `PickupComponent` is parented to a
  `Node2D` (prove the check before trusting it).
- The tables land in `addons/beep_game_builder_cs/INDEX.md` or a new `docs/ARCHETYPES.md` —
  the place a developer looks before composing an entity.
</content>
