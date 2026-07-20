# Phase 3 — Signal edge-triggering & double-emit

## Why

Signals that fire when they shouldn't: re-emitting a one-time event on every subsequent call, or
double-firing the same edge from two paths. A listener that plays a sound / shows a banner / awards a
reward gets triggered repeatedly. Round 1 fixed the per-frame spam class (TopDown `Stopped`, etc.);
this is the remaining re-emit/double-emit tail.

## The work

1. **GameFlowComponent** — `ecs/GameFlowComponent.cs:82-83` — `AddScore` re-emits `LevelComplete`
   on **every call** once `Score >= TargetScore` (not edge-triggered). Any scoring after the target
   re-fires completion. Latch: track a `_levelCompleteEmitted` flag, emit only on the crossing.
2. **HungerStaminaComponent** — `ecs/HungerStaminaComponent.cs:222-223` — `TryConsumeStamina` emits
   `StaminaCritical` **directly**, which can double-fire alongside the latched `CheckCriticalLevels`
   emission on the next `_Process`. Route it through the `_staminaCriticalActive` latch so it stays
   edge-triggered.
3. **ToggleSwitchComponent** — `ecs/ui/ToggleSwitchComponent.cs:43` — `SetState(_checkbox.ButtonPressed)`
   in `_Ready` runs the full path including `EmitSignal(Toggled, …)`, firing a spurious `Toggled`
   at construction before any user interaction. Set the initial visual state **without** emitting
   (inline the knob/bg positioning, or guard the emit behind an "initialized" flag).
4. **AchievementToastComponent** — `ecs/ui/AchievementToastComponent.cs:18-19,29-37` (**Decision C**)
   — both `ListenToGameApp` and `ListenToAchievementSystem` default true and the two sources are
   decoupled, so a game recording an unlock in **both** (`GameApp.UnlockAchievement` + 
   `BeepAchievementSystem.Unlock`) fires **two toasts** for one achievement. Options: dedupe by
   achievement id within a short window, or default only one listener on and document that a game
   picks its source. (The null-GameApp warning for this component is Phase 5.)

## Gotchas

- `GameFlow.LevelComplete` latch must **reset** when a new level starts (`ResetForNewLevel`/level
  load), or the second level can never complete. Find the reset path before latching.
- `HungerStamina`: the latch (`_staminaCriticalActive`) already exists for `CheckCriticalLevels` —
  route `TryConsumeStamina` through the *same* flag, don't add a second one.
- Don't suppress the *first* legitimate emit while fixing the double — verify the edge still fires
  once.

## Verify

1. Build + validator.
2. Editor: connect a print to `LevelComplete`, exceed `TargetScore`, keep scoring → fires once.
3. Drain stamina to critical via `TryConsumeStamina` then let `_Process` run → `StaminaCritical`
   fires once, not twice.
4. Instantiate a `ToggleSwitchComponent` with a listener attached at once → no `Toggled` at startup.
5. Unlock one achievement via both sources → one toast (per the Decision C resolution).
