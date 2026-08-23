# Phase 21 - UI Kit Reusable Minimum Size Contracts

## Status

Fixed.

## Finding

Several reusable UI-kit controls still assigned `CustomMinimumSize` once in `_Ready()` without a matching `_GetMinimumSize()` override. That works for the initial scene tree, but it is brittle in Godot containers because theme/font changes and inspector-driven layout edits need a current minimum-size contract, not just a one-time value.

The remaining generated-child exception is `KitArchetypes.cs`, which assigns fixed sizes to ornament children it creates. It is not itself a reusable control class.

## Fix

- Added `_GetMinimumSize()` overrides to the remaining reusable kit controls that set `CustomMinimumSize`.
- Added `UpdateMinimumSize()` to size-affecting setters for `KitHeartRow`, `KitChip`, `KitSpinner`, and `KitToggle`.
- Moved editable `KitRadarChart` into the focus/keyboard/focus-ring gate because it accepts pointer edits and should not be mouse-only.

## Verification

- `tests/addon_contract_scan.ps1` now scans the kit folder and rejects reusable controls that set `CustomMinimumSize` without `_GetMinimumSize()`.
- `tests/addon_contract_scan.ps1` now checks the new size-affecting setters for `UpdateMinimumSize()`.
- `tests/addon_contract_scan.ps1` now includes `KitRadarChart` in the actionable-control keyboard/focus gate.
