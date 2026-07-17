# World & atmosphere — 24 components

Weather, day/night, seasons, world FX, juice.

> **The atmosphere stack is the one system in this addon that actually works** — and it is the
> proof that the group pattern is viable. `WeatherSystemComponent` self-joins `"weather_system"`
> (`:225`) and **five consumers find it**. Copy this shape; do not copy `ComponentGroup`, which
> has never worked (see `infra.md`).

---

## ALIVE — the atmosphere stack

All mounted in `atmosphere.tscn`, which `survival_main.tscn:14` and others instance.

| Component | Evidence | Note |
|---|---|---|
| `AmbientController` | `atmosphere.tscn:14-15`; reached by `DayNightCycleComponent.cs:63` and `WeatherSystemComponent.cs:265` via `ForTree()`; composes layers `:73-84`; eases `_Process:86-97` | The CanvasModulate arbiter. Keep |
| `DayNightCycleComponent` | `:17-18`; contributes tint `:97`; `PhaseChanged` consumed by `LightingComponent.cs:66,78` | Keep |
| `WeatherSystemComponent` (+`.DayNight`, `.Intensity`, `.Overlays`) | `:20-21`; self-joins group `:225`; drives fog (`DynamicFogLayer.cs:129`), wind (`WindFieldComponent.cs:121`), HUD (`WeatherHUDComponent.cs:103`), bolts (`:605`) | **The hub — the one working system** |
| `DynamicFogLayer` | `:29-30`; `_Process:128-129` reads `CurrentWeather`/`WeatherIntensity`; `DefaultNoise()` (`:166-182`) guarantees the sampler is bound | Keep. The procedural fallback is the **correct** answer to a null asset export |
| `SeasonalComponent` | `:23-24`; `CurrentSeason` read by **5** consumers | Keep; two defects below |
| `LightningBoltComponent` | **In no `.tscn` — correctly.** Constructed and driven at `WeatherSystemComponent.cs:605-609` (`new`, `AddChild`, `Strike`); self-frees `:71-74` | Keep. **The one file where "not in any scene" is not a death signal** |
| `WeatherForecastUI` | 6 scenes (`platformer_main.tscn:8`, `racing_main.tscn:5`); extends `Control` | Keep |

### `SeasonalComponent` — two defects
1. **Seasons rotate every 7 real seconds.** `:74-79` accumulates `_seasonTimer += delta` in **real
   seconds** and compares against `DaysPerSeason = 7.0`, documented at `:27` as *"in-game days"*.
   `DayNightCycleComponent.DayLengthSeconds` (`:31`) is the unit that should scale it.
2. **Its headline feature is unimplemented.** `GetFoliageWindParams()` (`:129`) and
   `GetCurrentSeasonColor()` (`:138`) have **no callers**. The doc (`:6-10`) promises "foliage
   color, wind effects, environment tinting"; `_currentSeasonColor` is tweened (`:97-102`) into a
   variable nothing reads. Unlike `DayNightCycleComponent` it **never contributes to
   `AmbientController`**.

**Fix (S/M):** **read in-game days from `GameClock`/`DayNightCycleComponent`; delete `_seasonTimer`
(`:74`).** Do **not** patch it with a magic multiplier — `DayNightCycleComponent.cs:72` already
does the delta→in-game-hours conversion **correctly** (`TimeOfDay + delta * (24f / DayLengthSeconds)`),
in the same scene, and this component simply never read it. **The right clock was already there.**
This is the shipped instance of the time-unit confusion Phase 7 exists to end.

Then: contribute the season colour to `AmbientController` like DayNight does, or delete the two
dead getters and the doc claim.

---

## INERT — mounted but undriven

### `WeatherAudioController` — the most deceptive file in the addon
**Evidence:** `atmosphere.tscn:26-27` → `Setup:62` builds an audio **bus** and 4 players — **all
at `VolumeDb = -80f`** (`:86`, `:143`). `SetWeatherIntensity` (`:105`) and `PlayThunder` (`:127`)
have **0 callers repo-wide**. Its sole handler `OnWeatherChanged` (`:160-165`) is an **explicit
no-op whose comment asserts a caller that was never written** (`:163`).

It is mounted in the shipped scene, runs code every session, and mixes **permanent silence**. It
looks alive from the `.tscn`.

**Fix (S):** hook it at `WeatherSystemComponent.cs:565` (`EmitSignal(LightningStruck)`) and at the
weather-change emit. Then set a real volume.

### `AmbientAudioComponent`
**Evidence:** 0 refs repo-wide. `EnterCombat` (`:95`) / `ExitCombat` (`:106`) uncalled. Needs an
`Area2D` parent (`:61-65`) that no scene provides. Consumes `"weather_system"` (`:71`) — which
**is** joined, so that half would work.

**Fix (M):** ship a zone template, **and** a combat detector (nothing detects combat today). Or
delete — see `DELETE.md` borderline.

### `WindFieldComponent` — well-built, needs a host
**Evidence:** `_PhysicsProcess:88`. Reads live `WeatherSystemComponent.WindForce` (`:93`) — and
that system **is** scene-reached. Needs an `Area2D` host (`:57`) with a level-spanning shape that
no scene provides.

**Defects:** `:106` does `wind.X * 0.01f * CharacterPushAccel * dt` — **double-scales**, since
`:93` already multiplied by `PhysicsWindScale = 400`. Its comment at `:119` is a witness to the
`ComponentGroup` failure: *"joins its group in its `_Ready()` regardless of `ComponentGroup`; fall
back to a tree scan."*

