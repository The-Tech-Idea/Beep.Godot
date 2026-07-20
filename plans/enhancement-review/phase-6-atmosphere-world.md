# Phase 6 — Atmosphere & world polish

## Why

The atmosphere subsystem's wiring is fundamentally sound (the flag trace from genre.json
tuning → generator → `GameInfo` → components checks out at every hop, and the six
weather-enabled genres correctly instance `atmosphere.tscn` while the four weather-off ones
don't). What remains: one visible-in-every-scrolling-level rendering gap, a draw-order bug,
two features that ship computed-but-unconsumed, and consistency nits.

The two atmosphere *bugs* (day/night `Init()` seed, `DefaultSeason` unread) are in
**Phase 3** — this phase is the polish and the seam documentation.

## The work

### Rendering

1. **Precipitation doesn't follow the camera** — `ecs/atmosphere/WeatherSystemComponent.cs:317-335`
   — the emitter is created on the world-root parent at origin, viewport-sized, with
   `LocalCoords = true`, and is never repositioned — in a scrolling level, rain/snow/hail
   falls only near world origin. The cloud/fog overlays already solve this
   (`CameraOffsetInScreens()`); apply the same offset to the emitter each frame.
2. **Cloud shadows draw above clouds** — `ecs/atmosphere/WeatherSystemComponent.Overlays.cs:245-286`
   — `_cloudShadowOverlay` is added after `_cloudOverlay`, contradicting the line-267
   comment ("sits below the clouds"). Add the shadow overlay first (or set explicit ordering).

### Ships-dark / computed-but-unconsumed features

3. **Day/night ships dark in all 10 genres** (**Decision D6**) — `enable_day_night` is
   `false` in every `genre.json`, so `DayNightCycleComponent`'s visual never runs anywhere
   shipped (its clock still advances to feed seasons — deliberate, but undocumented).
   Either enable it in one showcase genre (survival is the natural fit) or add the doc note
   explaining the clock-only default and how to turn the visual on.
4. **`TimeOfDayChanged` / `PhaseChanged` have no consumer** —
   `ecs/atmosphere/DayNightCycleComponent.cs:99,111,156` — no HUD shows time-of-day. Cheap,
   coherent fix: `WeatherHUDComponent` (which already consumes `WeatherChanged`/`SeasonChanged`)
   gains an optional clock/phase label. Otherwise: doc-comment the seam.
5. **Foliage wind is inert** — `ecs/atmosphere/SeasonalComponent.cs:145-162` —
   `GetWindSpeedForSeason`/`FoliageWindStrength`/`GetFoliageWindParams()` are computed and
   nothing reads them; no foliage shader is wired. Either publish a global shader param
   (mirroring the existing `beep_wind_*` publish in the weather system) or doc-comment it as
   a pull-only developer seam.

### Consistency / hygiene

6. **WeatherAudioController ignores the enable flag** — `ecs/atmosphere/WeatherAudioController.cs:55-76`
   — unlike its siblings it never gates on `GameInfo.EnableWeather`; dropped into a
   weather-off scene it builds a live bus + six players. Read the flag for consistency.
7. **"Weather" audio bus persists** — `WeatherAudioController.cs:78-91` — the bus is added
   to `AudioServer` and never removed on `_ExitTree` (no duplicates thanks to the
   `GetBusIndex` guard, but it outlives the run into menus). Remove on exit, or comment why not.
8. **DynamicFogLayer material churn** — `ecs/atmosphere/DynamicFogLayer.cs:87-120` —
   `EnsureFogLayer` news a fresh `Shader`+`ShaderMaterial` even when it re-finds an existing
   `FogOverlay`. Single-call today; reuse the found node's material to make it re-entrant.
   (Its `DefaultNoise()` fallback at `:196` is the correct black-sampler fix — keep.)
9. **Silent shake miss** — `WeatherSystemComponent.cs:632` — `TriggerCameraShake` resolves by
   node name then `screen_shake` group; renamed + ungrouped → silent no-op. One `PushWarning`.

## Gotchas

- The emitter fix must respect `LocalCoords = true` semantics: offset the emitter node, not
  the particles' emission box, or trails will teleport on fast pans.
- If D6 enables day/night in survival, re-check `SeasonalComponent` interplay — the clock
  currently advances regardless; enabling the visual must not double-apply the ambient tint
  (both write through `AmbientController` contributions).

## Verify

1. Build + validator.
2. Editor: `survival_main` (or any weather genre), set weather to rain, walk the camera far
   from origin → rain still falls across the whole viewport.
3. Storm with cloud shadows visible → dapple renders *under* the cloud layer.
4. Quit to main menu after a run → no stray "Weather" bus (if 7 is taken as remove-on-exit).
