# Master TODO Tracker — Entity & Item Model

> Tracks the entity/item/equipment model for `addons/beep_game_builder_cs/`.
> Per-phase detail lives beside this file: `plans/entity-model/phase-N-*.md`.
>
> **Goal:** a Godot developer can model an entity — a player, an enemy, a **sword**, a
> **shield**, a potion — by composing existing components plus **data**, with the framework
> stating which components an archetype needs and which are wrong for it.
>
> **Scope:** framework only. Components, data model, composition guidance, validation.
> Not game content — no balance, no level layout, no assets. See `CLAUDE.md` § *Scope*.

---

## The problem

A sword is **both** a data problem and a composition problem, and the framework has no way to
express either half.

- **There is no weapon, equipment, armor, or shield component.** Verified: nothing in
  `ecs/` matches `weapon|equip|item|gear|armor|shield`.
- **`AttackComponent` bakes the numbers into the wielder** — `Damage`, `Range`, `Cooldown`,
  `IsRanged`, `ProjectileScene` are all `[Export]`s on the attacker
  (`ecs/AttackComponent.cs:13-18`). Equipping a sword cannot change any of them.
- **`PickupComponent` carries a string, not an item** — `[Export] string ItemId = "coin"`
  (`ecs/PickupComponent.cs:13`). A sword on the ground is the *word* "sword"; its damage
  exists nowhere.
- **Items are the only unloved data in the repo.** `InventoryComponent.InventoryItem`
  (`ecs/InventoryComponent.cs:30`) is a **plain nested class** with a stringly-typed
  `Dictionary<string, Variant> Stats` bag — so it cannot be authored in the inspector, saved
  as `.tres`, dragged onto an `[Export]`, or subclassed. Everything else already uses the
  idiomatic pattern: `GameInfo`, `UISkin`, `ColorPalette`, `GeometryProfile`,
  `CraftingRecipe`, `CraftingIngredient`, `QuestObjective` are all
  `[Tool][GlobalClass] : Resource`.

**The receiving half already exists and is idle.** `HealthComponent.Armor`
(`ecs/HealthComponent.cs:16`) and `ResistanceComponent`'s per-type multipliers
(`ecs/ResistanceComponent.cs:15-20`, matching `DamageTypeComponent.Type`) are ready for
armor to drive them. Nothing does.

## The approach

**Classes + inheritance for item data (Godot `Resource` subclasses), components for
behaviour — and a sword is *both*, because it has two representations.**

- **Definition** — `GameWeapon : Resource` (`.tres`). What stacks, saves, and appears in a
  shop. Not in the scene tree, so it cannot carry components. 99 potions cannot be 99 nodes.
- **Instance** — a node in the world. **This one can carry components**, and often should:
  a *wielded* sword may legitimately have `AttackComponent` (it needs only a `Node2D` parent)
  and `HealthComponent` as durability (it is a blind component; `Died` = it breaks). The
  definition points at it via `[Export] PackedScene? WieldScene`.

This mirrors what the repo already does for bullets: `ProjectileScene` is a `PackedScene`
whose instance carries `ProjectileComponent`. A weapon is the same shape.

**The rule is therefore not "a sword is only data"** — an earlier draft said that and it was
wrong. It is: *give an archetype a component only if **that representation** of it does that
thing.* A sword on the floor doesn't swing; a wielded one does; a save-file row isn't a node.

### One base, with the traits that decide composition

```
GameItem : Resource        Id, Icon, Rarity, MaxStack, WorldScene
                           IsStatic        — stays put (anvil, chest, rock) vs carried
                           IsDestructible  — can be broken   (+ MaxDurability)
  ├── GameEquipment        Slot, WieldScene
  │     ├── GameWeapon     Damage, DamageType, IsRanged, ProjectileScene
  │     ├── GameShield     Defense, BlockChance, Resistances
  │     └── GameArmor      Defense, Resistances
  ├── GameLiquid           Volume, IsDrinkable, HealAmount   (potion, fuel, oil)
  └── GameConsumable       HealAmount, StatusEffectId, Duration
```

