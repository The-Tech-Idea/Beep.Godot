# Phase 4 — Silent-failure sweep + input gates

## Why

"Never fail silently" is the repo's #1 rule because silent nulls hid most of its historical
defects — and the sweep found the class still alive in ~14 places. Every item here is the
same one-to-three-line fix: `GD.PushWarning` naming *what it got, what it needed, what to do*.
Plus 6 components that poll input actions without the `ControllerComponent.InputActionsAvailable`
gate, spamming per-frame "action doesn't exist" errors in any project that hasn't generated
its input map yet.

## The work

### Missing warnings on null resolves (parent/sibling/path)

| Component | Site | What goes silent |
|---|---|---|
| `AggroComponent` | `ecs/AggroComponent.cs:32` | non-Node2D parent → deaggro leash never applies |
| `AnimalBehaviorComponent` | `ecs/AnimalBehaviorComponent.cs:50-52` | missing `SeasonalComponent` or non-CharacterBody2D parent → animal fully inert |
| `AttackComponent` | `ecs/AttackComponent.cs:43` | non-Node2D parent → attack does nothing **but still emits `Attacked`** (also suppress that misleading emit) |
| `AutoHealComponent` | `ecs/AutoHealComponent.cs:34,64` | no sibling `HealthComponent` → silent no-op despite doc claim |
| `CameraZoomComponent` | `ecs/CameraZoomComponent.cs:28` | non-Camera2D parent → every zoom call no-ops (doc promises "attach to any Camera2D") |
| `FollowTargetComponent` | `ecs/FollowTargetComponent.cs:29-30` | non-Node2D parent / unresolved `TargetPath` → never follows |
| `HealthBarComponent` | `ecs/HealthBarComponent.cs:38-39` | no sibling `HealthComponent` → bar never appears |
| `HungerStaminaComponent` | `ecs/HungerStaminaComponent.cs:60` | non-body parent → movement-based drain silently off |
| `AudioComponent` | `ecs/AudioComponent.cs:16,42` | `AutoPlay` with null `Stream` plays nothing (no default asset exists — warn) |
| `BeepGenreScene` | `ecs/BeepGenreScene.cs:105` | missing GameApp autoload → all GameInfo wiring skipped, comment literally says "skip silently" |
| `GameInfoBinder` | `ecs/ui/GameInfoBinder.cs:55,59,64,68` | a **set but unresolvable** NodePath drops the binding — warn in the else branch of each non-empty path |
| `DataBinderHostComponent` | `ecs/ui/DataBinderHostComponent.cs:111` | `Bind()` with null source/target → nothing, no log |
| `HudComponent` | `ecs/ui/HudComponent.cs:42-66` | no GameFlow / no player / no HealthComponent → HUD binds nothing (doc says "silently skipped" — change the doc too) |
| `Match3BoardComponent` | `ecs/ui/Match3BoardComponent.cs:110,117-124` | no `GameFlowComponent` resolves → `?.AddScore` no-op, HUD stays 0 (warn once) |
| `DialogUIComponent` | `ecs/ui/DialogUIComponent.cs:219-221` | no `ThemePresetComponent` → silent white fallback |
| `AccordionComponent` | `ecs/ui/AccordionComponent.cs:39` | Container parent with no children → silent return |

### Player lookup by hardcoded name

- **DoorSwitchComponent** — `ecs/DoorSwitchComponent.cs:60` — finds the player via
  `FindChild("Player", false, false)`; any other node name fails the `RequiredItem` gate
  silently (the exact FindChild-by-name trap `EntityComponent`'s own doc warns against).
  Resolve through the `players` group; warn when the group is empty.

### Missing `InputActionsAvailable` gates (per-frame error spam pre-generation)

The three controllers and `FlyComponent` already gate; these six poll raw `Input.*`:

| Component | Site |
|---|---|
| `JumpComponent` | `ecs/JumpComponent.cs:73,78` (also consider exporting the hardcoded `"jump"` action name) |
| `SlideComponent` | `ecs/SlideComponent.cs:85` |
| `WallJumpComponent` | `ecs/WallJumpComponent.cs:120` |
| `DashComponent` | `ecs/DashComponent.cs:94,100-101` |
| `GlideComponent` | `ecs/GlideComponent.cs:48,62` |
| `HoverComponent` | `ecs/HoverComponent.cs:51` |

## Gotchas

- Warn **once** (in `_Ready` or a latched flag), not per frame — a warning spammed at 60 Hz
  is its own failure mode. `Match3BoardComponent` and `HudComponent` resolve lazily, so latch.
- `AttackComponent`: the fix is two-part — warn on the null parent AND stop emitting
  `Attacked` when nothing actually fired (a listener counting attacks gets lied to today).
- Don't add warnings to *optional* seams that already have working defaults — the rule
  targets features that silently disable, not every null check.

## Verify

1. Build + validator.
2. Editor: drop each listed component under a deliberately-wrong parent in a scratch scene →
   exactly one warning naming the component, the expectation, and the remedy.
3. Run `player_template.tscn` directly in a project with no generated input map → zero
   per-frame action errors in Output.
4. Class-check: grep the fixed files for `as Node2D`/`as Control`/`GetParent() as` followed
   by bare `return` — should come back empty for the listed sites.
