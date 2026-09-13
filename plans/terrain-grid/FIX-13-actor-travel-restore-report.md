# FIX-13 — A rejected actor-travel restore loads nothing and reports nothing

**Type:** fix (silent restore failure) · **Area:** `GridActorTravelComponent.Load`/`RestoreState`, `GridNavigationComponent` (pending-request budget), `ActorRegistryComponent` (dormant-motion claim) · **Status:** **IMPLEMENTED 2026-09-11** · **Effort:** XS (½ day) · **Risk:** low

## Outcome (2026-09-11)

`Load` now reads `RestoreState`'s result and, on a rejection, pushes a warning naming the save key and the saved count and raises a new `TravelRestoreFailed(int savedCount)` signal — the same "a restore must reach its consumer" discipline FIX-09 applies to the objective tracker. `RestoreState` itself is unchanged: still all-or-nothing, still leaving the live routes untouched on a rejection. New probe `tests/actor_travel_restore_probe.gd` (registered in `run_actor_checks.ps1`) covers both halves: a saved route whose goal is now outside the shrunk navigation bounds is refused and reported with `savedCount == 1` while the live route survives, and a save one entry over the pending-request budget returns `false` with `TravellerCount` unchanged. Mutation-proven: restoring the bool-discarding `Load` leaves the signal unfired (`"got 0"`). Build clean (0 warnings); `actor_travel`, `job_execution`, `actor_residency` and `actor_reattachment` probes green.

Two notes from landing it:

- The probe builds its save through `GameStateData.FromJsonString` rather than assigning `GameData`: the `Dictionary<string, Variant>` property is not assignable from GDScript, and the JSON path also exercises the real save-parse route.
- `RestoreState` keeps its `bool`; `Load` is the `ISaveable` void method, so the signal plus warning is the reporting channel — the plan's preferred option when no caller branches on the reason.

## Finding

`GridActorTravelComponent.Load` throws away the success/failure result of the restore, so a save whose in-flight travellers cannot be restored comes back with those travellers **silently gone** and nothing told the caller.

```csharp
// GridActorTravelComponent.cs:207-210
public void Load(GameBuilder.GameStateData state)
{
    if (state.GameData.TryGetValue(SaveKey, out var saved) && GridVariantReader.TryDictionary(saved, out var data))
        RestoreState(data);        // <- bool return discarded
}
```

`RestoreState` (`:182-204`) is a two-phase restore: it validates **every** saved route up front, and only once all pass does it clear the live routes (`:200`) and re-begin them. Every *reachable* rejection therefore returns `false` **before** anything is cleared:

- `:184-185` — the whole save is refused when `state.Count` exceeds the navigation pending-request budget (`MaximumPendingRequests - PendingPathRequestCount + _requests.Count`).
- `:189` — a malformed entry (non-string key, or a value that is not a dictionary).
- `:194-197` — a route whose actor is no longer dormant, whose owner no longer matches, whose goal is now out of bounds (e.g. loaded into a **smaller** map), whose position is non-finite, that cannot claim dormant motion, or whose speed is not positive.

In all of these `RestoreState` returns `false` with the live routes **untouched** — which is the safe half. The unsafe half is that `Load` cannot tell: the saved travellers are dropped on the floor, no warning is pushed, no signal is raised, and the integrator sees units that were mid-journey at save time simply never arrive. This is exactly the failure the engine's own restore rule forbids — *"a failed restore is reported; do not silently convert a corrupt/incompatible save into a new game"* — and CLAUDE.md rule 2 (*never swallow; report the outcome in the signature where a caller has a decision to make*).

(The originally-reported "partial drop of in-flight travellers mid-loop" is **not** the defect: the `:202` `BeginTravel` failure branch is effectively unreachable because the `:184-197` precheck mirrors every `BeginTravel` guard and `RemoveRoute` cancels this component's own pending requests before the budget-bounded loop. The real, narrower issue is the discarded result, above.)

## Design

- Make the outcome visible to the caller. `Load` reads the `bool` and, on `false`, reports through the failure path rather than returning as if it succeeded: push a warning naming the save key and the count of routes that could not be restored, and — so a HUD or campaign layer can react — raise a `TravelRestoreFailed(int savedCount)` signal (the tracker/calendar components already re-emit on restore; this mirrors that).
- Prefer reporting in the signature: give `RestoreState` a richer result than a bare `bool` only if a caller will branch on the reason; otherwise the `bool` plus a logged reason is enough. Do **not** add a silent fallback that fabricates a partial restore — failing safe (live routes untouched) is correct; failing *silently* is the bug.
- Leave the all-or-nothing budget refusal (`:184-185`) as-is behaviourally, but include it in what gets reported: a save legitimately larger than the current pending-request budget is a real, nameable reason the restore was declined.

## Guards (fail first)

- **Probe** (`tests/actor_travel_restore_probe.gd`, headless): register two dormant actors, `BeginTravel` both, `CaptureState`, then load a state that must be rejected — e.g. a saved route whose `goal` is outside the current `GridNavigationComponent` bounds (the shrink-map case). Assert that `Load` surfaces the failure: the new `TravelRestoreFailed` signal fires with the saved count (or `Load` returns `false`). Mutation: revert `Load` to discard the bool → no signal / no report → the probe fails.
- **Smoke** assertion: `RestoreState` on a save one entry over the pending-request budget returns `false` **and** leaves the existing routes intact (count unchanged). Mutation: move the `:200` clear above the validation loop → the existing routes are wiped on a rejected restore → the "intact" assertion fails.

## Dependencies / collisions

Sits in the same restore-correctness family as **FIX-07** (requeue drops in-progress world-execution work) and **FIX-09** (objective restore emits no signals) — all three are "a load path that loses or hides state." No shared code, so they land independently. Touches `ecs/grid/GridActorTravelComponent.cs` only for the fix; the probe uses `ActorRegistryComponent` + `GridNavigationComponent`. No collision with the streaming/`TerrainWorldComponent` session.

## Out of scope

Redesigning the budget model (whether a save larger than the live pending-request budget should be admitted by draining the queue first) — that is a separate capacity question. Partial/best-effort restore (restoring the routes that pass and reporting the rest) — the current all-or-nothing contract is retained; only its silence is fixed.
