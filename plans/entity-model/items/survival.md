# survival — GameItem tree

> Conforms to `items/README.md`. Read that first; the class-or-`.tres` rule is applied here
> without exception.

## 1. Does this genre need items at all?

**Yes — and it is the only genre whose core loop *is* the item loop.** rpg can be played with
items as flavour; survival cannot. Chop → material → craft → tool → chop harder is the genre.

The genre says so itself: `catalogs/skins/survival/genre.json:16-20` ships `crafting.tscn`,
`backpack.tscn`, `world_map.tscn`, and its description is *"Crafting, inventory, status meters
and world map"* (`:5`). `crafting.tscn:55,59,63` names three recipes as buttons — **Wood Axe**,
**Stone Spear**, **Fire Starter** — which is a tool, a weapon, and a utility, hardcoded as
`Button.text` against no `CraftingComponent` (the scene's only scripts are
`ThemePresetComponent`, `GameInfoBinder`, and the nav-only `Crafting.cs`, `crafting.tscn:3-5`).
`backpack.tscn:47-74` is eight literal `PanelContainer` slots with `columns = 8` (`:50`) — a
literal duplicate of `tuning.quick_slots: 8` (`genre.json:27`), a key **nothing reads**
(`core/BeepGenreGenerator.cs:222-231`; `WarnUnknownTuning`, `:236-244`). Same for
`status_decay_seconds` and `warning_threshold` (`genre.json:28-29`).

The consuming half is unusually healthy here, which is what makes the item gap loud:

- `HungerStaminaComponent` (`ecs/HungerStaminaComponent.cs:14`) is complete and
  well-composed — `ConsumeFood(float hungerRestore)` (`:180`), `DrinkWater(float thirstRestore)`
  (`:186`), `Rest(float)` (`:192`), and it already cross-reads temperature
  (`:37-40`). **Both of its restore methods take a number no item can supply.**
- `TemperatureComponent` (`ecs/TemperatureComponent.cs:20`) is live in the shipped main scene
  (`survival_main.tscn:6,23`) and backed by `tuning.enable_temperature: true` /
  `ambient_temperature: 15` — both of which **are** consumed (`core/BeepGenreGenerator.cs:208-209`).
- `CraftingComponent` + `CraftingRecipe` + `CraftingIngredient` (`ecs/CraftingComponent.cs:13,53,65`)
  is the pattern this whole initiative copies — `[Tool][GlobalClass] Resource`, inspector
  drag-and-drop, no strings-as-config in the exports **except one**:
  `CraftingIngredient.ItemId` (`:67`) and `CraftingRecipe.OutputItem` (`:57`) are bare
  `string`s that must match a template registered in **C#** at runtime
  (`InventoryComponent.RegisterItem(...)`, `ecs/InventoryComponent.cs:108`). The only
  cross-reference in the healthiest system in the addon is an **unvalidated string**. That is
  precisely what `[Export] GameItem` replaces.

(And `Craft()` deducts materials and grants nothing — README §known, cite only.)

## 2. The tree

**Spine branches used:** `GameItem`, `GameEquipment` → `GameWeapon` (+ the tool class earned
below), `GameArmor`; `GameLiquid`; `GameConsumable`. **`GameShield` is unused** — no survival
scene, tuning key, or component mentions blocking; the branch stays available, this genre just
doesn't author one.

```
GameItem          materials, campfire, workbench
GameEquipment  ├── GameWeapon    spear, bow
               │      └── GameTool   axe, pickaxe   ← earned, §3
               └── GameArmor    coat, boots
GameLiquid        water, fuel
GameConsumable    bandage
    └── GameFood      berries, cooked meat          ← earned, §3
```

The `.tres` set a developer would author:

| File | Class | Note |
|---|---|---|
| `axe_stone.tres`, `axe_iron.tres`, `pickaxe_stone.tres` | `GameTool` | Differ in `HarvestPower` + `ToolClass` + `Damage` — values, not kinds. Matches `crafting.tscn:55` "Wood Axe". |
| `spear_stone.tres` | `GameWeapon` | `crafting.tscn:59`. A spear harvests nothing → plain `GameWeapon`, not `GameTool`. |
| `bow_hunting.tres` | `GameWeapon` | `IsRanged = true` |
| `fire_starter.tres` | `GameItem` | `crafting.tscn:63`. Its whole job is being a `CraftingIngredient.ItemId` for a campfire. No field. |
| `coat_fur.tres`, `boots_hide.tres` | `GameArmor` | Cold protection would be a `Resistances` entry — `TemperatureComponent` does **not** read resistances (`ecs/TemperatureComponent.cs` has no `ResistanceComponent` reference), so **do not** invent a `WarmthRating` field. See §3. |
| `water_canteen.tres` | `GameLiquid` | `IsDrinkable = true`, `Volume` — the branch's reason for existing |
| `fuel_oil.tres` | `GameLiquid` | `IsDrinkable = false` |
| `berries.tres`, `meat_cooked.tres`, `meat_raw.tres` | `GameFood` | Differ in `HungerRestore`; `meat_raw` adds a `StatusEffectId` (inherited) |
| `bandage.tres` | `GameConsumable` | `HealAmount` — no hunger involvement |
| `wood.tres`, `stone.tres`, `fiber.tres`, `hide.tres`, `ore_iron.tres` | `GameItem` | Materials. `MaxStack` high. The `CraftingIngredient.ItemId` targets. |
| `campfire.tres`, `workbench.tres` | `GameItem` | `IsStatic = true` + `WorldScene`. The traits carry them (README §traits); a `GameStation` class would have an empty body. |

