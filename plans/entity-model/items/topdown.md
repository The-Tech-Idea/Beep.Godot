# topdown — GameItem tree

> Conforms to `items/README.md`. Read that first; the class-or-`.tres` rule is applied here
> without exception.

## 1. Does this genre need items at all?

**Yes — keys, consumables, quest items — but it is a thinner rpg, not a different problem.**

The genre's own config is nearly silent: `catalogs/skins/topdown/genre.json:11` has a `tuning`
block whose only gameplay key is `move_speed: 200` (and that one **is** consumed —
`core/BeepGenreGenerator.cs:183`). It declares one screen, `pause_subscreen.tscn`
(`genre.json:9`), and no `nav_wiring`. Its description is *"Top-down adventure / RPG"*
(`genre.json:6`).

The **scenes**, not the config, are what ask for items — and they ask loudly:

- `topdown/pause_subscreen.tscn:72-99` — an 8-column inventory grid of `PanelContainer`s, and
  at `:101-110` an **`EquipRow` with `WeaponSlot`, `ArmorSlot`, `AccessorySlot`**. Those three
  names are `EquipSlot.MainHand` / `Body` / `Accessory` (`phase-1-item-resources.md:109`)
  drawn as boxes. This genre's pause screen is a picture of `EquipmentComponent`.
- `topdown/pause_subscreen.tscn:122-129` — quests: *"Find the Master Sword (0/1)"*,
  *"Defeat 5 enemies (2/5)"*, *"Collect 100 gold (45/100)"* → `ObjectiveType.Collect`,
  `Kill`, `Collect` (`ecs/QuestComponent.cs:73`).
- `topdown/pause_subscreen.tscn:150-153` — a `GoldValue` label reading `🪙 45`.
- `templates/scenes/levels/topdown/level_1.tscn:20` — an **empty `Items` `Node2D`**, sitting
  beside `Enemies` (`:16`) and `TransitionZones` (`:18`). The level template has already
  reserved the parent node for item instances.

The whole screen is static markup — its only scripts are `ThemePresetComponent`,
`GameInfoBinder` and the nav-only `PauseSubscreen.cs` (`pause_subscreen.tscn:3-5`).

**One caveat that outranks the item model here.** `topdown_main.tscn:48` puts
`InteractableComponent` under the `Player` `CharacterBody2D` (`:29`). It requires an `Area2D`
parent (`ecs/InteractableComponent.cs:34`; `phase-5-archetypes-per-genre.md:36-38`) → the
topdown player **cannot interact with anything**. Every item verb in this genre routes through
interaction or pickup, so the tree below is authorable but unreachable until that is fixed.
Not this initiative's fix; stated so it is not rediscovered as an item bug.

## 2. The tree

**Spine branches used:** `GameItem`; `GameEquipment` → `GameWeapon`, `GameArmor`;
`GameConsumable`. **`GameShield` and `GameLiquid` are unused** — nothing in the genre's scenes
or components mentions blocking, volume, or an undrinkable liquid. The branches remain
available; this genre simply authors none.

```
GameItem          keys, quest items, gold
GameEquipment  ├── GameWeapon    sword, bow           → WeaponSlot
               └── GameArmor     tunic, ring          → ArmorSlot / AccessorySlot
GameConsumable    heart, potion, bomb
```

The `.tres` set a developer would author:

| File | Class | Note |
|---|---|---|
| `sword_master.tres` | `GameWeapon` | `pause_subscreen.tscn:123` names it as a quest target; it is a weapon **and** a `QuestObjective.TargetId`, which needs no extra field — see §3 |
| `bow_wood.tres` | `GameWeapon` | `IsRanged = true`, `ProjectileScene` |
| `tunic_green.tres` | `GameArmor` | `Slot = Body` → `ArmorSlot` (`pause_subscreen.tscn:107`) |
| `ring_power.tres` | `GameArmor` | `Slot = Accessory` → `AccessorySlot` (`pause_subscreen.tscn:110`). A ring is `Defense` + `Resistances` at a different slot — values, not a kind. |
| `heart_container.tres`, `potion_red.tres` | `GameConsumable` | `HealAmount` |
| `bomb.tres` | `GameConsumable` | `StatusEffectId` / a `WorldScene` that explodes. The throw is the scene's job, not a field's. |
| `key_small.tres`, `key_boss.tres` | `GameItem` | The only working item gate in the addon: `DoorSwitchComponent.RequiredItem` → `InventoryComponent.HasItem` (`ecs/DoorSwitchComponent.cs:21,60-66`). `Id` is the whole contract. |
| `gold.tres` | `GameItem` | `MaxStack` high. No currency component exists (grep `Currency|Gold` over `addons/` → 0 hits); `pause_subscreen.tscn:152` is a static label. A stack, not a kind. |
| `quest_map_piece.tres` | `GameItem` | `Id` matched by `QuestObjective.TargetId` (`ecs/QuestComponent.cs:77`) |

Traits (README §traits):

