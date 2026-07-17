# Items & economy — 17 components

Inventory, crafting, drops, pickups, interaction, production, growth.

> **This is where the entity-model plan lands, and the loop is broken end to end today:**
> you cannot pick anything up, nothing drops anything, and crafting eats your materials.
> Building `GameItem` on top of this would produce beautiful data nothing can move.
> **Fix the loop first** — that is what makes Phases 1–4 reachable rather than decorative.

---

## The item loop, as built

| Edge | State |
|---|---|
| `PickupComponent.Collected` → `InventoryComponent.AddItem` | **0 connections.** The signatures are *identical* — `(string itemId, int quantity)` — and were never wired. |
| `HealthComponent.Died` → `DropTableComponent.Roll()` | **Never.** Only `DestructibleComponent.Break` rolls, and it can't be damaged. |
| `CraftingComponent.Craft` → the output | **Deducts inputs, grants nothing.** |
| `DropTableComponent`'s entry list | **No `[Export]`.** Cannot yield, ever. |

**Four broken links in one loop.** The cheapest repair order is at the bottom of this doc.

---

## BROKEN

### `DropTableComponent` — the single highest-leverage fix in the addon
**Evidence:** `_entries` is `private readonly List<DropEntry>` (`:28`) with **no `[Export]`** —
while **eight unrelated knobs above it are exported** (`MinDrops:16`, `MaxDrops:17`,
`DropChance:18`, `DifficultyWeightMultiplier:19`, `ScatterRadius:20`, `DropLifetimeSeconds:21`,
`MaxPlacementAttempts:22`, `MinimumSpacing:23`). Its only filler `AddEntry()` (`:50`) has **0
callers**. So `Roll()` always returns at `:76` (`_entries.Count == 0`).

**Both callers are guaranteed no-ops:** `DestructibleComponent.cs:55` **and**
`CropGrowthComponent.cs:118`. The entire weighted / seasonal / scatter engine (`:104-173`) —
including the season-awareness at `:36-38,:45-48` that survival needs — is unreachable.

**Fix (M):** `[Export] DropTableEntry[]` as a `[GlobalClass] Resource`, mirroring
`CraftingIngredient` (`CraftingComponent.cs:65`) — the pattern is already in the file next door.

> **One fix un-deadens `DestructibleComponent`, `CropGrowthComponent`, and all loot at once.**
> It blocks Phase 4. Do it first.

### `CraftingComponent` — net item destruction
**Evidence:** `Craft()` (`:30`) removes inputs at `:40`, then `EmitSignal(Crafted,
recipe.OutputItem)` at `:42` — **and never calls `inventory.AddItem`.** The comment reads
`// Grant result.` above the emit. `OutputCount` (`:58`) and `CraftTime` (`:59`) are never read.

It already **has** the `InventoryComponent` as a parameter (`:21,:30`).

**Fix (S):** `inventory.AddItem(recipe.OutputItem, recipe.OutputCount)` before the signal.
**One line.** Also honour `CraftTime` or delete it — a shipped export that does nothing is this
repo's signature defect.

### `InventoryComponent.Interact.cs` — drag-drop, split, and tooltips are all dead
**Evidence:** `OnSlotGuiInput` (`:23`), `OnSlotMouseEntered` (`:64`), `OnSlotMouseExited` (`:69`)
are **private with 0 callers** — `Display.cs:42-54` builds the `PanelContainer` slots and **never
connects** `GuiInput`/`MouseEntered`/`MouseExited`. So drag-and-drop, right-click stack split, and
the whole tooltip path (`Display.cs:139 ProcessHover` gates on `_hoveredSlot < 0`; `SetHoverSlot`
is only reachable from the unwired handlers) are unreachable.

**Second defect:** `CycleSortMode` (`:81`) hardcodes `current = ByType` on every call → it always
sorts `ByRarity`. The button appears to work and does one thing forever.

**Fix (S):** connect the three signals in `BuildSlots`. Track sort state instead of reassigning it.

### `InteractableComponent` — semantically inverted, not merely misparented
**Evidence:** `_area = GetParent() as Area2D` (`:30`) → **null** on the `CharacterBody2D` parents
in `topdown_main.tscn:48` and `robot_npc_template.tscn:38` → `BodyEntered` never hooked (`:35`) →
`_playerInRange` never true → `_Input` (`:66`) returns forever. **Interaction is 100% dead in both
shipped scenes.**