Traits:

| `.tres` | `IsStatic` | `IsDestructible` |
|---|---|---|
| `axe_stone.tres` | false | **true** — `MaxDurability`; the wielded instance carries `HealthComponent`, `Died` = it breaks |
| `berries.tres`, `wood.tres` | false | false |
| `campfire.tres` | true | true |
| `workbench.tres` | true | false |

## 3. New framework classes this genre earns

**Two.** Both were argued against first.

### `GameTool : GameWeapon` — **earned**

```
GameTool : GameWeapon        [Tool][GlobalClass]
    ToolClass    : ToolClass  (Axe, Pickaxe, Shovel, Knife, Fishing)
    HarvestPower : float
```

Passes the rule: **two fields `GameWeapon` cannot express.**

- `HarvestPower` is not `Damage`. `phase-6 §5` specifies `HarvestableComponent` as *"requires
  tool class X, yields item Y ×N"* — a tier gate, unrelated to what the axe does to a wolf.
  Collapsing them means an axe balanced against trees is also balanced against enemies.
- `ToolClass` cannot be `DamageType`. The enum is Physical/Fire/Ice/Poison/Holy/Dark/
  Lightning/True (`ecs/DamageTypeComponent.cs:15`); "Axe" is not a damage type, and abusing
  the enum would put a parallel meaning on a field `ResistanceComponent` already reads
  (`ecs/ResistanceComponent.cs:27`).

