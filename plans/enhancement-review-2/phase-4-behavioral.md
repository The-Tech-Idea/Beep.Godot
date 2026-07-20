# Phase 4 — Behavioral correctness

## Why

Components that run but do the wrong thing — a public method that can't do what its name says, a
poll loop that skips half its bindings, an editor-time rebuild, a config that ignores an option.

## The work

1. **MovingPlatformComponent** — `ecs/MovingPlatformComponent.cs:123` — `Once`-mode completion sets
   `IsActive = false`, but `_PhysicsProcess` gates on `!IsActive`, so a later `Start()` (which only
   sets `_running = true`) **can never resume it** — round-1's own `Start`/`Stop` contract is broken
   for Once platforms. Fix: stop via `_running = false` (consistent with `Stop()`), not the category
   `IsActive` flag.
2. **DataBinderHostComponent** — `ecs/ui/DataBinderHostComponent.cs:98-107,190-217` — `_Process`
   only calls `RefreshAll()` (OneWay/OneWayToSource); **TwoWay bindings are never polled**, so
   `BindCheckBox` (defaults `TwoWay`) never writes UI edits back to the source unless a caller manually
   invokes `RefreshTwoWay()`. Fix: poll two-way in `_Process` (or a separate cadence), or change the
   default and document two-way as pull-only. Also `RefreshProperty` (`:214`) handles only `OneWay`,
   silently dropping `OneWayToSource` — align with `RefreshAll`.
3. **Match3BoardComponent** — `ecs/ui/Match3BoardComponent.cs:87,39-56` — `async void ResolveCascades`
   contains **no `await`** (CS1998): the whole clear→gravity→refill cascade runs in one frame with no
   animation beat. And `_Ready` runs `InitGrid()` + the GameApp difficulty ramp with **no
   `Engine.IsEditorHint()` guard** (siblings have it) → the board rebuilds in the editor. Fix: drop
   `async` (or add per-step awaits for the cascade beat) and add the editor guard.
4. **MenuComponent** — `ecs/ui/MenuComponent.cs:122-133` — `_ExitTree` disconnects
   `ActionTriggered -= nav.Dispatch` but leaves `_navWired = true`. `WireButtons()` is public; a
   remove-then-re-add + re-`WireButtons()` skips the `if (!_navWired …)` nav-rewire block → buttons
   re-wire and emit `ActionTriggered`, but **nothing dispatches** to Navigation. Fix: set
   `_navWired = false` in `_ExitTree`.
5. **CooldownComponent** — `ecs/CooldownComponent.cs:51` — `Reset()` ("force the cooldown to end
   immediately") zeroes `_timer` but doesn't emit `CooldownReady`, so listeners gated on that signal
   never learn the ability is available after a forced reset. Emit `CooldownReady` (and optionally
   `CooldownProgress(1)`).
6. **ModalComponent** — `ecs/ui/ModalComponent.cs:46,62-64` — a `StartVisible = true` modal leaves
   the dialog visible in `_Ready` but never builds the dark overlay, and `Open()` early-returns
   (already visible), so a start-visible modal has **no overlay** and `CloseOnOverlayClick` is dead
   until it's been Closed once. Fix: build the overlay in `_Ready` when `StartVisible`, or route
   `StartVisible` through `Open()`.
7. **TweenComponent.Pulse** — `ecs/TweenComponent.cs:92` (**Decision A**) — `SetLoops(0)` means
   **loop infinitely** in Godot 4, so a Pulse tween never completes and `TweenFinished` never fires.
   If perpetual is intended, document it; if a single pulse was intended, drop `SetLoops`.
8. **AIController** — `ecs/AIController.cs:80` (**Decision B**) — the proactive-chase gate
   (`Mode != Chase && Mode != Flee`) also fires for `Idle`, so an `Idle`/decorative NPC chases any
   target within `DetectionRange` (this is why `robot_npc_template` follows the player). Add
   `Mode != AIMode.Idle` to the condition, or a `bool AutoAcquire` export.

## Gotchas

- MovingPlatform: also re-check `RunCompleted` still fires once on the Once completion after switching
  from `IsActive` to `_running`.
- DataBinder two-way: polling two-way every frame can fight a one-way binding on the same property —
  keep the existing OneWay-wins precedence.
- Match3 `async void` → sync: confirm no caller `await`s it (it's `void`, so none can).

## Verify

1. Build + validator.
2. Editor: a `Once` platform → completes → `Start()` → moves again.
3. `BindCheckBox` two-way → toggling the checkbox writes back to the source object.
4. Open a Match3 scene in-editor → board does not rebuild; run it → cascade has a visible beat (if
   awaits added).
5. Remove+re-add a `MenuComponent` → buttons still navigate.
6. `Idle` AI NPC → does not chase (per Decision B).
