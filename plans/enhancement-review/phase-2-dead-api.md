# Phase 2 — Dead API: wire or remove

## Why

A declared `[Signal]` that never fires, or an `[Export]` that nothing reads, is worse than
absent: a developer connects/sets it and **waits forever with no error** — the exact
"silence indistinguishable from breakage" failure `CLAUDE.md` names. The signal audit found
7 dead signals and 2 dead C# events across 239/9; the component passes found 6 dead exports.
Each row below needs a **wire it or delete it** decision (D1 in the tracker); the
recommendation column is the default if no one objects.

## Dead signals (declared, never emitted anywhere)

| Signal | Where | Recommendation |
|---|---|---|
| `Fled` | `ecs/AnimalBehaviorComponent.cs:39` | **Wire** — emit on transition into `Fleeing` state; one line, the state machine already exists. |
| `LineDisplayed` | `ecs/DialogComponent.cs:20` | **Wire** — add a public `Advance()` that steps lines and emits; `TextSpeed` (dead export, below) becomes its pacing. Alternative: delete both and document that `DialogUIComponent` owns progression. |
| `DialogFinished` | `ecs/DialogComponent.cs:21` | Same decision as `LineDisplayed` — the same-named signal on `DialogUIComponent` (`:451`) is a different class; a `DialogComponent` subscriber today waits forever. |
| `QuestFailed` | `ecs/QuestComponent.cs:20` | **Wire** — add public `FailQuest()` that emits it. A quest system where quests cannot fail is half an API. |
| `BindingRefreshed` | `ecs/ui/DataBinderHostComponent.cs:29` | **Wire** — emit from `Binding.Refresh()` when the pushed value actually changed. |
| `OverwriteConfirmed` | `ecs/ui/SaveGameMenuComponent.cs:21` | **Wire** — `OnSavePressed` (`:134`) currently overwrites an occupied slot with *no confirmation path at all*; emit when the selected slot has metadata and gate the write on it. This is also a data-loss guard, not just API hygiene. |
| `LoadStarted` | `ecs/ui/SaveLoadManagerComponent.cs:26` | **Wire** — `ShowLoadMenu` (`:130`) just lacks the symmetric emit `ShowSaveMenu` has for `SaveStarted` (`:109`). One line. |

## Dead C# events (declared, never invoked)

| Event | Where | Recommendation |
|---|---|---|
| `RowDoubleClicked` | `core/BeepDataGrid.cs:29` | **Wire** — detect double-click in the row `GuiInput` (check `InputEventMouseButton.DoubleClick`) and invoke. |
| `ItemDoubleClicked` | `core/BeepTreeView.cs:22` | Same. |

## Dead exports (set in the inspector, read nowhere)

| Export | Where | Recommendation |
|---|---|---|
| `StatName` | `ecs/AutoHealComponent.cs:19` — tick always calls `_health.Heal()` regardless ("mana" config silently heals HP) | **Decision D3.** Wiring "mana"/"stamina" needs `StatsComponent`/`HungerStaminaComponent` routing — that belongs to the **entity-model** initiative. Default here: narrow to health-only (remove the export) and leave a doc comment pointing at entity-model. |
| `HealOnlyOutOfCombat` | `ecs/AutoHealComponent.cs:18` | **Wire** — gate the tick on time-since-last-damage (subscribe the sibling `HealthComponent.Damaged`, track a timestamp). Cheap and genuinely useful. |
| `AutoStart` | `ecs/MovingPlatformComponent.cs:19` | **Wire** — see Phase 3 (it's a behavior bug: `AutoStart=false` platforms still move). |
| `MaxScreenDistance` | `ecs/FloatingTextComponent.cs:21` | **Wire** — cull `ShowText` when the spawn point is farther than this from the camera; or delete. |
| `SaveMenuScenePath` / `LoadMenuScenePath` | `ecs/ui/SaveLoadManagerComponent.cs:19-20` — code always loads the `SaveMenuPrefab`/`LoadMenuPrefab` strings (`:112,139`) | **Wire** — prefer the NodePath/PackedScene over the string when set, warn if both are set. (Typed exports over path strings is the repo's own rule.) |
| `TextSpeed` | `ecs/DialogComponent.cs:15` | Rides the `LineDisplayed` decision above. |

## Duplicate pathways (found while answering "is save/load duplicated?")

Save/load itself is **layered, not duplicated** — one engine (`GameStateManagerComponent`,
autoload), one menu UI pair, one opener (`SaveLoadManagerComponent`), and the
`GameInfo.SaveDirectory`/`MaxSaveSlots` "duplicates" are a deliberate documented seed
(`ecs/GameStateManagerComponent.cs:69-80`). Round-2's `FindSaveables` reimplementation is
fixed (`:332` now calls `SaveableHelper.FindAllSaveables`). But two parallel-route overlaps
do exist:

| Overlap | Where | Recommendation |
|---|---|---|
| `NavigationComponent.SaveGameRequested`/`LoadGameRequested` — a second route to the save/load menus. `Dispatch("save_game"/"load_game")` emits them (`ecs/NavigationComponent.cs:109-110,149-150`), but **nothing consumes them**; the shipped `MainMenu.cs`/`PauseMenu.cs` bypass navigation and call `SaveLoadManagerComponent.ShowSave/LoadMenu()` directly. | `ecs/NavigationComponent.cs:109-110` | Keep the signals (valid public API for custom menus) but doc-comment on both `NavigationComponent` and `SaveLoadManagerComponent` which route is canonical — or have a sibling `SaveLoadManagerComponent` auto-subscribe them so both routes work. Decision rides D1. |
| `DialogComponent` vs `DialogUIComponent` — overlapping dialog responsibility with a **same-named `DialogFinished`** on each (only the UI one fires). Already covered by the dead-signal rows above; the decision there should also state which class owns dialog, in both doc comments. | `ecs/DialogComponent.cs` / `ecs/ui/DialogUIComponent.cs` | Per the signal rows above. |

Known and deliberate (do not re-flag): `TypewriterComponent` vs `UIEffectComponent`'s
Typewriter effect (round-2 decision: keep the lightweight standalone widget);
`ScreenShakeComponent` (camera) vs `ShakeComponent` (UI) and `SlideComponent` (crouch-slide)
vs `SlideInOutComponent` (UI) are name-twins in different domains, not duplicates.
The **two-score-systems** overlap (`GameFlowComponent.Score` vs `GameApp.SessionScore`) is
the real architectural one — it's Phase 5b.

## Gotchas

- Deletions need explicit go-ahead per the standing no-delete rule — the decision table IS
  that ask; nothing gets removed until D1/D3 are answered.
- When wiring `OverwriteConfirmed`, keep the default flow non-blocking: emit + a simple
  confirm state inside the menu, not a hard modal dependency.
- `DialogComponent` vs `DialogUIComponent` share a signal name — whatever the decision,
  leave a doc comment on both classes stating which one owns dialog progression.

## Verify

1. Build + validator.
2. Grep gate: after this phase, `[Signal]`-declared names with zero `EmitSignal` references
   must be **zero** (re-run the audit grep; it's the class-of-defect check).
3. Editor: save into an occupied slot → confirmation fires; fail a quest via `FailQuest()`
   → `QuestFailed` received by a test connection.
