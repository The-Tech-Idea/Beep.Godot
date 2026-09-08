# DUP-11 — `GridDispatchBoardComponent` is a second dispatch loop

**Type:** duplication (triage — owner's call) · **Area:** `GridDispatchBoardComponent`, `GridDispatchTaskDefinition`, `GridWorkerDispatchComponent`, `GridJobQueueComponent.Dispatch` · **Status:** proposed 2026-09-08 · **Effort:** S (½ day to relocate; M if merged) · **Risk:** low

## Finding

The grid has one real dispatch path: `GridJobQueueComponent` (+`.Dispatch`, `.Reservations`) → live `GridWorkerComponent` or dormant `GridWorkerDispatchComponent`/`GridActorTravelComponent`/`GridJobExecutionComponent`, all measured in **turns** on the work clock.

`GridDispatchBoardComponent` runs a parallel loop: `GridDispatchTaskDefinition` rows with `TravelSeconds`/`WorkSeconds`, a `Tween` per task that moves a unit sprite to the cell and back, and its own completion signal — none of it touching the job queue, reservations, chunk pins or the work clock. The master tracker already records the decision that its two exports "stay seconds because they drive a real-time showcase tween; a scan pin forbids any other grid file measuring work in seconds".

So it is *known* to be a showcase. The duplication risk is location and naming: it lives in `ecs/grid/` beside the real components, is `[GlobalClass]` and `[Tool]`, and a developer picking components from the Inspector sees two dispatchers.

## Options

1. **Relocate (recommended).** Move to `templates/scenes/grid/showcase/` (or `ecs/showcase/`) with the scenes that use it; rename `GridDispatchShowcaseComponent`; keep the seconds pin. Nothing in shipped gameplay references it (scene scan pending — record the result in the tracker before moving).
2. **Merge.** Re-implement the board as a *view* over `GridJobQueueComponent` (a job = a task; the tween follows the real worker). Only worth it if the showcase is meant to become a product feature.
3. **Leave as is** with a doc-comment banner. Weakest — the discoverability problem stays.

Removal is not proposed: the component is authored into showcase scenes and demonstrates the dispatch *shape* to developers.

## Guards

- If relocated: pin that no `ecs/grid/` file declares `TravelSeconds`/`WorkSeconds` (the existing seconds pin becomes unconditional).
- If merged: the real dispatch probes cover it.

## Dependencies / collisions

`ecs/grid/` collision — trivial. Decision needed from the owner before any move.

## Out of scope

The dispatch algorithm itself.
