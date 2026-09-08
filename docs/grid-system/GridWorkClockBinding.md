# GridWorkClockBinding

Internal sealed class: binds a component to the grid's work clock and reports whether it found one. Held **by composition** rather than inherited, because the components that need it do not share a base — `GridProductionComponent` is a plain `Node`; `GridWorkerComponent` and `GridExtractorComponent` are not.

Every timed grid subsystem needs the same two lines — subscribe to `WorkTick`, and self-tick when there is nothing to subscribe to — and five copies of that is exactly the duplication this codebase treats as a defect. The standalone fallback is what keeps template scenes and every headless probe running with no clock in the tree at all.

## Public API
- `bool FollowsClock` — true when a work clock is driving the owner; false means the owner must advance itself from its own frame delta.
- `bool Bind(Node owner, NodePath path, GridWorkClockComponent.WorkTickEventHandler onTick)` — finds the work clock (exported path first, then scene-wide, through `EntityComponent.Resolve`) and routes its `WorkTick` to the handler, which receives **turns**. Returns whether a clock was found so the caller can decide to self-tick; nothing is inferred from silence.
- `void Unbind()` — disconnects; safe to call with nothing bound.
- `static float TurnsForDelta(double delta)` — turns for a frame's delta in the self-ticking fallback: one turn per second, with a single huge frame clamped the way every other delta consumer in this addon clamps one.

## Dependencies
[GridWorkClockComponent](GridWorkClockComponent.md) (the signal), `EntityComponent.Resolve` (the lookup). Held by `GridProductionComponent`, `GridExtractorComponent`, `GridTransportChainComponent`, `GridHaulerComponent`.

## Notes
- The typical owner shape: `bool bound = _workClock.Bind(this, WorkClockPath, AdvanceWork); SetProcess(!bound);` in `_Ready`, `_workClock.Unbind()` in `_ExitTree`, and a `_Process` that only calls `AdvanceWork(GridWorkClockBinding.TurnsForDelta(delta))` — reached solely when no clock was found.
- `GridWorkerComponent` does not use this helper: its `_Process` must keep running on both axes for movement and claim polling, so only its work loop is clock-driven. It carries its own two-line bind for that reason, which is documented at the site.
