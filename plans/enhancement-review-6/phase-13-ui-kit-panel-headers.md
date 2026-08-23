# Phase 13 - UI Kit Panel Header Revision

## Status

Fixed.

## Finding

`KitPanel`, `KitPanelContainer`, and `KitCollapsiblePanel` did not share panel title chrome. `KitPanelContainer` carried its own banner and utility-strip renderer, `KitPanel` used the older `DrawBanner` helper, and `KitCollapsiblePanel` drew plain centered text directly on the panel body. That made panel headers visually inconsistent and made future bug fixes likely to land in only one code path.

## Fix

- Added `KitPanelHeaderStyle` with `Banner`, `UtilityStrip`, and `None`.
- Added shared `KitChrome.PanelHeaderRoom`, `KitChrome.PanelHeaderOverhang`, `KitChrome.PanelHeaderShape`, and `KitChrome.DrawPanelHeader`.
- Kept `KitChrome.DrawBanner` as a compatibility wrapper around the shared header renderer.
- Routed `KitPanel`, `KitPanelContainer`, and `KitCollapsiblePanel` through the shared renderer.
- Gave `KitCollapsiblePanel` exported header controls and defaulted it to `UtilityStrip` so the title does not compete with the edge handle.
- Added a contract-scan guard so panel classes keep using the shared header path.

## Verification

- `tests/addon_contract_scan.ps1` checks the shared header API and panel call sites.
- `tests/run_addon_checks.ps1` passes source contracts, clean C# build, Godot headless runtime smoke, and Godot headless editor startup smoke.

## Remaining Visual Risk

Manual editor review is still needed across all 10 genres and both light/dark theme families to judge final header proportions, especially `KitCollapsiblePanel` in `Banner` mode near top-edge handles.
