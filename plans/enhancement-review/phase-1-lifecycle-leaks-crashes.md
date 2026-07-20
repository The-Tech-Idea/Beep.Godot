# Phase 1 — Lifecycle: leaks, orphans, crash paths

## Why first

These are the findings that **crash or corrupt at runtime**, not just mislead. The headline:
`HudComponent` is the *only* unbalanced signal subscription in the entire repo (the audit
verified every other subscriber pairs `+=` with `-=` in `_ExitTree`) — and it ships in four
genre mains, so every generated platformer/shooter/topdown/puzzle project crashes on the
first damage/score event after a scene change frees the HUD.

## The work

### Dangling-handler crashes

1. **HudComponent** — `ecs/ui/HudComponent.cs:45-46,63` — subscribes
   `flow.ScoreChanged`, `flow.LivesChanged`, `health.HealthChanged` with no `_ExitTree` and
   no stored references. The player's `HealthComponent` and the scene `GameFlowComponent`
   outlive an overlay HUD; a freed HUD keeps receiving callbacks → crash on freed labels.
   Fix: store `_flow`/`_health` fields, disconnect both in `_ExitTree`.
2. **SafeAreaComponent** — `ecs/ui/SafeAreaComponent.cs:29` —
   `GetViewport().SizeChanged += Apply`, never disconnected, no `_ExitTree`; the viewport
   outlives the HUD. Add `_ExitTree` with the `-=`. Also `:26-29`: after warning that the
   parent isn't a Control it *still* subscribes and calls `Apply()` — `return` after the warning.

### Orphaned nodes / stuck state on free

3. **ModalComponent** — `ecs/ui/ModalComponent.cs` (no `_ExitTree` override) — freed while
   open: `_tween` never killed and `_overlay` (parented to the dialog's *parent*) is left
   onscreen **blocking all input**. Add `_ExitTree`: kill `_tween`, free `_overlay`.
4. **SlideComponent** — `ecs/SlideComponent.cs:99-100,108` — `StartSlide` shrinks the
   **shared** `RectangleShape2D.Size` and restores only in `EndSlide`; freed mid-slide, the
   shared shape stays shrunk for every other instance using that resource. Restore in
   `_ExitTree` (or make the shape unique before mutating).
5. **WallJumpComponent** — `ecs/WallJumpComponent.cs:60-81` — `SetupWallRays` injects
   `WallRayLeft`/`WallRayRight` into the body, never removed; detaching orphans them and
   re-adding duplicates them. Free the rays it created in `_ExitTree`.
6. **InventoryComponent.Display** — `ecs/InventoryComponent.Display.cs:33,153` —
   `_grid` and `_tooltipPanel` are added under `GetParent()` and never freed when the
   component is removed. Free both in `_ExitTree`.
7. **TableComponent** — `ecs/ui/TableComponent.cs:143-149,185-192` — rows live in the parent
   VBox with `this`-capturing `GuiInput`/hover lambdas; `_ExitTree` disconnects only header
   handlers. Track rows and free them (or parent rows under a node the component owns).

### Crash path in save/load

8. **SaveLoadManagerComponent** — `ecs/ui/SaveLoadManagerComponent.cs:119,146` —
   `scene.Instantiate<SaveGameMenuComponent>()` / `<LoadGameMenuComponent>()` **throws
   InvalidCastException** on a wrong prefab root, making the following `if (x == null)`
   guard unreachable (the `GetNode<T>` trap in generic form). Instantiate untyped,
   `as`-cast, `GD.PushError` on mismatch.
   Also `:101,130`: `ShowSaveMenu`/`ShowLoadMenu` are public and non-idempotent — repeated
   calls stack overlays. Guard against an already-open menu (idempotency rule).

### Tween/handler cleanup (consistency tier — same pattern, lower stakes)

9. **SlideInOutComponent** — `ecs/ui/SlideInOutComponent.cs:37,115` — no `_ExitTree` kill of
   `_activeTweens`; the `Finished` lambda captures `this` and runs on a freed component when
   targets outlive it.
10. **ComboCounterComponent** — `ecs/ui/ComboCounterComponent.cs:73` — `_punchTween` not
    killed on exit (node-bound so low risk; fix for consistency with every sibling).
11. **ThemePresetComponent** — `ecs/ui/ThemePresetComponent.cs:669-735` —
    `SetupButtonAnimations` attaches six handlers per button, never removed; `_ExitTree`
    kills tweens only. Store and disconnect (re-injection is already meta-guarded — good).

## Gotchas

- In `_ExitTree`, publishers may already be freed — wrap disconnects in
  `GodotObject.IsInstanceValid` checks where the publisher is another entity (HudComponent's
  `_health` especially).
- ModalComponent's `_overlay` lives on the *parent*, so `QueueFree` on the component won't
  take it along — that's the whole bug; don't "fix" it by reparenting and breaking dim-layer z-order.

## Verify

1. Build + validator (standard gates).
2. Editor: run `platformer_main.tscn`, take damage, `ChangeScene` to main menu, return, take
   damage again → **no ObjectDisposedException** in Output → C#.
3. Open a modal, free its scene while open → overlay gone, input works.
4. Save menu: call `ShowSaveMenu()` twice → one overlay.
