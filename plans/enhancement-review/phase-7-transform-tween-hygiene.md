# Phase 7 — Transform & convention hygiene

## Why

The `offset_transform_*` migration is nearly complete — the sweep confirmed every effect
component (Pulse, Shake, SlideInOut, UIEffect, ThemePreset button anims) uses it correctly.
Four stragglers remain, one of which is a shipped widget whose animation a container
actively fights. Plus small convention debts the sweep surfaced (string-form `CallDeferred`,
dead code, redundant casts) — cheap to clear while the files are open.

## The work

### Container-transform violations (the CLAUDE.md tween rule)

1. **AccordionComponent** — `ecs/ui/AccordionComponent.cs:91,94,100` — expand/collapse
   animates raw `scale` on children **inside its VBox container** — the exact forbidden
   pattern; the container overwrites it every layout pass. Set
   `ctrl.OffsetTransformEnabled = true` and tween `offset_transform_scale`. (BUG — the
   others below are latent.)
2. **SquashAndStretchComponent** — `ecs/SquashAndStretchComponent.cs:60-79` — tweens
   `"scale"` directly; the class explicitly supports a `_targetControl`, and a Control in a
   Container silently never squashes. Route Control targets through `offset_transform_scale`
   (Node2D targets stay as-is).
3. **TweenComponent** — `ecs/TweenComponent.cs:47-84` — most presets tween
   `"scale"`/`"position"` on `GetParent()`; only `CardHoverPop` uses `offset_transform_*`.
   Route the Control-facing presets the same way.
4. **DragComponent** — `ecs/ui/DragComponent.cs:71,88` — drags `_control.Position` directly;
   inside a layout Container the drag silently fights re-layout. A drag genuinely moves the
   control (offset transform is render-only), so here the right fix is a **warning** when the
   dragged Control's parent is a `Container`, not a transform swap.
5. **DialogUIComponent / ModalComponent** — `ecs/ui/DialogUIComponent.cs:427-433,443`,
   `ecs/ui/ModalComponent.cs:66-101` — both animate free overlays (documented, works), but a
   dev reparenting under a Container gets a silent stomp. One-line guard: warn if
   `GetParent() is Container`.

### `CallDeferred` string form (repo prefers `Callable.From(X).CallDeferred()`)

- `ecs/InventoryComponent.cs:95` (`BuildUI`)
- `ecs/SquashAndStretchComponent.cs:29`
- `ecs/TrailComponent.cs:24`
- `ecs/TemperatureComponent.cs:104`
- `ecs/ui/AnimatedMenuComponent.cs:38` (`ShowAnimated`)

All functionally work today; flagged because the trap list exists for a reason (generator
registration dependence) and `EffectComponent`/`InventoryComponent.Load` already model the
preferred form.

### Dead code / redundancy

- `ecs/ui/AnimatedMenuComponent.cs:153-160` — `GetDirectionOffset()` never called; remove.
- `ecs/AttackComponent.cs:33,42` — `_health` field assigned, never read; remove.
- `ecs/AnimalBehaviorComponent.cs:84,115` — redundant `(float)GD.Randf()` casts (the CLAUDE
  trap list itself corrected this belief); also `:65` re-casts an already-typed field.
- `ecs/ui/VignetteComponent.cs:60-74` — `Apply()` clobbers a pre-existing `ci.Material` with
  no restore path; cache and restore like `SkeletonLoaderComponent` does.
- `ecs/ui/ProgressRingComponent.cs:26-30` — `[Tool]` `_Process` lerps + `QueueRedraw()`
  every editor frame (no `IsEditorHint` guard — the round-2 defect class); `ValueChanged`
  fires from `SetValue` but not the `[Export]` setter; and the class extends `Control`
  directly instead of a category base — the only such deviation found. Guard, unify the
  setter, and either re-base or doc-comment why it's special.
- `ecs/ui/DataBinderHostComponent.cs:219 vs 213,226` — `BindingRemoved` emitted only from
  `Unbind(source, prop)`, not `Unbind(source)` or `Clear()`; make the event surface uniform.
- `ecs/ui/LocalizationComponent.cs:143-161` — `ReloadAll()` clears internal sets but
  previously-added `Translation`s stay in `TranslationServer` → duplicates accrete per
  reload. At minimum doc-comment the one-shot-boot assumption.
- `ecs/ui/SaveGameMenuComponent.cs:166-170` — `_ExitTree` detaches Save/Cancel but not the
  per-slot handlers from `WireSlotButtons` (`:122`); harmless, align for consistency.
- `ecs/ui/MenuComponent.cs:86-97` — `OnButtonPressed` resolves the pressed button via
  `GuiGetFocusOwner()` — wrong/no button when a mouse press doesn't move focus. Bind the
  button into each handler closure (`btn.Pressed += () => OnButtonPressed(btn)`).

## Gotchas

- Offset transforms are relative to the laid-out position — neutral is `Vector2.Zero`/`Vector2.One`,
  nothing to capture/restore (see CLAUDE.md § offset transform layer). Delete any
  original-position bookkeeping the migrated code carried.
- `MenuComponent`'s fix touches the action-dispatch path every menu uses — retest the full
  main-menu → game → pause loop, not just one button.

## Verify

1. Build + validator.
2. Editor: an `AccordionComponent` inside a themed VBox → sections animate open/closed
   visibly (before the fix the container snaps them back).
3. Menu loop end-to-end after the `MenuComponent` change (click with mouse, then keyboard).
4. Grep gate: `TweenProperty(.*, "scale"`/`"position"` over `ecs/ui/` + the two gameplay
   files → remaining hits are Node2D-only paths.
