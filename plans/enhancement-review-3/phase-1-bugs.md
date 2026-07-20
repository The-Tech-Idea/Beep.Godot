# Phase 1 — Real bugs

## Why first

Three genuine defects plus three orphan/leak cases the round-2 sweep missed. The precipitation bug
is the standout: it means weather particles have **never actually rendered** at steady state, and it
silently defeated round-2's per-type `Lifetime` fix.

## The work

1. **Precipitation `Amount` reallocated every frame** — `ecs/atmosphere/WeatherSystemComponent.Intensity.cs:121-122`
   — `_particles.Amount` is written **every frame** while emitting. Godot 4's `CpuParticles2D.set_amount`
   reallocates and deactivates the entire particle pool on each call (no same-value early-out), so at
   steady state (intensity pinned) the pool resets every frame — precipitation flickers at the emission
   strip and never falls or accumulates. This also nullifies round-2's `Lifetime` fix. Fix: cache the
   last applied amount, assign only on change:
   `int a = Mathf.Max(1, (int)(ParticleCount * _intensityCurrent)); if (a != _lastAmount) { _particles.Amount = a; _lastAmount = a; }`.
   **Verify against round-2's Lifetime change** — after this, slow precip should visibly fall across
   the viewport.
2. **Toast queue never drains dismissed toasts** — `ecs/ui/ToastNotificationComponent.cs:88-89,100`
   — the `Finished` lambda `QueueFree`s a naturally-dismissed toast but never removes it from
   `_activeToasts`. The next `ShowToast` runs `foreach (var t in _activeToasts) t.Position += …` over
   a **disposed** node → `ObjectDisposedException`. Reproduces whenever a toast self-dismisses while
   fewer than `MaxVisible` are up (the `while (Count > MaxVisible)` cap is the only remover and doesn't
   fire then). Freed entries also inflate the count, evicting real toasts early. Fix: switch
   `_activeToasts` from `Queue<Control>` to `List<Control>` and `Remove(toast)` in the `Finished`
   lambda (the round-2 `_toastTweens` tracking is unaffected — orthogonal).
3. **Inventory pollutes the scene at editor design time** — `ecs/InventoryComponent.cs:90-96` — `_Ready`
   is **not** gated on `Engine.IsEditorHint()`, so the deferred `BuildUI()` injects a `GridContainer`
   + tooltip `PanelContainer` into the parent Control at edit time, with `Owner` set
   (`InventoryComponent.Display.cs:34,166`) — so a scene save **serializes** them and every reopen
   re-adds more. This is the exact editor-pollution class `ParticleComponent`/`TrailComponent`/
   `SpawnerComponent` were guarded against; Inventory was missed. Fix: `if (Engine.IsEditorHint()) return;`
   before the `CallDeferred(BuildUI)` (still allocate `Slots` first, so runtime logic is intact).
4. **`ToggleSwitchComponent._bg` orphaned** — `ecs/ui/ToggleSwitchComponent.cs:82-87` — `_bg`
   (a `ColorRect` `AddChild`'d to the parent Button at `:60`) is not freed in `_ExitTree` (only the
   tween is killed and `Toggled` disconnected). The exact round-2 leftover-child pattern (Chip/Badge/
   Combo/BuffBar) — a peer the sweep missed. Fix: free `_bg` (guarded) + null it.
5. **`DialogUIComponent._panel` orphaned** — `ecs/ui/DialogUIComponent.cs:463-471` — `_panel`
   (`PanelContainer` added to the parent at `:123`) not freed in `_ExitTree`. Lower impact (dialog
   usually dies with its host) but the same pattern. Free `_panel` if valid.
6. **`RatingComponent` interactive-star handlers leak** — `ecs/ui/RatingComponent.cs:63-74` — the
   `GuiInput`/`MouseEntered`/`MouseExited` lambdas capture `this` and are attached to `Label`s that are
   children of the **parent** Container, with **no `_ExitTree`** to disconnect. If the component alone
   is freed, a later interaction fires on the freed component. Store disconnectors and detach in an
   `_ExitTree` (Stepper/Table/TabGroup are the pattern). Add `base._ExitTree()` there too (Phase 2).

## Gotchas

- Precipitation: keep the `Mathf.Max(1, …)` floor (Amount 0 disables emission); reset `_lastAmount`
  to a sentinel (-1) when the emitter is (re)built so the first real frame applies.
- Toast: the `Queue`→`List` switch touches the stacking loop and the `MaxVisible` eviction — keep
  both working (evict oldest = `RemoveAt(0)`).
- Inventory: the `IsEditorHint` guard must come **after** `Slots = new …` if any editor code path
  reads `Slots`; check what `_Ready` does before `CallDeferred`.

## Verify

1. Build + validator.
2. Editor: rain/snow in a scrolling level → particles actually fall and cover the viewport (not a
   flickering top strip). This is the payoff of both this fix and round-2's Lifetime change.
3. Fire several toasts, let one self-dismiss, fire another → no `ObjectDisposedException`.
4. Open a scene with an `InventoryComponent`, save, reopen → no injected `InventoryGrid`/tooltip
   nodes accreting in the saved scene.
5. Free a `ToggleSwitch`/`Rating` while its parent survives → no orphan, no callback on freed node.
