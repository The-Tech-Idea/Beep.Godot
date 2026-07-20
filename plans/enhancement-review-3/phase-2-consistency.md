# Phase 2 — Consistency sweeps

## Why

Two things round-2's sweeps applied unevenly. Both are mechanical and low-risk; doing them makes the
lifecycle discipline uniform so the next reviewer (and the next bug) has one rule, not exceptions.

## Sweep A — unguarded sibling `_health -=`

Round-2 added `base._ExitTree()` to five components but left their sibling `_health` unsubscribe
**unguarded** — the exact `IsInstanceValid` guard the repo applies everywhere else (AutoHeal, DropTable,
Particle, SpriteEffect, Destructible, DespawnOnDeath, GameOverOnDeath, BossHealthBar). A sibling
`HealthComponent` freed a frame earlier makes the bare `-=` throw `ObjectDisposedException` out of
`_ExitTree`. Add `&& GodotObject.IsInstanceValid(_health)` to each:

- `ecs/FlashComponent.cs:70` — `_health.Damaged -= _damagedHandler`
- `ecs/HealthBarComponent.cs:96` — `_health.HealthChanged -= OnHealthChanged`
- `ecs/HitSparkComponent.cs:79` — `_health.Damaged -= OnDamaged`
- `ecs/HitStopComponent.cs:75` — `_health.Damaged -= OnDamaged`
- `ecs/HitSoundComponent.cs:92` — `_health.Damaged -= OnDamaged`

## Sweep B — `base._ExitTree()` still missing (~19 overrides)

Round-2's base-call pass reached the gameplay `ecs/` files but missed most `ui/` overrides (and two
others). `EntityComponent._ExitTree` (`EntityComponent.cs:79-86`) removes the node from its
`ComponentGroup` and the **parent** from `EntityGroup`; skipping it leaves stale group memberships (a
group-based `GetNodesInGroup` lookup can then see a detached component). Each file still does its own
cleanup, so this is a correctness/consistency gap, not an active leak. Add `base._ExitTree();` (usually
first line) to each:

`ecs/ui/`: AnimatedMenuComponent.cs:152, CarouselComponent.cs:147, AccordionComponent.cs:128,
ToggleSwitchComponent.cs:82, BossHealthBarComponent.cs:98, BuffBarComponent.cs:107, DragComponent.cs:98,
ContextMenuComponent.cs:84, FlipCardComponent.cs:81, DialogUIComponent.cs:463, SaveGameMenuComponent.cs:208,
LoadGameMenuComponent.cs:204, SearchBarComponent.cs:103, TableComponent.cs:185, StepperComponent.cs:76,
MenuComponent.cs:115, SkeletonLoaderComponent.cs (verify — reviews disagreed; only add if its `_ExitTree`
lacks the call).
`ecs/`: TurnManager.cs:45 (extends `Node` — the base call is a harmless no-op, but add for uniformity;
it already nulls its `Instance` singleton, keep that).

**Cross-check with Phase 1:** ToggleSwitch, DialogUI, and Rating (new `_ExitTree`) all appear in both
phases — add the base call when you add their orphan-free / handler-disconnect in Phase 1, so each file
is touched once.

## Gotchas

- Verify each file **actually** lacks `base._ExitTree()` before adding it (a couple were reported by
  only one reviewer — grep each first). Do not double-add.
- `base._ExitTree()` position: first line is safest (group removal is independent of the override's own
  `-=`/tween-kill/QueueFree, so order doesn't matter, but first-line is the established convention).
- Some of these classes extend `UIComponent`/`EffectComponent`, whose `_ExitTree` chains to
  `EntityComponent` — calling `base._ExitTree()` reaches the group cleanup transitively. Correct.

## Verify

1. Build + validator.
2. Grep gate: `grep -L "base._ExitTree" <each listed file>` → empty (all chain up); and no file has
   two `base._ExitTree()` calls.
3. A component in a `ComponentGroup` that's removed (not the whole scene) → `GetNodesInGroup` no longer
   returns it.
