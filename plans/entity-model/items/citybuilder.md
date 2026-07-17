# citybuilder — no items; one archetype, one spec

> Read `README.md` first. This doc conforms to its rule and its template.

---

## 1. Does this genre need items at all?

**No.** City builder has one archetype — a **Building** — and it needs one class to describe
it. There is nothing to carry, stack, drop, or equip. No `GameItem` branch applies.

This is the shortest genre in the set, and the value of the doc is in saying what is **not** a
thing, because the two obvious mistakes are both expensive.

### What the genre ships today

`templates/scenes/citybuilder_main.tscn` is a placeholder:
`:16` `text = "City Builder gameplay placeholder"`, `PopulationLabel` (`:41`,
`text = "Population: 250"`), `BudgetLabel` (`:44`, `text = "Budget: 50000"`), a `GameFlow`,
and three `GenreScreenComponent` nodes wiring build/economy/districts screens
(`ecs/scenes/citybuilder/BuildMenu.cs`, `Economy.cs`, `Districts.cs`).

Verified absences: `grep -riln "population\|citizen\|footprint\|upkeep\|placement\|TileMap"`
over `addons/beep_game_builder_cs/` returns only `core/BeepWeightedTable.cs`,
`ecs/atmosphere/LightningBoltComponent.cs`, `ecs/DropTableComponent.cs`,
`ecs/LevelLoaderComponent.cs` — all unrelated. **No building, no grid, no citizen, no economy
exists.** The genre is a HUD for a city that does not exist.

`catalogs/skins/citybuilder/genre.json` declares `build_grid_size: 32`, `resource_slots: 6`,
`notification_stack: 5`. None is in `KnownTuningKeys` (`core/BeepGenreGenerator.cs:221-231`),
so all three trip `WarnUnknownTuning` (`:235-243`) on every generate.

### Mistake 1 — a grid cell is DATA, not a Node

`build_grid_size: 32` means a 32×32 board = **1,024 cells**; a modest 100×100 city is **10,000**.
A `Node` per cell is 10,000 `_Ready` calls, 10,000 entries in the scene tree, and 10,000
objects the editor must inspect — for a `bool` and an `int`. Godot nodes cost; a `Vector2I` key
does not.

A grid is `Dictionary<Vector2I, …>` or a `[,]`. It has no transform, no lifetime, no signals of
its own, and nothing to attach a component to. Making it a Node buys nothing and costs
everything.

The framework already demonstrates the right pattern — see below.

### Mistake 2 — a citizen is a SCALAR, not an agent

`citybuilder_main.tscn:41` says it outright: `text = "Population: 250"`. That is an
**aggregate**. It is a number that goes up when housing is built and down when it burns. It is
not 250 entities with `HealthComponent`, `MovementComponent` and pathfinding.

Giving a citizen a spec — `CitizenSpec`, `NeedsComponent`, a `GameItem` they carry — is
inventing a kind to fill a table (README § *The template*, item 1: *"Resist inventing kinds to
fill a table"*). Nothing in the genre reads a citizen; the HUD reads a count.

If a project later wants visible agents wandering the streets, they are **decoration** driven
by the scalar, or a different game. Not now, and **UNCERTAIN** whether ever.

### The pattern to copy: `Match3BoardComponent`

`ecs/ui/Match3BoardComponent.cs` is the proof that this codebase already knows the answer:

- the whole board is `private int[,]? _grid` (`:27`) — **not** a node per cell;
- mutation emits `CellChanged(int x, int y, int gemType)` (`:24`, emitted at `:190-193`);
- the class doc states the contract: *"gem rendering is left to the game (read Grid via
  GetGrid, or connect to CellChanged)"* (`:9-10`).