`IsStatic` / `IsDestructible` are the load-bearing part. They make the archetype rules
**derivable from the data** rather than remembered from a table:

| | | implies | forbids |
|---|---|---|---|
| anvil | static, indestructible | — | `MovementComponent`, `HealthComponent`, `PickupComponent` |
| rock | static, destructible | `HealthComponent` | `MovementComponent`, `PickupComponent` |
| sword | carried, destructible | `PickupComponent` (grounded), `HealthComponent` (durability) | — |
| potion | carried, indestructible | `PickupComponent` (grounded) | `HealthComponent` |

Four rows, four compositions, no folklore — and a validator can check a `WorldScene` against
its own `GameItem`'s traits (Phase 5).

**Equipment reaches combat through the pattern the codebase already uses.**
`AttackComponent` resolves an *optional* sibling and asks it for a modifier
(`ecs/AttackComponent.cs:51-56`, via `StatusEffectComponent.GetModifier`,
`ecs/StatusEffectComponent.cs:125`). `EquipmentComponent` answers the same shape, so an
entity without equipment is unaffected and nothing existing breaks.

---

## Progress

- [ ] **Phase 0 — Component disposition** — decide what the existing ~146 components *are*
      before adding any. **Blocks Phase 6**; its `DropTableComponent` and `AreaTriggerComponent`
      items block Phases 4 and 3.
      → `phase-0-component-disposition.md` (summary + rationale)
      → **`components/`** (per-component work orders — the detail)

### Component tracker → `components/`

Per-component disposition and fix for **every** component. `phase-0` is the *rationale*;
`components/` is the *work*. Where they disagree, `components/` wins.

| Doc | Covers | # | Status |
|---|---|---|---|
| [`components/README.md`](components/README.md) | index, verdicts, the 4 structural facts | — | [ ] |
| [`components/infra.md`](components/infra.md) | bases, autoloads, app spine, skin layer | 24 | [ ] |
| [`components/combat.md`](components/combat.md) | health, damage, attack, AI, projectiles | 22 | [ ] |
| [`components/movement.md`](components/movement.md) | controllers, abilities, camera | 17 | [ ] |
| [`components/world.md`](components/world.md) | atmosphere, weather, world FX, juice | 24 | [ ] |
| [`components/items.md`](components/items.md) | inventory, crafting, drops, pickups, production | 17 | [ ] |
| [`components/ui-widgets.md`](components/ui-widgets.md) | the drop-in widget library | 40 | [ ] |
| [`components/ui-screens.md`](components/ui-screens.md) | HUD, menus, the 33 screen scripts | 22 | [ ] |
| [`components/DELETE.md`](components/DELETE.md) | removals, with justification | 16 | [ ] |

### Phase 0 execution order

Two ~1-line wins first — each switches on a chain that is **already fully coded**:

- [x] **1. Place a pickup in a level** — 3 coins instanced into `levels/platformer/level_1.tscn`
      `Pickups`. `Pickup:102 → AddScore → ScoreChanged → HudComponent:45` is now reachable.
- [x] **2. `SpawnGroup = "enemies"`** — default changed in `SpawnerComponent.cs:19` (all spawners),
      so it un-inerts homing/turrets everywhere, not just one level.
- [x] **3. `EntityComponent` group-my-parent** — added `[Export] EntityGroup` that groups the
      **parent body** (not the component node), with a `PushWarning` when the parent is not a
      Node2D (the exact case targeting silently skips). Player tagged `"players"` in the 3 mains;
      `enemy_template` tagged `"enemies"`.
- [x] **4. `DropTableComponent` `[Export]`** — new `DropTableEntry : [GlobalClass] Resource`;
      `_entries` (no export) → `[Export] DropTableEntry[] Entries`. `Roll()` now warns on empty
      instead of silently returning. Nullable-enum gates became `AnySeason`/`AnyWeather` bools.
