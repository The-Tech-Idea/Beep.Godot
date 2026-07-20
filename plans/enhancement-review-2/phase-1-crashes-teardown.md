# Phase 1 — Crashes & teardown safety

## Why first

These are the findings that **throw or corrupt at runtime**, plus the one regression round 1's own
work introduced. Everything else is cosmetic next to a use-after-free.

## The work

### Use-after-free (the one true crash)
1. **ChipComponent** — `ecs/ui/ChipComponent.cs:65-86` — `_chip` is `AddChild`'d to the **parent**
   Container and there is **no `_ExitTree`**. The `closeBtn.Pressed` and `_chip.GuiInput` lambdas
   capture `this` and call `EmitSignal`/`AcceptEvent`. Free the ChipComponent while its parent (and
   thus `_chip`) survives, then click → `ObjectDisposedException` on the freed component. Fix: add
   `_ExitTree` that frees `_chip` (or reparent `_chip` under this node so it dies with it).

### Unguarded sibling `-=` (throws during teardown)
2. **ParticleComponent** — `ecs/ParticleComponent.cs:116` — `_health.Died -= Burst` with no
   `GodotObject.IsInstanceValid(_health)` guard. `_health` is a **sibling**; free order isn't
   guaranteed, so if the HealthComponent disposes first, the `-=` throws. Fix: guard it (mirror
   `RespawnComponent`/`TemperatureComponent`).
3. **SpriteEffectComponent** — `ecs/SpriteEffectComponent.cs:93` — same unguarded `_health.Died -= Play`.
4. Same guard, lower stakes, for the sites the reviews flagged: **BossHealthBarComponent** (`:100-101`),
   **DropTableComponent** (`:58`), **ContextMenuComponent** (`:88-89`), **DragComponent** (`:101-102`),
   **SquashAndStretchComponent** (`:83-94`, also re-fetches the sibling instead of caching it).

### Regression from round 1
5. **TweenComponent.CardHoverPop** — `ecs/TweenComponent.cs:105-109` — `CardHoverPop` hardcodes
   `"offset_transform_scale"` / `"offset_transform_rotation"`. Round 1 added a `bool ctrl` gate that
   only sets `OffsetTransformEnabled` for a Control target — so on a **Node2D** target those
   properties don't exist and the tween **errors at runtime** (the other presets route Node2D through
   raw scale/position, but CardHoverPop was left assuming Control). Fix: route Node2D CardHoverPop
   through raw `scale`/`rotation`, or `OffsetTransformEnabled` doesn't apply to Node2D so guard it.

### Broken re-enable / idempotency
6. **AutoHealComponent** — `ecs/AutoHealComponent.cs:45` — `_onDied = () => IsActive = false`
   permanently disables the component on death and never re-subscribes to `HealthComponent.Revived`,
   so a revived/respawned entity **never regenerates again**. Fix: subscribe `Revived` → re-enable,
   or drop the `IsActive = false` entirely (`_Process` already bails on `_health.IsDead`, so it's
   redundant *and* harmful).
7. **BeepGenreScene.ApplyGenre** — `ecs/BeepGenreScene.cs:82,95,156-173` — public and documented as
   re-runnable ("re-tune mid-game"), but `InstantiateMainScene` `AddChild`s a fresh main-scene copy
   every call → a second call **stacks a duplicate genre layout** (idempotency-rule violation). Fix:
   free/skip an existing `_<Genre>Main` child before instancing, or guard on a meta flag.

## Gotchas

- The `IsInstanceValid` guards are for **collaborators that outlive or free independently** (siblings,
  the player's Health) — not for owned children (tweens, timers), which die with the node.
- CardHoverPop: don't "fix" it by forcing `OffsetTransformEnabled` on Node2D — Node2D has no offset
  transform; it needs the raw property path.
- AutoHeal: confirm `HealthComponent` has a `Revived` signal before wiring; if not, the drop-the-flag
  option is the safe fix.

## Verify

1. Build + validator.
2. Editor: a scene with a `ChipComponent`; free/replace it, then click where the chip was → no
   `ObjectDisposedException` in Output → C#.
3. Kill an entity with `ParticleComponent`+`HealthComponent` and free the scene → no teardown throw.
4. Revive an entity with `AutoHealComponent` → HP regenerates again.
5. Call `BeepGenreScene.ApplyGenre()` twice → one main-scene child, not two.
6. A `TweenComponent` with `CardHoverPop` on a **Node2D** → no runtime error.
