# FIX-11 — Calendar HUD signal reconnect: date/season labels go dead after the calendar node is replaced

**Type:** fix · **Area:** `GridCalendarHudComponent` (mirrors `GridObjectivePanelComponent`, `EntityComponent.Resolve`) · **Status:** **IMPLEMENTED 2026-09-11** · **Effort:** S (½ day) · **Risk:** low

## Outcome (2026-09-11)

The calendar signal wiring moved out of `_Ready` into new `ConnectCalendarSignals`/`DisconnectCalendarSignals` helpers, with `ResolveReferences` reconnecting whenever it adopts a new instance (guarded on `_calendar == null || !IsInstanceValid(_calendar)`, exactly as `GridObjectivePanelComponent` does for its tracker); `_ExitTree` now calls the shared disconnect. `_Ready` keeps its `ResolveReferences()` call, which now performs the initial connect, so there is one connect owner rather than two. Guard `VerifyGridCalendarHudReconnect` was added to `GridPlacementSmoke` and wired into `Run()`: it builds a calendar plus a generated HUD, frees the calendar, adds a replacement at the same path, forces a re-resolve, then advances the replacement externally and asserts the HUD's date label followed. Mutation-proven: dropping the `ConnectCalendarSignals()` call leaves the label on the pre-advance date ("Year 2, Summer 10 vs Year 2, Summer 11"). Build clean (0 warnings). As with FIX-09, the full `headless_runtime_smoke` cannot reach the new guard here because of the pre-existing `GridPlacementSmoke.cs:171` failure; behaviour and mutation were validated with a focused probe in this session.

## Finding

`GridCalendarHudComponent` connects its calendar signals exactly once, in `_Ready`, and never again — but it re-resolves the calendar reference on every refresh. When the calendar node it points at is freed and replaced (a world reload swaps the `GridCalendarComponent`), the HUD binds to the new instance for its label text yet stays connected to nothing, so the date and season labels stop updating on external `AdvanceDay` calls.

The data flow:

1. `_Ready` (`ecs/grid/ui/GridCalendarHudComponent.cs:33-47`) resolves the calendar and then, guarded by `!Engine.IsEditorHint() && _calendar != null` at :39, subscribes `_calendar.DayAdvanced/SeasonChanged/YearChanged` (`:41-43`). This is the **only** site that connects those signals.
2. Every subsequent read goes through `ResolveReferences` (`:215-216`), which is a bare `EntityComponent.Resolve(this, CalendarPath, ref _calendar)` — it refreshes `_calendar` but wires no signal.
3. `EntityComponent.Resolve` (`ecs/EntityComponent.cs:167-177`) returns the cached reference only while `GodotObject.IsInstanceValid(existing)` holds; once the old calendar is freed it re-resolves `_calendar` to the **new** instance (`:172-174`). So after a reload, `_calendar` points at a live node that the HUD has never subscribed to.
4. The label handlers `OnDayAdvanced/OnSeasonChanged/OnYearChanged` (`:209-211`) each call `RefreshHud`, and they are the sole path that repaints the date/season text on an external advance. With the new calendar unconnected, they never fire, so the date display is frozen against the live calendar.

The gap is masked, which is why it reads as low severity but is real:

- `_Process` (`:61-77`) polls `GridWorkClockComponent.DayProgress01` and keeps the **progress bar** filling, so the HUD does not look dead.
- The in-HUD advance button routes through `RequestAdvanceDay` (`:177-190`), which calls `_calendar.AdvanceDay()` then `RefreshHud()` directly, so pressing the button still updates the labels.
- Only an **external** day advance (another system calling `AdvanceDay` on the calendar) depends on the signal, and only that path shows stale text.

The sibling `GridObjectivePanelComponent` does not have this defect: its `ResolveReferences` (`:180-189`) re-connects on every re-resolve — it gates on `_tracker == null || !GodotObject.IsInstanceValid(_tracker)`, and when that trips it calls both `EntityComponent.Resolve` and `ConnectTrackerSignals` (`:191-202`), which disconnects-then-reconnects the tracker's signals (editor-hint guarded). The calendar HUD is missing exactly this reconnect discipline.

## Design

Move the calendar signal wiring out of `_Ready` and into a `ConnectCalendarSignals()` helper called from `ResolveReferences`, mirroring `GridObjectivePanelComponent.ConnectTrackerSignals` — one owner for the connect/disconnect of these three signals, reached from the one place the reference is (re)resolved.