**The topdown case is worse.** The component sits on the **Player**, with a sibling
`InteractionZone` Area2D at `:43`, and `IsPlayer` (`:44`) filters entering bodies to those named
`"Player"`. **So even correctly parented it would only ever fire for another player.** The player
is the *interactor*, not the interactable.

**Two different fixes (S each):**
- `robot_npc_template` — semantics are **right**; reparent under `DetectionArea` (`:47`). 1-line
  scene fix.
- `topdown_main` — **remove it from the Player**; it belongs on NPCs.

**Also (S):** it holds `PromptText` (`:13`) that **nothing reads**, and emits
`PlayerEnteredRange`/`PlayerExitedRange` (`:51`,`:60`) that **nothing listens to** — while
`InteractionPromptComponent.Show()`/`Hide()` sit with 0 callers. See `ui-widgets.md`. ~4 lines
closes it and activates the dead export.

### `DoorSwitchComponent` — doors go invisible while staying solid
**Evidence:** `Toggle()` does `_door.SetDeferred("monitoring", !_isOpen)` (`:77`) — **`monitoring`
is an `Area2D` property**, but `_door` is typed `CollisionObject2D` (`:33`) and the doc says
StaticBody2D/AnimatableBody2D (`:7`). The property doesn't exist there → collision never changes.

**Second defect:** `FindChild("Player", false, false)` (`:55`) — a **hardcoded, non-recursive,
unowned** node name. Rename or nest the player and every gated door silently stops working.

**Fix (M):** disable the `CollisionShape2D` / clear `collision_layer` instead of `"monitoring"`.
Resolve the player by **group** (after the group fix), not by name.

### `CheckpointComponent` — computes the respawn position, then throws it away
**Evidence:** `_activated = true` (`:37`) is **never reset** → `SingleUse` (`:19`) is dead, every
checkpoint is single-use, and `:54` only sets `Monitoring = false` — it does **not** gate the
latch. It computes `pos` (`:39`), emits `CheckpointActivated(pos)` (`:52`) — and **persists
`app.SetCheckpoint(app.CurrentLevel)` (`:43`), a level *index*.** The position is discarded.
`GameApp.LastCheckpointLevel` (`:81`) is read by **nothing**. `HealOnActivate` defaults **true**
(`:16`). 0 scenes — `Checkpoints` is an empty `Node2D` (`level_1.tscn:28`).

**Fix (M):** store the `Vector2`; reset `_activated` on body exit; honour `SingleUse`.
**That makes it the proposed `RespawnComponent`** — no new type. The consumer edge
(`HealthComponent.Died` → move to checkpoint) still needs building.

**Cannot serve lap counting** — three independent blockers (permanent latch, level-index storage
so reverse/skip crossings are indistinguishable, `HealOnActivate` respawn semantics).
`LapGate`/`LapTracker` are genuinely new, derived from `AreaTriggerComponent`.

---

## ALIVE

### `PickupComponent` — the one score edge, in an orphaned template
**Evidence:** `pickup_template.tscn`; `_Process:54`. Reaches `GameFlowComponent.AddScore` (`:102`)
via a lazy `CurrentScene` scan (`:87`) — **the only `AddScore` caller in the addon.** Parent:
`Area2D` (`:48`, silent no-op on mismatch).

**The chain behind it is complete and correct:** `:102 → GameFlowComponent.cs:72 → ScoreChanged →
HudComponent.cs:45`. **It is unreachable solely because `pickup_template.tscn` is instanced by
zero scenes** (verified: referenced only by `BeepGenreGenerator.cs:301` and docs).

**Defects:**
- **Respawn is dead.** `Collect()` sets `p.ProcessMode = Disabled` (`:105`), which **propagates to
  this child component**, so `_Process` stops and `_respawnTimer` (`:61`) never decrements. The
  shipped default `RespawnSeconds = 0` (`:18`) hides it.
- `Collect()` (`:93`) **never touches an `InventoryComponent`** — the item economy has no source.
- It ships a `FloatingTextComponent` (`pickup_template.tscn:26`) whose `ShowText()` has 0 callers.

