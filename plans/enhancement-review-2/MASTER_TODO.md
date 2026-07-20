# Master TODO Tracker — Enhancement Review Round 2

> Second full one-by-one sweep of `addons/beep_game_builder_cs/` (2026-07-19), run **after** the
> round-1 fix pass (`plans/enhancement-review/`) landed. Per-phase detail lives beside this file.
>
> **Goal:** fix what the deeper pass found — a real use-after-free crash, unguarded teardown
> throws, a regression from round 1's own offset-transform work, and a systemic "leftover-child
> leak" class — plus the behavioral and silent-failure remainders.
>
> **Scope:** framework only (see `CLAUDE.md`). No game content, no art. The addon deliberately
> ships no UI textures — theme.json slots pointing at nonexistent PNGs are the intended graceful
> fallback (verified: `SkinCatalog.cs:513`), **not** a bug.

---

## How this review was produced

Eight parallel passes, each reading its files one by one **and** verifying round-1's fixes held:

| Pass | Coverage | Result |
|---|---|---|
| Gameplay A–I | 44/44 | 2 bugs, ~11 polish |
| Gameplay I–W + categories/stats/items | 54/54 | 2 bugs, ~8 polish |
| Atmosphere | 11/11 | all 6 recent fixes verified; 1 gap, 4 polish |
| UI A–M | 37/37 | 1 crash bug, ~8 gaps, ~8 polish |
| UI N–W | 34/34 | 1 gap, 4 polish |
| Scene templates | 25/25, validator **PASS** | robot NPC verified; 2 polish |
| Genre scenes + scripts | 34 scripts + 34 scenes | all clean; 3 polish |
| Repo-wide signal audit | ~228 signals, 7 C# events | **all clean** — 0 dead, 0 leaks, 0 stragglers |

**Round-1 verified: all fixes held, no regressions except one** (`TweenComponent.CardHoverPop` on a
Node2D target — Phase 1). The signal layer is now pristine: every declared signal is emitted, every
cross-object subscription is balanced, and the nine deleted APIs have zero code stragglers (one
stale `.md` doc line only). The BYO-texture fallback (`SkinCatalog.cs:513`) holds.

**Totals: ~40 findings — 6 bugs, ~12 gaps, ~22 polish.** No systemic rot; this is the long tail.

---

## Progress

- [x] **Phase 0 — Review** — the 8-pass sweep above; findings archived per phase doc.
- [x] **[Phase 1 — Crashes & teardown safety](phase-1-crashes-teardown.md)** — **done, build 0 errors,
      validator PASS.** `ChipComponent` `_ExitTree` frees `_chip`; `Particle`/`SpriteEffect`/`BossHealthBar`/
      `DropTable`/`ContextMenu`/`Drag`/`SquashAndStretch` sibling `-=` guarded with `IsInstanceValid`;
      `AutoHeal` no longer disables on death (drops the harmful `IsActive=false`; `_Process` already
      bails on `IsDead`) → regen resumes after revive; `BeepGenreScene.InstantiateMainScene` removes a
      prior `_<Genre>Main` before re-adding (idempotent) + warns on null load; `TweenComponent.CardHoverPop`
      uses type-correct props (raw scale/rotation on Node2D).
- [x] **[Phase 2 — Leftover-child & material leaks](phase-2-leftover-child-leaks.md)** — **done, build 0
      errors, validator PASS.** Badge/Combo/BuffBar/InteractionPrompt/MatchTimer now free their
      parent-hosted node in `_ExitTree` (InteractionPrompt & MatchTimer track a `_createdLabel` flag so
      an adopted parent Label is left alone); SkeletonLoader + ChromaticAberration restore the parent's
      prior material on exit; ToastNotification kills in-flight dismiss tweens + frees toasts.
- [x] **[Phase 3 — Signal edge-triggering & double-emit](phase-3-signal-edges.md)** — **done, build 0
      errors, validator PASS.** `GameFlow.LevelComplete` latched (re-armed in `Reset()`);
      `HungerStamina.StaminaCritical` routed through the existing `_staminaCriticalActive` latch;
      `ToggleSwitch` seeds initial state with `SetState(…, emit:false)`; `AchievementToast` dedupes by
      title within 750ms (Decision C) + warns on missing GameApp.
