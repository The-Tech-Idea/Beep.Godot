# Phase 16 - UI Kit Godot Sizing And Popup Behavior

## Status

Fixed.

## Finding

Several custom controls relied on one-time `CustomMinimumSize` assignment in `_Ready()`. That is weaker than Godot's container contract because minimum size should be queryable from the current theme/font metrics, especially after theme changes. `KitContextMenu` also behaved like a drawn mouse-only panel instead of a keyboard-friendly popup.

## Fix

- Added `_GetMinimumSize()` to revised custom controls so containers can query dynamic sizes.
- Kept existing `CustomMinimumSize` assignments as compatibility defaults for authored scenes.
- Updated `KitContextMenu` to opt into focus, grab focus on popup, select with arrow keys and Enter/Space, close with Escape, and size from `_GetMinimumSize()`.
- Added source contract checks for dynamic minimum sizing and context-menu keyboard behavior.

## Verification

- `tests/addon_contract_scan.ps1` checks dynamic minimum sizing and context-menu focus/key behavior.
- `tests/run_addon_checks.ps1` passes source contracts, clean C# build, Godot headless runtime smoke, and Godot headless editor startup smoke.

## Remaining Visual Risk

Popup placement near viewport edges still needs a visual/editor pass. `KitContextMenu.PopupAt()` currently uses the requested global position directly; a future phase should clamp or flip placement against the viewport bounds.
