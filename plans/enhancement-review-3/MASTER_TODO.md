# Master TODO Tracker — Enhancement Review Round 3

> Third full one-by-one sweep of `addons/beep_game_builder_cs/` (2026-07-19), run after rounds 1 & 2.
> Each pass **verified round-2's ~40 fixes held** (they all did — no regressions) and hunted the
> remaining tail. Per-phase detail beside this file.
>
> **Goal:** fix what the deep pass found — 3 real pre-existing bugs (one longstanding: precipitation
> never renders), and two *systemic consistency gaps* the round-2 sweeps applied unevenly.
>
> **Scope:** framework only. theme.json slots → nonexistent textures = intended BYO fallback
> (`SkinCatalog.cs:513`, re-verified HELD), not a bug.

---

## How this review was produced

Six parallel passes, each verifying round-2 changes + reading its files one by one:

| Pass | Coverage | Round-2 verdict | Result |
|---|---|---|---|
| Gameplay A–I | 44/44 | all HELD | 1 gap cluster, 1 polish |
| Gameplay I–W + categories/stats/items | 54/54 | all HELD | 1 bug, 1 gap, 4 polish |
| Atmosphere | 11/11 | all 5 HELD | 1 bug, 1 gap, 2 polish |
| UI A–M | 37/37 | all HELD | 1 bug, 2 gaps, 3 polish |
| UI N–W | 34/34 | all HELD | 1 bug, 2 gaps, 1 polish |
| Scenes + genre + signals | 25 scenes (validator PASS), 34 scripts, 232 signals | all HELD | signals **clean**, 1 gap |

**Round-2 verified: every change HELD, zero regressions.** Signal layer is pristine (no dead signals,
subscription hygiene balanced + guarded, zero stragglers to deleted APIs, the `ISaveable` rename
clean). ~20 findings — **3 bugs**, and the rest is the two consistency gaps + a short polish tail.

---

## Progress

- [x] **Phase 0 — Review** — the 6-pass sweep above; findings archived per phase doc.
- [x] **[Phase 1 — Real bugs](phase-1-bugs.md)** — **done, build 0 errors, validator PASS.**
      Precipitation `Amount` now assigned only on change (cached `_lastParticleAmount`, reset on weather
      change) — particles actually fall now, unmasking round-2's Lifetime fix; `Toast` `_activeToasts`
      switched `Queue`→`List` and pruned on dismiss (no more use-after-free); `Inventory._Ready`
      editor-guarded; `ToggleSwitch._bg`/`DialogUI._panel` freed on exit; `Rating` star handlers stored
      + disconnected + labels freed.
- [x] **[Phase 2 — Consistency sweeps](phase-2-consistency.md)** — **done, build 0 errors, validator
      PASS.** 5 sibling `_health -=` guarded with `IsInstanceValid` (Flash/HealthBar/HitSpark/HitStop/
      HitSound); `base._ExitTree()` added to 15 overrides (14 `ui/` + `TurnManager`). SkeletonLoader
      already had it. No duplicates.
- [x] **[Phase 3 — Behavioral & silent-failure tail](phase-3-behavioral.md)** — **done, build 0 errors,
      validator PASS.** MovingPlatform `Start()` rewinds a finished Once run (Decision A); runtime
      `EnableClouds` re-raises the flash to top; Quest tolerates null objective slots; Knockback gates
      on `!IsActive`; Turret warning editor-guarded; Interactable guards the action with `InputMap.HasAction`.
- [x] **[Phase 4 — Polish](phase-4-polish.md)** — **done, build 0 errors, validator PASS.** Cloud
      overlays now `Visible=false` when invisible (skips the fullscreen shader — the system's most
      expensive draw); weather-audio `Setup` early-returns when inactive; `MatchTimer` renders
      immediately; `Badge` no startup `CountChanged` (`emit:false`); `Carousel` restores `TopLevel` on
      exit; ChromaticAberration reuses its material; 5 `CallDeferred` string-forms → `Callable.From`.

**All 4 phases complete — build 0 errors, validator PASS throughout. ~20 findings resolved, incl.
the precipitation bug that had silently defeated rain/snow rendering.**

## Verification gates (every phase)

1. `dotnet build` → 0 errors.
2. `cd addons/beep_game_builder_cs/templates/scenes && ./validate_scenes.sh` → PASS.
3. Phase-specific editor checks in each doc.

## Decisions

| # | Decision | Phase |
|---|---|---|
| A | `MovingPlatform` Once restart: reset `_target` so `Start()` re-runs it, or document Once as single-shot (Start = no-op after completion)? | 3 |

## Cross-references

- **enhancement-review-2** (`plans/enhancement-review-2/`) — round 2, complete & verified held. This
  round is the third-pass tail: 3 bugs it didn't reach + the two spots its own sweeps applied unevenly.
- The precipitation `Amount` bug (Phase 1) is the root cause the round-2 `Lifetime` fix was working
  *around*; fixing it makes that fix visible.
