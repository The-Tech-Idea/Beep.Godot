# FIX-09 — Objective restore emits no signals: signal-driven HUDs stay stale after load

**Type:** fix · **Area:** `GridObjectiveTrackerComponent` (fix), `GridObjectivePanelComponent` (consumer/guard), pattern from `GridCalendarComponent` · **Status:** **PROPOSED 2026-09-11** · **Effort:** S (½ day) · **Risk:** low

## Finding

`GridObjectiveTrackerComponent.RestoreState` rewrites every objective's persisted fields but emits none of the tracker's three signals, so a signal-driven consumer never learns the state changed. A loaded save leaves the HUD showing pre-load objective progress.

1. **The tracker mutates silently on restore.** `RestoreState` (`GridObjectiveTrackerComponent.cs:172-195`) loops the saved objective array and assigns `objective.Progress` (`:189`), `objective.Active` (`:190`) and `objective.Completed` (`:191`) directly on the internal `ObjectiveState`, with no `EmitSignal`. Every *mutator* in this class pairs its state change with a signal — `SetObjectiveActive` emits `ObjectiveActivated` (`:57`), `SetProgress` emits `ObjectiveProgressChanged` (`:87`), `CompleteObjective` emits both `ObjectiveProgressChanged` and `ObjectiveCompleted` (`:107-108`), `ResetObjective` emits both (`:122-123`). Restore is the one state-writing path that skips this, so a consumer connected to the signals (`ObjectiveActivated(id, active)` `:15`, `ObjectiveProgressChanged(id, progress, target)` `:16`, `ObjectiveCompleted(id)` `:17`) sees nothing when a save is loaded.

