# rpg — GameItem tree

> Conforms to `items/README.md`. Read that first; the class-or-`.tres` rule is applied here
> without exception.

## 1. Does this genre need items at all?

**Yes — this is the reference case, and it is the genre the spine was drawn for.**

The genre declares three screens and all three are item- or item-adjacent:
`catalogs/skins/rpg/genre.json:16-20` ships `inventory.tscn`, `character.tscn`, `quests.tscn`.
Every one of them is currently **static markup**: `rpg/inventory.tscn:47-70` is a
`GridContainer` with six hand-placed `PanelContainer` slots and no `InventoryComponent`
anywhere in the scene (its only scripts are `ThemePresetComponent`, `GameInfoBinder` and the
nav-only `Inventory.cs`, `rpg/inventory.tscn:3-5`). The grid's `columns = 6`
(`rpg/inventory.tscn:50`) is a literal duplicate of `tuning.inventory_columns: 6`
(`genre.json:27`) — and that key is **read by nothing**
(`core/BeepGenreGenerator.cs:222-231` lists every consumed key; `WarnUnknownTuning`,
`core/BeepGenreGenerator.cs:236-244`, warns about the rest). Same for `dialogue_speed` and
`tooltip_delay` (`genre.json:28-29`).

So rpg's inventory today is a picture of an inventory. It needs items more than any other
genre, and it needs them as **data**, because it already has the two systems that would
consume them and both are gated on a bare string:

- `DoorSwitchComponent.RequiredItem` (`ecs/DoorSwitchComponent.cs:21`) resolves the player's
  `InventoryComponent` and calls `HasItem` (`ecs/DoorSwitchComponent.cs:60-66`). **This is
  the only working item gate in the addon** — keys are a real, already-supported archetype.
- `QuestObjective.TargetId` (`ecs/QuestComponent.cs:77`) is matched by string in
  `ProgressObjective` (`ecs/QuestComponent.cs:31`). The three shipped objectives —
  *"Defeat the Dark Lord (0/1)"*, *"Collect 5 Rare Artifacts (2/5)"*, *"Explore the Northern
  Ruins"* (`rpg/quests.tscn:52,55,58`) — are exactly `ObjectiveType.Kill` / `Collect` /
  `Reach` (`ecs/QuestComponent.cs:73`) rendered as labels.

## 2. The tree

**Spine branches used: all of them.** rpg is the genre that exercises every branch, which is
the point — it is the shape the spine was tested against.

```
GameItem          keys, quest items, materials, currency
GameEquipment  ├── GameWeapon    sword, axe, bow, staff
               ├── GameShield    buckler, tower shield
               └── GameArmor     helm, mail, boots
GameLiquid        potions (drinkable), lamp oil (not)
GameConsumable    scrolls, food, bandage
```

The `.tres` set a developer would author (`res://items/`, one folder per branch):