**Fix (S):** **place one in a level → scoring works end-to-end.** Then: `ProcessMode = Always` on
self (or disable only the sprite); call `ShowText($"+{ScoreValue}")` beside `:102`; resolve the
collector's inventory and `AddItem`. **(Phase 4)** `[Export] GameItem? Item` **replaces** `ItemId`
(`:13`) — no fallback, no dual path.

### `DialogComponent`
**Evidence:** `dialog_template.tscn:11-12`. **Reached** — `InteractableComponent.cs:31,73` resolves
it as a sibling and calls `Interact()`.

**Defect:** `DialogStarted` (`:19`) → `DialogUIComponent.StartFromDialogComponent`
(`ui/DialogUIComponent.cs:215`) has **0 connections**, and in `dialog_template.tscn` the
Dialog/DialogUI pair sits under a `CanvasLayer` with no `Interactable`.

**Fix (S):** add the subscription. It is the only thing between this and working.

---

## INERT — missing driver

### `InventoryComponent` (core) — complete, correct, and nothing awards items
**Evidence:** `_Process → ProcessInteraction` (`:99`). Data model is sound. **`AddItem` (`:126`)
is reached only by its own `Load()` (`:346`) and `CraftingComponent`.** `RegisterItem` (`:108`) 0
callers. Resolved by `DoorSwitchComponent.cs:60`. 0 scenes.

**On `ContainerComponent` (proposed) — it is ~15 lines here, not a new type:**
- **Two inventories already work.** Nothing is static; `Resize` (`:297`) handles sizes.
- **Only transfer is missing.** Every mutation is intra-instance: `MoveItem(int,int)` (`:201`)
  indexes `Slots[]` on `this` for both ends; `SplitStack` (`:228`) likewise. **No method takes
  another `InventoryComponent`.** Add `TransferTo(other, slot, qty)` over the existing public
  `RemoveAt` (`:191`) + `AddItem` (`:126`).
- **Real blocker:** `ParticipatesInSave` (`:58`) is documented *"tick it on the player's inventory
  only — `GameStateData` keeps a single Inventory slot"* → **a chest cannot persist** without keyed
  multi-inventory save.
- Drag-across-grids is moot — drag-drop is dead (see `Interact.cs` above).

**Fix (M, Phase 1):** `InventoryItem` (`:30-42`) — a plain nested class with a stringly
`Dictionary<string,Variant> Stats` — becomes the `GameItem` `Resource` tree. Slots become
`InventorySlot { GameItem Item; int Quantity; }` (a `.tres` is shared by reference; quantity must
not live on it). Persist **id + quantity**, re-resolve on load.

### `WorkComponent` — a correct production model that has never run
**Evidence:** `StartWork(float)` (`:28`) and `Tick(double)` (`:37`) both **0 callers**, and the
component has **no `_Process`** — it does not tick itself. **`Tick` takes a `delta`**, so it was
explicitly designed for an external driver that was never written.

**Model (`:13-25`):** `AvailableWork`, `WorkSpeed`, `OutputItemId`, `OutputQuantity`,
`LoopProduction`, `TotalWorkRequired`; signals `WorkStarted`/`WorkAccomplished`/`WorkDone`/
`WorkStopped`. A barracks producing a unit and a furnace smelting ore are both `StartWork → Tick →
WorkDone`. **The shape is right.**

> **An earlier draft called this "the genre's one real asset" and "a usable production model
> today". That was wrong** — it is a correct model that has *never executed*. It is an asset
> because it needs no designing, not because it is wired.

**New defect, found on re-read — `Progress` is inverted.** `AvailableWork` counts **down** from
`TotalWorkRequired` to 0 (`:31-32`, `:41`), so `Progress => AvailableWork / TotalWorkRequired`
(`:25`) starts at **1.0** and ends at **0.0**. It is a *remaining* fraction named `Progress`. Bind
a progress bar to it and it starts full and drains as the job completes. **Nobody has seen it be
wrong because nothing calls it** — it will look correct in review until the first UI is wired.

**Also:** `LoopProduction` (`:47-48`) resets `AvailableWork` but never re-emits `WorkStarted`, so a
looping producer signals `WorkStopped` never and `WorkStarted` once.

