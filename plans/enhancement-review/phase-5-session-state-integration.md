# Phase 5 — Session-state integration (GameApp → shipped UI)

## Why

The single biggest disconnect the sweep found: **`GameApp` tracks a full session and
broadcasts 12 signals — and not one thing in the framework listens.** `SessionScoreChanged`,
`LevelChanged`, `LevelCompleted`, `SessionStarted/Ended`, `GameRunningChanged`,
`GamePaused/Resumed`, `DifficultyChanged`, `AchievementUnlocked`, `CheckpointReached`,
`DevModeToggled` all fire (`ecs/GameApp.cs:217-418`) into the void — their only references
are doc comments in `core/GameAppGuide.cs`. Meanwhile every results screen displays
hardcoded literals ("Score: 8420") and the shipped HUD binds to a scene-local
`GameFlowComponent`, so score routed through `GameApp.AddScore` never reaches any pixel.

This is the phase with the biggest visible payoff: after it, the loop the framework promises
(play → score → results) actually shows real numbers.

## The work

### 5a. Results screens — bind session state or document the seam (**Decision D4**)

Every one displays scene-literal stats, reads nothing, and marks no seam (contrast
`CardBattle`/`PreLevel`/`LevelUpChoice`, which document their game-fills-this seams —
and `ecs/scenes/LevelSummary.cs`, the correct model):

| Screen | Dead literals |
|---|---|
| `ecs/scenes/GameOver.cs` + `game_over.tscn:36` | `StatsLabel` "Score: 0" — `GameInfoBinder` binds only the title |
| `ecs/scenes/platformer/LevelResults.cs` + `level_results.tscn:69-91` | score "1240", time, coins, deaths, stars |
| `ecs/scenes/shooter/RunResults.cs` + `run_results.tscn:71-79` | score "8420", kills, floor, gold |
| `ecs/scenes/puzzle/LevelComplete.cs` + `level_complete.tscn:56-62` | score "1820" + "New High Score!" (`GameApp.BestScore` exists precisely for this) |
| `ecs/scenes/racing/RaceResults.cs` + `race_results.tscn:42-52` | time, placement, reward |
| `ecs/scenes/puzzle/LevelFailed.cs` + `level_failed.tscn:42-48` | "980 / 1500" + progress bar (minor — its retry-bonus path is already documented) |

Recommended: bind what `GameApp` genuinely has (`SessionScore`, `BestScore`,
`CurrentLevel`) in each screen's `_Ready`, and mark the rest (kills, coins, placement —
genre-specific stats the framework doesn't track) as documented `PushWarning`-free doc-comment
seams. That split respects the scope rule: generic state is ours, genre stats are theirs.

### 5b. HUD ↔ GameApp

- `HudComponent` binds only the scene `GameFlowComponent`. Either subscribe
  `GameApp.SessionScoreChanged` as an additional (or fallback) source, or make
  `GameFlowComponent` forward score into `GameApp.AddSessionScore` so both stay coherent.
  Pick one direction and document it — today there are two score systems that never meet.

### 5c. Inert overlay buttons — wire or document (same standard as siblings)

Four screens render interactive buttons no script wires and no doc marks (siblings
`CharacterSelect`/`VehicleSelect`/`LevelUpChoice` record choices into `GameStateManager`
and document that applying them is the game's job):

| Screen | Inert buttons |
|---|---|
| `ecs/scenes/citybuilder/BuildMenu.cs` + `build_menu.tscn:46-56` | Item1/2/3 (House/Factory/Park) |
| `ecs/scenes/strategy/Research.cs` + `research.tscn:53-67` | Tech1..4 |
| `ecs/scenes/strategy/UnitPanel.cs` + `unit_panel.tscn:46-60` | Action1..4 (Move/Attack/Defend/Special) |
| `ecs/scenes/survival/Crafting.cs` + `crafting.tscn:53-63` | Recipe1/2/3 |

Fix: wire each to a `GameStateManager.SetGameData` record (the established pattern) + doc
comment that consuming the choice is the game's job. Label-row screens (Districts, Economy,
Diplomacy, WorldMap, Quests, Codex, Collection) are legitimate scaffolding — leave them.

### 5d. Missing consumers for live signals

- **`SettingsComponent.SettingsChanged`** (`ecs/ui/SettingsComponent.cs:284`) — emitted, no
  consumer; nothing reacts to a live settings change. Minimum: `SettingsMenu` re-reads on it.
- **Achievement toast** — `GameApp.AchievementUnlocked` and
  `BeepAchievementDebug.AchievementUnlocked` (`:40`) both fire with no listener, and the
  framework already ships `ToastNotificationComponent`. Wire an optional achievement toast
  (a small component that subscribes and shows the toast) — the framework's own pieces,
  currently unconnected.
- **`DayNightCycleComponent.TimeOfDayChanged`/`PhaseChanged`** — see Phase 6 (same class of
  gap, atmosphere-owned).

## Gotchas

- 5a must NOT invent per-genre stat tracking (kills/coins/placement) — that's game content.
  The framework binds only what `GameApp`/`GameStateManager` already hold; the rest is a
  documented seam. Guard this line in review.
- The HUD direction choice (5b) affects `GameOverOnDeathComponent`/`GameFlowComponent`
  callers — trace `AddScore` call sites before picking.
- New subscriptions added here must follow Phase 1's hygiene: `-=` in `_ExitTree`
  (GameApp is an autoload — it always outlives subscribers).

## Verify

1. Build + validator.
2. Editor, end-to-end: generate a platformer → play → score points → die → GameOver shows
   the real session score; beat the score → puzzle `LevelComplete` (or GameOver) reflects
   `BestScore` correctly.
3. Unlock a debug achievement → toast appears.
4. Signal audit re-grep: `GameApp` session signals now have ≥1 framework consumer where the
   framework ships the consuming surface (score/achievement); the rest remain documented API.
