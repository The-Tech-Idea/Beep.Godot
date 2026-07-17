# UI screens & HUD — 22 entries

The HUD, the menus, and the 33 `ecs/scenes/**` screen scripts.

> **Navigation works.** All 33 screens resolve to exactly one `.tscn` each, and `SceneNav` is the
> shared spine behind every one. The defect here is a different shape: **five screens write a
> selection nobody reads.**

---

## The headline: five write-only selection channels

`SelectedVehicle`, `SelectedCharacter`, `levelup_pick`, `retry_bonus` and `booster_N` are each
**written by exactly one screen and read by zero.**

Worse — **three of them carry comments boasting they *fixed* the "all buttons did the same thing"
bug** (`VehicleSelect.cs:24-25`, `CharacterSelect.cs:20-21`, `LevelUpChoice.cs:17-18`). The fix
stopped at the write. **Behaviourally the buttons are still identical**; the divergence just moved
from the handler into a field nobody reads. That is a *worse* state than the original bug, because
it now looks fixed.

**The template to follow:** `SetLevel` / `CurrentLevel` is the **one** selection channel with a
real consumer (`LevelLoaderComponent.cs:50`). The other five should look like it.

---

## ALIVE — real logic

### `MainMenu` (107 lines)
**Evidence:** `NewestSlot()` (`:66-80`) gates Continue visibility (`:26`); `OnContinuePressed`
(`:82-102`) uses `LoadForSceneChange` (defers restore past the scene swap — correct);
`OnNewGamePressed` (`:45-49`) routes via `GameInfo.NewGameScenePath` — **the edge that makes
Garage / CharacterSelect / LevelMap reachable.** Keep.

### `SettingsMenu` (128 lines)
**Evidence:** binds 6 widgets + resolution (`:49-59`) + locale (`:61-72`). `Bind` (`:98-106`)
**seeds before subscribing** (`:102-104`) — correct ordering. `OnClosePressed` (`:115-123`)
distinguishes overlay vs scene. Keep.

### `PauseMenu`, `PauseSubscreen`
**Evidence:** `PauseMenu:20-32`; `:24` opens settings as an **overlay** rather than `ChangeScene`
— correct, per the scene-layer conventions. `PauseSubscreen` tab rail (`:15-18`); `OnSavePressed`
(`:26-39`) does a real `SyncAllSaveables()` + `SaveAutosave()`. Keep.

### `LevelSelect`, `LevelMap`, `LevelComplete`, `LevelResults`
**Evidence:** the **one selection channel with a consumer** — `SetLevel` → `LevelLoaderComponent`
reads `CurrentLevel`. `LevelSelect.cs:26-30`, `LevelMap.cs:30-34`, `LevelComplete.cs:22-32`,
`LevelResults.cs:22-33` (both `CompleteLevel(CurrentLevel)` then advance). Keep.

### `CardBattle`
**Evidence:** the only genre screen with a state machine — `OnEndTurn` (`:32-47`) /
`EndOpponentTurn` (`:49-61`), re-entrancy guarded (`:41`, `:54`). Keep.

### `Garage`
**Evidence:** `:18-21` — **the sole inbound edge to `VehicleSelect`.** Keep.

### `HudComponent`
**Evidence:** 5 shipped scenes. Binds `GameFlowComponent` (`:42-49`) and the player's
`HealthComponent` (`:54-66`) via `PlayerPath` (`:23`). **`:45` is one of only six code-side
subscriptions in the addon.** Keep — and `PlayerPath` is the pattern `BossHealthBarComponent`
should have used.

### `SceneNav`
**Evidence:** static; called by **33** screen scripts. `CloseOrReturn` (`:57-60`) frees rather than
navigates — correct for overlays. Keep — **the highest-leverage file in `ui/`.**

### `GenreScreenComponent`
**Evidence:** 5 shipped scenes; opens genre screens via `GameInfo.GetGenreScenePath` (`:99`),
populated by `BeepGenreGenerator.cs:145`. Its `:126-133` documents the CanvasLayer-vs-Node2D trap
at length — **the doc `BossHealthBarComponent` needed and didn't read.** Keep.

### `SettingsOverlay`, `SaveLoadManagerComponent`, `SaveGameMenuComponent`, `LoadGameMenuComponent`, `PauseComponent`, `SceneTransitionComponent`, `AnimatedMenuComponent`
All shipped and reached. Notes:
- `SaveLoadManagerComponent` extends `GameplayComponent` (`:17`), not `UIComponent` — mild category
  smell, not misplaced.
- **`AnimatedMenuComponent` has never animated anything** — see below.