| File | Class | Why it is only a `.tres` |
|---|---|---|
| `sword_iron.tres`, `sword_steel.tres`, `axe_battle.tres`, `dagger.tres` | `GameWeapon` | Same fields, different numbers. README §rule. |
| `bow_short.tres` | `GameWeapon` | `IsRanged = true`, `ProjectileScene` set — a field value, not a class |
| `staff_fire.tres` | `GameWeapon` | `DamageType = Fire` — a value of the existing enum (`ecs/DamageTypeComponent.cs:15`) |
| `shield_buckler.tres`, `shield_tower.tres` | `GameShield` | differ only in `Defense`/`BlockChance` |
| `helm_leather.tres`, `mail_chain.tres`, `boots_travel.tres` | `GameArmor` | differ in `Slot` + `Defense` |
| `potion_health.tres`, `potion_mana.tres` | `GameLiquid` | `IsDrinkable = true`, differ in `HealAmount`/`StatusEffectId` |
| `oil_lamp.tres` | `GameLiquid` | `IsDrinkable = false` — the case the spine cites for the branch existing |
| `scroll_fireball.tres`, `bandage.tres` | `GameConsumable` | `StatusEffectId` + `Duration`, differing values |
| `key_brass.tres`, `key_dungeon.tres` | `GameItem` | A key has **no field a `GameItem` lacks**. Its whole behaviour is `Id` matching `DoorSwitchComponent.RequiredItem` (`ecs/DoorSwitchComponent.cs:21,62`). |
| `quest_amulet.tres`, `artifact_rare.tres` | `GameItem` | Likewise: `Id` matched against `QuestObjective.TargetId` (`ecs/QuestComponent.cs:77`) |
| `ore_iron.tres`, `hide_wolf.tres`, `herb.tres` | `GameItem` | Crafting inputs — `CraftingIngredient.ItemId` (`ecs/CraftingComponent.cs:67`) is the only thing that reads them |
| `gold.tres` | `GameItem` | Currency is `Id = "gold"`, `MaxStack = 9999`. There is **no currency component** in the addon (grep `Currency|Gold` over `addons/` → 0 hits); the pause screen's `🪙 45` is a static label (`topdown/pause_subscreen.tscn:152-153`). Currency is a stack, not a kind. |
| `anvil.tres`, `chest_oak.tres` | `GameItem` | `IsStatic = true` + `WorldScene`. The traits carry it; no class needed (README §traits). |

Traits, per README §traits — the developer sets these, and they derive the composition:

| `.tres` | `IsStatic` | `IsDestructible` |
|---|---|---|
| `sword_iron.tres` | false | true (→ `MaxDurability`) |
| `potion_health.tres` | false | false |
| `key_brass.tres` | false | false |
| `anvil.tres` | true | false |
| `chest_oak.tres` | true | true |

## 3. New framework classes this genre earns

**None.**

Every rpg archetype resolves to an existing spine branch plus values. The tests that were run
and failed to produce a class:

- **`RpgSword` / `Sword` / `Bow`** — README §corollary states it outright; the body would be
  default values.
- **`GameKey : GameItem`** — proposed field: *what door does it open?* But the direction is
  reversed in the working code: the **door** names the key
  (`DoorSwitchComponent.RequiredItem`, `ecs/DoorSwitchComponent.cs:21`), not the key the door.
  A key therefore adds no field. Body would be empty → `.tres`.
