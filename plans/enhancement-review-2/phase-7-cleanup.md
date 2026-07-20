# Phase 7 — Cleanup & consistency

## Why

The low-risk tail: consistency fixes, dead declarations, one stale doc line, and small polish. None
crash; all reduce confusion and match the patterns the earlier phases established. Do them last.

## The work

### `base._ExitTree()` chaining (skips `EntityComponent` group deregister)
These override `_ExitTree` without calling `base._ExitTree()`, so the base's `ComponentGroup`/
`EntityGroup` removal is skipped. Low impact (Godot auto-removes freed nodes from groups; `_EnterTree`
re-adds idempotently on reparent) but inconsistent with the components that chain up. Add
`base._ExitTree();`:
- `ecs/AudioComponent.cs:86`, `ecs/FlashComponent.cs:66`, `ecs/HealthBarComponent.cs:93`,
  `ecs/HitSparkComponent.cs:76`, `ecs/HitStopComponent.cs:68`
- `ecs/ParticleComponent.cs:114`, `ecs/PickupComponent.cs`, `ecs/ProjectileComponent.cs`,
  `ecs/SquashAndStretchComponent.cs`, `ecs/TrailComponent.cs`, `ecs/TweenComponent.cs`

### Dead declarations / stale references
- **AnimatedMenuComponent** — `ecs/ui/AnimatedMenuComponent.cs:20` — `[Export] AnimateExit` is
  declared but never read (`HideAnimated` is never auto-invoked). Wire it (call `HideAnimated` on
  close) or delete the export.
- **IGameStateable.cs** — the file declares `ISaveable` + `SaveableHelper`; **no `IGameStateable` type
  exists** anywhere. Rename the file to `ISaveable.cs` (and its `.uid` — keep them paired).
- **DataBinderHostComponent.md:108** — stale doc line lists the deleted `[Signal] BindingRefreshed`.
  Delete the line (markdown, harmless, but now lying).
- **DropTableComponent** — `ecs/DropTableComponent.cs:130,131,150` — redundant `(float)GD.Randf()`
  casts (`GD.Randf()` already returns `float`; round 1 removed these elsewhere, missed here). Drop.

### Small polish
- **ColorPalette** — `ecs/ui/ColorPalette.cs:79-86` — `ByName(null)` throws on `ToLowerInvariant()`.
  Guard null/empty.
- **KeybindManagerComponent** — `ecs/ui/KeybindManagerComponent.cs:108-115` — `Rebind` updates `Key`
  but not `Modifiers`, so a rebind can't change/clear the chord. Include modifiers.
- **ScreenShakeComponent** — `ecs/ScreenShakeComponent.cs:51` — `_Process` has no `!IsActive` guard
  (in-flight shake continues after deactivation); `AddToGroup("screen_shake")` never removed on exit;
  `_cam.Offset` not reset. Minor.
- **JumpComponent** — `ecs/JumpComponent.cs:124` — `ForceJump` skips the `!IsActive` check other paths
  honor.
- **InventoryComponent** — `ecs/InventoryComponent.cs:153-161` — a partial add (some fit, then full)
  emits `InventoryFull` but not `ItemAdded` for the portion that fit → pickup listeners undercount.
  Emit `ItemAdded(originalQuantity - quantity)` before returning false.
- **SettingsComponent** — `ecs/ui/SettingsComponent.cs:162` — the corrupt/missing-file early-return
  skips `ApplyAudioSettings`/`ApplyDisplaySettings`, so on fresh install/corrupt config the engine is
  never pushed the default field values shown in the UI. Apply defaults on the early-return.
- **GameOver / LevelComplete "(Best!)" on tie** — `ecs/scenes/GameOver.cs:18`,
  `ecs/scenes/puzzle/LevelComplete.cs:21` — `>=` shows "best/high score" on an exact tie, and GameOver
  is a loss screen where `BestScore` isn't updated. Use `>` and/or gate on a win. Cosmetic.
- **ThemePresetComponent** — `ecs/ui/ThemePresetComponent.cs:273` — `ApplyButtonOverrides(this, preset)`
  is redundant (`this` is already a descendant of `root`, already visited). Drop the second call.

## Gotchas

- `IGameStateable.cs` → `ISaveable.cs`: rename the `.cs` **and** its `.uid` together, and update any
  `docs/` references. Godot uses the `.uid`, so keep it; git tracks the rename.
- `base._ExitTree()`: add it at the **right point** (usually the end of the override), and don't
  double-free — the base only handles group removal, not the child/tween cleanup the override does.
- `ScreenShakeComponent` group removal: only remove from `screen_shake` if this component added
  itself (it does, in `_Ready`).

## Verify

1. Build + validator.
2. Grep gate: `grep -L "base._ExitTree" ` across the listed files → empty (all chain up).
3. `IGameStateable.cs` gone, `ISaveable.cs` present with its `.uid`, build clean.
4. `ByName(null)` returns a default instead of throwing.