- [x] **5. `CraftingComponent` grant the output** — added the missing
      `inventory.AddItem(recipe.OutputItem, recipe.OutputCount)`. No longer a material shredder.
- [x] **6. `Pickup.Collect → Inventory.AddItem`** — collector's inventory resolved from the
      entering body; item added. **Also fixed the respawn bug** (hiding instead of disabling the
      parent's `ProcessMode`, which was stopping the component's own timer) and wired the dead
      `FloatingTextComponent.ShowText("+100")`.
- [x] **10. `StatusEffectComponent` permanent channel** — `Duration < 0` ⇒ `IsPermanent`: never
      ticks, never expires. `IsExpired => !IsPermanent && Duration <= 0`. The channel a level-up
      upgrade lives in. *(Done early — tiny, unit-independent, unblocks `LevelUpChoice`.)*
- [x] **7. `AreaTriggerComponent` base** + port the two broken triggers (M) — new
      `ecs/categories/AreaTriggerComponent.cs`: the Area2D counterpart to
      `ControllerComponent.ResolveBody2D` — resolves the parent Area2D, **warns** on a wrong
      parent instead of silent-nulling, wires `BodyEntered`/`BodyExited` once, tears them down in
      `_ExitTree`; subclasses override `OnBodyEntered`/`OnBodyExited` and nothing else.
      `InteractableComponent` (was `GameplayComponent`) and `DoorSwitchComponent` (was
      `WorldComponent`) re-based onto it — each dropped its hand-rolled `GetParent() as Area2D` +
      manual subscription. Both shipped `Interactable` nodes sit on a `CharacterBody2D`, so they
      now **warn** on load rather than dying silently (the scene reparent is item 5 in `items.md`).
      Build clean, validator PASS. **Unblocks Phase 3's melee hitbox**; `Hazard`/`LapGate`/level-
      transition-zone become ~20-line subclasses. → `items.md`
- [x] **8. Damage: `GameDamage` packet + delete the 1-arg `TakeDamage`** (L) — **gates 8
      components.** New `GameDamage` readonly struct (`Amount`, `Type`, `Source`, `IsCrit`) with
      **no all-defaults ctor — type is required at every call site**, so the silent-Physical
      default that ate the type system cannot return. `HealthComponent.TakeDamage(GameDamage)`
      replaces **both** float overloads; all four callers (`AttackComponent`, `ProjectileComponent`,
      `TemperatureComponent`, `HealthComponent` passive) now pass an explicit `DamageType`. The
      `Type` enum was extracted out of the dead `DamageTypeComponent` into its own `DamageType.cs`
      (`ResistanceComponent` re-pointed at it), so the packet doesn't depend on the doomed class —
      **item 11 is now a pure file-delete of `DamageTypeComponent.cs`.** Build clean, no scene
      impact (`DamageTypeComponent` in 0 scenes). → `combat.md`, and Phase 3a
- [x] **9. `DestructibleComponent` unify behind `HealthComponent`** (M) — deleted the private
      `int HP` / `_currentHP` / `TakeDamage(int)` / `Damaged(int)` (all 0 callers, 0 scenes); it now
      resolves a sibling `HealthComponent` and breaks on `Died`. **Warns** when no sibling health
      exists (else it could never break); `Break()` is public + idempotent via a `_broken` latch;
      `_ExitTree` detaches. Every damage source (now packet-typed, item 8) reaches destructibles for
      free. Build clean.
- [~] **11. The delete list** (`DELETE.md`) — **safe deletes done (signed off).** Removed 12 dead
      files (+ `.uid`): `EntitySystem`, `LightingComponent`, `GameInfoNode`, the 4 football
      components, `Bob`/`Rotate`/`Parallax`/`Lifetime`, and `DamageTypeComponent.cs` (enum already
      extracted in item 8). Dead members: `FlashComponent._flashMaterial`, `ProjectileComponent`'s
      unused `hitNode` binding, `ProjectileModifierComponent.Spread` (enum value with no case,
      silently `Straight`), `BossHealthBarComponent.SlideDuration`. Removed the 13 dead private
      `ChangeScene` helpers (verified 0 callers each; the 19 live ones untouched). Stale
      `GameInfoNode` doc ref in `SkinPropertyHints` fixed; football doc-residue was already clean.
      Build + validator green. **HELD, need separate handling:** absorb-first (`FlyComponent`,
      `ui/Pulse`+`Shake`+`SlideInOut`) and borderline (`AnimalBehavior`, `Audio`, `Cooldown`).