Grid-as-array + a change signal + rendering left to the game. A `CityGridComponent` is that
shape with `Vector2I → BuildingSpec?` instead of `int`. Copy it; do not re-invent it.
(`Match3BoardComponent`'s own tragedy — 0 callers, 0 subscribers — is `puzzle.md`'s problem,
not this genre's. The *pattern* is right regardless of whether anything uses it.)

---

## 2. The tree

**There is none.** Citybuilder draws **zero** branches from the `GameItem` spine.

### Is a building a `GameItem`?

No. Against the spine's fields (README § *The spine*):

| spine field | on a building |
|---|---|
| `MaxStack` | meaningless — a city has 4 fire stations, not a stack of 4 |
| `IsStatic` | **always true**. A constant is not a field |
| `IsDestructible` | usually true — but that is `HealthComponent`'s job on the instance |
| `Rarity` | no — you build what you can afford, not what drops |
| `Icon`, `WorldScene` | fit |

The README's traits table is load-bearing *because* `IsStatic`/`IsDestructible` vary and
therefore **decide composition**. On a building `IsStatic` never varies. Inheriting `GameItem`
would import a stack size that means nothing and a trait that is a constant — and would assert,
falsely, that a hospital can be picked up.

And a building has one field no `GameItem` can hold: **`Footprint : Vector2I`**. The spine has
no notion of occupying more than one place, because a carried thing occupies none.

**`BuildingSpec` is a SIBLING of `GameItem`** — its own `Resource` root.

### The `.tres` set a developer authors

```
res://data/buildings/
    house.tres           BuildingSpec    1×1
    shop.tres            BuildingSpec    2×2
    factory.tres         BuildingSpec    3×3
    power_plant.tres     BuildingSpec    4×4
    road.tres            BuildingSpec    1×1
```

Five buildings, one class. They differ in **numbers and a scene** — README § *The one rule*.
There is no `HouseSpec`. A `road.tres` that needs *connectivity* logic a house does not is
still one class: connectivity is a component on the road's `Scene`, not a field on the spec —
unless it turns out to need a field, in which case `RoadSpec : BuildingSpec` earns itself
later, on evidence. **UNCERTAIN**; do not pre-empt it.

---

## 3. New framework classes this genre earns

### `BuildingSpec : Resource` — **earned. This is the whole genre.**

`[Tool][GlobalClass]`, per the repo's data pattern (`core/GameInfo.cs:15`,
`ecs/CraftingComponent.cs:53`, `ecs/QuestComponent.cs:71`).

Earned because **`Footprint`, `Upkeep` and `IncomePerTick` are fields no class in the repo has
anywhere.** Not defaults — fields. `GameItem` has no footprint (§2); `WorkComponent`
(`ecs/WorkComponent.cs:11`) has production but no cost, no placement and no recurring economy.

```
BuildingSpec : Resource                 [Tool][GlobalClass]
    Id, DisplayName, Description, Icon
    Footprint      : Vector2I           — cells occupied. The field GameItem cannot hold
    Cost           : Dictionary<string,int>   — one-off, to place
    Upkeep         : float              — per economy tick, recurring
    IncomePerTick  : float
    Scene          : PackedScene?       — the building in the world
```

`Scene` mirrors the spine's `WorldScene` and the repo's `ProjectileScene`-carries-
`ProjectileComponent` shape (MASTER_TODO § *The approach*) — same idea, not an inherited field.

**Deliberately absent:** `PopulationProvided`, `Pollution`, `HappinessDelta`, `Category`,
`UnlockTier`. Every one is a game-design decision, and a field nothing reads is (MASTER_TODO
§ *What the analysis changed*) "this repo's signature defect". A project that needs
`PopulationProvided` adds it when something reads it.

### Shared with strategy — **yes, and deliberately**

`strategy.md` § 3 needs the same class for a barracks and a farm, and adds **no field** to it.
A `CityBuildingSpec` / `StrategyBuildingSpec` split would be two renames of one class — the
README's smell test exactly (*"if it would be an empty class with a nicer name, it is a
`.tres`"*, and here not even that). **One class, in the framework, beside `GameItem`.**