| `.tres` | `IsStatic` | `IsDestructible` |
|---|---|---|
| `sword_master.tres` | false | false — a Zelda-shaped sword does not break; therefore **no `HealthComponent`** on its `WieldScene` (`phase-1-item-resources.md:151`) |
| `key_small.tres`, `gold.tres`, `potion_red.tres` | false | false |
| `pot_clay.tres` (the breakable urn) | **true** | **true** → `HealthComponent` as durability, `Died` = smash → roll a drop table |

That last row is the genre's one interesting composition, and it is derived from data, not
remembered.

## 3. New framework classes this genre earns

**None.**

topdown is a subset of rpg (`items/rpg.md` §3), and rpg earned nothing either. The tests run:

- **`GameKey : GameItem`** — the door names the key (`ecs/DoorSwitchComponent.cs:21`), not the
  reverse. No field. Empty body → `.tres`. (Same reasoning as `rpg.md` §3; cited, not
  re-derived.)
- **`GameQuestItem : GameItem`** — `QuestObjective.TargetId` names the item
  (`ecs/QuestComponent.cs:77`). `sword_master.tres` shows why a class here would be actively
  wrong: it is a `GameWeapon` **and** a quest target simultaneously, so "quest item" is a role
  the quest assigns, not a kind the item is. A class would force the developer to choose.
- **`GameAccessory : GameEquipment`** — the `AccessorySlot` (`pause_subscreen.tscn:110`) is
  `EquipSlot.Accessory`, a **value** of the enum `GameEquipment` already carries
  (`phase-1-item-resources.md:109`). A class would be a rename.
- **`GameCurrency`** — `MaxStack` covers it.

## 4. Components this implies

**Already serve it:**

| Component | Serving what |
|---|---|
| `DoorSwitchComponent` (`ecs/DoorSwitchComponent.cs:14`) | Keys. Works end to end — the addon's one intact item edge. |
| `InventoryComponent` (`ecs/InventoryComponent.cs:24`) | The bag behind `pause_subscreen.tscn:72-99` |
| `QuestComponent` / `QuestObjective` (`ecs/QuestComponent.cs:13,71`) | The three shipped objectives. `ProgressObjective` has no callers (README §known). |
| `PickupComponent` (`ecs/PickupComponent.cs:11`) | Ground items under `level_1.tscn:20`'s `Items` node. `Collected → AddItem` is 0 connections (README §known). |
| `HealthComponent` (`ecs/HealthComponent.cs:12`) | `pot_clay.tres` durability — blind, no parent cast |
| `DialogComponent` (`ecs/DialogComponent.cs`) | The `DialogLayer` at `topdown_main.tscn:58-80` — **inert markup**; `DialogStarted` reaches nothing (`phase-6:79`). Item-adjacent only (a shopkeeper), not item model. |
| `HealthComponent.Armor` (`:16`), `ResistanceComponent` (`ecs/ResistanceComponent.cs:15-22`) | The receiving half of `GameArmor` — idle, ready |

**New, forced by this tree — all already scheduled elsewhere; this genre adds no new ask:**

| Component | Why | Where |
|---|---|---|
| `EquipmentComponent` | `pause_subscreen.tscn:104-110` draws three equip slots and nothing can fill them. `GameStateData.EquippedWeapons` is a `List<string>` (`core/GameStateData.cs:258`) no component writes. | `phase-2-equipment.md` |
| `DropTableEntry : Resource` | `pot_clay.tres` smashing into a drop needs an authorable table; `_entries` has no `[Export]` (README §known). | `phase-6:69` |
| `ContainerComponent` | Only if the developer wants chests. `pause_subscreen` does not show one — **do not** count this as forced by topdown. | `phase-6:83` |

**Blocking, but not ours to fix here** (cite, do not re-litigate): the
`InteractableComponent`-on-`CharacterBody2D` bug (`topdown_main.tscn:48`,
`phase-5-archetypes-per-genre.md:36-38`), and `LevelTransitionComponent` for the empty
`TransitionZones` node (`level_1.tscn:18`, `phase-6:78`).

## 5. Content vs framework

**We ship:**
- The spine. **Nothing topdown-specific** — that is the finding.
- `EquipmentComponent`, `DropTableEntry`, and the `Pickup.Collected → Inventory.AddItem` edge
  (Phase 4) — which is what makes `level_1.tscn:20`'s `Items` node mean anything.
- `pause_subscreen.tscn` rewired: `InventoryComponent` behind the grid, `EquipmentComponent`
  behind `EquipRow`, `QuestComponent` behind the quest labels. A template is "correct
  structure, wired components" (`CLAUDE.md` § *Scope*); three hardcoded quest strings and an
  8-column grid of empty panels are not.

**The developer authors:**
- Every `.tres` in §2. All of them.
- Which door wants `key_boss.tres`; which quest wants `sword_master.tres`.
- The contents of `Items`, `Enemies`, `NPCs`, `TransitionZones` (`level_1.tscn:14-20`) — an
  empty level is the developer's canvas, by design (`CLAUDE.md` § *Scope*).
- All art and every `WorldScene`.

**The boundary:** topdown's item needs are met entirely by classes rpg already justified.
If this genre ever needs a class of its own, that is a signal the spine is wrong — not that
topdown is special.