### The 14 thin screen stubs
`Codex`, `Collection`, `DeckBuilder`, `BuildMenu`, `Districts`, `Economy`, `Character`,
`Inventory`, `Quests`, `Diplomacy`, `Research`, `UnitPanel`, `Backpack`, `Crafting`, `WorldMap`.

**Evidence:** 19 lines each, byte-identical shape; one BackButton via `CloseOrReturn`. Reached as
overlays through `GenreScreenComponent`. **Navigation is the job and it works.** Keep.

**Defect:** each retains a private `ChangeScene` helper at `:17` with **zero call sites** — residue
of the `SceneNav` refactor. **C# emits no warning for an unused private method.** The comment at
`:16` ("byte-identical in all 33 screen scripts") is now stale. **Delete all 13.** → `DELETE.md`

---

## ALIVE (crippled)

### `AnimatedMenuComponent` — ships in 5 scenes and has never animated anything
**Evidence:** 5 shipped scenes (`main_menu.tscn:6`, `game_over.tscn:6`, …). **The component
self-documents the bug at `:32-37`:** *"Both scenes that ship it parent it at the root … so it has
never animated anything."*

It animates `GetParent()`'s children; parented at the root, its "children" are the wrong nodes.

**Fix (S):** reparent under the button `VBoxContainer` in all 5 `.tscn`. Per the scene-layer
conventions in `CLAUDE.md`: *"parent it to the Container holding the items."*

### `LevelFailed` — Retry and "Retry with Bonus" are functionally identical
**Evidence:** navigation is real (`:15` → LevelMap, `:23`/`:32` → game). But **`retry_bonus`
(`:22`,`:31`) has no reader.** The two buttons do exactly the same thing.

**Fix (M):** give it a consumer, or collapse the two buttons. **A button that lies is worse than a
missing button.**

### `PreLevel` — boosters round-trip only to themselves
**Evidence:** navigation real (`:14-15`). **`booster_1..3` is written (`:35`) and read back
(`:33`) purely to re-seed its own checkbox.** No level scene reads it, despite the promise at
`:19-20`.

**Fix (M):** give it a consumer, or remove the boosters.

---

## INERT — write-only channels

### `VehicleSelect`
**Evidence:** `:28` writes `app.SelectedVehicle` — **read by nothing.** The only other references
are `GameApp.cs:55` (declaration) and `:244` (reset). Navigation works (`:18`,`:29`), but
`Garage.cs:21` already reaches the race directly, **so picking a car is its entire purpose and it
is dead.**

**Second defect:** `:18` hardcodes `res://scenes/ui/racing/garage.tscn` — invisible to
`nav_wiring`, as its own comment (`:13-17`) admits.

**Fix (M):** needs a vehicle spawner — which does not exist; racing ships **no player controller at
all**. Gate on `VehicleController` (see `movement.md`).

### `CharacterSelect`
**Evidence:** `:24` writes `SelectedCharacter`; only other refs are `GameApp.cs:53` (declaration)
and `:240` (reset). Same shape.

**Fix (M):** needs a spawner that reads it.

### `LevelUpChoice`
**Evidence:** `:42` `SetGameData(PickKey, action)`; **`PickKey` / `"levelup_pick"` appear nowhere
else.** Its `:9-11` concedes *"Applying the upgrade is the game's job"*. The metadata plumbing
(`:30-37`) and overlay `QueueFree` (`:47`) are correct — **the payload evaporates.**

**Fix (S, gated):** **there is no permanent-modifier channel.** The 2-line
`StatusEffectComponent` duration guard (`combat.md`) *creates* it — a `duration < 0` modifier **is**
a permanent upgrade. Then this screen works. **This is the shooter roguelite's core loop.**

### `BootComponent`
**Evidence:** 0 refs; **no boot scene ships.** `_Process:51` counts down then `TransitionOut()`
(`:63`) needs a `NavigationComponent` **sibling** no scene provides (falls back to
`ChangeSceneToFile`).

**Fix (S):** ship a `boot.tscn`, or delete.

---

## Order

1. **`AnimatedMenuComponent`** reparent in 5 scenes (S) — it ships in 5 scenes and does nothing.
2. **`StatusEffectComponent` duration guard** (S, in `combat.md`) → `LevelUpChoice` becomes real.
3. Delete the 13 dead `ChangeScene` helpers (S).
4. Give `retry_bonus` / `booster_N` a reader, or remove the buttons (M).
5. `VehicleSelect` / `CharacterSelect` — gated on a spawner + `VehicleController` (L).
6. Ship `boot.tscn`, or delete `BootComponent`.
