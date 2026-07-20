# Phase 6 — Atmosphere polish

## Why

The atmosphere pass verified **all six** of round-1's atmosphere fixes are correct (day/night gate,
`DefaultSeason`, camera-follow emitter, cloud/shadow order, bus removal, fog reuse) — but the
camera-follow emitter change had one visible side-effect, and a few pre-existing polish items remain.

## The work

1. **Slow precipitation only covers the top band** — `ecs/atmosphere/WeatherSystemComponent.cs:344,329`
   — round-1's `PositionEmitterAtCamera` pins the thin emission strip (`EmissionRectExtents.Y = 8`) to
   the **top** of the view (`center.Y - _lastEmitSize.Y*0.5`). With the default
   `CpuParticles2D.Lifetime = 1.0`, slow types don't cross the viewport before dying: Snow (`Gravity 50`,
   `vel 60`) falls ~85px/s, LeafFall (`Gravity 30`, `vel 40`) even less — so they render only near the
   top edge. Rain/Storm/Hail (gravity 600–1200) are fine. Fix: set a per-type `Lifetime` long enough
   to cross the viewport for the slow types (viewport-height / fall-speed, + margin), or emit over a
   taller rect. **This is the side-effect of round-1's fix — verify the fast types still look right
   after the change.**
2. **Lightning flash z-order** — `ecs/atmosphere/WeatherSystemComponent.cs:296` — the `_flashOverlay`
   is added first (sibling index 0), so the cloud/shadow overlays draw **over** the full-screen
   white-out. A lightning flash should be topmost. Fix: `overlayRoot.MoveChild(_flashOverlay, -1)`
   after the cloud overlays are built (or build the flash last). Pre-existing; not from the reorder.
3. **Dead tween field** — `ecs/atmosphere/WeatherSystemComponent.cs:201,690` —
   `_weatherTransitionTween` is declared and `?.Kill()`-ed in `_ExitTree` but **never assigned**
   (`TransitionTo` uses local `CreateTween()`). Remove the field (or wire it) — misleading dead code.
4. **Weather-audio tween churn** — `ecs/atmosphere/WeatherAudioController.cs:157,223` —
   `IntensityChanged` fires every frame while `_intensityCurrent` eases (~40 frames/transition), and
   each `SetWeatherIntensity` kills+recreates a 0.5s fade tween for up to 5 players → ~200 tweens per
   transition + laggy mid-transition volume. Converges correctly but churns. Fix: set `volume_db`
   directly (or a short fixed-step tween) when the change source is a per-frame intensity update;
   reserve the 0.5s fade for discrete weather changes.
5. **`SeasonChanged` doc** — `ecs/atmosphere/SeasonalComponent.cs:50` — emitted, no in-tree consumer
   (weather pulls `CurrentSeason` via property). Unlike DayNight's signals it isn't documented as an
   intentional hook. Add a one-line doc comment ("developer-facing hook; framework ships no consumer").

## Gotchas

- The per-type `Lifetime` fix must not make fast precipitation (rain/hail) linger too long (particles
  piling up) — scale `Lifetime` inversely to fall speed, don't set one global large value.
- Flash z-order: `MoveChild(-1)` is "to the end" (topmost in a CanvasLayer's draw order) — confirm the
  flash renders **above** clouds but the fog/vignette relationship is still what you want.

## Verify

1. Build + validator.
2. Editor: set weather to Snow in a scrolling level → flakes fall across the **whole** viewport, not
   just the top. Switch to Rain → still looks right (not lingering).
3. Trigger lightning → the white flash covers everything, clouds included.
4. Rapidly change weather → no audible volume lag / no tween-count spike (profile if unsure).
