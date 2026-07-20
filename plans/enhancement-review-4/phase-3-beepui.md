# Phase 3 — beep_ui GDScript effects & widgets

The entire `beep_ui` GDScript addon (29 `.gd`) was **never reviewed** by any prior round — those stayed in
the C# addon. It came back structurally sound (offset_transform rule followed consistently, 22 presets
uniform, theme_studio editor-safe) with three real effect/widget bugs and a blank-preview polish.

> **No `dotnet` gate applies to `.gd` files.** These are verified by reading + an editor F5 pass. The
> C# build/validator still must stay green (no C# touched here).

## Fixes

### 1. `effects/ui_effect.gd` — TYPEWRITER never completes, re-runs forever
- **`:110-111,445-459`** — `_play_typewriter` spawns no tween, so the `size() > tween_base` gate (`:259`)
  is never true, `effect_completed` never emits, and `_process_typewriter` **re-assigns the label's full
  text every frame forever** (finished entries are never removed from `_tw_states`). Fix: when
  `visible_n >= total`, emit `effect_completed`, drop that entry, and stop processing once `_tw_states`
  is empty.

### 2. `effects/ui_effect.gd` — BOB never signals done, leaves an offset
- **`:251,259`** — BOB spawns no tween → `effect_completed` never fires, and after `stop()` the target is
  left at its last bob offset (`stop()` doesn't zero it; only `reset()` does). Zero
  `offset_transform_position` in `stop()` for the bob case.

### 3. `widgets/toast_host.gd` + `widget_factory.gd` — factory toasts spawn off-position
- **`toast_host.gd:39-42,73`** — the toast X is centered on the **viewport** width, but the toast is a
  child of the host in host-local space — only correct if the host fills the screen at the origin.
  The factory host (`widget_factory.gd:291-292`) is built with a fixed `custom_minimum_size(320,120)` and
  **no full-rect anchors**, so a factory-dropped toast host mispositions its toasts. Fix: anchor the host
  `PRESET_FULL_RECT` in the factory **and** position toasts relative to the host `size`
  (`(size.x - toast_size.x) * 0.5`) so it's correct regardless of host bounds.

### 4. Polish
- **`theme_studio.gd:36-39`** — `_ready` calls `_refresh_theme_grid("")` but never `_update_preview()`,
  so the preview is **blank until the user clicks**, despite `_selected_preset = "Modern"` default. Call
  `_update_preview()` at the end of `_ready`.
- **`theme_applier.gd:410-413`** — hover/press handlers connected, never explicitly disconnected
  (`_exit_tree` only clears the dict). Godot auto-severs on free, but a reparent-while-alive keeps stale
  callbacks. Disconnect in `_exit_tree`.
- **`ui_effect.gd:328-334`** — an infinite PULSE (`pulse_loops <= 0`) with `looping = true` never ticks
  `effect_looped` (an infinite tween never emits `finished`). Note it; not worth restructuring.
- **`ui_effect.gd:101`** — `@tool` + `_process` runs every editor frame (early-returns on `!_is_playing`).
  `set_process(false)` until `play()` is cleaner; low priority.

## Verify
- No C# touched → `dotnet build` still 0 errors, `validate_scenes.sh` still PASS.
- Editor F5: drop a Typewriter effect → it types once and stops (no CPU spin); a factory toast host →
  toasts appear centered; open Theme Studio → the preview is populated on open.
