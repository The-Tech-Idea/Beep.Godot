# Phase 14 - UI Kit Focus And Keyboard Navigation

## Status

Fixed.

## Finding

Several custom UI-kit controls were visually interactive but only handled mouse input. That made them weaker than the drop-in controls that inherit Godot widgets, because users navigating with keyboard/controller focus could not reliably activate cards, selectors, grids, choices, pagers, sockets, rows, collapsible panels, or star ratings.

## Fix

- Added shared `KitChrome.IsConfirmKey`, `KitChrome.DirectionFromKey`, and `KitChrome.DrawFocusRing`.
- Added `FocusModeEnum.All` to covered custom interactive controls.
- Added Enter/Space activation to button-like custom controls.
- Added arrow/Home/End style navigation to selector, pager, slot grid, dialog choices, and star rating.
- Added `GrabFocus()` on mouse activation so mixed mouse/keyboard use has a stable focus target.
- Added visible focus rings that use the active skin's semantic accent/info color.

## Covered Controls

- `KitArrowSelector`
- `KitCollapsiblePanel`
- `KitDialogBox`
- `KitGemSlot`
- `KitItemCard`
- `KitLevelButton`
- `KitNodeCard`
- `KitPager`
- `KitRow`
- `KitSlotGrid`
- `KitStarRating`

## Verification

- `tests/addon_contract_scan.ps1` checks the shared focus helpers and covered custom control call sites.
- `tests/run_addon_checks.ps1` passes source contracts, clean C# build, Godot headless runtime smoke, and Godot headless editor startup smoke.

## Remaining Visual Risk

Manual controller/keyboard traversal should still be checked in real menus, because source-level focus support does not prove every scene has the desired tab order or focus neighbor graph.