This is also the README's corollary in action: *"a genre never gets a class just for being that
genre."* Two genres wanting the same thing is evidence the thing is framework, not that it is
two things.

### `GridCell` / `CellSpec` — **not earned, and actively wrong**

A cell is a `Vector2I` key into a dictionary. It has no fields of its own beyond "what is here",
which is the dictionary's value. §1, Mistake 1.

### `CitizenSpec` — **not earned**

Population is a scalar (`citybuilder_main.tscn:41`). §1, Mistake 2.

### `ResourceSpec` / `DistrictSpec` / `ZoneSpec` — **not earned**

`Cost : Dictionary<string,int>` covers resources by id. A district is a *set of cells* —
data, derivable from the grid, not a new kind of thing. `Districts.cs` is a screen, not a model.

### Any `GameItem` subclass — **none.**

---

## 4. Components this implies

### Serves already

| Component | Role | Caveat |
|---|---|---|
| `ecs/GameFlowComponent.cs` | already in `citybuilder_main.tscn:47` | |
| `ecs/WorkComponent.cs` | a factory producing goods — `StartWork` → `Tick` → `WorkDone(outputItem, quantity)` (`:28`, `:37`, `:21`) | **`Tick` has 0 callers addon-wide** (verified: `grep -rn "StartWork\|\.Tick(\|WorkComponent"` outside its own file → 0). Correct model, never run |
| `ecs/HealthComponent.cs` | *only if* buildings can burn/collapse. Blind; `Died` = demolished | |
| `ecs/ui/HudComponent.cs` | the Population/Budget labels | binds `GameFlowComponent.ScoreChanged` (`:45`), which is a score, not a budget |
| `ecs/ui/GenreScreenComponent.cs` | already wires build/economy/districts (`citybuilder_main.tscn:50,55,60`) | screens only |

### Forced new — one, and it is the pattern above

**`CityGridComponent`** — `Dictionary<Vector2I, BuildingSpec>` + `Place(Vector2I, BuildingSpec)`
+ a `CellChanged(Vector2I)` signal, footprint-aware occupancy, rendering left to the game.
Directly the `Match3BoardComponent` shape (`ecs/ui/Match3BoardComponent.cs:27`, `:24`, `:9-10`).

An **`EconomyComponent`** (tick `Upkeep`/`IncomePerTick`, hold the budget) is implied by
`BuildingSpec`'s fields having a consumer, but it is behaviour and this doc does not design it.
Named, not specified.

### Explicitly must not have

- **A Node per grid cell** — §1.
- **`MovementComponent`** on a building. The README's traits table derives this: static ⇒ no
  `MovementComponent`, no `PickupComponent`. Every building is static (§2), so the rule is
  unconditional here — the one genre where it is.

### Cites, does not re-derive

`DropTableComponent`'s loot list has no `[Export]`; `Pickup.Collected → Inventory.AddItem` is
0 connections; `Craft()` grants nothing — README § *What is already known*. All irrelevant:
citybuilder has no items. That is the finding.

---

## 5. Content vs framework

**We ship (framework):**

- `BuildingSpec : Resource` — the class only. Shared with strategy.
- `CityGridComponent` — grid-as-dictionary + `CellChanged`, rendering left to the game.
- Either wire `build_grid_size` / `resource_slots` / `notification_stack`, or delete them from
  `catalogs/skins/citybuilder/genre.json`.

**The developer authors (content):**

- Every `.tres`: `house.tres`, `factory.tres`, their costs, their upkeep, their footprints.
- Every building `PackedScene`: art, footprint collider, components.
- The economy's numbers, the unlock order, the map, the win condition.
- How the grid is drawn — `TileMap`, sprites, or nothing.

Per `CLAUDE.md` § *Scope*: framework only. **One archetype, one spec class, one grid component,
no items.** That is the complete answer for this genre.
