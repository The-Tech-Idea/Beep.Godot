# Phase 4 — Polish

## Why

The low-risk tail — a perf win, a comment/behavior mismatch, and small consistency nits. None crash;
do them last.

## The work

1. **Cloud shader runs while invisible** — `ecs/atmosphere/WeatherSystemComponent.Overlays.cs:299-351`
   — the cloud + shadow ColorRects always run their 5-octave FBM×2 fullscreen fragment shader even when
   `_cloudAlphaCurrent ≈ 0` (Clear weather) or when `EnableClouds` was turned off (modulate is zeroed
   but `Visible` stays true, so the fragment still executes). The doc calls this "the single most
   expensive thing the weather system does." Gate it: `_cloudOverlay.Visible = _cloudAlphaCurrent > 0.001f`
   (and the shadow likewise), and set `Visible=false` in the turning-off branch of the `EnableClouds`
   setter. Skips the fragment cost when nothing shows.
2. **Weather audio builds its bus when inactive** — `ecs/atmosphere/WeatherAudioController.cs:66-82` —
   `Setup()` is `CallDeferred` unconditionally, so with `IsActive==false` it still creates the "Weather"
   bus and all players (which `Play()` their loops at -80 dB and keep decoding), contradicting the
   comment that a weather-off scene gets "a silent, inactive controller". Fix: early-return `Setup()`
   when `!IsActive` (or soften the comment to match — early-return is better).
3. **MatchTimer blank until Start** — `ecs/ui/MatchTimerComponent.cs:38-52` — `UpdateText()` runs in
   `_Ready` **before** the deferred `EnsureLabel` creates the label, so it early-returns and the label
   shows blank until the first tick. Fix: call `UpdateText()` at the end of `EnsureLabel` (mirror
   `BadgeComponent.BuildBadge`).
4. **Badge spurious init emit** — `ecs/ui/BadgeComponent.cs:69-87` — `UpdateBadge()` emits
   `CountChanged(Count)` unconditionally, including the two init calls (`_Ready`, deferred `BuildBadge`),
   so a listener wired after `_Ready` gets a startup `CountChanged`. Gate the emit to actual
   `SetCount`/`Increment` changes (add an `emit` param like `ToggleSwitch`, or skip during build).
5. **Carousel leaves slides `TopLevel`** — `ecs/ui/CarouselComponent.cs:147-152` — `InitSlides` sets
   every slide `TopLevel=true` but `_ExitTree` never restores `TopLevel=false`; a component removed
   alone leaves its (pre-existing) slide children frozen `TopLevel` at their last global position.
   Restore `TopLevel=false` on exit.
6. **`CallDeferred` string-form** — prefer `Callable.From(X).CallDeferred()`: `ecs/AudioComponent.cs:29`,
   `ecs/HealthBarComponent.cs:33`, `ecs/BootComponent.cs:35`, `ecs/FootstepComponent.cs:45`,
   `ecs/HitSoundComponent.cs:53`. Works today (generator registers the private methods); align for
   consistency with the round-2 code beside them.
7. **ChromaticAberration repeat-Apply garbage** — `ecs/ui/ChromaticAberrationComponent.cs` — a repeat
   `Apply()` discards the old `_mat` (garbage, not a leak). Trivial: reuse `_mat` if already built, or
   note it. Lowest priority.

## Gotchas

- Cloud `Visible` gate: make sure a weather change from Clear→Storm sets `Visible=true` again (it should
  via `_cloudAlphaCurrent > 0` once alpha ramps) — don't leave clouds permanently hidden.
- Badge `emit` param: keep the interactive `SetCount`/`Increment` path emitting; only the build-time
  seed is silent.
- Carousel: restore `TopLevel=false` only for slides this component set (it set them all in `InitSlides`,
  so restoring all is correct).

## Verify

1. Build + validator.
2. Editor: Clear weather → clouds not rendering (Visible false); Storm → clouds show. Profile if unsure
   the shader cost dropped.
3. A fresh `MatchTimer` → shows the formatted time immediately, not blank.
4. A `Badge` with a listener attached at `_Ready` → no startup `CountChanged`.
