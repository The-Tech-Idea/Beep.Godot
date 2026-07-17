# Phase 6 — The missing components

**Goal:** name the framework-level gaps the archetype work exposed, ranked by leverage. Each
is general (`HazardComponent`), never content (`GoombaComponent`).

**Depends on:** Phase 5's archetype tables for the requirements.

---

## Ranked by reach

### 1. `EntityTagComponent` — nothing joins `"players"` or `"enemies"`

**The single highest-leverage addition in this report.** Three shipped components target
groups **nobody joins**:

- `AIController.TargetGroup = "players"` (`ecs/AIController.cs:21`)
- `TurretComponent.TargetGroup = "players"` (`ecs/TurretComponent.cs:17`)
- `ProjectileModifierComponent.TargetGroup = "enemies"` (`ecs/ProjectileModifierComponent.cs:22`)

Verified: no `AddToGroup("players")` anywhere. Only `InteractableComponent` survives, via a
`n.Name == "Player"` fallback (`:44`) — a hardcoded name, the same fragility as
`DoorSwitchComponent`.

So **every AI, every turret, and every homing projectile is inert by default, in every genre.**

`EntityComponent` already has the machinery — `ComponentGroup` + `AddToGroup` on `_EnterTree`
(`ecs/EntityComponent.cs:21-34`) — but it groups the **component node**, not the **body**, so
lookups expecting a `Node2D` never match. Either add a component that groups the *parent*, or
give `ComponentGroup` a "group my parent" mode. Small change, unblocks three systems.

### 2. `HazardComponent` — no way to damage on contact

No component damages a body on `Area2D` entry. Grep for `Hazard|KillZone|DamageZone` → nothing.
Consequences shipping today:

- `levels/platformer/level_1.tscn:38-42` — `LevelBoundary`, an `Area2D` + shape at y=1000,
  unmistakably a fall-out killzone. **No script, no connection.** Falling does nothing.
- `Hazards` is an empty `Node2D` in both platformer levels.
- `levels/shooter/*` — `WorldBounds`, an `Area2D` with **no shape and no script**.

Shape: Area2D-parented, on `BodyEntered` call `HealthComponent.TakeDamage(amount, type)`, with
`Continuous`/`OneShot`, `Cooldown`, `InstantKill`. Composes with `KnockbackComponent` the same
way `ProjectileComponent` already does (`:83-85`).

### 3. The three inert verbs

Shipped on templates, **zero callers**, verified by grep:

| Verb | Consequence |
|---|---|
| `AttackComponent.Attack()` (`:45`) | Nothing ever attacks. The whole melee/ranged pipeline is un-triggered. `AIController` emits `InAttackRange` (`:97`) and never calls it. |
| `AggroComponent.AddThreat()` (`:27`) | Threat table always empty, `CurrentTarget` always null. `HealthComponent.Damaged` is never routed to it. |
| `DestructibleComponent.TakeDamage(int)` (`:31`) | Every destructible is invulnerable — damage only lands on `HealthComponent`. |

These are **edges**, not components: `Damaged → AddThreat`, `AIController.InAttackRange →
Attack`, and either unifying `DestructibleComponent` behind `HealthComponent` or teaching
damage to find it. The last is the cleanest: give `DestructibleComponent` a sibling
`HealthComponent` and listen to `Died` — then every existing damage source reaches it for free,
and the duplicate `int HP` disappears.

### 4. The broken item edges

| Edge | State |
|---|---|
| `PickupComponent.Collected` → `InventoryComponent.AddItem` | **0 connections.** Identical signatures, never wired. Picking anything up puts it nowhere. |
| `HealthComponent.Died` → `DropTableComponent.Roll()` | Never. Only `DestructibleComponent.Break` rolls — and it can't be damaged (§3). **Nothing ever drops loot.** |
| `CraftingComponent.Craft` → the output item | **Deducts materials, grants nothing.** `ecs/CraftingComponent.cs` — the comment literally reads `// Grant result.` above an `EmitSignal(Crafted, recipe.OutputItem)` with no `AddItem`. Crafting is a material shredder. |
| `DropTableComponent`'s loot list | `private readonly List<DropEntry> _entries`, **no `[Export]`**, filled only by `AddEntry()` — which has no callers. A designer cannot author drops without C#. Fix with `DropTableEntry : Resource`, mirroring `CraftingIngredient`. |
| `QuestComponent.ProgressObjective` | Public, **no callers**. The shipped `quests.tscn` objective *"Defeat the Dark Lord (0/1)"* can never tick. |

Phase 4 covers the first; the rest belong here.

### 5. Genre-shaped gaps

| Missing | Genre | Note |
|---|---|---|
| `LevelTransitionComponent` | topdown | `TransitionZones` is an empty node; `LevelLoaderComponent.LoadLevel(int)` is public and is the right target — nothing calls it. |
| `DialogUIComponent` binding | topdown | `DialogComponent` emits `DialogStarted`; `topdown_main`'s `DialogLayer` is inert markup. *(A `DialogUIComponent` exists but builds its own UI and is parent-type-broken — see Phase 5.)* |
| `StatModifierComponent` | shooter | `LevelUpChoice` records a pick to `SetGameData` and says applying it "is the game's job" — but there is **no permanent-modifier channel**. `StatusEffectComponent` is timed-buff-shaped, and `ShooterController` consults it for **speed only**. So the roguelite's upgrades cannot affect anything. |
| `CharacterStatsComponent` | rpg | A general STR/DEX/INT block. `PlayerStatsComponent` is soccer (Phase 5). Gives `LevelingComponent.StatPoints` a destination. |
| `HarvestableComponent` | survival | "requires tool class X, yields item Y ×N". Currently `DestructibleComponent` + a code-only drop table + a pickup that doesn't reach the bag = three broken links for one loop. |
| `ContainerComponent` | rpg/survival | Chest = a second inventory + transfer. Nothing supports two inventories. |
| `CardDef : Resource` + deck/hand | cardgame | The genre is data-shaped; `hand_limit`/`card_fan_angle`/`card_hover_scale` are all inert. |
| vehicle controller | racing | None exists. *(Confirm against the racing/strategy/citybuilder/puzzle report.)* |

### 6. The refactor worth considering

**`AIController` self-drives** (`_body.Velocity = …; MoveAndSlide()`, `:62-63`), which is why it
is mutually exclusive with `MovementComponent`. If it instead wrote `DesiredDirection` into a
sibling `MovementComponent`, the AI-vs-Movement conflict class **dissolves in every genre**, and
`enemy_template` works as already composed. Bigger change; flagged, not scheduled.

## Sequencing

1. **`EntityTagComponent`** — unblocks three systems, small.
2. **The inert edges** (§3, §4) — mostly wiring existing signals; each is a few lines.
3. **`HazardComponent`** — fills three shipped-but-scriptless nodes.
4. Genre-shaped components (§5), as the genres are taken seriously.
5. The `AIController` refactor (§6), if at all.

## Verification

Per component: `dotnet build` 0 errors, `validate_scenes.sh` PASS, and an editor check that
the thing it unblocks now **actually happens** — e.g. for `EntityTagComponent`: an
`AIController` enemy chases the player, which today it provably cannot.
</content>
