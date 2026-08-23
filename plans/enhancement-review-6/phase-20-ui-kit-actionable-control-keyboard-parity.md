# Phase 20 - UI Kit Actionable Control Keyboard Parity

## Status

Fixed.

## Finding

A targeted scan for mouse-handled kit controls still found actionable widgets without keyboard parity:

- `KitInventorySlot`
- `KitRemovableChip`
- `KitTabStrip`
- `KitKnob`
- `KitBookSpread`
- `KitSpinWheel`

The remaining mouse-handled files after this pass are passive surfaces: `KitPanel`, `KitModalShade`, and `KitRadarChart`.

## Fix

- Added focus opt-in and visible focus rings to the actionable controls.
- Added Enter/Space activation for inventory slots and spin wheels.
- Added Delete/Backspace removal for removable chips.
- Added arrow/Home/End tab navigation for tab strips.
- Added arrow/Home/End value changes for knobs.
- Added arrow/Home/End page navigation for book spreads.
- Added mouse `GrabFocus()` so clicked controls become the active keyboard target.
- Added `_GetMinimumSize()` overrides to controls whose custom drawing owns the geometry.

## Verification

- `tests/addon_contract_scan.ps1` now includes these controls in the focus/keyboard/focus-ring gate.
- `tests/addon_contract_scan.ps1` now includes the newly sized custom controls in the `_GetMinimumSize()` gate.