2. **The panel is designed to be driven signal-first, so it goes stale.** `GridObjectivePanelComponent` repaints only from the three tracker handlers — `OnObjectiveActivated`/`OnObjectiveProgressChanged`/`OnObjectiveCompleted`, each `=> RefreshPanel()` (`GridObjectivePanelComponent.cs:214-216`) — which it connects in `ConnectTrackerSignals` (`:191-202`). None of them fire on restore. The `AutoRefresh` timer is documented as "only the safety net… objective changes arrive through the tracker's signals anyway" (`:22-27`), and when `AutoRefresh` is off the panel's `_Process` early-returns (`:61-62`) and `_Ready` never even starts processing (`SetProcess(AutoRefresh || Engine.IsEditorHint())`, `:50`). Consequences by mode:
   - `AutoRefresh` **on** (default): the panel corrects itself only on the next `RefreshIntervalSeconds` tick (default 0.5s, `:27`) — a visible stale flash of the pre-load numbers.
   - `AutoRefresh` **off** (the panel's own documented signal-only mode): the panel never repaints and shows pre-load objective progress indefinitely.
   This bites the mid-session reload case: the panel is already resolved and connected (`ResolveReferences` reconnects on `:180-189`), so an emit on restore would reach it. First-load at startup is separately covered by the deferred `RebuildPanel` in `_Ready` (`:48`), which reads current state directly — so this fix is specifically about restore reaching an already-connected consumer.

3. **The sibling calendar already treats this exact ordering hazard as a bug it must emit its way out of.** `GridCalendarComponent.RestoreState` deliberately re-emits after applying restored values: `EmitSignal(SignalName.DayAdvanced, DayOfSeason, (int)Season, Year)` (`GridCalendarComponent.cs:110`) with the comment "the restored date has to reach the HUD's labels" (`:109`), mirroring the same re-emit in `SetDate` (`:85`). The objective tracker is the outlier that does not follow the pattern its own neighbour established.

## Design

Re-emit the tracker's existing signals per restored objective, at the end of `RestoreState`'s loop body, mirroring `GridCalendarComponent.RestoreState`. One owner per fact is preserved: the signals stay the tracker's, the panel stays a pure consumer, and no new state or second notification path is introduced.

- After the three assignments in `RestoreState` (`GridObjectiveTrackerComponent.cs:189-193`), for each restored objective emit:
  - `EmitSignal(SignalName.ObjectiveProgressChanged, id, objective.Progress, target)` — `target` is already in hand at `:188`;
  - `EmitSignal(SignalName.ObjectiveActivated, id, objective.Active)`;
  - `EmitSignal(SignalName.ObjectiveCompleted, id)` when `objective.Completed`.
- Emit only inside the loop, i.e. only for objectives that matched a live `ObjectiveState` (`StateFor(id) != null`, `:184-186`) and were actually written — an unknown saved id is `continue`d and must not emit.
- Order mirrors the mutators: progress first (carries the target the row prints), then active, then completed — the same fields `RefreshPanel` reads back through `IsComplete`/`GetProgress`/`GetTarget`. Because each panel handler simply calls `RefreshPanel()` (idempotent full repaint), emitting all three per objective is safe and self-consistent; there is no partial-paint window.

This is the whole change — no signature change, no save-format change (`CaptureState` `:152-170` is untouched), no new API surface. Every emitted signal already has its real consumer in the tree (`GridObjectivePanelComponent`, and `GridObjectiveEventBinder` verified in the smoke), so rule 6 is satisfied without adding anything.

## Guards (fail first)

Add to `tests/GridPlacementSmoke.cs`, alongside `VerifyGridObjectivePanel` (`:3452`). Two guards, each mutation-proven by reverting the `RestoreState` emits.

- **End-to-end, all-public (primary).** Build a tracker with two active auto-complete objectives A (target 3) and B (target 1) and a `GridObjectivePanelComponent` with `AutoRefresh = false`, `HideCompleted = true`, `GenerateControlsWhenPathsEmpty = true` (same wiring as the existing panel smoke, `:3484-3493`). Render once — assert `panel.VisibleObjectiveRowCount() == 2` (both active). Capture a snapshot from a second tracker in which A is completed (`AddProgress("a", 3)` then `CaptureState()`), then call `panel`'s tracker `RestoreState(snapshot)` **without** calling `RefreshPanel()` afterward. Assert `panel.VisibleObjectiveRowCount() == 1` — the completed A dropped out because the restore emit drove a repaint. `VisibleObjectiveRowCount()` returns `RowCount` (`:149`) and does **not** self-refresh, so it reflects only what a signal actually painted. **Mutation:** remove the emits from `RestoreState` → no signal → panel never repaints → count stays 2 → fails. (Contrast: reading through `SummaryText`/`TextForObjective` would mask the bug, since both call `RefreshPanel` internally, `:126-135` — the guard must use the non-refreshing `VisibleObjectiveRowCount`.)

- **Direct signal assertion (unit-level).** Connect a recorder to the tracker's `ObjectiveProgressChanged`, `ObjectiveActivated` and `ObjectiveCompleted`, capture a snapshot at progress 3/completed, `ResetObjective`, then `RestoreState(snapshot)`; assert the recorder observed `ObjectiveProgressChanged("a", 3, 3)` and `ObjectiveCompleted("a")`. **Mutation:** remove the emits → recorder empty → fails. This pins the contract at the tracker directly, independent of any panel.

Both run headless under the existing `GridPlacementSmoke` entry (`:63-64` already invoke `VerifyGridObjectiveTracker`/`VerifyGridObjectivePanel`). No determinism/baseline probe is involved — this is a UI-signal fix, not a terrain-generation change.

## Dependencies / collisions

- **Precedent, not a dependency:** `GridCalendarComponent.RestoreState` (`:101-111`) is the pattern this copies; it is already implemented and needs no change.
- **FIX-11 (calendar HUD signal reconnect)** is the sibling in the same save/HUD-staleness family. It is a *different* defect (the calendar HUD fails to reconnect signals after a node re-resolve) and lands in a different file; note that `GridObjectivePanelComponent` already reconnects correctly in `ResolveReferences` (`:180-189`), so FIX-09 does not need to touch the reconnect path — the two fixes are complementary, not overlapping.
- **FIX-10 (objective panel goal-count undercount)** touches the same panel file (`GridObjectivePanelComponent.cs`) but a disjoint region (`VisibleObjectives`/`RefreshPanel` summing, `:151-178`/`:105-113`). No line overlap; land order does not matter.
- **ENH-02 (gameplay-vs-terrain edit classification, DONE)** is conceptually adjacent — a restore must re-notify its listeners — but shares no code with this grid-UI change.
- **No concurrent-session collision.** The other in-flight session owns `TerrainWorldComponent`/streaming/archive; this fix is entirely within the objective tracker and its HUD panel.

## Out of scope

- Changing the save format or `CaptureState` (`:152-170`) — untouched.
- Changing the `AutoRefresh` default or removing the safety-net timer — the fix makes the signal path correct so the timer stays a genuine fallback, not a crutch.
- FIX-10's goal-count undercount in the panel summary — separate finding, separate fix.
- The calendar HUD reconnect gap (FIX-11) — separate file, separate defect.
- Any new signal, save key, or public method — the fix reuses the tracker's three existing signals only.
