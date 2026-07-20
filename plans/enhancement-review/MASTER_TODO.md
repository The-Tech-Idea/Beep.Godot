# Master TODO Tracker — Full Component / Scene / Event Enhancement Review

> Tracks the fixes and enhancements from the **2026-07-19 full sweep** of every component,
> scene, and signal in `addons/beep_game_builder_cs/`. Per-phase detail lives beside this
> file: `plans/enhancement-review/phase-N-*.md`.
>
> **Goal:** close every defect and dead seam the sweep found — so that every shipped
> component works or warns, every declared signal/export does something, and the shipped
> UI actually consumes the session state the framework tracks.
>
> **Scope:** framework only (see `CLAUDE.md` § *Scope*). No game content, no assets.
> Overlaps with the **entity-model** initiative are cross-referenced, not duplicated.

---

## How this review was produced

Eight parallel review passes on 2026-07-19, each reading its files one by one:

| Pass | Coverage | Result |
|---|---|---|
| Gameplay components A–I | 44/44 files | 16 findings |
| Gameplay I–W + categories + stats + items | 54/54 files | 12 findings |
| Atmosphere subsystem + flag trace | 11/11 files + `atmosphere.tscn` | 9 findings |
| UI components A–M | 36/36 files | 19 findings |
| UI components N–W | 34/34 files | 14 findings |
| Shared scene templates | 25/25 `.tscn`, `validate_scenes.sh` **PASS** (all 11 checks) | 4 findings |
| Genre scenes + scripts | 34 scripts + 29 genre `.tscn`, genre.json cross-check clean | 10 findings |
| Repo-wide signal/event audit | 239 `[Signal]`s, 9 C# events, 0 `.tscn` connections | 7 dead signals, 2 dead events, 1 leak |

**Totals: 213 C# files + 54 unique scenes + 239 signals. ~70 findings: 17 bugs, ~20 gaps, ~35 polish.**

Health worth recording (verified, so nobody re-audits it):
- **Zero silently-dropped snake_case exports** in any `.tscn` — the validator's PascalCase check holds.
- Every `genre.json` `scenes[]` matches shipped files; all `nav_wiring` keys resolve.
- Navigation discipline is clean everywhere (overlays close via `SceneNav.CloseOrReturn`/`QueueFree`).
- Signal hygiene is excellent: of 239 signals, 232 are emitted, and **exactly one** unbalanced
  subscription exists in the whole repo (`HudComponent` — Phase 1).
- The player/enemy combat loop closes correctly in `player_template`/`enemy_template`
  (group joining via `EntityComponent.cs:66`, `AIController → AttackComponent` verified).
- `offset_transform_*` is used correctly throughout the effect components; only 4 stragglers remain (Phase 7).

---

## Progress

- [x] **Phase 0 — Review** — the 8-pass sweep above; findings recorded in the phase docs.
- [x] **[Phase 1 — Lifecycle: leaks, orphans, crash paths](phase-1-lifecycle-leaks-crashes.md)**
      — the only handler leak in the repo (`HudComponent`, crashes after scene change), a viewport
      subscription leak, an orphaned modal overlay, an unreachable-guard crash in save/load,
      injected nodes never cleaned up. 11 items, all mechanical. **Done — build 0 errors, validator PASS.**
      (SaveLoadManager's two dead NodePath exports were redesigned to typed `PackedScene`
      exports and `LoadStarted` was wired here too, folding in those Phase 2 rows.)
- [x] **[Phase 2 — Dead API: wire or remove](phase-2-dead-api.md)**
      — 7 never-emitted signals, 2 never-invoked C# events, 6 dead `[Export]`s. A developer
      who connects/sets any of these waits forever. **Done — DECISION REVISED to D1 = DELETE ALL
      (build 0 errors, no new warnings).** Deleted: signals `Fled`, `LineDisplayed`/`DialogFinished`,
      `QuestFailed`, `BindingRefreshed`, `OverwriteConfirmed`, `LoadStarted`; events `RowDoubleClicked`,
      `ItemDoubleClicked`; exports `TextSpeed`, `HealOnlyOutOfCombat`, `MaxScreenDistance`, `StatName`,
      `SaveMenuScene`/`LoadMenuScene`. **Kept** (genuine fixes, not dead API): the `BeepTreeView`
      name-collision bug fix (real `ItemActivated` signal now connected), the SaveLoad untyped-instantiate
      crash fix + idempotency guard, the SaveGameMenu two-press overwrite guard (data-loss protection,
      minus the removed signal), MovingPlatform/Marquee `Start()`/`Stop()` (Phase 3 behavior). D3:
      `StatName` removed — `AutoHealComponent` heals health only. Nav save/load signal overlap documented.
- [x] **[Phase 3 — Behavioral correctness](phase-3-behavioral-correctness.md)**
      — signals that fire every frame instead of on edges, `AutoStart` that doesn't,
      a season that always starts in Spring, an inert NPC template, pool mis-accounting.
      **Done — build 0 errors, validator PASS.** Edge-guarded TopDown `Stopped`, FollowTarget
      `TargetLost`/`TargetReached`, Hunger/Thirst/Stamina; wired MovingPlatform + Marquee `Start()`/`Stop()`,
      Seasonal `DefaultSeason`, DayNight seed gate, Settings `Language`, Chromatic runtime `Strength`,
      Rating `SetValue` emit, Counter numeric cache, ObjectPool self-heal + double-release guard,
      Turret pool contract. D2: robot_npc → wandering `AIController` (standalone MovementComponent removed).