**Why `: GameWeapon` and not `: GameEquipment`** (the alternative the brief asked to decide):
the genre's own shipped tools are swung objects — Wood Axe and Stone Spear sit side by side in
one recipe grid (`crafting.tscn:55,59`), and a survival axe that cannot be used on an animal is
a second item. Under `: GameEquipment` a fighting axe needs either duplicate `Damage` fields or
two `.tres` for one axe. Under `: GameWeapon` the cost is one inherited field a non-combat tool
leaves at zero (a fishing rod's `Damage = 0`) — which is the same cost the spine already
accepts for `MaxDurability` on an indestructible item (`phase-1-item-resources.md:104`).
The smaller wrong wins. **UNCERTAIN** — this is a judgement call, not a derivation; if a later
genre wants a purely non-combat tool line, re-parenting to `GameEquipment` and adding `Damage`
to `GameTool` is the escape hatch.

### `GameFood : GameConsumable` — **earned**

```
GameFood : GameConsumable    [Tool][GlobalClass]
    HungerRestore : float
```

Passes the rule: `HungerStaminaComponent.ConsumeFood(float hungerRestore)`
(`ecs/HungerStaminaComponent.cs:180`) takes a number that exists **nowhere** in the spine.
`GameConsumable` has `HealAmount`, `StatusEffectId`, `Duration`
(`phase-1-item-resources.md:125-126`) — hunger is a separate meter with a separate depletion
rate (`ecs/HungerStaminaComponent.cs:17,22`) and separate criticals (`:33,150`). Food is not a
potion with a different number; it feeds a different bar. Behaviour that could not otherwise
happen.

### The one spine amendment survival asks for, instead of a class

`DrinkWater(float thirstRestore)` (`ecs/HungerStaminaComponent.cs:186`) has the same problem as
`ConsumeFood`, but **`GameWater : GameLiquid` does not earn a class** — add
**`ThirstRestore : float` to `GameLiquid`**. The reason it is a field and not a subclass, while
`GameFood` is a subclass: `GameLiquid` already carries `IsDrinkable`
(`phase-1-item-resources.md:122`) — the spine put the drink discriminator *on the base*, so the
drink payload belongs beside it; a `GameWater` whose body was one field beside an existing flag
that already discriminates it is the README's "empty class with a nicer name". `GameConsumable`
has no eat/drink flag, so `GameFood` carries its own.

### Rejected

- **`GameMaterial : GameItem`** — wood, stone, fiber. Proposed field: *none found.* Their
  entire existence is `Id` + `MaxStack`, matched by `CraftingIngredient.ItemId`
  (`ecs/CraftingComponent.cs:67`). Empty body → `.tres`.
- **`GameStation : GameItem`** — campfire, workbench. `IsStatic = true` + `WorldScene` says
  everything (README §traits). Empty body → `.tres`.
- **`GameArmor.WarmthRating`** — tempting, and **wrong today**: `TemperatureComponent`
  computes from `AmbientTemp` and `TemperatureRecoveryRate` (`ecs/TemperatureComponent.cs:35,55`)
  and consults no resistance or equipment. A field nothing reads is the repo's signature defect
  (`phase-1-item-resources.md:57-59`). Warmth waits for a component that asks for it.

## 4. Components this implies

**Already serve it:**

| Component | Serving what |
|---|---|
| `CraftingComponent` / `CraftingRecipe` / `CraftingIngredient` (`ecs/CraftingComponent.cs:13,53,65`) | The whole crafting screen — **the pattern to follow**, needing only `[Export] GameItem` in place of `ItemId`/`OutputItem` strings (`:67,57`) |
| `HungerStaminaComponent` (`ecs/HungerStaminaComponent.cs:14`) | `GameFood.HungerRestore` → `ConsumeFood` (`:180`); `GameLiquid.ThirstRestore` → `DrinkWater` (`:186`) |
| `TemperatureComponent` (`ecs/TemperatureComponent.cs:20`) | Live in `survival_main.tscn:23`; consumes `ambient_temperature: 15` |
| `InventoryComponent` (`ecs/InventoryComponent.cs:24`) | The backpack. `columns` and `MaxSlots` should come from `quick_slots`, not a literal. |
| `HealthComponent` (`ecs/HealthComponent.cs:12`) | Tool durability on the `WieldScene` — blind, no parent cast (`phase-1-item-resources.md:19-21`); `Died` = the axe breaks |
| `DropTableComponent` (`ecs/DropTableComponent.cs:14`) | Harvest yield. Already season/weather-aware (`ecs/DropTableComponent.cs:36-38,45-48`) — the right home for "berries in Summer, none in Winter". |
| `CropGrowthComponent` (`ecs/CropGrowthComponent.cs:13`) | Farming. `Harvest()` already calls `_dropTable?.Roll()` (`:118`) — **the one place in the addon where a roll is actually triggered.** |
| `WorkComponent` (`ecs/WorkComponent.cs:11`) | Furnace/smelter. Its `OutputItemId` (`:15`) is the same bare string as `CraftingRecipe.OutputItem` and should become a `GameItem`. |

**New, forced by this tree:**

| Component | Why | Where |
|---|---|---|
| `HarvestableComponent` → **extend `CropGrowthComponent`, don't add a parallel type** | `GameTool.ToolClass`/`HarvestPower` have no reader without it. **Correction:** `CropGrowthComponent` is already ~70-80% of it — stages (`:15`), `Harvest()` (`:114`), `_dropTable?.Roll()` (`:118`). Missing only a tool-class gate and a non-crop (rock/tree) mode; drop its `_seasonal`-mandatory guard (`:73`) so non-crop harvestables work. **Blocked on `DropTableComponent`'s `[Export]` regardless** — `Roll()` can never yield today. | `phase-6 §5` — cite, do not re-derive |
| `EquipmentComponent` | Nothing can hold a `GameTool`. | `phase-2-equipment.md` |
| `ContainerComponent` → **~15 lines on `InventoryComponent`** | Storage box. **Correction: two inventories are already supported** (nothing is static; `Resize` `:297` handles sizes). Only cross-inventory **transfer** is missing — add `TransferTo(...)` over the existing `RemoveAt` + `AddItem`. Blocker: `ParticipatesInSave` (`:58`) is player-only, so a storage box **cannot persist** without keyed multi-inventory save. | `phase-6 §5` |
| `DropTableEntry : Resource` | `_entries` has no `[Export]` (README §known) — authored yields are impossible. `CropGrowthComponent.Harvest` already rolls the table, so this is the **shortest path to a working survival loop in the addon.** | `phase-6 §4` |

## 5. Content vs framework

**We ship:**
- `GameTool`, `GameFood`, and `GameLiquid.ThirstRestore` — plus the spine. Framework, because
  each is a **field a shipped component's method already demands** and cannot get.
- `HarvestableComponent`, `EquipmentComponent`, `ContainerComponent`, `DropTableEntry`.
- `CraftingIngredient.ItemId` / `CraftingRecipe.OutputItem` / `WorkComponent.OutputItemId`
  migrated from `string` to `GameItem` — replacing the unvalidated cross-reference. Ours: it is
  API surface, `CLAUDE.md` § *Scope*.
- `Craft() → AddItem` (Phase 4), and `crafting.tscn`/`backpack.tscn` rewired to real components
  instead of three `Button.text` literals and eight `PanelContainer`s.

**The developer authors:**
- Every `.tres` in §2, including the axe's `HarvestPower` and the berries' `HungerRestore`.
  Those are balance.
- Every `CraftingRecipe.tres` — *what* three woods and two fibers make. `crafting.tscn`'s
  "Wood Axe / Stone Spear / Fire Starter" are **placeholders to be replaced**, per
  `CLAUDE.md` § *Scope* ("genre mains and levels are scaffolding").
- `WorldScene`/`WieldScene` and all art. `levels/survival/level_1.tscn` ships a
  `PlayerSpawn` marker and a "Survival gameplay placeholder" label — an empty level is the
  developer's canvas, by design (`CLAUDE.md` § *Scope*).
- When to call `Craft`, `Harvest`, `ConsumeFood`.

**The boundary:** we ship the class that makes "this axe is tier 2 against wood" **expressible
and type-checked**. Which tier, and how many logs it yields, is the game.
