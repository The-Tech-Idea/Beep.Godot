# Phase 3 — Behavioral correctness

## Why

Components that run but do the **wrong thing**: signals that spam every frame instead of
firing on state edges (any listener that plays a sound or starts an animation retriggers at
60 Hz), configuration that doesn't configure, and one shipped template whose NPC physically
cannot move.

## The work

### Edge-emission bugs (emit on transition, not continuously)

1. **TopDownController** — `ecs/TopDownController.cs:57` — `Stopped` emits every physics
   frame while at rest. `MovementComponent` fixed this exact bug with an `_isMoving` edge
   guard — copy the pattern (`_wasMoving` flag, emit on the moving→stopped transition).
2. **FollowTargetComponent** — `ecs/FollowTargetComponent.cs:42-46,57-58` — with a null
   target, `TargetLost` emits **every frame**; while at the target, `TargetReached` emits
   every frame. Both need transition guards.
3. **HungerStaminaComponent** — `ecs/HungerStaminaComponent.cs:81-83` —
   `HungerChanged`/`ThirstChanged`/`StaminaChanged` emit every frame unconditionally.
   Emit only when the clamped value changed.

### Configuration that doesn't configure

4. **MovingPlatformComponent** — `ecs/MovingPlatformComponent.cs:19,41-45,61` —
   `AutoStart=false` platforms move anyway (`_PhysicsProcess` gates only on `!_paused`), and
   there's no `Start()`/`Stop()`. Add a `_running` field initialized from `AutoStart`, gate
   movement on it, expose `Start()`/`Stop()`.
5. **SeasonalComponent** — `ecs/atmosphere/SeasonalComponent.cs:63-70` — the generator writes
   `GameInfo.DefaultSeason` (`core/BeepGenreGenerator.cs:200-206`) but the component never
   reads it — every game starts in Spring. Read it in `_Ready` alongside the existing
   `EnableSeasons` read and recompute `_currentSeasonColor`.
6. **DayNightCycleComponent** — `ecs/atmosphere/DayNightCycleComponent.cs:76-80,101` —
   `Init()` calls `Apply()` unconditionally, seeding a stale one-time `day_night` tint into
   `AmbientController` even when `IsActive == false` (which is every shipped genre). Gate the
   seed on `IsActive`.
7. **SettingsComponent** — `ecs/ui/SettingsComponent.cs:73-78` — the `Language` setter
   persists + emits but never calls `ApplyLocaleSettings`, unlike `Fullscreen`/`ResolutionIndex`
   which self-apply; setting it directly changes nothing (works today only because
   `BootComponent`/`SettingsMenu` apply manually). Apply in the setter.
8. **ChromaticAberrationComponent** — `ecs/ui/ChromaticAberrationComponent.cs:42-46` —
   `Strength` reaches the shader only under `Engine.IsEditorHint()`; runtime changes are
   ignored. Push from a property setter (or drop the editor-only guard).
9. **RatingComponent** — `ecs/ui/RatingComponent.cs:88` — programmatic `SetValue` doesn't
   emit `RatingChanged` (interactive click does, `:61`). Emit for symmetry.
10. **MarqueeComponent** — `ecs/ui/MarqueeComponent.cs:44` — `AutoStart=false` leaves the
    marquee permanently inert: no public `Start()`/`Stop()` exists. Add them.
11. **CounterComponent** — `ecs/ui/CounterComponent.cs:41` — writes the label with
    `Format="N0"` (thousands separators) then re-parses it with plain `float.TryParse`,
    which fails on "1,000" → the next `CountTo` silently restarts from 0. Cache the numeric
    value (or parse with `NumberStyles.Any`, invariant culture).

### Resource accounting

12. **ObjectPoolComponent** — `ecs/ObjectPoolComponent.cs:61-67` — `Release()` doesn't check
    the instance isn't already pooled; a double-`Release` hands the same node to two `Get()`
    callers. Ignore instances already in `_pool`.
13. **TurretComponent** — `ecs/TurretComponent.cs:109` — pulls projectiles from the pool, but
    projectiles `QueueFree()` themselves and nothing calls `Release()` — the pool silently
    degrades to per-shot `Instantiate` after `MaxSize` shots. Either have pooled projectiles
    release back on expiry/hit, or don't route self-freeing projectiles through a pool.
    (This is a design seam with `ProjectileComponent`; pick one contract and document it.)

### The inert NPC template — **Decision D2**

14. **robot_npc_template.tscn** — `:26,32` — `Movement` (`ReadInput=false`, no controller,
    nothing calls `Move()`) and `Aggro` (only consumer is `AIController`, absent here) are
    both inert; the robot can never chase, flee, or move — and neither component warns,
    because each is individually "validly configured". Options:
    - **Wandering NPC:** add `AIController` (`Mode=Wander` or `Patrol` — it owns
      `MoveAndSlide` and consumes `AggroComponent`) and **remove** the standalone
      `MovementComponent` (they fight over `MoveAndSlide`).
    - **Static talk NPC:** drop `Movement`+`Aggro`, keep `Health` + `Interactable`.
    - Either way: if it can become hostile, `Health` needs an `EntityGroup` so it's targetable.

## Gotchas

- The `Stopped`/`TargetReached` fixes change observable signal cadence — anything a
  developer connected keeps working, just stops being spammed; note it in the commit message.
- `SeasonalComponent` must recompute its cached season color **after** overriding
  `CurrentSeason`, or the first ambient contribution uses the Spring color anyway.
- Turret/pool: don't "fix" by making `ProjectileComponent` never free itself — non-pooled
  spawners rely on self-freeing.

## Verify

1. Build + validator.
2. Editor: `topdown_main` idle → connect a print to `Stopped` → fires once, not continuously.
3. A `MovingPlatformComponent` with `AutoStart=false` → stationary until `Start()`.
4. Set a genre's `default_season` to `"winter"`, generate, run → winter tint on frame 1.
5. Robot NPC (per D2): either visibly wanders/patrols, or is a clean static interactable.