- [~] **12. The cheap wires** — **`InteractionPrompt` done** (~4 lines in `InteractableComponent`:
      lazy cross-tree `FindComponent<InteractionPromptComponent>(GetTree().Root)`, driven from the
      enter/exit overrides, activating the dead `PromptText` export; build-clean). **Remaining are
      scene edits needing an editor pass** (can't be Godot-verified from CLI): `ScreenShake` into the
      camera (caller already exists) + `MaxTrauma` fix, `SquashAndStretch` + `Jump` into
      `platformer_main` (producers already live), `AnimatedMenuComponent` reparent in 5 scenes.
- [x] **13. The five silent widget bugs** — all fixed, build-clean:
      `RatingComponent:56` closure now captures `idx` not the loop var; `TooltipComponent` gated on
      a new `_hovering` flag (no more pop-on-load); `TableComponent` rows are `PanelContainer`s that
      actually render zebra + hover (the orphaned `Panel` is gone); `SearchBarComponent` emits once
      per settle via `_debouncePending` (was a repeater); `ToastNotificationComponent` clears its
      static in `_ExitTree` + `Show` guards `IsInstanceValid`.
- [x] **Phase 1 — Item resources** — **done.** (1–3) `ecs/items/`: `GameItem` +
      `GameEquipment`(EquipSlot) + `GameWeapon`/`GameShield`/`GameArmor` + `GameLiquid`/
      `GameConsumable`, all `[Tool][GlobalClass] Resource`. `ItemRarity` extracted standalone;
      `DamageType` reused (item 8); `AttackSpeedMultiplier` dropped, durations clock-units.
      (Resolution) **`GameItemCatalog`** (signed-off choice) — a static, lazy, recursive `.tres`
      scan of `ItemsRoot` (default `res://items`) mapping `Id → GameItem`, mirroring `SkinCatalog`;
      `Register()` for runtime items; warns on empty/duplicate ids. (4) `InventoryComponent` now
      holds `InventorySlot?[]` (`Item`+`Quantity`+per-instance `Durability`/`Socketed`); nested
      `InventoryItem`/`ItemType`/`Stats`/`RegisterItem`/`GetTemplate` gone; `AddItem(GameItem)`,
      id-based `HasItem`/`RemoveItem`/`CountItem`; Display/Interact partials updated. (6) crafting
      strings retired — `CraftingIngredient.Item`/`CraftingRecipe.OutputItem` are `GameItem`,
      `Craft` warns on a null output; `PickupComponent` resolves its (still-string) `ItemId` via the
      catalog. (5) `Load()` re-resolves ids via the catalog and warns on a miss; **per-instance
      durability/socket persistence deferred to Phase 7** (nothing mutates them before then — the
      Save note says so). Build + validator green. → `phase-1-item-resources.md`
- [~] **Phase 2 — Stats & equipment** — **2a foundation done.** `ecs/stats/`: `StatModifier`
      (`Stat` id, `Op` Add/Multiply, `Amount`, `Duration` clock-units `< 0`=permanent, `Source`
      GodotObject for identity-withdrawal), `Stat` (BaseValue + modifiers → cached idempotent
      `Value` = (base+adds)×muls, `Changed` signal), and **`StatsComponent`** — the entity's one
      stat block: `GetValue`/`AddModifier`/`RemoveBySource`, and **the single duration ticker**
      (clock-driven like `WorkComponent`: `TurnEnded` if a `TurnManager` is present, else `_Process`).
      Producers only add/remove; they never tick — resolves the Duration-ownership tension.
      Build-clean. **Still to do:** 2b — refactor `StatusEffectComponent` onto `Stat` (replace the
      magic-string `GetModifier` API; move its 3 call sites: `AttackComponent`/`ShooterController`/
      `HealthComponent`; keep `speed_boost`+`damage_reduction` working — the regression test), and
      2c — `EquipmentComponent` (contributes item modifiers by Source, withdraws on unequip).
      → `phase-2-equipment.md`
- [ ] **Phase 3 — Damage packet, then combat integration** — **3a blocks 3b.** Includes the
      melee-hitbox fix that makes `GameWeapon.Range` real. → `phase-3-combat-integration.md`
- [x] **Phase 4 — Pickups & drops** — **done.** `PickupComponent.ItemId` (string) → `[Export]
      GameItem? Item` (null = valid score-only pickup); `Collect` adds the item directly and warns
      when an Item is set but the collector has no inventory. `Collected → AddItem` was already
      wired (item 6). `DropTableEntry.Scene` → `GameItem Item`; `Roll()` spawns the item's
      `WorldScene` and **stamps the dropped node's `PickupComponent.Item`** so the drop→pickup→
      inventory loop closes; `DropSpawned` now carries the `GameItem`. **`Died → Roll` wired** on
      `DropTableComponent` (loot-on-death); `DestructibleComponent` lost its `DropsOnDestroy`/`Roll`
      (both listen to the same `Died`, so a destructible-with-loot drops once, not twice).
      `pickup_template.tscn` ships an inline `GameItem` "coin" sub-resource. Build + validator green.
      **Validator gap fixed:** the "script on a typed node" check false-flagged the scripted
      `[sub_resource]`; it now tracks node context (verified it still catches a real
      script-on-CharacterBody2D). ⚠ The scripted sub-resource itself needs an editor pass to confirm
      it instantiates (CLI can't run Godot). → `phase-4-pickups-and-drops.md`
- [ ] **Phase 5 — Archetypes per genre** — required / optional / **must not have**, and make
      "must not" checkable. → `phase-5-archetypes-per-genre.md`
- [ ] **Phase 6 — Missing components** — ranked by leverage. **Read Phase 0 first** — 9 of the 16
      proposals already exist. → `phase-6-missing-components.md`
- [~] **Phase 7 — Dependencies & time** — **Part B (time) done; this unblocks Phase 2.** New
      `TurnManager` autoload (a Lamport clock: `CurrentTurn` + `TurnEnded` + `EndTurn()`, static
      `Instance`), registered by the generator **only when** `genre.json tuning.time_axis == "turns"`
      (added to cardgame + strategy); `GameInfo.TimeAxis` + `ApplyTuning` plumb it. Its presence in
      the tree IS the axis signal — no per-component genre lookup. `WorkComponent` finally has its
      driver (real-time `_Process`, turn-based `TurnEnded`; keeps `Tick(double)`), plus `Progress`
      un-inverted and the looping-producer `WorkStarted` re-emit; warns if axis is turns but no
      `TurnManager`. **The 7-second-seasons bug is dead at the root:** `DayNightCycleComponent` now
      exposes `DaysElapsed`; `SeasonalComponent` advances per in-game DAY (deleted `_seasonTimer`),
      warning if AutoCycle is on with no day/night clock. `CardBattle.OnEndTurn → TurnManager.EndTurn`.
      Build + validator green; both genre.json valid. **Part A (dependencies) mostly landed in Phase 1**
      — `GameWeapon.AmmoItem`/`AmmoPerUse`, `GameEquipment.SocketCount` + `InventorySlot.Socketed`,
      and crafting `GameItem` fields all exist. **Still open:** the author-time cycle check
      (recipes/sockets/research) and `ResearchNode : Resource` (strategy); ammo/socket *consumers*
      arrive with combat (3b). → `phase-7-dependencies-and-time.md`

### Per-genre item trees → `items/`

`items/README.md` is the **shared spine** and the rule for what may become a class; one doc
per genre beside it. Read the README first — the genre docs conform to it.

The rule, because it is the whole game: **a class earns existence only by adding a field or
behaviour its parent cannot express.** `sword_iron.tres`, `axe.tres` and `dagger.tres` are
`.tres` files of `GameWeapon` — a `SwordClass` would be a rename. A genre never gets a class
for being that genre; it earns one only by introducing a *kind of thing* the spine cannot
describe.

**Half the genres want no item tree at all.** They want a **spec** — a `.tres` describing a
vehicle, a building, a unit, a card — which is the same idea one level over: data by
inheritance, instanced through a `PackedScene`. Where a genre says "no items", its doc says
what it wants instead rather than forcing it into `GameItem`.

## What the analysis changed

Four parallel audits, hand-verified. Three findings reshaped the plan:

1. **`DamageTypeComponent` is dead** — 0 resolvers; every `TakeDamage` call in the addon uses
   the 1-arg overload, which hardcodes `Physical`. So `ResistanceComponent`'s Fire/Ice/Poison/
   Holy/Dark/Lightning **can never fire**, and an armour's resistances would be decorative.
   This became Phase **3a**, a prerequisite — the original plan assumed a pipeline that isn't
   there.
2. **`AttackComponent.Range` is never read** — melee is a point query at the cursor. So
   `GameWeapon.Range` must **not** be added in Phase 1 until melee hit detection is decided; a
   field that silently does nothing is this repo's signature defect.
3. **The item edges don't exist.** `Pickup.Collected → Inventory.AddItem`: **0 connections**.
   `Died → DropTable.Roll`: never. `Craft()` **deducts materials and grants nothing** — the
   comment reads `// Grant result.` above an `EmitSignal` with no `AddItem`. Building an item
   model on top of these would have produced beautiful data nothing could move.

**Highest leverage overall is not in this initiative:** nothing joins the `"players"` /
`"enemies"` groups, so `AIController`, `TurretComponent` and `ProjectileModifierComponent` are
inert in every genre (Phase 6, §1).

## Decisions

- **The genre picks the time axis. There is no `GameClock`.** The plan asked what an item *is* and
  what it *does*, never what it **needs** or **how long things take**. `StatModifier.Duration :
  float` (Phase 2) silently assumed real seconds, so **a cardgame buff lasting 3 turns could not be
  expressed** — cardgame + strategy are 2 of 10 genres. And it has **already cost a shipped
  feature**: `SeasonalComponent:74-75` adds real `delta` to a timer compared against
  `DaysPerSeason = 7.0` **commented `// in-game days`** → seasons rotate every **7 seconds**, while
  `DayNightCycleComponent:72` does the conversion *correctly* in the same scene, unread.

  **A first draft specified `GameClock { Mode, Ticked, AdvanceTurn }`. Researched against practice,
  it was the wrong abstraction** — (a) a **union interface**: `Delta`/`Scale`/`Paused` are
  meaningless for turns and `AdvanceTurn` for real time, so every implementer no-ops on half of it;
  (b) it **silently desyncs from `Timer`/`SceneTreeTimer`/`AnimationPlayer`/tweens**, all hardwired
  to `Engine.time_scale` — clean build, green validator, "the animation and the cooldown disagree"
  months later; Godot **rejected** per-node `time_scale`
  ([#2507](https://github.com/godotengine/godot-proposals/issues/2507)); (c) **Rule of Three** — two
  known cases is where you do *not* abstract.

  **What lands instead:** real-time genres use `delta` and `Engine.time_scale` (free interop);
  day/night + seasons are **derived views — a multiply, not a clock**; turn-based genres get a
  `TurnManager` that is an `int` and a signal (a **Lamport logical clock** — it shares no machinery
  with real time, which is *why* one interface couldn't hold both). **`genre.json`
  `tuning.time_axis` selects.** One axis per genre holds across every duration the framework needs —
  nothing mixes — so `Duration` needs **no unit tag** and no consumer branches.
  **`WorkComponent.Tick(double delta)` was right all along**: keep the signature, drive it from
  `_Process` or `TurnEnded`. → `phase-7-dependencies-and-time.md`
- **Construction deps stay on the recipe; consumption deps go on the item.** `CraftingRecipe`
  (`CraftingComponent.cs:53-60`) **already** models item-depends-on-items, and **recipe-owned is
  correct** — the same sword may be craftable three ways, so `RequiredItems[]` on `GameItem` would
  hardcode one path. Adopt it. But a gun *is* a thing that eats ammo regardless of how it was made,
  so `GameWeapon.AmmoItem` belongs on the item. **Sockets are per-instance** — `SocketCount` on the
  definition, `Socketed[]` on the slot; putting them on the `.tres` would share one set of gems
  across every iron sword, the same trap as durability. **A technology is not an item** — research
  prerequisites are a graph over nodes, not over `GameItem`s.
- **Audit before adding — the plan was written backwards.** It proposed 16 new components
  without first asking what we have. A four-way audit, every load-bearing claim hand-verified,
  found **9 of the 16 already exist** in a form that needs a fix or a wire, not a new type — and
  that roughly as many shipped components are inert, misnamed or dead as were proposed to be
  added. Adding to that pile without disposing of it makes the framework worse. Hence **Phase 0**,
  which lands first. The sharpest cases:
  - **`EntityTagComponent`** → `SpawnerComponent.cs:19` already groups the **body**
    (`:73 inst.AddToGroup(SpawnGroup)`); its default is just `"spawned"` instead of `"enemies"`.
    One default change makes turrets and homing work.
  - **`LevelTransitionComponent`** → `LevelLoaderComponent.cs:56` says verbatim *"this doubles as
    a runtime level transition."* It needs a caller, not a sibling.
  - **`InteractionPromptComponent`** → exists, complete, ~4 lines from working. Someone walked up
    to this wire and stopped.
- **`EntityComponent.ComponentGroup` is free to redefine.** It groups `this` — and
  `EntityComponent : Node`, **not `Node2D`** — while `AIController.cs:139` and
  `TurretComponent.cs:83` filter `node is Node2D`. So it is *structurally incapable* of serving
  the only consumers that want it. Verified free: **0 scenes set it**, and the components that
  really use groups (`DynamicFogLayer:64`, `SeasonalComponent:55`, `WeatherSystemComponent`)
  **bypass the export** and hardcode `AddToGroup` in `_Ready` — `WindFieldComponent.cs:119` even
  documents the workaround. The export has never been the channel.
- **`DestructibleComponent` unifies behind `HealthComponent` — the plan was arguing with
  itself.** Phase 5 said an archetype **MUST NOT** carry `HealthComponent` because
  `DestructibleComponent` has its own `[Export] int HP` (`:14`, verified) and two pools would
  conflict. Phase 6 said the opposite. **Phase 6 wins.** The conflict was real, but forbidding
  the *working* pool to protect the duplicate got it backwards: that private `HP` is exactly
  *why* destructibles cannot be hit — `AttackComponent.cs:99` and `ProjectileComponent.cs:78`
  resolve **only** `HealthComponent`, so `TakeDamage(int)` (`:31`) has 0 callers and every
  destructible in every genre is invulnerable. Delete the duplicate, keep the real one: every
  damage source reaches destructibles for free and `Died → Break()` replaces the dead entry
  point. **A contract that codifies a bug as a design is worse than no contract.**
- **`AreaTriggerComponent` — one primitive, five times.** Seven components hand-roll
  "Area2D-parented body trigger", **and two are BROKEN by exactly the parent-type failure the
  pattern invites**. Hazard, LevelTransition and LapGate are the same shape. Extract the base
  once: three "new components" become ~20-line subclasses and two BROKEN entries are fixed as a
  side effect. It is also Phase 3's melee hitbox — build it once.
- **`GameItem`, not `BeepItem`.** The repo splits its namespaces: `Game*` is the model
  (`GameInfo`, `GameStateData` — both `Resource`s), `Beep*` is tooling (`BeepFileUtils`,
  `BeepGenreGenerator`). An item is model.
- **Variation by inheritance, never by a component per kind.** One `GameWeapon`, not a
  `SwordComponent` / `AxeComponent` / `BowComponent`. Kinds that differ in *fields* are
  subclasses (`GameShield` has `BlockChance`; `GameLiquid` has `Volume`); kinds that differ
  only in *values* are `.tres` files of the same class.
- **The base carries traits, and the traits drive composition.** `IsStatic` and
  `IsDestructible` on `GameItem` determine what the world instance may be built from — an
  anvil (`static, indestructible`), a rock (`static, destructible`), a sword
  (`carried, destructible`), a potion (`carried, indestructible`) each compose differently,
  and **the data says how**. This is what makes Phase 5 checkable instead of folklore.
- **Two representations.** Definition = `Resource` (stacks, saves, shops). Instance = a node
  (`WorldScene` / `WieldScene`) that **may** carry `AttackComponent`, durability, a hitbox.
  Same shape the repo already uses for bullets (`ProjectileScene` + `ProjectileComponent`).
- **One modifier system: `Stat` + `StatModifier`.** A modifier is `{stat, op, amount, duration,
  source}`; `duration 0` = permanent. Equipment, timed buffs and permanent upgrades stop being
  three mechanisms. `EquipmentComponent` **contributes** modifiers rather than exposing
  `DamageBonus`/`DefenseBonus`/`ResistanceFor` accessors — those needed a new accessor plus a
  consumer edit per stat, which is how `StatusEffectComponent` ended up consulted at two
  hardcoded sites with two magic-string keys. Matches [community
  practice](https://minoqi.medium.com/modular-stat-attribute-system-tutorial-for-godot-4-0bac1c5062ce);
  fixes the shooter's missing permanent-upgrade channel and the two-damage-paths blocker for
  free. `StatusEffectComponent` is refactored onto it, API replaced.
- **~~Additive, not a rewrite.~~ WITHDRAWN.** It was premised on back-compat we do not have
  (dev code, no shipped consumers, no fallbacks or stubs). Three recommendations were hedged
  *only* to avoid touching signatures, and each left a field that silently does nothing — this
  repo's signature defect. They flip:
  - **damage packet (Option B), not the minimal fix** — gains `Source`, crit, on-hit; and the
    1-arg `TakeDamage` overload is **deleted**, since defaulting to `Physical` is *why* every
    hit is Physical;
  - **fix melee, then `GameWeapon.Range` is real** — replace the cursor point-query with a
    hitbox (the same primitive as `HazardComponent`) instead of omitting the field;
  - **`PickupComponent.Item` replaces `ItemId`** — no dual path.
- **`MUST NOT HAVE` is part of the contract** — but it is **conditional, not absolute**. A
  sword that cannot break must not have `HealthComponent`; one that can, should. The test is
  always *"does this representation of this thing do that?"*, never *"is it a sword?"*

## Conventions for these docs

- **Cite phase docs by section (`phase-6 §4`), never by line (`phase-6:69`).** The genre docs
  carried 12 line-number citations into `phase-6`; revising that doc silently invalidated every
  one — they pointed at blank lines and unrelated prose. Cross-doc line numbers are a
  same-shape defect to the `.tscn` `[Export]` names Godot drops: **wrong silently, and only
  found by looking.** All 12 are now section refs. Line numbers into *source* are fine — that's
  a different file that changes for different reasons, and `grep` re-finds them.
- **A 0-caller class is not automatically residue.** `BeepPathfindingGrid` has 0 callers and was
  nearly written off as dead; it is a working A* over an occupancy grid and the right base for
  `GridPlacementComponent`. Check what a thing *is* before pricing it. (Contrast `EntitySystem`:
  0 callers **and** 0 subclasses **and** no behaviour — that one is genuinely dead.)

## Verification

Every phase: `dotnet build` (0 errors) and `templates/scenes/validate_scenes.sh` (PASS).
Neither runs the game — see `CLAUDE.md` § *Testing*. Each phase names its own editor check.
</content>
