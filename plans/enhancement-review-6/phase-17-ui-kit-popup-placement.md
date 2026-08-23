# Phase 17 - UI Kit Popup Placement

## Status

Fixed.

## Finding

`KitContextMenu.PopupAt()` used the requested global position directly. Near the right or bottom viewport edge, the menu could render partly off-screen, which is not acceptable popup behavior for game UI or editor-facing tools.

## Fix

- Added `ClampedPopupPosition()` to calculate a viewport-safe global position.
- Used `Viewport.GetVisibleRect()` and a small margin before assigning `GlobalPosition`.
- Kept focus grab, keyboard navigation, Enter/Space activation, and Escape dismissal from Phase 16.

## Verification

- `tests/addon_contract_scan.ps1` checks that context menus clamp with `GetVisibleRect()` and do not assign raw requested popup position.
- `tests/run_addon_checks.ps1` passes source contracts, clean C# build, Godot headless runtime smoke, and Godot headless editor startup smoke.

## Remaining Visual Risk

Manual review should still check whether clamping is preferable to directional flipping for menus opened from controls near the right or bottom edge.
