# GridWorkerBuildActivityEffectComponent

The worker-side half of the construction-in-progress effect family: while a sibling `IWorker` is actively working a job whose kind is in `ActiveJobKinds`, periodically triggers a sibling `ParticleComponent` burst and/or `SpriteEffectComponent` play — hammering sparks, dust, a welding flash, whatever the unit's own effect scenes are. Neither sibling effect component is required; wire either, both, or neither (a configuration warning fires if neither is present).

Deliberately does not assume a human: the sibling worker is found via `IWorker` (or, for a GDScript worker, the same duck-typed shape `GridWorkerPorts` reads for the port contracts) — a crane, a robot, or a drone works exactly like `GridWorkerComponent` here, differing only in which effect scenes are wired to its own `ParticleComponent`/`SpriteEffectComponent`. `ActiveJobKinds` defaults to `["build"]` but is not hardcoded to it — the same component works for a gather/mine "in progress" effect on the same unit.

## Public API
- Exports: `JobQueuePath` (`NodePath`), `ActiveJobKinds` (`Array<string>`, default `["build"]`), `BurstIntervalSeconds`.
- `void Tick(double delta)` — public so an orchestrator can drive it directly, mirroring `GridWorkerComponent.Tick`.
- `bool IsEffectActive { get; }` — whether the effect is currently considered active.

## Dependencies
Reuses `ParticleComponent`/`SpriteEffectComponent` (`ecs/`) rather than reimplementing scene-instantiation — this component is a thin orchestrator, not a third particle system. Finds its sibling `IWorker` (or duck-typed equivalent) and `GridJobQueueComponent` (for job-kind lookup, via `NodePath` or scene-wide fallback).

## Notes
- Attach beside `IWorker` on the worker UNIT scene (not beside `GridBuildSiteComponent` — that's the structure-side effects' attachment point).
- Two `ParticleComponent` exports matter for a repeating effect on a moving worker, and both default the wrong way for it: `FollowParent` (default off — `ParticleComponent` is a plain `Node`, so its emitter has no canvas parent and sits at world (0,0) unless it follows) and `AutoQueueFree` (default on — the emitter is freed when the first burst finishes, after which every later `Burst()` is a silent no-op and the effect fires exactly once). This component does not override a developer's exports, but it warns once, naming the sibling, when `AutoQueueFree` is on. Set both on the `ParticleComponent`, as `templates/scenes/construction_demo.gd` does.
- The shipped `dust_puff.tscn`/`simple_burst_particles.tscn` use a 512px texture at scales meant for a bigger world; at 64px cells either one swallows the building. `templates/particles/construction_dust.tscn` is the same puff sized for a grid builder.
- One of the independent, optional components in the construction-in-progress effect family.