- **`GameQuestItem : GameItem`** — proposed field: *which quest?* Same reversal:
  `QuestObjective.TargetId` (`ecs/QuestComponent.cs:77`) names the item. Empty body → `.tres`.
  (A `bool IsQuestItem` to block dropping is tempting; nothing in `InventoryComponent` can
  drop an item — there is no drop path at all — so the field's behaviour would never happen.
  That is the repo's signature defect, per `MASTER_TODO.md` and `CLAUDE.md` § *Scope*.)
- **`GameCurrency : GameItem`** — proposed field: *stack semantics.* `MaxStack` already
  exists on `GameItem`. Empty body → `.tres`.
- **`GameScroll : GameConsumable`** — a scroll is `StatusEffectId` + `Duration`, which
  `GameConsumable` has. → `.tres`.

**This is the result worth reporting:** the reference genre — the hardest item case in the
catalog — needs **zero** new item classes. The spine as drawn in `phase-1-item-resources.md`
covers rpg completely. rpg's real gaps are all **components**, below.

## 4. Components this implies

**Already serve it (no work):**

| Component | Serving what |
|---|---|
| `InventoryComponent` (`ecs/InventoryComponent.cs:24`) | The bag. `HasItem`/`CountItem`/`AddItem`/`Sort` all work today; only the item *model* is wrong (`InventoryItem` nested class + `Stats` bag, `:30-42`) — Phase 1 replaces it. |
| `DoorSwitchComponent` (`ecs/DoorSwitchComponent.cs:14`) | Key gating — **the one item edge that works end to end.** |
| `QuestComponent` + `QuestObjective` (`ecs/QuestComponent.cs:13,71`) | The three shipped objective shapes. Note `ProgressObjective` has **no callers** (README §known / `phase-6:70`) — the model is right, the trigger is missing. |
| `CraftingComponent` + `CraftingRecipe`/`CraftingIngredient` (`ecs/CraftingComponent.cs:13,53,65`) | Smithing. `Craft()` grants nothing (README §known). |
| `HealthComponent.Armor` (`ecs/HealthComponent.cs:16`), `ResistanceComponent` (`ecs/ResistanceComponent.cs:15-22`) | The receiving half of `GameArmor.Defense`/`Resistances` — idle, ready, per `MASTER_TODO.md`. |
| `LevelingComponent` (`ecs/LevelingComponent.cs:13`) | XP/level. `StatPointsPerLevel` (`:19`) accrues points with nowhere to spend them. |
| `StatusEffectComponent.GetModifier` (`ecs/StatusEffectComponent.cs:125`) | The query shape `EquipmentComponent` copies (`phase-2-equipment.md:17-24`). |
| `HealthComponent` | Durability on a destructible item's `WorldScene`/`WieldScene` — it is blind, no parent cast (`phase-1-item-resources.md:19-21`). |

**New, forced by this tree — all already scheduled; cite, don't re-propose:**

| Component | Why the rpg tree forces it | Where |
|---|---|---|
| `EquipmentComponent` | Nothing can hold a `GameWeapon`. `GameStateData.EquippedWeapons` is a `List<string>` (`core/GameStateData.cs:258`) that no component writes. | `phase-2-equipment.md` |
| `CharacterStatsComponent` | `character.tscn:53-78` displays Strength 15 / Dexterity 12 / Constitution 10 / Intelligence 14 with **no backing component**. `PlayerStatsComponent` is a soccer block — Shooting/Passing/Dribbling/ShirtNumber (`ecs/PlayerStatsComponent.cs:14-33`), a false friend per README §known. Also gives `LevelingComponent.StatPoints` a destination. | `phase-6:81` |
| `ContainerComponent` | `chest_oak.tres` is in the tree above and nothing supports a second inventory or a transfer. | `phase-6:83` |
| `DropTableEntry : Resource` | `DropTableComponent._entries` has no `[Export]` (README §known) — an rpg with authored loot is impossible without C#. | `phase-6:69` |

**The tree does not force these, and rpg must not invent them:** a shop component, a
durability-decay component, a stat-requirement gate on equipment. Nothing in the genre's
shipped scenes asks for them, and a field no component reads is the defect this whole
initiative exists to stop.

## 5. Content vs framework

**We ship** (`CLAUDE.md` § *Scope* — "Components, wiring, scaffolding"):
- The `GameItem` hierarchy as `[Tool][GlobalClass] Resource` — Phase 1. No rpg-specific class.
- `EquipmentComponent`, `CharacterStatsComponent`, `ContainerComponent`, `DropTableEntry`.
- The edges: `Pickup.Collected → Inventory.AddItem`, `Craft → AddItem`, `Died → DropTable.Roll`
  (Phase 4). These are ours because they are *wiring*, not content.
- `inventory.tscn` / `character.tscn` / `quests.tscn` rewired to real components instead of
  hand-placed `PanelContainer`s — a template is "correct structure, wired components"
  (`CLAUDE.md` § *Scope*), and six literal slots against an inert `inventory_columns` key is
  neither.

**The developer authors:**
- Every `.tres` in §2. All of them. We ship **no** `sword_iron.tres` — that is balance and
  content ("what an upgrade grants", `CLAUDE.md` § *Scope*).
- The `WorldScene`/`WieldScene` for each item, and their art.
- Which quest asks for which item id; which door wants which key.
- When to call `ProgressObjective` / `Craft` / `Equip`. We provide the verbs and demonstrate
  one path (`CLAUDE.md` § *Scope*).

**The boundary:** an rpg item is a **`.tres` the developer makes** out of a class **we ship**.
If a developer must write C# to add a sword, that is our bug. If they must fill in its damage,
that is their game.
