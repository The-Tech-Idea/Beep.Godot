# DUP-06 — Retire the per-file `Dict*` wrappers; remove the calendar's dead helpers

**Type:** duplication fix / hygiene · **Area:** `GridCellDataComponent`, `GridJobQueueComponent`, `GridObjectComponent`, `GridRoadComponent`, `GridWorldStateComponent`, `GridCalendarComponent`, `ui/GridJobBoardComponent` · **Status:** **IMPLEMENTED 2026-09-09** (Dict* wrappers retired; GridMath owns the numeric guards; the calendar's dead helpers removed) · **Effort:** XS (½ day) · **Risk:** none

## Outcome (Dict* wrappers)

The thirteen per-file `Dict*` wrappers are gone; every caller reads through `GridVariantReader` directly. The consolidation was pure hygiene: every wrapper was a forwarder, and the `DictString` copies the plan flagged as having a different null rule turned out to be byte-identical to `GridVariantReader.String` (`ContainsKey(key) ? value.AsString() : fallback`) - so there was no behaviour to change. Three of the thirteen were already dead (the cell store's `DictString` and `DictInt`, the calendar's `DictFloat`); they were dead duplicates of `GridVariantReader` and went with the rest.

Verified: `dotnet build` clean, zero warnings; seven probes across the job, economy, chunk save/load, topology and building systems green - a zero-behaviour-change refactor stays invisible to them. A scan pin forbids a private `DictString`/`DictInt`/`DictFloat`/`DictBool`/`DictVector2I` anywhere under `ecs/grid`, and the mutation (re-adding one) trips it.

**Outcome: the numeric guards (2026-09-09, on the owner's call).** `GridMath` (static, `ecs/grid/`) is the one owner of the small range guards now: `NonNegativeFinite`, `FinitePositive`, `DeltaSeconds`, `FiniteVector`, `PositiveVector` and `IsFinite(Vector2)`. `GridCameraControllerComponent` - the surviving user - reaches them through `using static Beep.ECS.GridMath;`, so its ~30 call sites are unchanged and its six private copies are gone. `IsFinite(Vector2)` (used at its line 141) moved with the family; the plan had missed it. The calendar's three dead copies (`DeltaSeconds`/`PositiveFinite`/`NonNegativeFinite`, orphaned since the turn-axis rewrite moved day progress to `GridWorkClockComponent`, no callers - verified) are deleted. A scan pin keeps each guard's definition to `GridMath.cs` and is mutation-proven (re-adding a `NonNegativeFinite` copy anywhere else fires it). Build clean; the view-grid (camera controller), game-clock-axes (calendar) and grid-playground probes are green. `terrain_lab_grid_probe` is a pre-existing red - its assertion is on `TerrainWorldComponent.BuiltSize`, the same unfinished-TerrainWorldComponent family that aborts the contract scan, and is untouched by this verbatim guard move.

## Finding

`GridVariantReader` (Phase 3) is the shared `Variant`→typed reader (`Int`, `Float`, `String`, `Vector2I`, `Bool`, `TryDictionary`, `TryReadCell`, …). Thirteen private wrappers still sit in front of it, some forwarding, some re-implementing:

| Wrapper | Files |
|---|---|
| `private static string DictString(` | `GridCellDataComponent`, `GridJobQueueComponent`, `GridObjectComponent`, `GridRoadComponent`, `GridWorldStateComponent`, `ui/GridJobBoardComponent` (6) — the job board's is `dict.ContainsKey(key) ? dict[key].AsString() : fallback`, a different null/empty rule from `GridVariantReader.String` |
| `private static int DictInt(` | `GridCalendarComponent`, `GridCellDataComponent`, `GridJobQueueComponent`, `ui/GridJobBoardComponent` (4) |
| `private static float DictFloat(` | `GridCalendarComponent`, `GridJobQueueComponent`, `GridRoadComponent` (3) |

`GridCalendarComponent.cs:466-476` additionally carries `DictFloat`, `DeltaSeconds`, `PositiveFinite`, `NonNegativeFinite` with **no callers** in the file since the turn-axis rewrite moved day progress to `GridWorkClockComponent`. Per rule 6 this is investigated, not deleted on sight: the four are pure numeric guards whose only purpose was the removed real-time day loop; `GridCameraControllerComponent` has its own identically named copies (`NonNegativeFinite`, `FinitePositive`, `DeltaSeconds` at 660-670) that *are* used. So the calendar's are orphans of a deleted feature, and the camera's are the surviving owner of the same guards.

## Design

- Delete the thirteen wrappers; call `GridVariantReader.*` directly (or `using static Beep.ECS.GridVariantReader;` where a file has many reads — the pattern `GridBuildDefinition` already uses with `GridDefinitionReader`).
- Where a wrapper's rule differed (job board `DictString`), adopt `GridVariantReader.String`'s rule and note it in the panel's page — a missing key and an empty value both fall back.
- Move the numeric guards to one owner: `GridMath` (static, `ecs/grid/`) with `NonNegativeFinite`, `FinitePositive`, `DeltaSeconds`, `FiniteVector`, `PositiveVector`; `GridCameraControllerComponent` calls it; the calendar's dead copies go (removal is the owner's call — this plan recommends it on the evidence above).

## Guards

- Pin: `private static string DictString(`, `DictInt(`, `DictFloat(`, `DictVector2I(`, `DictBool(` declared nowhere under `ecs/grid/`. Mutation: restore one → fails.
- Pin: `NonNegativeFinite(` declared only in `GridMath.cs`.
- Existing `runtime_smoke.ps1` job-board assertions cover the `DictString` rule change (job rows still render id/kind/state).

## Dependencies / collisions

`ecs/grid/` collision with the other session — mechanical, stage per file. Independent otherwise.

## Out of scope

`GridDefinitionReader` (dual-key definition reading) — already the one owner for that job.
