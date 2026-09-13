# FIX-10 — Objective panel goal count: the summary reports the row cap, not the goal total

**Type:** fix · **Area:** `GridObjectivePanelComponent` (`ecs/grid/ui/`), reading `GridListPanelComponent.UpdateRows` · **Status:** **IMPLEMENTED 2026-09-11** · **Effort:** S (½ day) · **Risk:** low

## Outcome (2026-09-11)

The `if (objectives.Count >= MaxVisibleObjectives) break;` in `VisibleObjectives()` is gone, so the returned list is the full filtered goal set the summary counts; the drawn rows stay capped by `UpdateRows`' `maxVisible` argument, which already owned that decision. Guard `VerifyGridObjectivePanelGoalCount` was added to `GridPlacementSmoke` and wired into `Run()` — a separate method rather than an extension of `VerifyGridObjectivePanel`, so each guard keeps one concern: 8 active-on-start goals against the default cap of 6 must read `Goals 8 | Done 0` while drawing 6 rows, and completing a goal beyond the drawn rows must read `Goals 8 | Done 1` with the row count unchanged. Mutation-proven: restoring the `break` reads `Goals 6 | Done 0`. Build clean (0 warnings).

As with FIX-09 and FIX-11, the guard lives in `GridPlacementSmoke`, which the full headless smoke cannot currently reach (pre-existing failure at `GridPlacementSmoke.cs:171`), so behaviour and mutation were validated in this session with a focused probe driving the same public panel API.

## Finding

`MaxVisibleObjectives` is a row-count cap ("how many goal rows the panel draws", default 6, `GridObjectivePanelComponent.cs:29`). It is being made to enforce a second decision it was never meant to make: the headline `Goals N | Done M` count.

1. **`VisibleObjectives()` caps the enumeration, not just the rows.** It walks the tracker's objectives, applies the visibility filters (`HiddenUntilActive && !active` skip, `!active && !complete` skip, `HideCompleted && complete` skip), and then breaks out the moment the accumulated list reaches the cap (`GridObjectivePanelComponent.cs:158-175`, break at `:173-174`). The returned list is therefore never longer than `MaxVisibleObjectives`.

2. **`RefreshPanel` derives the summary totals from that already-capped list.** `objectives = VisibleObjectives()` (`:105`), `completed` is counted only within that list (`:106-109`), and the summary is written as `SummaryLabel!.Text = $"Goals {objectives.Count} | Done {completed}"` (`:111`). Both numbers are ceilinged at `MaxVisibleObjectives`. With the default cap of 6, ten active goals read `Goals 6`; if some of those uncounted goals are complete, `Done` undercounts too. The summary line is the only aggregate the player sees, so it silently misreports overall progress.

3. **The row cap does not depend on `VisibleObjectives()` truncating.** `RefreshPanel` already passes the cap through to the base renderer: `UpdateRows(ObjectiveRows(objectives), MaxVisibleObjectives)` (`:113`), and `GridListPanelComponent.UpdateRows` independently stops drawing once `shown >= maxVisible` (`GridListPanelComponent.cs:182-185`). So the truncation inside `VisibleObjectives()` is redundant for the rows — its only *observable* effect today is corrupting the summary count. Removing it leaves the drawn rows identical (the first `MaxVisibleObjectives` filtered goals, in the same enumeration order) while letting the summary see the full set.

## Design

One owner for two facts that were conflated: the **goal totals** are the full filtered set; the **row cap** is `UpdateRows`'s job.

- Drop the `if (objectives.Count >= MaxVisibleObjectives) break;` at `GridObjectivePanelComponent.cs:173-174` so `VisibleObjectives()` returns every objective that passes the `HiddenUntilActive` / active-or-complete / `HideCompleted` filters. That is the true active/completed goal set the summary must count.
- Leave `RefreshPanel` otherwise unchanged: `objectives.Count` and the `completed` loop (`:106-111`) now count the full set, and `UpdateRows(ObjectiveRows(objectives), MaxVisibleObjectives)` (`:113`) still caps the *drawn* rows at `MaxVisibleObjectives` via the base class's `shown >= maxVisible` guard. `ObjectiveRows` is a lazy `IEnumerable` (`:116-124`), so yielding the full set costs nothing past the rows `UpdateRows` actually consumes.
- `VisibleObjectiveRowCount()` stays as-is: it returns `RowCount` (the rendered-label count, `GridListPanelComponent.cs:47`), which remains bounded by the cap because `UpdateRows` bounds it — the existing smoke assertions on row count still hold.

No new API, no new field: this is the removal of one over-eager `break`, and the summary and the row list each read from the owner that should decide them. No renderer or GDScript consumer references the truncation behaviour, so the compiler needs no sweep beyond the one file.

## Guards (fail first)

Extend `GridPlacementSmoke.VerifyGridObjectivePanel` (`tests/GridPlacementSmoke.cs:3452`), which already builds a tracker + panel with `AutoRefresh = false` and asserts `SummaryText()`.

- **Assertion:** add more active-on-start objectives than the cap — e.g. 8 `GridObjectiveDefinition`s with `ActiveOnStart = true` against the default `MaxVisibleObjectives = 6` — then assert `panel.SummaryText() == "Goals 8 | Done 0"` **and** `panel.VisibleObjectiveRowCount() == 6` (rows still capped). Complete one and assert `SummaryText() == "Goals 8 | Done 1"` (the completed goal counted even though it sits beyond the six drawn rows, if ordered past them).
- **Mutation that trips it:** restore the `break` at `:173-174`. `VisibleObjectives()` returns 6, the summary reads `Goals 6 | Done 0`, and the assertion fails. The row-count assertion (`== 6`) passes in both states, proving the fix changed only the count, not the rows drawn — so the guard fails on exactly the defect and not on an incidental row-rendering change.

This is a deterministic HUD assertion, not a terrain-generation change, so it does not touch `tests/terrain_generation_baseline_probe.gd`; the existing `GridPlacementSmoke` harness is the right home.

## Dependencies / collisions

- **FIX-09** (objective tracker `RestoreState` emits no signals) touches the same panel's refresh path from the tracker side; independent of this count fix but both land in the objective HUD — sequence them so the FIX-09 restore probe and this count probe are both present in `VerifyGridObjectivePanel`.
- No collision with the streaming group (ENH-03/04, the concurrent `TerrainWorldComponent` session); this is a self-contained grid-UI file.
- DUP-05 / ENH-01 / ENH-02 / DUP-13 / ENH-12 do not touch `ecs/grid/ui/`.

## Out of scope

- Row ordering, sorting, or which goals win the visible slots when more than `MaxVisibleObjectives` exist — the drawn set is unchanged (first-N in enumeration order).
- Any signal-on-restore behaviour (that is FIX-09).
- `HideCompleted` / `HiddenUntilActive` filter semantics — the fix counts exactly the set those filters already admit, only without the cap.
- The base `GridListPanelComponent.UpdateRows` cap and the worker/job-board panels that share it — they compute their summaries from their own full sets already and are unaffected.
