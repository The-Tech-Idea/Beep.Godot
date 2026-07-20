# Phase 3 — Behavioral & silent-failure tail

## The work

1. **MovingPlatform Once can't restart** (**Decision A**) — `ecs/MovingPlatformComponent.cs:53,125` —
   after a Once-mode run completes, `Start()` re-emits `RunCompleted` and doesn't move: `_target` is
   pinned at `_points.Length-1`, so `_PhysicsProcess` treats the current position as "arrived", re-runs
   `AdvanceTarget()` → `_running=false; EmitSignal(RunCompleted)` again. The doc says "Begin (or
   resume)". Fix per Decision A: **(recommended)** on `Start()`, if Once and at the end, reset
   `_target=1, _forward=true` so it re-runs; OR document Once as single-shot (Start = no-op after
   completion) and guard the re-emit.
2. **Runtime `EnableClouds` toggle flips flash z-order** — `ecs/atmosphere/WeatherSystemComponent.Overlays.cs:245-292`
   (via the setter at `WeatherSystemComponent.cs:163`) — turning `EnableClouds` false→true at runtime
   calls `EnsureCloudOverlays`, which `AddChild`s the clouds **after** the existing `WeatherFlash`,
   leaving the flash below the clouds (the build path fixed this with `MoveChild(_flashOverlay,-1)`, but
   the setter doesn't re-run it). Fix: after `EnsureCloudOverlays(...)` in the setter, re-issue
   `MoveChild(_flashOverlay, -1)` when the flash is valid. (Extract a tiny `RaiseFlashToTop()` helper
   shared by the build path and the setter.) Low severity — only if code toggles clouds on post-init.
3. **Quest NREs on a null objective entry** — `ecs/QuestComponent.cs:54` — `Objectives[i].TargetId` /
   `.RequiredCount` dereference array entries with no null check; an authored `Objectives` array with an
   empty slot NREs in `ProgressObjective`/`IsObjectiveComplete`. Fix: skip null entries in
   `EnsureCounts` and the loops.
4. **Knockback ignores `!IsActive`** — `ecs/KnockbackComponent.cs:63` — `_PhysicsProcess` gates on
   `_body==null || _remaining<=0` but not `!IsActive`; a knockback in flight keeps integrating (and
   driving `MoveAndSlide` in the owns-integration path) after deactivation. Add the `!IsActive` guard
   for consistency. (The `:79` "instant-set controller overwrites the impulse" note is a documented
   limitation — add a one-line doc comment; don't rework controller integration.)
5. **Turret warns in the editor** — `ecs/TurretComponent.cs:42` — the `ProjectileScene == null` warning
   isn't wrapped in `!Engine.IsEditorHint()`, so it fires at design time before the developer wires the
   scene (Spawner gates its equivalent). Wrap it.
6. **Interactable input error before generation** — `ecs/InteractableComponent.cs:81` —
   `@event.IsActionPressed(InputAction)` runs with no `InputMap.HasAction(InputAction)` guard, so a
   template run before the input map is generated logs a per-event error. AreaTrigger subclasses can't
   reach `ControllerComponent.InputActionsAvailable`, so guard inline with `InputMap.HasAction(InputAction)`.

## Gotchas

- MovingPlatform reset (Decision A recommended path): resetting `_target=1` mid-`Start()` must also
  clear the "at end" state so `RunCompleted` doesn't immediately re-fire; test a full Once→Start→Once cycle.
- The `RaiseFlashToTop()` helper must no-op safely when `_flashOverlay` is null or clouds are disabled.
- Quest: a null objective slot should be *skipped*, not counted — make sure `_counts` indexing stays
  aligned with `Objectives` (index by position, tolerate null).

## Verify

1. Build + validator.
2. Editor: a Once platform → completes → `Start()` → re-runs (or is a documented no-op, per Decision A);
   `RunCompleted` fires the right number of times.
3. Toggle `EnableClouds` on at runtime, trigger lightning → flash still covers the clouds.
4. A Quest with a null objective slot → no NRE on progress.
5. Run `player_template`/an interactable before generating a project → no per-event input errors;
   a turret with no ProjectileScene → no editor-time warning, warns only at runtime.
