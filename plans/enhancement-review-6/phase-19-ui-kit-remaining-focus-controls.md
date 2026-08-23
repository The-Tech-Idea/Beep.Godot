# Phase 19 - UI Kit Remaining Focus Controls

## Status

Fixed.

## Finding

The earlier keyboard/focus pass covered the most common custom controls, but three interactive controls were still mouse-only:

- `KitTree`
- `KitLevelPath`
- `KitSegmentedIconGroup`

Custom `Control` nodes that can be clicked should also opt into focus, support keyboard/controller navigation, and draw an obvious focus state.

## Fix

- Added `FocusMode = FocusModeEnum.All` to all three controls.
- Added arrow/Home/End navigation using `KitChrome.DirectionFromKey()`.
- Added Enter/Space activation using `KitChrome.IsConfirmKey()`.
- Added mouse `GrabFocus()` on activation.
- Added shared focus-ring drawing through `KitChrome.DrawFocusRing()`.
- Added `_GetMinimumSize()` to `KitSegmentedIconGroup`.
- Centralized activation paths for tree and level path nodes so mouse and keyboard behavior stay aligned.

## Verification

- `tests/addon_contract_scan.ps1` now includes these controls in the focus/keyboard/focus-ring gate.
- `tests/addon_contract_scan.ps1` now rejects `KitSegmentedIconGroup` if it loses `_GetMinimumSize()`.