- Add `private void ConnectCalendarSignals()`: return early when `_calendar == null || Engine.IsEditorHint()`; otherwise `-=` then `+=` each of `DayAdvanced/SeasonChanged/YearChanged` (idempotent, so a redundant call cannot double-subscribe), matching the objective panel's shape.
- Change `ResolveReferences` (`:215-216`) to reconnect when it actually adopts a new instance, following the panel's guard so a valid-and-unchanged calendar is not re-wired every call:

  ```csharp
  private void ResolveReferences()
  {
      if (_calendar == null || !GodotObject.IsInstanceValid(_calendar))
      {
          EntityComponent.Resolve(this, CalendarPath, ref _calendar);
          ConnectCalendarSignals();
      }
  }
  ```

  This is safe because `EntityComponent.Resolve` only mutates `_calendar` when the cached one is invalid; when it is valid the guard skips both the resolve and the reconnect.
- **Delete** the `_Ready` connect block (`:39-44`) — it is now redundant with the reconnect in `ResolveReferences` (which `_Ready` already calls at :35). Per the no-legacy rule this is a move, not a duplicate left in place: two connect sites would be a second owner of the same wiring. `_Ready` keeps its `ResolveReferences()` call, which now also performs the initial connect.
- Keep `_ExitTree`'s explicit disconnect (`:49-59`), or fold it into a `DisconnectCalendarSignals()` that both `_ExitTree` and `ConnectCalendarSignals`'s `-=` prefix share; either is acceptable, but do not leave a connect path without a matching disconnect on teardown.

No new public surface, no new consumer needed — the change relocates existing wiring behind the existing `ResolveReferences` call sites (`RebuildHud:90`, `RefreshHud:159`, `RequestAdvanceDay:179`, `DateText:194`, `_Ready:35`), all of which already run.

## Guards (fail first)

**Headless smoke — external advance after a calendar swap.** In a headless C# smoke (the `GridPlacementSmoke` family) or a `.gd` probe:

1. Build calendar `C1` and a `GridCalendarHudComponent` with `CalendarPath` pointing at it; run `_Ready`.
2. Free `C1`; add a fresh `C2` reachable at the same `CalendarPath`.
3. Force a re-resolve — call `hud.DateText()` (or `RefreshHud()`), which invokes `ResolveReferences` and adopts `C2`.
4. Call `C2.AdvanceDay()` (emits `DayAdvanced`).
5. **Assert** the HUD's date label text equals `C2.DisplayDate()` after the advance (i.e. the label moved with the external advance).

Mutation that trips it: revert the fix — remove the `ConnectCalendarSignals()` call from `ResolveReferences` (or restore the connect-only-in-`_Ready` form). After the swap the HUD is subscribed to the freed `C1`, `OnDayAdvanced` never fires for `C2`, and the label holds `C2`'s pre-advance date, so the assertion fails. This proves the guard can fail on exactly the missing reconnect.

**Optional scan pin (`tests/addon_contract_scan.ps1`).** Assert that `GridCalendarHudComponent`'s `DayAdvanced +=` subscription lives in a method also referenced from `ResolveReferences`, and forbid a lone `DayAdvanced +=` inside `_Ready`. Mutation: move the subscription back into `_Ready` → pin fails. The behavioral smoke is the primary guard; the pin is the cheap regression fence.

## Dependencies / collisions

- **FIX-09** (`FIX-09-objective-restore-signals.md`) is the complementary save-restore case: it makes `GridObjectiveTrackerComponent.RestoreState` re-emit its signals so signal-only HUDs repaint after load, citing `GridCalendarComponent.RestoreState`'s explicit `DayAdvanced` re-emit as the established pattern. FIX-11 is the other half on the *listener* side — the HUD must be connected to the live calendar for that re-emit to land. The two are independent fixes but share the calendar/HUD signal path; land them consistently.
- No collision with the streaming group (ENH-03/04, the concurrent `TerrainWorldComponent`/archive session) — this is UI-only and touches no terrain streaming, cell store, or job-queue code. DUP-05, ENH-01, ENH-02, DUP-13, ENH-12 do not intersect this file.
- Shares only the `EntityComponent.Resolve` contract (`ecs/EntityComponent.cs:167-177`), which is read, not modified.

## Out of scope

- The save-restore re-emit on the tracker/calendar side (FIX-09) — this plan does not change any `RestoreState`.
- The progress-bar polling in `_Process` and the advance-button binding, both of which already work.
- Generalising the resolve-and-reconnect pattern into a shared base for all signal-driven HUD panels; that is a broader refactor across `GridObjectivePanelComponent`, this component, and any future panel, and should be its own item rather than smuggled in under a single-component fix.