### ⚠ `Tick(double delta)` is not a defect — it is the addon's only clock-injectable component

**Do NOT "fix" it by adding a `_Process`.** An earlier draft proposed exactly that. It is wrong.

`Tick` takes an **external** `delta` because a factory must tick **once per turn** in cardgame and
strategy, and **once per frame** in survival. `_Process` hardcodes the real-time answer and
excludes 2 of the 10 genres. **This signature is the right one** — the original author had the
insight and nothing else in the addon adopted it.

**Fix (S):** drive `Tick` from **`GameClock.Ticked`** (Phase 7). Keep the signature exactly.
`WorkComponent` then works in every genre, unchanged, and `TotalWorkRequired` / `WorkSpeed` become
clock units — so "a barracks takes 3 turns" and "a furnace takes 30 seconds" are the same two
fields.

**Also fix (S):**
- **Invert `Progress`** (`:25`), or rename it `Remaining` — it currently counts *down* from 1.0.
- **Re-emit `WorkStarted` on loop** (`:47-48`) — a looping producer signals `WorkStarted` once and
  `WorkStopped` never.
- **`OutputItemId` (`:15`) → `GameItem`** (Phase 1) — the same bare string as
  `CraftingRecipe.OutputItem`, retired in the same change.

### `CropGrowthComponent` — 70-80% of the proposed `HarvestableComponent`
**Evidence:** `_Process:71`. **`Harvest()` (`:114`) has 0 callers** → the `Harvested` stage is
unreachable. Its payoff is `_dropTable?.Roll()` (`:118`) — **which can never yield**. It spawns a
`ColorRect` into a `Node2D` parent (`:66`). `_seasonal` resolves from `GetTree().Root` (`:51`), and
`survival_main.tscn:14` **does** instance `atmosphere.tscn` — so that half would work.

**Fix (M):** **extend this, don't add a parallel `HarvestableComponent`.** It already has stages
(`:15`), `Harvest()`, and the drop roll. Add a tool-class gate and a non-crop (rock/tree) mode;
drop the `_seasonal`-mandatory guard (`:73`). **Blocked on `DropTableComponent` regardless.**

### `QuestComponent`
**Evidence:** `ProgressObjective` (`:26`) and `CompleteObjective` (`:46`) both **0 callers**. 0
scenes. `QuestObjective` (`:71`) is already a proper `[Tool][GlobalClass] Resource` — the right
pattern. The shipped `quests.tscn` objective *"Defeat the Dark Lord (0/1)"* can never tick.

**Fix (M):** needs an event bus — `HealthComponent.Died` / `PickupComponent.Collected` →
`ProgressObjective`. Gated on damage and on the pickup edge.

### `ParticleComponent`
**Evidence:** `_Process` (follow) `:56`. **`Burst()` (`:62`) has 0 callers.** Needs both a node
**and** a `ParticleScene` (`:34`) or `SetupParticles` does nothing. 0 scenes.

**Fix (S):** `HealthComponent.Died → Burst()`. Ship a default scene or warn on null.

### `FloatingTextComponent` — instantiated, and does literally nothing
**Evidence:** `pickup_template.tscn:26-27` as `PickupText`. **`ShowText()` (`:25`) has 0 callers**
repo-wide, and the scene has no `[connection]`. `MaxScreenDistance` (`:21`) never read. No
`_Process` — it is a passive verb bag.

**Fix (S):** call it beside `PickupComponent.cs:102`. **The clearest instance of fact 3 in the
repo.**

---

## Order

1. **`DropTableComponent` `[Export]`** (M) — un-deadens destructibles, harvesting, all loot.
2. **Place a pickup in a level** (S) — scoring works end-to-end.
3. **`CraftingComponent` grant** (S) — one line; stops material destruction.
4. **`Pickup.Collect → Inventory.AddItem`** (S) — gives the item economy a source.
5. `InteractableComponent` — two scene fixes (S) + the prompt wire (S).
6. `InventoryComponent.Interact` — connect three signals (S).
7. `CheckpointComponent` → `RespawnComponent` (M).
8. `WorkComponent` — `Progress` inversion + self-tick (S).
9. **Phase 1** — `GameItem` tree; `InventoryItem` and every `ItemId` string retire onto it.
