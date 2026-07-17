# strategy — no item tree; `UnitSpec` + `BuildingSpec`, and a maybe

> Read `README.md` first. This doc conforms to its rule and its template.

---

## 1. Does this genre need items at all?

**Not as its spine.** Strategy's atoms are a **unit** and a **building**, and neither is a
carried, stacked, dropped thing. What it needs is **specs**: a `.tres` per unit type, a `.tres`
per building type, and a tech graph. Items may return at the edges (§2) — but they are not the
model.

### What the genre ships today

`templates/scenes/strategy_main.tscn` is a placeholder in the literal sense:
`:16` `text = "Strategy gameplay placeholder"`, plus `ResourcesLabel` (`:41`,
`text = "Resources: 100"`), `UnitsLabel` (`:44`, `text = "Units: 10"`), a `GameFlow`, and three
`GenreScreenComponent` nodes for the research/diplomacy/unit-panel screens. No unit. No
building. No map.

### The three components you would expect, and where they are not

- **Selection / orders / formation: nothing.** `grep -ril "selection\|formation\|squad\|rally"`
  over `addons/beep_game_builder_cs/` returns only unrelated hits — `core/BeepWeightedTable.cs`,
  `core/GameInfo.cs`, `ecs/atmosphere/WeatherSystemComponent.cs`, `ecs/GameApp.cs`,
  `ecs/LevelLoaderComponent.cs`, `ecs/PlayerStatsComponent.cs`, `ecs/scenes/puzzle/PreLevel.cs`,
  `ecs/ui/IThemePreset.cs`, `ecs/ui/ShapeOverrides.cs`, `ecs/ui/SkinCatalog.cs`. There is no
  selection component, no order queue, no formation.
- **`MarqueeComponent` is a TEXT TICKER, not a drag-select box.** `ecs/ui/MarqueeComponent.cs:11`
  — *"Scrolling text marquee/ticker. Attach to any Label."* Its exports are `Speed`,
  `PauseAtStart`, `PauseAtEnd`, `Bounce`, `AutoStart`, and it resolves `GetParent() as Label`.
  It is a news crawl. The name is a false friend of the same order as the three the README
  already lists (`PlayerStatsComponent` is a soccer stat block; `NavigationComponent` is a
  scene-transition wirer, not pathfinding).
- **Pathfinding exists and has no callers.** `core/BeepEncryptionPathfinding.cs:68`
  `class BeepPathfindingGrid` is a real A\* over a `bool[,]` walkable grid with
  `SetObstacle` and `FindPath(Vector2I, Vector2I)`. `grep` for `BeepPathfindingGrid` outside its
  own file → **0 hits**. It is not a `Node`, not `[GlobalClass]`, and lives in an
  encryption-and-pathfinding grab-bag file. Not usable as-is; worth knowing it is there.

### `WorkComponent` is the genre's one real asset — with a caveat

`ecs/WorkComponent.cs:11` — *"Blind — works for furnaces, factories, workbenches, labs,
kitchens."* It is a genuinely correct production model:

- `AvailableWork`, `WorkSpeed`, `OutputItemId`, `OutputQuantity`, `LoopProduction`,
  `TotalWorkRequired` (`:13-18`)
- `StartWork(float)` (`:28`), `Tick(double delta)` (`:37`), `Progress` (`:25`)
- `WorkStarted` / `WorkAccomplished(amount, progress)` / `WorkDone(outputItem, quantity)` /
  `WorkStopped` (`:20-23`)

A barracks producing a unit and a factory producing a resource are both `StartWork` →
`Tick` → `WorkDone`. That is the right shape, and it already exists.

**The caveat, verified and not to be glossed:** `grep -rn "StartWork\|\.Tick(\|WorkDone\|WorkComponent"`
across `addons/` excluding its own file → **0 hits**. `WorkComponent` has no `_Process`; `Tick`
must be called from outside, and nobody calls it. It is a correct model that has never run.
It is an asset because it does not need designing — not because it is wired.

### The tuning block is decoration

`catalogs/skins/strategy/genre.json:27-29` declares `resource_slots: 5`, `unit_card_width: 220`,
`map_overlay_opacity: 0.78`. None is in `KnownTuningKeys`
(`core/BeepGenreGenerator.cs:221-231`), so all three trip `WarnUnknownTuning` (`:235-243`).
Every non-weather, non-save key this genre declares is inert.

---

## 2. The tree

**Mostly none.** Strategy's spine is `UnitSpec` / `BuildingSpec` / `TechNode`, all siblings of
`GameItem`, not subclasses.

