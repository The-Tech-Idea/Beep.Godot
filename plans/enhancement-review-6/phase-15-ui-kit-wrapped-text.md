# Phase 15 - UI Kit Wrapped Text

## Status

Fixed.

## Finding

Several text-heavy kit controls used one-line fitting or private word wrapping. That made long strings shrink too far, ignored explicit newlines in some paths, and handled long unbroken words inconsistently. The affected surfaces are common user-facing UI: dialog bodies, tooltips, toasts, speech bubbles, and item-card descriptions.

## Fix

- Added `KitChrome.WrapLines` for paragraph-aware wrapping.
- Added `KitChrome.DrawWrappedText` for bounded multi-line rendering with optional ellipsis.
- Routed `KitDialogBox`, `KitSpeechBubble`, `KitTooltip`, `KitToast`, and `KitItemCard` descriptions through the shared helper.
- Made `KitToast.Message` a multiline export so authored toast text matches the rendering path.

## Verification

- `tests/addon_contract_scan.ps1` checks that the shared helper exists and covered controls use it.
- `tests/run_addon_checks.ps1` passes source contracts, clean C# build, Godot headless runtime smoke, and Godot headless editor startup smoke.

## Remaining Visual Risk

Manual visual review is still needed for localized strings and very narrow containers, because the automated gate verifies code paths and runtime load, not final typography across every scene size.
