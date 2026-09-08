# DUP-06 — Retire the per-file `Dict*` wrappers; remove the calendar's dead helpers

**Type:** duplication fix / hygiene · **Area:** `GridCellDataComponent`, `GridJobQueueComponent`, `GridObjectComponent`, `GridRoadComponent`, `GridWorldStateComponent`, `GridCalendarComponent`, `ui/GridJobBoardComponent` · **Status:** proposed 2026-09-08 · **Effort:** XS (½ day) · **Risk:** none

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