### Are a unit and a building `GameItem`s?

No. Same test as racing, against the spine's fields (`README` § *The spine*):

| spine field | on a unit | on a building |
|---|---|---|
| `MaxStack` | no — 10 riflemen are 10 entities, not a stack of 10 | no |
| `IsStatic` | it *moves*, that is its defining trait | trivially true — a constant, not a variable |
| `IsDestructible` | yes, but that is `HealthComponent`'s job on the instance | yes, same |
| `Rarity` | no — you build what you can afford | no |
| `WorldScene` | fits | fits |

A unit is not a definition-of-a-carryable; it is a definition-of-an-**entity**. `MaxStack` and
`IsStatic` are exactly the fields the README calls load-bearing, and on a unit they are
meaningless and constant respectively. Inheriting `GameItem` would inherit the two fields that
make the traits table work, in the two cases where they decide nothing.

**Siblings, not subclasses.** Own `Resource` roots.

### Do units carrying equipment pull `GameItem` back in?

**Argue it, because the honest answer is "yes, at one edge, and only optionally."**

Some RTS do equip units — an upgrade that swaps a rifle for a laser is, structurally, a
`GameEquipment` in a slot. The spine already models that: `GameEquipment : GameItem` has
`Slot`, `WieldScene`; `GameWeapon` has `Damage`, `DamageType`, `IsRanged`, `ProjectileScene`.
If a project wants it, it composes:

```
UnitSpec.DefaultEquipment : GameEquipment[]      ← an optional [Export]
```

This is a **reference**, not an inheritance. `UnitSpec` is not a `GameItem` because it
*points at* equipment, any more than a chest is a sword because it contains one. The spine's
existing branch serves it without strategy earning a single new item class.

And in this repo it would not yet work anyway: per README § *What is already known*,
`DamageTypeComponent` is dead (0 resolvers, every hit is `Physical`), `AttackComponent.Range`
is never read, and nothing joins `"players"` / `"enemies"` — so `AIController` is inert in
every genre. Unit equipment is a **Phase 2/3 dependency**, not a strategy design decision.
Leave the `[Export]` out until Phase 2 lands.

Most RTS, incidentally, model upgrades as a **tech**, not an item — which is what `TechNode`
is for, and which needs no `GameItem` at all.

### The `.tres` set a developer authors

```
res://data/units/          worker.tres, soldier.tres, archer.tres, siege.tres        UnitSpec
res://data/buildings/      hq.tres, barracks.tres, farm.tres, tower.tres             BuildingSpec
res://data/tech/           bronze_working.tres, masonry.tres, ballistics.tres        TechNode
```

Four units, one class. They differ in **numbers** — README § *The one rule*. There is no
`ArcherSpec`. A siege engine that is genuinely a *different kind of thing* (fields no unit has)
would be a subclass; a siege engine that is a slow unit with big damage is a `.tres`.

---

## 3. New framework classes this genre earns

All `[Tool][GlobalClass] : Resource` — the repo's established data pattern
(`core/GameInfo.cs:15`, `ecs/CraftingComponent.cs:53`, `ecs/QuestComponent.cs:71`).

### `UnitSpec : Resource` — **earned**

Nothing in the repo describes a unit type. `HealthComponent`/`AttackComponent`/`MovementComponent`
bake numbers into an **instance** — the exact defect MASTER_TODO § *The problem* names for
`AttackComponent` (*"bakes the numbers into the wielder"*, `ecs/AttackComponent.cs:13-18`).
A spec is the definition those instances are built from, and it holds fields none of them can:
cost, build time, population supply.

```
UnitSpec : Resource
    Id, DisplayName, Icon
    Cost         : Godot.Collections.Dictionary<string,int>   — resource id → amount
    BuildTime    : float          — feeds WorkComponent.StartWork
    Supply       : int            — population cost
    Scene        : PackedScene?   — the unit, carrying Health/Attack/Movement
```

`Cost`/`BuildTime`/`Supply` are fields, not defaults. Earned.

### `BuildingSpec : Resource` — **earned, and shared with citybuilder**

See `citybuilder.md` § 3 for the full argument and field list — this is deliberately **one
class in one place**, not two. Strategy adds no field to it: a barracks is a building with a
footprint, a cost, an upkeep, and a `WorkComponent` in its scene. A `StrategyBuildingSpec`
would be a rename. **Not earned as a separate class.**

