# Audit: Component Gaps + App/Scene Integration

> Comprehensive pass over the addon finding everything that is NOT yet a `[GlobalClass]`
> component and what's missing from the app-creation / scene-management flow.
> Generated 2026-07-11.

## Priority 1 — Kill the split-brain (GDScript vs C# autoloads)

The genre generator STILL registers GDScript autoloads that conflict with the C# components:

| GDScript autoload (registered) | C# equivalent (already exists) | Action |
|---|---|---|
| `scene_manager.gd` | `NavigationComponent` + `SceneTransitionComponent` | Retire GDScript; NavigationComponent is the sole scene system |
| `game_manager.gd` | `GameFlowComponent` + `GameApp` | Retire GDScript; GameFlow + GameApp cover it |
| `save_manager.gd` | (needs `SaveManagerComponent` — see P2) | Port to C# component |
| `audio_manager.gd` | `AudioComponent` (partial) | Consolidate into `AudioManagerComponent` |

**Files to change:** `BeepGenreGenerator.cs` (remove GDScript autoload registration), `genre_templates.json` (remove `autoloads`/`scripts` arrays), `BeepScriptGenerator.cs` (stop generating manager .gd files).

---

## Priority 2 — Missing C# components (no equivalent exists)

These `.gd.template` behaviors have NO C# component — create them:

| Component | Behavior (from template) | Priority |
|---|---|---|
| `CheckpointComponent` | Area2D respawn point, activate/heal on touch | High (platformer) |
| `MovingPlatformComponent` | AnimatableBody2D waypoints/loop/pause | High (platformer) |
| `DoorSwitchComponent` | Switch/lever + key-gated door | Medium (adventure) |
| `TurretComponent` | Aim + fire at player + LOS | Medium (shooter) |
| `WeatherSystemComponent` | Rain/snow/storm/fog particle cycle | Medium |
| `DayNightCycleComponent` | Ambient light cycle + dawn/dusk signals | Medium |
| `ObjectPoolComponent` | PackedScene instance pooling (consolidate BeepPoolManager<T>) | High |
| `SaveManagerComponent` | Save/load game state + scene-state snapshot/restore | High |
| `DialogUIComponent` | Dialog textbox UI (port dialog_system.gd) | Medium (adventure) |
| `ProjectileModifierComponent` family | Homing/Bounce/Spread modifiers | Low |

---

## Priority 3 — Consolidate duplicated core↔ecs logic

Static classes in `core/` that duplicate existing ecs/ components — consolidate:

| core/ static class | ecs/ component already exists | Action |
|---|---|---|
| `BeepStateMachine` | `StateMachineComponent` | Delete core version; use component |
| `BeepDataBinder` | `DataBinderHostComponent` | Move logic into the component host |
| `BeepKeybindManager` | `KeybindManagerComponent` | Move logic into the component |
| `BeepAudioManager` | `AudioComponent` | Consolidate into `AudioManagerComponent` |
| `BeepCoroutine` | (none yet) | Create `CoroutineHostComponent` |
| `BeepConfigManager` | (none yet) | Create `ConfigManagerComponent` |
| `BeepLocalization` | (none yet) | Create `LocalizationComponent` |

---

## Priority 4 — App lifecycle layer (doesn't exist at all)

No boot/splash/loading/app-state components exist. Create:

| Component | Purpose |
|---|---|
| `BootComponent` | Loads game_info.tres, initializes GameApp, applies settings, transitions to menu |
| `SplashComponent` | Logo/title card with timed fade |
| `LoadingScreenComponent` | Progress bar while a heavy level packs (async) |
| `AppStateComponent` | Boot→Menu→Loading→Game→GameOver state machine driving NavigationComponent |

---

## Priority 5 — Scene management gaps

What `NavigationComponent` + `SceneTransitionComponent` are missing:

1. **Additive scene loading** — no `load_scene_additive` support
2. **Loading screen** — no progress-bar intermediate scene
3. **Scene pooling** — no PackedScene cache
4. **Scene state save/restore** — no per-scene snapshot (collected items, dead enemies)
5. **Scene history stack** — no "return to previous scene"
