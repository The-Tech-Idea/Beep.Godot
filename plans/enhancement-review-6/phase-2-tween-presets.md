# Phase 2: Tween Presets

## Why

`addons/beep_game_builder_cs/ecs/TweenComponent.cs:14` exposes 22 enum values. Only 11 switch cases are implemented, starting at `TweenComponent.cs:55`; the rest hit `default` at `TweenComponent.cs:115` and warn at `TweenComponent.cs:119`.

Status: fixed.

## Work

- Updated the summary comment at `TweenComponent.cs:8` so it no longer claims "90+" presets.
- Implemented concrete cases for:
  - `SlideOut`
  - `BounceOut`
  - `ScaleUp`
  - `ScaleDown`
  - `RotateIn`
  - `RotateOut`
  - `Flip`
  - `Float`
  - `SpriteStretch`
  - `TeleportIn`
  - `FlipCard`
- Preserved the current Control-vs-Node2D split using `offset_transform_*` for `Control` targets.

## Gotchas

- Control nodes inside containers should not animate raw `position` or `scale`.
- Perpetual presets such as `Float` should document that `TweenFinished` will not fire until stopped.
- `SpriteStretch` and `TeleportIn` need explicit product decisions because the current code gives no behavior contract.

## Verify

- Static enum/case check reports 22 enum values and 22 switch cases.
- `dotnet build .\Beep.Godot.csproj` completes with 0 errors.
- Runtime visual verification in a Godot scene is still recommended for tuning distances, easing, and flip direction.