- [x] **[Phase 4 — Silent-failure sweep + input gates](phase-4-silent-failure-sweep.md)**
      — ~14 missing `PushWarning`s on null resolves (the repo's #1 defect class, still alive)
      and 6 components polling input actions without the `InputActionsAvailable` gate.
      **Done — build 0 errors.** Warnings added across Aggro/Attack/CameraZoom/HealthBar/Audio/
      BeepGenreScene/GameInfoBinder/Match3/DialogUI/Accordion (Hud/AutoHeal/FollowTarget/Hunger/
      AnimalBehavior/DataBinder folded into earlier phases); Attack no longer emits `Attacked` on a
      no-op; DoorSwitch resolves the player via the `players` group; input gates added to
      Jump/Dash/Glide/Hover (Slide/WallJump done in Phase 1).
- [x] **[Phase 5 — Session-state integration](phase-5-session-state-integration.md)**
      — `GameApp`'s 12 session signals have **zero consumers**; every results screen shows
      hardcoded stat literals; 4 overlay screens ship inert buttons. The biggest visible win.
      **Done — build 0 errors, validator PASS.** 5b was already in code (`GameFlowComponent.AddScore`
      forwards to `GameApp.AddSessionScore`). Results screens (GameOver/LevelResults/RunResults/
      LevelComplete) now bind real `SessionScore`/`BestScore`; RaceResults/LevelFailed genre stats
      documented as seams. The 4 inert overlays (BuildMenu/Research/UnitPanel/Crafting) record a
      choice via `GameStateManager.SetGameData` + doc. New `AchievementToastComponent` bridges both
      achievement sources to the shipped toast; `SettingsChanged` documented as external hook.
- [x] **[Phase 6 — Atmosphere & world polish](phase-6-atmosphere-world.md)**
      — precipitation doesn't follow the camera, cloud shadows draw above clouds,
      day/night ships dark in all 10 genres, foliage wind computed but unconsumed.
      **Done — build 0 errors, validator PASS.** Precipitation now follows the camera; cloud shadow
      reordered under clouds; WeatherAudio reads the enable flag + removes its bus on exit; lightning
      shake-miss warns; fog material reused not re-newed. D6 + the TimeOfDay/foliage-wind seams
      documented in doc comments (day/night ships dark by deliberate default — not changing genre
      content). The two atmosphere *bugs* (DayNight seed, DefaultSeason) landed in Phase 3.
- [x] **[Phase 7 — Transform & convention hygiene](phase-7-transform-tween-hygiene.md)**
      — 4 remaining container-transform violations (1 in a shipped widget), `CallDeferred`
      string-form stragglers, dead code, redundant casts. **Done — build 0 errors, validator PASS.**
      Accordion (Phase 4), SquashAndStretch, TweenComponent converted to offset-transform for Control
      targets; MenuComponent now binds each button (no focus-owner guess); Drag/Dialog/Modal warn on
      Container hosts; 5 `CallDeferred` → `Callable.From`; dead `GetDirectionOffset`/`_health` removed;
      Vignette restores prior material; ProgressRing editor-guarded + setter emits; Localization doc.
- [x] **[Phase 8 — UX, accessibility & template consistency](phase-8-ux-consistency.md)**
      — keyboard/gamepad focus for 5 mouse-only widgets, genre-main HUD inconsistency,
      leftover empty `UI` layers, small export additions. **Done — build 0 errors, validator PASS.**
      Keyboard/gamepad: Carousel (ui_left/right), Chip (focus + ui_accept), Rating (focus + ui_left/right),
      Modal (grab focus + ui_cancel to close), Load/SaveGameMenu initial focus. Added `BossName` export;
      Settings warns on a corrupt (vs absent) config; `TemperatureAffectsThirst` landed in Phase 3.
      D5 (six genre mains without `HudComponent`): resolved as **intentional** — those genres show
      genre-specific stats (Speed/Lap, Resources) the generic HUD doesn't map, so it's the developer's
      canvas (scope rule); not editing scenes. Empty `UI` layers + topdown generation-time path left
      as-is (cosmetic, no functional defect).

## Verification gates (every phase)

1. `dotnet build` → 0 errors (~148 pre-existing nullable warnings are noise).
2. `cd addons/beep_game_builder_cs/templates/scenes && ./validate_scenes.sh` → PASS.
3. Phase-specific runtime checks listed at the bottom of each phase doc — **run them in the
   editor before checking the box.** Per `CLAUDE.md`: "compile-verified" is not "tested".
4. When a phase fixes a *class* of defect, add a `validate_scenes.sh` check for it where the
   class is scene-detectable (and make the new check fail once before trusting it).

## Decisions needed before executing (details in phase docs)

| # | Decision | Phase |
|---|---|---|
| D1 | Each dead signal/export: **wire it or delete it** (per-item table in the phase doc) | 2 |
| D2 | `robot_npc_template`: wandering NPC (add `AIController`) or static talk NPC (drop `Movement`+`Aggro`)? | 3 |
| D3 | `AutoHealComponent.StatName`: wire to `StatsComponent` (entity-model overlap) or narrow to health-only? | 2 |
| D4 | Results screens: bind live `GameApp` session state, or documented `PushWarning` seams? | 5 |
| D5 | Six genre mains without `HudComponent`: add one, or ship a "developer wires stats here" doc note? | 8 |
| D6 | Day/night: enable in ≥1 genre so the feature is demonstrable, or document that it ships dark? | 6 |

## Cross-references

- **entity-model initiative** (`plans/entity-model/`) — owns the item/equipment data model.
  D3 and any `StatsComponent` wiring land there if answered "wire".
- **component-audit-round2** (`plans/component-audit-round2.md`) — the prior audit's fixes have
  landed (its Tier-1/3 items no longer reproduce; `PowerSource`/`PowerReceiver` are gone).
  This sweep supersedes it as the current defect inventory.