- [x] **[Phase 4 — Behavioral correctness](phase-4-behavioral.md)** — **done, build 0 errors, validator
      PASS.** MovingPlatform Once stops via `_running` (Start resumes); DataBinder now polls `RefreshTwoWay`;
      Match3 editor-guarded + `async` dropped; Menu resets `_navWired` on exit; `Cooldown.Reset` emits
      `CooldownReady`; Modal StartVisible routes through `Open()` (builds overlay); Cooldown/AI decisions
      applied — **B**: Idle AI no longer auto-chases; **A**: `Pulse` documented as intentional perpetual loop.
- [x] **[Phase 5 — Silent-failure warnings (round 2)](phase-5-silent-failures.md)** — **done, build 0
      errors, validator PASS.** Warnings added: Attack ranged→melee fallthrough (latched), DoorSwitch
      unresolved path, Boot missing Settings, the 4 overlay wire helpers (else PushWarning);
      BeepGenreScene load + AchievementToast null folded into Phases 1/3. **Decision D:** the three
      sandbox genres (strategy/citybuilder/cardgame) are intentionally completion-less — recorded, no
      `LevelCompletePath` added.
- [x] **[Phase 6 — Atmosphere polish](phase-6-atmosphere.md)** — **done, build 0 errors, validator
      PASS.** Per-type particle `Lifetime` so snow/leaves cross the full viewport; lightning flash
      `MoveChild(-1)` to topmost; dead `_weatherTransitionTween` field removed; weather-audio now sets
      volume directly on the per-frame intensity path (no tween churn), keeping the tween only for the
      deactivate fade; `SeasonChanged` documented as a developer hook.
- [x] **[Phase 7 — Cleanup & consistency](phase-7-cleanup.md)** — **done, build 0 errors, validator
      PASS.** `base._ExitTree()` chained across all 11 flagged components; `AnimateExit` dead export
      deleted; `IGameStateable.cs` → `ISaveable.cs` (+ `.uid`); stale `BindingRefreshed` doc line removed;
      redundant `GD.Randf()` casts dropped; `ColorPalette.ByName(null)` guarded; `JumpComponent.ForceJump`
      IsActive-gated; `ThemePreset` redundant call removed; `KeybindManager.Rebind` now sets modifiers;
      `ScreenShake` re-centers on deactivation; `Inventory` emits `ItemAdded` on partial add; `Settings`
      applies defaults on early-return; GameOver/LevelComplete "(Best!)" now strictly-greater.

**All 7 phases complete — build 0 errors, validator PASS throughout. ~40 findings resolved.**

## Verification gates (every phase)

1. `dotnet build` → 0 errors (~144 pre-existing nullable warnings are noise).
2. `cd addons/beep_game_builder_cs/templates/scenes && ./validate_scenes.sh` → PASS.
3. Phase-specific editor checks in each doc — **run them.** "Compile-verified" is not "tested."

## Decisions needed

| # | Decision | Phase |
|---|---|---|
| A | `TweenComponent.Pulse` `SetLoops(0)` = infinite loop — intended perpetual pulse, or single pulse? | 4 |
| B | `AIController` proactive-chase on `Idle` / the talk-NPC following — add `AutoAcquire` export, or leave? | 4 |
| C | `AchievementToast` double-toast — dedupe by id, or document "wire one source"? | 3 |
| D | strategy/citybuilder/cardgame have no `LevelCompletePath` — intentional (sandbox), or add parity? | 5 |

## Cross-references

- **enhancement-review** (`plans/enhancement-review/`) — round 1, complete. This round confirms it
  held and picks up the long tail + one regression it introduced.
- The `_ExitTree`/`base._ExitTree()`/`IsInstanceValid` guard patterns here extend the lifecycle
  discipline round-1 Phase 1 established — same rules, remaining sites.