**UNCERTAIN — where it lives.** Shared between two genres means it is not genre content; it
belongs beside `GameItem` in the framework. Which folder is a Phase-1 filing question.

### `TechNode : Resource` — **earned**

The `QuestObjective` pattern, one level over. `ecs/QuestComponent.cs:71-83` is the precedent:
a small `Resource` with `Description`, `Type`, `TargetId`, `RequiredCount`, `CurrentCount`, and
a derived `IsComplete`. A tech is the same shape plus the one thing a quest does not have — a
**prerequisite edge**:

```
TechNode : Resource
    Id, DisplayName, Description, Icon
    Cost         : Dictionary<string,int>
    ResearchTime : float
    Requires     : TechNode[]     — the field QuestObjective cannot express
    Unlocks      : Resource[]     — UnitSpec / BuildingSpec / TechNode
```

`Requires` makes it a graph. No existing class holds an edge to itself. Earned.

### `ResourceSpec : Resource` — **not earned**

`Cost : Dictionary<string,int>` keyed by a string id covers it. A gold/wood/stone type carrying
an icon and a display name is a **UI concern**, and inventing a class so the HUD can find an
icon is inventing a kind to fill a table. **UNCERTAIN** if a project wants resource icons in
the framework; revisit then, not now.

### `SelectableSpec`, `FormationSpec`, `OrderSpec` — **not earned**

Selection, orders and formations are **behaviour** (components), not data. §4.

### An item class of any kind — **none.**

Strategy earns **zero** new `GameItem` subclasses. If it equips units it uses `GameEquipment`
/ `GameWeapon` exactly as the spine already defines them (§2).

---

## 4. Components this implies

### Serves already

| Component | Role | Caveat |
|---|---|---|
| `ecs/WorkComponent.cs` | production: barracks, factory, lab. `StartWork` → `Tick` → `WorkDone(outputItem, quantity)` | **`Tick` has 0 callers addon-wide** (§1). The model is right; the wiring is absent |
| `ecs/HealthComponent.cs` | unit and building HP | `Armor` (`:16`) is idle — MASTER_TODO § *The problem* |
| `ecs/AttackComponent.cs` | unit attacks | `Range` never read (README § *known*) — an RTS unit with no reach is a real blocker, not a nit |
| `ecs/GameFlowComponent.cs` | already in `strategy_main.tscn:47` | |
| `ecs/QuestComponent.cs` | campaign objectives — `QuestObjective.Type` already has `Kill`, `Collect`, `Reach`, `Survive` (`:73`) | |
| `ecs/AIController.cs` | enemy units | **inert in every genre** — nothing joins `"players"`/`"enemies"` (README § *known*; MASTER_TODO ranks this highest-leverage overall, Phase 6 §1) |

### Forced new — by the tree, not by taste

- **`ProductionComponent`** (or simply a caller for `WorkComponent`) — turns `UnitSpec.BuildTime`
  into `StartWork`, and `WorkDone` into an instanced `UnitSpec.Scene`. `WorkComponent` is the
  engine; nothing turns the key.
- **`SelectionComponent`** — box/click select over a group. Nothing like it exists (§1), and
  `MarqueeComponent` is not it.
- **`TechTreeComponent`** — holds researched `TechNode`s, gates `Requires`, drives
  `ecs/scenes/strategy/Research.cs`.

**Out of scope for this doc:** all three are behaviour, and the item model does not decide
them. Named so the tree's implications are visible; not designed here.

### Not this genre's problem

`ecs/TrainingComponent.cs` and `ecs/ContractComponent.cs` are **football-manager residue**
(README § *known*, "three false friends"). `TrainingComponent` sounds like unit training. It is
not. Do not reach for it.

---

## 5. Content vs framework

**We ship (framework):**

- `UnitSpec`, `TechNode` — classes only.
- `BuildingSpec` — shared with citybuilder, defined once (`citybuilder.md` § 3).
- A caller for `WorkComponent.Tick`, so the one good model in the genre runs.
- Either wire `resource_slots` / `unit_card_width` / `map_overlay_opacity`, or delete them from
  `catalogs/skins/strategy/genre.json:27-29`.

**The developer authors (content):**

- Every `.tres`: which units exist, what they cost, what they beat.
- Every `PackedScene`: the unit's art, collider, and component composition.
- The tech graph's shape and the faction roster.
- Maps, missions, AI difficulty.

Per `CLAUDE.md` § *Scope*: framework only — no balance, no roster, no maps. We ship the classes
that can describe a unit, a building and a tech; we do not ship a faction.