**Fix (M):** add the `Area2D` host to `platformer_main`; fix the double scale. High payoff.

### `ScreenShakeComponent` — has a live caller and no node
**Evidence:** `_Process:40`. **`WeatherSystemComponent.cs:623-632` scans the tree for it and calls
`Shake(...)` on lightning** — and that system is scene-reached. **Zero scenes contain the node**,
so `FindChild` returns null.

**Defect:** `MaxTrauma = 100` with `DefaultIntensity = 5` (`:13-15`) → `trauma01 = 0.05` →
`shake = 0.0025` → offset ≈ **0.05 px**. Invisible even once wired.

**Fix (S):** add under the `Camera2D` in `platformer_main`/`topdown_main`; set `MaxTrauma ≈ 1`.
**The cheapest wire in the addon** — the caller already exists.

### `AnimalBehaviorComponent`
**Evidence:** 0 refs; `_Process:55`; `_body = GetParent() as CharacterBody2D` (`:52`, safe).
`TryHunt()` (`:119`) 0 callers. Needs Seasonal + Weather siblings (`:25`,`:35`) **and** an AI
driver. **No animal entity scene ships**; `survival_main.tscn` has none.

**Defect:** dead re-check at `:65` — `_body is CharacterBody2D cb` on an already-typed field.

**Fix:** **borderline — needs a call.** Keep only if survival is meant to ship animals; otherwise
delete. → `DELETE.md`

---

## INERT — missing wire

### `TrailComponent` — the trail sticks to the thing it trails
**Evidence:** `_Process:56`. 0 scenes, 0 callers.

**Defects:**
- **`:61` uses `parent2D.Position`, not `GlobalPosition`** — and the `Line2D` is a **child** of the
  parent, so the points are parent-local *and* the line rides along. The trail follows the blade
  instead of staying in the world. **It cannot work as written.**
- Categorized `: UIComponent` — a world effect requiring `Node2D` (`:31`).

**Fix (S):** global-space points (or reparent the `Line2D` to the level); reclassify to
`WorldComponent`; wire to `projectile_template`.

### `AudioComponent`
**Evidence:** 0 scenes, 0 callers (`Play`/`PlayOneShot` `:47`,`:60`). **Not redundant** with
`AmbientAudioComponent` (a zone crossfader) or `FootstepComponent` (owns its own player).

**Fix:** **borderline.** No duplicate exists, but **every audio need in the addon is met by a
component rolling its own `AudioStreamPlayer`** — it was never adopted, for a reason not
recoverable from the code. Either adopt it across those components or delete. → `DELETE.md`

---

## BROKEN

### `LightingComponent` — `_Process` is an empty body with a comment
**Evidence:** `:70` — an **empty `_Process`**. Uses `DirectionalLight2D` (`:38`, `:55`) against a
2D shadow model that does not work that way. **`:124` tweens `"shadow_item_cull_margin"` — an
`int` property — with a float tween.** `_lastPhase` (`:41`) written at `:106`, never read. Finds
`DayNightCycleComponent` (`:63`), which *is* scene-reached — but **zero scenes contain a
`LightingComponent`.**

**Fix:** **delete.** `DayNightCycleComponent` already tints the scene; this is a broken second
path. → `DELETE.md`

---

## REDUNDANT

### `BobComponent` → `PickupComponent`
**Evidence:** `PickupComponent.cs:15-17,69-70` already exports `FloatAmplitude`/`FloatSpeed`/
`AutoRotate` and does the sin-bob itself — which is what `pickup_template.tscn:21-23` ships.
`BobComponent` has 0 refs, and **latches `_startPos` at `_Ready` (`:30`), teleporting a moving
parent back.**

**Fix:** delete. → `DELETE.md`

### `RotateComponent` → `PickupComponent.AutoRotate`
**Evidence:** `_Process:27`, correct, 0 scenes. Duplicated by `PickupComponent.AutoRotate`
(`:17`,`:70`) **for the coin case — the one place it would be used.**

**Fix:** delete. → `DELETE.md`

### `ParallaxComponent` → Godot's `ParallaxBackground`
**Evidence:** `_Process:45`, correct, with a good camera fallback (`:35`). 0 scenes. Duplicates the
engine node — **which is what the shipped levels already use**
(`levels/shooter/level_1.tscn:10-11`). Also consumes a `FollowCameraGroup` (`:33`) nobody joins.

**Fix:** delete. → `DELETE.md`

### `LifetimeComponent` → `ProjectileComponent.MaxLifetime`
**Evidence:** `_Process:29`, correct, 0 scenes, 0 callers. It belongs on `projectile_template` —
which already relies on `ProjectileComponent.MaxLifetime` (`:14`). Two timers freeing one node.

**Fix:** delete. → `DELETE.md`

---

## Order

1. **`ScreenShakeComponent`** into the camera + `MaxTrauma` fix (S) — the caller already exists.
2. `WeatherAudioController` hook at `WeatherSystemComponent.cs:565` (S) — stop mixing silence.
3. `SeasonalComponent` timer scale (S) — seasons currently rotate every 7 seconds.
4. `TrailComponent` global-space fix + reclassify (S).
5. `WindFieldComponent` `Area2D` host + double-scale fix (M).
6. Delete: `LightingComponent`, `BobComponent`, `RotateComponent`, `ParallaxComponent`,
   `LifetimeComponent`.
7. Decide: `AnimalBehaviorComponent`, `AudioComponent`.
